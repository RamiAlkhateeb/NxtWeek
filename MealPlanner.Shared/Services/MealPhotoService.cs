using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MealPlanner.Shared.Models;
using Microsoft.JSInterop;

namespace MealPlanner.Shared.Services;

/// <summary>
/// AI-generated meal photos, shared by everyone: one small JPEG data URL per meal at MealPhotos/{mealId}, kept
/// apart from the catalog so the catalog download stays small. Which meals have a photo is read once with a
/// shallow request; each photo is fetched only when a meal is shown, and kept in memory for the session.
/// New photos are drawn by the Makdous photo worker (Cloudflare Workers AI, free, no key; see photo-worker/).
/// A user's own private photo (<see cref="IMealImageService"/>) always wins over the shared one.
/// </summary>
public interface IMealPhotoService
{
    /// <summary>The shared photo of a meal, or null when there is none (or it cannot be read).</summary>
    Task<string?> GetAsync(string mealId);
    /// <summary>Whether a shared photo exists for the meal (from the one-request index; nothing is downloaded).</summary>
    Task<bool> HasAsync(string mealId);
    /// <summary>Draws a photo with the photo worker, shrinks it to 480 px and shares it. Throws <see cref="MealPhotoException"/>.</summary>
    Task<string> GenerateAsync(MealCatalogItem meal);
    /// <summary>Raised with the meal id when a photo was generated, so every picture of that meal updates.</summary>
    event Action<string>? Changed;
}

public enum MealPhotoProblem
{
    /// <summary>Today's free allowance is used up; try again tomorrow.</summary>
    DailyLimit,
    /// <summary>Too many photos asked for in a minute.</summary>
    Busy,
    /// <summary>The model could not draw this dish.</summary>
    Failed,
    /// <summary>The photo service could not be reached (offline, not deployed yet).</summary>
    Unavailable
}

public sealed class MealPhotoException(MealPhotoProblem problem, string? detail = null)
    : Exception(detail ?? problem.ToString())
{
    public MealPhotoProblem Problem { get; } = problem;

    /// <summary>The translation key that explains the problem to the user.</summary>
    public static string MessageKey(Exception ex) => ex is MealPhotoException p
        ? p.Problem switch
        {
            MealPhotoProblem.DailyLimit => "nut_photo_err_daily",
            MealPhotoProblem.Busy => "nut_photo_err_busy",
            MealPhotoProblem.Failed => "nut_photo_err_failed",
            _ => "nut_photo_err_unavailable"
        }
        : "nut_photo_err_unavailable";
}

public sealed class MealPhotoOptions
{
    public string WorkerUrl { get; set; } = "https://makdous-photos.rami13195.workers.dev";
}

public sealed class MealPhotoService(HttpClient http, FirebaseOptions options, MealPhotoOptions photoOptions, IJSRuntime js) : IMealPhotoService
{
    public const int Size = 480;

    private readonly string baseUrl = options.DatabaseUrl.TrimEnd('/');
    private readonly Dictionary<string, string?> photos = new();
    private readonly Dictionary<string, Task<string?>> loading = new();
    private Task<HashSet<string>>? index;

    public event Action<string>? Changed;

    private string Url(string path) => $"{baseUrl}/{path}.json";

    public async Task<string?> GetAsync(string mealId)
    {
        if (string.IsNullOrWhiteSpace(mealId)) return null;
        if (photos.TryGetValue(mealId, out var known)) return known;
        if (!(await IndexAsync()).Contains(mealId)) return null;
        if (!loading.TryGetValue(mealId, out var task)) loading[mealId] = task = FetchAsync(mealId);
        return await task;
    }

    public async Task<bool> HasAsync(string mealId) => photos.TryGetValue(mealId, out var p) && p is not null || (await IndexAsync()).Contains(mealId);

    private Task<HashSet<string>> IndexAsync() => index ??= LoadIndexAsync();

    private async Task<HashSet<string>> LoadIndexAsync()
    {
        try
        {
            var shallow = await http.GetFromJsonAsync<JsonObject>($"{Url("MealPhotos")}?shallow=true");
            return shallow?.Select(child => child.Key).ToHashSet() ?? [];
        }
        catch
        {
            index = null; // try again next time
            return [];
        }
    }

    private async Task<string?> FetchAsync(string mealId)
    {
        try
        {
            var url = await http.GetFromJsonAsync<string?>(Url($"MealPhotos/{Uri.EscapeDataString(mealId)}"));
            url = url is not null && url.StartsWith("data:image/", StringComparison.Ordinal) ? url : null;
            photos[mealId] = url;
            return url;
        }
        catch { return null; }
        finally { loading.Remove(mealId); }
    }

    public async Task<string> GenerateAsync(MealCatalogItem meal)
    {
        string raw;
        try
        {
            using var response = await http.PostAsJsonAsync($"{photoOptions.WorkerUrl.TrimEnd('/')}/photo", new { mealId = meal.Id });
            var body = await ReadAsync(response);
            var image = Text(body, "image");
            var status = (int)response.StatusCode;
            if (status == 200 && string.IsNullOrEmpty(image)) status = 502;
            switch (status)
            {
                case 200:
                    raw = image!;
                    break;
                case 409:
                    // Someone else drew it a moment ago: show theirs.
                    photos.Remove(meal.Id);
                    index = null;
                    return await GetAsync(meal.Id) ?? throw new MealPhotoException(MealPhotoProblem.Failed);
                case 429:
                    throw new MealPhotoException(Text(body, "error") == "daily_limit" ? MealPhotoProblem.DailyLimit : MealPhotoProblem.Busy);
                case 502:
                    throw new MealPhotoException(MealPhotoProblem.Failed, Text(body, "detail"));
                default:
                    throw new MealPhotoException(MealPhotoProblem.Unavailable, $"{status} {Text(body, "error")}");
            }
        }
        catch (HttpRequestException ex) { throw new MealPhotoException(MealPhotoProblem.Unavailable, ex.Message); }
        catch (TaskCanceledException ex) { throw new MealPhotoException(MealPhotoProblem.Unavailable, ex.Message); }

        var small = await js.InvokeAsync<string?>("nxtweek.squareJpeg", raw, Size)
            ?? throw new MealPhotoException(MealPhotoProblem.Failed, "The picture could not be read.");
        using (var saved = await http.PutAsJsonAsync(Url($"MealPhotos/{Uri.EscapeDataString(meal.Id)}"), small))
            saved.EnsureSuccessStatusCode();
        photos[meal.Id] = small;
        (await IndexAsync()).Add(meal.Id);
        Changed?.Invoke(meal.Id);
        return small;
    }

    private static string? Text(JsonObject? body, string name) =>
        body is not null && body.TryGetPropertyValue(name, out var node) && node is JsonValue v && v.TryGetValue<string>(out var text) ? text : null;

    private static async Task<JsonObject?> ReadAsync(HttpResponseMessage response)
    {
        try { return await response.Content.ReadFromJsonAsync<JsonObject>(); }
        catch { return null; }
    }
}
