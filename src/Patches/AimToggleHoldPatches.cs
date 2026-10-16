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
        new AddTwoKeyStatesPatch(4).Enable();
        new AddTwoKeyStatesPatch(5).Enable();
        new AddOneKeyStatesPatch().Enable();
        new UpdateInputPatch().Enable();

        new ForceTacticalModePatch().Enable();
        new ToggleInteractionPatch().Enable();

        Settings.ToggleOrHoldAim.SettingChanged += OnSettingChanged;
        Settings.ToggleOrHoldInteract.SettingChanged += OnSettingChanged;
        Settings.ToggleOrHoldSprint.SettingChanged += OnSettingChanged;
        Settings.ToggleOrHoldTactical.SettingChanged += OnSettingChanged;
        Settings.ToggleOrHoldHeadlight.SettingChanged += OnSettingChanged;
        Settings.ToggleOrHoldGoggles.SettingChanged += OnSettingChanged;
    }

    // 5.0 has two ToggleInputKeyCombination constructors (with and without a pressing command); patch each
    public class AddTwoKeyStatesPatch(int parameterCount) : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.GetDeclaredConstructors(typeof(ToggleInputKeyCombination))
                .Single(c => !c.IsStatic && !c.IsIl2CppPointerConstructor() && c.GetParameters().Length == parameterCount);
        }

        [PatchPostfix]
        public static void Postfix(InputKeyCombination __instance, EGameKey gameKey)
        {
            if (!ToggleHold.IsEnabled(gameKey))
            {
                return;
            }

            InputKeyCombination.KeyCombinationState[] states =
            [
                .. __instance.States,
                new ToggleHoldIdleState(__instance),
                new ToggleHoldClickOrHoldState(__instance),
                new ToggleHoldHoldState(__instance)
            ];
            __instance.States = states;
        }
    }

    public class AddOneKeyStatesPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.GetDeclaredConstructors(typeof(InputKeyCombination)).Single(c => !c.IsStatic && !c.IsIl2CppPointerConstructor());
        }

        [PatchPostfix]
        public static void Postfix(InputKeyCombination __instance, EGameKey gameKey)
        {
            if (!ToggleHold.IsEnabled(gameKey))
            {
                return;
            }

            InputKeyCombination.KeyCombinationState[] states =
            [
                .. __instance.States,
                new ToggleHoldIdleState(__instance),
                new ToggleHoldClickOrHoldState(__instance),
                new ToggleHoldHoldState(__instance)
            ];
            __instance.States = states;
        }
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