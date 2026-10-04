using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public sealed record ParsedMealEntry(DateOnly Date, string Name, MealType MealType);
public sealed record ParsedPlanResult(List<ParsedMealEntry> Meals, List<string> ShoppingItems);

public interface IGeminiPlannerService
{
    Task<ParsedPlanResult> ParsePromptAsync(string prompt);
}
