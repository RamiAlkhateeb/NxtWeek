using System.Net.Http.Json;
using MealPlanner.Shared.Models;
using MealPlanner.Shared.Nutrition;

namespace MealPlanner.Shared.Services;

// Every write targets its own child path (days/{date}, nutritionProfile, body/{date}) so the
// sibling subtrees of users/{key} are never touched; see FirebaseUserService for why.
public sealed class FirebaseNutritionStore(HttpClient http, FirebaseOptions options) : INutritionStore
{
    private readonly string baseUrl = options.DatabaseUrl.TrimEnd('/');
    private string Url(string path) => $"{baseUrl}/{path}.json";
    private static string Key(string value) => Uri.EscapeDataString(value);

    public async Task<Dictionary<DateOnly, PlanDay>> GetDaysAsync(string uid, DateOnly start, DateOnly end)
    {
        var range = $"orderBy=%22$key%22&startAt=%22{start:yyyy-MM-dd}%22&endAt=%22{end:yyyy-MM-dd}%22";
        var daysTask = http.GetFromJsonAsync<Dictionary<string, PlanDay>>($"{Url($"users/{Key(uid)}/days")}?{range}");
        var legacyTask = http.GetFromJsonAsync<Dictionary<string, WeeklyPlanEntry>>($"{Url($"users/{Key(uid)}/weeklyPlan")}?{range}");
        await Task.WhenAll(daysTask, legacyTask);

        var result = new Dictionary<DateOnly, PlanDay>();
        foreach (var legacy in (await legacyTask ?? []).Values)
            if (legacy.Date >= start && legacy.Date <= end)
                result[legacy.Date] = DayMath.FromLegacy(legacy);

        // A day saved in the new shape wins over its legacy entry.
        foreach (var (key, day) in await daysTask ?? [])
        {
            if (!DateOnly.TryParseExact(key, "yyyy-MM-dd", out var date)) continue;
            day.Date = date;
            day.Slots ??= new();
            result[date] = day;
        }
        return result;
    }

    public async Task SaveDayAsync(string uid, PlanDay day)
    {
        using var response = await http.PutAsJsonAsync(Url($"users/{Key(uid)}/days/{day.Date:yyyy-MM-dd}"), day);
        response.EnsureSuccessStatusCode();
    }

    public async Task<NutritionProfile?> GetProfileAsync(string uid)
    {
        var profile = await http.GetFromJsonAsync<NutritionProfile>(Url($"users/{Key(uid)}/nutritionProfile"));
        if (profile is null) return null;
        profile.Overrides ??= new();
        profile.Overrides.Training ??= new();
        // Meal times and switched-off meals moved to mealPrefs in 3.1; drop them so a save does not write them back.
        profile.MealTimes = null;
        profile.OffSlots = null;
        return profile;
    }

    private MealPrefs? cachedPrefs;
    private string? cachedPrefsUid;

    public async Task<MealPrefs> GetMealPrefsAsync(string uid)
    {
        if (cachedPrefs is not null && cachedPrefsUid == uid) return cachedPrefs;
        var prefs = await http.GetFromJsonAsync<MealPrefs>(Url($"users/{Key(uid)}/mealPrefs"));
        if (prefs is null)
        {
            // First run on 3.1: carry the old meal times over, and keep the afternoon snack only if it is in use.
            var today = DateOnly.FromDateTime(DateTime.Today);
            var legacyTask = http.GetFromJsonAsync<NutritionProfile>(Url($"users/{Key(uid)}/nutritionProfile"));
            var upcomingTask = http.GetFromJsonAsync<Dictionary<string, PlanDay>>(
                $"{Url($"users/{Key(uid)}/days")}?orderBy=%22$key%22&startAt=%22{today:yyyy-MM-dd}%22");
            await Task.WhenAll(legacyTask, upcomingTask);
            var afternoon = (await upcomingTask ?? []).Values.Any(d => d.Slots?.ContainsKey(MealSlot.PostWorkout) == true);
            prefs = MealPrefs.FromLegacy(await legacyTask, afternoon);
            await SaveMealPrefsAsync(uid, prefs);
        }
        cachedPrefs = prefs.Normalize();
        cachedPrefsUid = uid;
        return cachedPrefs;
    }

    public async Task SaveMealPrefsAsync(string uid, MealPrefs prefs)
    {
        using var response = await http.PutAsJsonAsync(Url($"users/{Key(uid)}/mealPrefs"), prefs);
        response.EnsureSuccessStatusCode();
        cachedPrefs = prefs;
        cachedPrefsUid = uid;
    }

    public async Task SaveProfileAsync(string uid, NutritionProfile profile)
    {
        // PATCH, not PUT: the fields from before 3.1 (mealTimes, offSlots, trainingDays) stay in the database untouched.
        using var response = await http.PatchAsJsonAsync(Url($"users/{Key(uid)}/nutritionProfile"), profile);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<BodyLog>> GetBodyLogsAsync(string uid)
    {
        var logs = await http.GetFromJsonAsync<Dictionary<string, BodyLog>>(Url($"users/{Key(uid)}/body")) ?? [];
        foreach (var (key, log) in logs)
            if (DateOnly.TryParseExact(key, "yyyy-MM-dd", out var date)) log.Date = date;
        return logs.Values.OrderBy(x => x.Date).ToList();
    }

    public async Task SaveBodyLogAsync(string uid, BodyLog log)
    {
        using var response = await http.PutAsJsonAsync(Url($"users/{Key(uid)}/body/{log.Date:yyyy-MM-dd}"), log);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Dictionary<DateOnly, string>> GetProgressPhotosAsync(string uid)
    {
        var stored = await http.GetFromJsonAsync<Dictionary<string, string>>(Url($"users/{Key(uid)}/progressPhotos")) ?? [];
        var result = new Dictionary<DateOnly, string>();
        foreach (var (key, url) in stored)
            if (DateOnly.TryParseExact(key, "yyyy-MM-dd", out var monday) && url.StartsWith("data:image/", StringComparison.Ordinal))
                result[monday] = url;
        return result;
    }

    public async Task SaveProgressPhotoAsync(string uid, DateOnly monday, string dataUrl)
    {
        using var response = await http.PutAsJsonAsync(Url($"users/{Key(uid)}/progressPhotos/{monday:yyyy-MM-dd}"), dataUrl);
        response.EnsureSuccessStatusCode();
    }
}
