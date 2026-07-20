using MealPlanner.Shared.Services;
using Microsoft.JSInterop;
using System;
using System.Threading.Tasks;

namespace MealPlanner.Web.Services;

public sealed class LocalAuthService(IJSRuntime js) : IAuthService
{
    private AuthUser? _cachedUser;

    public async ValueTask<AuthUser?> GetCurrentUserAsync()
    {
        if (_cachedUser is not null)
        {
            return _cachedUser;
        }

        try
        {
            var sanitizedEmail = await js.InvokeAsync<string?>("localStorage.getItem", "nxtweek.currentUserEmail");
            if (string.IsNullOrWhiteSpace(sanitizedEmail))
            {
                return null;
            }

            var rawEmail = await js.InvokeAsync<string?>("localStorage.getItem", "nxtweek.currentUserRawEmail");
            if (string.IsNullOrWhiteSpace(rawEmail))
            {
                rawEmail = sanitizedEmail.Replace("_at_", "@").Replace("_dot_", ".");
            }

            _cachedUser = new AuthUser
            {
                Uid = sanitizedEmail,
                Email = rawEmail,
                IdToken = ""
            };

            return _cachedUser;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LocalAuthService] Error reading from localStorage: {ex.Message}");
            return null;
        }
    }

    public async ValueTask<AuthUser> SignInAsync(string email)
    {
        var sanitized = EmailUtils.Sanitize(email);
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", "nxtweek.currentUserEmail", sanitized);
            await js.InvokeVoidAsync("localStorage.setItem", "nxtweek.currentUserRawEmail", email.Trim());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LocalAuthService] Error writing to localStorage: {ex.Message}");
        }

        _cachedUser = new AuthUser
        {
            Uid = sanitized,
            Email = email.Trim(),
            IdToken = ""
        };

        return _cachedUser;
    }

    public async ValueTask SignOutAsync()
    {
        _cachedUser = null;
        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", "nxtweek.currentUserEmail");
            await js.InvokeVoidAsync("localStorage.removeItem", "nxtweek.currentUserRawEmail");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LocalAuthService] Error removing from localStorage: {ex.Message}");
        }
    }
}
