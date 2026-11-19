using System.Text.Json.Nodes;
using MealPlanner.Shared.Nutrition;
using Xunit;

namespace MealPlanner.Tests;

public class AiFallbackTests
{
    [Fact]
    public async Task With_a_key_Gemini_answers_and_the_worker_is_not_asked()
    {
        var worker = 0;
        var result = await AiFallback.RunAsync(() => Task.FromResult(true), () => Task.FromResult("gemini"), () => { worker++; return Task.FromResult("worker"); });
        Assert.Equal("gemini", result);
        Assert.Equal(0, worker);
    }

    [Fact]
    public async Task Without_a_key_the_worker_answers()
    {
        var gemini = 0;
        var result = await AiFallback.RunAsync(() => Task.FromResult(false), () => { gemini++; return Task.FromResult("gemini"); }, () => Task.FromResult("worker"));
        Assert.Equal("worker", result);
        Assert.Equal(0, gemini);
    }

    [Fact]
    public async Task A_Gemini_failure_falls_back_to_the_worker()
    {
        var result = await AiFallback.RunAsync(() => Task.FromResult(true), () => Task.FromException<string>(new InvalidOperationException("429")), () => Task.FromResult("worker"));
        Assert.Equal("worker", result);
    }

    [Fact]
    public async Task A_failing_key_check_counts_as_no_key()
    {
        var result = await AiFallback.RunAsync(() => Task.FromException<bool>(new Exception()), () => Task.FromResult("gemini"), () => Task.FromResult("worker"));
        Assert.Equal("worker", result);
    }

    [Fact]
    public async Task When_both_fail_the_workers_error_is_thrown()
    {
        await Assert.ThrowsAsync<TimeoutException>(() => AiFallback.RunAsync(() => Task.FromResult(true),
            () => Task.FromException<string>(new InvalidOperationException()), () => Task.FromException<string>(new TimeoutException())));
    }

    [Fact]
    public async Task Cancelling_does_not_fall_back()
    {
        var worker = 0;
        await Assert.ThrowsAsync<OperationCanceledException>(() => AiFallback.RunAsync(() => Task.FromResult(true),
            () => Task.FromException<string>(new OperationCanceledException()), () => { worker++; return Task.FromResult("worker"); }));
        Assert.Equal(0, worker);
    }

    [Fact]
    public void Parses_the_workers_details_into_the_Gemini_shape()
    {
        var body = JsonNode.Parse("""{"details":{"ingredients":[{"id":"rice","name":"رز","quantity":80,"unit":"g","aisle":"pantry"}],"servings":4,"minutes":45,"kcal":520,"protein":42,"carbs":55,"fat":12,"fibre":3}}""");
        var d = AiFallback.ParseDetails(body)!;
        Assert.Equal("rice", d.Ingredients.Single().Id);
        Assert.Equal(80, d.Ingredients.Single().Quantity);
        Assert.Equal(4, d.Servings);
        Assert.Equal(45, d.Minutes);
        Assert.Equal(520, d.Kcal);
        Assert.Equal(3, d.Fibre);
        // And it passes the same plausibility check Gemini's answers do.
        Assert.Same(d, MealDetailsMapper.Validate(d));
    }

    [Fact]
    public void Missing_or_broken_details_are_null()
    {
        Assert.Null(AiFallback.ParseDetails(null));
        Assert.Null(AiFallback.ParseDetails(JsonNode.Parse("""{"error":"model_failed"}""")));
        Assert.Null(AiFallback.ParseDetails(JsonNode.Parse("""{"details":{"kcal":"lots"}}""")));
    }

    [Fact]
    public void Parses_the_workers_store_reply()
    {
        var look = StoreLogo.Clean(AiFallback.ParseStore(JsonNode.Parse("""{"domain":"lidl.de","emoji":"🛒"}""")));
        Assert.Equal("lidl.de", look.Domain);
        Assert.Equal("🛒", look.Emoji);
        Assert.Null(AiFallback.ParseStore(JsonNode.Parse("[]")));
    }
}
