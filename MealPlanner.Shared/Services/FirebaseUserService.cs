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

        return new UserProfile
        {
            Username = username,
            PreferredCuisines = dto.PreferredCuisines ?? new(),
            SelectedMealIds = dto.SelectedMealIds ?? new(),
            FavoriteMealIds = dto.FavoriteMealIds ?? new()
        };
    }

    public async Task CreateProfileAsync(UserProfile profile)
    {
        var cleanUsername = Uri.EscapeDataString(profile.Username);
        var dto = new UserProfileDto
        {
            PreferredCuisines = profile.PreferredCuisines,
            SelectedMealIds = profile.SelectedMealIds,
            FavoriteMealIds = profile.FavoriteMealIds
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

    public async Task SaveFavoriteMealsAsync(string username, List<string> mealIds)
    {
        var cleanUsername = Uri.EscapeDataString(username);
        await _http.PutAsJsonAsync($"{_baseUrl}/users/{cleanUsername}/favoriteMealIds.json", mealIds);
    }

    public async Task ToggleFavoriteMealAsync(string username, string mealId)
    {
        var profile = await GetProfileAsync(username);
        if (profile is null) return;

        var favorites = profile.FavoriteMealIds ?? new();
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
        var cleanUsername = Uri.EscapeDataString(username);
        var plan = await _http.GetFromJsonAsync<Dictionary<string, WeeklyPlanValueDto>>($"{_baseUrl}/users/{cleanUsername}/weeklyPlan.json");
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

    public async Task SaveWeeklyPlanEntryAsync(string username, WeeklyPlanEntry entry)
    {
        var cleanUsername = Uri.EscapeDataString(username);
        var dateStr = entry.Date.ToString("yyyy-MM-dd");
        var dto = new WeeklyPlanValueDto
        {
            MealId = entry.MealId,
            IsFavorite = entry.IsFavorite
        };
        await _http.PutAsJsonAsync($"{_baseUrl}/users/{cleanUsername}/weeklyPlan/{dateStr}.json", dto);
    }

    public async Task SaveWeeklyPlanEntriesAsync(string username, List<WeeklyPlanEntry> entries)
    {
        foreach (var entry in entries)
        {
            await SaveWeeklyPlanEntryAsync(username, entry);
        }
    }

    private class UserProfileDto
    {
        public List<Cuisine>? PreferredCuisines { get; set; }
        public List<string>? SelectedMealIds { get; set; }
        public List<string>? FavoriteMealIds { get; set; }
    }

    private class WeeklyPlanValueDto
    {
        public string MealId { get; set; } = string.Empty;
        public bool IsFavorite { get; set; }
    }
}
