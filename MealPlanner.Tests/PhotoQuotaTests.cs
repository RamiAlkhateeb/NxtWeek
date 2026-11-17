using MealPlanner.Shared.Services;

namespace MealPlanner.Tests;

public class PhotoQuotaTests
{
    // Must match mondayOf in photo-worker/src/index.js: weeks run Monday to Sunday in UTC.
    [Theory]
    [InlineData("2026-11-22T23:30:00Z", "2026-11-16")]
    [InlineData("2026-11-23T00:10:00Z", "2026-11-23")]
    [InlineData("2026-11-18T12:00:00Z", "2026-11-16")]
    public void Weeks_start_on_monday_in_utc(string utc, string monday) =>
        Assert.Equal(DateOnly.Parse(monday), PhotoQuota.MondayOf(DateTime.Parse(utc, null, System.Globalization.DateTimeStyles.AdjustToUniversal)));

    [Fact]
    public void Left_never_goes_below_zero()
    {
        Assert.Equal(7, new PhotoQuota(3, 10, default).Left);
        Assert.Equal(0, new PhotoQuota(12, 10, default).Left);
    }
}

public class MealTwinsTests
{
    [Theory]
    [InlineData("n12", "en_n12")]
    [InlineData("de_n3", "en_n3")]
    [InlineData("en_n3", null)]
    [InlineData("m5", null)]
    [InlineData("de_m2", null)]
    [InlineData("abc123", null)]
    public void Seeded_dishes_point_to_their_english_meal(string id, string? twin) =>
        Assert.Equal(twin, MealTwins.EnglishTwinId(id));
}
