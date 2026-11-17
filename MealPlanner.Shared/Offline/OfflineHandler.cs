using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MealPlanner.Shared.Services;
using Microsoft.JSInterop;

namespace MealPlanner.Shared.Offline;

/// <summary>Whether the database could not be reached on the last try, and how many changes wait to be sent.</summary>
public sealed class OfflineState
{
    public bool IsOffline { get; private set; }
    public int Pending { get; private set; }
    public event Action? Changed;

    internal void Set(bool offline, int pending)
    {
        if (offline == IsOffline && pending == Pending) return;
        IsOffline = offline;
        Pending = pending;
        Changed?.Invoke();
    }
}

/// <summary>
/// Lets the app keep working without a connection. Database reads are remembered on the device; when the
/// network fails a remembered read is answered instead, with the changes still waiting laid over it. Writes that
/// fail for lack of network go into an outbox in localStorage and are sent in order once the database answers
/// again (before the next write, on the next start, and from <see cref="FlushAsync"/>). Health data and photos
/// are never kept or queued (<see cref="OfflineMerge.MayKeep"/>). Requests to anything but the database pass through.
/// </summary>
public sealed class OfflineHandler(IJSRuntime js, FirebaseOptions options, OfflineState state) : DelegatingHandler
{
    public const string OutboxKey = "nxtweek.offline.outbox";
    public const string CachePrefix = "nxtweek.offline.get:";
    private const int MaxCachedChars = 600_000;

    private readonly Uri root = new(options.DatabaseUrl);
    private readonly SemaphoreSlim gate = new(1, 1);
    private List<PendingWrite>? outbox;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri is { } uri ? OfflineMerge.PathOf(uri, root) : null;
        if (path is null || !OfflineMerge.MayKeep(path) || request.Method == HttpMethod.Post)
            return await base.SendAsync(request, cancellationToken);

        return request.Method == HttpMethod.Get
            ? await ReadAsync(request, path, cancellationToken)
            : await WriteAsync(request, path, cancellationToken);
    }

    private async Task<HttpResponseMessage> ReadAsync(HttpRequestMessage request, string path, CancellationToken token)
    {
        var key = CachePrefix + path + request.RequestUri!.Query;
        var pending = await OutboxAsync();
        if (pending.Count > 0) await FlushAsync(token);
        try
        {
            var response = await base.SendAsync(request, token);
            if (response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NoContent)
            {
                var text = await response.Content.ReadAsStringAsync(token);
                if (text.Length <= MaxCachedChars) await StoreAsync(key, text);
                var waiting = await OutboxAsync();
                if (waiting.Count > 0) text = Merge(text, path, request.RequestUri.Query, waiting);
                var copy = Json(text);
                copy.Headers.Date = response.Headers.Date;
                response.Dispose();
                Report(false);
                return copy;
            }
            Report(false);
            return response;
        }
        catch (HttpRequestException) when (!token.IsCancellationRequested)
        {
            Report(true);
            var cached = await GetAsync(key) ?? await FromAncestorAsync(path, request.RequestUri.Query);
            var waiting = await OutboxAsync();
            if (cached is null && !waiting.Any(w => Related(w.Path, path))) throw;
            return Json(Merge(cached ?? "null", path, request.RequestUri.Query, waiting));
        }
    }

    private async Task<HttpResponseMessage> WriteAsync(HttpRequestMessage request, string path, CancellationToken token)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(token);
        if ((await OutboxAsync()).Count > 0 && !await FlushAsync(token))
            return await QueueAsync(request, path, body);
        try
        {
            var response = await base.SendAsync(Clone(request.Method, request.RequestUri!, body), token);
            Report(false);
            return response;
        }
        catch (HttpRequestException) when (!token.IsCancellationRequested)
        {
            Report(true);
            return await QueueAsync(request, path, body);
        }
    }

    /// <summary>Sends the waiting changes in order. True when none are left.</summary>
    public async Task<bool> FlushAsync(CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try
        {
            var list = await OutboxAsync();
            while (list.Count > 0)
            {
                var next = list[0];
                try
                {
                    using var response = await base.SendAsync(Clone(new HttpMethod(next.Method), new Uri(next.Url), next.Body), token);
                    // A refused write (4xx) would be refused forever; it is dropped rather than block the rest.
                    if ((int)response.StatusCode >= 500) { Report(false); return false; }
                }
                catch (HttpRequestException)
                {
                    Report(true);
                    return false;
                }
                list.RemoveAt(0);
                await SaveOutboxAsync();
            }
            Report(false);
            return true;
        }
        finally { gate.Release(); }
    }

    /// <summary>Reads the outbox from the device (once) so the banner can show what is waiting after a restart.</summary>
    public async Task<int> PendingAsync() { var list = await OutboxAsync(); Report(state.IsOffline, list.Count); return list.Count; }

    private async Task<HttpResponseMessage> QueueAsync(HttpRequestMessage request, string path, string? body)
    {
        var list = await OutboxAsync();
        list.Add(new PendingWrite { Method = request.Method.Method, Path = path, Url = request.RequestUri!.ToString(), Body = body, At = DateTime.UtcNow });
        await SaveOutboxAsync();
        Report(true);
        var echo = Json(request.Method == HttpMethod.Delete ? "null" : body ?? "null");
        echo.Headers.Add("X-Makdous-Queued", "1");
        return echo;
    }

    private static string Merge(string text, string path, string query, List<PendingWrite> waiting)
    {
        try { return OfflineMerge.Apply(JsonNode.Parse(text), path, query, waiting)?.ToJsonString() ?? "null"; }
        catch (JsonException) { return text; }
    }

    private static bool Related(string a, string b) => a == b || a.StartsWith(b + "/", StringComparison.Ordinal) || b.StartsWith(a + "/", StringComparison.Ordinal);

    private static HttpRequestMessage Clone(HttpMethod method, Uri url, string? body) => new(method, url)
    {
        Content = body is null ? null : new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage Json(string text) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(text, Encoding.UTF8, "application/json")
    };

    private void Report(bool offline, int? pending = null) => state.Set(offline, pending ?? outbox?.Count ?? 0);

    private async Task<List<PendingWrite>> OutboxAsync()
    {
        if (outbox is not null) return outbox;
        try
        {
            var text = await js.InvokeAsync<string?>("localStorage.getItem", OutboxKey);
            outbox = text is null ? new() : JsonSerializer.Deserialize<List<PendingWrite>>(text) ?? new();
        }
        catch { outbox = new(); }
        return outbox;
    }

    private async Task SaveOutboxAsync()
    {
        try
        {
            if (outbox is { Count: > 0 }) await js.InvokeVoidAsync("localStorage.setItem", OutboxKey, JsonSerializer.Serialize(outbox));
            else await js.InvokeVoidAsync("localStorage.removeItem", OutboxKey);
        }
        catch { /* storage full or blocked: the change still waits in memory */ }
    }

    private async Task<string?> GetAsync(string key)
    {
        try { return await js.InvokeAsync<string?>("localStorage.getItem", key); }
        catch { return null; }
    }

    // A read never made online, answered from a remembered read that contains it (for example one day of a week).
    private async Task<string?> FromAncestorAsync(string path, string query)
    {
        try
        {
            var candidates = await js.InvokeAsync<string[][]>("nxtweek.offlineAncestors", CachePrefix, path);
            foreach (var pair in candidates.OrderByDescending(c => c[0].Split('?')[0].Length))
            {
                var rest = pair[0][CachePrefix.Length..];
                var q = rest.IndexOf('?');
                var (ancestor, ancestorQuery) = q < 0 ? (rest, "") : (rest[..q], rest[q..]);
                if (OfflineMerge.TryFromCached(path, query, ancestor, ancestorQuery, JsonNode.Parse(pair[1]), out var value))
                    return value?.ToJsonString() ?? "null";
            }
        }
        catch { }
        return null;
    }

    private async Task StoreAsync(string key, string text)
    {
        try { await js.InvokeVoidAsync("nxtweek.offlineStore", CachePrefix, key, text); }
        catch { /* the cache is a convenience */ }
    }
}
