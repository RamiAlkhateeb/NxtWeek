using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Nutrition;

public sealed record Suggestion(MealCatalogItem Meal, NutritionInfo Nutrition);

public sealed record PlannedPick(MealSlot Slot, MealCatalogItem Meal, NutritionInfo Nutrition);

public sealed record ProteinPlan(IReadOnlyList<PlannedPick> Picks, double Kcal, double Protein);

/// <summary>
/// Pure suggestion rules from the 3.0 prototype. Candidates are catalog meals the caller has already
/// narrowed to the user's language; meals without nutrition are never suggested.
/// </summary>
public static class Suggestions
{
    public const int MaxSuggestions = 3;
    public const double SnackMaxKcal = 250;
    /// <summary>Per slot, only the most protein-dense candidates go into the brute-force plan, so it stays fast.</summary>
    public const int PlannerCandidatesPerSlot = 10;

    public static bool Suits(MealCatalogItem meal, MealSlot slot) => meal.SuitableSlots.Count == 0 || meal.SuitableSlots.Contains(slot);

    public static HashSet<string> UsedMealIds(PlanDay? day, IReadOnlyList<MealSlot> slots) =>
        day is null ? [] : slots.Where(day.Slots.ContainsKey).Select(s => day.Slots[s].MealId).OfType<string>().ToHashSet();

    /// <summary>
    /// Top 3 meals for an empty slot, highest protein first: suits the slot, not already on the day,
    /// fits the open calories, and (with several slots still empty) leaves room for the others.
    /// </summary>
    public static List<Suggestion> ForSlot(MealSlot slot, double openKcal, int emptySlots, ISet<string> usedToday, IEnumerable<MealCatalogItem> candidates)
    {
        var empty = Math.Max(1, emptySlots);
        var perSlot = Math.Max(150, openKcal / empty) * 1.3;
        return candidates
            .Where(m => m.Nutrition is not null && !usedToday.Contains(m.Id) && Suits(m, slot))
            .Where(m => m.Nutrition!.Kcal <= openKcal && (empty == 1 || m.Nutrition.Kcal <= perSlot))
            .OrderByDescending(m => m.Nutrition!.Protein)
            .ThenBy(m => m.Nutrition!.Kcal)
            .Take(MaxSuggestions)
            .Select(m => new Suggestion(m, m.Nutrition!))
            .ToList();
    }

    /// <summary>
    /// One meal per empty slot (no meal twice, nothing already on the day) that reaches the open protein
    /// within the open calories, with as little extra protein and as few calories as possible.
    /// Null when there are no empty slots or no combination fits.
    /// </summary>
    public static ProteinPlan? PlanProtein(IReadOnlyList<MealSlot> emptySlots, double openKcal, double openProtein, ISet<string> usedToday, IEnumerable<MealCatalogItem> candidates)
    {
        if (emptySlots.Count == 0) return null;
        var pool = candidates.Where(m => m.Nutrition is not null && !usedToday.Contains(m.Id) && m.Nutrition.Kcal <= openKcal).ToList();
        var perSlot = emptySlots
            .Select(s => pool.Where(m => Suits(m, s)).OrderByDescending(m => m.Nutrition!.Protein).Take(PlannerCandidatesPerSlot).ToList())
            .ToList();

        PlannedPick[]? best = null;
        double bestScore = double.MaxValue, bestKcal = 0, bestProtein = 0;
        var picks = new PlannedPick[emptySlots.Count];

        void Search(int i, double kcal, double protein)
        {
            if (kcal > openKcal) return;
            if (i == emptySlots.Count)
            {
                // Reaching the protein beats any shortfall; then the least overshoot, then the fewest calories.
                var score = protein >= openProtein ? protein - openProtein : 1000 + (openProtein - protein);
                score += kcal / 1000;
                if (score < bestScore) { bestScore = score; best = (PlannedPick[])picks.Clone(); bestKcal = kcal; bestProtein = protein; }
                return;
            }
            foreach (var meal in perSlot[i])
            {
                if (picks.Take(i).Any(p => p.Meal.Id == meal.Id)) continue;
                picks[i] = new PlannedPick(emptySlots[i], meal, meal.Nutrition!);
                Search(i + 1, kcal + meal.Nutrition!.Kcal, protein + meal.Nutrition.Protein);
            }
        }

        Search(0, 0, 0);
        return best is null ? null : new ProteinPlan(best, bestKcal, bestProtein);
    }

    /// <summary>"Snack under 250 kcal with protein": the highest-protein snack that also fits what is open.</summary>
    public static Suggestion? Snack(double openKcal, ISet<string> usedToday, IEnumerable<MealCatalogItem> candidates) =>
        candidates
            .Where(m => m.Nutrition is not null && !usedToday.Contains(m.Id) && m.SuitableSlots.Contains(MealSlot.Snack))
            .Where(m => m.Nutrition!.Kcal <= SnackMaxKcal && m.Nutrition.Kcal <= openKcal)
            .OrderByDescending(m => m.Nutrition!.Protein)
            .Select(m => new Suggestion(m, m.Nutrition!))
            .FirstOrDefault();
}

[Flags]
public enum MealFilters
{
    None = 0,
    HighProtein = 1,
    Under600 = 2,
    Quick = 4,
    Favourites = 8
}

/// <summary>The Meals tab's nutrition filters and the pick-mode order.</summary>
public static class MealFiltering
{
    public const double HighProteinGrams = 30;
    public const double LowKcal = 600;
    public const int QuickMinutes = 20;

    public static bool Matches(MealCatalogItem meal, MealFilters filters, ISet<string> favourites)
    {
        var n = meal.Nutrition;
        if (filters.HasFlag(MealFilters.HighProtein) && !(n is not null && n.Protein >= HighProteinGrams)) return false;
        if (filters.HasFlag(MealFilters.Under600) && !(n is not null && n.Kcal < LowKcal)) return false;
        if (filters.HasFlag(MealFilters.Quick) && !(meal.PreparationMinutes is > 0 and <= QuickMinutes)) return false;
        if (filters.HasFlag(MealFilters.Favourites) && !favourites.Contains(meal.Id)) return false;
        return true;
    }

    public static bool FitsToday(MealCatalogItem meal, double openKcal) => meal.Nutrition is not null && meal.Nutrition.Kcal <= openKcal;

    /// <summary>Choosing for a slot: meals that fit the open calories first, then by protein.</summary>
    public static IEnumerable<MealCatalogItem> PickOrder(IEnumerable<MealCatalogItem> meals, double openKcal) =>
        meals.OrderByDescending(m => FitsToday(m, openKcal)).ThenByDescending(m => m.Nutrition?.Protein ?? 0);
}
