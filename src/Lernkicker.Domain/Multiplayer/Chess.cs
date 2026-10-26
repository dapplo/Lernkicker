using System.Text.Json;

namespace Lernkicker.Domain.Multiplayer;

/// <summary>
/// A chess move from one square to another; <c>Promotion</c> ("Q", "R", "B" or "N") when a pawn reaches the last
/// row. Castling is the king's move of two squares.
/// </summary>
public sealed record ChessMove(Cell From, Cell To, string? Promotion = null) : GameMove;

/// <summary>
/// The board for one player. <c>Board[row]</c> is a string of eight squares, row 0 is rank 8 (black's side):
/// "KQRBNP" are white pieces, "kqrbnp" black ones, '.' is empty. <c>Moves</c> are the legal moves of the player
/// when it is their turn; <c>Check</c> says the side to move is in check. <c>Ending</c>: "checkmate", "stalemate",
/// "repetition", "fiftyMoves", "material" or null while the game goes on (or ended by giving up).
/// </summary>
public sealed record ChessView(
    IReadOnlyList<string> Board, bool IAmWhite, bool WhiteToMove, IReadOnlyList<ChessMove> Moves, bool Check,
    ChessMove? Last, string? Ending, bool Over) : GameView;

/// <summary>
/// Chess for two with all rules: castling, en passant, promotion, check and checkmate; stalemate, threefold
/// repetition, the fifty-move rule and too little material to mate are draws. Who plays white is drawn by lot.
/// </summary>
public sealed class Chess : IGameRules
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly (int Dx, int Dy)[] Straight = [(1, 0), (-1, 0), (0, 1), (0, -1)];
    private static readonly (int Dx, int Dy)[] Diagonal = [(1, 1), (1, -1), (-1, 1), (-1, -1)];
    private static readonly (int Dx, int Dy)[] Knight = [(1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2)];
    private static readonly string[] Promotions = ["Q", "R", "B", "N"];

    public static readonly IReadOnlyList<string> StartBoard =
        ["rnbqkbnr", "pppppppp", "........", "........", "........", "........", "PPPPPPPP", "RNBQKBNR"];

    public string Game => MultiplayerGames.Chess;

    public int MinPlayers => 2;

    public int MaxPlayers => 2;

    public string Start(int players, Random random)
    {
        var position = new Position(StartBoard, true, "KQkq", null, 0);
        return Save(new State(position, random.Next(2), [position.Key], null, null, false));
    }

    public GameStep Play(string state, int seat, GameMove move)
    {
        var s = Load(state);
        var white = seat == s.WhiteSeat;
        if (s.Over || white != s.Position.WhiteToMove)
        {
            return GameStep.Refused(GameErrors.NotYourTurn);
        }

        if (move is not ChessMove m)
        {
            return GameStep.Refused(GameErrors.MoveInvalid);
        }

        var chosen = LegalMoves(s.Position).FirstOrDefault(l => l.From == m.From && l.To == m.To && l.Promotion == m.Promotion?.ToUpperInvariant());
        if (chosen is null)
        {
            return GameStep.Refused(GameErrors.ChessMoveInvalid);
        }

        var position = Apply(s.Position, chosen);
        var history = s.History.Append(position.Key).ToList();
        var next = s with { Position = position, History = history, Last = chosen };

        if (LegalMoves(position).Count == 0)
        {
            return InCheck(position.Board, position.WhiteToMove)
                ? GameStep.Won(Save(next with { Over = true, Ending = "checkmate" }), seat)
                : GameStep.Drawn(Save(next with { Over = true, Ending = "stalemate" }));
        }

        var ending = history.Count(k => k == position.Key) >= 3 ? "repetition"
            : position.HalfMoves >= 100 ? "fiftyMoves"
            : TooLittleMaterial(position.Board) ? "material"
            : null;
        return ending is null ? GameStep.Next(Save(next)) : GameStep.Drawn(Save(next with { Over = true, Ending = ending }));
    }

    public GameStep Resign(string state, int seat) => GameStep.Won(Save(Load(state) with { Over = true }), 1 - seat);

    public IReadOnlyList<int> ToMove(string state)
    {
        var s = Load(state);
        return s.Over ? [] : [s.Position.WhiteToMove ? s.WhiteSeat : 1 - s.WhiteSeat];
    }

    public GameView View(string state, int seat)
    {
        var s = Load(state);
        var white = seat == s.WhiteSeat;
        var p = s.Position;
        var moves = !s.Over && white == p.WhiteToMove ? LegalMoves(p) : [];
        return new ChessView(p.Board, white, p.WhiteToMove, moves, InCheck(p.Board, p.WhiteToMove), s.Last, s.Ending, s.Over);
    }

    /// <summary>The legal moves of the side to move.</summary>
    public static List<ChessMove> LegalMoves(Position p) =>
        PseudoMoves(p).Where(m => !InCheck(Apply(p, m).Board, p.WhiteToMove)).ToList();

    private static IEnumerable<ChessMove> PseudoMoves(Position p)
    {
        var white = p.WhiteToMove;
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                var piece = p.Board[y][x];
                if (piece == '.' || char.IsUpper(piece) != white)
                {
                    continue;
                }

                var from = new Cell(x, y);
                switch (char.ToUpperInvariant(piece))
                {
                    case 'P':
                        foreach (var m in PawnMoves(p, from, white))
                        {
                            yield return m;
                        }

                        break;
                    case 'N':
                        foreach (var to in Jumps(p.Board, from, white, Knight))
                        {
                            yield return new ChessMove(from, to);
                        }

                        break;
                    case 'B':
                    case 'R':
                    case 'Q':
                        var directions = char.ToUpperInvariant(piece) switch { 'B' => Diagonal, 'R' => Straight, _ => [.. Straight, .. Diagonal] };
                        foreach (var to in Slides(p.Board, from, white, directions))
                        {
                            yield return new ChessMove(from, to);
                        }

                        break;
                    case 'K':
                        foreach (var to in Jumps(p.Board, from, white, [.. Straight, .. Diagonal]))
                        {
                            yield return new ChessMove(from, to);
                        }

                        foreach (var m in Castlings(p, from, white))
                        {
                            yield return m;
                        }

                        break;
                }
            }
        }
    }

    private static IEnumerable<ChessMove> PawnMoves(Position p, Cell from, bool white)
    {
        var dy = white ? -1 : 1;
        var startRow = white ? 6 : 1;
        var lastRow = white ? 0 : 7;
        var targets = new List<Cell>();
        if (Empty(p.Board, from.X, from.Y + dy))
        {
            targets.Add(new Cell(from.X, from.Y + dy));
            if (from.Y == startRow && Empty(p.Board, from.X, from.Y + 2 * dy))
            {
                targets.Add(new Cell(from.X, from.Y + 2 * dy));
            }
        }

        foreach (var dx in new[] { -1, 1 })
        {
            var to = new Cell(from.X + dx, from.Y + dy);
            if (OnBoard(to) && (Enemy(p.Board, to, white) || p.EnPassant == to))
            {
                targets.Add(to);
            }
        }

        foreach (var to in targets)
        {
            if (to.Y == lastRow)
            {
                foreach (var promotion in Promotions)
                {
                    yield return new ChessMove(from, to, promotion);
                }
            }
            else
            {
                yield return new ChessMove(from, to);
            }
        }
    }

    private static IEnumerable<ChessMove> Castlings(Position p, Cell from, bool white)
    {
        var row = white ? 7 : 0;
        if (from != new Cell(4, row) || InCheck(p.Board, white))
        {
            yield break;
        }

        // King side: f and g empty and not attacked; queen side: b, c and d empty, c and d not attacked
        if (p.Castling.Contains(white ? 'K' : 'k') && Empty(p.Board, 5, row) && Empty(p.Board, 6, row)
            && !Attacked(p.Board, new Cell(5, row), !white) && !Attacked(p.Board, new Cell(6, row), !white))
        {
            yield return new ChessMove(from, new Cell(6, row));
        }

        if (p.Castling.Contains(white ? 'Q' : 'q') && Empty(p.Board, 1, row) && Empty(p.Board, 2, row) && Empty(p.Board, 3, row)
            && !Attacked(p.Board, new Cell(3, row), !white) && !Attacked(p.Board, new Cell(2, row), !white))
        {
            yield return new ChessMove(from, new Cell(2, row));
        }
    }

    /// <summary>The position after a move (assumed to be pseudo-legal).</summary>
    private static Position Apply(Position p, ChessMove m)
    {
        var board = p.Board.Select(r => r.ToCharArray()).ToArray();
        var piece = board[m.From.Y][m.From.X];
        var kind = char.ToUpperInvariant(piece);
        var white = char.IsUpper(piece);
        var capture = board[m.To.Y][m.To.X] != '.';

        board[m.From.Y][m.From.X] = '.';
        if (kind == 'P' && m.To == p.EnPassant)
        {
            board[m.From.Y][m.To.X] = '.';
            capture = true;
        }

        board[m.To.Y][m.To.X] = m.Promotion is { } promotion ? (white ? promotion[0] : char.ToLowerInvariant(promotion[0])) : piece;

        if (kind == 'K' && Math.Abs(m.To.X - m.From.X) == 2)
        {
            var (rookFrom, rookTo) = m.To.X == 6 ? (7, 5) : (0, 3);
            board[m.To.Y][rookTo] = board[m.To.Y][rookFrom];
            board[m.To.Y][rookFrom] = '.';
        }

        var castling = p.Castling;
        foreach (var (square, rights) in new[] { (new Cell(4, 7), "KQ"), (new Cell(7, 7), "K"), (new Cell(0, 7), "Q"), (new Cell(4, 0), "kq"), (new Cell(7, 0), "k"), (new Cell(0, 0), "q") })
        {
            if (m.From == square || m.To == square)
            {
                castling = string.Concat(castling.Where(c => !rights.Contains(c)));
            }
        }

        var enPassant = kind == 'P' && Math.Abs(m.To.Y - m.From.Y) == 2 ? new Cell(m.From.X, (m.From.Y + m.To.Y) / 2) : null;
        var halfMoves = kind == 'P' || capture ? 0 : p.HalfMoves + 1;
        return new Position(board.Select(r => new string(r)).ToList(), !p.WhiteToMove, castling, enPassant, halfMoves);
    }

    private static bool InCheck(IReadOnlyList<string> board, bool white)
    {
        var king = white ? 'K' : 'k';
        for (var y = 0; y < 8; y++)
        {
            var x = board[y].IndexOf(king);
            if (x >= 0)
            {
                return Attacked(board, new Cell(x, y), !white);
            }
        }

        return false;
    }

    /// <summary>Whether a piece of the given side attacks the square.</summary>
    private static bool Attacked(IReadOnlyList<string> board, Cell square, bool byWhite)
    {
        char Own(char piece) => byWhite ? piece : char.ToLowerInvariant(piece);

        // A pawn attacks diagonally forward, so it stands diagonally behind the square
        var pawnRow = square.Y + (byWhite ? 1 : -1);
        if (new[] { square.X - 1, square.X + 1 }.Any(x => At(board, x, pawnRow) == Own('P')))
        {
            return true;
        }

        if (Knight.Any(d => At(board, square.X + d.Dx, square.Y + d.Dy) == Own('N'))
            || Straight.Concat(Diagonal).Any(d => At(board, square.X + d.Dx, square.Y + d.Dy) == Own('K')))
        {
            return true;
        }

        return Straight.Any(d => FirstPiece(board, square, d) is var c && (c == Own('R') || c == Own('Q')))
            || Diagonal.Any(d => FirstPiece(board, square, d) is var c && (c == Own('B') || c == Own('Q')));
    }

    private static char FirstPiece(IReadOnlyList<string> board, Cell from, (int Dx, int Dy) d)
    {
        for (var (x, y) = (from.X + d.Dx, from.Y + d.Dy); x is >= 0 and < 8 && y is >= 0 and < 8; x += d.Dx, y += d.Dy)
        {
            if (board[y][x] != '.')
            {
                return board[y][x];
            }
        }

        return '.';
    }

    private static IEnumerable<Cell> Jumps(IReadOnlyList<string> board, Cell from, bool white, (int Dx, int Dy)[] steps) =>
        steps.Select(d => new Cell(from.X + d.Dx, from.Y + d.Dy))
            .Where(to => OnBoard(to) && (board[to.Y][to.X] == '.' || Enemy(board, to, white)));

    private static IEnumerable<Cell> Slides(IReadOnlyList<string> board, Cell from, bool white, (int Dx, int Dy)[] directions)
    {
        foreach (var (dx, dy) in directions)
        {
            for (var to = new Cell(from.X + dx, from.Y + dy); OnBoard(to); to = new Cell(to.X + dx, to.Y + dy))
            {
                if (board[to.Y][to.X] == '.')
                {
                    yield return to;
                    continue;
                }

                if (Enemy(board, to, white))
                {
                    yield return to;
                }

                break;
            }
        }
    }

    /// <summary>Only kings, or a king with a single bishop or knight against a lone king: nobody can mate.</summary>
    private static bool TooLittleMaterial(IReadOnlyList<string> board)
    {
        var pieces = string.Concat(board).Where(c => c != '.' && char.ToUpperInvariant(c) != 'K').ToList();
        return pieces.Count == 0 || (pieces.Count == 1 && char.ToUpperInvariant(pieces[0]) is 'B' or 'N');
    }

    private static bool OnBoard(Cell c) => c.X is >= 0 and < 8 && c.Y is >= 0 and < 8;

    private static char At(IReadOnlyList<string> board, int x, int y) => x is >= 0 and < 8 && y is >= 0 and < 8 ? board[y][x] : '.';

    private static bool Empty(IReadOnlyList<string> board, int x, int y) => x is >= 0 and < 8 && y is >= 0 and < 8 && board[y][x] == '.';

    private static bool Enemy(IReadOnlyList<string> board, Cell c, bool white) => board[c.Y][c.X] != '.' && char.IsUpper(board[c.Y][c.X]) != white;

    private static State Load(string state) => JsonSerializer.Deserialize<State>(state, Json)!;

    private static string Save(State state) => JsonSerializer.Serialize(state, Json);

    /// <summary>
    /// Everything that decides the next moves. <c>EnPassant</c> is the square a pawn just skipped; <c>HalfMoves</c>
    /// counts moves since the last capture or pawn move.
    /// </summary>
    public sealed record Position(IReadOnlyList<string> Board, bool WhiteToMove, string Castling, Cell? EnPassant, int HalfMoves)
    {
        /// <summary>Equal for positions that count as the same for a repetition.</summary>
        public string Key => $"{string.Concat(Board)} {WhiteToMove} {Castling} {EnPassant}";
    }

    private sealed record State(Position Position, int WhiteSeat, IReadOnlyList<string> History, ChessMove? Last, string? Ending, bool Over);
}
