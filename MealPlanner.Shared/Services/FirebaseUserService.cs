using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public class FirebaseUserService : IUserService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private string? _cachedHouseholdId;

    public FirebaseUserService(HttpClient http, FirebaseOptions options)
    {
        _http = http;
        _baseUrl = options.DatabaseUrl.TrimEnd('/');
    }

    public async Task<UserProfile?> GetProfileAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;

        var cleanUsername = Uri.EscapeDataString(username);
        var dto = await _http.GetFromJsonAsync<UserProfileDto>($"{_baseUrl}/users/{cleanUsername}.json");
        if (dto is null) return null;

        var profile = new UserProfile
        {
            Username = username,
            PreferredCuisines = dto.PreferredCuisines ?? new(),
            SelectedMealIds = dto.SelectedMealIds ?? new(),
            HouseholdId = dto.HouseholdId ?? string.Empty,
            PendingLinkRequests = dto.PendingLinkRequests ?? new()
        };

        // Cache household ID for current session
        if (!string.IsNullOrEmpty(profile.HouseholdId))
        {
            _cachedHouseholdId = profile.HouseholdId;
            
            // Also retrieve FavoriteMealIds from household
            var favorites = await _http.GetFromJsonAsync<List<string>>($"{_baseUrl}/households/{profile.HouseholdId}/favoriteMealIds.json");
            profile.FavoriteMealIds = favorites ?? new();
        }

        return profile;
    }

    public async Task CreateProfileAsync(UserProfile profile)
    {
        var cleanUsername = Uri.EscapeDataString(profile.Username);

        // If user profile has no household ID, auto-create a solo household
        if (string.IsNullOrEmpty(profile.HouseholdId))
        {
            var newHouseholdId = "hh_" + Guid.NewGuid().ToString("N");
            profile.HouseholdId = newHouseholdId;

            var household = new Household
            {
                Id = newHouseholdId,
                MemberUsernames = new List<string> { profile.Username },
                FavoriteMealIds = profile.FavoriteMealIds ?? new()
            };
            await _http.PutAsJsonAsync($"{_baseUrl}/households/{newHouseholdId}.json", household);
        }

        var dto = new UserProfileDto
        {
            PreferredCuisines = profile.PreferredCuisines,
            SelectedMealIds = profile.SelectedMealIds,
            HouseholdId = profile.HouseholdId,
            PendingLinkRequests = profile.PendingLinkRequests
        };
        await _http.PutAsJsonAsync($"{_baseUrl}/users/{cleanUsername}.json", dto);
    }

    public async Task SavePreferredCuisinesAsync(string username, List<Cuisine> cuisines)
    {
        var cleanUsername = Uri.EscapeDataString(username);
        await _http.PutAsJsonAsync($"{_baseUrl}/users/{cleanUsername}/preferredCuisines.json", cuisines);
    }

    public async Task SaveSelectedMealsAsync(string username, List<string> mealIds)
    {
        var cleanUsername = Uri.EscapeDataString(username);
        await _http.PutAsJsonAsync($"{_baseUrl}/users/{cleanUsername}/selectedMealIds.json", mealIds);
    }

    public async Task<string> GetHouseholdIdAsync(string username)
    {
        if (!string.IsNullOrEmpty(_cachedHouseholdId))
        {
            return _cachedHouseholdId;
        }

        var profile = await GetProfileAsync(username);
        if (profile is not null && !string.IsNullOrEmpty(profile.HouseholdId))
        {
            _cachedHouseholdId = profile.HouseholdId;
            return _cachedHouseholdId;
        }

        return string.Empty;
    }

    public void ClearCachedHouseholdId()
    {
        _cachedHouseholdId = null;
    }

    public async Task SaveFavoriteMealsAsync(string username, List<string> mealIds)
    {
        var householdId = await GetHouseholdIdAsync(username);
        if (string.IsNullOrEmpty(householdId)) return;

        await _http.PutAsJsonAsync($"{_baseUrl}/households/{householdId}/favoriteMealIds.json", mealIds);
    }

    public async Task ToggleFavoriteMealAsync(string username, string mealId)
    {
        var householdId = await GetHouseholdIdAsync(username);
        if (string.IsNullOrEmpty(householdId)) return;

        var favorites = await _http.GetFromJsonAsync<List<string>>($"{_baseUrl}/households/{householdId}/favoriteMealIds.json") ?? new();
        if (favorites.Contains(mealId))
        {
            favorites.Remove(mealId);
        }
        else
        {
            favorites.Add(mealId);
        }
        await SaveFavoriteMealsAsync(username, favorites);
    }

    public async Task<List<WeeklyPlanEntry>> GetWeeklyPlanAsync(string username, DateOnly start, DateOnly end)
    {
        var householdId = await GetHouseholdIdAsync(username);
        if (string.IsNullOrEmpty(householdId)) return new List<WeeklyPlanEntry>();

        var plan = await _http.GetFromJsonAsync<Dictionary<string, WeeklyPlanValueDto>>($"{_baseUrl}/households/{householdId}/weeklyPlan.json");
        if (plan is null) return new List<WeeklyPlanEntry>();

        var entries = new List<WeeklyPlanEntry>();
        foreach (var kvp in plan)
        {
            if (DateOnly.TryParse(kvp.Key, out var date))
            {
                if (date >= start && date <= end)
                {
                    entries.Add(new WeeklyPlanEntry
                    {
                        Date = date,
                        MealId = kvp.Value.MealId,
                        IsFavorite = kvp.Value.IsFavorite
                    });
                }
            }
        }
        return entries.OrderBy(e => e.Date).ToList();
    }

    public Task<List<WeeklyPlanEntry>> GetMonthPlanAsync(string username, int year, int month)
    {
        var start = new DateOnly(year, month, 1);
        return GetWeeklyPlanAsync(username, start, start.AddMonths(1).AddDays(-1));
    }

    public async Task SaveWeeklyPlanEntryAsync(string username, WeeklyPlanEntry entry)
    {
        var householdId = await GetHouseholdIdAsync(username);
        if (string.IsNullOrEmpty(householdId)) return;

        var dateStr = entry.Date.ToString("yyyy-MM-dd");
        var dto = new WeeklyPlanValueDto
        {
            MealId = entry.MealId,
            IsFavorite = entry.IsFavorite
        };
        await _http.PutAsJsonAsync($"{_baseUrl}/households/{householdId}/weeklyPlan/{dateStr}.json", dto);
    }

    public async Task SaveWeeklyPlanEntriesAsync(string username, List<WeeklyPlanEntry> entries)
    {
        foreach (var entry in entries)
        {
            await SaveWeeklyPlanEntryAsync(username, entry);
        }
    }

    // --- Account Linking Implementation ---

    public async Task<bool> SendLinkRequestAsync(string fromUsername, string toUsername)
    {
        if (string.IsNullOrWhiteSpace(fromUsername) || string.IsNullOrWhiteSpace(toUsername)) return false;
        if (fromUsername.Equals(toUsername, StringComparison.OrdinalIgnoreCase)) return false;

        var toProfile = await GetProfileAsync(toUsername);
        if (toProfile is null) return false; // Target user does not exist

        var requests = toProfile.PendingLinkRequests ?? new();
        if (!requests.Contains(fromUsername, StringComparer.OrdinalIgnoreCase))
        {
            requests.Add(fromUsername);
            var cleanToUsername = Uri.EscapeDataString(toUsername);
            await _http.PutAsJsonAsync($"{_baseUrl}/users/{cleanToUsername}/pendingLinkRequests.json", requests);
        }

        return true;
    }

    public async Task AcceptLinkRequestAsync(string acceptingUsername, string requesterUsername)
    {
        var acceptingProfile = await GetProfileAsync(acceptingUsername);
        var requesterProfile = await GetProfileAsync(requesterUsername);

        if (acceptingProfile is null || requesterProfile is null) return;

        var requesterHouseholdId = requesterProfile.HouseholdId;
        if (string.IsNullOrEmpty(requesterHouseholdId)) return;

        var oldHouseholdId = acceptingProfile.HouseholdId;

        // 1. Reassign accepting user's HouseholdId to the requester's HouseholdId
        acceptingProfile.HouseholdId = requesterHouseholdId;
        
        // Remove requester from the pending requests list
        acceptingProfile.PendingLinkRequests.RemoveAll(x => x.Equals(requesterUsername, StringComparison.OrdinalIgnoreCase));

        // Update accepting user profile
        var cleanAccepting = Uri.EscapeDataString(acceptingUsername);
        var acceptingDto = new UserProfileDto
        {
            PreferredCuisines = acceptingProfile.PreferredCuisines,
            SelectedMealIds = acceptingProfile.SelectedMealIds,
            HouseholdId = acceptingProfile.HouseholdId,
            PendingLinkRequests = acceptingProfile.PendingLinkRequests
        };
        await _http.PutAsJsonAsync($"{_baseUrl}/users/{cleanAccepting}.json", acceptingDto);

        // 2. Add both to requester's Household MemberUsernames
        var requesterHousehold = await _http.GetFromJsonAsync<Household>($"{_baseUrl}/households/{requesterHouseholdId}.json");
        if (requesterHousehold is not null)
        {
            if (requesterHousehold.MemberUsernames is null)
            {
                requesterHousehold.MemberUsernames = new List<string>();
            }

            if (!requesterHousehold.MemberUsernames.Contains(requesterUsername, StringComparer.OrdinalIgnoreCase))
            {
                requesterHousehold.MemberUsernames.Add(requesterUsername);
            }
            if (!requesterHousehold.MemberUsernames.Contains(acceptingUsername, StringComparer.OrdinalIgnoreCase))
            {
                requesterHousehold.MemberUsernames.Add(acceptingUsername);
            }

            await _http.PutAsJsonAsync($"{_baseUrl}/households/{requesterHouseholdId}.json", requesterHousehold);
        }

        // 3. Discard accepting user's old solo household (DELETE households/{oldHouseholdId}.json)
        if (!string.IsNullOrEmpty(oldHouseholdId) && !oldHouseholdId.Equals(requesterHouseholdId, StringComparison.OrdinalIgnoreCase))
        {
            await _http.DeleteAsync($"{_baseUrl}/households/{oldHouseholdId}.json");
        }

        // Clear local cache for this session
        ClearCachedHouseholdId();
    }

    public async Task RejectLinkRequestAsync(string acceptingUsername, string requesterUsername)
    {
        var acceptingProfile = await GetProfileAsync(acceptingUsername);
        if (acceptingProfile is null) return;

        acceptingProfile.PendingLinkRequests.RemoveAll(x => x.Equals(requesterUsername, StringComparison.OrdinalIgnoreCase));

        var cleanAccepting = Uri.EscapeDataString(acceptingUsername);
        await _http.PutAsJsonAsync($"{_baseUrl}/users/{cleanAccepting}/pendingLinkRequests.json", acceptingProfile.PendingLinkRequests);
    }

    public async Task AssignMealToDateAsync(string username, DateOnly date, string mealId)
    {
        var entry = new WeeklyPlanEntry
        {
            Date = date,
            MealId = mealId,
            IsFavorite = false
        };
        await SaveWeeklyPlanEntryAsync(username, entry);
    }

    private class UserProfileDto
    {
        public List<Cuisine>? PreferredCuisines { get; set; }
        public List<string>? SelectedMealIds { get; set; }
        public List<string>? FavoriteMealIds { get; set; } // Kept for serialization compatibility if needed
        public string HouseholdId { get; set; } = string.Empty;
        public List<string>? PendingLinkRequests { get; set; }
    }

    private class WeeklyPlanValueDto
    {
        public string MealId { get; set; } = string.Empty;
        public bool IsFavorite { get; set; }
    }
}
