using System.Net.Http.Json;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

public sealed class FirebaseUserService(HttpClient http, FirebaseOptions options, IAuthService auth) : IUserService
{
    private readonly string baseUrl = options.DatabaseUrl.TrimEnd('/');
    private string? householdId;
    private async Task<string> Url(string path)
    {
        var user = await auth.GetCurrentUserAsync() ?? throw new UnauthorizedAccessException("Sign in is required.");
        return $"{baseUrl}/{path}.json";
    }
    private static string Key(string value) => Uri.EscapeDataString(value);
    public async Task<UserProfile?> GetProfileAsync(string uid)
    {
        var profile = await http.GetFromJsonAsync<UserProfile>(await Url($"users/{Key(uid)}"));
        if (profile is not null) householdId = profile.HouseholdId;
        return profile;
    }
    public async Task CreateProfileAsync(UserProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Uid)) throw new ArgumentException("UID is required.");
        if (string.IsNullOrWhiteSpace(profile.HouseholdId))
        {
            profile.HouseholdId = "hh_" + Guid.NewGuid().ToString("N");
            await http.PutAsJsonAsync(await Url($"households/{profile.HouseholdId}"), new Household { Id = profile.HouseholdId, MemberIds = [profile.Uid] });
        }
        await http.PutAsJsonAsync(await Url($"users/{Key(profile.Uid)}"), profile);
        householdId = profile.HouseholdId;
    }
    public async Task SavePreferredCuisinesAsync(string uid, List<Cuisine> cuisines) => await http.PutAsJsonAsync(await Url($"users/{Key(uid)}/preferredCuisines"), cuisines);
    public async Task SaveSelectedMealsAsync(string uid, List<string> mealIds) => await http.PutAsJsonAsync(await Url($"users/{Key(uid)}/selectedMealIds"), mealIds);
    public async Task<string> GetHouseholdIdAsync(string uid) => householdId ?? (await GetProfileAsync(uid))?.HouseholdId ?? "";
    public void ClearCachedHouseholdId() => householdId = null;
    public async Task SaveFavoriteMealsAsync(string uid, List<string> mealIds) => await http.PutAsJsonAsync(await Url($"households/{await GetHouseholdIdAsync(uid)}/favoriteMealIds"), mealIds);
    public async Task ToggleFavoriteMealAsync(string uid, string mealId)
    {
        var url = await Url($"households/{await GetHouseholdIdAsync(uid)}/favoriteMealIds");
        var values = await http.GetFromJsonAsync<List<string>>(url) ?? [];
        if (!values.Remove(mealId)) values.Add(mealId);
        await http.PutAsJsonAsync(url, values);
    }
    public async Task<List<WeeklyPlanEntry>> GetWeeklyPlanAsync(string uid, DateOnly start, DateOnly end)
    {
        var plan = await http.GetFromJsonAsync<Dictionary<string, WeeklyPlanEntry>>(await Url($"households/{await GetHouseholdIdAsync(uid)}/weeklyPlan")) ?? [];
        return plan.Values.Where(x => x.Date >= start && x.Date <= end).OrderBy(x => x.Date).ToList();
    }
    public Task<List<WeeklyPlanEntry>> GetMonthPlanAsync(string uid, int year, int month) => GetWeeklyPlanAsync(uid, new DateOnly(year, month, 1), new DateOnly(year, month, 1).AddMonths(1).AddDays(-1));
    public async Task SaveWeeklyPlanEntryAsync(string uid, WeeklyPlanEntry entry) => await http.PutAsJsonAsync(await Url($"households/{await GetHouseholdIdAsync(uid)}/weeklyPlan/{entry.Date:yyyy-MM-dd}"), entry);
    public async Task SaveWeeklyPlanEntriesAsync(string uid, List<WeeklyPlanEntry> entries) { foreach (var entry in entries) await SaveWeeklyPlanEntryAsync(uid, entry); }
    public async Task<bool> SendLinkRequestAsync(string fromUid, string toUid)
    {
        if (fromUid == toUid || await GetProfileAsync(toUid) is null) return false;
        var requests = await http.GetFromJsonAsync<List<string>>(await Url($"users/{Key(toUid)}/pendingLinkRequestUids")) ?? [];
        if (!requests.Contains(fromUid)) { requests.Add(fromUid); await http.PutAsJsonAsync(await Url($"users/{Key(toUid)}/pendingLinkRequestUids"), requests); }
        return true;
    }
    public async Task AcceptLinkRequestAsync(string acceptingUid, string requesterUid)
    {
        var accepting = await GetProfileAsync(acceptingUid); var requester = await GetProfileAsync(requesterUid);
        if (accepting is null || requester is null) return;
        var house = await http.GetFromJsonAsync<Household>(await Url($"households/{requester.HouseholdId}")); if (house is null) return;
        if (!house.MemberIds.Contains(acceptingUid)) house.MemberIds.Add(acceptingUid);
        accepting.HouseholdId = requester.HouseholdId; accepting.PendingLinkRequestUids.Remove(requesterUid);
        await http.PutAsJsonAsync(await Url($"households/{house.Id}"), house);
        await http.PutAsJsonAsync(await Url($"users/{Key(acceptingUid)}"), accepting);
        householdId = house.Id;
    }
    public async Task RejectLinkRequestAsync(string acceptingUid, string requesterUid)
    { var p = await GetProfileAsync(acceptingUid); if (p is null) return; p.PendingLinkRequestUids.Remove(requesterUid); await http.PutAsJsonAsync(await Url($"users/{Key(acceptingUid)}"), p); }
    public Task AssignMealToDateAsync(string uid, DateOnly date, string mealId) => SaveWeeklyPlanEntryAsync(uid, new() { Date = date, MealId = mealId });
}
