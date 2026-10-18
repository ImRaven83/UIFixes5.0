using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.InputSystem;
using EFT.Settings;
using EFT.Settings.Game;
using EFT.UI;
using HarmonyLib;
using SPTushonka.Reflection.Patching;

namespace UIFixes;

public static class AimToggleHoldPatches
{
    public static void Enable()
    {
        new UpdateInputPatch().SafeEnable();

        new ForceTacticalModePatch().SafeEnable();
        new ToggleInteractionPatch().SafeEnable();

        Settings.ToggleOrHoldAim.SettingChanged += OnSettingChanged;
        Settings.ToggleOrHoldInteract.SettingChanged += OnSettingChanged;
        Settings.ToggleOrHoldSprint.SettingChanged += OnSettingChanged;
        Settings.ToggleOrHoldTactical.SettingChanged += OnSettingChanged;
        Settings.ToggleOrHoldHeadlight.SettingChanged += OnSettingChanged;
        Settings.ToggleOrHoldGoggles.SettingChanged += OnSettingChanged;
    }

    public class UpdateInputPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(InputKeyCombination), nameof(InputKeyCombination.UpdateInput));
        }

        [PatchPrefix]
        public static void Prefix(InputKeyCombination __instance, ref Il2CppSystem.Collections.Generic.List<IGameKey> inputKeys)
        {
            // The extra states were added in the constructors on mono, but detouring ToggleInputKeyCombination's constructors
            // makes them throw in il2cpp. UpdateInput runs whenever bindings are applied, so add them here instead.
            if (ToggleHold.IsEnabled(__instance.GameKey))
            {
                AddStates(__instance);
            }

            // BSG implemented tactical as an entirely new abomination, so I have to disable the "release tactical" 
            if (__instance.GameKey == EGameKey.ReleaseTactical && ToggleHold.IsEnabled(EGameKey.Tactical))
            {
                inputKeys = new Il2CppSystem.Collections.Generic.List<IGameKey>();

                // Force BSG's tactical mode to "press"
                Singleton<SettingsManager>.Instance.Game.Settings.TacticalInputMode.SetValue(GameSettingsGroup.ETacticalInputMode.Press);
            }
        }

        [PatchPostfix]
        public static void Postfix(InputKeyCombination __instance)
        {
            if (ToggleHold.IsEnabled(__instance.GameKey))
            {
                __instance.ChangeState((InputKeyCombination.EKeyState)ToggleHoldState.Idle);
            }
        }
    }

    private static void AddStates(InputKeyCombination keyCombination)
    {
        var states = keyCombination.States;
        // The custom states must land at their enum indexes, right after the game's own
        if (states == null || states.Length != (int)ToggleHoldState.Idle)
        {
            return;
        }

        InputKeyCombination.KeyCombinationState[] newStates =
        [
            .. states,
            new ToggleHoldIdleState(keyCombination),
            new ToggleHoldClickOrHoldState(keyCombination),
            new ToggleHoldHoldState(keyCombination)
        ];
        keyCombination.States = newStates;
    }

    // If using toggle/hold tactical, need to force the "tactical device mode" to be press.
    public class ForceTacticalModePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Property(typeof(FirearmHandsInputTranslator), nameof(FirearmHandsInputTranslator.IsPressTacticalInteraction)).SetMethod;
        }

        [PatchPrefix]
        public static void Prefix(ref bool value)
        {
            if (ToggleHold.IsEnabled(EGameKey.Tactical))
            {
                value = true;
            }
        }
    }

    // Begin/End Interacting is implemented as a two-state keybind, but unlike the others it's not toggle/end, it's begin/end. 
    // And none of the interactions handle receiving begin while they're active
    public class ToggleInteractionPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ActionPanel), nameof(ActionPanel.TranslateCommand));
        }

        [PatchPrefix]
        public static void Prefix(ActionPanel __instance, ref ECommand command)
        {
            if (command == ECommand.BeginInteracting && __instance._owner != null && __instance._owner.Player.CurrentManagedState is PlantPlayerState)
            {
                command = ECommand.EndInteracting;
            }
        }
    }


    private static void OnSettingChanged(object sender, EventArgs args)
    {
        // Force BSG's tactical mode to "press"
        Singleton<SettingsManager>.Instance.Game.Settings.TacticalInputMode.SetValue(GameSettingsGroup.ETacticalInputMode.Press);

        // Will "save" control settings, running InputKeyCombination.UpdateInput, which will set (or unset) toggle/hold behavior
        Singleton<SettingsManager>.Instance.Control.Controller.UpdateBindings();
    }
}