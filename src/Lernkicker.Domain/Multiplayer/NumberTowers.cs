using System.Text.Json;

namespace Lernkicker.Domain.Multiplayer;

/// <summary>
/// Puts a card on one of the four towers in the middle (<c>Tower</c> 0 to 3). <c>From</c>: "stock" (the top of the
/// own stock), "hand" (the hand card <c>Card</c>, 1 to 12 or 0 for a star) or "discard" (the top of the own discard
/// pile <c>Pile</c>, 0 to 3).
/// </summary>
public sealed record NumberTowersPlay(string From, int Tower, int Card = 0, int Pile = 0) : GameMove;

/// <summary>Ends the turn by putting the hand card <c>Card</c> on the own discard pile <c>Pile</c> (0 to 3).</summary>
public sealed record NumberTowersDiscard(int Card, int Pile) : GameMove;

/// <summary>Ends the turn without a card to discard (only when the hand is empty and nothing is left to draw).</summary>
public sealed record NumberTowersPass : GameMove;

/// <summary>
/// What happened last, for the others to see. <c>Kind</c>: "play", "discard", "pass" or "resign"; <c>Done</c>: the
/// card finished its tower at 12.
/// </summary>
public sealed record NumberTowersAction(int Seat, string Kind, int Card = 0, string? From = null, int Tower = 0, int Pile = 0, bool Done = false);

/// <summary>
/// One player at the table as everyone sees them: the top of the stock and how many cards are left in it, how many
/// cards are in the hand, and the four discard piles (open, bottom card first).
/// </summary>
public sealed record NumberTowersSeat(int Seat, int? StockTop, int StockCount, int HandCount, IReadOnlyList<IReadOnlyList<int>> Discards);

/// <summary>
/// The table for one player: the own hand (0 is a star), every player's stock top and discard piles, and the four
/// towers: <c>Towers</c> is the height of each (the number it shows, 0 when empty) and <c>TowerTops</c> the card on
/// top (0 for a star standing in for the number).
/// </summary>
public sealed record NumberTowersView(
    IReadOnlyList<int> Hand, IReadOnlyList<NumberTowersSeat> Seats, IReadOnlyList<int> Towers, IReadOnlyList<int> TowerTops,
    int Turn, int DeckSize, IReadOnlyList<int> Out, NumberTowersAction? Last, bool Over) : GameView;

/// <summary>
/// "Number Towers" for two to four players, a sequence-stacking card game: 144 number cards (1 to 12, twelve of each)
/// and 18 stars that stand in for any number. Everyone gets a face-down stock of <see cref="StockSize"/> cards with
/// the top card open. In the middle there are four towers everyone builds on: a tower starts with a 1, then 2, 3 and
/// so on up to 12; a full tower goes back into the deck. On your turn you fill your hand up to five cards, then put as
/// many cards on the towers as you can or want, from the hand, the top of your stock or the top of one of your four
/// discard piles; if you use up all five hand cards you draw five new ones. You end the turn by putting one hand card
/// on one of your discard piles. Whoever gets rid of the whole stock first wins.
/// </summary>
public sealed class NumberTowers : IGameRules
{
    public const int StockSize = 20;
    public const int HandSize = 5;
    public const int TowerCount = 4;
    public const int PileCount = 4;
    public const int Highest = 12;
    public const int Star = 0;
    public const int Stars = 18;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Game => MultiplayerGames.NumberTowers;

    public int MinPlayers => 2;

    public int MaxPlayers => 4;

    /// <summary>All 162 cards: twelve of each number and the stars.</summary>
    public static IReadOnlyList<int> Deck =>
        Enumerable.Range(1, Highest).SelectMany(n => Enumerable.Repeat(n, Highest)).Concat(Enumerable.Repeat(Star, Stars)).ToList();

    public string Start(int players, Random random)
    {
        var deck = Deck.OrderBy(_ => random.Next()).ToList();
        var stocks = new List<List<int>>();
        for (var p = 0; p < players; p++)
        {
            stocks.Add(deck.GetRange(0, StockSize));
            deck.RemoveRange(0, StockSize);
        }

        var s = new State(
            stocks, Enumerable.Range(0, players).Select(_ => new List<int>()).ToList(),
            Enumerable.Range(0, players).Select(_ => Enumerable.Range(0, PileCount).Select(_ => new List<int>()).ToList()).ToList(),
            Enumerable.Range(0, TowerCount).Select(_ => new List<int>()).ToList(), deck, [], random.Next(players), [], null, false, random.Next());
        Fill(s, s.Turn);
        return Save(s);
    }

    public GameStep Play(string state, int seat, GameMove move)
    {
        var s = Load(state);
        if (s.Over || s.Turn != seat)
        {
            return GameStep.Refused(GameErrors.NotYourTurn);
        }

        return move switch
        {
            NumberTowersPlay play => PlayCard(s, seat, play),
            NumberTowersDiscard discard => Discard(s, seat, discard),
            NumberTowersPass when s.Hands[seat].Count == 0 => GameStep.Next(Save(Advance(s with { Last = new NumberTowersAction(seat, "pass") }))),
            _ => GameStep.Refused(GameErrors.MoveInvalid),
        };
    }

    public GameStep Resign(string state, int seat)
    {
        var s = Load(state);
        if (s.Over || s.Out.Contains(seat))
        {
            return GameStep.Refused(GameErrors.MoveInvalid);
        }

        // The cards go back into the game, so the others do not run out
        s.Done.AddRange(s.Stocks[seat]);
        s.Done.AddRange(s.Hands[seat]);
        s.Done.AddRange(s.Discards[seat].SelectMany(p => p));
        s.Stocks[seat].Clear();
        s.Hands[seat].Clear();
        s.Discards[seat].ForEach(p => p.Clear());
        var next = s with { Out = [.. s.Out, seat], Last = new NumberTowersAction(seat, "resign") };
        var active = Enumerable.Range(0, s.Stocks.Count).Where(p => !next.Out.Contains(p)).ToList();
        if (active.Count == 1)
        {
            return GameStep.Won(Save(next with { Over = true }), active[0]);
        }

        return GameStep.Next(Save(s.Turn == seat ? Advance(next) : next));
    }

    public IReadOnlyList<int> ToMove(string state)
    {
        var s = Load(state);
        return s.Over ? [] : [s.Turn];
    }

    public GameView View(string state, int seat)
    {
        var s = Load(state);
        var seats = Enumerable.Range(0, s.Stocks.Count)
            .Select(p => new NumberTowersSeat(
                p, s.Stocks[p].Count > 0 ? s.Stocks[p][^1] : null, s.Stocks[p].Count, s.Hands[p].Count,
                s.Discards[p].Select(d => (IReadOnlyList<int>)d.ToList()).ToList()))
            .ToList();
        return new NumberTowersView(
            s.Hands[seat].Order().ToList(), seats, s.Towers.Select(t => t.Count).ToList(), s.Towers.Select(t => t.Count > 0 ? t[^1] : Star).ToList(),
            s.Turn, s.Deck.Count, s.Out, s.Last, s.Over);
    }

    /// <summary>Whether the card may go on the tower: the next number, or a star.</summary>
    public static bool Fits(int card, int towerHeight) => card == Star || card == towerHeight + 1;

    private static GameStep PlayCard(State s, int seat, NumberTowersPlay play)
    {
        if (play.Tower is < 0 or >= TowerCount)
        {
            return GameStep.Refused(GameErrors.MoveInvalid);
        }

        var source = play.From switch
        {
            "stock" => s.Stocks[seat],
            "hand" when s.Hands[seat].Contains(play.Card) => s.Hands[seat],
            "discard" when play.Pile is >= 0 and < PileCount => s.Discards[seat][play.Pile],
            _ => null,
        };
        if (source is null || source.Count == 0)
        {
            return GameStep.Refused(GameErrors.MoveInvalid);
        }

        var card = play.From == "hand" ? play.Card : source[^1];
        var tower = s.Towers[play.Tower];
        if (!Fits(card, tower.Count))
        {
            return GameStep.Refused(GameErrors.CardDoesNotFit);
        }

        if (play.From == "hand")
        {
            source.Remove(card);
        }
        else
        {
            source.RemoveAt(source.Count - 1);
        }

        tower.Add(card);
        var done = tower.Count == Highest;
        if (done)
        {
            // A full tower is cleared; its cards are shuffled into the deck when that runs out
            s.Done.AddRange(tower);
            tower.Clear();
        }

        var next = s with { Last = new NumberTowersAction(seat, "play", card, play.From, play.Tower, play.Pile, done) };
        if (s.Stocks[seat].Count == 0)
        {
            return GameStep.Won(Save(next with { Over = true }), seat);
        }

        if (s.Hands[seat].Count == 0)
        {
            Fill(next, seat);
        }

        return GameStep.Next(Save(next));
    }

    private static GameStep Discard(State s, int seat, NumberTowersDiscard discard)
    {
        if (!s.Hands[seat].Contains(discard.Card) || discard.Pile is < 0 or >= PileCount)
        {
            return GameStep.Refused(GameErrors.MoveInvalid);
        }

        s.Hands[seat].Remove(discard.Card);
        s.Discards[seat][discard.Pile].Add(discard.Card);
        return GameStep.Next(Save(Advance(s with { Last = new NumberTowersAction(seat, "discard", discard.Card, Pile: discard.Pile) })));
    }

    /// <summary>The next player still in the game, whose hand is filled up for the turn.</summary>
    private static State Advance(State s)
    {
        var turn = s.Turn;
        do
        {
            turn = (turn + 1) % s.Stocks.Count;
        }
        while (s.Out.Contains(turn));

        var next = s with { Turn = turn };
        Fill(next, turn);
        return next;
    }

    /// <summary>Draws up to five hand cards; an empty deck is made from the finished towers, shuffled.</summary>
    private static void Fill(State s, int seat)
    {
        var hand = s.Hands[seat];
        while (hand.Count < HandSize)
        {
            if (s.Deck.Count == 0 && s.Done.Count > 0)
            {
                var random = new Random(s.Seed);
                s.Deck.AddRange(s.Done.OrderBy(_ => random.Next()));
                s.Done.Clear();
                s.Seed = random.Next();
            }

            if (s.Deck.Count == 0)
            {
                return;
            }

            hand.Add(s.Deck[^1]);
            s.Deck.RemoveAt(s.Deck.Count - 1);
        }
    }

    private static State Load(string state) => JsonSerializer.Deserialize<State>(state, Json)!;

    private static string Save(State state) => JsonSerializer.Serialize(state, Json);

    /// <summary>
    /// Stacks are lists with the top card last. <c>Done</c>: cards of finished towers (and of players who gave up),
    /// shuffled into the deck with <c>Seed</c> once it runs out. Every move loads a fresh copy, so changing the lists
    /// in place is safe.
    /// </summary>
    private sealed record State(
        List<List<int>> Stocks, List<List<int>> Hands, List<List<List<int>>> Discards, List<List<int>> Towers, List<int> Deck,
        List<int> Done, int Turn, IReadOnlyList<int> Out, NumberTowersAction? Last, bool Over, int Seed)
    {
        public int Seed { get; set; } = Seed;
    }
}
