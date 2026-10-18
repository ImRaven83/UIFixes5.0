using System;
using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.Utilities;
using HarmonyLib;
using SPTushonka.Reflection.Patching;
using TMPro;
using UnityEngine;

namespace UIFixes;

public static class StashSearchPatches
{
    private static string Query = null;

    private static ToggleEFT SearchButton = null;

    public static void Enable()
    {
        new FocusStashSearchPatch().SafeEnable();
        new OpenSearchPatch().SafeEnable();

        new AddSearchStashPatch().SafeEnable();
        new PositionSearchStashPatch().SafeEnable();
    }

    public class FocusStashSearchPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.DeclaredMethod(typeof(StashSearchWindow), nameof(StashSearchWindow.Show));
        }

        [PatchPostfix]
        public static void Postfix(StashSearchWindow __instance)
        {
            __instance._searchField.GetOrAddComponent<SearchKeyListener>();

            if (!string.IsNullOrEmpty(Query))
            {
                __instance._searchField.text = Query;
                Query = null;
            }

            __instance._searchField.ActivateInputField();
            __instance._searchField.Select();
        }
    }

    public class OpenSearchPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(SimpleStashPanel), nameof(SimpleStashPanel.Show));
        }

        [PatchPostfix]
        public static void Postfix(SimpleStashPanel __instance)
        {
            if (__instance._searchTab == null || Plugin.InRaid())
            {
                return;
            }

            SearchButton = __instance._searchTab;

            var listener = __instance._searchTab.GetOrAddComponent<SearchKeyListener>();
            listener.Init(() =>
            {
                if (Settings.StashSearchContextMenu.Value)
                {
                    SetQuery();
                }

                OpenSearch();
            });
        }

        private static void SetQuery()
        {
            if (!Settings.ItemContextBlocksTextInputs.Value && Plugin.TextboxActive())
            {
                return;
            }

            // Item under cursor
            ItemContext itemContext = ItemUiContext.Instance.CurrentItemContext;

            // Item being dragged
            DragItemContext dragItemContext = ItemUiContext.Instance.R().DragItemContext;

            // Only do anything if the mouse is over an item and nothing is being dragged
            if (itemContext == null || dragItemContext != null)
            {
                return;
            }

            Query = itemContext.Item.Name.Localized();
        }
    }

    private static void OpenSearch()
    {
        if (SearchButton != null && !SearchButton.IsOn)
        {
            // Yes, this is the only way to invoke the button without causing issues. BSG never ceases to impress.
            SearchButton.SetHover(true);
            SearchButton.SetClick(true);
            SearchButton.SetClick(false);
            SearchButton.SetHover(false);
        }
    }

    public class AddSearchStashPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ItemUiContext), nameof(ItemUiContext.GetItemContextInteractions));
        }

        [PatchPostfix]
        private static void Prefix(ItemContext itemContext, ContextInteractions<EItemInfoButton> __result)
        {
            if (!Settings.StashSearchContextMenu.Value || Plugin.InRaid())
            {
                return;
            }

            // Not that
            if (__result.GetType().FullName == "UIFixes.EmptySlotMenu")
            {
                return;
            }

            if (itemContext.ViewType != EItemViewType.Inventory && itemContext.ViewType != EItemViewType.TradingPlayer)
            {
                return;
            }

            var item = itemContext.Item;
            if (item == null)
            {
                return;
            }

            var text = "UI/SearchWindow/Tooltip/SearchInStash".Localized(EFT.EStringCase.Upper);
            __result._dynamicInteractions["StashSearch"] = new(
                "StashSearch",
                text,
                (System.Action)(() =>
                {
                    Query = item.Name.Localized();
                    OpenSearch();
                }),
                ResourcesCache.Pop<Sprite>("Characteristics/Icons/FilterSearch"),
                null,
                null);
        }
    }

    public class PositionSearchStashPatch : ModulePatch
    {
        private static Transform TargetSibling = null;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(InteractionButtonsContainer),
                nameof(InteractionButtonsContainer.CreateContextButton),
                [typeof(string), typeof(string), typeof(SimpleContextMenuButton), typeof(RectTransform), typeof(Sprite), typeof(Il2CppSystem.Action), typeof(Il2CppSystem.Action), typeof(bool), typeof(bool)]);
        }

        [PatchPostfix]
        public static void Postfix(string key, SimpleContextMenuButton __result)
        {
            if (Plugin.InRaid())
            {
                return;
            }

            if (key == EItemInfoButton.NeededSearch.ToString())
            {
                TargetSibling = __result.Transform;
            }

            // Dynamic actions use the localized string for the key, because BSG
            var text = "UI/SearchWindow/Tooltip/SearchInStash".Localized(EStringCase.Upper);
            if (key != text)
            {
                return;
            }

            if (TargetSibling != null)
            {
                __result.Transform.SetSiblingIndex(TargetSibling.GetSiblingIndex() + 1);
                TargetSibling = null;
            }
        }
    }
}