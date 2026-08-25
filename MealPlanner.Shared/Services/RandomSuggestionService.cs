using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public class RandomSuggestionService : ISuggestionService
{
    private readonly IMealCatalogService _catalogService;
    private readonly IUserService _userService;

    public RandomSuggestionService(IMealCatalogService catalogService, IUserService userService)
    {
        _catalogService = catalogService;
        _userService = userService;
    }

    public async Task<List<string>> GetSuggestionsAsync(string username, List<Meal> recentMeals, int count = 5)
    {
        // 1. Fetch user profile. A profile is optional for suggestions; it only
        // supplies the user's favorite meal IDs.
        var profile = await _userService.GetProfileAsync(username);

        // 2. Fetch all catalog items
        var allCatalog = await _catalogService.GetAllMealsAsync();
        if (allCatalog.Count == 0) return new List<string>();

        // 3. Use the shared catalog as the candidate pool. Restricting this to a
        // user's historical selection can make suggestions alternate between two meals.
        var favoriteMealIds = profile?.FavoriteMealIds ?? new();
        var eligible = allCatalog.Where(m => !m.IsArchived).ToList();

        // 4. Prefer meals not already used in the supplied period. IDs avoid
        // false matches caused by casing or whitespace differences in names.
        var usedMealIds = recentMeals
            .Where(m => !string.IsNullOrWhiteSpace(m.MealId))
            .Select(m => m.MealId)
            .ToHashSet(StringComparer.Ordinal);
        var nonRecent = eligible.Where(m => !usedMealIds.Contains(m.Id)).ToList();
        
        // If everything was filtered out, revert to eligible
        if (nonRecent.Count == 0)
        {
            nonRecent = eligible;
        }

        // 5. Build weighted pool: favorites appear 3x more often
        var weightedPool = new List<MealCatalogItem>();
        foreach (var meal in nonRecent)
        {
            var isFavorite = favoriteMealIds.Contains(meal.Id);
            var weight = isFavorite ? 3 : 1;
            for (int i = 0; i < weight; i++)
            {
                weightedPool.Add(meal);
            }
        }

        // 6. Randomly select distinct catalog entries.
        var rng = Random.Shared;
        var result = new List<string>();
        var remainingPool = weightedPool.ToList();

        while (result.Count < count && remainingPool.Count > 0)
        {
            var idx = rng.Next(remainingPool.Count);
            var selected = remainingPool[idx];
            
            if (!result.Contains(selected.Name))
            {
                result.Add(selected.Name);
            }
            
            // Remove all instances of this meal from the temporary pool to avoid repeating the same meal in suggestions
            remainingPool.RemoveAll(m => m.Id == selected.Id);
        }

        return result;
    }
}
