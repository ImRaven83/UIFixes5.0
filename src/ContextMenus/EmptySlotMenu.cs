using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.Ragfair;
using Il2CppInterop.Runtime.Injection;

namespace UIFixes;

// Injected into il2cpp (see Plugin.Load) so the game's context menu code sees these overrides
public class EmptySlotMenu : BaseItemContextInteractions
{
    private static readonly Il2CppSystem.Collections.Generic.List<EItemInfoButton> Actions = MakeActions();

    private static Il2CppSystem.Collections.Generic.List<EItemInfoButton> MakeActions()
    {
        var actions = new Il2CppSystem.Collections.Generic.List<EItemInfoButton>();
        actions.Add(EItemInfoButton.LinkedSearch);
        return actions;
    }

    private static readonly List<EItemUiContextType> AllowedContextTypes = [
        EItemUiContextType.TraderScreen,
        EItemUiContextType.InventoryScreen,
        EItemUiContextType.RagfairScreen,
        EItemUiContextType.Hideout,
        EItemUiContextType.TransferItemsScreen];

    private readonly Slot _slot;

    public EmptySlotMenu(IntPtr pointer) : base(pointer) { }

    public EmptySlotMenu(Slot slot, ItemContext itemContext, ItemUiContext itemUiContext, Action closeAction)
        : base(ClassInjector.DerivedConstructorPointer<EmptySlotMenu>())
    {
        ClassInjector.DerivedConstructorBody(this);
        ClassInjector.InvokeBaseConstructor(this, itemContext, itemUiContext, (Il2CppSystem.Action)closeAction);
        _slot = slot;
    }

    public override Il2CppSystem.Collections.Generic.IEnumerable<EItemInfoButton> AvailableInteractions => Actions;

    public override void ExecuteInteractionInternal(EItemInfoButton interaction)
    {
        switch (interaction)
        {
            case EItemInfoButton.LinkedSearch:
                Search(new(EFilterType.LinkedSearch, _slot.ParentItem.TemplateId + ":" + _slot.ID, true));
                break;
            default:
                break;
        }
    }

    // Base IsActive pukes on mannequin slots, reimpliment it here for linked slots without troublesome parts
    public override bool IsActive(EItemInfoButton button)
    {
        return button switch
        {
            EItemInfoButton.LinkedSearch => !Plugin.InRaid() && AllowedContextTypes.Contains(ItemUiContext.ContextType),
            _ => false,
        };
    }

    // Base IsInteractive pukes on mannequin slots, reimpliment it here for linked slots without troublesome parts
    public override IResult IsInteractive(EItemInfoButton button)
    {
        var ragfair = ItemUiContext.Session.RagFair;
        switch (button)
        {
            case EItemInfoButton.LinkedSearch:
                if (ragfair == null)
                {
                    return new FailedResult("You can't use flea market right now", 0);
                }

                if (ragfair.Disabled)
                {
                    return new FailedResult(ragfair.GetFormattedStatusDescription(), 0);
                }

                return SuccessfulResult.New;
            default:
                return new FailedResult("Unsupported button");
        }
    }

    // Base subscriptions to items redraws, but no item means not necessary (and pukes on mannequins)
    public override Il2CppSystem.Action SubscribeOnRedraw() => (Action)NoOp;
    public override Il2CppSystem.Action SubscribeOnClose() => (Action)NoOp;

    private static void NoOp() { }
}