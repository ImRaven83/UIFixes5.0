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

        // Needed for converting managed tasks back to il2cpp tasks; no-op if SPT already installed it
        MainThread.Install(this);

        Settings.Init(Config);

        R.Init();

        NetworkTransactionWatcher.Enable();
        ConfirmDialogKeysPatches.Enable();
        MailPatches.Enable();
        FixTooltipPatches.Enable();
        QuickAccessPanelPatches.Enable();
        FocusFleaOfferNumberPatches.Enable();
        HideoutSearchPatches.Enable();
        HideoutLevelPatches.Enable();
        InspectWindowResizePatches.Enable();
        InspectWindowStatsPatches.Enable();
        new RemoveDoorActionsPatch().Enable();
        ScrollPatches.Enable();
        StackFirItemsPatches.Enable();
        SwapPatches.Enable();
        SyncScrollPositionPatches.Enable();
        new TransferConfirmPatch().Enable();
        PlayerModelViewPatches.Enable();
        WeaponPresetConfirmPatches.Enable();
        WeaponZoomPatches.Enable();
        WeaponPanPatches.Enable();
        WeaponPreviewPatches.Enable();
        new MoveTaskbarPatch().Enable();
        FixFleaPatches.Enable();
        FleaPrevSearchPatches.Enable();
        KeepOfferWindowOpenPatches.Enable();
        AddOfferClickablePricesPatches.Enable();
        new AssortUnlocksPatch().Enable(); // TODO: Implement server side
        new AutofillQuestItemsPatch().Enable();
        ContextMenuPatches.Enable();
        TradingAutoSwitchPatches.Enable();
        AddOfferRememberAutoselectPatches.Enable();
        KeepMessagesOpenPatches.Enable();
        TradeQuantityPatches.Enable();
        RememberRepairerPatches.Enable();
        new GridWindowButtonsPatch().Enable();
        MagPresetsPatches.Enable();
        WindowPatches.Enable();
        ContextMenuShortcutPatches.Enable();
        OpenSortingTablePatches.Enable();
        LoadAmmoInRaidPatches.Enable();
        MultiSelectPatches.Enable();
        LoadMultipleMagazinesPatches.Enable();
        UnloadAmmoPatches.Enable();
        FixTraderControllerPatches.Enable();
        new PutToolsBackPatch().Enable();
        RebindConsumablesPatches.Enable();
        AimToggleHoldPatches.Enable();
        ReorderGridsPatches.Enable();
        GPCoinPatches.Enable();
        FleaSlotSearchPatches.Enable();
        FilterOutOfStockPatches.Enable();
        SortPatches.Enable();
        ReloadInPlacePatches.Enable();
        BarterOfferPatches.Enable();
        CursorPatches.Enable();
        LimitDragPatches.Enable();
        HideoutCameraPatches.Enable();
        WeaponModdingPatches.Enable();
        TagPatches.Enable();
        TacticalBindsPatches.Enable();
        new OperationQueuePatch().Enable();
        SliderPatches.Enable();
        DropdownPatches.Enable();
        new FixPlayerInspectPatch().Enable();
        QuestKeysPatches.Enable();
        new ModifyUnsearchedContainerPatch().Enable();
        RemoveAdsPatches.Enable();
        BTRPaymentPatches.Enable();
        TradingHighlightPatches.Enable();
        TraderAvatarPatches.Enable();
        FilterStockPresetsPatches.Enable();
        WishlistPatches.Enable();
        StashSearchPatches.Enable();
        InternalMagPatches.Enable();
        BarrelOnlyPatches.Enable();
        RevolverPatches.Enable();
        TextboxPatches.Enable();
        VariableScopeFixPatches.Enable();
        new TransferMergePatch().Enable();
        GridHighlightPatches.Enable();
        QueueInputPatches.Enable();
        new CompassGogglesPatch().Enable();
        new FixGridPrepareItemsPatch().Enable();
        new FixTraderFiltersPatch().Enable();
        QuickMovePreviewPatches.Enable();
        new RotateKeybindPatch().Enable();
        new HideInviteUIPatch().Enable();
        ServicesPatches.Enable();

        IL2CPPChainloader.Instance.Finished += CheckForOldInterop;
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