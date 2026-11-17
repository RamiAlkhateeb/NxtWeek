using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

/// <summary>
/// Recipe steps for a meal, shared by everyone in the catalog (MealCatalog/{id}/steps). They are typed by hand or
/// fetched for free from the photo worker's text model (POST /steps), which writes them in the meal's own language.
/// Errors are <see cref="MealPhotoException"/>, shown with <see cref="MealPhotoException.Describe"/>.
/// </summary>
public interface IRecipeService
{
    Task<List<RecipeStep>> FetchStepsAsync(MealCatalogItem meal, bool replace = false);
    Task SaveStepsAsync(MealCatalogItem meal, List<RecipeStep> steps);
}

public sealed class RecipeService(HttpClient http, MealPhotoOptions options, IAuthService auth, IMealCatalogService catalog) : IRecipeService
{
    public async Task<List<RecipeStep>> FetchStepsAsync(MealCatalogItem meal, bool replace = false)
    {
        var user = (await auth.GetCurrentUserAsync())?.Uid;
        if (string.IsNullOrWhiteSpace(user)) throw new MealPhotoException(MealPhotoProblem.Unavailable, "Not signed in.");
        JsonObject? body;
        int status;
        try
        {
            using var response = await http.PostAsJsonAsync($"{options.WorkerUrl.TrimEnd('/')}/steps", new { mealId = meal.Id, user, replace });
            status = (int)response.StatusCode;
            try { body = await response.Content.ReadFromJsonAsync<JsonObject>(); } catch { body = null; }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new MealPhotoException(MealPhotoProblem.Unavailable, ex.Message);
        }

        if (status == 200 && body?["steps"] is JsonArray list)
        {
            var steps = list.Select(s => new RecipeStep
            {
                Text = s?["text"]?.GetValue<string>()?.Trim() ?? "",
                Minutes = s?["minutes"] is JsonValue m && m.TryGetValue<int>(out var minutes) && minutes > 0 ? minutes : null
            }).Where(s => s.Text.Length > 0).ToList();
            if (steps.Count > 0)
            {
                await SaveStepsAsync(meal, steps);
                return steps;
            }
        }
        var error = body?["error"] is JsonValue e && e.TryGetValue<string>(out var text) ? text : null;
        throw new MealPhotoException(status switch
        {
            429 when error == "daily_limit" => MealPhotoProblem.DailyLimit,
            429 => MealPhotoProblem.Busy,
            502 => MealPhotoProblem.Failed,
            _ => MealPhotoProblem.Unavailable
        }, error);
    }

    public async Task SaveStepsAsync(MealCatalogItem meal, List<RecipeStep> steps)
    {
        await catalog.SaveStepsAsync(meal.Id, steps);
        meal.Steps = steps;
    }
}
