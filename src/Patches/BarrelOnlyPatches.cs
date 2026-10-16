using SPTushonka.Reflection.Il2Cpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using SPTushonka.Reflection.Patching;

namespace UIFixes;

public static class BarrelOnlyPatches
{
    public static void Enable()
    {
        new LoadBarrelPatch().Enable();

        new LoadAmmoIsActivePatch().Enable();
        new LoadAmmoIsInteractivePatch().Enable();
        new LoadAmmoSubInteractionsPatch().Enable();
        new UnloadAmmoPatch().Enable();
    }

    public class LoadBarrelPatch : ModulePatch
    {
        public static bool InPatch = false;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.DeclaredMethod(typeof(ItemController), nameof(ItemController.LoadMultiBarrelWeapon));
        }

        [PatchPrefix]
        public static bool Prefix(ItemController __instance, Weapon weapon, Ammo ammo, int ammoCount, ref Il2CppSystem.Threading.Tasks.Task<IResult> __result)
        {
            if (InPatch || ammoCount < 1)
            {
                return true;
            }

            InPatch = true;

            if (Singleton<GUISounds>.Instantiated)
            {
                Singleton<GUISounds>.Instance.PlayUILoadSound();
            }

            // ammoCount is incoming stack size, adust to make sense
            ammoCount = Math.Min(ammoCount, weapon.FreeChamberSlotsCount);
            Task task;
            if (ammoCount > 1)
            {
                var taskSerializer = TaskSerializerBase.Create<LoadTaskSerializer>(ItemUiContext.Instance.gameObject);

                // Pass in loadCount - 1 symbolically, in reality this will only ever load 1 at a time
                task = taskSerializer.Initialize(
                    Enumerable.Range(0, ammoCount),
                    i => (__instance.LoadMultiBarrelWeapon(weapon, ammo, ammoCount - i)).AsManaged());
            }
            else
            {
                // BSG doesn't handle 1 bullet correctly, and will have already moved the stack into the weapon
                task = ammo.Parent.Container.ParentItem == weapon ? Task.CompletedTask : __instance.LoadMultiBarrelWeapon(weapon, ammo, 1).AsManaged();
            }

            __result = task.ContinueWith(t =>
            {
                InPatch = false;
                return SuccessfulResult.New;
            }).ToIl2Cpp();

            return false;
        }

        private class LoadTaskSerializer : TaskSerializer<int, IResult> { }
    }

    public class LoadAmmoIsActivePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ItemContextInteractionsSwitcher), nameof(ItemContextInteractionsSwitcher.IsActive));
        }

        [PatchPrefix]
        public static bool Prefix(ItemContextInteractionsSwitcher __instance, EItemInfoButton button, ref bool __result)
        {
            if (!Settings.LoadAmmoOnInternalMags.Value)
            {
                return true;
            }

            if (__instance.Gameplay || (button != EItemInfoButton.LoadAmmo && button != EItemInfoButton.UnloadAmmo))
            {
                return true;
            }

            if (__instance.Weapon == null || __instance.Weapon.ReloadMode != Weapon.EReloadMode.OnlyBarrel)
            {
                return true;
            }

            __result = true;
            return false;
        }
    }

    public class LoadAmmoIsInteractivePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ItemContextInteractionsSwitcher), nameof(ItemContextInteractionsSwitcher.IsInteractive));
        }

        [PatchPrefix]
        public static bool Prefix(ItemContextInteractionsSwitcher __instance, EItemInfoButton button, ref IResult __result)
        {
            if (!Settings.LoadAmmoOnInternalMags.Value)
            {
                return true;
            }

            if (button != EItemInfoButton.LoadAmmo && button != EItemInfoButton.UnloadAmmo)
            {
                return true;
            }

            if (__instance.Weapon == null || __instance.Weapon.ReloadMode != Weapon.EReloadMode.OnlyBarrel)
            {
                return true;
            }

            if (button == EItemInfoButton.LoadAmmo && __instance.Weapon.FreeChamberSlotsCount == 0)
            {
                __result = new FailedResult("InventoryError/You can't load ammo into this item", 0);
                return false;
            }

            if (button == EItemInfoButton.UnloadAmmo && __instance.Weapon.ChamberAmmoCount == 0)
            {
                __result = new FailedResult("InventoryError/You can't unload from this item", 0);
                return false;
            }

            __result = SuccessfulResult.New;
            return false;
        }
    }

    public class LoadAmmoSubInteractionsPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.DeclaredMethod(typeof(BaseInventoryItemContextInteractions), nameof(BaseInventoryItemContextInteractions.CreateSubInteractions));
        }

        [PatchPrefix]
        public static bool Prefix(BaseInventoryItemContextInteractions __instance, EItemInfoButton parentInteraction, ISubInteractionsWrapper subInteractionsWrapper)
        {
            if (parentInteraction != EItemInfoButton.LoadAmmo ||
                __instance.ItemContext.Item is not Weapon weapon ||
                weapon.ReloadMode != Weapon.EReloadMode.OnlyBarrel)
            {
                return true;
            }

            subInteractionsWrapper.SetSubInteractions(new BarrelLoadAmmoInteractions(weapon, __instance.ItemUiContext));
            return false;
        }

        // Injected into il2cpp (see Plugin.Load). The game's ContextInteractions<T> is generic and can't be subclassed from here,
        // so this derives from BaseEmptyContextInteractions and only uses dynamic interactions.
        public class BarrelLoadAmmoInteractions : BaseEmptyContextInteractions
        {
            public override bool HasIcons => false;

            private readonly bool _foundAny = false;
            private readonly Weapon _weapon;
            private readonly ItemUiContext _itemUiContext;

            public BarrelLoadAmmoInteractions(IntPtr pointer) : base(pointer) { }

            public BarrelLoadAmmoInteractions(Weapon weapon, ItemUiContext itemUiContext)
                : base(ClassInjector.DerivedConstructorPointer<BarrelLoadAmmoInteractions>())
            {
                ClassInjector.DerivedConstructorBody(this);
                ClassInjector.InvokeBaseConstructor(this, itemUiContext);

                _weapon = weapon;
                _itemUiContext = itemUiContext;

                foreach (var (ammoTemplateId, count) in FindCompatibleAmmo())
                {
                    _foundAny = true;
                    string nameKey = ammoTemplateId + " Name";
                    string text = string.Format("<b><color=#C6C4B2>{0}</color> <color=#ADB8BC>({1})</color></b>", nameKey.Localized(), count);

                    AddDynamicInteraction(ammoTemplateId, text, (Action)(() => LoadAmmo(ammoTemplateId).HandleExceptions()));
                }

                if (!_foundAny)
                {
                    AddDynamicInteraction("UIFixesNoCompatibleAmmo", "NoCompatibleAmmo", null, null, null, "InventoryError/NoCompatibleAmmo");
                }
            }

            // BSG never wrote this for barrel-only guns
            private Dictionary<string, int> FindCompatibleAmmo()
            {
                var inventory = _itemUiContext.R().Inventory;

                var ammo = new Il2CppSystem.Collections.Generic.List<Ammo>();
                inventory.Stash.GetAllAssembledItems(ammo);
                inventory.Equipment.GetAllAssembledItems(ammo);

                Dictionary<string, int> results = [];
                foreach (var ammoItem in ammo)
                {
                    if (_itemUiContext.IsAvailable(ammoItem) && CheckCompatability(ammoItem))
                    {
                        if (!results.TryGetValue(ammoItem.TemplateId, out int existingCount))
                        {
                            existingCount = 0;
                        }

                        results[ammoItem.TemplateId] = existingCount + ammoItem.StackObjectsCount;
                    }
                }

                return results;
            }

            private bool CheckCompatability(Ammo ammo)
            {
                return _weapon.Chambers[0].Filters.CheckItemFilter(ammo);
            }

            public async Task LoadAmmo(string ammoTemplateId)
            {
                var inventory = _itemUiContext.R().Inventory;

                // try from stash, then equipment
                if (!await TryLoadFromContainer(inventory.Stash, ammoTemplateId))
                {
                    await TryLoadFromContainer(inventory.Equipment, ammoTemplateId);
                }

                if (Singleton<GUISounds>.Instantiated)
                {
                    Singleton<GUISounds>.Instance.PlayUILoadSound();
                }
            }

            private async Task<bool> TryLoadFromContainer(CompoundItem container, string ammoTemplateId)
            {
                var ammo = new Il2CppSystem.Collections.Generic.List<Ammo>();
                container.GetAllAssembledItems(ammo);

                var matchingAmmo = ammo.Where(a => a.TemplateId == ammoTemplateId && _itemUiContext.IsAvailable(a))
                    .OrderBy(a => a.SpawnedInSession)
                    .ThenBy(a => a.StackObjectsCount);

                var traderController = _itemUiContext.R().ItemController;

                foreach (var ammoItem in matchingAmmo)
                {
                    int stackCount;
                    do
                    {
                        stackCount = ammoItem.StackObjectsCount;
                        if (stackCount == 0)
                        {
                            break;

                        }

                        var operation = _weapon.Apply(traderController, ammoItem, int.MaxValue, true);
                        if (operation.Failed)
                        {
                            return true;
                        }

                        var result = await traderController.TryRunNetworkTransaction(operation);
                        if (result.Failed || _weapon.FreeChamberSlotsCount == 0)
                        {
                            return true;
                        }
                    } while (stackCount != ammoItem.StackObjectsCount);
                }

                return false;
            }
        }
    }

    public class UnloadAmmoPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ItemUiContext), nameof(ItemUiContext.UnloadAmmo));
        }

        [PatchPrefix]
        public static bool Prefix(ItemUiContext __instance, ItemContext itemContext, ref Il2CppSystem.Threading.Tasks.Task __result)
        {
            if (itemContext.Item is not Weapon weapon || weapon.ReloadMode != Weapon.EReloadMode.OnlyBarrel)
            {
                return true;
            }

            var equipmentBlocked = itemContext.ViewType == EItemViewType.InventoryDuringMatching;

            var taskSerializer = TaskSerializerBase.Create<UnloadChambersTaskSerializer>(ItemUiContext.Instance.gameObject);
            __result = taskSerializer.Initialize(
                weapon.Chambers,
                c => UnloadChamber(c, __instance._inventoryController, equipmentBlocked)).ToIl2Cpp();

            return false;
        }

        public static async Task UnloadChamber(Slot chamber, InventoryController inventoryController, bool equipmentBlocked)
        {
            if (chamber.ContainedItem is not Ammo ammoItem)
            {
                return;
            }

            var destinations = new Il2CppSystem.Collections.Generic.List<CompoundItem>();
            if (!equipmentBlocked)
            {
                destinations.Add(inventoryController.Inventory.Equipment);
            }

            if (inventoryController.Inventory.Stash != null)
            {
                destinations.Add(inventoryController.Inventory.Stash);
            }

            var operation = ItemManipulator.QuickFindAppropriatePlace(
                ammoItem,
                inventoryController,
                destinations,
                ItemManipulator.EMoveItemOrder.UnloadAmmo,
                true);


            if (!operation.Failed)
            {
                await inventoryController.TryRunNetworkTransaction(operation);
                Singleton<GUISounds>.Instance.PlayItemSound(ammoItem.ItemSound, EInventorySoundType.drop, false);
            }

            return;
        }

        private class UnloadChambersTaskSerializer : TaskSerializer<Slot> { }
    }
}