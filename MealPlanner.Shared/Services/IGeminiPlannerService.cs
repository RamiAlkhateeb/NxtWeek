using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public sealed record ParsedMealEntry(DateOnly Date, string Name, MealType MealType);
public sealed record ParsedPlanResult(List<ParsedMealEntry> Meals, List<string> ShoppingItems, string ModelUsed);

public interface IGeminiPlannerService
{
    Task<List<string>> ListAvailableModelsAsync();
    Task<ParsedPlanResult> ParsePromptAsync(string prompt);
}
