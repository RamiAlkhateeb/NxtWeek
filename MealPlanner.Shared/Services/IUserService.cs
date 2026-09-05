using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public interface IUserService
{
    Task<UserProfile?> GetProfileAsync(string uid);
    Task CreateProfileAsync(UserProfile profile);
    Task SaveSelectedMealsAsync(string uid, List<string> mealIds);
    Task SaveFavoriteMealsAsync(string uid, List<string> mealIds);
    Task ToggleFavoriteMealAsync(string uid, string mealId);
    Task<List<WeeklyPlanEntry>> GetWeeklyPlanAsync(string uid, DateOnly start, DateOnly end);
    Task<List<WeeklyPlanEntry>> GetMonthPlanAsync(string uid, int year, int month);
    Task<bool> HasAnyPlanEntryAsync(string uid);
    Task<List<UserProfile>> GetRegisteredUsersAsync(string excludeUid);
    Task<UserProfile?> GetNamedProfileAsync(string username);
    Task SaveWeeklyPlanEntryAsync(string uid, WeeklyPlanEntry entry);
    Task SaveWeeklyPlanEntriesAsync(string uid, List<WeeklyPlanEntry> entries);
    Task AssignMealToDateAsync(string uid, DateOnly date, string mealId);
    Task<bool> IsUsernameAvailableAsync(string username, string currentUid);
    Task<string> RenameUserAsync(string currentUid, string requestedUsername);
    Task<string?> GetDataKeyForAuthUidAsync(string authUid);
    Task LinkAuthAccountAsync(string userKey, string authUid, string email);
    Task SendFriendRequestAsync(string senderUid, string recipientUid);
    Task AcceptFriendRequestAsync(string uid, string senderUid);
    Task DeclineFriendRequestAsync(string uid, string senderUid);

}
