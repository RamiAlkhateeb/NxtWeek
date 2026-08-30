namespace MealPlanner.Shared.Models;

public class FriendRequest
{
    public string SenderUid { get; set; } = string.Empty;
    public string SenderDisplayName { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
}
