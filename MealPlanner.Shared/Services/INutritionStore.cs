using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

/// <summary>
/// Slot plans, the nutrition profile and body measurements, all under users/{key} in Firebase.
/// Profile and body data belong to a signed-in account and are never written to localStorage.
/// </summary>
public interface INutritionStore
{
    /// <summary>Days in [start, end], keyed by date. Days saved before slots existed come back with their meal as dinner.</summary>
    Task<Dictionary<DateOnly, PlanDay>> GetDaysAsync(string uid, DateOnly start, DateOnly end);
    Task SaveDayAsync(string uid, PlanDay day);

    /// <summary>The stored profile, or null when the user has not set one up yet.</summary>
    Task<NutritionProfile?> GetProfileAsync(string uid);
    Task SaveProfileAsync(string uid, NutritionProfile profile);

    Task<List<BodyLog>> GetBodyLogsAsync(string uid);
    Task SaveBodyLogAsync(string uid, BodyLog log);
}
