// Kept free of the match types, so the client links this file for playing alone (see Lernkicker.Client.csproj). There
// it gets a namespace of its own: the web project references both, and the same type twice would clash.
#if LINKED_IN_CLIENT
namespace Lernkicker.Client.Reaction;
#else
namespace Lernkicker.Domain.Multiplayer;
#endif

/// <summary>
/// Something to tap in a reaction game: <c>Place</c> is the hole (moles, 0 to 8) or the lane (balloons, 0 to 5);
/// it shows from <c>At</c> for <c>Duration</c> milliseconds after the round starts. In the reflex game <c>At</c> is
/// how long the light stays red and <c>Duration</c> the longest reaction that still counts. <c>Kind</c> says what it is
/// (<see cref="ReactionKinds"/>).
/// </summary>
public sealed record ReactionTarget(int Id, int Place, int At, int Duration, string Kind);

/// <summary>
/// A tap on target <c>Id</c>, <c>At</c> milliseconds after the round started. In the reflex game <c>At</c> is the
/// reaction time after the light turned green, or -1 for tapping too early.
/// </summary>
public sealed record ReactionHit(int Id, int At);

/// <summary>What a round brought: the points, the good taps and the wrong ones (a hedgehog, a black balloon, too early).</summary>
public sealed record ReactionResult(int Points, int Hits, int Wrong);

public static class ReactionKinds
{
    public const string Mole = "mole";
    public const string GoldenMole = "golden";
    public const string Hedgehog = "hedgehog";
    public const string Balloon = "balloon";
    public const string GoldenBalloon = "goldenBalloon";
    public const string BlackBalloon = "blackBalloon";
    public const string Light = "light";
}

/// <summary>
/// The rounds of the reaction games and how they score. Everyone at a table plays the same round, so the luck of the
/// draw is the same for all; the points are worked out here from the taps, never taken from the device.
/// </summary>
public static class ReactionRounds
{
    public const string Moles = "moles";
    public const string Balloons = "balloons";
    public const string Reflex = "reflex";

    public static readonly IReadOnlyList<string> Games = [Moles, Balloons, Reflex];

    /// <summary>How long the moles and balloons rounds last.</summary>
    public const int Length = 30_000;

    public const int Holes = 9;

    public const int Lanes = 6;

    /// <summary>The rise times of the balloons (the client has one CSS speed per value).</summary>
    public static readonly int[] RiseTimes = [3200, 3800, 4400, 5000];

    public const int LightRounds = 6;

    /// <summary>The longest reaction that still counts.</summary>
    public const int SlowestReaction = 1500;

    /// <summary>Faster than this nobody reacts; it was a lucky guess.</summary>
    public const int FastestReaction = 100;

    /// <summary>Taps a little before or after a target's time still count (the screen and the finger are not exact).</summary>
    public const int Slack = 200;

    /// <summary>A new round for the game; the targets come in the order they show up.</summary>
    public static IReadOnlyList<ReactionTarget> Round(string game, Random random) => game switch
    {
        Moles => MoleRound(random),
        Balloons => BalloonRound(random),
        Reflex => LightRound(random),
        _ => throw new ArgumentOutOfRangeException(nameof(game)),
    };

    /// <summary>
    /// Scores the taps. Taps on targets that do not exist, at times the target was not there, or a second tap on the
    /// same target count for nothing. The points never go below zero, not even for a moment.
    /// </summary>
    public static ReactionResult Score(IReadOnlyList<ReactionTarget> round, IEnumerable<ReactionHit> hits)
    {
        var targets = round.ToDictionary(t => t.Id);
        var seen = new HashSet<int>();
        var (points, good, wrong) = (0, 0, 0);
        foreach (var hit in hits)
        {
            if (!targets.TryGetValue(hit.Id, out var target) || !seen.Add(hit.Id))
            {
                continue;
            }

            if (target.Kind == ReactionKinds.Light)
            {
                if (hit.At < 0)
                {
                    wrong++;
                }
                else if (hit.At is >= FastestReaction and <= SlowestReaction)
                {
                    good++;
                    points += LightPoints(hit.At);
                }

                continue;
            }

            if (hit.At < target.At - Slack || hit.At > target.At + target.Duration + Slack)
            {
                continue;
            }

            // Like the counter on the screen: a wrong tap never takes it below zero
            var value = Value(target.Kind);
            points = Math.Max(0, points + value);
            if (value > 0)
            {
                good++;
            }
            else
            {
                wrong++;
            }
        }

        return new ReactionResult(points, good, wrong);
    }

    /// <summary>What a tap on a target is worth; hedgehogs and black balloons cost points.</summary>
    public static int Value(string kind) => kind switch
    {
        ReactionKinds.Mole or ReactionKinds.Balloon => 10,
        ReactionKinds.GoldenMole or ReactionKinds.GoldenBalloon => 25,
        ReactionKinds.Hedgehog or ReactionKinds.BlackBalloon => -15,
        _ => 0,
    };

    /// <summary>A quick reaction is worth up to 100 points: 250 ms bring 85, 500 ms 60, a second 10.</summary>
    public static int LightPoints(int milliseconds) => Math.Clamp((1100 - milliseconds) / 10, 0, 100);

    private static List<ReactionTarget> MoleRound(Random random)
    {
        var targets = new List<ReactionTarget>();
        var free = new int[Holes];
        for (var at = 800; at < Length - 900;)
        {
            // Faster and shorter towards the end
            var progress = (double)at / Length;
            var stay = (int)(1400 - 650 * progress);
            var holes = Enumerable.Range(0, Holes).Where(h => free[h] <= at).ToList();
            if (holes.Count > 0)
            {
                var hole = holes[random.Next(holes.Count)];
                var roll = random.Next(100);
                var kind = roll < 8 ? ReactionKinds.GoldenMole : roll < 22 ? ReactionKinds.Hedgehog : ReactionKinds.Mole;
                var duration = Math.Min(kind == ReactionKinds.GoldenMole ? stay * 2 / 3 : stay, Length - at);
                targets.Add(new ReactionTarget(targets.Count, hole, at, duration, kind));
                // A short pause before the same hole is used again, so a new mole is never the old one
                free[hole] = at + duration + 300;
            }

            at += (int)(1050 - 500 * progress) + random.Next(-150, 151);
        }

        return targets;
    }

    private static List<ReactionTarget> BalloonRound(Random random)
    {
        var targets = new List<ReactionTarget>();
        var lastLane = -1;
        for (var at = 400; at < Length - 1500;)
        {
            var progress = (double)at / Length;
            var lane = random.Next(Lanes - 1);
            // Never twice in the same lane in a row, so balloons do not hide each other
            lane = lane >= lastLane && lastLane >= 0 ? lane + 1 : lane;
            lastLane = lane;
            var roll = random.Next(100);
            var kind = roll < 8 ? ReactionKinds.GoldenBalloon : roll < 20 ? ReactionKinds.BlackBalloon : ReactionKinds.Balloon;
            // Slower at first; golden balloons are always the fastest
            var speed = kind == ReactionKinds.GoldenBalloon
                ? 0
                : Math.Clamp(RiseTimes.Length - 1 - (int)(progress * RiseTimes.Length) + random.Next(-1, 1), 0, RiseTimes.Length - 1);
            targets.Add(new ReactionTarget(targets.Count, lane, at, RiseTimes[speed], kind));
            at += (int)(750 - 330 * progress) + random.Next(-120, 121);
        }

        return targets;
    }

    private static List<ReactionTarget> LightRound(Random random) =>
        Enumerable.Range(0, LightRounds)
            .Select(i => new ReactionTarget(i, 0, 1200 + random.Next(2400), SlowestReaction, ReactionKinds.Light))
            .ToList();
}
