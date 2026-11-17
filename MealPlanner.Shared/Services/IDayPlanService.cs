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
    /// <summary>Moves a meal to another slot or day (swapping with what is there). Returns the days that changed.</summary>
    Task<List<PlanDay>> MoveEntryAsync(string uid, DateOnly fromDate, MealSlot fromSlot, DateOnly toDate, MealSlot toSlot);
    /// <summary>Copies a meal to another slot or day, replacing what is there. Returns the day that changed, or null.</summary>
    Task<PlanDay?> CopyEntryAsync(string uid, DateOnly fromDate, MealSlot fromSlot, DateOnly toDate, MealSlot toSlot);
    /// <summary>Copies a week's meals and servings into the empty slots of another week (<see cref="PlanMoves.CopyWeek"/>). Returns the days written.</summary>
    Task<List<PlanDay>> CopyWeekAsync(string uid, DateOnly fromMonday, DateOnly toMonday, DateOnly? notBefore = null);
}

/// <remarks>
/// When the user syncs with a friend (<see cref="SyncLink"/>), every change that moves a catalog meal on a day
/// from today on is mirrored into the partner's day by <see cref="SyncMerge.Mirror"/>. A failed mirror never
/// fails the user's own save.
/// </remarks>
public sealed class DayPlanService(INutritionStore store, Func<DateOnly>? today = null) : IDayPlanService
{
    private DateOnly Today => today?.Invoke() ?? DateOnly.FromDateTime(DateTime.Today);

    public Task<Dictionary<DateOnly, PlanDay>> GetDaysAsync(string uid, DateOnly start, DateOnly end) =>
        store.GetDaysAsync(uid, start, end);

    public Task<PlanDay> SetEntryAsync(string uid, DateOnly date, MealSlot slot, SlotEntry entry) =>
        ChangeAsync(uid, date, day => day.Slots[slot] = entry);

    public Task<PlanDay> UpdateEntryAsync(string uid, DateOnly date, MealSlot slot, Action<SlotEntry> change) =>
        ChangeAsync(uid, date, day => { if (day.Slots.TryGetValue(slot, out var entry)) change(entry); });

    public Task<PlanDay> RemoveEntryAsync(string uid, DateOnly date, MealSlot slot) =>
        ChangeAsync(uid, date, day => day.Slots.Remove(slot));

    public async Task<List<PlanDay>> MoveEntryAsync(string uid, DateOnly fromDate, MealSlot fromSlot, DateOnly toDate, MealSlot toSlot)
    {
        var from = await LoadAsync(uid, fromDate);
        var to = fromDate == toDate ? from : await LoadAsync(uid, toDate);
        var fromBefore = from.Clone();
        var toBefore = to.Clone();
        if (!PlanMoves.Move(from, fromSlot, to, toSlot)) return [];
        // The target is saved first: if the second save fails the meal exists twice rather than nowhere.
        await SaveAsync(uid, toBefore, to);
        if (fromDate == toDate) return [to];
        await SaveAsync(uid, fromBefore, from);
        return [to, from];
    }

    public async Task<PlanDay?> CopyEntryAsync(string uid, DateOnly fromDate, MealSlot fromSlot, DateOnly toDate, MealSlot toSlot)
    {
        var from = await LoadAsync(uid, fromDate);
        var to = fromDate == toDate ? from : await LoadAsync(uid, toDate);
        var before = to.Clone();
        if (!PlanMoves.Copy(from, fromSlot, to, toSlot)) return null;
        await SaveAsync(uid, before, to);
        return to;
    }

    public async Task<List<PlanDay>> CopyWeekAsync(string uid, DateOnly fromMonday, DateOnly toMonday, DateOnly? notBefore = null)
    {
        var from = await store.GetDaysAsync(uid, fromMonday, fromMonday.AddDays(6));
        var to = await store.GetDaysAsync(uid, toMonday, toMonday.AddDays(6));
        var all = new Dictionary<DateOnly, PlanDay>(from);
        foreach (var (date, day) in to) all[date] = day;
        var changed = PlanMoves.CopyWeek(all, fromMonday, toMonday, notBefore);
        foreach (var day in changed) await SaveAsync(uid, to.GetValueOrDefault(day.Date), day);
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
        var day = await LoadAsync(uid, date);
        var before = day.Clone();
        change(day);
        await SaveAsync(uid, before, day);
        return day;
    }

    private async Task SaveAsync(string uid, PlanDay? before, PlanDay after)
    {
        await store.SaveDayAsync(uid, after);
        if (after.Date < Today || !SyncMerge.MealsChanged(before, after)) return;
        try
        {
            if (await store.GetSyncLinkAsync(uid) is not { Partner.Length: > 0 } link) return;
            var partnerDay = (await store.GetDaysAsync(link.Partner, after.Date, after.Date)).GetValueOrDefault(after.Date);
            if (SyncMerge.Mirror(before, after, partnerDay, after.Date, Today) is { } mirrored)
                await store.SaveDayAsync(link.Partner, mirrored);
        }
        catch { /* the partner's copy catches up on their next change; my own save stands */ }
    }
}
