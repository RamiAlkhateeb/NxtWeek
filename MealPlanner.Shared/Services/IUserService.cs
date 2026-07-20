using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public interface IUserService
{
    Task<UserProfile?> GetProfileAsync(string uid);
    Task CreateProfileAsync(UserProfile profile);
    Task SavePreferredCuisinesAsync(string uid, List<Cuisine> cuisines);
    Task SaveSelectedMealsAsync(string uid, List<string> mealIds);
    Task SaveFavoriteMealsAsync(string uid, List<string> mealIds);
    Task ToggleFavoriteMealAsync(string uid, string mealId);
    Task<List<WeeklyPlanEntry>> GetWeeklyPlanAsync(string uid, DateOnly start, DateOnly end);
    Task<List<WeeklyPlanEntry>> GetMonthPlanAsync(string uid, int year, int month);
    Task SaveWeeklyPlanEntryAsync(string uid, WeeklyPlanEntry entry);
    Task SaveWeeklyPlanEntriesAsync(string uid, List<WeeklyPlanEntry> entries);

        
    // Household helpers
    Task<string> GetHouseholdIdAsync(string uid);
    void ClearCachedHouseholdId();

    // Account Linking
    Task<bool> SendLinkRequestAsync(string fromUid, string toUid);
    Task AcceptLinkRequestAsync(string acceptingUid, string requesterUid);
    Task RejectLinkRequestAsync(string acceptingUid, string requesterUid);
    
    // Shared Date Assignment
    Task AssignMealToDateAsync(string uid, DateOnly date, string mealId);

}
