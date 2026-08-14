using System.Collections.Generic;

namespace MealPlanner.Shared.Models;

public class UserProfile
{
    public string Uid { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> SelectedMealIds { get; set; } = new();
    public List<string> FavoriteMealIds { get; set; } = new();
}
