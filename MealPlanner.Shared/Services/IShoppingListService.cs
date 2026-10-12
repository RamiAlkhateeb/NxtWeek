using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public sealed record ShoppingItemOptions(int Quantity = 1, string? Unit = null, string? Aisle = null, string? SourceMeal = null);

public interface IShoppingListService
{
    Task<List<ShoppingShop>> GetShopsAsync(string uid);
    Task<ShoppingShop> AddShopAsync(string uid, string name);
    Task DeleteShopAsync(string uid, string shopId);
    Task<ShoppingItem> AddItemAsync(string uid, string shopId, string name, ShoppingItemOptions? options = null);
    Task SetItemQuantityAsync(string uid, string shopId, string itemId, int quantity);
    Task SetItemBoughtAsync(string uid, string shopId, string itemId, bool isBought);
    Task DeleteItemAsync(string uid, string shopId, string itemId);
}
