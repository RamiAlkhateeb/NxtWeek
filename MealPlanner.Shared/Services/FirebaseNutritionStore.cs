using MealPlanner.Shared.Models;
using MealPlanner.Shared.Nutrition;

namespace MealPlanner.Shared.Services;

// Every write targets its own child path (days/{date}, nutritionProfile, body/{date}) so the
// sibling subtrees of users/{key} are never touched; see FirebaseUserService for why.
public sealed class FirebaseNutritionStore(FirebaseRest db) : INutritionStore
{
    private static string Key(string value) => FirebaseRest.Key(value);

    public async Task<Dictionary<DateOnly, PlanDay>> GetDaysAsync(string uid, DateOnly start, DateOnly end)
    {
        var range = $"orderBy=%22$key%22&startAt=%22{start:yyyy-MM-dd}%22&endAt=%22{end:yyyy-MM-dd}%22";
        var daysTask = db.GetAsync<Dictionary<string, PlanDay>>($"users/{Key(uid)}/days", range);
        var legacyTask = db.GetAsync<Dictionary<string, WeeklyPlanEntry>>($"users/{Key(uid)}/weeklyPlan", range);
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
        await db.PutAsync($"users/{Key(uid)}/days/{day.Date:yyyy-MM-dd}", day);
    }

    public async Task<NutritionProfile?> GetProfileAsync(string uid)
    {
        var profile = await db.GetAsync<NutritionProfile>($"users/{Key(uid)}/nutritionProfile");
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
        var prefs = await db.GetAsync<MealPrefs>($"users/{Key(uid)}/mealPrefs");
        if (prefs is null)
        {
            // First run on 3.1: carry the old meal times over, and keep the afternoon snack only if it is in use.
            var today = DateOnly.FromDateTime(DateTime.Today);
            var legacyTask = db.GetAsync<NutritionProfile>($"users/{Key(uid)}/nutritionProfile");
            var upcomingTask = db.GetAsync<Dictionary<string, PlanDay>>(
                $"users/{Key(uid)}/days", $"orderBy=%22$key%22&startAt=%22{today:yyyy-MM-dd}%22");
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
        await db.PutAsync($"users/{Key(uid)}/mealPrefs", prefs);
        cachedPrefs = prefs;
        cachedPrefsUid = uid;
    }

    public async Task SaveProfileAsync(string uid, NutritionProfile profile)
    {
        // PATCH, not PUT: the fields from before 3.1 (mealTimes, offSlots, trainingDays) stay in the database untouched.
        await db.PatchAsync($"users/{Key(uid)}/nutritionProfile", profile);
    }

    public async Task<List<BodyLog>> GetBodyLogsAsync(string uid)
    {
        var logs = await db.GetAsync<Dictionary<string, BodyLog>>($"users/{Key(uid)}/body") ?? [];
        foreach (var (key, log) in logs)
            if (DateOnly.TryParseExact(key, "yyyy-MM-dd", out var date)) log.Date = date;
        return logs.Values.OrderBy(x => x.Date).ToList();
    }

    public async Task SaveBodyLogAsync(string uid, BodyLog log)
    {
        await db.PutAsync($"users/{Key(uid)}/body/{log.Date:yyyy-MM-dd}", log);
    }

    public async Task<Dictionary<DateOnly, string>> GetProgressPhotosAsync(string uid)
    {
        var stored = await db.GetAsync<Dictionary<string, string>>($"users/{Key(uid)}/progressPhotos") ?? [];
        var result = new Dictionary<DateOnly, string>();
        foreach (var (key, url) in stored)
            if (DateOnly.TryParseExact(key, "yyyy-MM-dd", out var monday) && url.StartsWith("data:image/", StringComparison.Ordinal))
                result[monday] = url;
        return result;
    }

    public async Task SaveProgressPhotoAsync(string uid, DateOnly monday, string dataUrl)
    {
        await db.PutAsync($"users/{Key(uid)}/progressPhotos/{monday:yyyy-MM-dd}", dataUrl);
    }

    public async Task<SharedReview?> GetSharedReviewAsync(string uid) =>
        await db.GetAsync<SharedReview?>($"users/{Key(uid)}/sharedReview");

    public async Task ShareReviewAsync(string uid, SharedReview review)
    {
        await db.PutAsync($"users/{Key(uid)}/sharedReview", review);
    }

    public async Task<SyncLink?> GetSyncLinkAsync(string uid) =>
        await db.GetAsync<SyncLink?>($"users/{Key(uid)}/sync");
}
