using System.Collections.Generic;
using System.Reflection;
using EFT;
using EFT.Hideout;
using EFT.UI;
using HarmonyLib;
using SPTushonka.Reflection.Patching;
using UnityEngine;

namespace UIFixes;

public static class RemoveAdsPatches
{
    public static void Enable()
    {
        new StashPanelAdPatch().Enable();
        new HideoutStashAdPatch().Enable();
    }

    public class StashPanelAdPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.DeclaredMethod(typeof(SimpleStashPanel), nameof(SimpleStashPanel.Show));
        }

        [PatchPostfix]
        public static void Postfix(SimpleStashPanel __instance)
        {
            if (__instance._externalObtainIcon != null)
            {
                __instance._externalObtainIcon.SetActive(false);
            }
        }
    }

    public class HideoutStashAdPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.DeclaredMethod(typeof(InteractionPanel), nameof(InteractionPanel.SetInfo));
        }

        [PatchPostfix]
        public static void Postfix(InteractionPanel __instance)
        {
            __instance._additionalInfo[EAreaType.Stash].SetActive(false);
        }
    }
}