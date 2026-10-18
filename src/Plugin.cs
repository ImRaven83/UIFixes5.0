using System;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Comfort.Common;
using EFT;
using TMPro;
using SPTushonka.Reflection.Il2Cpp;
using UnityEngine.EventSystems;

namespace UIFixes;

[BepInPlugin("com.tyfon.uifixes", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency("com.fika.core", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("xyz.pit.fireteam", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("com.lacyway.mc", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(ConfigManagerProxy.GUID, BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BasePlugin
{
    public static Plugin Instance;

    public ManualLogSource Logger => Log;

    public override void Load()
    {
        Instance = this;

        // Every MonoBehaviour (and other il2cpp-derived type) in this assembly has to be registered before it can be used
        Il2CppInjection.RegisterAll(typeof(Plugin).Assembly, Log);

        // Needed for converting managed tasks back to il2cpp tasks and for awaits to resume on the Unity thread; no-op if SPT already installed it
        try
        {
            MainThread.Install(this);
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Could not install main thread dispatcher: {ex.Message}");
        }

        Settings.Init(Config);

        R.Init();

        SafeEnable(NetworkTransactionWatcher.Enable);
        SafeEnable(ConfirmDialogKeysPatches.Enable);
        SafeEnable(MailPatches.Enable);
        SafeEnable(FixTooltipPatches.Enable);
        SafeEnable(QuickAccessPanelPatches.Enable);
        SafeEnable(FocusFleaOfferNumberPatches.Enable);
        SafeEnable(HideoutSearchPatches.Enable);
        SafeEnable(HideoutLevelPatches.Enable);
        SafeEnable(InspectWindowResizePatches.Enable);
        SafeEnable(InspectWindowStatsPatches.Enable);
        new RemoveDoorActionsPatch().SafeEnable();
        SafeEnable(ScrollPatches.Enable);
        SafeEnable(StackFirItemsPatches.Enable);
        SafeEnable(SwapPatches.Enable);
        SafeEnable(SyncScrollPositionPatches.Enable);
        new TransferConfirmPatch().SafeEnable();
        SafeEnable(PlayerModelViewPatches.Enable);
        SafeEnable(WeaponPresetConfirmPatches.Enable);
        SafeEnable(WeaponZoomPatches.Enable);
        SafeEnable(WeaponPanPatches.Enable);
        SafeEnable(WeaponPreviewPatches.Enable);
        new MoveTaskbarPatch().SafeEnable();
        SafeEnable(FixFleaPatches.Enable);
        SafeEnable(FleaPrevSearchPatches.Enable);
        SafeEnable(KeepOfferWindowOpenPatches.Enable);
        SafeEnable(AddOfferClickablePricesPatches.Enable);
        new AssortUnlocksPatch().SafeEnable(); // TODO: Implement server side
        new AutofillQuestItemsPatch().SafeEnable();
        SafeEnable(ContextMenuPatches.Enable);
        SafeEnable(TradingAutoSwitchPatches.Enable);
        SafeEnable(AddOfferRememberAutoselectPatches.Enable);
        SafeEnable(KeepMessagesOpenPatches.Enable);
        SafeEnable(TradeQuantityPatches.Enable);
        SafeEnable(RememberRepairerPatches.Enable);
        new GridWindowButtonsPatch().SafeEnable();
        SafeEnable(MagPresetsPatches.Enable);
        SafeEnable(WindowPatches.Enable);
        SafeEnable(ContextMenuShortcutPatches.Enable);
        SafeEnable(OpenSortingTablePatches.Enable);
        SafeEnable(LoadAmmoInRaidPatches.Enable);
        SafeEnable(MultiSelectPatches.Enable);
        SafeEnable(LoadMultipleMagazinesPatches.Enable);
        SafeEnable(UnloadAmmoPatches.Enable);
        SafeEnable(FixTraderControllerPatches.Enable);
        new PutToolsBackPatch().SafeEnable();
        SafeEnable(RebindConsumablesPatches.Enable);
        SafeEnable(AimToggleHoldPatches.Enable);
        SafeEnable(ReorderGridsPatches.Enable);
        SafeEnable(GPCoinPatches.Enable);
        SafeEnable(FleaSlotSearchPatches.Enable);
        SafeEnable(FilterOutOfStockPatches.Enable);
        SafeEnable(SortPatches.Enable);
        SafeEnable(ReloadInPlacePatches.Enable);
        SafeEnable(BarterOfferPatches.Enable);
        SafeEnable(CursorPatches.Enable);
        SafeEnable(LimitDragPatches.Enable);
        SafeEnable(HideoutCameraPatches.Enable);
        SafeEnable(WeaponModdingPatches.Enable);
        SafeEnable(TagPatches.Enable);
        SafeEnable(TacticalBindsPatches.Enable);
        new OperationQueuePatch().SafeEnable();
        SafeEnable(SliderPatches.Enable);
        SafeEnable(DropdownPatches.Enable);
        new FixPlayerInspectPatch().SafeEnable();
        SafeEnable(QuestKeysPatches.Enable);
        new ModifyUnsearchedContainerPatch().SafeEnable();
        SafeEnable(RemoveAdsPatches.Enable);
        SafeEnable(BTRPaymentPatches.Enable);
        SafeEnable(TradingHighlightPatches.Enable);
        SafeEnable(TraderAvatarPatches.Enable);
        SafeEnable(FilterStockPresetsPatches.Enable);
        SafeEnable(WishlistPatches.Enable);
        SafeEnable(StashSearchPatches.Enable);
        SafeEnable(InternalMagPatches.Enable);
        SafeEnable(BarrelOnlyPatches.Enable);
        SafeEnable(RevolverPatches.Enable);
        SafeEnable(TextboxPatches.Enable);
        SafeEnable(VariableScopeFixPatches.Enable);
        new TransferMergePatch().SafeEnable();
        SafeEnable(GridHighlightPatches.Enable);
        SafeEnable(QueueInputPatches.Enable);
        new CompassGogglesPatch().SafeEnable();
        new FixGridPrepareItemsPatch().SafeEnable();
        new FixTraderFiltersPatch().SafeEnable();
        SafeEnable(QuickMovePreviewPatches.Enable);
        new RotateKeybindPatch().SafeEnable();
        new HideInviteUIPatch().SafeEnable();
        SafeEnable(ServicesPatches.Enable);

        IL2CPPChainloader.Instance.Finished += CheckForOldInterop;
    }

    // One feature failing to patch (e.g. after a game update) shouldn't take the rest of the mod down with it
    private void SafeEnable(Action enable)
    {
        try
        {
            enable();
        }
        catch (Exception ex)
        {
            Log.LogError($"Failed to enable {enable.Method.DeclaringType?.Name}: {ex}");
        }
    }

    public static bool InRaid()
    {
        var instance = Singleton<AbstractGame>.Instance;
        return instance != null && instance.InRaid;
    }

    public static bool TextboxActive()
    {
        return EventSystem.current?.currentSelectedGameObject != null &&
            EventSystem.current.currentSelectedGameObject.activeInHierarchy &&
            EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;
    }

    private void CheckForOldInterop()
    {
        var pluginsWithOldDependency = IL2CPPChainloader.Instance.Plugins.Values.Where(p => p.Dependencies.Any(d => d.DependencyGUID == "Tyfon.UIFixes"));
        if (pluginsWithOldDependency.Any())
        {
            var names = string.Join(",", [.. pluginsWithOldDependency.Select(p => p.Metadata.Name)]);
            Logger.LogWarning($"The following client mods depend on an old version of UI Fixes, and need to update their dependency GUID to 'com.tyfon.uifixes': {names}");
        }
    }

    private static bool? IsFikaPresent;

    public static bool FikaPresent()
    {
        if (!IsFikaPresent.HasValue)
        {
            if (IL2CPPChainloader.Instance.Plugins.TryGetValue("com.fika.core", out BepInEx.PluginInfo pluginInfo))
            {
                // UI Fixes no longer ships Fika integration for SPT 5.0; this only gates features that conflict with it
                IsFikaPresent = true;
            }
            else
            {
                IsFikaPresent = false;
            }
        }

        return IsFikaPresent.Value;
    }

    private static bool? IsMergeConsumablesPresent;

    public static bool MergeConsumablesPresent()
    {
        if (!IsMergeConsumablesPresent.HasValue)
        {
            IsMergeConsumablesPresent = IL2CPPChainloader.Instance.Plugins.ContainsKey("com.lacyway.mc");
        }

        return IsMergeConsumablesPresent.Value;
    }

    private static bool? IsPITFireteamPresent;

    public static bool PITFireteamPresent()
    {
        if (!IsPITFireteamPresent.HasValue)
        {
            IsPITFireteamPresent = IL2CPPChainloader.Instance.Plugins.ContainsKey("xyz.pit.fireteam");
        }

        return IsPITFireteamPresent.Value;
    }
}