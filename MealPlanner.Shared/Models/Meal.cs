namespace MealPlanner.Shared.Models;

public class Meal
{
    public string Id { get; set; } = string.Empty;   // ISO date, e.g. "2026-02-02"
    public DateOnly Date { get; set; }
    public string DayName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
}