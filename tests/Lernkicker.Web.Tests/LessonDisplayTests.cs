using Lernkicker.Client.Services;

namespace Lernkicker.Web.Tests;

/// <summary>The client's helpers for lessons: text formatting and hiding words of a poem.</summary>
public class LessonDisplayTests
{
    [Fact]
    public void Text_has_paragraphs_lists_bold_and_italic()
    {
        var parts = MiniMarkdown.Parse("Lies **zweimal** laut.\nDann *leise*.\n\n- eins\n- **zwei**\nDanach weiter.");

        Assert.Equal([false, true, false], parts.Select(p => p.IsList));
        Assert.Equal([new("Lies "), new("zweimal", Bold: true), new(" laut. Dann "), new("leise", Italic: true), new MiniMarkdown.Span(".")], parts[0].Lines[0]);
        Assert.Equal(2, parts[1].Lines.Count);
        Assert.Equal(new MiniMarkdown.Span("zwei", Bold: true), Assert.Single(parts[1].Lines[1]));
        Assert.Equal("Danach weiter.", Assert.Single(parts[2].Lines[0]).Text);
    }

    [Fact]
    public void Html_and_lonely_stars_stay_plain_text()
    {
        var span = Assert.Single(MiniMarkdown.Spans("<b>3 * 4</b>"));

        Assert.Equal(new MiniMarkdown.Span("<b>3 * 4</b>"), span);
    }

    [Theory]
    [InlineData(0, "Im Nebel ruhet noch die Welt,")]
    [InlineData(3, "Im _____ _____ ____ ___ ____,")]
    [InlineData(4, "__ _____ _____ ____ ___ ____,")]
    public void Poem_words_are_hidden_step_by_step(int level, string expected)
    {
        var words = PoemHelp.Words("Im Nebel ruhet noch die Welt,", level, 0);

        Assert.Equal(expected, string.Join(' ', words.Select(w => w.Leading + (w.Hidden ? PoemHelp.Gap(w.Text) : w.Text) + w.Trailing)));
    }

    [Fact]
    public void Punctuation_stays_visible()
    {
        var word = Assert.Single(PoemHelp.Words("„Welt!“", 4, 0));

        Assert.Equal(("„", "Welt", "!“", true), (word.Leading, word.Text, word.Trailing, word.Hidden));
    }

    [Fact]
    public void Every_level_hides_at_least_as_much_as_the_one_before()
    {
        const string line = "Herbstkräftig die gedämpfte Welt in warmem Golde fließen";
        var hidden = Enumerable.Range(0, PoemHelp.Levels).Select(l => PoemHelp.Words(line, l, 1).Count(w => w.Hidden)).ToList();

        Assert.Equal(hidden.Order(), hidden);
        Assert.Equal(0, hidden[0]);
        Assert.Equal(8, hidden[^1]);
    }
}
