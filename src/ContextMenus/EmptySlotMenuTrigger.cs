using System;
using EFT.InventoryLogic;
using EFT.UI;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UIFixes;

public class EmptySlotMenuTrigger : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    public EmptySlotMenuTrigger(System.IntPtr pointer) : base(pointer) { }

    private ItemUiContext _itemUiContext;
    private Slot _slot;
    private ItemContext _parentContext;
    private bool _hovered = false;

    public void Init(Slot slot, ItemContext parentContext, ItemUiContext itemUiContext)
    {
        _itemUiContext = itemUiContext;
        _slot = slot;
        _parentContext = parentContext;
    }

    public void Update()
    {
        if (!_hovered)
        {
            return;
        }

        if (Settings.LinkedSearchKeyBind.Value.IsDown())
        {
            using EmptySlotContext context = new(_slot, _parentContext, _itemUiContext);
            var interactions = _itemUiContext.GetItemContextInteractions(context, null);
            interactions.ExecuteInteraction(EItemInfoButton.LinkedSearch);

            // Call this explicitly since screen transition prevents it from firing normally
            OnPointerExit(null);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            EmptySlotContext context = new(_slot, _parentContext, _itemUiContext);
            _itemUiContext.ShowContextMenu(context, eventData.position);
        }
    }

    public void OnPointerDown(PointerEventData eventData) { }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
    }

    public void OnPointerUp(PointerEventData eventData) { }
}

// Injected into il2cpp (see Plugin.Load) so the game's context menu code sees these overrides
public class EmptySlotContext : ItemContext
{
    private readonly Slot _slot;
    private readonly ItemUiContext _itemUiContext;

    public EmptySlotContext(IntPtr pointer) : base(pointer) { }

    public EmptySlotContext(Slot slot, ItemContext parentContext, ItemUiContext itemUiContext)
        : base(ClassInjector.DerivedConstructorPointer<EmptySlotContext>())
    {
        ClassInjector.DerivedConstructorBody(this);
        ClassInjector.InvokeBaseConstructor(this, parentContext.Item, parentContext.ViewType, parentContext);
        _slot = slot;
        _itemUiContext = itemUiContext;
    }

    public override ContextInteractions<EItemInfoButton> GetItemContextInteractions(Il2CppSystem.Action closeAction)
    {
        return new EmptySlotMenu(_slot, Source, _itemUiContext, () =>
        {
            Dispose();
            closeAction?.Invoke();
        });
    }

    public override ItemContext CreateChild(Item item)
    {
        // Should never happen
        throw new NotImplementedException();
    }
}
