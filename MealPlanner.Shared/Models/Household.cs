using System.Collections.Generic;

namespace MealPlanner.Shared.Models;

public class Household
{
    public string Id { get; set; } = string.Empty;
    public List<string> MemberIds { get; set; } = new();
    public Dictionary<string, WeeklyPlanEntry> WeeklyPlan { get; set; } = new();
    public List<string> FavoriteMealIds { get; set; } = new();
}
