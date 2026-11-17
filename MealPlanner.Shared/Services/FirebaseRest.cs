using System.Net.Http.Json;

namespace MealPlanner.Shared.Services;

/// <summary>
/// The Firebase Realtime Database REST calls the services share: one place that builds URLs from database paths
/// and fails on an unsuccessful answer. Paths are relative to the root and their keys already escaped
/// (<see cref="Key"/>).
/// </summary>
public sealed class FirebaseRest(HttpClient http, FirebaseOptions options)
{
    private readonly string baseUrl = options.DatabaseUrl.TrimEnd('/');

    public static string Key(string value) => Uri.EscapeDataString(value);

    public string Url(string path) => $"{baseUrl}/{path}.json";

    /// <param name="query">A query string without "?", e.g. <c>shallow=true</c> or an <c>orderBy</c> key range.</param>
    public Task<T?> GetAsync<T>(string path, string? query = null) =>
        http.GetFromJsonAsync<T>(string.IsNullOrEmpty(query) ? Url(path) : $"{Url(path)}?{query}");

    public async Task PutAsync<T>(string path, T value)
    {
        using var response = await http.PutAsJsonAsync(Url(path), value);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Changes several children of <paramref name="path"/> at once; keys may be deeper paths and a null value deletes.
    /// Firebase applies a multi-path update atomically, so nothing ever sees half of it.
    /// </summary>
    public async Task PatchAsync<T>(string path, T value)
    {
        using var response = await http.PatchAsJsonAsync(Url(path), value);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(string path)
    {
        using var response = await http.DeleteAsync(Url(path));
        response.EnsureSuccessStatusCode();
    }
}
