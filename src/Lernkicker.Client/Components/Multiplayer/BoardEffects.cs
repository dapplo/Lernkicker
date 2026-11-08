using Lernkicker.Contracts;

namespace Lernkicker.Client.Components.Multiplayer;

/// <summary>
/// One-off Lottie effects on squares of a board game ("poof" where a piece was taken, "sparkle" where one became a
/// king or queen), worked out by comparing the board before and after a move. Each new move gets a new round, so the
/// same square can play again (the round is part of the element's key).
/// </summary>
public sealed class BoardEffects
{
    private IReadOnlyList<string>? _board;

    public IReadOnlyDictionary<CellDto, string> Effects { get; private set; } = new Dictionary<CellDto, string>();

    public int Round { get; private set; }

    /// <summary>
    /// Takes the new board. <paramref name="from"/>/<paramref name="to"/> are the last move's first and last square;
    /// <paramref name="isPromoted"/> tells whether the piece on <c>to</c> changed into a better one on the way.
    /// Nothing plays for the board seen first.
    /// </summary>
    public void Update(IReadOnlyList<string> board, CellDto? from, CellDto? to, Func<char, char, bool> isPromoted)
    {
        var before = _board;
        _board = board;
        if (before is null || ReferenceEquals(before, board) || before.SequenceEqual(board) || from is null || to is null)
        {
            return;
        }

        var effects = new Dictionary<CellDto, string>();
        var count = (IReadOnlyList<string> b) => b.Sum(row => row.Count(c => c != '.'));
        if (count(board) < count(before))
        {
            // Taken where the piece landed on another, or a piece that vanished elsewhere (checkers jumps, en passant)
            if (At(before, to) != '.' && to != from)
            {
                effects[to] = "poof";
            }
            else
            {
                for (var y = 0; y < board.Count; y++)
                {
                    for (var x = 0; x < board[y].Length; x++)
                    {
                        var cell = new CellDto(x, y);
                        if (before[y][x] != '.' && board[y][x] == '.' && cell != from)
                        {
                            effects[cell] = "poof";
                        }
                    }
                }
            }
        }

        if (isPromoted(At(before, from), At(board, to)))
        {
            effects[to] = "sparkle";
        }

        Effects = effects;
        Round++;
    }

    private static char At(IReadOnlyList<string> board, CellDto cell) => board[cell.Y][cell.X];
}
