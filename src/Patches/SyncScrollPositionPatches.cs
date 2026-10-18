using System.Reflection;
using EFT.UI;
using EFT.UI.Ragfair;
using HarmonyLib;
using SPTushonka.Reflection.Patching;
using UnityEngine;
using UnityEngine.UI;

namespace UIFixes;

public static class SyncScrollPositionPatches
{
    private static float StashScrollPosition = 1f;

    public static void Enable()
    {
        new SyncStashScrollPatch().SafeEnable();
        new SyncOfferStashScrollPatch().SafeEnable();
    }

    private static void UpdateScrollPosition(Vector2 position)
    {
        StashScrollPosition = position.y;
    }

    private static void SynchronizeScrollRect(UIInputNode element, ScrollRect scrollRect = null)
    {
        if (!Settings.SynchronizeStashScrolling.Value || element == null || (scrollRect ??= element.GetComponentInChildren<ScrollRect>()) == null)
        {
            return;
        }

        scrollRect.onValueChanged.RemoveListener((System.Action<UnityEngine.Vector2>)(UpdateScrollPosition));

        scrollRect.WaitForEndOfFrame((System.Action)(() =>
        {
            scrollRect.verticalNormalizedPosition = StashScrollPosition;
            scrollRect.onValueChanged.AddListener((System.Action<UnityEngine.Vector2>)(UpdateScrollPosition));
        }));
    }

    public class SyncStashScrollPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(SimpleStashPanel), nameof(SimpleStashPanel.Show));
        }

        [PatchPostfix]
        public static void Postfix(SimpleStashPanel __instance)
        {
            SynchronizeScrollRect(__instance);
        }
    }

    public class SyncOfferStashScrollPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.DeclaredMethod(typeof(AddOfferWindow), nameof(AddOfferWindow.Show));
        }

        [PatchPostfix]
        public static void Postfix(AddOfferWindow __instance)
        {
            SynchronizeScrollRect(__instance);
        }
    }
}