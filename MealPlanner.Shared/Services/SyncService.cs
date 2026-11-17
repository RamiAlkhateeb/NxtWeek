using MealPlanner.Shared.Models;
using MealPlanner.Shared.Nutrition;

namespace MealPlanner.Shared.Services;

/// <summary>What accepting a sync request would change: the requester and how many of my days take their meals.</summary>
public sealed record SyncPreview(SyncRequest Request, int DaysChanged);

/// <summary>
/// Starting and stopping a sync between two friends. A request waits at users/{partner}/syncRequest (and
/// users/{me}/syncOutgoing) until the friend accepts or declines; one partner at a time.
/// </summary>
public interface ISyncService
{
    Task<SyncLink?> GetLinkAsync(string uid);
    Task<SyncRequest?> GetIncomingAsync(string uid);
    Task<string?> GetOutgoingAsync(string uid);
    /// <summary>Asks a friend to sync. Throws <see cref="SyncException"/> when either side already syncs.</summary>
    Task RequestAsync(string uid, string displayName, string partner);
    Task CancelAsync(string uid);
    Task DeclineAsync(string uid);
    Task<SyncPreview?> PreviewAcceptAsync(string uid, DateOnly today);
    /// <summary>
    /// Accepts the waiting request: my meals from today on become the requester's (my eaten and skipped
    /// entries and quick adds stay), my open shopping items join their list, and both sides are linked.
    /// </summary>
    Task AcceptAsync(string uid, DateOnly today);
    /// <summary>Stops syncing for both. The one who used the partner's list gets a copy of its open items.</summary>
    Task StopAsync(string uid);
}

public enum SyncProblem { AlreadySyncing, PartnerSyncing, NoRequest }

public sealed class SyncException(SyncProblem problem) : Exception(problem.ToString())
{
    public SyncProblem Problem { get; } = problem;
}

public sealed class FirebaseSyncService(FirebaseRest db, INutritionStore store, IShoppingListOwner listOwner) : ISyncService
{
    /// <summary>How far ahead a starting sync takes over the requester's meals.</summary>
    public const int AdoptDays = 56;

    private static string Key(string value) => FirebaseRest.Key(value);

    public Task<SyncLink?> GetLinkAsync(string uid) => store.GetSyncLinkAsync(uid);

    public Task<SyncRequest?> GetIncomingAsync(string uid) =>
        db.GetAsync<SyncRequest?>($"users/{Key(uid)}/syncRequest");

    public Task<string?> GetOutgoingAsync(string uid) =>
        db.GetAsync<string?>($"users/{Key(uid)}/syncOutgoing");

    public async Task RequestAsync(string uid, string displayName, string partner)
    {
        if (await GetLinkAsync(uid) is not null) throw new SyncException(SyncProblem.AlreadySyncing);
        if (await GetLinkAsync(partner) is not null) throw new SyncException(SyncProblem.PartnerSyncing);
        await CancelAsync(uid); // one request at a time
        await PutAsync($"users/{Key(partner)}/syncRequest", new SyncRequest { From = uid, FromName = displayName });
        await PutAsync($"users/{Key(uid)}/syncOutgoing", partner);
    }

    public async Task CancelAsync(string uid)
    {
        if (await GetOutgoingAsync(uid) is { Length: > 0 } partner
            && await GetIncomingAsync(partner) is { } request && request.From == uid)
            await DeleteAsync($"users/{Key(partner)}/syncRequest");
        await DeleteAsync($"users/{Key(uid)}/syncOutgoing");
    }

    public async Task DeclineAsync(string uid)
    {
        if (await GetIncomingAsync(uid) is { } request && await GetOutgoingAsync(request.From) == uid)
            await DeleteAsync($"users/{Key(request.From)}/syncOutgoing");
        await DeleteAsync($"users/{Key(uid)}/syncRequest");
    }

    public async Task<SyncPreview?> PreviewAcceptAsync(string uid, DateOnly today)
    {
        if (await GetIncomingAsync(uid) is not { From.Length: > 0 } request) return null;
        var (theirs, mine) = await DaysAsync(request.From, uid, today);
        return new SyncPreview(request, SyncMerge.Adopt(theirs, mine, today, today.AddDays(AdoptDays)).Count);
    }

    public async Task AcceptAsync(string uid, DateOnly today)
    {
        if (await GetIncomingAsync(uid) is not { From.Length: > 0 } request) throw new SyncException(SyncProblem.NoRequest);
        var host = request.From;
        if (await GetLinkAsync(uid) is not null) throw new SyncException(SyncProblem.AlreadySyncing);
        if (await GetLinkAsync(host) is not null) throw new SyncException(SyncProblem.PartnerSyncing);

        var (theirs, mine) = await DaysAsync(host, uid, today);
        foreach (var day in SyncMerge.Adopt(theirs, mine, today, today.AddDays(AdoptDays)))
            await store.SaveDayAsync(uid, day);

        // My open items join the shared list; my own list stays as it was underneath.
        await CopyOpenItemsAsync(from: uid, to: host);

        await PutAsync($"users/{Key(host)}/sync", new SyncLink { Partner = uid, ListHost = host, Since = today });
        await PutAsync($"users/{Key(uid)}/sync", new SyncLink { Partner = host, ListHost = host, Since = today });
        await DeleteAsync($"users/{Key(uid)}/syncRequest");
        await DeleteAsync($"users/{Key(host)}/syncOutgoing");
        listOwner.Forget(uid);
        listOwner.Forget(host);
    }

    public async Task StopAsync(string uid)
    {
        if (await GetLinkAsync(uid) is not { Partner.Length: > 0 } link) return;
        var guest = link.ListHost == uid ? link.Partner : uid;
        try { await CopyOpenItemsAsync(from: link.ListHost, to: guest); }
        catch { /* the link is removed anyway; the shared list is still there with its host */ }
        await DeleteAsync($"users/{Key(link.Partner)}/sync");
        await DeleteAsync($"users/{Key(uid)}/sync");
        listOwner.Forget(uid);
        listOwner.Forget(link.Partner);
    }

    private async Task<(Dictionary<DateOnly, PlanDay> Theirs, Dictionary<DateOnly, PlanDay> Mine)> DaysAsync(string them, string me, DateOnly today)
    {
        var theirs = store.GetDaysAsync(them, today, today.AddDays(AdoptDays));
        var mine = store.GetDaysAsync(me, today, today.AddDays(AdoptDays));
        await Task.WhenAll(theirs, mine);
        return (await theirs, await mine);
    }

    // Copies the open items of one person's own list into another's first store, skipping names already open there.
    private async Task CopyOpenItemsAsync(string from, string to)
    {
        var source = await ReadShopsAsync(from);
        var items = source.Values.SelectMany(s => s.Items?.Values ?? Enumerable.Empty<ShoppingItem>()).Where(i => !i.IsBought).ToList();
        if (items.Count == 0) return;
        var target = await ReadShopsAsync(to);
        var open = target.Values.SelectMany(s => s.Items?.Values ?? Enumerable.Empty<ShoppingItem>()).Where(i => !i.IsBought)
            .Select(i => i.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var (shopId, shop) = target.FirstOrDefault();
        if (shop is null)
        {
            shop = new ShoppingShop { Id = Guid.NewGuid().ToString("N"), Name = source.Values.First().Name, CreatedAtUtc = DateTime.UtcNow };
            shopId = shop.Id;
            await PutAsync($"users/{Key(to)}/shopping/{shopId}", shop);
        }
        foreach (var item in items.Where(i => !open.Contains(i.Name.Trim())))
        {
            var copy = new ShoppingItem
            {
                Id = Guid.NewGuid().ToString("N"), Name = item.Name, Quantity = item.Quantity, Unit = item.Unit, Aisle = item.Aisle,
                SourceMeal = item.SourceMeal, IngredientKey = item.IngredientKey, UsedIn = item.UsedIn
            };
            await PutAsync($"users/{Key(to)}/shopping/{Key(shopId)}/items/{copy.Id}", copy);
        }
    }

    private async Task<Dictionary<string, ShoppingShop>> ReadShopsAsync(string key) =>
        await db.GetAsync<Dictionary<string, ShoppingShop>>($"users/{Key(key)}/shopping") ?? new();

    private Task PutAsync<T>(string path, T value) => db.PutAsync(path, value);

    private Task DeleteAsync(string path) => db.DeleteAsync(path);
}
