using System.Text;

namespace Lernkicker.Domain.Quizzes;

/// <summary>Where a word sits in the grid: its first letter and the step to the next letter.</summary>
public sealed record WordPlacement(int Word, int Row, int Column, int RowStep, int ColumnStep);

/// <summary>A generated word search grid. <c>Rows</c> holds one string per row, one letter per column.</summary>
public sealed record WordSearchGrid(IReadOnlyList<string> Rows, IReadOnlyList<WordPlacement> Placements)
{
    /// <summary>Smallest grid a size is picked from when the quiz does not set one.</summary>
    public const int DefaultMinSize = 8;

    /// <summary>Largest grid picked when the quiz does not set one (still fits a phone), unless a word is longer.</summary>
    public const int DefaultMaxSize = 12;

    private const int AttemptsPerSize = 50;
    private const string Latin = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public int Size => Rows.Count;

    /// <summary>The letters of a word as they appear in the grid: composed and upper case.</summary>
    public static string Letters(string word) => word.Normalize(NormalizationForm.FormC).ToUpperInvariant();

    /// <summary>
    /// Hides the words in a new grid. Longer words are placed first; each word takes the direction used least so
    /// far, so horizontal, vertical (and diagonal) words come out balanced. Crossing words share a letter if the
    /// exercise allows overlaps. If the words do not fit after a number of attempts, the grid grows by one.
    /// </summary>
    public static WordSearchGrid Generate(WordSearchExercise exercise, Random random)
    {
        var words = exercise.Words.Select(Letters).ToList();
        var directions = Directions(exercise.AllowsDiagonal, exercise.AllowsBackwards);
        var filler = Filler(words);
        for (var size = StartSize(exercise, words); ; size++)
        {
            for (var attempt = 0; attempt < AttemptsPerSize; attempt++)
            {
                if (TryPlace(words, size, directions, exercise.AllowsOverlap, random) is { } placed)
                {
                    return Fill(placed.Cells, placed.Placements, filler, random);
                }
            }
        }
    }

    private static int StartSize(WordSearchExercise exercise, List<string> words)
    {
        var longest = words.Max(w => w.Length);
        if (exercise.Size is { } size)
        {
            return Math.Max(size, longest);
        }

        // About half of the cells hold letters of the words, the rest is filler
        var forLetters = (int)Math.Ceiling(Math.Sqrt(words.Sum(w => w.Length) * 2.0));
        return Math.Max(longest, Math.Clamp(forLetters, DefaultMinSize, DefaultMaxSize));
    }

    private static List<(int Row, int Column)> Directions(bool diagonal, bool backwards)
    {
        List<(int, int)> forward = diagonal ? [(0, 1), (1, 0), (1, 1), (-1, 1)] : [(0, 1), (1, 0)];
        return backwards ? [.. forward, .. forward.Select(d => (-d.Item1, -d.Item2))] : forward;
    }

    /// <summary>Filler letters: the alphabet plus the words' own letters (e.g. umlauts), so no letter gives a word away.</summary>
    private static string Filler(List<string> words)
    {
        var letters = string.Concat(words);
        return letters.Any(c => Latin.Contains(c)) ? Latin + letters : letters;
    }

    private static (char?[,] Cells, List<WordPlacement> Placements)? TryPlace(
        List<string> words, int size, List<(int Row, int Column)> directions, bool overlap, Random random)
    {
        var cells = new char?[size, size];
        var placements = new List<WordPlacement>();
        var order = words.Select((w, i) => (Word: w, Index: i, Tie: random.Next())).OrderByDescending(x => x.Word.Length).ThenBy(x => x.Tie);
        foreach (var (word, index, _) in order)
        {
            // Least used axis (horizontal, vertical, diagonal) first, then least used direction on it
            var byUse = directions
                .Select(d => (Direction: d, Axis: placements.Count(p => Axis(p.RowStep, p.ColumnStep) == Axis(d.Row, d.Column)),
                    Same: placements.Count(p => (p.RowStep, p.ColumnStep) == d), Tie: random.Next()))
                .OrderBy(x => x.Axis).ThenBy(x => x.Same).ThenBy(x => x.Tie)
                .Select(x => x.Direction);
            WordPlacement? chosen = null;
            foreach (var (rowStep, columnStep) in byUse)
            {
                var spots = new List<WordPlacement>();
                for (var row = 0; row < size; row++)
                {
                    for (var column = 0; column < size; column++)
                    {
                        var spot = new WordPlacement(index, row, column, rowStep, columnStep);
                        if (Fits(cells, size, word, spot, overlap))
                        {
                            spots.Add(spot);
                        }
                    }
                }

                if (spots.Count > 0)
                {
                    chosen = spots[random.Next(spots.Count)];
                    break;
                }
            }

            if (chosen is null)
            {
                return null;
            }

            for (var i = 0; i < word.Length; i++)
            {
                cells[chosen.Row + i * chosen.RowStep, chosen.Column + i * chosen.ColumnStep] = word[i];
            }

            placements.Add(chosen);
        }

        return (cells, placements.OrderBy(p => p.Word).ToList());
    }

    /// <summary>0 horizontal, 1 vertical, 2 diagonal.</summary>
    private static int Axis(int rowStep, int columnStep) => rowStep == 0 ? 0 : columnStep == 0 ? 1 : 2;

    private static bool Fits(char?[,] cells, int size, string word, WordPlacement spot, bool overlap)
    {
        var endRow = spot.Row + (word.Length - 1) * spot.RowStep;
        var endColumn = spot.Column + (word.Length - 1) * spot.ColumnStep;
        if (endRow < 0 || endRow >= size || endColumn < 0 || endColumn >= size)
        {
            return false;
        }

        for (var i = 0; i < word.Length; i++)
        {
            if (cells[spot.Row + i * spot.RowStep, spot.Column + i * spot.ColumnStep] is { } letter && (!overlap || letter != word[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static WordSearchGrid Fill(char?[,] cells, List<WordPlacement> placements, string filler, Random random)
    {
        var size = cells.GetLength(0);
        var rows = Enumerable.Range(0, size)
            .Select(row => string.Concat(Enumerable.Range(0, size).Select(column => cells[row, column] ?? filler[random.Next(filler.Length)])))
            .ToList();
        return new WordSearchGrid(rows, placements);
    }
}
