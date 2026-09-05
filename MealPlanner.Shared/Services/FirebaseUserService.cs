using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using MealPlanner.Shared.Models;

namespace MealPlanner.Shared.Services;

// Each browser-local user owns a single, independent plan under users/{uid}.
//
// IMPORTANT (data safety): a user's node under users/{uid} is a single Firebase
// Realtime Database document that holds the profile fields PLUS sibling subtrees
// (weeklyPlan, shopping, favoriteMealIds, ...). Whole-document PUT writes would
// silently delete those sibling subtrees because the UserProfile model does not
// contain them. For that reason every profile mutation here uses PATCH (shallow
// merge) or a targeted path write, and username rename MOVES the whole subtree
// instead of rebuilding it, so no plan/shopping data is ever dropped.
public sealed class FirebaseUserService(HttpClient http, FirebaseOptions options) : IUserService
{
    private readonly string baseUrl = options.DatabaseUrl.TrimEnd('/');
    private string Url(string path) => $"{baseUrl}/{path}.json";
    private static string Key(string value) => Uri.EscapeDataString(value);

    public async Task<UserProfile?> GetProfileAsync(string uid)
    {
        var profile = await http.GetFromJsonAsync<UserProfile>(Url($"users/{Key(uid)}"));
        if (profile is null) return null;
        profile.Uid = uid; // node key is authoritative
        NormalizeCollections(profile);
        return profile;
    }

    // Merge-only write. Never PUTs the whole document, so sibling weeklyPlan /
    // shopping / favorite nodes already stored under the same uid survive.
    public async Task CreateProfileAsync(UserProfile profile)
    {
        profile.Uid = profile.Uid.Trim();
        if (string.IsNullOrWhiteSpace(profile.Uid))
            throw new InvalidOperationException("هوية المستخدم غير صالحة.");

        var patch = new Dictionary<string, object?>
        {
            ["uid"] = profile.Uid,
            ["username"] = profile.Username,
            ["authUid"] = profile.AuthUid,
            ["syncEmail"] = profile.SyncEmail,
            ["displayName"] = profile.DisplayName,
            ["firstWeekAutoFilled"] = profile.FirstWeekAutoFilled,
            ["selectedMealIds"] = profile.SelectedMealIds ?? new(),
            ["favoriteMealIds"] = profile.FavoriteMealIds ?? new(),
            ["friendIds"] = profile.FriendIds ?? new(),
            ["outgoingFriendRequestIds"] = profile.OutgoingFriendRequestIds ?? new()
        };
        if (profile.IncomingFriendRequests is { Count: > 0 })
            patch["incomingFriendRequests"] = profile.IncomingFriendRequests;

        using var response = await http.PatchAsJsonAsync(Url($"users/{Key(profile.Uid)}"), patch);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("تعذر حفظ الملف الشخصي. حاول مرة أخرى.");
    }

    public async Task SaveSelectedMealsAsync(string uid, List<string> mealIds)
    {
        using var response = await http.PutAsJsonAsync(Url($"users/{Key(uid)}/selectedMealIds"), mealIds);
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveFavoriteMealsAsync(string uid, List<string> mealIds)
    {
        using var response = await http.PutAsJsonAsync(Url($"users/{Key(uid)}/favoriteMealIds"), mealIds);
        response.EnsureSuccessStatusCode();
    }

    public async Task ToggleFavoriteMealAsync(string uid, string mealId)
    {
        var values = await http.GetFromJsonAsync<List<string>>(Url($"users/{Key(uid)}/favoriteMealIds")) ?? [];
        if (!values.Remove(mealId)) values.Add(mealId);
        await SaveFavoriteMealsAsync(uid, values);
    }

    public async Task<List<WeeklyPlanEntry>> GetWeeklyPlanAsync(string uid, DateOnly start, DateOnly end)
    {
        var plan = await http.GetFromJsonAsync<Dictionary<string, WeeklyPlanEntry>>(Url($"users/{Key(uid)}/weeklyPlan")) ?? [];
        return plan.Values
            .Where(x => x.Date >= start && x.Date <= end)
            .OrderBy(x => x.Date)
            .ToList();
    }

    public Task<List<WeeklyPlanEntry>> GetMonthPlanAsync(string uid, int year, int month) =>
        GetWeeklyPlanAsync(uid, new DateOnly(year, month, 1), new DateOnly(year, month, 1).AddMonths(1).AddDays(-1));

    public async Task<bool> HasAnyPlanEntryAsync(string uid)
    {
        var plan = await http.GetFromJsonAsync<Dictionary<string, WeeklyPlanEntry>>(Url($"users/{Key(uid)}/weeklyPlan"));
        return plan is { Count: > 0 };
    }

    public async Task SaveWeeklyPlanEntryAsync(string uid, WeeklyPlanEntry entry)
    {
        using var response = await http.PutAsJsonAsync(Url($"users/{Key(uid)}/weeklyPlan/{entry.Date:yyyy-MM-dd}"), entry);
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveWeeklyPlanEntriesAsync(string uid, List<WeeklyPlanEntry> entries)
    {
        foreach (var entry in entries) await SaveWeeklyPlanEntryAsync(uid, entry);
    }

    public Task AssignMealToDateAsync(string uid, DateOnly date, string mealId) =>
        SaveWeeklyPlanEntryAsync(uid, new() { Date = date, MealId = mealId });

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

        // Reserve the requested name (optimistic concurrency via ETag).
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

        // Read the raw node so every child subtree (weeklyPlan, shopping, ...) is
        // carried over to the new key instead of being dropped.
        JsonNode? rawNode = null;
        using (var rawGet = await http.GetAsync(Url($"users/{Key(currentUid)}")))
        {
            if (rawGet.IsSuccessStatusCode)
            {
                var rawJson = await rawGet.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(rawJson) && rawJson != "null")
                    rawNode = JsonNode.Parse(rawJson);
            }
        }

        // Update friend references across other users (explicit path writes only),
        // then move the user's own whole node. Each change targets just one field
        // of a friend's profile so their weeklyPlan / shopping are never touched.
        var allProfiles = await http.GetFromJsonAsync<Dictionary<string, UserProfile>>(Url("users")) ?? new();

        var friendPatches = new Dictionary<string, object?>();
        foreach (var pair in allProfiles.Where(p => !string.Equals(p.Key, currentUid, StringComparison.OrdinalIgnoreCase)))
        {
            var profile = pair.Value;
            profile.FriendIds ??= new();
            if (profile.FriendIds.Any(id => string.Equals(id, currentUid, StringComparison.OrdinalIgnoreCase)))
                friendPatches[$"users/{Key(pair.Key)}/friendIds"] = profile.FriendIds
                    .Select(id => string.Equals(id, currentUid, StringComparison.OrdinalIgnoreCase) ? username : id)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            profile.OutgoingFriendRequestIds ??= new();
            if (profile.OutgoingFriendRequestIds.Any(id => string.Equals(id, currentUid, StringComparison.OrdinalIgnoreCase)))
                friendPatches[$"users/{Key(pair.Key)}/outgoingFriendRequestIds"] = profile.OutgoingFriendRequestIds
                    .Select(id => string.Equals(id, currentUid, StringComparison.OrdinalIgnoreCase) ? username : id)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            profile.IncomingFriendRequests ??= new();
            if (profile.IncomingFriendRequests.ContainsKey(currentUid))
            {
                var request = profile.IncomingFriendRequests[currentUid];
                request.SenderUid = username;
                var rebuilt = new Dictionary<string, FriendRequest>(profile.IncomingFriendRequests);
                rebuilt.Remove(currentUid);
                rebuilt[username] = request;
                friendPatches[$"users/{Key(pair.Key)}/incomingFriendRequests"] = rebuilt;
            }
        }

        if (friendPatches.Count > 0)
        {
            using (var friendResp = await http.PatchAsJsonAsync(Url(""), friendPatches))
            {
                if (!friendResp.IsSuccessStatusCode)
                    throw new InvalidOperationException("تعذر حفظ اسم المستخدم. حاول مرة أخرى.");
            }
        }

        // 2) Move the entire user node to the new key and remove the old one.
        // Apply the identity fields directly on the raw copy so weeklyPlan /
        // shopping / favorites move untouched, and no conflicting sub-paths are sent.
        JsonObject moved = rawNode as JsonObject ?? new JsonObject();
        moved["uid"] = username;
        moved["username"] = username;
        moved["authUid"] = current.AuthUid;
        moved["syncEmail"] = current.SyncEmail;
        // Display name mirrors the username (a user no longer shows as "ضيف" guest).
        moved["displayName"] = username;

        var movePatch = new Dictionary<string, object?>
        {
            [$"users/{Key(username)}"] = moved,
            [$"users/{Key(currentUid)}"] = null,
            [$"usernames/{Key(username)}"] = new UsernameReservation { UserKey = username, AuthUid = current.AuthUid }
        };
        if (!string.IsNullOrWhiteSpace(current.AuthUid)) movePatch[$"authUsers/{Key(current.AuthUid)}"] = username;
        if (!string.Equals(currentUid, username, StringComparison.OrdinalIgnoreCase) && !currentUid.StartsWith("guest_", StringComparison.OrdinalIgnoreCase))
            movePatch[$"usernames/{Key(currentUid)}"] = null;

        using (var update = await http.PatchAsJsonAsync(Url(""), movePatch))
        {
            if (!update.IsSuccessStatusCode)
                throw new InvalidOperationException("تعذر حفظ اسم المستخدم. حاول مرة أخرى.");
        }

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
        profile.DisplayName = userKey; // mirror username instead of showing "ضيف"
        await CreateProfileAsync(profile);
        await http.PutAsJsonAsync(Url($"authUsers/{Key(authUid)}"), userKey);
        await http.PutAsJsonAsync(Url($"usernames/{Key(userKey)}"), new UsernameReservation { UserKey = userKey, AuthUid = authUid });
    }

    // Discovery: list registered (named) users so the app can show "other people
    // using the app" with an Add (friend request) button. A user is surfaced only
    // when they have claimed a username (profile.Username is set and the node key
    // is that username), never random guest keys or legacy email-keyed records.
    public async Task<List<UserProfile>> GetRegisteredUsersAsync(string excludeUid)
    {
        var all = await http.GetFromJsonAsync<Dictionary<string, UserProfile>>(Url("users")) ?? new();
        var result = new List<UserProfile>();
        foreach (var pair in all)
        {
            var key = pair.Key;
            if (string.Equals(key, excludeUid, StringComparison.OrdinalIgnoreCase)) continue;
            if (key.StartsWith("guest_", StringComparison.OrdinalIgnoreCase)) continue;
            var profile = pair.Value;
            if (profile is null) continue;
            if (string.IsNullOrWhiteSpace(profile.Username)) continue;
            if (!string.Equals(key, profile.Username, StringComparison.OrdinalIgnoreCase)) continue;
            profile.Uid = key;
            NormalizeCollections(profile);
            result.Add(profile);
        }
        return result.OrderBy(p => p.Username, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // Recovery for the "cleared browser storage / new device" case: given a
    // username, return the profile ONLY if a real named account exists under that
    // key (never a guest id or a stray node whose Username doesn't match the key).
    public async Task<UserProfile?> GetNamedProfileAsync(string username)
    {
        var normalized = NormalizeUsernameForLookup(username);
        if (normalized is null) return null;
        var profile = await http.GetFromJsonAsync<UserProfile>(Url($"users/{Key(normalized)}"));
        if (profile is null) return null;
        // Only named accounts are recoverable by username.
        if (string.IsNullOrWhiteSpace(profile.Username) ||
            !string.Equals(normalized, profile.Username, StringComparison.OrdinalIgnoreCase))
            return null;
        profile.Uid = normalized;
        NormalizeCollections(profile);
        return profile;
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

        var patch = new Dictionary<string, object?>
        {
            [$"users/{Key(recipientUid)}/incomingFriendRequests/{Key(senderUid)}"] = recipient.IncomingFriendRequests[senderUid],
            [$"users/{Key(senderUid)}/outgoingFriendRequestIds"] = sender.OutgoingFriendRequestIds
        };
        using var response = await http.PatchAsJsonAsync(Url(""), patch);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("تعذر إرسال طلب الصداقة. حاول مرة أخرى.");
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

        var patch = new Dictionary<string, object?>
        {
            [$"users/{Key(uid)}/friendIds"] = profile.FriendIds,
            [$"users/{Key(uid)}/incomingFriendRequests"] = profile.IncomingFriendRequests,
            [$"users/{Key(senderUid)}/friendIds"] = sender.FriendIds,
            [$"users/{Key(senderUid)}/outgoingFriendRequestIds"] = sender.OutgoingFriendRequestIds ?? new()
        };
        using var response = await http.PatchAsJsonAsync(Url(""), patch);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("تعذر قبول طلب الصداقة. حاول مرة أخرى.");
    }

    public async Task DeclineFriendRequestAsync(string uid, string senderUid)
    {
        var profile = await GetProfileAsync(uid)
            ?? throw new InvalidOperationException("تعذر تحميل حسابك.");
        profile.IncomingFriendRequests?.Remove(senderUid);

        var patch = new Dictionary<string, object?>
        {
            [$"users/{Key(uid)}/incomingFriendRequests"] = profile.IncomingFriendRequests ?? new Dictionary<string, FriendRequest>()
        };
        using (var response = await http.PatchAsJsonAsync(Url(""), patch))
        {
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("تعذر رفض طلب الصداقة. حاول مرة أخرى.");
        }

        var sender = await GetProfileAsync(senderUid);
        if (sender is not null && sender.OutgoingFriendRequestIds?.Remove(uid) == true)
        {
            using var resp = await http.PatchAsJsonAsync(Url(""), new Dictionary<string, object?>
            {
                [$"users/{Key(senderUid)}/outgoingFriendRequestIds"] = sender.OutgoingFriendRequestIds
            });
            if (!resp.IsSuccessStatusCode) throw new InvalidOperationException("تعذر رفض طلب الصداقة. حاول مرة أخرى.");
        }
    }

    private static bool HasUsername(UserProfile profile) => !string.IsNullOrWhiteSpace(profile.Username) && !profile.Uid.StartsWith("guest_", StringComparison.OrdinalIgnoreCase);
    // Lenient normalization for lookups: returns null (not found) rather than
    // throwing when the input can't possibly be a valid username.
    private static string? NormalizeUsernameForLookup(string value)
    {
        var username = value.Trim().ToLowerInvariant();
        if (username.Length is < 3 or > 20 || !username.All(character => char.IsAsciiLetterOrDigit(character) || character == '_'))
            return null;
        return username;
    }
    private static string NormalizeUsername(string value)
    {
        var username = value.Trim().ToLowerInvariant();
        if (username.Length is < 3 or > 20 || !username.All(character => char.IsAsciiLetterOrDigit(character) || character == '_'))
            throw new InvalidOperationException("اسم المستخدم يجب أن يحتوي 3 إلى 20 حرفاً إنجليزياً أو رقماً أو _.");
        return username;
    }

    private static void NormalizeCollections(UserProfile profile)
    {
        profile.SelectedMealIds ??= new();
        profile.FavoriteMealIds ??= new();
        profile.FriendIds ??= new();
        profile.OutgoingFriendRequestIds ??= new();
        profile.IncomingFriendRequests ??= new();
        // displayName mirrors the username for any account that has claimed one
        // (fixes legacy rows that still show "ضيف" / guest for a named user).
        if (!string.IsNullOrWhiteSpace(profile.Username) &&
            !string.Equals(profile.DisplayName, profile.Username, StringComparison.OrdinalIgnoreCase))
            profile.DisplayName = profile.Username;
    }
}
