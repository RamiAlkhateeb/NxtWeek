using System.Collections.Generic;

namespace MealPlanner.Shared.Models;

public class UserProfile
{
    public string Uid { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool AuthVerified { get; set; } = false;
    public string DisplayName { get; set; } = string.Empty;
    public List<Cuisine> PreferredCuisines { get; set; } = new();
    public List<string> SelectedMealIds { get; set; } = new();
    public List<string> FavoriteMealIds { get; set; } = new();
    public string HouseholdId { get; set; } = string.Empty;
    public List<string> PendingLinkRequestUids { get; set; } = new();
}
