using System.Text;
using Lernkicker.Contracts;

namespace Lernkicker.Client.Services;

/// <summary>Helpers for the crossword game on the child's device.</summary>
public static class Crossword
{
    /// <summary>The cells of a word, from its first letter.</summary>
    public static IEnumerable<(int Row, int Column)> Cells(CrosswordWordDto word) =>
        Enumerable.Range(0, word.Answer.Length).Select(i => word.Down ? (word.Row + i, word.Column) : (word.Row, word.Column + i));

    /// <summary>What the child typed, as the grid shows it: composed and upper case, like the answers.</summary>
    public static string Letters(string? typed) => (typed ?? "").Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant();
}
