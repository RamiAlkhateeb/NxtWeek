using System.Text.RegularExpressions;

namespace MealPlanner.Shared.Nutrition;

/// <summary>
/// Seeded meals that are the same dish in three languages: n12 (Arabic), en_n12, de_n12. The English one carries
/// the shared photo; the others show it. Mirrors englishTwinId in photo-worker/src/index.js.
/// </summary>
public static partial class MealTwins
{
    [GeneratedRegex(@"^(?:de_)?(n\d+)$")]
    private static partial Regex Twinned();

    /// <summary>The English meal that is the same dish, or null (English meals and meals without twins).</summary>
    public static string? EnglishTwinId(string? mealId) =>
        mealId is not null && Twinned().Match(mealId) is { Success: true } m ? $"en_{m.Groups[1].Value}" : null;
}
