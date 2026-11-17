using MealPlanner.Shared.Models;
using MealPlanner.Shared.Nutrition;
using static MealPlanner.Shared.Services.FirebaseRest;

namespace MealPlanner.Shared.Services;

/// <summary>
/// While two friends sync, both use one list: the one at users/{ListHost}/shopping. The host is looked up from
/// users/{key}/sync and remembered for a short while.
/// </summary>
public sealed class SyncedListOwner(INutritionStore store) : IShoppingListOwner
{
    private static readonly TimeSpan Remember = TimeSpan.FromSeconds(15);
    private readonly Dictionary<string, (string Host, DateTime Until)> hosts = new();

    public async Task<string> GetAsync(string uid)
    {
        if (hosts.TryGetValue(uid, out var known) && known.Until > DateTime.UtcNow) return known.Host;
        string host;
        try { host = (await store.GetSyncLinkAsync(uid))?.ListHost is { Length: > 0 } h ? h : uid; }
        catch { host = uid; }
        hosts[uid] = (host, DateTime.UtcNow + Remember);
        return host;
    }

    public void Forget(string uid) => hosts.Remove(uid);
}

/// <summary>Database paths of a shopping list: users/{owner}/shopping/{shop}/items/{item}.</summary>
internal static class ShoppingPaths
{
    public static string List(string owner) => $"users/{Key(owner)}/shopping";
    public static string Shop(string owner, string shopId) => $"{List(owner)}/{Key(shopId)}";
    public static string Item(string owner, string shopId, string itemId) => $"{Shop(owner, shopId)}/items/{Key(itemId)}";
    /// <summary>
    /// An item's place relative to the list, as a key in a multi-path update on <see cref="List"/>. Keys in a request
    /// body are not URL-escaped.
    /// </summary>
    public static string ItemInList(string shopId, string itemId) => $"{shopId}/items/{itemId}";
    public static string Routes(string owner) => $"users/{Key(owner)}/shoppingRoutes";
    public static string Dismissed(string owner) => $"users/{Key(owner)}/shoppingDismissed";
    public static string User(string owner) => $"users/{Key(owner)}";

    /// <summary>Fills in ids that older data kept only as keys.</summary>
    public static List<ShoppingShop> Read(Dictionary<string, ShoppingShop>? shops) =>
        (shops ?? new()).Select(pair =>
        {
            var shop = pair.Value;
            if (string.IsNullOrWhiteSpace(shop.Id)) shop.Id = pair.Key;
            shop.Items ??= new();
            foreach (var (key, item) in shop.Items)
                if (string.IsNullOrWhiteSpace(item.Id)) item.Id = key;
            return shop;
        }).ToList();

    public static string Name(string value) =>
        value.Trim() is { Length: > 0 } name ? name : throw new ArgumentException("A name is required.", nameof(value));
}

public sealed class FirebaseShoppingListService(FirebaseRest db, IShoppingListOwner owner) : IShoppingListService
{
    public async Task<List<ShoppingShop>> GetShopsAsync(string uid) =>
        ShoppingPaths.Read(await db.GetAsync<Dictionary<string, ShoppingShop>>(ShoppingPaths.List(await owner.GetAsync(uid))));

    public async Task<ShoppingItem> AddItemAsync(string uid, string shopId, string name, ShoppingItemOptions? options = null)
    {
        options ??= new ShoppingItemOptions();
        var item = new ShoppingItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = ShoppingPaths.Name(name),
            Quantity = Math.Max(1, options.Quantity),
            Unit = options.Unit,
            Aisle = options.Aisle,
            SourceMeal = options.SourceMeal,
            IngredientKey = options.IngredientKey,
            UsedIn = options.UsedIn
        };
        await db.PutAsync(ShoppingPaths.Item(await owner.GetAsync(uid), shopId, item.Id), item);
        return item;
    }

    public async Task UpdateItemAsync(string uid, string shopId, string itemId, string name, int quantity, string? unit) =>
        await db.PatchAsync(ShoppingPaths.Item(await owner.GetAsync(uid), shopId, itemId),
            new { name = ShoppingPaths.Name(name), quantity = Math.Max(1, quantity), unit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim() });

    public async Task SetItemBoughtAsync(string uid, string shopId, string itemId, bool isBought) =>
        await db.PatchAsync(ShoppingPaths.Item(await owner.GetAsync(uid), shopId, itemId),
            new { isBought, boughtAtUtc = isBought ? DateTime.UtcNow : (DateTime?)null });

    public async Task UpdateAutoItemAsync(string uid, string shopId, string itemId, int quantity, int usedIn, string? sourceMeal) =>
        await db.PatchAsync(ShoppingPaths.Item(await owner.GetAsync(uid), shopId, itemId),
            new { quantity = Math.Max(1, quantity), usedIn, sourceMeal });

    public async Task DeleteItemAsync(string uid, string shopId, string itemId) =>
        await db.DeleteAsync(ShoppingPaths.Item(await owner.GetAsync(uid), shopId, itemId));

    public async Task RemoveItemsAsync(string uid, IEnumerable<(string ShopId, string ItemId)> items, IReadOnlyDictionary<string, int>? dismiss = null)
    {
        // One multi-path write on the user: the items go and their dismissals are kept together.
        var update = new Dictionary<string, object?>();
        foreach (var (shopId, itemId) in items) update[$"shopping/{ShoppingPaths.ItemInList(shopId, itemId)}"] = null;
        foreach (var (key, amount) in dismiss ?? new Dictionary<string, int>()) update[$"shoppingDismissed/{key}"] = amount;
        if (update.Count > 0) await db.PatchAsync(ShoppingPaths.User(await owner.GetAsync(uid)), update);
    }

    public async Task ClearAsync(string uid, IReadOnlyDictionary<string, int> dismiss)
    {
        var host = await owner.GetAsync(uid);
        var shops = ShoppingPaths.Read(await db.GetAsync<Dictionary<string, ShoppingShop>>(ShoppingPaths.List(host)));
        var update = shops.Where(s => s.Items.Count > 0).ToDictionary(s => $"shopping/{s.Id}/items", _ => (object?)null);
        foreach (var (key, amount) in dismiss) update[$"shoppingDismissed/{key}"] = amount;
        if (update.Count > 0) await db.PatchAsync(ShoppingPaths.User(host), update);
    }

    public async Task RestoreItemsAsync(string uid, IEnumerable<(string ShopId, ShoppingItem Item)> items, IEnumerable<string> undismiss)
    {
        var update = new Dictionary<string, object?>();
        foreach (var (shopId, item) in items) update[$"shopping/{ShoppingPaths.ItemInList(shopId, item.Id)}"] = item;
        foreach (var key in undismiss) update[$"shoppingDismissed/{key}"] = null;
        if (update.Count > 0) await db.PatchAsync(ShoppingPaths.User(await owner.GetAsync(uid)), update);
    }

    public async Task<Dictionary<string, int>> GetDismissedAsync(string uid) =>
        await db.GetAsync<Dictionary<string, int>>(ShoppingPaths.Dismissed(await owner.GetAsync(uid))) ?? new();

    public async Task UndismissAsync(string uid, IEnumerable<string> keys)
    {
        var update = keys.Distinct().ToDictionary(k => k, _ => (object?)null);
        if (update.Count > 0) await db.PatchAsync(ShoppingPaths.Dismissed(await owner.GetAsync(uid)), update);
    }
}

public sealed class FirebaseShoppingStoreService(FirebaseRest db, IShoppingListOwner owner) : IShoppingStoreService
{
    public async Task<ShoppingShop> AddShopAsync(string uid, string name)
    {
        var shop = new ShoppingShop { Id = Guid.NewGuid().ToString("N"), Name = ShoppingPaths.Name(name), CreatedAtUtc = DateTime.UtcNow };
        await db.PutAsync(ShoppingPaths.Shop(await owner.GetAsync(uid), shop.Id), shop);
        return shop;
    }

    public async Task<ShoppingShop> GetOrAddDefaultAsync(string uid, string defaultName)
    {
        var shops = ShoppingPaths.Read(await db.GetAsync<Dictionary<string, ShoppingShop>>(ShoppingPaths.List(await owner.GetAsync(uid))));
        return ShoppingStores.Default(shops) ?? await AddShopAsync(uid, defaultName);
    }

    public async Task RenameShopAsync(string uid, string shopId, string name) =>
        await db.PatchAsync(ShoppingPaths.Shop(await owner.GetAsync(uid), shopId), new { name = ShoppingPaths.Name(name) });

    public async Task DeleteShopAsync(string uid, string shopId, string moveItemsTo)
    {
        if (shopId == moveItemsTo) throw new ArgumentException("A store's items cannot move to itself.", nameof(moveItemsTo));
        var host = await owner.GetAsync(uid);
        var shop = await db.GetAsync<ShoppingShop>(ShoppingPaths.Shop(host, shopId));
        var update = new Dictionary<string, object?> { [shopId] = null };
        foreach (var (key, item) in shop?.Items ?? new())
        {
            if (string.IsNullOrWhiteSpace(item.Id)) item.Id = key;
            update[ShoppingPaths.ItemInList(moveItemsTo, item.Id)] = item;
        }
        await db.PatchAsync(ShoppingPaths.List(host), update);

        // Items remembered for the deleted store go where its items went.
        var routes = await db.GetAsync<Dictionary<string, string>>(ShoppingPaths.Routes(host)) ?? new();
        var moved = routes.Where(r => r.Value == shopId).ToDictionary(r => r.Key, _ => moveItemsTo);
        if (moved.Count > 0) await db.PatchAsync(ShoppingPaths.Routes(host), moved);
    }

    public async Task MoveItemAsync(string uid, string fromShopId, string toShopId, ShoppingItem item)
    {
        if (fromShopId == toShopId) return;
        var host = await owner.GetAsync(uid);
        await db.PatchAsync(ShoppingPaths.List(host), new Dictionary<string, object?>
        {
            [ShoppingPaths.ItemInList(toShopId, item.Id)] = item,
            [ShoppingPaths.ItemInList(fromShopId, item.Id)] = null
        });
        await db.PutAsync($"{ShoppingPaths.Routes(host)}/{Key(StoreRoute.Key(item))}", toShopId);
    }

    public async Task<Dictionary<string, string>> GetStoreRoutesAsync(string uid) =>
        await db.GetAsync<Dictionary<string, string>>(ShoppingPaths.Routes(await owner.GetAsync(uid))) ?? new();

    public async Task SetLookAsync(string uid, string shopId, StoreLook? look) =>
        await db.PatchAsync(ShoppingPaths.Shop(await owner.GetAsync(uid), shopId),
            new { logoDomain = look?.Domain, emoji = look?.Emoji, logoChecked = look is not null });
}
