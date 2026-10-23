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

    /// <summary>Answers Firebase REST requests from a dictionary and records every URL asked for.</summary>
    private sealed class FakeFirebase(Dictionary<string, string> responses) : HttpMessageHandler
    {
        public List<string> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            Requests.Add(path);
            var body = responses.GetValueOrDefault(path, "null");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public async Task Reading_a_profile_never_downloads_plan_or_health_data()
    {
        var fake = new FakeFirebase(new()
        {
            ["/users/sara.json?shallow=true"] = """{"uid":"sara","username":"sara","displayName":"sara","firstWeekAutoFilled":true,"shareNutrition":true,"friendIds":true,"days":true,"body":true,"nutritionProfile":true,"weeklyPlan":true,"shopping":true}""",
            ["/users/sara/friendIds.json"] = """["omar"]"""
        });
        var service = new FirebaseUserService(new HttpClient(fake), new FirebaseOptions { DatabaseUrl = "https://db.example/" });

        var profile = await service.GetProfileAsync("sara");

        Assert.NotNull(profile);
        Assert.Equal("sara", profile!.Username);
        Assert.True(profile.FirstWeekAutoFilled);
        Assert.True(profile.ShareNutrition);
        Assert.Equal(["omar"], profile.FriendIds);
        Assert.Equal(["/users/sara.json?shallow=true", "/users/sara/friendIds.json"], fake.Requests);
    }

    [Fact]
    public async Task Missing_profile_is_null()
    {
        var service = new FirebaseUserService(new HttpClient(new FakeFirebase(new())), new FirebaseOptions { DatabaseUrl = "https://db.example/" });

        Assert.Null(await service.GetProfileAsync("nobody"));
    }

    [Fact]
    public async Task Discovering_people_reads_only_username_reservations()
    {
        var fake = new FakeFirebase(new()
        {
            ["/usernames.json"] = """{"sara":{"userKey":"sara"},"omar":{"userKey":"omar"},"me":{"userKey":"me"},"stale":{"userKey":"other"}}"""
        });
        var service = new FirebaseUserService(new HttpClient(fake), new FirebaseOptions { DatabaseUrl = "https://db.example/" });

        var people = await service.GetRegisteredUsersAsync("me");

        Assert.Equal(["omar", "sara"], people.Select(p => p.Username));
        Assert.Equal(["/usernames.json"], fake.Requests);
    }
}
