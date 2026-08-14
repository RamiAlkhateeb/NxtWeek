using MealPlanner.Shared.Services;
using Microsoft.JSInterop;
namespace MealPlanner.Web.Services;
public sealed class LocalDataExportService(IAuthService auth, IUserService users) : IDataExportService
{ public async Task<string> ExportCurrentWeekAsync() { var user = await auth.GetCurrentUserAsync() ?? throw new InvalidOperationException(); var start = DateOnly.FromDateTime(DateTime.Today).AddDays(-(7 + (int)DateTime.Today.DayOfWeek - 1) % 7); var plan = await users.GetWeeklyPlanAsync(user.Uid, start, start.AddDays(6)); return string.Join('\n', plan.Select(x => $"{x.Date:yyyy-MM-dd},{x.MealId}")); } }
public sealed class LocalDataImportService(IJSRuntime js) : IDataImportService
{ public async Task ClearLocalDataAsync() { await js.InvokeVoidAsync("localStorage.removeItem", "meals-cache"); } }
public sealed class BrowserPreferenceService(IJSRuntime js) : IThemeService, ILanguageService
{ public Task<string> GetAsync() => js.InvokeAsync<string?>("localStorage.getItem", "nxtweek.theme").AsTask().ContinueWith(x => x.Result ?? "system"); public Task SetAsync(string v) => js.InvokeVoidAsync("localStorage.setItem", "nxtweek.theme", v).AsTask(); Task<string> ILanguageService.GetAsync() => js.InvokeAsync<string?>("localStorage.getItem", "nxtweek.language").AsTask().ContinueWith(x => x.Result ?? "ar"); Task ILanguageService.SetAsync(string v) => js.InvokeVoidAsync("localStorage.setItem", "nxtweek.language", v).AsTask(); }
