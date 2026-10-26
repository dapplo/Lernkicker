using Lernkicker.Client.Services;

namespace Lernkicker.Web.Tests;

public class ListFilterTests
{
    [Fact]
    public void An_empty_filter_matches_everything()
    {
        var filter = new ListFilter();

        Assert.False(filter.Active);
        Assert.True(filter.Matches("Einmaleins", null, "math", "Mathe", 3));
    }

    [Theory]
    [InlineData("einmal")]
    [InlineData("MATHE")]
    [InlineData("rechnen")]
    public void The_search_looks_at_title_topic_and_subject_name(string text) =>
        Assert.True(new ListFilter { Text = text }.Matches("Einmaleins", "Kopfrechnen", "math", "Mathe", 3));

    [Fact]
    public void Subject_and_grade_must_both_fit()
    {
        var filter = new ListFilter { Subject = "math", Grade = 4 };

        Assert.True(filter.Matches("A", null, "math", "Mathe", 4));
        Assert.False(filter.Matches("A", null, "math", "Mathe", 3));
        Assert.False(filter.Matches("A", null, "german", "Deutsch", 4));
    }

    [Fact]
    public void The_adults_grade_matches_material_without_a_school_year()
    {
        var filter = new ListFilter { Grade = ListFilter.Adults };

        Assert.True(filter.Matches("Mau-Mau", null, "other", "Sonstiges", null));
        Assert.False(filter.Matches("Mau-Mau", null, "other", "Sonstiges", 2));
    }

    [Fact]
    public void Clear_resets_every_filter()
    {
        var filter = new ListFilter { Text = "x", Kind = "quiz", Subject = "math", Grade = 2 };

        filter.Clear();

        Assert.False(filter.Active);
    }
}
