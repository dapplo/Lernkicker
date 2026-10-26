using System.Text.Json;

namespace Lernkicker.Domain.Multiplayer;

/// <summary>A ship on the 10 × 10 sea: its first cell (top left) and which way it points.</summary>
public sealed record Ship(int X, int Y, int Length, bool Horizontal)
{
    public IEnumerable<(int X, int Y)> Cells() =>
        Enumerable.Range(0, Length).Select(i => Horizontal ? (X + i, Y) : (X, Y + i));

    public bool Covers(int x, int y) => Cells().Contains((x, y));
}

/// <summary>A shot at the other player's sea.</summary>
public sealed record Shot(int X, int Y, bool Hit);

/// <summary>Both players place their whole fleet at once.</summary>
public sealed record BattleshipPlacement(IReadOnlyList<Ship> Ships) : GameMove;

public sealed record BattleshipShot(int X, int Y) : GameMove;

public static class BattleshipPhases
{
    /// <summary>Both players place their ships; neither sees the other's.</summary>
    public const string Placing = "placing";

    public const string Shooting = "shooting";

    public const string Over = "over";
}

/// <summary>
/// One player's view: the own fleet and the shots on it, the own shots on the other sea and the enemy ships already
/// sunk. The rest of the enemy fleet stays hidden until the game is over.
/// </summary>
public sealed record BattleshipView(
    string Phase,
    int Size,
    IReadOnlyList<int> Fleet,
    int Turn,
    IReadOnlyList<Ship>? MyShips,
    bool OpponentPlaced,
    IReadOnlyList<Shot> MyShots,
    IReadOnlyList<Shot> TheirShots,
    IReadOnlyList<Ship> SunkShips) : GameView;

/// <summary>
/// Battleship ("Schiffe versenken") for two: a 10 × 10 sea and a fleet of 5, 4, 3, 3 and 2. Ships lie straight and
/// must not touch, not even at the corners. A hit lets the player shoot again; whoever sinks the whole other fleet
/// first wins.
/// </summary>
public sealed class Battleship : IGameRules
{
    public const int Size = 10;

    public static readonly IReadOnlyList<int> Fleet = [5, 4, 3, 3, 2];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Game => MultiplayerGames.Battleship;

    public int MinPlayers => 2;

    public int MaxPlayers => 2;

    public string Start(int players, Random random) =>
        Save(new State(BattleshipPhases.Placing, random.Next(2), [null, null], [[], []], null));

    public GameStep Play(string state, int seat, GameMove move)
    {
        var s = Load(state);
        return move switch
        {
            BattleshipPlacement placement => Place(s, seat, placement.Ships),
            BattleshipShot shot => Shoot(s, seat, shot.X, shot.Y),
            _ => GameStep.Refused(GameErrors.MoveInvalid),
        };
    }

    public GameStep Resign(string state, int seat)
    {
        var s = Load(state);
        var winner = 1 - seat;
        return GameStep.Won(Save(s with { Phase = BattleshipPhases.Over, Winner = winner }), winner);
    }

    public IReadOnlyList<int> ToMove(string state)
    {
        var s = Load(state);
        return s.Phase switch
        {
            BattleshipPhases.Placing => Enumerable.Range(0, 2).Where(seat => s.Fleets[seat] is null).ToList(),
            BattleshipPhases.Shooting => [s.Turn],
            _ => [],
        };
    }

    public GameView View(string state, int seat)
    {
        var s = Load(state);
        var other = 1 - seat;
        var theirFleet = s.Fleets[other] ?? [];
        var sunk = s.Phase == BattleshipPhases.Over
            ? theirFleet
            : theirFleet.Where(ship => IsSunk(ship, s.Shots[seat])).ToList();
        return new BattleshipView(
            s.Phase, Size, Fleet, s.Turn, s.Fleets[seat], s.Fleets[other] is not null,
            s.Shots[seat], s.Shots[other], sunk);
    }

    /// <summary>A fleet placed at random, for the "mix" button.</summary>
    public static IReadOnlyList<Ship> RandomFleet(Random random)
    {
        while (true)
        {
            var ships = new List<Ship>();
            foreach (var length in Fleet)
            {
                for (var tries = 0; tries < 100; tries++)
                {
                    var horizontal = random.Next(2) == 0;
                    var ship = new Ship(
                        random.Next(horizontal ? Size - length + 1 : Size),
                        random.Next(horizontal ? Size : Size - length + 1),
                        length, horizontal);
                    if (ships.All(placed => !Touches(placed, ship)))
                    {
                        ships.Add(ship);
                        break;
                    }
                }
            }

            if (ships.Count == Fleet.Count)
            {
                return ships;
            }
        }
    }

    /// <summary>The right ships, all on the sea, none touching another.</summary>
    public static bool IsValidFleet(IReadOnlyList<Ship>? ships) =>
        ships is not null
        && ships.Select(s => s.Length).Order().SequenceEqual(Fleet.Order())
        && ships.All(s => s.Cells().All(c => c.X is >= 0 and < Size && c.Y is >= 0 and < Size))
        && ships.SelectMany((a, i) => ships.Skip(i + 1).Select(b => (a, b))).All(pair => !Touches(pair.a, pair.b));

    private static bool Touches(Ship a, Ship b) =>
        a.Cells().Any(ca => b.Cells().Any(cb => Math.Abs(ca.X - cb.X) <= 1 && Math.Abs(ca.Y - cb.Y) <= 1));

    private static bool IsSunk(Ship ship, IReadOnlyList<Shot> shots) =>
        ship.Cells().All(c => shots.Any(s => s.X == c.X && s.Y == c.Y));

    private static GameStep Place(State s, int seat, IReadOnlyList<Ship> ships)
    {
        if (s.Phase != BattleshipPhases.Placing || s.Fleets[seat] is not null)
        {
            return GameStep.Refused(GameErrors.NotYourTurn);
        }

        if (!IsValidFleet(ships))
        {
            return GameStep.Refused(GameErrors.FleetInvalid);
        }

        var fleets = s.Fleets.ToList();
        fleets[seat] = ships.ToList();
        var phase = fleets.All(f => f is not null) ? BattleshipPhases.Shooting : BattleshipPhases.Placing;
        return GameStep.Next(Save(s with { Phase = phase, Fleets = fleets }));
    }

    private static GameStep Shoot(State s, int seat, int x, int y)
    {
        if (s.Phase != BattleshipPhases.Shooting || s.Turn != seat)
        {
            return GameStep.Refused(GameErrors.NotYourTurn);
        }

        if (x is < 0 or >= Size || y is < 0 or >= Size || s.Shots[seat].Any(shot => shot.X == x && shot.Y == y))
        {
            return GameStep.Refused(GameErrors.ShotInvalid);
        }

        var other = 1 - seat;
        var enemy = s.Fleets[other]!;
        var hit = enemy.Any(ship => ship.Covers(x, y));
        var shots = s.Shots.Select(list => list.ToList()).ToList();
        shots[seat].Add(new Shot(x, y, hit));

        if (enemy.All(ship => IsSunk(ship, shots[seat])))
        {
            return GameStep.Won(Save(s with { Phase = BattleshipPhases.Over, Shots = shots, Winner = seat }), seat);
        }

        // A hit earns another shot; a miss passes the turn
        return GameStep.Next(Save(s with { Shots = shots, Turn = hit ? seat : other }));
    }

    private static State Load(string state) => JsonSerializer.Deserialize<State>(state, Json)!;

    private static string Save(State state) => JsonSerializer.Serialize(state, Json);

    private sealed record State(string Phase, int Turn, IReadOnlyList<List<Ship>?> Fleets, IReadOnlyList<List<Shot>> Shots, int? Winner);
}
