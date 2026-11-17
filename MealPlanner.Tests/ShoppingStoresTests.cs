using MealPlanner.Shared.Models;
using MealPlanner.Shared.Nutrition;

namespace MealPlanner.Tests;

public class ShoppingStoresTests
{
    private static ShoppingShop Shop(string id, string name, int minute = 0, params string[] items) => new()
    {
        Id = id, Name = name, CreatedAtUtc = minute == 0 ? default : new DateTime(2026, 1, 1, 0, minute, 0, DateTimeKind.Utc),
        Items = items.ToDictionary(i => i, i => new ShoppingItem { Id = i, Name = i })
    };

    [Fact]
    public void The_first_store_made_is_the_default_and_listed_first()
    {
        var shops = new List<ShoppingShop> { Shop("b", "Lidl", 5), Shop("a", "Aldi", 9), Shop("c", "List", 1) };
        Assert.Equal("c", ShoppingStores.Default(shops)!.Id);
        Assert.Equal(new[] { "List", "Aldi", "Lidl" }, ShoppingStores.Ordered(shops).Select(s => s.Name));
    }

    [Fact]
    public void Stores_from_before_creation_times_come_first_in_a_stable_order()
    {
        var shops = new List<ShoppingShop> { Shop("z", "Old two"), Shop("y", "Old one"), Shop("x", "New", 3) };
        Assert.Equal("y", ShoppingStores.Default(shops)!.Id);
        Assert.Empty(ShoppingStores.Ordered([]));
        Assert.Null(ShoppingStores.Default([]));
    }

    [Fact]
    public void A_remembered_store_is_used_only_while_it_exists()
    {
        var shops = new List<ShoppingShop> { Shop("a", "Aldi", 1), Shop("b", "Lidl", 2) };
        var routes = new Dictionary<string, string> { ["rice|g"] = "b", ["milk|ml"] = "gone" };
        Assert.Equal("b", ShoppingStores.Remembered("rice|g", routes, shops)!.Id);
        Assert.Null(ShoppingStores.Remembered("milk|ml", routes, shops));
        Assert.Null(ShoppingStores.Remembered("eggs|pc", routes, shops));
    }

    [Fact]
    public void An_item_caught_in_two_stores_does_not_break_the_lookup()
    {
        var shops = new List<ShoppingShop> { Shop("a", "Aldi", 1, "i1", "i2"), Shop("b", "Lidl", 2, "i2", "i3") };
        var map = ShoppingStores.StoreOfItems(shops);
        Assert.Equal(new Dictionary<string, string> { ["i1"] = "a", ["i2"] = "a", ["i3"] = "b" }, map);
    }

    [Fact]
    public void A_deleted_stores_items_go_to_the_default_or_the_next_store()
    {
        var shops = new List<ShoppingShop> { Shop("a", "List", 1), Shop("b", "Lidl", 2), Shop("c", "Aldi", 3) };
        Assert.Equal("a", ShoppingStores.HeirOf("b", shops)!.Id);
        Assert.Equal("c", ShoppingStores.HeirOf("a", shops)!.Id);
        Assert.Null(ShoppingStores.HeirOf("a", [shops[0]]));
    }

    [Fact]
    public void Names_are_compared_ignoring_case_and_spaces()
    {
        var shops = new List<ShoppingShop> { Shop("a", "Lidl", 1) };
        Assert.True(ShoppingStores.NameTaken(" lidl ", shops));
        Assert.False(ShoppingStores.NameTaken("Lidl", shops, exceptId: "a"));
        Assert.False(ShoppingStores.NameTaken("Aldi", shops));
    }
}

public class StoreLogoTests
{
    [Theory]
    [InlineData("lidl.de", "lidl.de")]
    [InlineData("https://www.Carrefour.com/fr/", "carrefour.com")]
    [InlineData("www.aldi-sued.de", "aldi-sued.de")]
    [InlineData("", null)]
    [InlineData("not a domain", null)]
    [InlineData("localhost", null)]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("lidl..de", null)]
    public void Only_plain_domains_are_kept(string reply, string? expected) =>
        Assert.Equal(expected, StoreLogo.Clean(new StoreLogoReply { Domain = reply }).Domain);

    [Theory]
    [InlineData("🛒", "🛒")]
    [InlineData("👨‍🍳", "👨‍🍳")]
    [InlineData("🥖 ", "🥖")]
    [InlineData("🛒🥖", null)]
    [InlineData("A", null)]
    [InlineData("shop", null)]
    [InlineData("", null)]
    public void Only_one_emoji_is_kept(string reply, string? expected) =>
        Assert.Equal(expected, StoreLogo.Clean(new StoreLogoReply { Emoji = reply }).Emoji);

    [Fact]
    public void No_answer_is_no_look() => Assert.Equal(StoreLook.None, StoreLogo.Clean(null));

    [Fact]
    public void Icon_url_and_initial()
    {
        Assert.Equal("https://www.google.com/s2/favicons?sz=128&domain=lidl.de", StoreLogo.IconUrl("lidl.de"));
        Assert.Equal("L", StoreLogo.Initial(" lidl"));
        Assert.Equal("ك", StoreLogo.Initial("كارفور"));
        Assert.Equal("?", StoreLogo.Initial(" "));
    }
}
