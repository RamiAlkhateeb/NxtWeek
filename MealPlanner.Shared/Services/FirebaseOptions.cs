namespace MealPlanner.Shared.Services;

public class FirebaseOptions
{
    public string DatabaseUrl { get; set; } = "https://YOUR-PROJECT-ID-default-rtdb.firebaseio.com";
    // Copy these values from Firebase Console > Project settings > Your apps (Web app).
    // They are public identifiers, not secrets.
    public string ApiKey { get; set; } = string.Empty;
    public string AuthDomain { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
}
