using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Comfort.Common;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using Il2CppInterop.Runtime.Injection;

namespace UIFixes;

// Injected into il2cpp (see Plugin.Load). The game's ContextInteractions<T> is generic and can't be subclassed from here,
// so this derives from BaseEmptyContextInteractions and only uses dynamic interactions.
public class OpenInteractions : BaseEmptyContextInteractions
{
    private readonly ItemContext _itemContext;

    public OpenInteractions(System.IntPtr pointer) : base(pointer) { }

    public OpenInteractions(ItemContext itemContext, ItemUiContext itemUiContext)
        : base(ClassInjector.DerivedConstructorPointer<OpenInteractions>())
    {
        ClassInjector.DerivedConstructorBody(this);
        ClassInjector.InvokeBaseConstructor(this, itemUiContext);

        _itemContext = itemContext;
        AddDynamicInteraction("UIFixesOpenAll", "All", (System.Action)OpenAll);
    }

    private void OpenAll()
    {
        if (_itemContext == null || _itemContext.Item is not CompoundItem compoundItem)
        {
            return;
        }

        var taskSerializer = TaskSerializerBase.Create<NestedContainerTaskSerializer>(UIContext.gameObject);
        taskSerializer.Initialize(GetNestedContainers(_itemContext), containerContext =>
        {
            if (containerContext != null)
            {
                UIContext.OpenItem(containerContext.Item as CompoundItem, containerContext);
            }

            return Task.CompletedTask;
        });
    }

    public override bool HasIcons
    {
        get { return false; }
    }

    private IEnumerable<ItemContext> GetNestedContainers(ItemContext first)
    {
        var windowRoot = Singleton<PreloaderUI>.Instance;
        CompoundItem parent = first.Item as CompoundItem;

        yield return first;

        while (true)
        {
            var innerContainers = parent.GetFirstLevelItems()
                .Where(i => i != parent)
                .Where(i => i is CompoundItem innerContainer && innerContainer.Grids.Any());
            if (innerContainers.Count() != 1)
            {
                yield break;
            }

            var targetId = innerContainers.First().Id;
            var targetItemView = windowRoot.GetComponentsInChildren<GridItemView>().FirstOrDefault(itemView => itemView.Item.Id == targetId);
            if (targetItemView == null)
            {
                yield return null; // Keeps returning null until the window is open
            }

            parent = targetItemView.Item as CompoundItem;
            yield return targetItemView.ItemContext;
        }
    }
}

public class NestedContainerTaskSerializer : TaskSerializer<ItemContext> { }