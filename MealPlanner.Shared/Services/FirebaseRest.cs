using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MealPlanner.Shared.Services;

/// <summary>
/// The one way the app talks to the Firebase Realtime Database: REST calls on paths relative to the root, with
/// keys already escaped by <see cref="Key"/>. Methods named <c>Try…</c> report failure as false or null; the others
/// throw <see cref="HttpRequestException"/> on an unsuccessful answer. Every request goes through the app's
/// <see cref="HttpClient"/>, so the offline handler sees it.
/// </summary>
public sealed class FirebaseRest(HttpClient http, FirebaseOptions options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string baseUrl = options.DatabaseUrl.TrimEnd('/');

    /// <summary>Escapes one key for a URL path. Keys inside a request body (multi-path updates) are not escaped.</summary>
    public static string Key(string value) => Uri.EscapeDataString(value);

    /// <summary>The REST URL of a path (the empty path is the root).</summary>
    public string Url(string path) => $"{baseUrl}/{path}.json";

    /// <param name="query">A query string without "?", e.g. <c>shallow=true</c> or an <c>orderBy</c> key range.</param>
    public Task<T?> GetAsync<T>(string path, string? query = null) =>
        http.GetFromJsonAsync<T>(string.IsNullOrEmpty(query) ? Url(path) : $"{Url(path)}?{query}", Json);

    /// <summary>A node as raw JSON, so nothing under it is lost to a model; null when it is missing or the read fails.</summary>
    public async Task<JsonObject?> TryGetNodeAsync(string path)
    {
        try
        {
            using var response = await http.GetAsync(Url(path));
            if (!response.IsSuccessStatusCode) return null;
            var text = await response.Content.ReadAsStringAsync();
            return string.IsNullOrWhiteSpace(text) || text == "null" ? null : JsonNode.Parse(text) as JsonObject;
        }
        catch (HttpRequestException) { return null; }
    }

    /// <summary>Reads a value with its ETag, for a write that must not overwrite a change made meanwhile.</summary>
    public async Task<(T? Value, string ETag)> GetWithETagAsync<T>(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Url(path));
        request.Headers.TryAddWithoutValidation("X-Firebase-ETag", "true");
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync();
        var value = string.IsNullOrWhiteSpace(text) || text == "null" ? default : JsonSerializer.Deserialize<T>(text, Json);
        return (value, response.Headers.ETag?.Tag ?? "null_etag");
    }

    /// <summary>Writes only if the value is unchanged since it was read with <paramref name="etag"/>. False when it changed.</summary>
    public async Task<bool> TryPutIfMatchAsync<T>(string path, T value, string etag)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, Url(path)) { Content = JsonContent.Create(value, options: Json) };
        request.Headers.TryAddWithoutValidation("if-match", etag);
        using var response = await http.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    public async Task PutAsync<T>(string path, T value)
    {
        if (!await TryPutAsync(path, value)) throw new HttpRequestException($"Writing {path} failed.");
    }

    public async Task<bool> TryPutAsync<T>(string path, T value)
    {
        using var response = await http.PutAsJsonAsync(Url(path), value, Json);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Changes several children of <paramref name="path"/> at once (the empty path is the root); keys may be deeper
    /// paths and a null value deletes. Firebase applies a multi-path update atomically, so nothing ever sees half of it.
    /// </summary>
    public async Task PatchAsync<T>(string path, T value)
    {
        if (!await TryPatchAsync(path, value)) throw new HttpRequestException($"Updating {(path.Length == 0 ? "the root" : path)} failed.");
    }

    public async Task<bool> TryPatchAsync<T>(string path, T value)
    {
        using var response = await http.PatchAsJsonAsync(Url(path), value, Json);
        return response.IsSuccessStatusCode;
    }

    public async Task DeleteAsync(string path)
    {
        using var response = await http.DeleteAsync(Url(path));
        response.EnsureSuccessStatusCode();
    }
}
