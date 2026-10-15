using System.Collections.Generic;
using EFT.InventoryLogic;

namespace UIFixes;

public static class ItemCacheHelper
{
    private static readonly Il2CppSystem.Collections.Generic.List<Item> AllItemsCache = new();

    public static void UpdateAllItemsCache(InventoryController inventoryController)
    {
        AllItemsCache.Clear();
        inventoryController.Inventory.Stash.GetAllAssembledItemsNonAlloc(AllItemsCache);
        inventoryController.Inventory.QuestStashItems.GetAllAssembledItemsNonAlloc(AllItemsCache);
        inventoryController.Inventory.QuestRaidItems.GetAllAssembledItemsNonAlloc(AllItemsCache);
    }

    public static Il2CppSystem.Collections.Generic.List<Item> GetAllItemsCache()
    {
        return AllItemsCache;
    }
}