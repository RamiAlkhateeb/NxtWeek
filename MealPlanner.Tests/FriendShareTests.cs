using System.Net;
using System.Text;
using MealPlanner.Shared.Services;

namespace MealPlanner.Tests;

public class FriendShareTests
{
    private static readonly Dictionary<string, NutritionInfo> Meals = new() { ["m"] = new() { Kcal = 500, Protein = 40 } };

    [Fact]
    public void Shares_only_eaten_calories_and_protein()
    {
        var day = new PlanDay
        {
            Slots =
            {
                [MealSlot.Meal1] = new SlotEntry { MealId = "m", Eaten = true, Servings = 2 },
                [MealSlot.Dinner] = new SlotEntry { MealId = "m" },
                [MealSlot.Snack] = new SlotEntry { Quick = new QuickAddEntry { Name = "Nuts", Kcal = 150, Protein = 4 }, Eaten = true }
            }
        };

        Assert.Equal((1150, 84), FriendShare.EatenOn(day, Meals.GetValueOrDefault));
        Assert.Null(FriendShare.EatenOn(new PlanDay { Slots = { [MealSlot.Dinner] = new SlotEntry { MealId = "m" } } }, Meals.GetValueOrDefault));
        Assert.Null(FriendShare.EatenOn(null, Meals.GetValueOrDefault));
    }

    [Fact]
    public void Copy_brings_meals_fresh_and_leaves_quick_adds()
    {
        var day = new PlanDay
        {
            Slots =
            {
                [MealSlot.Meal1] = new SlotEntry { MealId = "m", Eaten = true, Servings = 2, PrepBatches = 3, Leftover = true },
                [MealSlot.Snack] = new SlotEntry { Quick = new QuickAddEntry { Name = "Nuts" } }
            }
        };

        var copy = FriendShare.CopyableSlots(day);

        var e = Assert.Single(copy).Value;
        Assert.Equal("m", e.MealId);
        Assert.False(e.Eaten);
        Assert.False(e.Leftover);
        Assert.Equal(1, e.Servings);
        Assert.Equal(1, e.PrepBatches);
    }

    /// <summary>Answers Firebase REST requests from a dictionary and records every request (with its body).</summary>
    private sealed class FakeFirebase(Dictionary<string, string> responses) : HttpMessageHandler
    {
        public List<string> Requests { get; } = new();
        public List<(string Method, string Path, string Body)> Writes { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            if (request.Method == HttpMethod.Get) Requests.Add(path);
            else Writes.Add((request.Method.Method, path, request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
            var body = responses.GetValueOrDefault(path, "null");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    // Firebase's real shallow answer: every child, text and booleans included, comes back as true.
    private static Dictionary<string, string> Sara() => new()
    {
        ["/users/sara.json?shallow=true"] = """{"uid":true,"username":true,"displayName":true,"authUid":true,"firstWeekAutoFilled":true,"shareNutrition":true,"friendIds":true,"days":true,"body":true,"nutritionProfile":true,"weeklyPlan":true,"shopping":true}""",
        ["/users/sara/uid.json"] = "\"sara\"",
        ["/users/sara/username.json"] = "\"sara\"",
        ["/users/sara/displayName.json"] = "\"Sara\"",
        ["/users/sara/authUid.json"] = "\"auth-1\"",
        ["/users/sara/firstWeekAutoFilled.json"] = "false",
        ["/users/sara/shareNutrition.json"] = "true",
        ["/users/sara/friendIds.json"] = """["omar"]"""
    };

    private static FirebaseUserService Service(FakeFirebase fake) =>
        new(new HttpClient(fake), new FirebaseOptions { DatabaseUrl = "https://db.example/" });

    [Fact]
    public async Task Reading_a_profile_gets_its_fields_but_never_plan_or_health_data()
    {
        var fake = new FakeFirebase(Sara());

        var profile = await Service(fake).GetProfileAsync("sara");

        Assert.NotNull(profile);
        Assert.Equal("sara", profile!.Username);
        Assert.Equal("Sara", profile.DisplayName);
        Assert.Equal("auth-1", profile.AuthUid);
        Assert.False(profile.FirstWeekAutoFilled);
        Assert.True(profile.ShareNutrition);
        Assert.Equal(["omar"], profile.FriendIds);
        Assert.DoesNotContain(fake.Requests, r => r.Contains("/days") || r.Contains("/body") || r.Contains("/nutritionProfile") || r.Contains("/weeklyPlan") || r.Contains("/shopping"));
        Assert.DoesNotContain(fake.Requests, r => r == "/users/sara.json");
    }

    [Fact]
    public async Task Recovery_finds_a_named_account()
    {
        var profile = await Service(new FakeFirebase(Sara())).GetNamedProfileAsync("Sara");

        Assert.NotNull(profile);
        Assert.Equal("sara", profile!.Uid);
    }

    [Fact]
    public async Task Missing_profile_is_null()
    {
        Assert.Null(await Service(new FakeFirebase(new())).GetProfileAsync("nobody"));
        Assert.Null(await Service(new FakeFirebase(new())).GetNamedProfileAsync("nobody"));
    }

    [Fact]
    public async Task Saving_a_profile_never_blanks_identity_fields()
    {
        var fake = new FakeFirebase(new());

        await Service(fake).CreateProfileAsync(new UserProfile { Uid = "sara", FirstWeekAutoFilled = true });

        var (_, _, body) = Assert.Single(fake.Writes);
        Assert.DoesNotContain("\"username\"", body);
        Assert.DoesNotContain("\"displayName\"", body);
        Assert.DoesNotContain("\"authUid\"", body);
        Assert.DoesNotContain("\"syncEmail\"", body);
        Assert.Contains("\"firstWeekAutoFilled\":true", body);
    }

    [Fact]
    public async Task An_existing_account_has_data()
    {
        var service = Service(new FakeFirebase(Sara()));

        Assert.True(await service.HasDataAsync("sara"));
        Assert.False(await service.HasDataAsync("nobody"));
    }

    [Fact]
    public async Task Discovering_people_reads_only_username_reservations()
    {
        var fake = new FakeFirebase(new()
        {
            ["/usernames.json"] = """{"sara":{"userKey":"sara"},"omar":{"userKey":"omar"},"me":{"userKey":"me"},"stale":{"userKey":"other"}}"""
        });

        var people = await Service(fake).GetRegisteredUsersAsync("me");

        Assert.Equal(["omar", "sara"], people.Select(p => p.Username));
        Assert.Equal(["/usernames.json"], fake.Requests);
    }
}
