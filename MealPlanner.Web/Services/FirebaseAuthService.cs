using MealPlanner.Shared.Services;
using Microsoft.JSInterop;

namespace MealPlanner.Web.Services;

public sealed class FirebaseAuthService(IJSRuntime js, FirebaseOptions options) : IAuthService
{
    private object Config => new { apiKey = options.ApiKey, authDomain = options.AuthDomain, projectId = options.ProjectId };
    public async ValueTask<AuthUser?> GetCurrentUserAsync() { await js.InvokeVoidAsync("nxtWeekAuth.init", Config); return await js.InvokeAsync<AuthUser?>("nxtWeekAuth.currentUser"); }
    public async ValueTask SendSignInLinkAsync(string email, string continueUrl) { await js.InvokeVoidAsync("nxtWeekAuth.init", Config); await js.InvokeVoidAsync("nxtWeekAuth.sendSignInLink", email, continueUrl); }
    public async ValueTask<AuthUser?> CompleteSignInAsync(string email) { await js.InvokeVoidAsync("nxtWeekAuth.init", Config); return await js.InvokeAsync<AuthUser?>("nxtWeekAuth.completeSignIn", email); }
    public async ValueTask SignOutAsync() { await js.InvokeVoidAsync("nxtWeekAuth.init", Config); await js.InvokeVoidAsync("nxtWeekAuth.signOut"); }
}
