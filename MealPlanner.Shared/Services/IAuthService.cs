using System.Threading.Tasks;

namespace MealPlanner.Shared.Services;

public interface IAuthService
{
    ValueTask<AuthUser?> GetCurrentUserAsync();
    ValueTask<AuthUser> SignInAsync(string email);
    ValueTask SignOutAsync();
}

public sealed class AuthUser
{
    public string Uid { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string IdToken { get; init; } = string.Empty;
}
