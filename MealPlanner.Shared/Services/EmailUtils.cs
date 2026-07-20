namespace MealPlanner.Shared.Services;

public static class EmailUtils
{
    /// <summary>
    /// Converts an email into a Firebase Realtime Database-safe key.
    /// Firebase keys cannot contain '.', '#', '$', '[', ']', or '/'.
    /// </summary>
    public static string Sanitize(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty.", nameof(email));

        return email.Trim()
                     .ToLowerInvariant()
                     .Replace("@", "_at_")
                     .Replace(".", "_dot_")
                     .Replace("#", "_hash_")
                     .Replace("$", "_dollar_")
                     .Replace("[", "_lb_")
                     .Replace("]", "_rb_")
                     .Replace("/", "_slash_");
    }
}