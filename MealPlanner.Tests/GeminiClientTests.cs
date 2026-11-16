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

public class GeminiImageTests
{
    [Theory]
    [InlineData("gemini-2.5-flash-image", true)]
    [InlineData("gemini-3-pro-image-preview", true)]
    [InlineData("gemini-2.0-flash-preview-image-generation", true)]
    [InlineData("imagen-4.0-generate-001", false)]
    [InlineData("gemini-2.5-flash", false)]
    [InlineData("gemini-2.5-flash-preview-tts", false)]
    public void Only_gemini_image_models_draw(string name, bool expected) =>
        Assert.Equal(expected, GeminiClient.IsImageModel(name));

    [Fact]
    public void Stable_flash_image_models_come_first()
    {
        var ranked = GeminiClient.RankImageModels(["gemini-3-pro-image-preview", "gemini-2.5-flash-image-preview", "gemini-2.5-flash-image", "gemini-2.5-flash"]);

        Assert.Equal(["gemini-2.5-flash-image", "gemini-2.5-flash-image-preview"], ranked);
    }

    [Fact]
    public void The_first_image_part_is_read()
    {
        var json = """{"candidates":[{"content":{"parts":[{"text":"Here you go"},{"inlineData":{"mimeType":"image/png","data":"AQID"}}]},"finishReason":"STOP"}]}""";

        var image = GeminiClient.ImageFrom(json, "gemini-2.5-flash-image");

        Assert.Equal([1, 2, 3], image.Data);
        Assert.Equal("image/png", image.MimeType);
    }

    [Theory]
    [InlineData("""{"candidates":[{"content":{"parts":[{"text":"I can't draw that"}]},"finishReason":"STOP"}]}""", GeminiErrorKind.Other)]
    [InlineData("""{"candidates":[{"finishReason":"IMAGE_SAFETY"}]}""", GeminiErrorKind.Blocked)]
    [InlineData("""{"promptFeedback":{"blockReason":"SAFETY"}}""", GeminiErrorKind.Blocked)]
    public void A_reply_without_a_picture_is_an_error(string json, GeminiErrorKind kind) =>
        Assert.Equal(kind, Assert.Throws<GeminiApiException>(() => GeminiClient.ImageFrom(json, "m")).Kind);
}
