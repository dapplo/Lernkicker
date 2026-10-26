using Lernkicker.Domain.Multiplayer;

namespace Lernkicker.Domain.Tests;

public class PlaySettingsTests
{
    private static readonly Guid FamilyA = Guid.NewGuid(), FamilyB = Guid.NewGuid();

    private static PlaySettings Child(Guid family, string reach = PlayReach.Family, Guid[]? families = null, Guid[]? children = null, params string[] games)
    {
        var settings = new PlaySettings(family, Guid.NewGuid());
        settings.Update(games.Length == 0 ? [MultiplayerGames.Battleship] : games, reach, families ?? [], children ?? []);
        return settings;
    }

    [Fact]
    public void Brothers_and_sisters_always_meet()
    {
        Assert.True(Child(FamilyA).CanPlayWith(Child(FamilyA), MultiplayerGames.Battleship));
    }

    [Fact]
    public void Nothing_works_without_the_game()
    {
        var locked = PlaySettings.Locked(FamilyA, Guid.NewGuid());
        Assert.False(locked.CanPlayWith(Child(FamilyA), MultiplayerGames.Battleship));
        Assert.False(Child(FamilyA).CanPlayWith(locked, MultiplayerGames.Battleship));
    }

    [Fact]
    public void Both_families_have_to_agree()
    {
        var open = Child(FamilyA, PlayReach.Everyone);
        var closed = Child(FamilyB);
        Assert.False(open.CanPlayWith(closed, MultiplayerGames.Battleship));
        Assert.False(closed.CanPlayWith(open, MultiplayerGames.Battleship));

        var everyone = Child(FamilyB, PlayReach.Everyone);
        Assert.True(open.CanPlayWith(everyone, MultiplayerGames.Battleship));
        Assert.True(everyone.CanPlayWith(open, MultiplayerGames.Battleship));
    }

    [Fact]
    public void Picked_families_and_children_count_only_when_selected()
    {
        var calvin = Child(FamilyA, PlayReach.Everyone);
        var lena = Child(FamilyB, PlayReach.Selected, families: [FamilyA]);
        Assert.True(lena.CanPlayWith(calvin, MultiplayerGames.Battleship));

        var max = Child(FamilyB, PlayReach.Selected, children: [calvin.ChildId]);
        Assert.True(max.CanPlayWith(calvin, MultiplayerGames.Battleship));
        Assert.False(max.CanPlayWith(Child(FamilyA, PlayReach.Everyone), MultiplayerGames.Battleship));

        max.Update(max.Games, PlayReach.Family, max.Families, max.Children);
        Assert.False(max.CanPlayWith(calvin, MultiplayerGames.Battleship));
        Assert.Equal([calvin.ChildId], max.Children);
    }

    [Fact]
    public void Unknown_games_and_reaches_are_refused()
    {
        Assert.False(PlaySettings.IsValid(["go"], PlayReach.Family, [], []));
        Assert.False(PlaySettings.IsValid([MultiplayerGames.Battleship], "world", [], []));
        Assert.True(PlaySettings.IsValid([MultiplayerGames.Battleship], PlayReach.Selected, [FamilyB], []));
    }
}

public class BattleshipTests
{
    private static readonly Battleship Rules = new();
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Every ship in its own row, from the left edge, with a free row between them.</summary>
    private static readonly IReadOnlyList<Ship> Rows =
    [
        new(0, 0, 5, true), new(0, 2, 4, true), new(0, 4, 3, true), new(0, 6, 3, true), new(0, 8, 2, true),
    ];

    [Fact]
    public void Random_fleets_follow_the_rules()
    {
        var random = new Random(7);
        for (var i = 0; i < 200; i++)
        {
            Assert.True(Battleship.IsValidFleet(Battleship.RandomFleet(random)));
        }
    }

    [Fact]
    public void Ships_must_be_complete_on_the_sea_and_not_touch()
    {
        Assert.True(Battleship.IsValidFleet(Rows));
        Assert.False(Battleship.IsValidFleet(Rows.Take(4).ToList()));
        Assert.False(Battleship.IsValidFleet([.. Rows.Take(4), new Ship(9, 8, 2, true)]));
        Assert.False(Battleship.IsValidFleet([.. Rows.Take(4), new Ship(0, 7, 2, true)]));
        // Corners count as touching
        Assert.False(Battleship.IsValidFleet([.. Rows.Take(4), new Ship(3, 7, 2, false)]));
        Assert.True(Battleship.IsValidFleet([.. Rows.Take(4), new Ship(4, 8, 2, true)]));
    }

    [Fact]
    public void Nobody_sees_the_other_fleet_until_it_sinks()
    {
        var state = Start(out var first);
        state = Next(Rules.Play(state, first, new BattleshipShot(0, 0)));

        var mine = (BattleshipView)Rules.View(state, first);
        var theirs = (BattleshipView)Rules.View(state, 1 - first);
        Assert.Equal(Rows, mine.MyShips);
        Assert.Empty(mine.SunkShips);
        Assert.Equal([new Shot(0, 0, true)], mine.MyShots);
        Assert.Equal([new Shot(0, 0, true)], theirs.TheirShots);
    }

    [Fact]
    public void Placing_happens_once_and_both_place_before_shooting()
    {
        var state = Rules.Start(2, new Random(1));
        Assert.Equal([0, 1], Rules.ToMove(state));
        Assert.Equal(GameErrors.NotYourTurn, Rules.Play(state, 0, new BattleshipShot(0, 0)).Error);
        Assert.Equal(GameErrors.FleetInvalid, Rules.Play(state, 0, new BattleshipPlacement(Rows.Take(3).ToList())).Error);

        state = Next(Rules.Play(state, 0, new BattleshipPlacement(Rows)));
        Assert.Equal([1], Rules.ToMove(state));
        Assert.True(((BattleshipView)Rules.View(state, 1)).OpponentPlaced);
        Assert.Equal(GameErrors.NotYourTurn, Rules.Play(state, 0, new BattleshipPlacement(Rows)).Error);
    }

    [Fact]
    public void A_hit_shoots_again_a_miss_passes_the_turn()
    {
        var state = Start(out var first);
        var second = 1 - first;

        state = Next(Rules.Play(state, first, new BattleshipShot(0, 0)));
        Assert.Equal([first], Rules.ToMove(state));
        Assert.Equal(GameErrors.ShotInvalid, Rules.Play(state, first, new BattleshipShot(0, 0)).Error);
        Assert.Equal(GameErrors.ShotInvalid, Rules.Play(state, first, new BattleshipShot(10, 0)).Error);

        state = Next(Rules.Play(state, first, new BattleshipShot(9, 9)));
        Assert.Equal([second], Rules.ToMove(state));
        Assert.Equal(GameErrors.NotYourTurn, Rules.Play(state, first, new BattleshipShot(5, 5)).Error);
    }

    [Fact]
    public void Sinking_the_whole_fleet_wins()
    {
        var state = Start(out var first);
        var cells = Rows.SelectMany(s => s.Cells()).ToList();
        GameStep step = GameStep.Next(state);
        foreach (var (x, y) in cells)
        {
            step = Rules.Play(step.State!, first, new BattleshipShot(x, y));
            Assert.True(step.IsValid);
        }

        Assert.Equal(first, step.Winner);
        Assert.Empty(Rules.ToMove(step.State!));
        // At the end, the loser sees where the winner's ships were
        Assert.Equal(Rows, ((BattleshipView)Rules.View(step.State!, 1 - first)).SunkShips);
    }

    [Fact]
    public void Giving_up_lets_the_other_win()
    {
        var step = Rules.Resign(Start(out _), 0);
        Assert.Equal(1, step.Winner);
        Assert.Equal(BattleshipPhases.Over, ((BattleshipView)Rules.View(step.State!, 0)).Phase);
    }

    [Fact]
    public void A_table_starts_when_the_last_seat_is_taken()
    {
        var host = Guid.NewGuid();
        var match = Match.Open(Rules, 2, host, Guid.NewGuid(), Now);
        Assert.Equal(MatchStatus.Waiting, match.Status);
        Assert.False(match.Leave(host, Now));

        match.Join(Rules, Guid.NewGuid(), Guid.NewGuid(), new Random(1), Now);
        Assert.Equal(MatchStatus.Playing, match.Status);
        Assert.NotNull(match.State);
        Assert.Equal([0, 1], match.ToMove(Rules));
        Assert.Throws<InvalidOperationException>(() => match.Join(Rules, Guid.NewGuid(), Guid.NewGuid(), new Random(1), Now));

        match.Apply(Rules.Resign(match.State!, 1), Now, resignedSeat: 1);
        Assert.Equal(MatchStatus.Finished, match.Status);
        Assert.Equal(0, match.Winner);
        Assert.Equal(1, match.Resigned);
        Assert.Empty(match.ToMove(Rules));
    }

    private static string Start(out int first)
    {
        var state = Rules.Start(2, new Random(3));
        state = Next(Rules.Play(state, 0, new BattleshipPlacement(Rows)));
        state = Next(Rules.Play(state, 1, new BattleshipPlacement(Rows)));
        first = Rules.ToMove(state).Single();
        return state;
    }

    private static string Next(GameStep step)
    {
        Assert.True(step.IsValid, step.Error);
        return step.State!;
    }
}
