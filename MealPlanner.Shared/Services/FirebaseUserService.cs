using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
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

    public async Task<bool> IsUsernameAvailableAsync(string username, string currentUid)
    {
        var normalized = NormalizeUsername(username);
        var reservation = await http.GetFromJsonAsync<UsernameReservation>(Url($"usernames/{Key(normalized)}"));
        return reservation is null || string.Equals(reservation.UserKey, currentUid, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string> RenameUserAsync(string currentUid, string requestedUsername)
    {
        var username = NormalizeUsername(requestedUsername);
        var current = await GetProfileAsync(currentUid) ?? new UserProfile { Uid = currentUid, DisplayName = "ضيف" };
        if (string.Equals(currentUid, username, StringComparison.OrdinalIgnoreCase)) return username;

        var reservationUrl = Url($"usernames/{Key(username)}");
        using var get = new HttpRequestMessage(HttpMethod.Get, reservationUrl);
        get.Headers.TryAddWithoutValidation("X-Firebase-ETag", "true");
        using var existing = await http.SendAsync(get);
        existing.EnsureSuccessStatusCode();
        var etag = existing.Headers.ETag?.Tag ?? "null_etag";
        var existingBody = await existing.Content.ReadAsStringAsync();
        var reservation = string.IsNullOrWhiteSpace(existingBody) || existingBody == "null"
            ? null : JsonSerializer.Deserialize<UsernameReservation>(existingBody, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (reservation is not null && !string.Equals(reservation.UserKey, currentUid, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("اسم المستخدم مستخدم بالفعل.");

        if (reservation is null)
        {
            using var reserve = new HttpRequestMessage(HttpMethod.Put, reservationUrl)
            {
                Content = JsonContent.Create(new UsernameReservation { UserKey = username, AuthUid = current.AuthUid })
            };
            reserve.Headers.TryAddWithoutValidation("if-match", etag);
            using var reserved = await http.SendAsync(reserve);
            if (!reserved.IsSuccessStatusCode)
                throw new InvalidOperationException("تم اختيار اسم المستخدم من مستخدم آخر. جرب اسماً آخر.");
        }

        var allProfiles = await http.GetFromJsonAsync<Dictionary<string, UserProfile>>(Url("users")) ?? new();
        current.Uid = username;
        current.Username = username;
        var changes = new Dictionary<string, object?>
        {
            [$"users/{username}"] = current,
            [$"users/{currentUid}"] = null,
            [$"usernames/{username}"] = new UsernameReservation { UserKey = username, AuthUid = current.AuthUid }
        };
        if (!string.IsNullOrWhiteSpace(current.AuthUid)) changes[$"authUsers/{current.AuthUid}"] = username;

        foreach (var pair in allProfiles.Where(pair => !string.Equals(pair.Key, currentUid, StringComparison.OrdinalIgnoreCase)))
        {
            var profile = pair.Value;
            var changed = false;
            profile.FriendIds ??= new();
            for (var index = 0; index < profile.FriendIds.Count; index++)
                if (string.Equals(profile.FriendIds[index], currentUid, StringComparison.OrdinalIgnoreCase)) { profile.FriendIds[index] = username; changed = true; }
            profile.OutgoingFriendRequestIds ??= new();
            for (var index = 0; index < profile.OutgoingFriendRequestIds.Count; index++)
                if (string.Equals(profile.OutgoingFriendRequestIds[index], currentUid, StringComparison.OrdinalIgnoreCase)) { profile.OutgoingFriendRequestIds[index] = username; changed = true; }
            profile.IncomingFriendRequests ??= new();
            if (profile.IncomingFriendRequests.Remove(currentUid, out var request))
            {
                request.SenderUid = username;
                profile.IncomingFriendRequests[username] = request;
                changed = true;
            }
            if (changed) changes[$"users/{pair.Key}"] = profile;
        }
        if (!string.Equals(currentUid, username, StringComparison.OrdinalIgnoreCase)) changes[$"usernames/{currentUid}"] = null;

        using var update = await http.PatchAsJsonAsync(Url(""), changes);
        if (!update.IsSuccessStatusCode)
            throw new InvalidOperationException("تعذر حفظ اسم المستخدم. حاول مرة أخرى.");
        return username;
    }

    public async Task<string?> GetDataKeyForAuthUidAsync(string authUid) =>
        await http.GetFromJsonAsync<string>(Url($"authUsers/{Key(authUid)}"));

    public async Task LinkAuthAccountAsync(string userKey, string authUid, string email)
    {
        var profile = await GetProfileAsync(userKey) ?? throw new InvalidOperationException("تعذر العثور على اسم المستخدم المرتبط بهذا الرابط.");
        profile.AuthUid = authUid;
        profile.SyncEmail = email;
        profile.Username = userKey;
        await CreateProfileAsync(profile);
        await http.PutAsJsonAsync(Url($"authUsers/{Key(authUid)}"), userKey);
        await http.PutAsJsonAsync(Url($"usernames/{Key(userKey)}"), new UsernameReservation { UserKey = userKey, AuthUid = authUid });
    }

    public async Task SendFriendRequestAsync(string senderUid, string recipientUid)
    {
        senderUid = senderUid.Trim();
        recipientUid = recipientUid.Trim();
        if (string.Equals(senderUid, recipientUid, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("لا يمكنك إضافة نفسك.");

        var sender = await GetProfileAsync(senderUid) ?? throw new InvalidOperationException("اختر اسم مستخدم أولاً من الإعدادات.");
        if (!HasUsername(sender)) throw new InvalidOperationException("اختر اسم مستخدم أولاً من الإعدادات.");
        var recipient = await GetProfileAsync(recipientUid)
            ?? throw new InvalidOperationException("لم نعثر على مستخدم بهذا الاسم.");
        if (!HasUsername(recipient)) throw new InvalidOperationException("هذا المستخدم لم يجهز اسم مستخدم بعد.");
        if (recipient.FriendIds?.Contains(senderUid, StringComparer.OrdinalIgnoreCase) == true)
            throw new InvalidOperationException("أنتم أصدقاء بالفعل.");

        recipient.IncomingFriendRequests ??= new();
        if (recipient.IncomingFriendRequests.ContainsKey(senderUid))
            throw new InvalidOperationException("تم إرسال طلب صداقة بالفعل.");

        recipient.IncomingFriendRequests[senderUid] = new FriendRequest
        {
            SenderUid = senderUid,
            SenderDisplayName = sender.DisplayName ?? senderUid,
            SentAtUtc = DateTime.UtcNow
        };
        sender.OutgoingFriendRequestIds ??= new();
        if (!sender.OutgoingFriendRequestIds.Contains(recipientUid, StringComparer.OrdinalIgnoreCase)) sender.OutgoingFriendRequestIds.Add(recipientUid);
        await CreateProfileAsync(recipient);
        await CreateProfileAsync(sender);
    }

    public async Task AcceptFriendRequestAsync(string uid, string senderUid)
    {
        var profile = await GetProfileAsync(uid)
            ?? throw new InvalidOperationException("تعذر تحميل حسابك.");
        if (!HasUsername(profile)) throw new InvalidOperationException("اختر اسم مستخدم أولاً من الإعدادات.");
        profile.IncomingFriendRequests ??= new();
        if (!profile.IncomingFriendRequests.Remove(senderUid))
            throw new InvalidOperationException("لم يعد طلب الصداقة متاحاً.");

        var sender = await GetProfileAsync(senderUid)
            ?? throw new InvalidOperationException("تعذر العثور على هذا المستخدم.");
        profile.FriendIds ??= new();
        sender.FriendIds ??= new();
        if (!profile.FriendIds.Contains(senderUid, StringComparer.OrdinalIgnoreCase)) profile.FriendIds.Add(senderUid);
        if (!sender.FriendIds.Contains(uid, StringComparer.OrdinalIgnoreCase)) sender.FriendIds.Add(uid);
        sender.OutgoingFriendRequestIds?.Remove(uid);

        await CreateProfileAsync(profile);
        await CreateProfileAsync(sender);
    }

    public async Task DeclineFriendRequestAsync(string uid, string senderUid)
    {
        var profile = await GetProfileAsync(uid)
            ?? throw new InvalidOperationException("تعذر تحميل حسابك.");
        profile.IncomingFriendRequests?.Remove(senderUid);
        await CreateProfileAsync(profile);
        var sender = await GetProfileAsync(senderUid);
        if (sender is not null) { sender.OutgoingFriendRequestIds?.Remove(uid); await CreateProfileAsync(sender); }
    }

    private static bool HasUsername(UserProfile profile) => !string.IsNullOrWhiteSpace(profile.Username) && !profile.Uid.StartsWith("guest_", StringComparison.OrdinalIgnoreCase);
    private static string NormalizeUsername(string value)
    {
        var username = value.Trim().ToLowerInvariant();
        if (username.Length is < 3 or > 20 || !username.All(character => char.IsAsciiLetterOrDigit(character) || character == '_'))
            throw new InvalidOperationException("اسم المستخدم يجب أن يحتوي 3 إلى 20 حرفاً إنجليزياً أو رقماً أو _.");
        return username;
    }
}
