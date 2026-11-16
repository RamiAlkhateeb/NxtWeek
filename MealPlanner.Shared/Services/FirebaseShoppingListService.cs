using System.Net.Http.Json;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

/// <remarks>
/// While two friends sync, both use one list: the one at users/{ListHost}/shopping. The host is looked up from
/// users/{key}/sync and remembered for a short while.
/// </remarks>
public sealed class FirebaseShoppingListService(HttpClient http, FirebaseOptions options, INutritionStore store) : IShoppingListService
{
    private static readonly TimeSpan HostTtl = TimeSpan.FromSeconds(15);
    private readonly Dictionary<string, (string Host, DateTime Until)> hosts = new();

    public async Task<string> GetListOwnerAsync(string uid)
    {
        if (hosts.TryGetValue(uid, out var known) && known.Until > DateTime.UtcNow) return known.Host;
        string host;
        try { host = (await store.GetSyncLinkAsync(uid))?.ListHost is { Length: > 0 } h ? h : uid; }
        catch { host = uid; }
        hosts[uid] = (host, DateTime.UtcNow + HostTtl);
        return host;
    }

    public void ForgetListOwner(string uid) => hosts.Remove(uid);

    private readonly string baseUrl = options.DatabaseUrl.TrimEnd('/');
    private string Url(string path) => $"{baseUrl}/{path}.json";
    private static string Key(string value) => Uri.EscapeDataString(value);

    public async Task<List<ShoppingShop>> GetShopsAsync(string uid)
    {
        var shops = await http.GetFromJsonAsync<Dictionary<string, ShoppingShop>>(Url($"users/{Key(await GetListOwnerAsync(uid))}/shopping")) ?? new();
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
        using var response = await http.PutAsJsonAsync(Url($"users/{Key(await GetListOwnerAsync(uid))}/shopping/{shop.Id}"), shop);
        response.EnsureSuccessStatusCode();
        return shop;
    }

    public async Task DeleteShopAsync(string uid, string shopId)
    {
        using var response = await http.DeleteAsync(Url($"users/{Key(await GetListOwnerAsync(uid))}/shopping/{Key(shopId)}"));
        response.EnsureSuccessStatusCode();
    }

    public async Task<ShoppingItem> AddItemAsync(string uid, string shopId, string name, ShoppingItemOptions? options = null)
    {
        options ??= new ShoppingItemOptions();
        var item = new ShoppingItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = RequiredName(name, "العنصر"),
            Quantity = Math.Max(1, options.Quantity),
            Unit = options.Unit,
            Aisle = options.Aisle,
            SourceMeal = options.SourceMeal,
            IngredientKey = options.IngredientKey,
            UsedIn = options.UsedIn
        };
        using var response = await http.PutAsJsonAsync(Url($"users/{Key(await GetListOwnerAsync(uid))}/shopping/{Key(shopId)}/items/{item.Id}"), item);
        response.EnsureSuccessStatusCode();
        return item;
    }

    public async Task SetItemQuantityAsync(string uid, string shopId, string itemId, int quantity)
    {
        using var response = await http.PatchAsJsonAsync(Url($"users/{Key(await GetListOwnerAsync(uid))}/shopping/{Key(shopId)}/items/{Key(itemId)}"), new { quantity = Math.Max(1, quantity) });
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateItemAsync(string uid, string shopId, string itemId, string name, int quantity, string? unit)
    {
        var update = new { name = RequiredName(name, "العنصر"), quantity = Math.Max(1, quantity), unit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim() };
        using var response = await http.PatchAsJsonAsync(Url($"users/{Key(await GetListOwnerAsync(uid))}/shopping/{Key(shopId)}/items/{Key(itemId)}"), update);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetItemBoughtAsync(string uid, string shopId, string itemId, bool isBought)
    {
        var update = new { isBought, boughtAtUtc = isBought ? DateTime.UtcNow : (DateTime?)null };
        using var response = await http.PatchAsJsonAsync(Url($"users/{Key(await GetListOwnerAsync(uid))}/shopping/{Key(shopId)}/items/{Key(itemId)}"), update);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateAutoItemAsync(string uid, string shopId, string itemId, int quantity, int usedIn, string? sourceMeal)
    {
        var update = new { quantity = Math.Max(1, quantity), usedIn, sourceMeal };
        using var response = await http.PatchAsJsonAsync(Url($"users/{Key(await GetListOwnerAsync(uid))}/shopping/{Key(shopId)}/items/{Key(itemId)}"), update);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteItemAsync(string uid, string shopId, string itemId)
    {
        using var response = await http.DeleteAsync(Url($"users/{Key(await GetListOwnerAsync(uid))}/shopping/{Key(shopId)}/items/{Key(itemId)}"));
        response.EnsureSuccessStatusCode();
    }

    private static string RequiredName(string value, string label)
    {
        var name = value.Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException($"أدخل اسم {label}.");
        return name;
    }
}
