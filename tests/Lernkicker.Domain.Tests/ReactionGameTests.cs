using Lernkicker.Domain.Multiplayer;

namespace Lernkicker.Domain.Tests;

public class ReactionGameTests
{
    [Theory]
    [InlineData(ReactionRounds.Moles)]
    [InlineData(ReactionRounds.Balloons)]
    public void A_round_fills_thirty_seconds_and_never_puts_two_moles_in_one_hole(string game)
    {
        var round = ReactionRounds.Round(game, new Random(1));
        Assert.InRange(round.Count, 25, 80);
        Assert.Equal(Enumerable.Range(0, round.Count), round.Select(t => t.Id));
        Assert.All(round, t => Assert.InRange(t.At, 0, ReactionRounds.Length));
        Assert.Contains(round, t => ReactionRounds.Value(t.Kind) < 0);
        Assert.Contains(round, t => ReactionRounds.Value(t.Kind) > 10);
        if (game == ReactionRounds.Moles)
        {
            Assert.All(round, t => Assert.InRange(t.Place, 0, ReactionRounds.Holes - 1));
            foreach (var hole in round.GroupBy(t => t.Place))
            {
                var times = hole.OrderBy(t => t.At).ToList();
                Assert.All(times.Zip(times.Skip(1)), p => Assert.True(p.First.At + p.First.Duration < p.Second.At));
            }
        }
        else
        {
            Assert.All(round, t => Assert.Contains(t.Duration, ReactionRounds.RiseTimes));
            Assert.All(round.Zip(round.Skip(1)), p => Assert.NotEqual(p.First.Place, p.Second.Place));
        }
    }

    [Fact]
    public void Only_taps_at_the_right_time_count_and_points_never_go_below_zero()
    {
        ReactionTarget[] round =
        [
            new(0, 0, 1000, 1000, ReactionKinds.Hedgehog),
            new(1, 1, 1500, 1000, ReactionKinds.Mole),
            new(2, 2, 3000, 800, ReactionKinds.GoldenMole),
            new(3, 3, 5000, 1000, ReactionKinds.Mole),
        ];

        var result = ReactionRounds.Score(round,
        [
            new(0, 1100), // a hedgehog first: still 0, not -15
            new(1, 1600), // +10
            new(1, 1700), // the same mole twice counts once
            new(2, 3500), // +25
            new(3, 9000), // long gone
            new(7, 5100), // no such mole
        ]);
        Assert.Equal(new ReactionResult(35, 2, 1), result);
    }

    [Fact]
    public void Reflex_points_depend_on_the_reaction_and_early_taps_bring_nothing()
    {
        var round = ReactionRounds.Round(ReactionRounds.Reflex, new Random(2));
        Assert.Equal(ReactionRounds.LightRounds, round.Count);
        Assert.All(round, t => Assert.InRange(t.At, 1200, 3600));

        var result = ReactionRounds.Score(round, [new(0, 250), new(1, -1), new(2, 50), new(3, 500), new(4, 5000)]);
        Assert.Equal(new ReactionResult(85 + 60, 2, 1), result);
    }

    [Fact]
    public void Everyone_plays_the_same_round_and_the_most_points_win()
    {
        var rules = new ReactionGame(ReactionRounds.Moles);
        var state = rules.Start(3, new Random(3));
        var round = ((ReactionView)rules.View(state, 0)).Round;
        Assert.Equal(round, ((ReactionView)rules.View(state, 2)).Round);
        Assert.Equal([0, 1, 2], rules.ToMove(state));

        var good = round.Where(t => ReactionRounds.Value(t.Kind) > 0).Select(t => new ReactionHit(t.Id, t.At + 100)).ToList();
        state = rules.Play(state, 1, new ReactionPlay(good)).State!;
        Assert.Equal(GameErrors.NotYourTurn, rules.Play(state, 1, new ReactionPlay([])).Error);

        // Seat 0 has not played yet, so it does not see what seat 1 scored; seat 1 sees its own
        var before = (ReactionView)rules.View(state, 0);
        Assert.True(before.Seats[1].Played);
        Assert.Null(before.Seats[1].Result);
        Assert.NotNull(((ReactionView)rules.View(state, 1)).Seats[1].Result);

        state = rules.Play(state, 0, new ReactionPlay(good.Take(3).ToList())).State!;
        Assert.Equal([2], rules.ToMove(state));
        var end = rules.Play(state, 2, new ReactionPlay([]));
        Assert.True(end.Over);
        Assert.Equal(1, end.Winner);
        Assert.Equal(good.Count, ((ReactionView)rules.View(end.State!, 0)).Seats[1].Result!.Hits);
    }

    [Fact]
    public void The_same_points_are_a_draw_and_the_last_one_left_wins()
    {
        var rules = new ReactionGame(ReactionRounds.Balloons);
        var state = rules.Start(2, new Random(4));
        state = rules.Play(state, 0, new ReactionPlay([])).State!;
        Assert.True(rules.Play(state, 1, new ReactionPlay([])) is { Over: true, Winner: null });

        var three = rules.Start(3, new Random(5));
        three = rules.Resign(three, 0).State!;
        var last = rules.Resign(three, 2);
        Assert.Equal((true, 1), (last.Over, last.Winner));
    }
}
