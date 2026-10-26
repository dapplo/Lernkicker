namespace Lernkicker.Domain.Multiplayer;

/// <summary>
/// The rules of one game for two or more players. The state is the game's own JSON, kept by the match; every move is
/// checked here on the server, and each player only gets their own <see cref="GameView"/> (no peeking at hidden
/// ships or cards).
/// </summary>
public interface IGameRules
{
    string Game { get; }

    int MinPlayers { get; }

    int MaxPlayers { get; }

    /// <summary>A new game for the seats 0 to <paramref name="players"/> - 1.</summary>
    string Start(int players, Random random);

    GameStep Play(string state, int seat, GameMove move);

    /// <summary>The player at <paramref name="seat"/> gives up.</summary>
    GameStep Resign(string state, int seat);

    /// <summary>The seats that are expected to move now (all of them while setting up).</summary>
    IReadOnlyList<int> ToMove(string state);

    GameView View(string state, int seat);
}

/// <summary>A player's move; each game has its own kinds.</summary>
public abstract record GameMove;

/// <summary>What one player sees of the game.</summary>
public abstract record GameView;

/// <summary>
/// The outcome of a move: the new state, or why it was refused. <c>Over</c> ends the game, won by <c>Winner</c> or
/// drawn when that is null.
/// </summary>
public sealed record GameStep(string? State, string? Error = null, int? Winner = null, bool Over = false)
{
    public bool IsValid => Error is null;

    public static GameStep Refused(string error) => new(null, error);

    public static GameStep Next(string state) => new(state);

    public static GameStep Won(string state, int winner) => new(state, Winner: winner, Over: true);

    /// <summary>Nobody wins: a full board, or no way left to win.</summary>
    public static GameStep Drawn(string state) => new(state, Over: true);
}

/// <summary>Why a move was refused. Codes the UI translates (see Application's ErrorCodes).</summary>
public static class GameErrors
{
    public const string NotYourTurn = "play.notYourTurn";
    public const string MoveInvalid = "play.moveInvalid";
    public const string FleetInvalid = "battleship.fleetInvalid";
    public const string ShotInvalid = "battleship.shotInvalid";
    public const string ColumnFull = "connectFour.columnFull";
    public const string MustCapture = "checkers.mustCapture";
    public const string CardNotPlayable = "mauMau.cardNotPlayable";
    public const string MustDraw = "mauMau.mustDraw";
    public const string ChessMoveInvalid = "chess.moveInvalid";
}

public static class MatchStatus
{
    /// <summary>An open table in the lobby, waiting for players.</summary>
    public const string Waiting = "waiting";

    public const string Playing = "playing";

    public const string Finished = "finished";
}

/// <summary>A child at a table. Players come from different families, so a match belongs to no single family.</summary>
public sealed class MatchPlayer
{
    public Guid MatchId { get; private set; }
    public int Seat { get; private set; }
    public Guid ChildId { get; private set; }
    public Guid FamilyId { get; private set; }

    public MatchPlayer(Guid matchId, int seat, Guid childId, Guid familyId)
    {
        MatchId = matchId;
        Seat = seat;
        ChildId = childId;
        FamilyId = familyId;
    }

    internal void MoveTo(int seat) => Seat = seat;
}

/// <summary>
/// One game between children, from the open table in the lobby to the end. Turn-based: the state is stored after
/// every move, so a game survives reloads and restarts, and nobody has to be online at the same time.
/// </summary>
public sealed class Match
{
    /// <summary>Open tables a child may have at once.</summary>
    public const int MaxOpenTables = 3;

    /// <summary>Running games a child may have at once.</summary>
    public const int MaxRunning = 10;

    private readonly List<MatchPlayer> _players = [];

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Game { get; private set; }
    public int Seats { get; private set; }
    public string Status { get; private set; } = MatchStatus.Waiting;

    /// <summary>The game's state (see <see cref="IGameRules"/>); null until all seats are taken.</summary>
    public string? State { get; private set; }

    public int? Winner { get; private set; }

    /// <summary>The seat that gave up, if the game ended that way.</summary>
    public int? Resigned { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<MatchPlayer> Players => _players;

    public Match(string game, int seats, DateTimeOffset createdAt)
    {
        Game = game;
        Seats = seats;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    /// <summary>Opens a table with the host on seat 0.</summary>
    public static Match Open(IGameRules rules, int seats, Guid childId, Guid familyId, DateTimeOffset now)
    {
        var match = new Match(rules.Game, seats, now);
        match._players.Add(new MatchPlayer(match.Id, 0, childId, familyId));
        return match;
    }

    public MatchPlayer Host => _players.MinBy(p => p.Seat)!;

    public bool IsFull => _players.Count >= Seats;

    public int? SeatOf(Guid childId) => _players.FirstOrDefault(p => p.ChildId == childId)?.Seat;

    /// <summary>
    /// A player was deleted (their child profile or family): the game cannot go on. Seats stay numbered, so the
    /// gap shows in the count.
    /// </summary>
    public bool IsAbandoned => Status == MatchStatus.Playing && _players.Count < Seats;

    /// <summary>Takes the next free seat; the last one starts the game.</summary>
    public void Join(IGameRules rules, Guid childId, Guid familyId, Random random, DateTimeOffset now)
    {
        if (Status != MatchStatus.Waiting || IsFull || SeatOf(childId) is not null)
        {
            throw new InvalidOperationException("The table takes no more players.");
        }

        var seat = Enumerable.Range(0, Seats).First(s => _players.All(p => p.Seat != s));
        _players.Add(new MatchPlayer(Id, seat, childId, familyId));
        UpdatedAt = now;
        if (IsFull)
        {
            Begin(rules, random);
        }
    }

    /// <summary>Whether the host may start before every seat is taken (games for more than two).</summary>
    public bool CanStartEarly(IGameRules rules) => Status == MatchStatus.Waiting && _players.Count >= rules.MinPlayers;

    /// <summary>The host starts with the players at the table; the empty seats go away.</summary>
    public void StartEarly(IGameRules rules, Random random, DateTimeOffset now)
    {
        if (!CanStartEarly(rules))
        {
            throw new InvalidOperationException("Not enough players.");
        }

        // The rules number seats without gaps; a player who left before the start leaves one
        var seat = 0;
        foreach (var player in _players.OrderBy(p => p.Seat).ToList())
        {
            player.MoveTo(seat++);
        }

        Seats = _players.Count;
        UpdatedAt = now;
        Begin(rules, random);
    }

    private void Begin(IGameRules rules, Random random)
    {
        State = rules.Start(Seats, random);
        Status = MatchStatus.Playing;
    }

    /// <summary>A player leaves a table that has not started. False for the host: the table closes instead.</summary>
    public bool Leave(Guid childId, DateTimeOffset now)
    {
        if (Status != MatchStatus.Waiting || Host.ChildId == childId)
        {
            return false;
        }

        _players.RemoveAll(p => p.ChildId == childId);
        UpdatedAt = now;
        return true;
    }

    /// <summary>Applies a checked step from the rules.</summary>
    public void Apply(GameStep step, DateTimeOffset now, int? resignedSeat = null)
    {
        if (!step.IsValid || Status != MatchStatus.Playing)
        {
            throw new InvalidOperationException("Only a valid step of a running game.");
        }

        State = step.State;
        UpdatedAt = now;
        if (step.Over)
        {
            Winner = step.Winner;
            Resigned = resignedSeat;
            Status = MatchStatus.Finished;
        }
    }

    /// <summary>The seats whose move it is; nobody once the game is over.</summary>
    public IReadOnlyList<int> ToMove(IGameRules rules) =>
        Status == MatchStatus.Playing && State is not null && !IsAbandoned ? rules.ToMove(State) : [];
}
