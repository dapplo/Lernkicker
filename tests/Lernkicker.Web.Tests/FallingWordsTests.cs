using Lernkicker.Client.Services;
using Lernkicker.Contracts;

namespace Lernkicker.Web.Tests;

/// <summary>The rules of the falling words game on the child's device (Lernkicker.Client).</summary>
public class FallingWordsTests
{
    private static readonly FallingWordDto Weizen = new(0, "Weizen", "Getreide");

    private static FallingWordsSheetDto Sheet(int targets) =>
        new("food", "Fang die Wörter!", null, 1, ["Getreide", "Gemüse", "Obst", "Nuss", "Pilz"], [Weizen], targets, 10);

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void The_targets_hold_the_answer_and_others(int count)
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var targets = FallingWords.Targets(Sheet(count), Weizen, new Random(seed));

            Assert.Equal(count, targets.Count);
            Assert.Single(targets, t => t == "Getreide");
            Assert.Equal(count, targets.Distinct().Count());
        }
    }

    [Fact]
    public void The_answer_is_not_always_in_the_same_place()
    {
        var places = Enumerable.Range(0, 30).Select(seed => FallingWords.Targets(Sheet(3), Weizen, new Random(seed)).ToList().IndexOf("Getreide"));

        Assert.Equal(3, places.Distinct().Count());
    }

    [Fact]
    public void Words_fall_faster_but_not_too_fast()
    {
        Assert.Equal(10_000, FallingWords.FallMilliseconds(10, 0));
        Assert.True(FallingWords.FallMilliseconds(10, 3) < FallingWords.FallMilliseconds(10, 2));
        Assert.Equal(4_500, FallingWords.FallMilliseconds(10, 100));
    }

    [Fact]
    public void Every_theme_has_a_unique_id() =>
        Assert.Equal(Theme.All.Count, Theme.All.Select(t => t.Id).Distinct().Count());

    [Fact]
    public void An_unknown_theme_falls_back_to_the_first() =>
        Assert.Same(Theme.All[0], Theme.Find("pirates"));
}
