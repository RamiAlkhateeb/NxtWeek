using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

/// <summary>Puts a catalog meal on a day of the user's plan (used by the Meals grid and the meal details page).</summary>
public interface IMealAssignService
{
    Task AssignAsync(string uid, DateOnly date, MealCatalogItem meal);
}

public sealed class MealAssignService(IUserService users, IMealCacheService cache) : IMealAssignService
{
    public async Task AssignAsync(string uid, DateOnly date, MealCatalogItem meal)
    {
        await users.AssignMealToDateAsync(uid, date, meal.Id);

        // Keep the read-through week cache in step so the Week page never flashes the old meal.
        var cached = await cache.GetCachedWeekAsync();
        if (cached is null) return;
        var id = date.ToString("yyyy-MM-dd");
        cached.RemoveAll(m => m.Id == id);
        cached.Add(new Meal
        {
            Id = id, Date = date, DayName = date.DayOfWeek.ToString(),
            Name = meal.Name, MealId = meal.Id, IsFavorite = false,
            SideDishes = meal.SideDishes, Ingredients = meal.Ingredients,
            MealType = meal.MealType, Language = meal.Language
        });
        await cache.SetCachedWeekAsync(cached);
    }
}
