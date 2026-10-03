using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

/// <summary>
/// Parses a free-text prompt ("add pizza for Tuesday, put milk on my list") into
/// explicit meal assignments and shopping-list items via the Gemini API, mirroring
/// the sibling task-breaker app's GeminiService: try the user's selected model,
/// fall back through a known-good list on failure, and force structured JSON back
/// via generationConfig.response_schema instead of parsing free text ourselves.
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

    public async Task<ParsedPlanResult> ParsePromptAsync(string prompt)
    {
        var apiKey = await _settings.GetGeminiApiKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("No Gemini API key set. Add one in Settings.");

        var modelsToTry = await GetModelsToTryAsync();
        Exception? lastError = null;

        foreach (var model in modelsToTry)
        {
            try
            {
                return await ParsePromptCoreAsync(prompt, model, apiKey);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
            {
                lastError = ex;
            }
        }

        throw new InvalidOperationException(
            "None of the configured Gemini models could parse that. Check your API key, model access, and connection.",
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

    private async Task<ParsedPlanResult> ParsePromptCoreAsync(string prompt, string model, string apiKey)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var languageName = _loc.AvailableLanguages.FirstOrDefault(l => l.Code == _loc.CurrentLanguage)?.NativeName ?? _loc.CurrentLanguage;

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(apiKey)}";
        var fullPrompt = $"""
            Today's date is {today:yyyy-MM-dd} ({today.DayOfWeek}). Resolve relative dates
            ("tomorrow", "Friday", "next week") against this date and return explicit ISO 8601
            dates (yyyy-MM-dd). Never return a date before today unless the user names a past
            date explicitly.

            Respond in this language: {languageName} (code: {_loc.CurrentLanguage}). Meal names
            and shopping item names in your response must be written in that language, using
            natural everyday food vocabulary a home cook would use.

            Pull out two independent things from the user's request:
            - "meals": dishes to add to specific days, each with an explicit ISO date, a name,
              and a mealType (exactly one of: Meat, Chicken, Fish, Vegetarian, Vegan).
            - "shoppingItems": plain grocery items to add to a shopping list.

            Either list may be empty if the request doesn't mention that kind of thing.

            User request: {prompt}
            """;

        var body = new
        {
            contents = new[] { new { parts = new[] { new { text = fullPrompt } } } },
            generationConfig = new
            {
                response_mime_type = "application/json",
                response_schema = new
                {
                    type = "OBJECT",
                    properties = new
                    {
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
                                    mealType = new { type = "STRING", @enum = new[] { "Meat", "Chicken", "Fish", "Vegetarian", "Vegan" } }
                                },
                                required = new[] { "date", "name", "mealType" }
                            }
                        },
                        shoppingItems = new { type = "ARRAY", items = new { type = "STRING" } }
                    },
                    required = new[] { "meals", "shoppingItems" }
                }
            }
        };

        using var response = await _http.PostAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GeminiResponse>();
        var text = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text
            ?? throw new InvalidOperationException("Gemini returned no content.");
        var parsed = JsonSerializer.Deserialize<ParseResultDto>(text)
            ?? throw new InvalidOperationException("Gemini returned an invalid response.");

        var meals = ValidateMeals(parsed.Meals, today);
        var shoppingItems = parsed.ShoppingItems
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();

        if (meals.Count == 0 && shoppingItems.Count == 0)
            throw new InvalidOperationException("Gemini could not find any meals or shopping items in that request.");

        return new ParsedPlanResult(meals, shoppingItems, model);
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

            meals.Add(new ParsedMealEntry(date, name, mealType));
        }

        return meals;
    }

    private sealed class ModelListResponse { [JsonPropertyName("models")] public List<GeminiModel>? Models { get; set; } }
    private sealed class GeminiModel
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("supportedGenerationMethods")] public List<string>? SupportedGenerationMethods { get; set; }
    }
    private sealed class ParseResultDto
    {
        [JsonPropertyName("meals")] public List<ParsedMealDto> Meals { get; set; } = [];
        [JsonPropertyName("shoppingItems")] public List<string> ShoppingItems { get; set; } = [];
    }
    private sealed class ParsedMealDto
    {
        [JsonPropertyName("date")] public string Date { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("mealType")] public string MealType { get; set; } = "";
    }
    private sealed class GeminiResponse { [JsonPropertyName("candidates")] public List<Candidate>? Candidates { get; set; } }
    private sealed class Candidate { [JsonPropertyName("content")] public Content? Content { get; set; } }
    private sealed class Content { [JsonPropertyName("parts")] public List<Part>? Parts { get; set; } }
    private sealed class Part { [JsonPropertyName("text")] public string? Text { get; set; } }
}
