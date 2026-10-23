using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public sealed record ShoppingItemOptions(int Quantity = 1, string? Unit = null, string? Aisle = null, string? SourceMeal = null, string? IngredientKey = null, int UsedIn = 0);

public interface IShoppingListService
{
    Task<List<ShoppingShop>> GetShopsAsync(string uid);
    Task<ShoppingShop> AddShopAsync(string uid, string name);
    Task DeleteShopAsync(string uid, string shopId);
    Task<ShoppingItem> AddItemAsync(string uid, string shopId, string name, ShoppingItemOptions? options = null);
    Task SetItemQuantityAsync(string uid, string shopId, string itemId, int quantity);
    Task UpdateItemAsync(string uid, string shopId, string itemId, string name, int quantity, string? unit);
    Task SetItemBoughtAsync(string uid, string shopId, string itemId, bool isBought);
    Task DeleteItemAsync(string uid, string shopId, string itemId);
    /// <summary>Updates the amount of an item "Add this week's ingredients" created, when the plan changed.</summary>
    Task UpdateAutoItemAsync(string uid, string shopId, string itemId, int quantity, int usedIn, string? sourceMeal);
}
