using MealPlanner.Shared.Models;
using MealPlanner.Shared.Nutrition;

namespace MealPlanner.Shared.Services;

public sealed record ShoppingItemOptions(int Quantity = 1, string? Unit = null, string? Aisle = null, string? SourceMeal = null, string? IngredientKey = null, int UsedIn = 0);

/// <summary>Whose list a user shops from: their own, or the sync partner's that hosts the shared list.</summary>
public interface IShoppingListOwner
{
    Task<string> GetAsync(string uid);
    /// <summary>Forgets the remembered owner after a sync starts or stops.</summary>
    void Forget(string uid);
}

/// <summary>The shopping list and its items. Stores are managed by <see cref="IShoppingStoreService"/>.</summary>
public interface IShoppingListService
{
    /// <summary>Every store with its items, as stored (the order is not meaningful; see <see cref="ShoppingStores.Ordered"/>).</summary>
    Task<List<ShoppingShop>> GetShopsAsync(string uid);
    Task<ShoppingItem> AddItemAsync(string uid, string shopId, string name, ShoppingItemOptions? options = null);
    Task UpdateItemAsync(string uid, string shopId, string itemId, string name, int quantity, string? unit);
    Task SetItemBoughtAsync(string uid, string shopId, string itemId, bool isBought);
    /// <summary>Updates the amount of an item the plan made, when the plan changed.</summary>
    Task UpdateAutoItemAsync(string uid, string shopId, string itemId, int quantity, int usedIn, string? sourceMeal);
    Task DeleteItemAsync(string uid, string shopId, string itemId);
    /// <summary>
    /// Removes items in one write. <paramref name="dismiss"/> remembers removed plan items (key → amount the plan
    /// needed) so the list does not add them back until the plan needs more (<see cref="ShoppingAggregator.Sync"/>).
    /// </summary>
    Task RemoveItemsAsync(string uid, IEnumerable<(string ShopId, string ItemId)> items, IReadOnlyDictionary<string, int>? dismiss = null);
    /// <summary>Empties the whole list in one write (the stores stay), remembering the plan items as removed.</summary>
    Task ClearAsync(string uid, IReadOnlyDictionary<string, int> dismiss);
    /// <summary>Plan items the user removed, by <see cref="ShoppingAggregator.DismissKey"/>, with the amount needed then.</summary>
    Task<Dictionary<string, int>> GetDismissedAsync(string uid);
    /// <summary>Forgets removals that no longer apply.</summary>
    Task UndismissAsync(string uid, IEnumerable<string> keys);
}

/// <summary>The stores on the list: adding, renaming and deleting them, which store an item goes to, and their logos.</summary>
public interface IShoppingStoreService
{
    Task<ShoppingShop> AddShopAsync(string uid, string name);
    /// <summary>The default store, made with <paramref name="defaultName"/> when the list has no store yet.</summary>
    Task<ShoppingShop> GetOrAddDefaultAsync(string uid, string defaultName);
    Task RenameShopAsync(string uid, string shopId, string name);
    /// <summary>Deletes a store, moving its items to another in the same write.</summary>
    Task DeleteShopAsync(string uid, string shopId, string moveItemsTo);
    /// <summary>Moves an item to another store in one write and remembers that store for it.</summary>
    Task MoveItemAsync(string uid, string fromShopId, string toShopId, ShoppingItem item);
    /// <summary>Which store each item goes to (by <see cref="StoreRoute.Key(ShoppingItem)"/>), remembered when an item is moved.</summary>
    Task<Dictionary<string, string>> GetStoreRoutesAsync(string uid);
    /// <summary>Saves the store's picture; null forgets it so it is looked up again (after a rename).</summary>
    Task SetLookAsync(string uid, string shopId, StoreLook? look);
}

/// <summary>The key an item's store is remembered under: its ingredient key for plan items, else its name.</summary>
public static class StoreRoute
{
    public static string Key(ShoppingItem item) => Key(item.IngredientKey, item.Name);

    public static string Key(string? ingredientKey, string name)
    {
        var raw = string.IsNullOrEmpty(ingredientKey) ? "txt:" + name.Trim().ToLowerInvariant() : ingredientKey;
        // Firebase keys cannot hold . # $ [ ] or /.
        return string.Concat(raw.Select(c => c is '.' or '#' or '$' or '[' or ']' or '/' ? '_' : c));
    }
}
