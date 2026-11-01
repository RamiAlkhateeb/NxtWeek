using Nxt.UI.Ai;

namespace MealPlanner.Tests;

/// <summary>The pure parts of the shared Gemini client: which models are offered and tried, and how errors read.</summary>
public class GeminiClientTests
{
    [Theory]
    [InlineData("gemini-2.5-flash", true)]
    [InlineData("gemini-flash-latest", true)]
    [InlineData("gemini-2.5-pro", true)]
    [InlineData("gemini-2.5-flash-preview-tts", false)]
    [InlineData("gemini-2.0-flash-preview-image-generation", false)]
    [InlineData("gemini-embedding-001", false)]
    [InlineData("gemini-2.5-flash-native-audio-dialog", false)]
    [InlineData("gemini-live-2.5-flash", false)]
    [InlineData("gemini-2.5-computer-use-preview", false)]
    [InlineData("gemma-3-27b-it", false)]
    [InlineData("learnlm-2.0-flash", false)]
    [InlineData("", false)]
    public void Only_chat_models_are_offered(string name, bool expected) =>
        Assert.Equal(expected, GeminiClient.IsChatModel(name));

    [Fact]
    public void Backups_are_flash_lite_then_flash_newest_first_without_the_selected_one()
    {
        var available = new[]
        {
            "gemini-2.0-flash", "gemini-2.5-pro", "gemini-2.5-flash", "gemini-2.5-flash-lite",
            "gemini-2.0-flash-lite", "gemini-2.5-flash-preview-tts", "gemini-3.0-flash-preview", "gemma-3-4b-it"
        };

        var ranked = GeminiClient.RankFallbacks(available, selected: "gemini-2.5-pro");

        Assert.Equal(["gemini-2.5-flash-lite", "gemini-2.0-flash-lite", "gemini-2.5-flash"], ranked);
    }

    [Fact]
    public void Latest_aliases_lead_their_group()
    {
        var ranked = GeminiClient.RankFallbacks(["gemini-2.5-flash", "gemini-flash-latest"], selected: "x");

        Assert.Equal("gemini-flash-latest", ranked[0]);
    }

    [Theory]
    [InlineData(400, "API_KEY_INVALID", "INVALID_ARGUMENT", "API key not valid. Please pass a valid API key.", GeminiErrorKind.InvalidKey)]
    [InlineData(403, null, "PERMISSION_DENIED", "Method doesn't allow unregistered callers.", GeminiErrorKind.InvalidKey)]
    [InlineData(429, null, "RESOURCE_EXHAUSTED", "You exceeded your current quota.", GeminiErrorKind.Quota)]
    [InlineData(404, null, "NOT_FOUND", "models/gemini-9 is not found for API version v1beta.", GeminiErrorKind.ModelUnavailable)]
    [InlineData(400, null, "INVALID_ARGUMENT", "JSON mode is not enabled for this model.", GeminiErrorKind.ModelUnavailable)]
    [InlineData(503, null, "UNAVAILABLE", "The model is overloaded.", GeminiErrorKind.Other)]
    public void Errors_say_what_to_do(int status, string? reason, string? statusText, string message, GeminiErrorKind expected) =>
        Assert.Equal(expected, GeminiClient.ClassifyError(status, reason, statusText, message));
}
