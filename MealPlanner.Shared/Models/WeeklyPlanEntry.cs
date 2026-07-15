using System;

namespace MealPlanner.Shared.Models;

public class WeeklyPlanEntry
{
    public DateOnly Date { get; set; }
    public string MealId { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
}
