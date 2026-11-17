using MealPlanner.Shared.Models;
using MealPlanner.Shared.Nutrition;

namespace MealPlanner.Tests;

public class SurprisePicksTests
{
    private static MealCatalogItem M(string id, string lang = "en", bool archived = false) => new() { Id = id, Name = id, Language = lang, IsArchived = archived };
    private static readonly HashSet<string> None = [];

    [Fact]
    public void Picks_distinct_meals_in_the_language_avoiding_planned_and_archived()
    {
        var catalog = new[] { M("a"), M("b"), M("c"), M("x", "de"), M("old", archived: true) };
        var picks = SurprisePicks.Pick(catalog, "en", None, new HashSet<string> { "a" }, 5, new Random(1));
        Assert.Equal(new[] { "b", "c" }, picks.Select(p => p.Id).Order());
    }

    [Fact]
    public void Falls_back_to_planned_meals_and_other_languages_rather_than_nothing()
    {
        Assert.Single(SurprisePicks.Pick([M("a")], "en", None, new HashSet<string> { "a" }, 3, new Random(1)));
        Assert.Equal("x", SurprisePicks.Pick([M("x", "de")], "en", None, None, 1, new Random(1)).Single().Id);
        Assert.Empty(SurprisePicks.Pick([], "en", None, None, 3, new Random(1)));
    }

    [Fact]
    public void Favourites_come_up_more_often()
    {
        var catalog = new[] { M("fav"), M("b"), M("c"), M("d") };
        var favs = new HashSet<string> { "fav" };
        var first = Enumerable.Range(0, 400).Count(seed => SurprisePicks.Pick(catalog, "en", favs, None, 1, new Random(seed))[0].Id == "fav");
        Assert.InRange(first, 400 * 3 / 6 - 50, 400 * 3 / 6 + 50); // 3 of 6 weighted entries
    }
}
