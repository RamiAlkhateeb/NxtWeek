namespace MealPlanner.Shared.Services;

public static class ShoppingListExtensions
{
    /// <summary>
    /// Adds the ingredients that are not already on any of the user's lists (case-insensitive) to the
    /// first list, creating a list named <paramref name="defaultShopName"/> when there is none.
    /// </summary>
    public static async Task<(int Added, string ShopName)> AddIngredientsAsync(
        this IShoppingListService service, string uid, IEnumerable<string> ingredients, string defaultShopName)
    {
        var shops = await service.GetShopsAsync(uid);
        var existing = shops.SelectMany(s => s.Items.Values).Select(i => i.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = ingredients.Select(i => i.Trim()).Where(i => i.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).Where(i => !existing.Contains(i)).ToList();

        var shop = shops.FirstOrDefault() ?? await service.AddShopAsync(uid, defaultShopName);
        if (missing.Count == 0) return (0, shop.Name);

        foreach (var name in missing) await service.AddItemAsync(uid, shop.Id, name);
        return (missing.Count, shop.Name);
    }
}
