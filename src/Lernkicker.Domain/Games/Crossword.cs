using Lernkicker.Domain.Quizzes;

namespace Lernkicker.Domain.Games;

/// <summary>
/// A word in a crossword: its number, first letter (row and column from the top left) and direction. <c>Answer</c>
/// is in upper case, one letter per cell.
/// </summary>
public sealed record CrosswordWord(int Number, int Row, int Column, bool Down, string Answer, string Clue)
{
    public IEnumerable<(int Row, int Column)> Cells() =>
        Enumerable.Range(0, Answer.Length).Select(i => Down ? (Row + i, Column) : (Row, Column + i));
}

/// <summary>A generated crossword: the words cross each other and fit in <c>Width</c> × <c>Height</c> cells.</summary>
public sealed record Crossword(int Width, int Height, IReadOnlyList<CrosswordWord> Words)
{
    /// <summary>Largest width or height, so the grid still fits a phone.</summary>
    public const int MaxSize = 13;

    private const int Attempts = 40;

    /// <summary>
    /// Builds a crossword from words and their clues. Each attempt starts with the longest word and adds the others
    /// where they cross a word already placed, preferring places that cross more than one word and keep the grid
    /// small. Words that cross nothing are left out. The attempt with the most words wins, then the smallest one.
    /// </summary>
    public static Crossword Generate(IReadOnlyList<(string Word, string Clue)> words, Random random)
    {
        var entries = words.Select(w => (Answer: WordSearchGrid.Letters(w.Word), w.Clue)).ToList();
        Board? best = null;
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            // The first attempt goes from long to short; the others shuffle a little for variety
            var order = attempt == 0
                ? entries.OrderByDescending(e => e.Answer.Length).ToList()
                : entries.OrderByDescending(e => e.Answer.Length + random.Next(4)).ToList();
            var board = Build(order, random);
            if (best is null || board.Placed.Count > best.Placed.Count || (board.Placed.Count == best.Placed.Count && board.Area < best.Area))
            {
                best = board;
            }
        }

        return best!.ToCrossword();
    }

    private static Board Build(List<(string Answer, string Clue)> order, Random random)
    {
        var board = new Board();
        board.Place(order[0].Answer, order[0].Clue, 0, 0, random.Next(2) == 0);
        foreach (var (answer, clue) in order.Skip(1))
        {
            var spots = board.Spots(answer).ToList();
            if (spots.Count == 0)
            {
                continue;
            }

            var top = spots.Max(s => s.Score);
            var (row, column, down, _) = spots.Where(s => s.Score == top).ElementAt(random.Next(spots.Count(s => s.Score == top)));
            board.Place(answer, clue, row, column, down);
        }

        return board;
    }

    /// <summary>Placed words on an unbounded board (coordinates may be negative until the grid is cut out).</summary>
    private sealed class Board
    {
        private readonly Dictionary<(int Row, int Column), char> _cells = [];

        public List<(string Answer, string Clue, int Row, int Column, bool Down)> Placed { get; } = [];

        private int Top => _cells.Keys.Min(c => c.Row);
        private int Bottom => _cells.Keys.Max(c => c.Row);
        private int Left => _cells.Keys.Min(c => c.Column);
        private int Right => _cells.Keys.Max(c => c.Column);

        public int Area => (Bottom - Top + 1) * (Right - Left + 1);

        public void Place(string answer, string clue, int row, int column, bool down)
        {
            for (var i = 0; i < answer.Length; i++)
            {
                _cells[down ? (row + i, column) : (row, column + i)] = answer[i];
            }

            Placed.Add((answer, clue, row, column, down));
        }

        /// <summary>Every place where the word crosses a placed letter and breaks no rule; more crossings and a smaller grid score higher.</summary>
        public IEnumerable<(int Row, int Column, bool Down, int Score)> Spots(string answer)
        {
            foreach (var ((cellRow, cellColumn), letter) in _cells.ToList())
            {
                for (var i = 0; i < answer.Length; i++)
                {
                    if (answer[i] != letter)
                    {
                        continue;
                    }

                    foreach (var down in new[] { false, true })
                    {
                        var (row, column) = down ? (cellRow - i, cellColumn) : (cellRow, cellColumn - i);
                        if (Crossings(answer, row, column, down) is { } crossings)
                        {
                            yield return (row, column, down, crossings * 10 - GrowthIfPlaced(answer, row, column, down));
                        }
                    }
                }
            }
        }

        /// <summary>How many letters the word would share, or null if it clashes, touches a word side by side, or makes the grid too big.</summary>
        private int? Crossings(string answer, int row, int column, bool down)
        {
            var (rowStep, columnStep) = down ? (1, 0) : (0, 1);
            if (_cells.ContainsKey((row - rowStep, column - columnStep)) || _cells.ContainsKey((row + rowStep * answer.Length, column + columnStep * answer.Length)))
            {
                return null;
            }

            var crossings = 0;
            for (var i = 0; i < answer.Length; i++)
            {
                var cell = (Row: row + rowStep * i, Column: column + columnStep * i);
                if (_cells.TryGetValue(cell, out var existing))
                {
                    if (existing != answer[i] || Placed.Any(p => p.Down == down && Covers(p, cell)))
                    {
                        return null;
                    }

                    crossings++;
                }
                else if (_cells.ContainsKey((cell.Row + columnStep, cell.Column + rowStep)) || _cells.ContainsKey((cell.Row - columnStep, cell.Column - rowStep)))
                {
                    // A new letter must not sit right next to another word's letter
                    return null;
                }
            }

            var newTop = Math.Min(Top, row);
            var newLeft = Math.Min(Left, column);
            var newBottom = Math.Max(Bottom, row + rowStep * (answer.Length - 1));
            var newRight = Math.Max(Right, column + columnStep * (answer.Length - 1));
            var fits = newBottom - newTop + 1 <= MaxSize && newRight - newLeft + 1 <= MaxSize;
            return crossings > 0 && crossings < answer.Length && fits ? crossings : null;
        }

        private int GrowthIfPlaced(string answer, int row, int column, bool down)
        {
            var bottom = down ? row + answer.Length - 1 : row;
            var right = down ? column : column + answer.Length - 1;
            var area = (Math.Max(Bottom, bottom) - Math.Min(Top, row) + 1) * (Math.Max(Right, right) - Math.Min(Left, column) + 1);
            return area - Area;
        }

        private static bool Covers((string Answer, string Clue, int Row, int Column, bool Down) placed, (int Row, int Column) cell) =>
            placed.Down
                ? cell.Column == placed.Column && cell.Row >= placed.Row && cell.Row < placed.Row + placed.Answer.Length
                : cell.Row == placed.Row && cell.Column >= placed.Column && cell.Column < placed.Column + placed.Answer.Length;

        /// <summary>Cuts the grid out and numbers the words in reading order of their first letters, like a printed crossword.</summary>
        public Crossword ToCrossword()
        {
            var (top, left) = (Top, Left);
            var starts = Placed.Select(p => (Row: p.Row - top, Column: p.Column - left)).Distinct().OrderBy(s => s.Row).ThenBy(s => s.Column).ToList();
            var words = Placed
                .Select(p => new CrosswordWord(starts.IndexOf((p.Row - top, p.Column - left)) + 1, p.Row - top, p.Column - left, p.Down, p.Answer, p.Clue))
                .OrderBy(w => w.Down).ThenBy(w => w.Number)
                .ToList();
            return new Crossword(Right - left + 1, Bottom - top + 1, words);
        }
    }
}
