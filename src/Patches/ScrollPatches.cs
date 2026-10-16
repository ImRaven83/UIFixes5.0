using System;
using System.Collections.Generic;
using System.Reflection;
using EFT.Hideout;
using EFT.Quests;
using EFT.UI;
using EFT.UI.Chat;
using EFT.UI.Ragfair;
using EFT.UI.Utilities.LightScroller;
using HarmonyLib;
using SPTushonka.Reflection.Patching;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UIFixes;

public static class ScrollPatches
{
    public static void Enable()
    {
        new EnhanceStashScrollingPatch().Enable();
        new EnchanceTraderStashScrollingPatch().Enable();
        new EnhanceFleaScrollingPatch().Enable();
        new EnhanceMailScrollingPatch().Enable();

        new MouseScrollingSpeedPatch().Enable();
        new LightScrollerSpeedPatch().Enable();

        new EnhanceHideoutScrollingPatch().Enable();
        new EnhanceTaskListScrollingPatch().Enable();
        new OpenLastTaskPatch().Enable();
    }

    private static bool HandleInput(ScrollRect scrollRect)
    {
        if (Plugin.TextboxActive())
        {
            return false;
        }

        if (scrollRect != null)
        {
            if (Settings.UseHomeEnd.Value)
            {
                if (Input.GetKeyDown(KeyCode.Home))
                {
                    scrollRect.verticalNormalizedPosition = 1f;
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.End))
                {
                    scrollRect.verticalNormalizedPosition = 0f;
                    return true;
                }
            }

            if (Settings.RebindPageUpDown.Value)
            {
                if (Input.GetKeyDown(KeyCode.PageUp))
                {
                    // Duplicate this code to avoid running it every frame
                    Rect contentRect = scrollRect.content.rect;
                    Rect viewRect = scrollRect.RectTransform().rect;
                    float pageSize = viewRect.height / contentRect.height;


                    scrollRect.verticalNormalizedPosition = Math.Min(1f, scrollRect.verticalNormalizedPosition + pageSize);
                    return true;
                }

                if (Input.GetKeyDown(KeyCode.PageDown))
                {
                    // Duplicate this code to avoid running it every frame
                    Rect contentRect = scrollRect.content.rect;
                    Rect viewRect = scrollRect.RectTransform().rect;
                    float pageSize = viewRect.height / contentRect.height;


                    scrollRect.verticalNormalizedPosition = Math.Max(0f, scrollRect.verticalNormalizedPosition - pageSize);
                    return true;
                }
            }
        }

        return false;
    }

    // LightScrollers don't expose heights that I can see, so just fudge it with fake OnScroll events
    private static bool HandleInput(LightScroller lightScroller)
    {
        if (lightScroller != null)
        {
            if (Settings.UseHomeEnd.Value)
            {
                if (Input.GetKeyDown(KeyCode.Home))
                {
                    lightScroller.SetScrollPosition(lightScroller._order == LightScroller.EScrollOrder.Straight ? 0f : 1f);
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.End))
                {
                    lightScroller.SetScrollPosition(lightScroller._order == LightScroller.EScrollOrder.Straight ? 1f : 0f);
                    return true;
                }
            }

            if (Settings.RebindPageUpDown.Value)
            {
                if (Input.GetKeyDown(KeyCode.PageUp))
                {
                    var eventData = new PointerEventData(EventSystem.current)
                    {
                        scrollDelta = new Vector2(0f, 25f)
                    };
                    lightScroller.OnScroll(eventData);
                    return true;
                }
                if (Input.GetKeyDown(KeyCode.PageDown))
                {
                    var eventData = new PointerEventData(EventSystem.current)
                    {
                        scrollDelta = new Vector2(0f, -25f)
                    };
                    lightScroller.OnScroll(eventData);
                    return true;
                }
            }
        }

        return false;
    }

    // Transpilers can't edit il2cpp method bodies, so instead of removing BSG's PageUp/PageDown handling,
    // remember the scroll position before their Update and put it back afterwards
    private static bool PageKeyDown()
    {
        return Settings.RebindPageUpDown.Value && !Plugin.TextboxActive() && (Input.GetKeyDown(KeyCode.PageUp) || Input.GetKeyDown(KeyCode.PageDown));
    }

    private static float? CaptureScroll(ScrollRect scrollRect)
    {
        return scrollRect != null && PageKeyDown() ? scrollRect.verticalNormalizedPosition : null;
    }

    private static float? CaptureScroll(LightScroller lightScroller)
    {
        return lightScroller != null && PageKeyDown() ? lightScroller.NormalizedScrollPosition : null;
    }

    private static void RestoreScroll(ScrollRect scrollRect, float? position)
    {
        if (scrollRect != null && position.HasValue)
        {
            scrollRect.verticalNormalizedPosition = position.Value;
            HandleInput(scrollRect);
        }
    }

    private static void RestoreScroll(LightScroller lightScroller, float? position)
    {
        if (lightScroller != null && position.HasValue)
        {
            lightScroller.SetScrollPosition(position.Value);
            HandleInput(lightScroller);
        }
    }

    public class KeyScrollListener : MonoBehaviour
    {
        public KeyScrollListener(System.IntPtr pointer) : base(pointer) { }

        private ScrollRect _scrollRect;

        public UnityEvent OnKeyScroll;

        public void Awake()
        {
            _scrollRect = GetComponent<ScrollRect>();
            OnKeyScroll = new();
        }

        public void Update()
        {
            if (HandleInput(_scrollRect))
            {
                OnKeyScroll.Invoke();
            }
        }
    }

    // Improve scrolling on stashes
    public class EnhanceStashScrollingPatch : ModulePatch
    {
        // SimpleStashPanel no longer has an Update method in 5.0, so drive the key handling from a component on its scroll rect
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.DeclaredMethod(typeof(SimpleStashPanel), nameof(SimpleStashPanel.Show));
        }

        [PatchPostfix]
        public static void Postfix(SimpleStashPanel __instance)
        {
            // Ignore Trading screen, that is handled separately
            if (__instance._itemUiContext?.ContextType == EItemUiContextType.TraderScreen)
            {
                return;
            }

            // For some reason, sometimes SimpleStashPanel doesn't have a reference to its own ScrollRect?
            var scrollRect = __instance._stashScroll ?? __instance.GetComponentInChildren<ScrollRect>();
            scrollRect?.GetOrAddComponent<KeyScrollListener>();
        }
    }

    public class EnchanceTraderStashScrollingPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(TraderDealScreen), nameof(TraderDealScreen.Update));
        }

        [PatchPrefix]
        public static void Prefix(TraderDealScreen __instance, ref float? __state)
        {
            var scrollRect = __instance.TradeMode == ETradeMode.Purchase ? __instance._traderScroll : __instance._stashScroll;
            __state = CaptureScroll(scrollRect);
            if (!__state.HasValue)
            {
                HandleInput(scrollRect);
            }
        }

        [PatchPostfix]
        public static void Postfix(TraderDealScreen __instance, float? __state)
        {
            RestoreScroll(__instance.TradeMode == ETradeMode.Purchase ? __instance._traderScroll : __instance._stashScroll, __state);
        }
    }

    public class EnhanceFleaScrollingPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(OfferViewList), nameof(OfferViewList.Update));
        }

        [PatchPrefix]
        public static void Prefix(OfferViewList __instance, ref float? __state)
        {
            __state = CaptureScroll(__instance._scroller);
            if (!__state.HasValue)
            {
                HandleInput(__instance._scroller);
            }
        }

        [PatchPostfix]
        public static void Postfix(OfferViewList __instance, float? __state)
        {
            RestoreScroll(__instance._scroller, __state);
        }
    }

    public class EnhanceHideoutScrollingPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(AreaScreenSubstrate), nameof(AreaScreenSubstrate.Awake));
        }

        [PatchPostfix]
        public static void Postfix(AreaScreenSubstrate __instance)
        {
            ScrollRect scrollRect = __instance.transform.Find("Content/CurrentLevel/CurrentContainer/Scrollview")?.GetComponent<ScrollRect>();
            if (scrollRect == null)
            {
                return;
            }

            scrollRect.GetOrAddComponent<KeyScrollListener>();
        }
    }

    public class EnhanceMailScrollingPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(MessagesContainer), nameof(MessagesContainer.Update));
        }

        [PatchPrefix]
        public static void Prefix(MessagesContainer __instance, ref float? __state)
        {
            __state = CaptureScroll(__instance._scroller);
            if (!__state.HasValue)
            {
                HandleInput(__instance._scroller);
            }
        }

        [PatchPostfix]
        public static void Postfix(MessagesContainer __instance, float? __state)
        {
            RestoreScroll(__instance._scroller, __state);
        }
    }

    public class MouseScrollingSpeedPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ScrollRectNoDrag), nameof(ScrollRectNoDrag.OnScroll));
        }

        [PatchPrefix]
        public static void Prefix(PointerEventData data)
        {
            int multi = Settings.UseRaidMouseScrollMulti.Value && Plugin.InRaid() ? Settings.MouseScrollMultiInRaid.Value : Settings.MouseScrollMulti.Value;
            data.scrollDelta *= multi;
        }
    }

    public class LightScrollerSpeedPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(LightScroller), nameof(LightScroller.Scroll));
        }

        [PatchPrefix]
        public static void Prefix(ref float deltaPixels)
        {
            int multi = Settings.UseRaidMouseScrollMulti.Value && Plugin.InRaid() ? Settings.MouseScrollMultiInRaid.Value : Settings.MouseScrollMulti.Value;
            deltaPixels *= multi;
        }
    }

    public class EnhanceTaskListScrollingPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.DeclaredMethod(typeof(TasksPanel), nameof(TasksPanel.Show));
        }

        [PatchPostfix]
        public static void Postfix(TasksPanel __instance)
        {
            var keyScroller = __instance._scrollRect.GetOrAddComponent<KeyScroller>();
            keyScroller.Init(__instance._scrollRect);
        }
    }

    public class KeyScroller : MonoBehaviour
    {
        public KeyScroller(System.IntPtr pointer) : base(pointer) { }

        ScrollRect _scrollRect;

        public void Init(ScrollRect scrollRect)
        {
            _scrollRect = scrollRect;
        }

        public void Update()
        {
            HandleInput(_scrollRect);
        }
    }

    public class OpenLastTaskPatch : ModulePatch
    {
        private static string LastQuestId = null;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(NotesTask), nameof(NotesTask.Show));
        }

        [PatchPostfix]
        public static void Postfix(NotesTask __instance, Quest quest)
        {
            void OnTaskSelected(bool open)
            {
                LastQuestId = open ? quest.Id : null;
            }

            Toggle toggle = __instance.GetComponent<Toggle>();
            toggle.onValueChanged.AddListener((System.Action<bool>)(OnTaskSelected));
            __instance.R().UI.AddDisposable(() => toggle.onValueChanged.RemoveListener((System.Action<bool>)(OnTaskSelected)));

            if (quest.Id == LastQuestId)
            {
                toggle.isOn = true;
            }
        }
    }
}