using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

/// <summary>Fills the empty days of a week with suggested meals ("Surprise me").</summary>
public interface IWeekFillService
{
    /// <returns>How many days were filled.</returns>
    Task<int> FillEmptyDaysAsync(string uid, IReadOnlyList<Meal> weekMeals);
}

public sealed class WeekFillService(ISuggestionService suggestions, IMealCatalogService catalog, IUserService users) : IWeekFillService
{
    public async Task<int> FillEmptyDaysAsync(string uid, IReadOnlyList<Meal> weekMeals)
    {
        var emptyDays = weekMeals.Where(m => string.IsNullOrWhiteSpace(m.MealId)).ToList();
        if (emptyDays.Count == 0) return 0;

        var names = await suggestions.GetSuggestionsAsync(uid, weekMeals.ToList(), emptyDays.Count);
        if (names.Count == 0) return 0;

        var all = await catalog.GetAllMealsAsync();
        var filled = 0;
        foreach (var (day, name) in emptyDays.Zip(names))
        {
            var item = all.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (item is null) continue;
            await users.AssignMealToDateAsync(uid, day.Date, item.Id);
            filled++;
        }

        return filled;
    }
}
