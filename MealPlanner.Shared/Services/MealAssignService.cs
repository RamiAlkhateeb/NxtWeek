using MealPlanner.Shared.Models;
using MealPlanner.Shared.Nutrition;

namespace MealPlanner.Shared.Services;

/// <summary>Puts a catalog meal into a slot of a day (Meals grid, meal details, assistant, Surprise week).</summary>
public interface IMealAssignService
{
    /// <param name="slot">The slot to fill; when null, the first empty slot the meal suits.</param>
    /// <returns>The slot the meal went into.</returns>
    Task<MealSlot> AssignAsync(string uid, DateOnly date, MealCatalogItem meal, MealSlot? slot = null);
}

public sealed class MealAssignService(IDayPlanService plan, INutritionStore store) : IMealAssignService
{
    public async Task<MealSlot> AssignAsync(string uid, DateOnly date, MealCatalogItem meal, MealSlot? slot = null)
    {
        var target = slot ?? await PickSlotAsync(uid, date, meal);
        await plan.SetEntryAsync(uid, date, target, new SlotEntry { MealId = meal.Id });
        return target;
    }

    private async Task<MealSlot> PickSlotAsync(string uid, DateOnly date, MealCatalogItem meal)
    {
        var profile = await store.GetProfileAsync(uid) ?? new NutritionProfile();
        var days = await plan.GetDaysAsync(uid, date, date);
        days.TryGetValue(date, out var day);
        return SlotPicker.FirstEmptyFor(day, DayMath.TypeOf(day, date, profile), profile.MealTimes, meal);
    }
}
