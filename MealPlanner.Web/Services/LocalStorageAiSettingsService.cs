using MealPlanner.Shared.Services;
using Microsoft.JSInterop;

namespace MealPlanner.Web.Services;

public class LocalStorageAiSettingsService : IAiSettingsService
{
    private const string ApiKeyKey = "nxtweek.geminiApiKey";
    private const string ModelKey = "nxtweek.geminiModel";
    public const string DefaultModel = "gemini-2.0-flash";

    private readonly IJSRuntime _js;

    public LocalStorageAiSettingsService(IJSRuntime js) => _js = js;

    public Task<string?> GetGeminiApiKeyAsync() => _js.InvokeAsync<string?>("localStorage.getItem", ApiKeyKey).AsTask();

    public Task SaveGeminiApiKeyAsync(string apiKey) => _js.InvokeVoidAsync("localStorage.setItem", ApiKeyKey, apiKey.Trim()).AsTask();

    public async Task<string> GetGeminiModelAsync() => await _js.InvokeAsync<string?>("localStorage.getItem", ModelKey) ?? DefaultModel;

    public Task SaveGeminiModelAsync(string model) =>
        _js.InvokeVoidAsync("localStorage.setItem", ModelKey, string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim()).AsTask();
}
