using System;
using EFT.InputSystem;
using Il2CppInterop.Runtime.Injection;

namespace UIFixes;

public enum ToggleHoldState
{
    Idle = 13,
    ClickOrHold = 14,
    Holding = 15
}

// Injected into il2cpp (see Plugin.Load) so InputKeyCombination's state machine calls these overrides
public class ToggleHoldIdleState : InputKeyCombination.KeyCombinationState
{
    public ToggleHoldIdleState(IntPtr pointer) : base(pointer) { }

    public ToggleHoldIdleState(InputKeyCombination keyCombination)
        : base(ClassInjector.DerivedConstructorPointer<ToggleHoldIdleState>())
    {
        ClassInjector.DerivedConstructorBody(this);
        ClassInjector.InvokeBaseConstructor(this, keyCombination);
    }

    public override ECommand GetCommand(float deltaTime)
    {
        if (!CanProcess())
        {
            return ECommand.None;
        }

        HandleKeys(false);
        KeyCombination.ChangeState((InputKeyCombination.EKeyState)ToggleHoldState.ClickOrHold);
        return GetCommandInternal();
    }

    protected bool CanProcess()
    {
        return GetKeysStatus(out EKeyPress ekeyPress) && (ekeyPress == EKeyPress.Down);
    }
}

// Injected into il2cpp (see Plugin.Load) so InputKeyCombination's state machine calls these overrides
public class ToggleHoldClickOrHoldState : InputKeyCombination.KeyCombinationState
{
    public ToggleHoldClickOrHoldState(IntPtr pointer) : base(pointer) { }

    public ToggleHoldClickOrHoldState(InputKeyCombination keyCombination)
        : base(ClassInjector.DerivedConstructorPointer<ToggleHoldClickOrHoldState>())
    {
        ClassInjector.DerivedConstructorBody(this);
        ClassInjector.InvokeBaseConstructor(this, keyCombination);
    }

    public override void Enter()
    {
        _timer = KeyCombination.DoubleClickTimeout;
    }

    public override ECommand GetCommand(float deltaTime)
    {
        if (GetKeysStatus(out EKeyPress ekeyPress))
        {
            if (ekeyPress == EKeyPress.Hold)
            {
                HandleKeys(false);
                if (LongEnough(deltaTime))
                {
                    KeyCombination.ChangeState((InputKeyCombination.EKeyState)ToggleHoldState.Holding);
                }

                return ECommand.None;
            }
        }

        UnhandleKeys(null);
        KeyCombination.ChangeState((InputKeyCombination.EKeyState)ToggleHoldState.Idle);
        return ECommand.None;
    }

    private bool LongEnough(float deltaTime)
    {
        _timer -= deltaTime;
        return _timer <= 0f;
    }

    private float _timer;
}

// Injected into il2cpp (see Plugin.Load) so InputKeyCombination's state machine calls these overrides
public class ToggleHoldHoldState : InputKeyCombination.KeyCombinationState
{
    public ToggleHoldHoldState(IntPtr pointer) : base(pointer) { }

    public ToggleHoldHoldState(InputKeyCombination keyCombination)
        : base(ClassInjector.DerivedConstructorPointer<ToggleHoldHoldState>())
    {
        ClassInjector.DerivedConstructorBody(this);
        ClassInjector.InvokeBaseConstructor(this, keyCombination);
    }

    public override ECommand GetCommand(float deltaTime)
    {
        if (GetKeysStatus(out EKeyPress ekeyPress) && ekeyPress == EKeyPress.Hold)
        {
            HandleKeys(false);
            return ECommand.None;
        }

        UnhandleKeys(null);
        KeyCombination.ChangeState((InputKeyCombination.EKeyState)ToggleHoldState.Idle);

        return KeyCombination is ToggleInputKeyCombination toggleKeyCombination ? toggleKeyCombination._disableCommand : KeyCombination._currentCommand;
    }
}

public static class ToggleHold
{
    public static bool IsEnabled(EGameKey gameKey)
    {
        return gameKey switch
        {
            EGameKey.Aim => Settings.ToggleOrHoldAim.Value,
            EGameKey.Interact => Settings.ToggleOrHoldInteract.Value,
            EGameKey.Tactical => Settings.ToggleOrHoldTactical.Value,
            EGameKey.ToggleGoggles => Settings.ToggleOrHoldGoggles.Value,
            EGameKey.ToggleHeadLight => Settings.ToggleOrHoldHeadlight.Value,
            EGameKey.Sprint => Settings.ToggleOrHoldSprint.Value,
            EGameKey.Slot4 => IsQuickbindEnabled(EGameKey.Slot4),
            EGameKey.Slot5 => IsQuickbindEnabled(EGameKey.Slot5),
            EGameKey.Slot6 => IsQuickbindEnabled(EGameKey.Slot6),
            EGameKey.Slot7 => IsQuickbindEnabled(EGameKey.Slot7),
            EGameKey.Slot8 => IsQuickbindEnabled(EGameKey.Slot8),
            EGameKey.Slot9 => IsQuickbindEnabled(EGameKey.Slot9),
            EGameKey.Slot0 => IsQuickbindEnabled(EGameKey.Slot0),
            _ => false
        };
    }

    private static bool IsQuickbindEnabled(EGameKey gameKey)
    {
        return Quickbind.GetType(gameKey) switch
        {
            Quickbind.ItemType.Tactical => Settings.ToggleOrHoldTactical.Value,
            Quickbind.ItemType.Headlight => Settings.ToggleOrHoldHeadlight.Value,
            Quickbind.ItemType.NightVision => Settings.ToggleOrHoldGoggles.Value,
            _ => false,
        };
    }
}