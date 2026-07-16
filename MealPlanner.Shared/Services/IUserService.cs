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

        
    // Household helpers
    Task<string> GetHouseholdIdAsync(string username);
    void ClearCachedHouseholdId();

    // Account Linking
    Task<bool> SendLinkRequestAsync(string fromUsername, string toUsername);
    Task AcceptLinkRequestAsync(string acceptingUsername, string requesterUsername);
    Task RejectLinkRequestAsync(string acceptingUsername, string requesterUsername);
    
    // Shared Date Assignment
    Task AssignMealToDateAsync(string username, DateOnly date, string mealId);
}
