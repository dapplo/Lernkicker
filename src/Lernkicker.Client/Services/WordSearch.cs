using System.Text;
using Lernkicker.Contracts;

namespace Lernkicker.Client.Services;

/// <summary>A letter in the word search grid.</summary>
public readonly record struct Cell(int Row, int Column);

/// <summary>Turns two tapped letters of a word search into a word: the line between them, read either way.</summary>
public static class WordSearch
{
    /// <summary>The cells from <paramref name="start"/> to <paramref name="end"/> if they lie on a row, a column or a diagonal; otherwise null.</summary>
    public static IReadOnlyList<Cell>? Line(Cell start, Cell end)
    {
        var rows = end.Row - start.Row;
        var columns = end.Column - start.Column;
        if (rows != 0 && columns != 0 && Math.Abs(rows) != Math.Abs(columns))
        {
            return null;
        }

        var length = Math.Max(Math.Abs(rows), Math.Abs(columns)) + 1;
        var (rowStep, columnStep) = (Math.Sign(rows), Math.Sign(columns));
        return Enumerable.Range(0, length).Select(i => new Cell(start.Row + i * rowStep, start.Column + i * columnStep)).ToList();
    }

    /// <summary>
    /// The word the line spells, forwards or backwards, among the words not found yet. Either order counts, so it
    /// does not matter whether the child taps the first or the last letter first.
    /// </summary>
    public static SheetItemDto? Find(IReadOnlyList<string> grid, IReadOnlyList<Cell> line, IEnumerable<SheetItemDto> words, ICollection<int> found)
    {
        if (line.Count < 2 || line.Any(c => c.Row < 0 || c.Row >= grid.Count || c.Column < 0 || c.Column >= grid[c.Row].Length))
        {
            return null;
        }

        var forwards = string.Concat(line.Select(c => grid[c.Row][c.Column]));
        var backwards = string.Concat(forwards.Reverse());
        return words.FirstOrDefault(w => !found.Contains(w.Index) && (Same(w.Text, forwards) || Same(w.Text, backwards)));
    }

    private static bool Same(string word, string letters) =>
        string.Equals(word.Normalize(NormalizationForm.FormC), letters, StringComparison.OrdinalIgnoreCase);
}
