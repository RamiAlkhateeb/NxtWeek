using System.Collections.Generic;

namespace MealPlanner.Shared.Models;

public class UserProfile
{
    public string Username { get; set; } = string.Empty;
    public List<Cuisine> PreferredCuisines { get; set; } = new();
    public List<string> SelectedMealIds { get; set; } = new();
    public List<string> FavoriteMealIds { get; set; } = new();
    public string HouseholdId { get; set; } = string.Empty;
    public List<string> PendingLinkRequests { get; set; } = new();
}
