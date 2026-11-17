using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public sealed record ShoppingItemOptions(int Quantity = 1, string? Unit = null, string? Aisle = null, string? SourceMeal = null, string? IngredientKey = null, int UsedIn = 0);

public interface IShoppingListService
{
    Task<List<ShoppingShop>> GetShopsAsync(string uid);
    /// <summary>Whose list this user shops from: their own key, or the sync partner hosting the shared list.</summary>
    Task<string> GetListOwnerAsync(string uid);
    /// <summary>Forgets the remembered list owner after a sync starts or stops.</summary>
    void ForgetListOwner(string uid);
    Task<ShoppingShop> AddShopAsync(string uid, string name);
    Task DeleteShopAsync(string uid, string shopId);
    Task RenameShopAsync(string uid, string shopId, string name);
    /// <summary>Moves an item to another store, keeping everything else about it.</summary>
    Task MoveItemAsync(string uid, string fromShopId, string toShopId, ShoppingItem item);
    /// <summary>Which store each item goes to (by <see cref="StoreRoute.Key"/>), remembered when an item is moved.</summary>
    Task<Dictionary<string, string>> GetStoreRoutesAsync(string uid);
    Task RememberStoreAsync(string uid, string routeKey, string shopId);
    Task<ShoppingItem> AddItemAsync(string uid, string shopId, string name, ShoppingItemOptions? options = null);
    Task SetItemQuantityAsync(string uid, string shopId, string itemId, int quantity);
    Task UpdateItemAsync(string uid, string shopId, string itemId, string name, int quantity, string? unit);
    Task SetItemBoughtAsync(string uid, string shopId, string itemId, bool isBought);
    Task DeleteItemAsync(string uid, string shopId, string itemId);
    /// <summary>Updates the amount of an item "Add this week's ingredients" created, when the plan changed.</summary>
    Task UpdateAutoItemAsync(string uid, string shopId, string itemId, int quantity, int usedIn, string? sourceMeal);
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
