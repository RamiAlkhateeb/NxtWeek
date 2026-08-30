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
        if (_cachedUser is not null && !_cachedUser.IsGuest)
        {
            return _cachedUser;
        }

        try
        {
            var emailUser = await js.InvokeAsync<AuthUser?>("nxtweek.emailAuth.getCurrentUser");
            if (emailUser is not null && !string.IsNullOrWhiteSpace(emailUser.AuthUid))
            {
                var dataKey = await js.InvokeAsync<string?>("localStorage.getItem", "nxtweek.activeUserKey");
                _cachedUser = new AuthUser { Uid = string.IsNullOrWhiteSpace(dataKey) ? emailUser.Uid : dataKey, AuthUid = emailUser.AuthUid, Email = emailUser.Email, DisplayName = emailUser.DisplayName, IdToken = emailUser.IdToken, IsGuest = false };
                return _cachedUser;
            }
            if (_cachedUser is not null) return _cachedUser;
            var sanitizedEmail = await js.InvokeAsync<string?>("localStorage.getItem", "nxtweek.currentUserEmail");
            if (string.IsNullOrWhiteSpace(sanitizedEmail))
            {
                sanitizedEmail = await js.InvokeAsync<string?>("localStorage.getItem", "nxtweek.guestId");
                if (string.IsNullOrWhiteSpace(sanitizedEmail))
                {
                    sanitizedEmail = "guest_" + Guid.NewGuid().ToString("N");
                    await js.InvokeVoidAsync("localStorage.setItem", "nxtweek.guestId", sanitizedEmail);
                }
                _cachedUser = new AuthUser { Uid = sanitizedEmail, Email = "", DisplayName = "ضيف", IdToken = "", IsGuest = true };
                return _cachedUser;
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
                DisplayName = rawEmail,
                IdToken = "",
                IsGuest = false
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
            DisplayName = email.Trim(),
            IdToken = "",
            IsGuest = false
        };

        return _cachedUser;
    }

    public async ValueTask SignOutAsync()
    {
        _cachedUser = null;
        try
        {
            await js.InvokeVoidAsync("nxtweek.emailAuth.signOut");
            await js.InvokeVoidAsync("localStorage.removeItem", "nxtweek.currentUserEmail");
            await js.InvokeVoidAsync("localStorage.removeItem", "nxtweek.currentUserRawEmail");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LocalAuthService] Error removing from localStorage: {ex.Message}");
        }
    }

    public async ValueTask SendEmailLinkAsync(string email, string username) => await js.InvokeVoidAsync("nxtweek.emailAuth.sendLink", email, username);
    public async ValueTask<bool> HasPendingEmailLinkAsync() => await js.InvokeAsync<bool>("nxtweek.emailAuth.hasPendingLink");
    public async ValueTask<string> GetPendingEmailLinkUsernameAsync() => await js.InvokeAsync<string>("nxtweek.emailAuth.getPendingUsername");
    public async ValueTask<AuthUser?> CompleteStoredEmailLinkAsync() => await js.InvokeAsync<AuthUser?>("nxtweek.emailAuth.completeStoredLink");
    public async ValueTask<AuthUser?> CompleteEmailLinkAsync(string email) => await js.InvokeAsync<AuthUser?>("nxtweek.emailAuth.completeLink", email);

    public async ValueTask SetActiveDataKeyAsync(string dataKey)
    {
        await js.InvokeVoidAsync("localStorage.setItem", "nxtweek.activeUserKey", dataKey);
        await js.InvokeVoidAsync("localStorage.setItem", "nxtweek.guestId", dataKey);
        if (_cachedUser is not null) _cachedUser = new AuthUser { Uid = dataKey, AuthUid = _cachedUser.AuthUid, Email = _cachedUser.Email, DisplayName = _cachedUser.DisplayName, IdToken = _cachedUser.IdToken, IsGuest = _cachedUser.IsGuest };
    }
}
