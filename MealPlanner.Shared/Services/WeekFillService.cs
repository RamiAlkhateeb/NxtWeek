using MealPlanner.Shared.Models;
using MealPlanner.Shared.Nutrition;

namespace MealPlanner.Shared.Services;

/// <summary>"Surprise week": puts a suggested meal into every empty Meal 1 (lunch) of the given days.</summary>
public interface IWeekFillService
{
    /// <returns>How many days were filled.</returns>
    Task<int> FillEmptyMeal1Async(string uid, IReadOnlyList<DateOnly> dates, IReadOnlyDictionary<DateOnly, PlanDay> days);
}

public sealed class WeekFillService(IMealCatalogService catalog, IUserService users, IDayPlanService plan, ILocalizationService loc) : IWeekFillService
{
    public async Task<int> FillEmptyMeal1Async(string uid, IReadOnlyList<DateOnly> dates, IReadOnlyDictionary<DateOnly, PlanDay> days)
    {
        var empty = dates.Where(d => !days.TryGetValue(d, out var day) || !day.Slots.ContainsKey(MealSlot.Meal1)).ToList();
        if (empty.Count == 0) return 0;

        var planned = days.Values.SelectMany(d => d.Slots.Values).Select(e => e.MealId)
            .OfType<string>().Where(id => id.Length > 0).ToHashSet(StringComparer.Ordinal);
        var favourites = (await users.GetProfileAsync(uid))?.FavoriteMealIds.ToHashSet() ?? [];
        var picks = SurprisePicks.Pick(await catalog.GetAllMealsAsync(), loc.CurrentLanguage, favourites, planned, empty.Count, Random.Shared);

        foreach (var (date, meal) in empty.Zip(picks))
            await plan.SetEntryAsync(uid, date, MealSlot.Meal1, new SlotEntry { MealId = meal.Id });
        return Math.Min(empty.Count, picks.Count);
    }
}
