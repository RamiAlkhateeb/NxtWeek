using System.Net.Http.Json;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

// Each browser-local user owns a single, independent plan under users/{uid}.
public sealed class FirebaseUserService(HttpClient http, FirebaseOptions options) : IUserService
{
    private readonly string baseUrl = options.DatabaseUrl.TrimEnd('/');
    private string Url(string path) => $"{baseUrl}/{path}.json";
    private static string Key(string value) => Uri.EscapeDataString(value);
    public Task<UserProfile?> GetProfileAsync(string uid) => http.GetFromJsonAsync<UserProfile>(Url($"users/{Key(uid)}"));
    public Task CreateProfileAsync(UserProfile profile) => http.PutAsJsonAsync(Url($"users/{Key(profile.Uid)}"), profile);
    public Task SaveSelectedMealsAsync(string uid, List<string> mealIds) => http.PutAsJsonAsync(Url($"users/{Key(uid)}/selectedMealIds"), mealIds);
    public Task SaveFavoriteMealsAsync(string uid, List<string> mealIds) => http.PutAsJsonAsync(Url($"users/{Key(uid)}/favoriteMealIds"), mealIds);
    public async Task ToggleFavoriteMealAsync(string uid, string mealId)
    { var values = await http.GetFromJsonAsync<List<string>>(Url($"users/{Key(uid)}/favoriteMealIds")) ?? []; if (!values.Remove(mealId)) values.Add(mealId); await SaveFavoriteMealsAsync(uid, values); }
    public async Task<List<WeeklyPlanEntry>> GetWeeklyPlanAsync(string uid, DateOnly start, DateOnly end)
    { var plan = await http.GetFromJsonAsync<Dictionary<string, WeeklyPlanEntry>>(Url($"users/{Key(uid)}/weeklyPlan")) ?? []; return plan.Values.Where(x => x.Date >= start && x.Date <= end).OrderBy(x => x.Date).ToList(); }
    public Task<List<WeeklyPlanEntry>> GetMonthPlanAsync(string uid, int year, int month) => GetWeeklyPlanAsync(uid, new DateOnly(year, month, 1), new DateOnly(year, month, 1).AddMonths(1).AddDays(-1));
    public Task SaveWeeklyPlanEntryAsync(string uid, WeeklyPlanEntry entry) => http.PutAsJsonAsync(Url($"users/{Key(uid)}/weeklyPlan/{entry.Date:yyyy-MM-dd}"), entry);
    public async Task SaveWeeklyPlanEntriesAsync(string uid, List<WeeklyPlanEntry> entries) { foreach (var entry in entries) await SaveWeeklyPlanEntryAsync(uid, entry); }
    public Task AssignMealToDateAsync(string uid, DateOnly date, string mealId) => SaveWeeklyPlanEntryAsync(uid, new() { Date = date, MealId = mealId });

    public async Task SendFriendRequestAsync(string senderUid, string recipientUid)
    {
        senderUid = senderUid.Trim();
        recipientUid = recipientUid.Trim();
        if (string.Equals(senderUid, recipientUid, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("لا يمكنك إضافة نفسك.");

        var recipient = await GetProfileAsync(recipientUid)
            ?? throw new InvalidOperationException("لم نعثر على مستخدم بهذا الاسم.");
        if (recipient.FriendIds?.Contains(senderUid, StringComparer.OrdinalIgnoreCase) == true)
            throw new InvalidOperationException("أنتم أصدقاء بالفعل.");

        recipient.IncomingFriendRequests ??= new();
        if (recipient.IncomingFriendRequests.ContainsKey(senderUid))
            throw new InvalidOperationException("تم إرسال طلب صداقة بالفعل.");

        var sender = await GetProfileAsync(senderUid);
        recipient.IncomingFriendRequests[senderUid] = new FriendRequest
        {
            SenderUid = senderUid,
            SenderDisplayName = sender?.DisplayName ?? senderUid,
            SentAtUtc = DateTime.UtcNow
        };
        await CreateProfileAsync(recipient);
    }

    public async Task AcceptFriendRequestAsync(string uid, string senderUid)
    {
        var profile = await GetProfileAsync(uid)
            ?? throw new InvalidOperationException("تعذر تحميل حسابك.");
        profile.IncomingFriendRequests ??= new();
        if (!profile.IncomingFriendRequests.Remove(senderUid))
            throw new InvalidOperationException("لم يعد طلب الصداقة متاحاً.");

        var sender = await GetProfileAsync(senderUid)
            ?? throw new InvalidOperationException("تعذر العثور على هذا المستخدم.");
        profile.FriendIds ??= new();
        sender.FriendIds ??= new();
        if (!profile.FriendIds.Contains(senderUid, StringComparer.OrdinalIgnoreCase)) profile.FriendIds.Add(senderUid);
        if (!sender.FriendIds.Contains(uid, StringComparer.OrdinalIgnoreCase)) sender.FriendIds.Add(uid);

        await CreateProfileAsync(profile);
        await CreateProfileAsync(sender);
    }

    public async Task DeclineFriendRequestAsync(string uid, string senderUid)
    {
        var profile = await GetProfileAsync(uid)
            ?? throw new InvalidOperationException("تعذر تحميل حسابك.");
        profile.IncomingFriendRequests?.Remove(senderUid);
        await CreateProfileAsync(profile);
    }
}
