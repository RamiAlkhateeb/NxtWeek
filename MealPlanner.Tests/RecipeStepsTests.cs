namespace MealPlanner.Tests;

public class RecipeStepsTests
{
    [Fact]
    public void One_step_per_line_without_numbering_and_with_timers()
    {
        var steps = RecipeSteps.Parse("1. Rinse the rice\n\n2) Simmer for 20 min\n- Rest 5 minutes, then serve\n• Serve");

        Assert.Equal(["Rinse the rice", "Simmer for 20 min", "Rest 5 minutes, then serve", "Serve"], steps.Select(s => s.Text));
        Assert.Equal([null, 20, 5, null], steps.Select(s => s.Minutes));
    }

    [Theory]
    [InlineData("اطبخ الأرز ١٥ دقيقة", 15)]
    [InlineData("اتركه 10 دقائق", 10)]
    [InlineData("10 Minuten köcheln lassen", 10)]
    [InlineData("Backen, 25 Min.", 25)]
    [InlineData("Add 2 eggs", null)]
    [InlineData("Bake 900 min", null)]
    public void Times_in_every_language(string line, int? minutes) =>
        Assert.Equal(minutes, Assert.Single(RecipeSteps.Parse(line)).Minutes);

    [Fact]
    public void Round_trip_and_limit()
    {
        var text = string.Join("\n", Enumerable.Range(1, 30).Select(i => $"Step {i}"));
        var steps = RecipeSteps.Parse(text);
        Assert.Equal(RecipeSteps.MaxSteps, steps.Count);
        Assert.Equal(RecipeSteps.Parse(RecipeSteps.ToText(steps)).Select(s => s.Text), steps.Select(s => s.Text));
    }

    [Fact]
    public void Text_keeps_timers_not_written_in_the_line()
    {
        var steps = new List<RecipeStep> { new() { Text = "Cook the rice", Minutes = 12 }, new() { Text = "Fry 5 min", Minutes = 5 }, new() { Text = "Serve" } };
        Assert.Equal("Cook the rice (12 min)\nFry 5 min\nServe", RecipeSteps.ToText(steps));
        Assert.Equal(new int?[] { 12, 5, null }, RecipeSteps.Parse(RecipeSteps.ToText(steps)).Select(s => s.Minutes));
        Assert.Equal(12, RecipeSteps.Parse(RecipeSteps.ToText(steps, "{0} دقيقة"))[0].Minutes);
        Assert.Equal(12, RecipeSteps.Parse(RecipeSteps.ToText(steps, "{0} Min."))[0].Minutes);
    }
}
