using System.Text.Json.Nodes;
using MealPlanner.Shared.Offline;
using Xunit;

public class OfflineMergeTests
{
    private static PendingWrite W(string method, string path, string? body = null) => new() { Method = method, Path = path, Body = body };
    private static string? Run(string? cached, string path, string query, params PendingWrite[] writes) =>
        OfflineMerge.Apply(cached is null ? null : JsonNode.Parse(cached), path, query, writes)?.ToJsonString();

    [Theory]
    [InlineData("users/a/days/2026-11-17", true)]
    [InlineData("users/a/shopping", true)]
    [InlineData("MealCatalog", true)]
    [InlineData("users/a/body/2026-11-17", false)]
    [InlineData("users/a/nutritionProfile", false)]
    [InlineData("users/a/progressPhotos/2026-11-16", false)]
    [InlineData("users/a/mealPhotos", false)]
    [InlineData("MealPhotos/m1", false)]
    [InlineData("PhotoQuota/a", false)]
    [InlineData("users", false)]
    public void Keeps_only_non_private_paths(string path, bool expected) => Assert.Equal(expected, OfflineMerge.MayKeep(path));

    [Fact]
    public void Path_is_unescaped_and_only_for_the_database()
    {
        var root = new Uri("https://db.example.app/");
        Assert.Equal("users/a b/days", OfflineMerge.PathOf(new Uri("https://db.example.app/users/a%20b/days.json?shallow=true"), root));
        Assert.Null(OfflineMerge.PathOf(new Uri("https://other.example/users.json"), root));
        Assert.Null(OfflineMerge.PathOf(new Uri("https://db.example.app/index.html"), root));
    }

    [Fact]
    public void Put_below_the_read_replaces_that_child()
    {
        var result = Run("""{"d1":{"x":1},"d2":{"x":2}}""", "users/a/days", "", W("PUT", "users/a/days/d2", """{"x":3}"""));
        Assert.Equal("""{"d1":{"x":1},"d2":{"x":3}}""", result);
    }

    [Fact]
    public void Writes_apply_in_order_and_delete_removes()
    {
        var result = Run("""{"d1":{"x":1}}""", "users/a/days", "",
            W("PUT", "users/a/days/d2", """{"x":2}"""), W("DELETE", "users/a/days/d1"));
        Assert.Equal("""{"d2":{"x":2}}""", result);
    }

    [Fact]
    public void Patch_merges_keys_including_paths()
    {
        var result = Run("""{"name":"A","items":{"i1":{"q":1}}}""", "users/a/shopping/s1", "",
            W("PATCH", "users/a/shopping/s1", """{"name":"B","items/i2":{"q":2}}"""));
        Assert.Equal("""{"name":"B","items":{"i1":{"q":1},"i2":{"q":2}}}""", result);
    }

    [Fact]
    public void Write_on_an_ancestor_gives_the_part_read()
    {
        Assert.Equal("""{"x":5}""", Run("""{"x":1}""", "users/a/days/d1", "", W("PUT", "users/a/days", """{"d1":{"x":5}}""")));
        Assert.Null(Run("""{"x":1}""", "users/a/days/d1", "", W("DELETE", "users/a")));
        Assert.Equal("7", Run("""{"x":1}""", "users/a/days/d1/x", "", W("PATCH", "users/a", """{"days/d1/x":7}""")));
    }

    [Fact]
    public void Unrelated_writes_leave_the_read_alone()
    {
        Assert.Equal("""{"x":1}""", Run("""{"x":1}""", "users/a/days", "", W("PUT", "users/b/days/d1", "{}")));
    }

    [Fact]
    public void Key_range_and_shallow_queries_are_honoured()
    {
        var range = "?orderBy=%22%24key%22&startAt=%222026-11-16%22&endAt=%222026-11-22%22";
        var result = Run("""{"2026-11-16":{"x":1}}""", "users/a/days", range,
            W("PUT", "users/a/days/2026-11-30", """{"x":9}"""), W("PUT", "users/a/days/2026-11-17", """{"x":2}"""));
        Assert.Equal("""{"2026-11-16":{"x":1},"2026-11-17":{"x":2}}""", result);

        Assert.Equal("""{"p":true,"q":true}""", Run("""{"p":true}""", "users/a", "?shallow=true", W("PUT", "users/a/q", "\"v\"")));
    }

    [Fact]
    public void Nothing_cached_yet_still_shows_the_waiting_change()
    {
        Assert.Equal("""{"x":1}""", Run(null, "users/a/days/d1", "", W("PUT", "users/a/days/d1", """{"x":1}""")));
    }
}

public class OfflineAncestorTests
{
    private const string Week = "?orderBy=%22%24key%22&startAt=%222026-11-16%22&endAt=%222026-11-22%22";

    private static (bool, string?) Run(string path, string query, string cachedPath, string cachedQuery, string cached)
    {
        var ok = OfflineMerge.TryFromCached(path, query, cachedPath, cachedQuery, JsonNode.Parse(cached), out var value);
        return (ok, value?.ToJsonString());
    }

    [Fact]
    public void A_day_is_read_from_the_week()
    {
        Assert.Equal((true, """{"x":1}"""), Run("users/a/days/2026-11-17", "", "users/a/days", Week, """{"2026-11-17":{"x":1}}"""));
        Assert.Equal((true, (string?)null), Run("users/a/days/2026-11-18", "", "users/a/days", Week, """{"2026-11-17":{"x":1}}"""));
        Assert.False(Run("users/a/days/2026-12-01", "", "users/a/days", Week, "{}").Item1);
        Assert.Equal((true, "\"v\""), Run("users/a/sync/partner", "", "users/a", "", """{"sync":{"partner":"v"}}"""));
        Assert.False(Run("users/a/sync", "", "users/a", "?shallow=true", """{"sync":true}""").Item1);
        Assert.False(Run("users/a", "", "users/a", "", "{}").Item1);
    }

    [Fact]
    public void A_narrower_key_range_is_read_from_a_wider_one()
    {
        var day = "?orderBy=%22%24key%22&startAt=%222026-11-17%22&endAt=%222026-11-17%22";
        var cached = """{"2026-11-16":{"x":0},"2026-11-17":{"x":1}}""";
        Assert.Equal((true, """{"2026-11-17":{"x":1}}"""), Run("users/a/days", day, "users/a/days", Week, cached));
        Assert.Equal((true, """{"2026-11-17":{"x":1}}"""), Run("users/a/days", day, "users/a/days", "", cached));
        Assert.False(Run("users/a/days", Week, "users/a/days", day, cached).Item1);
        Assert.False(Run("users/a/days/2026-11-17", day, "users/a/days", Week, cached).Item1);
    }
}
