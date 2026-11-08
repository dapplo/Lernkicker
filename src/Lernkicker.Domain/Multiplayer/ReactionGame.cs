using System.Text.Json;

namespace Lernkicker.Domain.Multiplayer;

/// <summary>A player's round in a reaction game: every tap, in order.</summary>
public sealed record ReactionPlay(IReadOnlyList<ReactionHit> Hits) : GameMove;

/// <summary>
/// A seat as one player sees it: whether it played its round and, once the player has played their own (or the
/// game is over), what it brought. <c>Out</c>: gave up.
/// </summary>
public sealed record ReactionSeat(bool Played, ReactionResult? Result, bool Out);

/// <summary>The round everyone plays, and how far the table is.</summary>
public sealed record ReactionView(string Game, IReadOnlyList<ReactionTarget> Round, IReadOnlyList<ReactionSeat> Seats, bool Over) : GameView;

/// <summary>
/// A reaction game for two to four: everyone plays the same round (<see cref="ReactionRounds"/>), each whenever
/// they like, so nobody has to be online at the same time. When everyone has played, the most points win; the
/// same points at the top are a draw.
/// </summary>
public sealed class ReactionGame(string game) : IGameRules
{
    /// <summary>More taps than this in one round are left out (nobody taps 30 times a second).</summary>
    public const int MaxTaps = 1000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Game => game;

    public int MinPlayers => 2;

    public int MaxPlayers => 4;

    public string Start(int players, Random random) =>
        Save(new State(ReactionRounds.Round(game, random), Enumerable.Repeat<ReactionResult?>(null, players).ToList(), [], false));

    public GameStep Play(string state, int seat, GameMove move)
    {
        var s = Load(state);
        if (s.Over || s.Out.Contains(seat) || s.Results[seat] is not null)
        {
            return GameStep.Refused(GameErrors.NotYourTurn);
        }

        if (move is not ReactionPlay { Hits: { } hits })
        {
            return GameStep.Refused(GameErrors.MoveInvalid);
        }

        var results = s.Results.ToList();
        results[seat] = ReactionRounds.Score(s.Round, hits.Where(h => h is not null).Take(MaxTaps));
        return Settle(s with { Results = results });
    }

    public GameStep Resign(string state, int seat)
    {
        var s = Load(state);
        if (s.Over || s.Out.Contains(seat))
        {
            return GameStep.Refused(GameErrors.MoveInvalid);
        }

        return Settle(s with { Out = [.. s.Out, seat] });
    }

    public IReadOnlyList<int> ToMove(string state)
    {
        var s = Load(state);
        return s.Over ? [] : Active(s).Where(seat => s.Results[seat] is null).ToList();
    }

    public GameView View(string state, int seat)
    {
        var s = Load(state);
        // The others' points only show after the own round, so nobody knows what to beat before
        var reveal = s.Over || s.Results[seat] is not null;
        var seats = s.Results
            .Select((r, i) => new ReactionSeat(r is not null, reveal || i == seat ? r : null, s.Out.Contains(i)))
            .ToList();
        return new ReactionView(game, s.Round, seats, s.Over);
    }

    /// <summary>Ends the game when the last one still in has played, or when only one is left.</summary>
    private static GameStep Settle(State s)
    {
        var active = Active(s).ToList();
        if (active.Count == 1)
        {
            return GameStep.Won(Save(s with { Over = true }), active[0]);
        }

        if (active.Any(seat => s.Results[seat] is null))
        {
            return GameStep.Next(Save(s));
        }

        var over = Save(s with { Over = true });
        var best = active.Max(seat => s.Results[seat]!.Points);
        var top = active.Where(seat => s.Results[seat]!.Points == best).ToList();
        return top.Count == 1 ? GameStep.Won(over, top[0]) : GameStep.Drawn(over);
    }

    private static IEnumerable<int> Active(State s) => Enumerable.Range(0, s.Results.Count).Where(seat => !s.Out.Contains(seat));

    private static State Load(string state) => JsonSerializer.Deserialize<State>(state, Json)!;

    private static string Save(State state) => JsonSerializer.Serialize(state, Json);

    private sealed record State(IReadOnlyList<ReactionTarget> Round, IReadOnlyList<ReactionResult?> Results, IReadOnlyList<int> Out, bool Over);
}
