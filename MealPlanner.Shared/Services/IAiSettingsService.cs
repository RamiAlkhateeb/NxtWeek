namespace MealPlanner.Shared.Services;

/// <summary>
/// The Gemini API key and model a user has pasted into Settings for the AI assistant.
/// Stored client-side only (browser localStorage) — there is no backend to hold it,
/// and each person supplies their own free key, mirroring how the sibling
/// IdeaSplit/task-breaker app does it.
/// </summary>
public interface IAiSettingsService
{
    Task<string?> GetGeminiApiKeyAsync();
    Task SaveGeminiApiKeyAsync(string apiKey);
    Task<string> GetGeminiModelAsync();
    Task SaveGeminiModelAsync(string model);
}
