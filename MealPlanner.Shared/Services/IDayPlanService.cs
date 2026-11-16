using MealPlanner.Shared.Models;
using MealPlanner.Shared.Nutrition;

namespace MealPlanner.Shared.Services;

/// <summary>
/// The only writer of a user's day slots. Every change reads the day, changes it and saves the
/// whole day back, so a day saved before slots existed becomes the new shape on its first edit.
/// </summary>
public interface IDayPlanService
{
    Task<Dictionary<DateOnly, PlanDay>> GetDaysAsync(string uid, DateOnly start, DateOnly end);
    Task<PlanDay> SetEntryAsync(string uid, DateOnly date, MealSlot slot, SlotEntry entry);
    Task<PlanDay> UpdateEntryAsync(string uid, DateOnly date, MealSlot slot, Action<SlotEntry> change);
    Task<PlanDay> RemoveEntryAsync(string uid, DateOnly date, MealSlot slot);
    /// <summary>Replaces every slot of the day (copying a friend's day).</summary>
    Task<PlanDay> ReplaceSlotsAsync(string uid, DateOnly date, Dictionary<MealSlot, SlotEntry> slots);
    /// <summary>Moves a meal to another slot or day (swapping with what is there). Returns the days that changed.</summary>
    Task<List<PlanDay>> MoveEntryAsync(string uid, DateOnly fromDate, MealSlot fromSlot, DateOnly toDate, MealSlot toSlot);
    /// <summary>Copies a meal to another slot or day, replacing what is there. Returns the day that changed, or null.</summary>
    Task<PlanDay?> CopyEntryAsync(string uid, DateOnly fromDate, MealSlot fromSlot, DateOnly toDate, MealSlot toSlot);
    /// <summary>Copies a week's meals and servings into the empty slots of another week (<see cref="PlanMoves.CopyWeek"/>). Returns the days written.</summary>
    Task<List<PlanDay>> CopyWeekAsync(string uid, DateOnly fromMonday, DateOnly toMonday, DateOnly? notBefore = null);
}

public sealed class DayPlanService(INutritionStore store) : IDayPlanService
{
    public Task<Dictionary<DateOnly, PlanDay>> GetDaysAsync(string uid, DateOnly start, DateOnly end) =>
        store.GetDaysAsync(uid, start, end);

    public Task<PlanDay> SetEntryAsync(string uid, DateOnly date, MealSlot slot, SlotEntry entry) =>
        ChangeAsync(uid, date, day => day.Slots[slot] = entry);

    public Task<PlanDay> UpdateEntryAsync(string uid, DateOnly date, MealSlot slot, Action<SlotEntry> change) =>
        ChangeAsync(uid, date, day => { if (day.Slots.TryGetValue(slot, out var entry)) change(entry); });

    public Task<PlanDay> RemoveEntryAsync(string uid, DateOnly date, MealSlot slot) =>
        ChangeAsync(uid, date, day => day.Slots.Remove(slot));

    public Task<PlanDay> ReplaceSlotsAsync(string uid, DateOnly date, Dictionary<MealSlot, SlotEntry> slots) =>
        ChangeAsync(uid, date, day => day.Slots = new Dictionary<MealSlot, SlotEntry>(slots));

    public async Task<List<PlanDay>> MoveEntryAsync(string uid, DateOnly fromDate, MealSlot fromSlot, DateOnly toDate, MealSlot toSlot)
    {
        var from = await LoadAsync(uid, fromDate);
        var to = fromDate == toDate ? from : await LoadAsync(uid, toDate);
        if (!PlanMoves.Move(from, fromSlot, to, toSlot)) return [];
        // The target is saved first: if the second save fails the meal exists twice rather than nowhere.
        await store.SaveDayAsync(uid, to);
        if (fromDate == toDate) return [to];
        await store.SaveDayAsync(uid, from);
        return [to, from];
    }

    public async Task<PlanDay?> CopyEntryAsync(string uid, DateOnly fromDate, MealSlot fromSlot, DateOnly toDate, MealSlot toSlot)
    {
        var from = await LoadAsync(uid, fromDate);
        var to = fromDate == toDate ? from : await LoadAsync(uid, toDate);
        if (!PlanMoves.Copy(from, fromSlot, to, toSlot)) return null;
        await store.SaveDayAsync(uid, to);
        return to;
    }

    public async Task<List<PlanDay>> CopyWeekAsync(string uid, DateOnly fromMonday, DateOnly toMonday, DateOnly? notBefore = null)
    {
        var from = await store.GetDaysAsync(uid, fromMonday, fromMonday.AddDays(6));
        var to = await store.GetDaysAsync(uid, toMonday, toMonday.AddDays(6));
        var all = new Dictionary<DateOnly, PlanDay>(from);
        foreach (var (date, day) in to) all[date] = day;
        var changed = PlanMoves.CopyWeek(all, fromMonday, toMonday, notBefore);
        foreach (var day in changed) await store.SaveDayAsync(uid, day);
        return changed;
    }

    private async Task<PlanDay> LoadAsync(string uid, DateOnly date)
    {
        var days = await store.GetDaysAsync(uid, date, date);
        var day = days.TryGetValue(date, out var existing) ? existing : new PlanDay { Date = date };
        day.Date = date;
        return day;
    }

    private async Task<PlanDay> ChangeAsync(string uid, DateOnly date, Action<PlanDay> change)
    {
        var days = await store.GetDaysAsync(uid, date, date);
        var day = days.TryGetValue(date, out var existing) ? existing : new PlanDay { Date = date };
        day.Date = date;
        change(day);
        await store.SaveDayAsync(uid, day);
        return day;
    }
}
