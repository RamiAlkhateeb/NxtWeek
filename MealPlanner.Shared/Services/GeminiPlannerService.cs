using System.Text.Json.Serialization;
using MealPlanner.Shared.Models;
using Nxt.UI.Ai;

namespace MealPlanner.Shared.Services;

/// <summary>
/// The meal-planning side of the family AI assistant. Transport, model fallback and structured JSON
/// come from the shared <see cref="GeminiClient"/>; this class owns the cooking-assistant prompt and
/// validation. The shared client is single-turn, so the conversation is passed as a transcript.
/// </summary>
public class GeminiPlannerService : IGeminiPlannerService
{
    private const int MaxPastDays = 1;
    private const int MaxFutureDays = 60;
    private const int MaxTranscriptTurns = 20;

    private readonly GeminiClient _gemini;
    private readonly ILocalizationService _loc;

    public GeminiPlannerService(GeminiClient gemini, ILocalizationService loc)
    {
        _gemini = gemini;
        _loc = loc;
    }

    public async Task<AssistantReply> ChatAsync(IReadOnlyList<ChatTurn> history, PlanChanges? pending, string planContext,
        IReadOnlyList<string> currentShoppingItems)
    {
        if (history.Count == 0 || !history[^1].IsUser)
            throw new ArgumentException("The conversation must end with a user message.", nameof(history));

        var today = DateOnly.FromDateTime(DateTime.Now);
        var prompt = BuildPrompt(history, pending, planContext, currentShoppingItems, today);

        var schema = new
        {
            type = "OBJECT",
            properties = new
            {
                message = new { type = "STRING" },
                action = new { type = "STRING", @enum = new[] { "chat", "propose", "apply", "cancel" } },
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
                shoppingItems = new { type = "ARRAY", items = new { type = "STRING" } },
                removeShoppingItems = new { type = "ARRAY", items = new { type = "STRING" } }
            },
            required = new[] { "message", "action", "meals", "shoppingItems", "removeShoppingItems" }
        };

        AssistantReply? reply = null;
        await _gemini.GenerateJsonAsync<ChatResultDto>(prompt, schema, parsed =>
        {
            var message = parsed.Message?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(message))
                throw new InvalidOperationException("Gemini returned an empty reply.");

            var changes = new PlanChanges(
                ValidateMeals(parsed.Meals ?? [], today),
                CleanList(parsed.ShoppingItems),
                CleanList(parsed.RemoveShoppingItems));

            var action = parsed.Action?.Trim().ToLowerInvariant() switch
            {
                "propose" when !changes.IsEmpty => AssistantAction.Propose,
                "apply" => AssistantAction.Apply,
                "cancel" => AssistantAction.Cancel,
                _ => AssistantAction.Chat
            };

            reply = new AssistantReply(message, action, changes);
            return parsed;
        });
        return reply!;
    }

    private string BuildPrompt(IReadOnlyList<ChatTurn> history, PlanChanges? pending, string planContext,
        IReadOnlyList<string> currentShoppingItems, DateOnly today)
    {
        var languageName = _loc.AvailableLanguages.FirstOrDefault(l => l.Code == _loc.CurrentLanguage)?.NativeName ?? _loc.CurrentLanguage;
        var shoppingList = currentShoppingItems.Count > 0
            ? string.Join("\n", currentShoppingItems.Take(300).Select(item => $"- {item}"))
            : "(the list is empty)";
        var transcript = string.Join("\n\n", history.TakeLast(MaxTranscriptTurns)
            .Select(turn => $"{(turn.IsUser ? "USER" : "ASSISTANT")}: {turn.Text}"));

        return $"""
            You are the friendly home-cooking assistant inside "Makdous", a family weekly meal planner.
            You help the user decide what to cook, plan meals onto days, and keep their shopping list.

            Today is {today:yyyy-MM-dd} ({today.DayOfWeek}). Resolve relative dates ("tomorrow", "Friday",
            "next week") against this date and always give explicit ISO dates (yyyy-MM-dd), never before today.
            Always reply in {languageName} (language code: {_loc.CurrentLanguage}), including dish and item
            names, using everyday food vocabulary a home cook would use.

            The user's plan for the coming days (FREE means nothing is planned yet):
            {(string.IsNullOrWhiteSpace(planContext) ? "(unknown)" : planContext)}

            Current shopping list:
            {shoppingList}

            Proposal waiting for the user's answer:
            {DescribePending(pending)}

            HOW TO BEHAVE
            - Talk like a helpful friend: short, warm replies (2 to 5 sentences). Discuss before acting.
            - When the user tells you which ingredients they have, suggest 2 to 4 dishes that mostly use
              them, say in a few words what each still needs (if anything), and ask which they like and
              for which days.
            - If something important is missing (which dish, which day, dietary needs), ask ONE short
              question instead of guessing.
            - Prefer FREE days when the user hasn't said which day. Mention when a day already has a meal
              and ask before replacing it.
            - Politely steer off-topic requests back to cooking, meals and shopping.

            THE "action" FIELD — nothing is saved unless you return "apply"
            - "chat": you are suggesting options, asking a question or just talking. Leave all lists empty.
            - "propose": the user picked or asked for specific dishes/items. Fill the lists with the
              COMPLETE set of changes (it replaces any earlier proposal) and in "message" say briefly what
              you would do and ask them to confirm. Do this even for direct requests like "add pizza on
              Friday" — always confirm first.
            - "apply": ONLY when a proposal is waiting AND the user's latest message clearly agrees to it
              ("yes", "ok", "add them", "go ahead"). Repeat that proposal's lists unchanged. If they agree
              but change something ("yes, but without the parsley"), use "propose" with the updated set.
            - "cancel": the user turns the waiting proposal down. Leave the lists empty.

            LIST RULES
            - "meals": each needs an ISO date, a name, a mealType (exactly one of: Meat, Chicken, Fish,
              Vegetarian, Vegan) and its main ingredients.
            - "shoppingItems": items to ADD. Only things the user asked for or agreed to buy — never
              assume they want every missing ingredient bought.
            - "removeShoppingItems": items to take OFF the list. Copy each name exactly from the current
              shopping list above; only include items that are on it.

            CONVERSATION SO FAR (lines in [brackets] are notes from the app, never write them yourself):
            {transcript}
            """;
    }

    private static string DescribePending(PlanChanges? pending)
    {
        if (pending is null || pending.IsEmpty) return "(none)";

        var parts = new List<string>();
        if (pending.Meals.Count > 0)
            parts.Add("meals: " + string.Join("; ", pending.Meals.Select(m => $"{m.Date:yyyy-MM-dd} {m.Name} ({m.MealType})")));
        if (pending.ShoppingItems.Count > 0)
            parts.Add("add to shopping list: " + string.Join(", ", pending.ShoppingItems));
        if (pending.RemoveShoppingItems.Count > 0)
            parts.Add("remove from shopping list: " + string.Join(", ", pending.RemoveShoppingItems));
        return string.Join("\n", parts);
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

    private sealed class ChatResultDto
    {
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("action")] public string? Action { get; set; }
        [JsonPropertyName("meals")] public List<ParsedMealDto>? Meals { get; set; } = [];
        [JsonPropertyName("shoppingItems")] public List<string>? ShoppingItems { get; set; } = [];
        [JsonPropertyName("removeShoppingItems")] public List<string>? RemoveShoppingItems { get; set; } = [];
    }
    private sealed class ParsedMealDto
    {
        [JsonPropertyName("date")] public string Date { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("mealType")] public string MealType { get; set; } = "";
        [JsonPropertyName("ingredients")] public List<string>? Ingredients { get; set; } = [];
    }
}
