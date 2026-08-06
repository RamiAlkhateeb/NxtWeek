namespace MealPlanner.Shared.Services;

public sealed record HouseholdSettings(string DisplayName, string HouseholdId);
public interface IHouseholdService
{
    Task<HouseholdSettings> GetCurrentAsync();
    Task<bool> JoinAsync(string householdId);
}
