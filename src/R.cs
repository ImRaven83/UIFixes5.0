using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using EFT.UI.Ragfair;
using EFT.UI.WeaponModding;
using UnityEngine;

namespace UIFixes;

// Under Mono these wrappers used reflection to reach private members. The il2cpp interop assemblies expose every
// member publicly (fields as properties), so they're now thin pass-throughs kept to avoid churning every call site.
public static class R
{
    public static void Init() { }

    public abstract class Wrapper<T>(T value)
    {
        public T Value { get; } = value;
    }

    public class UIElement(EFT.UI.UIElement value) : Wrapper<EFT.UI.UIElement>(value)
    {
        public UIParent UI => Value.UI;
    }

    public class UIInputNode(EFT.UI.UIInputNode value) : Wrapper<EFT.UI.UIInputNode>(value)
    {
        public UIParent UI => Value.UI;
    }

    public class CompactCharacteristicPanel(EFT.UI.CompactCharacteristicPanel value) : Wrapper<EFT.UI.CompactCharacteristicPanel>(value)
    {
        public UIParent UI => Value.UI;
        public ItemAttribute ItemAttribute => Value.ItemAttribute;
        public ItemAttribute CompareItemAttribute => Value.CompareItemAttribute;
    }

    public class GridView(EFT.UI.DragAndDrop.GridView value) : Wrapper<EFT.UI.DragAndDrop.GridView>(value)
    {
        public ItemController ItemController => Value._itemController;
        public bool NonInteractable => Value._nonInteractable;
        public static Color ValidMoveColor => EFT.UI.DragAndDrop.GridView.ValidMoveColor;
        public static Color InvalidOperationColor => EFT.UI.DragAndDrop.GridView.InvalidOperationColor;
        public static Color SwapColor => EFT.UI.DragAndDrop.GridView._swapColor;
        public Il2CppSystem.Collections.Generic.Dictionary<string, ItemView> ItemViews => Value.ItemViews;
    }

    public class InteractionButtonsContainer(EFT.UI.InteractionButtonsContainer value) : Wrapper<EFT.UI.InteractionButtonsContainer>(value)
    {
        public UIParent UI => Value.UI;
        public SimpleContextMenu ContextMenu => Value._subMenu;
        public SimpleContextMenuButton ContextMenuButton => Value._subMenuButton;
    }

    public class RagfairScreen(EFT.UI.Ragfair.RagfairScreen value) : Wrapper<EFT.UI.Ragfair.RagfairScreen>(value)
    {
        public UIParent UI => Value.UI;
        public OfferViewList OfferViewList => Value._selectedTab;
    }

    public class CategoryView(EFT.UI.Ragfair.CategoryView value) : Wrapper<EFT.UI.Ragfair.CategoryView>(value)
    {
        public bool IsOpen => Value._categoryOpen;
    }

    public class ItemUiContext(EFT.UI.ItemUiContext value) : Wrapper<EFT.UI.ItemUiContext>(value)
    {
        public ItemController ItemController => Value._itemController;
        public InventoryController InventoryController => Value._inventoryController;
        public Inventory Inventory => Value._inventory;
        public InventoryEquipment InventoryEquipment => Value._equipment;
        public DragItemContext DragItemContext => Value._dragItemContext;
        public ContextInteractions<EItemInfoButton> ItemInfoInteractions
        {
            get => Value._currentContextInteractions;
            set => Value._currentContextInteractions = value;
        }
    }

    public class TradingItemView(EFT.UI.DragAndDrop.TradingItemView value) : Wrapper<EFT.UI.DragAndDrop.TradingItemView>(value)
    {
        public bool IsBeingSold => Value._isBeingSold.Value;
    }

    public class ModSlotView(EFT.UI.DragAndDrop.ModSlotView value) : Wrapper<EFT.UI.DragAndDrop.ModSlotView>(value)
    {
        public UIParent UI => Value.UI;
        public EFT.UI.DragAndDrop.ModSlotView.TooltipData TooltipData => Value._tooltipData;
    }

    public class WeaponPreview(EFT.UI.WeaponModding.WeaponPreview value) : Wrapper<EFT.UI.WeaponModding.WeaponPreview>(value)
    {
        public Transform PreviewPivot => Value._previewPivot;
    }
}

public static class RExtentensions
{
    public static R.UIElement R(this UIElement value) => new(value);
    public static R.UIInputNode R(this UIInputNode value) => new(value);
    public static R.CompactCharacteristicPanel R(this CompactCharacteristicPanel value) => new(value);
    public static R.GridView R(this GridView value) => new(value);
    public static R.InteractionButtonsContainer R(this InteractionButtonsContainer value) => new(value);
    public static R.RagfairScreen R(this RagfairScreen value) => new(value);
    public static R.CategoryView R(this CategoryView value) => new(value);
    public static R.ItemUiContext R(this ItemUiContext value) => new(value);
    public static R.TradingItemView R(this TradingItemView value) => new(value);
    public static R.ModSlotView R(this ModSlotView value) => new(value);
    public static R.WeaponPreview R(this WeaponPreview value) => new(value);
}
