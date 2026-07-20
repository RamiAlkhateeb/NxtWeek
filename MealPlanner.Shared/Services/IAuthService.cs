namespace MealPlanner.Shared.Services;

public interface IAuthService
{
    ValueTask<AuthUser?> GetCurrentUserAsync();
    ValueTask SendSignInLinkAsync(string email, string continueUrl);
    ValueTask<AuthUser?> CompleteSignInAsync(string email);
    ValueTask SignOutAsync();
}

public sealed class AuthUser
{
    public string Uid { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string IdToken { get; init; } = string.Empty;
}
