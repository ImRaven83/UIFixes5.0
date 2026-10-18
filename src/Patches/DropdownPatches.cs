using System;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using Diz.LanguageExtensions;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using EFT.UI.WeaponModding;
using HarmonyLib;
using SPTushonka.Reflection.Patching;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UIFixes;

public static class DropdownPatches
{
    public static ModdingScreenSlotView OpenSlotView = null;

    public static void Enable()
    {
        new ModdingScreenListenOpenPatch().SafeEnable();
        new ModdingScreenListenClosePatch().SafeEnable();

        new SlotViewClickOpenPatch().SafeEnable();
        new ItemSelectionCellClickOpenPatch().SafeEnable();

        new EmptySlotRemovePointerDownPatch().SafeEnable();
        new EmptySlotInteractablePatch().SafeEnable();
        new EmptySlotClickPatch().SafeEnable();

        new BackgroundClickClosePatch().SafeEnable();

        new FixBlockedItemSwapPatch().SafeEnable();
    }

    public class ModdingScreenListenOpenPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ModdingScreenSlotView), nameof(ModdingScreenSlotView.MenuOpenHandler));
        }

        [PatchPostfix]
        public static void Postfix(ModdingScreenSlotView __instance, ModdingScreenSlotView slotView)
        {
            if (__instance == slotView)
            {
                OpenSlotView = __instance;
            }
        }
    }

    public class ModdingScreenListenClosePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ModdingScreenSlotView), nameof(ModdingScreenSlotView.MenuClosedHandler));
        }

        [PatchPostfix]
        public static void Postfix()
        {
            OpenSlotView = null;
        }
    }

    public class BackgroundClickClosePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(CameraViewporter), nameof(CameraViewporter.OnEnable));
        }

        [PatchPostfix]
        public static void Postfix(CameraViewporter __instance)
        {
            __instance.GetOrAddComponent<ClickHandler>().Init(() =>
            {
                if (OpenSlotView != null)
                {
                    Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.ButtonOver);
                    OpenSlotView.DropDownButtonClickHandler();
                }
            });
        }
    }

    public class SlotViewClickOpenPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.DeclaredMethod(typeof(ModdingSelectableItemView), nameof(ModdingSelectableItemView.OnClick));
        }

        [PatchPostfix]
        public static void Postfix(ModdingSelectableItemView __instance, PointerEventData.InputButton button, bool doubleClick)
        {
            if (button == PointerEventData.InputButton.Left && !doubleClick)
            {
                var parentView = __instance.GetComponentInParent<ModdingScreenSlotView>();
                if (parentView == null)
                {
                    return;
                }

                Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.ButtonOver);
                parentView.DropDownButtonClickHandler();
            }
        }
    }

    public class ItemSelectionCellClickOpenPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ItemSelectionCell), nameof(ItemSelectionCell.Awake));
        }

        [PatchPostfix]
        public static void Postfix(ItemSelectionCell __instance)
        {
            __instance.GetOrAddComponent<ClickHandler>().Init(() =>
            {
                Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.ButtonOver);
                __instance.DropdownButtonClickHandler();
            });
        }
    }

    // BSG uses PointerDown() here but every other choice is on click
    public class EmptySlotRemovePointerDownPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // Fully qualified because declared with the interface in the name
            return AccessTools.Method(typeof(EmptyItemView), "UnityEngine.EventSystems.IPointerDownHandler.OnPointerDown");
        }

        [PatchPrefix]
        public static bool Prefix()
        {
            return false;
        }
    }

    public class EmptySlotInteractablePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.PropertySetter(typeof(EmptyItemView), nameof(EmptyItemView.Interactable));
        }

        [PatchPrefix]
        public static bool Prefix(EmptyItemView __instance, bool value)
        {
            __instance._interactable = value;
            return false;
        }
    }

    public class EmptySlotClickPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.DeclaredMethod(typeof(EmptyItemView), nameof(EmptyItemView.Show));
        }

        [PatchPostfix]
        public static void Postfix(EmptyItemView __instance)
        {
            // initialize _interactable since BSG can't be bothered to
            __instance._interactable = true;

            __instance.GetOrAddComponent<ClickHandler>().Init(() =>
            {
                if (__instance.Interactable) // normal behavior
                {
                    __instance._itemContext?.ToggleSelection();
                }
                else // Patched behavior
                {
                    var parentView = __instance.GetComponentInParent<ModdingScreenSlotView>();
                    if (parentView == null)
                    {
                        return;
                    }

                    Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.ButtonOver);
                    parentView.DropDownButtonClickHandler();
                }
            });
        }
    }

    // BSG forgets to rollback the removeOperation below if the addOperation fails. Reimplement method to fix.
    public class FixBlockedItemSwapPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.DeclaredMethod(typeof(EditBuildManipulation), nameof(EditBuildManipulation.Select));
        }

        [PatchPrefix]
        public static bool Prefix(EditBuildManipulation __instance, Item item, Slot slot, bool simulate, ref Error error, ref bool __result)
        {
            if (item?.TemplateId == slot.ContainedItem?.TemplateId)
            {
                error = null;
                __result = true;
                return false;
            }

            OperationResult removeOperation = default;
            if (slot.ContainedItem != null)
            {
                removeOperation = ItemManipulator.Remove(slot.ContainedItem, __instance.InventoryController, item == null && simulate);
                error = removeOperation.Error;
                __result = removeOperation.Succeeded;
                if (removeOperation.Failed)
                {
                    return false;
                }
            }

            OperationResult<AddResult> addOperation = default;
            if (item != null)
            {
                addOperation = ItemManipulator.Add(
                    item.CloneItem(__instance.InventoryController),
                    slot.CreateItemAddress(),
                    __instance.InventoryController,
                    simulate);
                error = addOperation.Error;
                __result = addOperation.Succeeded;
            }

            if (simulate || addOperation.Failed)
            {
                removeOperation.Value?.RollBack();
            }

            return false;
        }
    }

    private class ClickHandler : MonoBehaviour, IPointerClickHandler
    {
        public ClickHandler(System.IntPtr pointer) : base(pointer) { }

        private Action _onClick;

        public void Init(Action onClick)
        {
            _onClick = onClick;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                _onClick();
            }
        }
    }
}