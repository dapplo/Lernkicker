using Lernkicker.Client.Services;
using Lernkicker.Contracts;

namespace Lernkicker.Web.Tests;

/// <summary>How the child's taps in a word search become a word (Lernkicker.Client).</summary>
public class WordSearchTests
{
    private static readonly string[] Grid =
    [
        "HAFERR",
        "XMXXXO",
        "XAXXXG",
        "XIXSXG",
        "XSXXXE",
        "XXXXXN",
    ];

    private static readonly SheetItemDto[] Words = [new(0, "Hafer"), new(1, "Mais"), new(2, "Roggen")];

    [Fact]
    public void A_line_runs_along_a_row_a_column_or_a_diagonal()
    {
        Assert.Equal([new Cell(0, 0), new Cell(0, 1), new Cell(0, 2)], WordSearch.Line(new Cell(0, 0), new Cell(0, 2)));
        Assert.Equal([new Cell(2, 2), new Cell(1, 1), new Cell(0, 0)], WordSearch.Line(new Cell(2, 2), new Cell(0, 0)));
        Assert.Null(WordSearch.Line(new Cell(0, 0), new Cell(1, 2)));
    }

    [Theory]
    [InlineData(0, 0, 0, 4, 0)]
    [InlineData(0, 4, 0, 0, 0)]
    [InlineData(1, 1, 4, 1, 1)]
    [InlineData(5, 5, 0, 5, 2)]
    public void Either_end_may_be_tapped_first(int startRow, int startColumn, int endRow, int endColumn, int expected)
    {
        var line = WordSearch.Line(new Cell(startRow, startColumn), new Cell(endRow, endColumn))!;

        Assert.Equal(expected, WordSearch.Find(Grid, line, Words, [])?.Index);
    }

    [Fact]
    public void A_word_found_already_or_a_wrong_line_is_no_word()
    {
        var hafer = WordSearch.Line(new Cell(0, 0), new Cell(0, 4))!;

        Assert.Null(WordSearch.Find(Grid, hafer, Words, [0]));
        Assert.Null(WordSearch.Find(Grid, WordSearch.Line(new Cell(0, 0), new Cell(0, 3))!, Words, []));
    }

    [Fact]
    public void Umlauts_match_in_any_case()
    {
        Assert.NotNull(WordSearch.Find(["GRÄSER"], WordSearch.Line(new Cell(0, 0), new Cell(0, 5))!, [new SheetItemDto(0, "Gräser")], []));
    }
}
