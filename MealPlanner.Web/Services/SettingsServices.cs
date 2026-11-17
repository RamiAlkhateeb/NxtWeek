using MealPlanner.Shared.Services;
using Microsoft.JSInterop;

namespace MealPlanner.Web.Services;

/// <summary>Keeps the UI language in localStorage. The theme lives in Nxt.UI's ThemeService.</summary>
public sealed class BrowserPreferenceService(IJSRuntime js) : ILanguageService
{
    private const string Key = "nxtweek.language";

    public async Task<string> GetAsync() => await js.InvokeAsync<string?>("localStorage.getItem", Key) ?? "ar";

    public Task SetAsync(string value) => js.InvokeVoidAsync("localStorage.setItem", Key, value).AsTask();
}
