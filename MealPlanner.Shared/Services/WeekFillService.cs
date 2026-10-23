using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

/// <summary>"Surprise week": puts a suggested meal into every empty Meal 1 (lunch) of the given days.</summary>
public interface IWeekFillService
{
    /// <returns>How many days were filled.</returns>
    Task<int> FillEmptyMeal1Async(string uid, IReadOnlyList<DateOnly> dates, IReadOnlyDictionary<DateOnly, PlanDay> days);
}

public sealed class WeekFillService(ISuggestionService suggestions, IMealCatalogService catalog, IDayPlanService plan) : IWeekFillService
{
    public async Task<int> FillEmptyMeal1Async(string uid, IReadOnlyList<DateOnly> dates, IReadOnlyDictionary<DateOnly, PlanDay> days)
    {
        var empty = dates.Where(d => !days.TryGetValue(d, out var day) || !day.Slots.ContainsKey(MealSlot.Meal1)).ToList();
        if (empty.Count == 0) return 0;

        // The suggester avoids meals already planned this week.
        var planned = days.Values
            .SelectMany(d => d.Slots.Values)
            .Where(e => !string.IsNullOrWhiteSpace(e.MealId))
            .Select(e => new Meal { MealId = e.MealId! })
            .ToList();
        var names = await suggestions.GetSuggestionsAsync(uid, planned, empty.Count);
        if (names.Count == 0) return 0;

        var all = await catalog.GetAllMealsAsync();
        var filled = 0;
        foreach (var (date, name) in empty.Zip(names))
        {
            var item = all.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (item is null) continue;
            await plan.SetEntryAsync(uid, date, MealSlot.Meal1, new SlotEntry { MealId = item.Id });
            filled++;
        }
        return filled;
    }
}
