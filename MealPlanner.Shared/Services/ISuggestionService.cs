using System.Collections.Generic;
using System.Threading.Tasks;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public interface ISuggestionService
{
    Task<List<string>> GetSuggestionsAsync(string username, List<Meal> recentMeals, int count = 5);
}