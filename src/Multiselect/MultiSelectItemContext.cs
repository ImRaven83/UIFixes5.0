using System;
using System.Reflection;
using EFT.InventoryLogic;
using HarmonyLib;
using SPTushonka.Reflection.Patching;
using Il2CppInterop.Runtime.Injection;

namespace UIFixes;

// Injected into il2cpp (see Plugin.Load). DragItemContext is sealed in il2cpp, so the game may call ItemContext's
// virtuals non-virtually; the overrides below are also enforced by MultiSelectItemContextPatches.
public class MultiSelectItemContext : DragItemContext
{
    public MultiSelectItemContext(IntPtr pointer) : base(pointer) { }

    public MultiSelectItemContext(ItemContext itemContext, ItemRotation rotation)
        : base(ClassInjector.DerivedConstructorPointer<MultiSelectItemContext>())
    {
        ClassInjector.DerivedConstructorBody(this);
        ClassInjector.InvokeBaseConstructor(this, itemContext, rotation);

        // Adjust event handlers
        if (Source != null)
        {
            // Listen for underlying context being disposed, it might mean the item is gone (merged, destroyed, etc)
            Source.OnDisposed += OnParentDispose;

            // This serves no purpose and causes stack overflows. The base constructor subscribed natively,
            // so remove whatever handler targets this object rather than a converted managed delegate.
            Source.OnCloseDependentWindowField = Source.OnCloseDependentWindowField.WithoutTarget(this);
        }
    }

    public MultiSelectItemContext Refresh()
    {
        return Item == Source.Item ? new MultiSelectItemContext(Source, ItemRotation) : null;
    }

    public void UpdateDragContext(DragItemContext itemContext)
    {
        SetPosition(itemContext.CursorPosition, itemContext.ItemPosition);
        ItemRotation = itemContext.ItemRotation;
    }

    public override void Dispose()
    {
        base.Dispose();
        OnDisposed();
    }

    internal void OnDisposed()
    {
        if (Source != null)
        {
            Source.OnDisposed -= OnParentDispose;
        }
    }

    private void OnParentDispose()
    {
        if (Item.CurrentAddress == null ||
            (Item.CurrentAddress.Container.ParentItem is Magazine &&
            Item.CurrentAddress.Container.ParentItem is not CylinderMagazine))
        {
            // This item was entirely merged away, or went into a magazine
            MultiSelect.Deselect(this);
        }
    }

    // used by ItemUiContext.QuickFindAppropriatePlace, the one that picks a container, i.e. ctrl-click
    // DragItemContext (drag) defaults to None, but we want what the underlying item allows
    public override bool CanQuickMoveTo(ETargetContainer targetContainer)
    {
        return Source != null
            ? Source.CanQuickMoveTo(targetContainer)
            : base.CanQuickMoveTo(targetContainer);
    }
}

public static class MultiSelectItemContextPatches
{
    public static void Enable()
    {
        new CanQuickMoveToPatch().SafeEnable();
        new DisposePatch().SafeEnable();
    }

    public class CanQuickMoveToPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.DeclaredMethod(typeof(ItemContext), nameof(ItemContext.CanQuickMoveTo));
        }

        [PatchPrefix]
        public static bool Prefix(ItemContext __instance, ETargetContainer targetContainer, ref bool __result)
        {
            if (__instance is not MultiSelectItemContext multiSelectItemContext || multiSelectItemContext.Source == null)
            {
                return true;
            }

            __result = multiSelectItemContext.Source.CanQuickMoveTo(targetContainer);
            return false;
        }
    }

    public class DisposePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.DeclaredMethod(typeof(ItemContext), nameof(ItemContext.Dispose));
        }

        [PatchPostfix]
        public static void Postfix(ItemContext __instance)
        {
            if (__instance is MultiSelectItemContext multiSelectItemContext)
            {
                multiSelectItemContext.OnDisposed();
            }
        }
    }
}
