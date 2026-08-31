using System.Collections.Generic;

namespace MealPlanner.Shared.Models;

public class UserProfile
{
    public string Uid { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string AuthUid { get; set; } = string.Empty;
    public string SyncEmail { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> SelectedMealIds { get; set; } = new();
    public List<string> FavoriteMealIds { get; set; } = new();
    public Dictionary<string, ShoppingShop> Shopping { get; set; } = new();
    public List<string> FriendIds { get; set; } = new();
    public Dictionary<string, FriendRequest> IncomingFriendRequests { get; set; } = new();
    public List<string> OutgoingFriendRequestIds { get; set; } = new();
}
