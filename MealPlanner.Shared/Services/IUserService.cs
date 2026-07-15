using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public interface IUserService
{
    Task<UserProfile?> GetProfileAsync(string username);
    Task CreateProfileAsync(UserProfile profile);
    Task SavePreferredCuisinesAsync(string username, List<Cuisine> cuisines);
    Task SaveSelectedMealsAsync(string username, List<string> mealIds);
    Task SaveFavoriteMealsAsync(string username, List<string> mealIds);
    Task ToggleFavoriteMealAsync(string username, string mealId);
    Task<List<WeeklyPlanEntry>> GetWeeklyPlanAsync(string username, DateOnly start, DateOnly end);
    Task SaveWeeklyPlanEntryAsync(string username, WeeklyPlanEntry entry);
    Task SaveWeeklyPlanEntriesAsync(string username, List<WeeklyPlanEntry> entries);
}
