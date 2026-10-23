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
    Task<PlanDay> SetDayTypeAsync(string uid, DateOnly date, DayType type);
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

    public Task<PlanDay> SetDayTypeAsync(string uid, DateOnly date, DayType type) =>
        ChangeAsync(uid, date, day => day.Type = type);

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
