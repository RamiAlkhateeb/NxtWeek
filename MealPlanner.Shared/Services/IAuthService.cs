using System.Threading.Tasks;

namespace MealPlanner.Shared.Services;

public interface IAuthService
{
    ValueTask<AuthUser?> GetCurrentUserAsync();
    ValueTask<AuthUser> SignInAsync(string email);
    ValueTask SendEmailLinkAsync(string email, string username);
    ValueTask<bool> HasPendingEmailLinkAsync();
    ValueTask<string> GetPendingEmailLinkUsernameAsync();
    ValueTask<AuthUser?> CompleteStoredEmailLinkAsync();
    ValueTask<AuthUser?> CompleteEmailLinkAsync(string email);
    ValueTask SetActiveDataKeyAsync(string dataKey);
    ValueTask SignOutAsync();
}

public sealed class AuthUser
{
    public string Uid { get; init; } = string.Empty;
    public string AuthUid { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string IdToken { get; init; } = string.Empty;
    public bool IsGuest { get; init; }
}
