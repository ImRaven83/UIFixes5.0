using Il2CppInterop.Runtime.InteropTypes.Arrays;
using SPTushonka.Reflection.Il2Cpp;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EFT.Builds;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using SPTushonka.Reflection.Patching;
using SPTushonka.Reflection.Utils;
using UnityEngine;

namespace UIFixes;

public static class LoadMultipleMagazinesPatches
{
    private static ItemFilter[] CombinedFilters;

    public static void Enable()
    {
        new FindCompatibleAmmoPatch().SafeEnable();
        new CheckItemFilterPatch().SafeEnable();
        new LoadAmmoPatch().SafeEnable();
        new FilterMagPresetsPatch().SafeEnable();
        new LoadPresetPatch().SafeEnable();
    }

    public class FindCompatibleAmmoPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ItemUiContext), nameof(ItemUiContext.FindCompatibleAmmo), [typeof(Magazine)]);
        }

        [PatchPrefix]
        public static void Prefix()
        {
            if (MultiSelect.Active)
            {
                CombinedFilters = MultiSelect.SortedItemContexts()
                    .Select(itemContext => itemContext.Item)
                    .OfType<Magazine>()
                    .SelectMany(mag => mag.Cartridges.Filters)
                    .ToArray();
            }
        }

        [PatchPostfix]
        public static void Postfix()
        {
            CombinedFilters = null;
        }
    }

    public class CheckItemFilterPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ItemFilterExtension), nameof(ItemFilterExtension.CheckItemFilter));
        }

        [PatchPrefix]
        public static void Prefix(ref Il2CppReferenceArray<ItemFilter> filters)
        {
            if (CombinedFilters == null)
            {
                return;
            }

            filters = CombinedFilters;
        }
    }

    public class LoadAmmoPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // Declared on the base class in 5.0; Harmony can only patch the declaring method
            return AccessTools.DeclaredMethod(typeof(LoadAmmoContextInteractions), nameof(LoadAmmoContextInteractions.LoadAmmo));
        }

        [PatchPrefix]
        public static bool Prefix(LoadAmmoContextInteractions __instance, string ammoTemplateId, ref Il2CppSystem.Threading.Tasks.Task __result)
        {
            if (!MultiSelect.Active || __instance is not LoadMagContextInteractions)
            {
                return true;
            }

            __result = MultiSelect.LoadAmmoAll(__instance.UIContext, ammoTemplateId, false).ToIl2Cpp();
            return false;
        }
    }

    public class FilterMagPresetsPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(MagPresetContextInteractions), nameof(MagPresetContextInteractions.IsPresetCompatible));
        }

        [PatchPrefix]
        public static void Prefix()
        {
            if (MultiSelect.Active)
            {
                CombinedFilters = MultiSelect.SortedItemContexts()
                    .Select(itemContext => itemContext.Item)
                    .OfType<Magazine>()
                    .SelectMany(mag => mag.Cartridges.Filters)
                    .ToArray();
            }
        }

        [PatchPostfix]
        public static void Postfix()
        {
            CombinedFilters = null;
        }
    }

    public class LoadPresetPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(MagPresetContextInteractions), nameof(MagPresetContextInteractions.PresetSelectHandler));
        }

        [PatchPrefix]
        public static bool Prefix(MagPresetContextInteractions __instance, MagPreset preset)
        {
            if (!MultiSelect.Active)
            {
                return true;
            }

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                return true;
            }

            var magazines = MultiSelect.SortedItemContexts().Select(itemContext => itemContext.Item).OfType<Magazine>();
            __instance._uiContext.ApplyMagPreset(preset, magazines.ToIl2CppList()).HandleExceptions();

            return false;
        }
    }
}