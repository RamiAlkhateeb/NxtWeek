using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public interface ISuggestionService
{
    Task<List<string>> GetSuggestionsAsync(List<Meal> recentMeals, int count = 5);
}