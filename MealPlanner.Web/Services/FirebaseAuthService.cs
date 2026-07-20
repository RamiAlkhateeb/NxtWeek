using MealPlanner.Shared.Services;
using Microsoft.JSInterop;

namespace MealPlanner.Web.Services;

public sealed class FirebaseAuthService(IJSRuntime js, FirebaseOptions options) : IAuthService
{
    private object Config => new { apiKey = options.ApiKey, authDomain = options.AuthDomain, projectId = options.ProjectId };
    public async ValueTask<AuthUser?> GetCurrentUserAsync()
    {
        Console.WriteLine("[C#-Auth] GetCurrentUserAsync() called.");
        await js.InvokeVoidAsync("nxtWeekAuth.init", Config);
        var user = await js.InvokeAsync<AuthUser?>("nxtWeekAuth.currentUser");
        Console.WriteLine($"[C#-Auth] GetCurrentUserAsync() returning user: {(user == null ? "null" : user.Uid)}");
        return user;
    }
    public async ValueTask SendSignInLinkAsync(string email, string continueUrl)
    {
        Console.WriteLine($"[C#-Auth] SendSignInLinkAsync() called for email: {email}");
        await js.InvokeVoidAsync("nxtWeekAuth.init", Config);
        await js.InvokeVoidAsync("nxtWeekAuth.sendSignInLink", email, continueUrl);
        Console.WriteLine("[C#-Auth] SendSignInLinkAsync() completed.");
    }
    public async ValueTask<AuthUser?> CompleteSignInAsync(string email)
    {
        Console.WriteLine($"[C#-Auth] CompleteSignInAsync() called for email: {email}");
        await js.InvokeVoidAsync("nxtWeekAuth.init", Config);
        var user = await js.InvokeAsync<AuthUser?>("nxtWeekAuth.completeSignIn", email);
        Console.WriteLine($"[C#-Auth] CompleteSignInAsync() returning user: {(user == null ? "null" : user.Uid)}");
        return user;
    }
    public async ValueTask SignOutAsync()
    {
        Console.WriteLine("[C#-Auth] SignOutAsync() called.");
        await js.InvokeVoidAsync("nxtWeekAuth.init", Config);
        await js.InvokeVoidAsync("nxtWeekAuth.signOut");
        Console.WriteLine("[C#-Auth] SignOutAsync() completed.");
    }
}
