using System.Text.Json.Serialization;
using MealPlanner.Shared.Models;
using Nxt.UI.Ai;

namespace MealPlanner.Shared.Services;

/// <summary>
/// Parses a free-text prompt ("add pizza for Tuesday, put milk on my list") into explicit meal
/// assignments and shopping-list items. Transport, model fallback and structured JSON come from the
/// family's shared <see cref="GeminiClient"/>; this class only owns the meal-planning prompt and validation.
/// </summary>
public class GeminiPlannerService : IGeminiPlannerService
{
    private const int MaxPastDays = 1;
    private const int MaxFutureDays = 60;

    private readonly GeminiClient _gemini;
    private readonly ILocalizationService _loc;

    public GeminiPlannerService(GeminiClient gemini, ILocalizationService loc)
    {
        _gemini = gemini;
        _loc = loc;
    }

    public async Task<ParsedPlanResult> ParsePromptAsync(string prompt, IReadOnlyList<string>? currentShoppingItems = null)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var languageName = _loc.AvailableLanguages.FirstOrDefault(l => l.Code == _loc.CurrentLanguage)?.NativeName ?? _loc.CurrentLanguage;

        var currentList = currentShoppingItems is { Count: > 0 }
            ? string.Join("\n", currentShoppingItems.Take(300).Select(item => $"- {item}"))
            : "(the list is empty)";

        var fullPrompt = $"""
            Today's date is {today:yyyy-MM-dd} ({today.DayOfWeek}). Resolve relative dates
            ("tomorrow", "Friday", "next week") against this date and return explicit ISO 8601
            dates (yyyy-MM-dd). Never return a date before today unless the user names a past
            date explicitly.

            Respond in this language: {languageName} (code: {_loc.CurrentLanguage}). Meal names
            and shopping item names in your response must be written in that language, using
            natural everyday food vocabulary a home cook would use.

            Pull out three independent things from the user's request:
            - "meals": dishes to add to specific days, each with an explicit ISO date, a name,
              and a mealType (exactly one of: Meat, Chicken, Fish, Vegetarian, Vegan).
            - "shoppingItems": plain grocery items to add to a shopping list.
            - "removeShoppingItems": items the user wants taken OFF or deleted from the shopping
              list ("remove", "delete", "take off", "clear"). Each entry must be copied exactly,
              character for character, from the current list below. Only include items that are
              on that list. To clear the whole list, return every item on it.

            Any list may be empty if the request doesn't mention that kind of thing.

            Current shopping list:
            {currentList}

            User request: {prompt}
            """;

        var schema = new
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
                shoppingItems = new { type = "ARRAY", items = new { type = "STRING" } },
                removeShoppingItems = new { type = "ARRAY", items = new { type = "STRING" } }
            },
            required = new[] { "meals", "shoppingItems", "removeShoppingItems" }
        };

        ParsedPlanResult? result = null;
        await _gemini.GenerateJsonAsync<ParseResultDto>(fullPrompt, schema, parsed =>
        {
            var meals = ValidateMeals(parsed.Meals, today);
            var shoppingItems = parsed.ShoppingItems
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();

            var removals = parsed.RemoveShoppingItems
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();

            if (meals.Count == 0 && shoppingItems.Count == 0 && removals.Count == 0)
                throw new InvalidOperationException("Gemini could not find any meals or shopping items in that request.");

            result = new ParsedPlanResult(meals, shoppingItems, removals);
            return parsed;
        });
        return result!;
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

    private sealed class ParseResultDto
    {
        [JsonPropertyName("meals")] public List<ParsedMealDto> Meals { get; set; } = [];
        [JsonPropertyName("shoppingItems")] public List<string> ShoppingItems { get; set; } = [];
        [JsonPropertyName("removeShoppingItems")] public List<string> RemoveShoppingItems { get; set; } = [];
    }
    private sealed class ParsedMealDto
    {
        [JsonPropertyName("date")] public string Date { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("mealType")] public string MealType { get; set; } = "";
    }
}
