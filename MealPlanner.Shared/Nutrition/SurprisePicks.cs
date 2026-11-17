using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Nutrition;

/// <summary>
/// "Surprise week": random meals for empty days, in the user's language (any language when none match), avoiding
/// meals already planned, with favourites three times as likely. Never the same meal twice. Pure and tested.
/// </summary>
public static class SurprisePicks
{
    public const int FavouriteWeight = 3;

    public static List<MealCatalogItem> Pick(IEnumerable<MealCatalogItem> catalog, string language, IReadOnlySet<string> favourites,
        IReadOnlySet<string> avoid, int count, Random random)
    {
        var live = catalog.Where(m => !m.IsArchived).ToList();
        var inLanguage = live.Where(m => string.Equals(string.IsNullOrWhiteSpace(m.Language) ? "ar" : m.Language, language, StringComparison.OrdinalIgnoreCase)).ToList();
        var pool = inLanguage.Count > 0 ? inLanguage : live;
        var fresh = pool.Where(m => !avoid.Contains(m.Id)).ToList();
        if (fresh.Count > 0) pool = fresh;

        var weighted = pool.SelectMany(m => Enumerable.Repeat(m, favourites.Contains(m.Id) ? FavouriteWeight : 1)).ToList();
        var picks = new List<MealCatalogItem>();
        while (picks.Count < count && weighted.Count > 0)
        {
            var pick = weighted[random.Next(weighted.Count)];
            picks.Add(pick);
            weighted.RemoveAll(m => m.Id == pick.Id);
        }
        return picks;
    }
}
