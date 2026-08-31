using System.Net.Http.Json;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public sealed class FirebaseShoppingListService(HttpClient http, FirebaseOptions options) : IShoppingListService
{
    private readonly string baseUrl = options.DatabaseUrl.TrimEnd('/');
    private string Url(string path) => $"{baseUrl}/{path}.json";
    private static string Key(string value) => Uri.EscapeDataString(value);

    public async Task<List<ShoppingShop>> GetShopsAsync(string uid)
    {
        var shops = await http.GetFromJsonAsync<Dictionary<string, ShoppingShop>>(Url($"users/{Key(uid)}/shopping")) ?? new();
        return shops.Select(pair =>
        {
            var shop = pair.Value;
            shop.Id = string.IsNullOrWhiteSpace(shop.Id) ? pair.Key : shop.Id;
            shop.Items ??= new();
            foreach (var item in shop.Items)
                item.Value.Id = string.IsNullOrWhiteSpace(item.Value.Id) ? item.Key : item.Value.Id;
            return shop;
        }).OrderBy(shop => shop.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<ShoppingShop> AddShopAsync(string uid, string name)
    {
        name = RequiredName(name, "المتجر");
        var existing = await GetShopsAsync(uid);
        if (existing.Any(shop => string.Equals(shop.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("هذا المتجر موجود بالفعل في قائمتك.");

        var shop = new ShoppingShop { Id = Guid.NewGuid().ToString("N"), Name = name };
        using var response = await http.PutAsJsonAsync(Url($"users/{Key(uid)}/shopping/{shop.Id}"), shop);
        response.EnsureSuccessStatusCode();
        return shop;
    }

    public async Task DeleteShopAsync(string uid, string shopId)
    {
        using var response = await http.DeleteAsync(Url($"users/{Key(uid)}/shopping/{Key(shopId)}"));
        response.EnsureSuccessStatusCode();
    }

    public async Task<ShoppingItem> AddItemAsync(string uid, string shopId, string name)
    {
        var item = new ShoppingItem { Id = Guid.NewGuid().ToString("N"), Name = RequiredName(name, "العنصر") };
        using var response = await http.PutAsJsonAsync(Url($"users/{Key(uid)}/shopping/{Key(shopId)}/items/{item.Id}"), item);
        response.EnsureSuccessStatusCode();
        return item;
    }

    public async Task SetItemBoughtAsync(string uid, string shopId, string itemId, bool isBought)
    {
        var update = new { isBought, boughtAtUtc = isBought ? DateTime.UtcNow : (DateTime?)null };
        using var response = await http.PatchAsJsonAsync(Url($"users/{Key(uid)}/shopping/{Key(shopId)}/items/{Key(itemId)}"), update);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteItemAsync(string uid, string shopId, string itemId)
    {
        using var response = await http.DeleteAsync(Url($"users/{Key(uid)}/shopping/{Key(shopId)}/items/{Key(itemId)}"));
        response.EnsureSuccessStatusCode();
    }

    private static string RequiredName(string value, string label)
    {
        var name = value.Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException($"أدخل اسم {label}.");
        return name;
    }
}
