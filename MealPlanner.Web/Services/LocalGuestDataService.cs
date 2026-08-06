using System.Text.Json;
using MealPlanner.Shared.Models;
using MealPlanner.Shared.Services;
using Microsoft.JSInterop;

namespace MealPlanner.Web.Services;

/// <summary>Guest-mode store. One compact localStorage document keeps the app fully usable offline.</summary>
public sealed class LocalGuestDataService(IJSRuntime js) : IUserService, IMealCatalogService
{
    private const string Key = "nxtweek.guestData.v1";
    private sealed class Store
    {
        public Dictionary<string, UserProfile> Profiles { get; set; } = new();
        public List<MealCatalogItem> Catalog { get; set; } = new();
        public Dictionary<string, Dictionary<string, WeeklyPlanEntry>> Plans { get; set; } = new();
    }

    private async Task<Store> ReadAsync()
    {
        var json = await js.InvokeAsync<string?>("localStorage.getItem", Key);
        return string.IsNullOrWhiteSpace(json) ? new Store() : JsonSerializer.Deserialize<Store>(json) ?? new Store();
    }
    private Task WriteAsync(Store store) => js.InvokeVoidAsync("localStorage.setItem", Key, JsonSerializer.Serialize(store)).AsTask();

    public async Task<UserProfile?> GetProfileAsync(string uid) => (await ReadAsync()).Profiles.GetValueOrDefault(uid);
    public async Task CreateProfileAsync(UserProfile profile) { var s = await ReadAsync(); s.Profiles[profile.Uid] = profile; await WriteAsync(s); }
    public async Task SavePreferredCuisinesAsync(string uid, List<Cuisine> cuisines) { var p = await RequireProfile(uid); p.PreferredCuisines = cuisines; await SaveProfile(p); }
    public async Task SaveSelectedMealsAsync(string uid, List<string> ids) { var p = await RequireProfile(uid); p.SelectedMealIds = ids; await SaveProfile(p); }
    public async Task SaveFavoriteMealsAsync(string uid, List<string> ids) { var p = await RequireProfile(uid); p.FavoriteMealIds = ids; await SaveProfile(p); }
    public async Task ToggleFavoriteMealAsync(string uid, string id) { var p = await RequireProfile(uid); if (!p.FavoriteMealIds.Remove(id)) p.FavoriteMealIds.Add(id); await SaveProfile(p); }
    public async Task<List<WeeklyPlanEntry>> GetWeeklyPlanAsync(string uid, DateOnly start, DateOnly end)
    {
        var s = await ReadAsync(); if (!s.Plans.TryGetValue(uid, out var plan)) return [];
        return plan.Values.Where(x => x.Date >= start && x.Date <= end).ToList();
    }
    public async Task<List<WeeklyPlanEntry>> GetMonthPlanAsync(string uid, int year, int month) => (await GetWeeklyPlanAsync(uid, new DateOnly(year, month, 1), new DateOnly(year, month, DateTime.DaysInMonth(year, month))));
    public async Task SaveWeeklyPlanEntryAsync(string uid, WeeklyPlanEntry entry)
    {
        var s = await ReadAsync(); if (!s.Plans.TryGetValue(uid, out var plan)) s.Plans[uid] = plan = new(); plan[entry.Date.ToString("yyyy-MM-dd")] = entry; await WriteAsync(s);
    }
    public async Task SaveWeeklyPlanEntriesAsync(string uid, List<WeeklyPlanEntry> entries) { foreach (var entry in entries) await SaveWeeklyPlanEntryAsync(uid, entry); }
    public async Task<string> GetHouseholdIdAsync(string uid) { var p = await RequireProfile(uid); if (string.IsNullOrWhiteSpace(p.HouseholdId)) { p.HouseholdId = "local_" + uid; await SaveProfile(p); } return p.HouseholdId; }
    public void ClearCachedHouseholdId() { }
    public Task<bool> SendLinkRequestAsync(string fromUid, string toUid) => Task.FromResult(false);
    public Task AcceptLinkRequestAsync(string acceptingUid, string requesterUid) => Task.CompletedTask;
    public Task RejectLinkRequestAsync(string acceptingUid, string requesterUid) => Task.CompletedTask;
    public Task AssignMealToDateAsync(string uid, DateOnly date, string mealId) => SaveWeeklyPlanEntryAsync(uid, new WeeklyPlanEntry { Date = date, MealId = mealId });

    public async Task<List<MealCatalogItem>> GetAllMealsAsync() => (await ReadAsync()).Catalog;
    public async Task<MealCatalogItem?> GetMealByIdAsync(string id) => (await ReadAsync()).Catalog.FirstOrDefault(x => x.Id == id);
    public async Task<List<MealCatalogItem>> GetFilteredMealsAsync(List<Cuisine>? cuisines, MealType? type) => (await GetAllMealsAsync()).Where(x => (cuisines is null || cuisines.Count == 0 || cuisines.Contains(x.Cuisine)) && (!type.HasValue || x.MealType == type)).ToList();
    public async Task UpsertMealAsync(MealCatalogItem meal) { var s = await ReadAsync(); s.Catalog.RemoveAll(x => x.Id == meal.Id); s.Catalog.Add(meal); await WriteAsync(s); }
    public async Task<bool> IsCatalogSeededAsync() => (await ReadAsync()).Catalog.Count > 0;
    public async Task SeedCatalogAsync(List<MealCatalogItem> meals) { var s = await ReadAsync(); if (s.Catalog.Count == 0) { s.Catalog = meals; await WriteAsync(s); } }

    private async Task<UserProfile> RequireProfile(string uid) => await GetProfileAsync(uid) ?? new UserProfile { Uid = uid, HouseholdId = "local_" + uid };
    private async Task SaveProfile(UserProfile profile) { var s = await ReadAsync(); s.Profiles[profile.Uid] = profile; await WriteAsync(s); }
}
