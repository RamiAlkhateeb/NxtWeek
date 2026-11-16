using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MealPlanner.Shared.Models;
using Microsoft.JSInterop;
using Nxt.UI.Ai;

namespace MealPlanner.Shared.Services;

/// <summary>
/// AI-generated meal photos, shared by everyone: one small JPEG data URL per meal at MealPhotos/{mealId}, kept
/// apart from the catalog so the catalog download stays small. Which meals have a photo is read once with a
/// shallow request; each photo is fetched only when a meal is shown, and kept in memory for the session.
/// A user's own private photo (<see cref="IMealImageService"/>) always wins over the shared one.
/// </summary>
public interface IMealPhotoService
{
    /// <summary>The shared photo of a meal, or null when there is none (or it cannot be read).</summary>
    Task<string?> GetAsync(string mealId);
    /// <summary>Whether a shared photo exists for the meal (from the one-request index; nothing is downloaded).</summary>
    Task<bool> HasAsync(string mealId);
    /// <summary>Whether this user can generate photos at all (a Gemini key is saved).</summary>
    Task<bool> CanGenerateAsync();
    /// <summary>Draws a photo with the user's Gemini key, shrinks it to 480 px and shares it. Throws GeminiApiException.</summary>
    Task<string> GenerateAsync(MealCatalogItem meal);
    /// <summary>Raised with the meal id when a photo was generated, so every picture of that meal updates.</summary>
    event Action<string>? Changed;
}

public sealed class MealPhotoService(HttpClient http, FirebaseOptions options, GeminiClient gemini, IJSRuntime js) : IMealPhotoService
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

    public Task<bool> CanGenerateAsync() => gemini.HasKeyAsync();

    public async Task<string> GenerateAsync(MealCatalogItem meal)
    {
        var image = await gemini.GenerateImageAsync(Prompt(meal));
        var raw = $"data:{image.MimeType};base64,{Convert.ToBase64String(image.Data)}";
        var small = await js.InvokeAsync<string?>("nxtweek.squareJpeg", raw, Size)
            ?? throw new GeminiApiException(GeminiErrorKind.Other, image.Model, null, "The picture could not be read.");
        using var response = await http.PutAsJsonAsync(Url($"MealPhotos/{Uri.EscapeDataString(meal.Id)}"), small);
        response.EnsureSuccessStatusCode();
        photos[meal.Id] = small;
        (await IndexAsync()).Add(meal.Id);
        Changed?.Invoke(meal.Id);
        return small;
    }

    /// <summary>One plated serving, photographed from above, nothing else in the picture.</summary>
    public static string Prompt(MealCatalogItem meal)
    {
        var ingredients = meal.IngredientLines.Count > 0
            ? string.Join(", ", meal.IngredientLines.Select(l => l.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Take(8))
            : string.Join(", ", meal.Ingredients.Where(i => !string.IsNullOrWhiteSpace(i)).Take(8));
        return $"A realistic, appetising food photograph of the dish \"{meal.Name.Trim()}\""
            + (ingredients.Length > 0 ? $" (made with {ingredients})" : "")
            + ". One serving, plated as it is usually served, seen from slightly above, soft natural daylight, "
            + "plain light background, square framing with the dish in the centre. "
            + "No text, no labels, no logos, no hands, no people.";
    }
}
