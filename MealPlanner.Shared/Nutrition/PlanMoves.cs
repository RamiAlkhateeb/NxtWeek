using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Nutrition;

/// <summary>Pure rules for moving or copying a planned meal to another slot or day.</summary>
public static class PlanMoves
{
    /// <summary>
    /// Moves the entry to the target slot. A meal already there swaps back into the source slot.
    /// On another day both start unanswered (not eaten, not skipped); within the same day they keep their state.
    /// False when the source slot is empty or source and target are the same place.
    /// </summary>
    public static bool Move(PlanDay from, MealSlot fromSlot, PlanDay to, MealSlot toSlot)
    {
        var sameDay = ReferenceEquals(from, to) || from.Date == to.Date;
        if (sameDay && fromSlot == toSlot) return false;
        if (!from.Slots.TryGetValue(fromSlot, out var moving)) return false;

        to.Slots.TryGetValue(toSlot, out var displaced);
        to.Slots[toSlot] = sameDay ? moving : Unanswered(moving);
        if (displaced is null) from.Slots.Remove(fromSlot);
        else from.Slots[fromSlot] = sameDay ? displaced : Unanswered(displaced);
        return true;
    }

    /// <summary>
    /// Puts a fresh copy of the entry in the target slot, replacing what is there: same meal and servings,
    /// not eaten, not skipped, no meal prep. False when the source slot is empty or it is the same place.
    /// </summary>
    public static bool Copy(PlanDay from, MealSlot fromSlot, PlanDay to, MealSlot toSlot)
    {
        if ((ReferenceEquals(from, to) || from.Date == to.Date) && fromSlot == toSlot) return false;
        if (!from.Slots.TryGetValue(fromSlot, out var source)) return false;
        to.Slots[toSlot] = new SlotEntry
        {
            MealId = source.MealId,
            Quick = source.Quick is { } q ? new QuickAddEntry { Name = q.Name, Amount = q.Amount, Kcal = q.Kcal, Protein = q.Protein, Carbs = q.Carbs, Fat = q.Fat } : null,
            Servings = source.Servings
        };
        return true;
    }

    /// <summary>
    /// "Copy this week": each day of the source week puts fresh copies (meal and servings only) into the matching
    /// day of the target week. Only empty slots are filled, so nothing already planned is replaced.
    /// Days before <paramref name="notBefore"/> are left alone, so a week already under way only gets its days ahead.
    /// Returns the target days that changed.
    /// </summary>
    public static List<PlanDay> CopyWeek(IReadOnlyDictionary<DateOnly, PlanDay> days, DateOnly fromMonday, DateOnly toMonday, DateOnly? notBefore = null)
    {
        var changed = new List<PlanDay>();
        if (fromMonday == toMonday) return changed;
        for (var i = 0; i < 7; i++)
        {
            if (!days.TryGetValue(fromMonday.AddDays(i), out var from) || from.Slots.Count == 0) continue;
            var date = toMonday.AddDays(i);
            if (date < notBefore) continue;
            var to = days.TryGetValue(date, out var existing) ? existing.Clone() : new PlanDay { Date = date };
            to.Date = date;
            var any = false;
            foreach (var slot in from.Slots.Keys)
                if (!to.Slots.ContainsKey(slot) && (from.Slots[slot].MealId is not null || from.Slots[slot].Quick is not null))
                    any |= Copy(from, slot, to, slot);
            if (any) changed.Add(to);
        }
        return changed;
    }

    private static SlotEntry Unanswered(SlotEntry entry)
    {
        entry.Eaten = false;
        entry.Skipped = false;
        return entry;
    }
}
