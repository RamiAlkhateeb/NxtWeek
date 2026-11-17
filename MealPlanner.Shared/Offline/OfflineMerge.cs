using System.Text.Json.Nodes;

namespace MealPlanner.Shared.Offline;

/// <summary>A change made without a connection, waiting to be sent: PUT, PATCH or DELETE on a database path.</summary>
public sealed class PendingWrite
{
    public string Method { get; set; } = "PUT";
    /// <summary>Unescaped path below the database root, without ".json", for example "users/a/days/2026-11-17".</summary>
    public string Path { get; set; } = "";
    /// <summary>The full request URL, sent again as it was.</summary>
    public string Url { get; set; } = "";
    public string? Body { get; set; }
    public DateTime At { get; set; }
}

/// <summary>
/// Pure rules for working offline: which paths may be kept on the device, and what a cached read looks like
/// once the changes still waiting to be sent are laid over it, so the app shows its own edits while offline.
/// </summary>
public static class OfflineMerge
{
    // Health data and photos are never kept on the device or queued: they need the connection, as before.
    private static readonly string[] PrivateSegments = ["body", "nutritionProfile", "progressPhotos", "mealPhotos"];

    public static bool MayKeep(string path)
    {
        var segments = Segments(path);
        if (segments.Length == 0) return false;
        if (segments[0] is "MealPhotos" or "PhotoQuota") return false;
        return segments[0] == "users"
            ? segments.Length >= 2 && !segments.Skip(2).Any(s => PrivateSegments.Contains(s))
            : true;
    }

    /// <summary>The path below the database root for a request URL, unescaped and without ".json".</summary>
    public static string? PathOf(Uri url, Uri databaseRoot)
    {
        if (!string.Equals(url.Host, databaseRoot.Host, StringComparison.OrdinalIgnoreCase)) return null;
        var path = url.AbsolutePath;
        if (!path.EndsWith(".json", StringComparison.Ordinal)) return null;
        path = path[..^5].Trim('/');
        return string.Join('/', path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.UnescapeDataString));
    }

    /// <summary>
    /// Lays the waiting writes, oldest first, over a cached read of <paramref name="path"/>. Understands the two
    /// queries the app uses: <c>shallow=true</c> and <c>orderBy="$key"</c> with <c>startAt</c>/<c>endAt</c>.
    /// </summary>
    public static JsonNode? Apply(JsonNode? cached, string path, string query, IEnumerable<PendingWrite> writes)
    {
        var target = Segments(path);
        var node = cached?.DeepClone();
        var shallow = query.Contains("shallow=true", StringComparison.Ordinal);
        var changed = false;
        foreach (var write in writes)
        {
            var at = Segments(write.Path);
            var body = write.Method == "DELETE" || write.Body is null ? null : JsonNode.Parse(write.Body);
            if (StartsWith(at, target))
            {
                var rest = at[target.Length..];
                node = write.Method == "PATCH" ? Patch(node, rest, body) : Set(node, rest, body);
                changed = true;
            }
            else if (StartsWith(target, at))
            {
                var rest = target[at.Length..];
                if (write.Method == "PATCH")
                {
                    // A multi-path patch on an ancestor: each key may itself be a path.
                    foreach (var (key, value) in body as JsonObject ?? new JsonObject())
                    {
                        var keyPath = Segments(key);
                        if (StartsWith(rest, keyPath)) node = Get(value, rest[keyPath.Length..])?.DeepClone();
                        else if (StartsWith(keyPath, rest)) node = Set(node, keyPath[rest.Length..], value?.DeepClone());
                        else continue;
                        changed = true;
                    }
                }
                else
                {
                    node = Get(body, rest)?.DeepClone();
                    changed = true;
                }
            }
        }
        if (!changed) return node;
        node = Filter(node, query);
        if (shallow && node is JsonObject obj)
            return new JsonObject(obj.Select(p => KeyValuePair.Create(p.Key, (JsonNode?)JsonValue.Create(true))));
        return node;
    }

    /// <summary>
    /// Answers a read (<paramref name="path"/> with <paramref name="query"/>) from another remembered read that
    /// contains it: one of its ancestors (a day from the loaded week), or the same path with a wider key range.
    /// False when the remembered read cannot say (a shallow read, a range that does not cover it, other queries).
    /// </summary>
    public static bool TryFromCached(string path, string query, string cachedPath, string cachedQuery, JsonNode? cached, out JsonNode? value)
    {
        value = null;
        var target = Segments(path);
        var top = Segments(cachedPath);
        if (!StartsWith(target, top)) return false;
        var want = Query(query);
        var have = Query(cachedQuery);
        if (have.ContainsKey("shallow") || want.ContainsKey("shallow")) return false;
        if (have.Keys.Concat(want.Keys).Any(k => k is not ("orderBy" or "startAt" or "endAt"))) return false;
        if (have.TryGetValue("orderBy", out var o1) && o1 != "$key") return false;
        if (want.TryGetValue("orderBy", out var o2) && o2 != "$key") return false;

        if (target.Length == top.Length)
        {
            // The same path: the remembered key range must cover the one asked for.
            if (have.Count == 0 && want.Count == 0) return false; // that would be the same read
            if (!Covers(have, want)) return false;
            value = Filter(cached?.DeepClone(), query);
            return true;
        }
        if (want.Count > 0) return false;
        var child = target[top.Length];
        if (have.TryGetValue("startAt", out var start) && string.CompareOrdinal(child, start) < 0) return false;
        if (have.TryGetValue("endAt", out var end) && string.CompareOrdinal(child, end) > 0) return false;
        value = Get(cached, target[top.Length..])?.DeepClone();
        return true;
    }

    private static bool Covers(Dictionary<string, string> have, Dictionary<string, string> want)
    {
        if (have.Count == 0) return true;
        have.TryGetValue("startAt", out var hs); have.TryGetValue("endAt", out var he);
        want.TryGetValue("startAt", out var ws); want.TryGetValue("endAt", out var we);
        if (hs is not null && (ws is null || string.CompareOrdinal(ws, hs) < 0)) return false;
        if (he is not null && (we is null || string.CompareOrdinal(we, he) > 0)) return false;
        return true;
    }

    private static Dictionary<string, string> Query(string query) =>
        query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=', 2))
            .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => p.Length > 1 ? Uri.UnescapeDataString(p[1]).Trim('"') : "");

    private static JsonNode? Filter(JsonNode? node, string query)
    {
        if (node is not JsonObject obj || !query.Contains("orderBy", StringComparison.Ordinal)) return node;
        var parts = Query(query);
        if (!parts.TryGetValue("orderBy", out var orderBy) || orderBy != "$key") return node;
        parts.TryGetValue("startAt", out var start);
        parts.TryGetValue("endAt", out var end);
        foreach (var key in obj.Select(p => p.Key).ToList())
            if ((start is { Length: > 0 } && string.CompareOrdinal(key, start) < 0) || (end is { Length: > 0 } && string.CompareOrdinal(key, end) > 0))
                obj.Remove(key);
        return obj.Count == 0 ? null : obj;
    }

    private static JsonNode? Patch(JsonNode? node, string[] at, JsonNode? body)
    {
        foreach (var (key, value) in body as JsonObject ?? new JsonObject())
            node = Set(node, [.. at, .. Segments(key)], value?.DeepClone());
        return node;
    }

    private static JsonNode? Set(JsonNode? node, string[] at, JsonNode? value)
    {
        if (at.Length == 0) return value is JsonObject { Count: 0 } ? null : value;
        var obj = node as JsonObject ?? new JsonObject();
        var child = obj[at[0]]?.DeepClone();
        obj.Remove(at[0]);
        var next = Set(child, at[1..], value);
        if (next is not null) obj[at[0]] = next;
        return obj.Count == 0 ? null : obj;
    }

    private static JsonNode? Get(JsonNode? node, string[] at)
    {
        foreach (var segment in at)
            node = node is JsonObject obj && obj.TryGetPropertyValue(segment, out var child) ? child : null;
        return node;
    }

    private static string[] Segments(string path) => path.Split('/', StringSplitOptions.RemoveEmptyEntries);

    private static bool StartsWith(string[] path, string[] prefix) =>
        path.Length >= prefix.Length && prefix.Select((s, i) => s == path[i]).All(x => x);
}
