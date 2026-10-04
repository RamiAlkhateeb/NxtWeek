using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public sealed record ParsedMealEntry(DateOnly Date, string Name, MealType MealType);
public sealed record ParsedPlanResult(List<ParsedMealEntry> Meals, List<string> ShoppingItems, List<string> RemoveShoppingItems);

public interface IGeminiPlannerService
{
    /// <param name="currentShoppingItems">Names already on the user's shopping list. The model is told
    /// to return removals using these exact names, so "take milk off" can be matched reliably.</param>
    Task<ParsedPlanResult> ParsePromptAsync(string prompt, IReadOnlyList<string>? currentShoppingItems = null);
}
