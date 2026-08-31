using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public interface IShoppingListService
{
    Task<List<ShoppingShop>> GetShopsAsync(string uid);
    Task<ShoppingShop> AddShopAsync(string uid, string name);
    Task DeleteShopAsync(string uid, string shopId);
    Task<ShoppingItem> AddItemAsync(string uid, string shopId, string name);
    Task SetItemBoughtAsync(string uid, string shopId, string itemId, bool isBought);
    Task DeleteItemAsync(string uid, string shopId, string itemId);
}
