using System.Net.Http.Json;
using System.Text.Json.Nodes;
using MealPlanner.Shared.Models;
using MealPlanner.Shared.Nutrition;

namespace MealPlanner.Shared.Services;

/// <summary>
/// The free text AI on the photo worker (Cloudflare Workers AI), used when the user has no Gemini key or Gemini fails:
/// POST /details for a meal's ingredients and nutrition, POST /store for a store's logo. Errors are
/// <see cref="MealPhotoException"/> (daily allowance used up, busy, unavailable, failed).
/// </summary>
public interface IWorkerAi
{
    Task<SuggestedMealDetails> SuggestDetailsAsync(MealCatalogItem meal);
    Task<StoreLogoReply> FindStoreAsync(string storeName);
}

public sealed class WorkerAiClient(HttpClient http, MealPhotoOptions options, IAuthService auth) : IWorkerAi
{
    public async Task<SuggestedMealDetails> SuggestDetailsAsync(MealCatalogItem meal)
    {
        var body = await PostAsync("details", user => new
        {
            user,
            mealId = meal.Id,
            name = meal.Name,
            type = meal.MealType.ToString(),
            language = string.IsNullOrWhiteSpace(meal.Language) ? "ar" : meal.Language,
            ingredients = meal.Ingredients.Where(i => !string.IsNullOrWhiteSpace(i)).Take(30).Select(i => i.Trim()).ToList()
        });
        return AiFallback.ParseDetails(body) ?? throw new MealPhotoException(MealPhotoProblem.Failed, "No details in the reply.");
    }

    public async Task<StoreLogoReply> FindStoreAsync(string storeName)
    {
        var body = await PostAsync("store", user => new { user, name = storeName.Trim() });
        return AiFallback.ParseStore(body) ?? throw new MealPhotoException(MealPhotoProblem.Failed, "No store in the reply.");
    }

    private async Task<JsonObject?> PostAsync(string path, Func<string, object> payload)
    {
        var user = (await auth.GetCurrentUserAsync())?.Uid;
        if (string.IsNullOrWhiteSpace(user)) throw new MealPhotoException(MealPhotoProblem.Unavailable, "Not signed in.");
        JsonObject? body;
        int status;
        try
        {
            using var response = await http.PostAsJsonAsync($"{options.WorkerUrl.TrimEnd('/')}/{path}", payload(user));
            status = (int)response.StatusCode;
            try { body = await response.Content.ReadFromJsonAsync<JsonObject>(); } catch { body = null; }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new MealPhotoException(MealPhotoProblem.Unavailable, ex.Message);
        }
        if (status == 200) return body;
        var error = body?["error"] is JsonValue e && e.TryGetValue<string>(out var text) ? text : null;
        throw new MealPhotoException(status switch
        {
            429 when error == "daily_limit" => MealPhotoProblem.DailyLimit,
            429 => MealPhotoProblem.Busy,
            502 => MealPhotoProblem.Failed,
            _ => MealPhotoProblem.Unavailable
        }, error);
    }
}
