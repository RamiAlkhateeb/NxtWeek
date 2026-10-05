using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

/// <summary>
/// A conversational cooking assistant on top of the Gemini API, mirroring the sibling
/// task-breaker app's GeminiService: try the user's selected model, fall back through a
/// known-good list on failure, and force structured JSON back via response_schema. Each
/// reply carries a chat message plus an optional proposal (meals and/or shopping items)
/// that the page shows behind an explicit "Add" button.
/// </summary>
public class GeminiPlannerService : IGeminiPlannerService
{
    private const int MaxPastDays = 1;
    private const int MaxFutureDays = 60;

    private static readonly string[] FallbackModels =
    [
        "gemini-2.0-flash",
        "gemini-2.0-flash-lite",
        "gemini-1.5-flash",
        "gemini-1.5-pro"
    ];

    private readonly HttpClient _http;
    private readonly IAiSettingsService _settings;
    private readonly ILocalizationService _loc;

    public GeminiPlannerService(HttpClient http, IAiSettingsService settings, ILocalizationService loc)
    {
        _http = http;
        _settings = settings;
        _loc = loc;
    }

    public async Task<List<string>> ListAvailableModelsAsync()
    {
        var apiKey = await _settings.GetGeminiApiKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey)) return [];

        try
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={Uri.EscapeDataString(apiKey)}";
            using var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return [];

            var result = await response.Content.ReadFromJsonAsync<ModelListResponse>();
            return result?.Models?
                .Where(model => model.SupportedGenerationMethods?.Contains("generateContent", StringComparer.OrdinalIgnoreCase) == true)
                .Select(model => model.Name?.Replace("models/", "", StringComparison.OrdinalIgnoreCase))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return [];
        }
    }

    public async Task<AssistantReply> ChatAsync(IReadOnlyList<ChatTurn> history, string planContext)
    {
        var apiKey = await _settings.GetGeminiApiKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("No Gemini API key set. Add one in Settings.");
        if (history.Count == 0 || !history[^1].IsUser)
            throw new ArgumentException("The conversation must end with a user message.", nameof(history));

        var modelsToTry = await GetModelsToTryAsync();
        Exception? lastError = null;

        foreach (var model in modelsToTry)
        {
            try
            {
                return await ChatCoreAsync(history, planContext, model, apiKey);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException(
            "None of the configured Gemini models could answer. Check your API key, model access, and connection.",
            lastError);
    }

    private async Task<IEnumerable<string>> GetModelsToTryAsync()
    {
        var selectedModel = await _settings.GetGeminiModelAsync();
        var availableModels = await ListAvailableModelsAsync();
        var knownFallbacks = availableModels.Count == 0
            ? FallbackModels
            : FallbackModels.Where(model => availableModels.Contains(model, StringComparer.OrdinalIgnoreCase));
        return new[] { selectedModel }.Concat(knownFallbacks).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<AssistantReply> ChatCoreAsync(IReadOnlyList<ChatTurn> history, string planContext, string model, string apiKey)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(apiKey)}";

        var body = new
        {
            system_instruction = new { parts = new[] { new { text = BuildSystemInstruction(today, planContext) } } },
            contents = history.Select(turn => new
            {
                role = turn.IsUser ? "user" : "model",
                parts = new[] { new { text = turn.Text } }
            }).ToArray(),
            generationConfig = new
            {
                response_mime_type = "application/json",
                response_schema = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        message = new { type = "STRING" },
                        meals = new
                        {
                            type = "ARRAY",
                            items = new
                            {
                                type = "OBJECT",
                                properties = new
                                {
                                    date = new { type = "STRING" },
                                    name = new { type = "STRING" },
                                    mealType = new { type = "STRING", @enum = new[] { "Meat", "Chicken", "Fish", "Vegetarian", "Vegan" } },
                                    ingredients = new { type = "ARRAY", items = new { type = "STRING" } }
                                },
                                required = new[] { "date", "name", "mealType" }
                            }
                        },
                        shoppingItems = new { type = "ARRAY", items = new { type = "STRING" } }
                    },
                    required = new[] { "message", "meals", "shoppingItems" }
                }
            }
        };

        using var response = await _http.PostAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GeminiResponse>();
        var text = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text
            ?? throw new InvalidOperationException("Gemini returned no content.");
        var parsed = JsonSerializer.Deserialize<ChatResultDto>(text)
            ?? throw new InvalidOperationException("Gemini returned an invalid response.");

        var message = parsed.Message?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(message))
            throw new InvalidOperationException("Gemini returned an empty reply.");

        var meals = ValidateMeals(parsed.Meals ?? [], today);
        var shoppingItems = CleanList(parsed.ShoppingItems);
        var proposal = meals.Count == 0 && shoppingItems.Count == 0 ? null : new AssistantProposal(meals, shoppingItems);

        return new AssistantReply(message, proposal, model);
    }

    private string BuildSystemInstruction(DateOnly today, string planContext)
    {
        var languageName = _loc.AvailableLanguages.FirstOrDefault(l => l.Code == _loc.CurrentLanguage)?.NativeName ?? _loc.CurrentLanguage;

        return $"""
            You are the friendly home-cooking assistant inside "Makdous", a family weekly meal planner.
            You help the user decide what to cook, plan meals onto days, and keep a shopping list.

            Today is {today:yyyy-MM-dd} ({today.DayOfWeek}).
            Always reply in {languageName} (language code: {_loc.CurrentLanguage}), including dish and item names,
            using everyday food vocabulary a home cook would use.

            The user's plan for the coming days (FREE means nothing is planned yet):
            {(string.IsNullOrWhiteSpace(planContext) ? "(unknown)" : planContext)}

            How to behave:
            - Talk like a helpful friend: short, warm replies (2 to 5 sentences). Discuss before acting.
            - When the user tells you what ingredients they have, suggest 2 to 4 dishes that mostly use them,
              say in a few words what each one still needs (if anything), and ask which ones they like and for
              which days. Do not add anything yet.
            - If something important is missing (which dish, which day, dietary needs), ask ONE short question
              instead of guessing. Don't ask more than you need.
            - Prefer FREE days when the user hasn't said which day. Mention when a day already has a meal and
              ask before replacing it.

            The "meals" and "shoppingItems" fields are a PROPOSAL shown to the user with an "Add" button.
            Nothing is saved until they tap it.
            - Leave both lists EMPTY while you are still suggesting options, asking questions, or chatting.
            - Fill them only when the user has picked or explicitly asked for specific dishes or items. Then
              say in "message" exactly what you'd add and that they can tap Add to confirm.
            - Each meal needs an explicit ISO date (yyyy-MM-dd) on or after today, a name, a mealType
              (exactly one of: Meat, Chicken, Fish, Vegetarian, Vegan) and its main ingredients.
            - Put an ingredient into "shoppingItems" only if the user asked for it or agreed to buy what is
              missing — never assume they want everything bought.
            - A new proposal replaces any earlier one, so repeat everything that should still be added.

            Earlier assistant turns may end with a bracketed system note such as "[Proposal shown: ...]" or
            "[User added this]". Those notes are written by the app, not by you — use them to know what was
            proposed and what was already added, but never write such notes yourself.
            Politely steer off-topic requests back to cooking, meals and shopping.
            """;
    }

    private static List<ParsedMealEntry> ValidateMeals(List<ParsedMealDto> dtos, DateOnly today)
    {
        var meals = new List<ParsedMealEntry>();
        foreach (var dto in dtos)
        {
            var name = dto.Name?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (!DateOnly.TryParseExact(dto.Date, "yyyy-MM-dd", out var date)) continue;
            if (date < today.AddDays(-MaxPastDays) || date > today.AddDays(MaxFutureDays)) continue;

            var mealType = Enum.TryParse<MealType>(dto.MealType, ignoreCase: true, out var parsedType)
                ? parsedType
                : MealType.Vegetarian;

            meals.Add(new ParsedMealEntry(date, name, mealType, CleanList(dto.Ingredients)));
        }

        return meals;
    }

    private static List<string> CleanList(List<string>? values) =>
        values?
            .Select(value => value?.Trim() ?? "")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

    private sealed class ModelListResponse { [JsonPropertyName("models")] public List<GeminiModel>? Models { get; set; } }
    private sealed class GeminiModel
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("supportedGenerationMethods")] public List<string>? SupportedGenerationMethods { get; set; }
    }
    private sealed class ChatResultDto
    {
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("meals")] public List<ParsedMealDto>? Meals { get; set; } = [];
        [JsonPropertyName("shoppingItems")] public List<string>? ShoppingItems { get; set; } = [];
    }
    private sealed class ParsedMealDto
    {
        [JsonPropertyName("date")] public string Date { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("mealType")] public string MealType { get; set; } = "";
        [JsonPropertyName("ingredients")] public List<string>? Ingredients { get; set; } = [];
    }
    private sealed class GeminiResponse { [JsonPropertyName("candidates")] public List<Candidate>? Candidates { get; set; } }
    private sealed class Candidate { [JsonPropertyName("content")] public Content? Content { get; set; } }
    private sealed class Content { [JsonPropertyName("parts")] public List<Part>? Parts { get; set; } }
    private sealed class Part { [JsonPropertyName("text")] public string? Text { get; set; } }
}
