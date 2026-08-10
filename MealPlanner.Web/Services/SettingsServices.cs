using MealPlanner.Shared.Services;
using Microsoft.JSInterop;

namespace MealPlanner.Web.Services;

public sealed class LocalHouseholdService(IAuthService auth, LocalGuestDataService localUsers, FirebaseUserService firebaseUsers) : IHouseholdService
{
    public async Task<HouseholdSettings> GetCurrentAsync()
    {
        var user = await auth.GetCurrentUserAsync() ?? throw new InvalidOperationException("No local user.");
        var localProfile = await localUsers.GetProfileAsync(user.Uid) ?? new MealPlanner.Shared.Models.UserProfile { Uid = user.Uid, DisplayName = "ضيف" };
        var firebaseProfile = await firebaseUsers.GetProfileAsync(user.Uid);
        if (firebaseProfile is null)
        {
            firebaseProfile = new MealPlanner.Shared.Models.UserProfile
            {
                Uid = user.Uid,
                Email = localProfile.Email,
                DisplayName = localProfile.DisplayName,
                SelectedMealIds = localProfile.SelectedMealIds,
                FavoriteMealIds = localProfile.FavoriteMealIds
            };
            await firebaseUsers.CreateProfileAsync(firebaseProfile);
        }
        localProfile.HouseholdId = firebaseProfile.HouseholdId;
        await localUsers.CreateProfileAsync(localProfile);
        return new HouseholdSettings(firebaseProfile.DisplayName ?? localProfile.DisplayName ?? "ضيف", firebaseProfile.HouseholdId);
    }
    public async Task<bool> JoinAsync(string householdId)
    {
        var user = await auth.GetCurrentUserAsync();
        if (user is null || string.IsNullOrWhiteSpace(householdId)) return false;
        await GetCurrentAsync(); // guarantees a Firebase user and household exist first
        if (!await firebaseUsers.JoinHouseholdAsync(user.Uid, householdId)) return false;
        var profile = await localUsers.GetProfileAsync(user.Uid);
        if (profile is not null) { profile.HouseholdId = householdId.Trim(); await localUsers.CreateProfileAsync(profile); }
        return true;
    }
}

public sealed class LocalDataExportService(IAuthService auth, IUserService users, IMealCatalogService catalog) : IDataExportService
{
    public async Task<string> ExportCurrentWeekAsync()
    {
        var user = await auth.GetCurrentUserAsync() ?? throw new InvalidOperationException("No local user.");
        var today = DateOnly.FromDateTime(DateTime.Today);
        var start = today.AddDays(-(7 + (int)today.DayOfWeek - (int)DayOfWeek.Monday) % 7);
        var plan = await users.GetWeeklyPlanAsync(user.Uid, start, start.AddDays(6));
        var meals = await catalog.GetAllMealsAsync();
        var rows = plan.Select(x => new { Date = x.Date.ToString("yyyy-MM-dd"), Day = x.Date.DayOfWeek.ToString(), Meal = meals.FirstOrDefault(m => m.Id == x.MealId)?.Name ?? "" }).Where(x => x.Meal.Length > 0);
        return "Date,Day,Meal\n" + string.Join("\n", rows.Select(x => $"{x.Date},{x.Day},\"{x.Meal.Replace("\"", "\"\"")}\""));
    }
}

public sealed class LocalDataImportService(IJSRuntime js) : IDataImportService
{
    public async Task ClearLocalDataAsync()
    {
        await js.InvokeVoidAsync("localStorage.removeItem", "nxtweek.guestData.v1");
        await js.InvokeVoidAsync("localStorage.removeItem", "meals-cache");
        await js.InvokeVoidAsync("localStorage.removeItem", "nxtweek.guestId");
    }
}

public sealed class BrowserPreferenceService(IJSRuntime js) : IThemeService, ILanguageService
{
    public Task<string> GetAsync() => js.InvokeAsync<string?>("localStorage.getItem", "nxtweek.theme").AsTask().ContinueWith(x => x.Result ?? "system");
    public Task SetAsync(string theme) => js.InvokeVoidAsync("localStorage.setItem", "nxtweek.theme", theme).AsTask();
    Task<string> ILanguageService.GetAsync() => js.InvokeAsync<string?>("localStorage.getItem", "nxtweek.language").AsTask().ContinueWith(x => x.Result ?? "ar");
    Task ILanguageService.SetAsync(string language) => js.InvokeVoidAsync("localStorage.setItem", "nxtweek.language", language).AsTask();
}
