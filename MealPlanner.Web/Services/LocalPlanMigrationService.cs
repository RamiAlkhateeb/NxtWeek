using MealPlanner.Shared.Services;
using Microsoft.JSInterop;

namespace MealPlanner.Web.Services;

/// <summary>Migrates pre-Firebase guest plans once, without deleting the local copy.</summary>
public sealed class LocalPlanMigrationService(
    IAuthService auth,
    LocalGuestDataService localData,
    FirebaseUserService firebaseData,
    IJSRuntime js)
{
    public async Task MigrateAsync()
    {
        try
        {
            var user = await auth.GetCurrentUserAsync();
            if (user is null || string.IsNullOrWhiteSpace(user.Uid)) return;
            var migrationKey = $"nxtweek.firebasePlanMigration.{user.Uid}";
            if (await js.InvokeAsync<string?>("localStorage.getItem", migrationKey) == "done") return;

            await firebaseData.GetHouseholdIdAsync(user.Uid);
            var localPlans = await localData.GetAllWeeklyPlanAsync(user.Uid);
            if (localPlans.Count > 0)
                await firebaseData.SaveWeeklyPlanEntriesAsync(user.Uid, localPlans);

            await js.InvokeVoidAsync("localStorage.setItem", migrationKey, "done");
        }
        catch
        {
            // Firebase may be offline. Try again on the next launch.
        }
    }
}
