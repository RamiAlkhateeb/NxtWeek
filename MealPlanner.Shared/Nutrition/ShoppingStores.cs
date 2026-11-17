using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Nutrition;

/// <summary>
/// Which store is which on the shopping list. The first store made is the default: typed items and plan items
/// without a remembered store go there, and it is listed first; the others follow by name. Pure and tested.
/// </summary>
public static class ShoppingStores
{
    public static ShoppingShop? Default(IEnumerable<ShoppingShop> shops) =>
        shops.OrderBy(s => s.CreatedAtUtc).ThenBy(s => s.Id, StringComparer.Ordinal).FirstOrDefault();

    public static List<ShoppingShop> Ordered(IReadOnlyCollection<ShoppingShop> shops) => Default(shops) is { } first
        ? [first, .. shops.Where(s => s.Id != first.Id).OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)]
        : [];

    /// <summary>The remembered store for an item (<see cref="Services.StoreRoute.Key(ShoppingItem)"/>), when it still exists.</summary>
    public static ShoppingShop? Remembered(string routeKey, IReadOnlyDictionary<string, string> routes, IEnumerable<ShoppingShop> shops) =>
        routes.TryGetValue(routeKey, out var id) ? shops.FirstOrDefault(s => s.Id == id) : null;

    /// <summary>Each item's store by item id. An item caught in two stores (a move half done) counts in the first.</summary>
    public static Dictionary<string, string> StoreOfItems(IEnumerable<ShoppingShop> shops)
    {
        var map = new Dictionary<string, string>();
        foreach (var shop in shops)
            foreach (var id in shop.Items.Values.Select(i => i.Id))
                map.TryAdd(id, shop.Id);
        return map;
    }

    /// <summary>The store a deleted store's items move to: the default, or the first other one when the default goes.</summary>
    public static ShoppingShop? HeirOf(string shopId, IReadOnlyCollection<ShoppingShop> shops) =>
        Ordered(shops).FirstOrDefault(s => s.Id != shopId);

    public static bool NameTaken(string name, IEnumerable<ShoppingShop> shops, string? exceptId = null) =>
        shops.Any(s => s.Id != exceptId && string.Equals(s.Name.Trim(), name.Trim(), StringComparison.CurrentCultureIgnoreCase));
}
