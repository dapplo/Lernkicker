using System.Text.Json;

namespace Lernkicker.Domain.Multiplayer;

/// <summary>Drops a disc into a column (0 = left).</summary>
public sealed record ConnectFourDrop(int Column) : GameMove;

/// <summary>
/// The board as everyone sees it (nothing is hidden): <c>Cells[row][column]</c> holds the seat of the disc or -1,
/// row 0 is the top. <c>Line</c> are the four (or more) cells that won; <c>Last</c> is the newest disc.
/// </summary>
public sealed record ConnectFourView(
    IReadOnlyList<IReadOnlyList<int>> Cells, int Turn, IReadOnlyList<Cell> Line, Cell? Last, bool Over) : GameView;

/// <summary>A place on a board: column and row.</summary>
public sealed record Cell(int X, int Y);

/// <summary>
/// Connect Four ("Vier gewinnt") for two: discs drop into seven columns of six; four in a row across, down or
/// diagonally win. A full board is a draw. The player who starts is drawn by lot.
/// </summary>
public sealed class ConnectFour : IGameRules
{
    public const int Columns = 7;
    public const int Rows = 6;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Game => MultiplayerGames.ConnectFour;

    public int MinPlayers => 2;

    public int MaxPlayers => 2;

    public string Start(int players, Random random) =>
        Save(new State(Enumerable.Repeat(-1, Columns * Rows).ToList(), random.Next(2), null, [], false));

    public GameStep Play(string state, int seat, GameMove move)
    {
        var s = Load(state);
        if (s.Over || s.Turn != seat)
        {
            return GameStep.Refused(GameErrors.NotYourTurn);
        }

        if (move is not ConnectFourDrop { Column: >= 0 and < Columns } drop)
        {
            return GameStep.Refused(GameErrors.MoveInvalid);
        }

        // The disc falls to the lowest free row
        var row = Enumerable.Range(0, Rows).Reverse().FirstOrDefault(r => s.Cells[Index(drop.Column, r)] == -1, -1);
        if (row < 0)
        {
            return GameStep.Refused(GameErrors.ColumnFull);
        }

        var cells = s.Cells.ToList();
        cells[Index(drop.Column, row)] = seat;
        var last = new Cell(drop.Column, row);
        var line = WinningLine(cells, last, seat);
        if (line.Count > 0)
        {
            return GameStep.Won(Save(s with { Cells = cells, Last = last, Line = line, Over = true }), seat);
        }

        if (cells.All(c => c != -1))
        {
            return GameStep.Drawn(Save(s with { Cells = cells, Last = last, Over = true }));
        }

        return GameStep.Next(Save(s with { Cells = cells, Last = last, Turn = 1 - seat }));
    }

    public GameStep Resign(string state, int seat)
    {
        var s = Load(state);
        return GameStep.Won(Save(s with { Over = true }), 1 - seat);
    }

    public IReadOnlyList<int> ToMove(string state)
    {
        var s = Load(state);
        return s.Over ? [] : [s.Turn];
    }

    public GameView View(string state, int seat)
    {
        var s = Load(state);
        var rows = Enumerable.Range(0, Rows)
            .Select(r => (IReadOnlyList<int>)Enumerable.Range(0, Columns).Select(c => s.Cells[Index(c, r)]).ToList())
            .ToList();
        return new ConnectFourView(rows, s.Turn, s.Line, s.Last, s.Over);
    }

    private static int Index(int column, int row) => row * Columns + column;

    /// <summary>The cells of every line of four or more through the new disc.</summary>
    private static List<Cell> WinningLine(List<int> cells, Cell from, int seat)
    {
        var line = new List<Cell>();
        foreach (var (dx, dy) in new[] { (1, 0), (0, 1), (1, 1), (1, -1) })
        {
            var run = new List<Cell> { from };
            foreach (var sign in new[] { 1, -1 })
            {
                for (var (x, y) = (from.X + sign * dx, from.Y + sign * dy);
                     x is >= 0 and < Columns && y is >= 0 and < Rows && cells[Index(x, y)] == seat;
                     x += sign * dx, y += sign * dy)
                {
                    run.Add(new Cell(x, y));
                }
            }

            if (run.Count >= 4)
            {
                line.AddRange(run.Where(c => !line.Contains(c)));
            }
        }

        return line;
    }

    private static State Load(string state) => JsonSerializer.Deserialize<State>(state, Json)!;

    private static string Save(State state) => JsonSerializer.Serialize(state, Json);

    private sealed record State(IReadOnlyList<int> Cells, int Turn, Cell? Last, IReadOnlyList<Cell> Line, bool Over);
}
