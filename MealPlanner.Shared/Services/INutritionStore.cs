using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

/// <summary>
/// Slot plans, meal preferences, the nutrition profile and body measurements, all under users/{key} in Firebase.
/// Profile and body data belong to a signed-in account and are never written to localStorage.
/// </summary>
public interface INutritionStore
{
    /// <summary>Days in [start, end], keyed by date. Days saved before slots existed come back with their meal as Meal 1.</summary>
    Task<Dictionary<DateOnly, PlanDay>> GetDaysAsync(string uid, DateOnly start, DateOnly end);
    Task SaveDayAsync(string uid, PlanDay day);

    /// <summary>The stored profile, or null when the user has not set one up yet.</summary>
    Task<NutritionProfile?> GetProfileAsync(string uid);
    Task SaveProfileAsync(string uid, NutritionProfile profile);

    /// <summary>
    /// Meal times, which meals are on, and display preferences. Never null: a user from before 3.1 gets
    /// theirs built once from the old profile, anyone else gets the defaults.
    /// </summary>
    Task<MealPrefs> GetMealPrefsAsync(string uid);
    Task SaveMealPrefsAsync(string uid, MealPrefs prefs);

    Task<List<BodyLog>> GetBodyLogsAsync(string uid);
    Task SaveBodyLogAsync(string uid, BodyLog log);

    /// <summary>The private weekly photos (JPEG data URLs) keyed by the week's Monday. Health data: account only.</summary>
    Task<Dictionary<DateOnly, string>> GetProgressPhotosAsync(string uid);
    Task SaveProgressPhotoAsync(string uid, DateOnly monday, string dataUrl);

    // What the user chose to share from a Sunday review (users/{key}/sharedReview); null when never shared.
    Task<SharedReview?> GetSharedReviewAsync(string uid);
    Task ShareReviewAsync(string uid, SharedReview review);
}
