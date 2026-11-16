using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Nutrition;

/// <summary>
/// Pure rules for syncing meals between two friends. Only catalog meals travel: servings, eaten, skipped,
/// meal prep and leftovers stay personal, quick adds stay personal, and an entry the other person already
/// answered (eaten or skipped) is history and never changed. Nothing before today is touched.
/// </summary>
public static class SyncMerge
{
    /// <summary>
    /// Puts <paramref name="mealId"/> (or nothing) into the slot of <paramref name="target"/>. Keeps the target's
    /// servings when a meal is swapped for another. Returns whether the target changed.
    /// </summary>
    public static bool ApplySlot(PlanDay target, MealSlot slot, string? mealId)
    {
        target.Slots.TryGetValue(slot, out var current);
        if (current is not null && (current.Eaten || current.Skipped || current.Quick is not null)) return false;
        if (string.IsNullOrEmpty(mealId))
            return current is not null && target.Slots.Remove(slot);
        if (current?.MealId == mealId) return false;
        target.Slots[slot] = new SlotEntry { MealId = mealId, Servings = current?.Servings ?? 1 };
        return true;
    }

    /// <summary>
    /// After one person changed a day from <paramref name="before"/> to <paramref name="after"/>, the same meal
    /// changes applied to the partner's day. Returns the partner's new day, or null when nothing changes there.
    /// </summary>
    public static PlanDay? Mirror(PlanDay? before, PlanDay? after, PlanDay? partner, DateOnly date, DateOnly today)
    {
        if (date < today) return null;
        var target = partner?.Clone() ?? new PlanDay();
        target.Date = date;
        var changed = false;
        foreach (var slot in Enum.GetValues<MealSlot>())
        {
            var was = MealOf(before, slot);
            var now = MealOf(after, slot);
            if (was == now) continue;
            changed |= ApplySlot(target, slot, now);
        }
        return changed ? target : null;
    }

    /// <summary>
    /// When a sync starts, the person accepting takes over the requester's meals from <paramref name="from"/>
    /// to <paramref name="to"/>: their own unanswered catalog meals are replaced or removed. Returns the days
    /// that change, so the confirmation can say how many.
    /// </summary>
    public static List<PlanDay> Adopt(IReadOnlyDictionary<DateOnly, PlanDay> theirs, IReadOnlyDictionary<DateOnly, PlanDay> mine, DateOnly from, DateOnly to)
    {
        var changed = new List<PlanDay>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            theirs.TryGetValue(date, out var source);
            var target = mine.TryGetValue(date, out var existing) ? existing.Clone() : new PlanDay();
            target.Date = date;
            var any = false;
            foreach (var slot in Enum.GetValues<MealSlot>())
                any |= ApplySlot(target, slot, MealOf(source, slot));
            if (any) changed.Add(target);
        }
        return changed;
    }

    /// <summary>Whether a change between two versions of a day moved any catalog meal (the only thing mirrored).</summary>
    public static bool MealsChanged(PlanDay? before, PlanDay? after) =>
        Enum.GetValues<MealSlot>().Any(slot => MealOf(before, slot) != MealOf(after, slot));

    private static string? MealOf(PlanDay? day, MealSlot slot) =>
        day is not null && day.Slots.TryGetValue(slot, out var e) && e.Quick is null && !string.IsNullOrEmpty(e.MealId) ? e.MealId : null;
}
