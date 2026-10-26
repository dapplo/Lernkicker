using System.Text.Json;

namespace Lernkicker.Domain.Multiplayer;

/// <summary>A move along its squares: the start, then every landing square (several when capturing in a row).</summary>
public sealed record CheckersMove(IReadOnlyList<Cell> Path) : GameMove;

/// <summary>
/// The board for one player. <c>Board[row]</c> is a string of eight squares: '.' empty, 'w'/'b' a white or black
/// piece, 'W'/'B' a king; row 0 is the top, black starts there. <c>Moves</c> are the moves the player may make now
/// (only captures when a capture is possible); <c>Last</c> is the path of the last move.
/// </summary>
public sealed record CheckersView(
    IReadOnlyList<string> Board, bool IAmWhite, bool WhiteToMove, IReadOnlyList<IReadOnlyList<Cell>> Moves,
    IReadOnlyList<Cell> Last, bool Over) : GameView;

/// <summary>
/// Checkers ("Dame") for two, the German way: 8 × 8 board, twelve pieces each on the dark squares, white begins.
/// Pieces step diagonally forward and capture forward and backward; capturing is compulsory and goes on while it
/// can. A piece that ends its move on the far row becomes a king, which moves and captures any distance ("flying
/// king"). Whoever cannot move any more loses; 40 moves in a row without a capture or a piece moving forward are a
/// draw. Who plays white is drawn by lot.
/// </summary>
public sealed class Checkers : IGameRules
{
    public const int Size = 8;

    /// <summary>Moves (of both players) without a capture or a plain piece moving, after which the game is drawn.</summary>
    public const int QuietMovesForDraw = 40;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly (int Dx, int Dy)[] Diagonals = [(1, 1), (1, -1), (-1, 1), (-1, -1)];

    public string Game => MultiplayerGames.Checkers;

    public int MinPlayers => 2;

    public int MaxPlayers => 2;

    public string Start(int players, Random random)
    {
        var board = Enumerable.Range(0, Size).Select(row => new string(Enumerable.Range(0, Size)
            .Select(x => (x + row) % 2 == 0 ? '.' : row < 3 ? 'b' : row > 4 ? 'w' : '.').ToArray())).ToList();
        return Save(new State(board, random.Next(2), true, 0, [], false));
    }

    public GameStep Play(string state, int seat, GameMove move)
    {
        var s = Load(state);
        var white = seat == s.WhiteSeat;
        if (s.Over || white != s.WhiteToMove)
        {
            return GameStep.Refused(GameErrors.NotYourTurn);
        }

        if (move is not CheckersMove { Path: { Count: >= 2 } path })
        {
            return GameStep.Refused(GameErrors.MoveInvalid);
        }

        var board = Grid(s.Board);
        var legal = LegalMoves(board, white);
        var chosen = legal.FirstOrDefault(m => m.Path.SequenceEqual(path));
        if (chosen is null)
        {
            return GameStep.Refused(legal.Any(m => m.Captured.Count > 0) ? GameErrors.MustCapture : GameErrors.MoveInvalid);
        }

        var from = chosen.Path[0];
        var to = chosen.Path[^1];
        var piece = board[from.Y][from.X];
        board[from.Y][from.X] = '.';
        foreach (var captured in chosen.Captured)
        {
            board[captured.Y][captured.X] = '.';
        }

        var farRow = white ? 0 : Size - 1;
        board[to.Y][to.X] = to.Y == farRow ? char.ToUpperInvariant(piece) : piece;

        // Captures and plain pieces moving forward make progress; kings wandering about do not
        var quiet = chosen.Captured.Count == 0 && char.IsUpper(piece) ? s.QuietMoves + 1 : 0;
        var next = s with { Board = Rows(board), WhiteToMove = !white, QuietMoves = quiet, Last = chosen.Path };
        if (LegalMoves(board, !white).Count == 0)
        {
            return GameStep.Won(Save(next with { Over = true }), seat);
        }

        return quiet >= QuietMovesForDraw ? GameStep.Drawn(Save(next with { Over = true })) : GameStep.Next(Save(next));
    }

    public GameStep Resign(string state, int seat) => GameStep.Won(Save(Load(state) with { Over = true }), 1 - seat);

    public IReadOnlyList<int> ToMove(string state)
    {
        var s = Load(state);
        return s.Over ? [] : [s.WhiteToMove ? s.WhiteSeat : 1 - s.WhiteSeat];
    }

    public GameView View(string state, int seat)
    {
        var s = Load(state);
        var white = seat == s.WhiteSeat;
        var moves = !s.Over && white == s.WhiteToMove
            ? LegalMoves(Grid(s.Board), white).Select(m => m.Path).ToList()
            : [];
        return new CheckersView(s.Board, white, s.WhiteToMove, moves, s.Last, s.Over);
    }

    /// <summary>Every move the side may make: only captures (each run to its end) when there are any.</summary>
    private static List<Candidate> LegalMoves(char[][] board, bool white)
    {
        var captures = new List<Candidate>();
        var steps = new List<Candidate>();
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var piece = board[y][x];
                if (piece == '.' || IsWhite(piece) != white)
                {
                    continue;
                }

                var from = new Cell(x, y);
                Captures(board, from, piece, [from], [], captures);
                Steps(board, from, piece, steps);
            }
        }

        return captures.Count > 0 ? captures : steps;
    }

    private static void Steps(char[][] board, Cell from, char piece, List<Candidate> moves)
    {
        var forward = IsWhite(piece) ? -1 : 1;
        foreach (var (dx, dy) in Diagonals)
        {
            if (!char.IsUpper(piece) && dy != forward)
            {
                continue;
            }

            for (var (x, y) = (from.X + dx, from.Y + dy); OnBoard(x, y) && board[y][x] == '.'; x += dx, y += dy)
            {
                moves.Add(new Candidate([from, new Cell(x, y)], []));
                if (!char.IsUpper(piece))
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Follows every capture from <paramref name="at"/>. A captured piece stays on the board until the move ends,
    /// so it can neither be jumped twice nor be landed on.
    /// </summary>
    private static void Captures(char[][] board, Cell at, char piece, List<Cell> path, List<Cell> captured, List<Candidate> moves)
    {
        var start = path[0];
        var found = false;
        foreach (var (dx, dy) in Diagonals)
        {
            var (x, y) = (at.X + dx, at.Y + dy);
            if (char.IsUpper(piece))
            {
                while (OnBoard(x, y) && IsEmpty(board, x, y, start))
                {
                    (x, y) = (x + dx, y + dy);
                }
            }

            var enemy = new Cell(x, y);
            if (!OnBoard(x, y) || IsEmpty(board, x, y, start) || IsWhite(board[y][x]) == IsWhite(piece) || captured.Contains(enemy))
            {
                continue;
            }

            for (var (lx, ly) = (x + dx, y + dy); OnBoard(lx, ly) && IsEmpty(board, lx, ly, start); lx += dx, ly += dy)
            {
                var landing = new Cell(lx, ly);
                found = true;
                Captures(board, landing, piece, [.. path, landing], [.. captured, enemy], moves);
                if (!char.IsUpper(piece))
                {
                    break;
                }
            }
        }

        if (!found && captured.Count > 0)
        {
            moves.Add(new Candidate(path, captured));
        }
    }

    /// <summary>The square the moving piece left counts as empty while it captures.</summary>
    private static bool IsEmpty(char[][] board, int x, int y, Cell start) => board[y][x] == '.' || (x == start.X && y == start.Y);

    private static bool OnBoard(int x, int y) => x is >= 0 and < Size && y is >= 0 and < Size;

    private static bool IsWhite(char piece) => piece is 'w' or 'W';

    private static char[][] Grid(IReadOnlyList<string> board) => board.Select(r => r.ToCharArray()).ToArray();

    private static List<string> Rows(char[][] board) => board.Select(r => new string(r)).ToList();

    private static State Load(string state) => JsonSerializer.Deserialize<State>(state, Json)!;

    private static string Save(State state) => JsonSerializer.Serialize(state, Json);

    private sealed record Candidate(IReadOnlyList<Cell> Path, IReadOnlyList<Cell> Captured);

    private sealed record State(IReadOnlyList<string> Board, int WhiteSeat, bool WhiteToMove, int QuietMoves, IReadOnlyList<Cell> Last, bool Over);
}
