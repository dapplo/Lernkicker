using System.Text.Json;

namespace Lernkicker.Domain.Multiplayer;

/// <summary>
/// Plays a card, e.g. "H7" (suit C, S, H or D for clubs, spades, hearts, diamonds; rank 7, 8, 9, T for ten, J, Q, K,
/// A). A jack needs the suit the player wishes for.
/// </summary>
public sealed record MauMauPlay(string Card, string? Suit = null) : GameMove;

/// <summary>Draws a card, or the cards the sevens before asked for.</summary>
public sealed record MauMauDraw : GameMove;

/// <summary>Ends the turn after drawing a card that does not fit (or that the player keeps).</summary>
public sealed record MauMauPass : GameMove;

/// <summary>What happened last, for the others to see.</summary>
public sealed record MauMauAction(int Seat, string Kind, string? Card = null, int Count = 0, string? Suit = null);

/// <summary>
/// The table for one player: the own hand (and which cards may be played now), how many cards the others hold,
/// the top card and the wished suit after a jack. <c>PendingDraw</c> is what sevens added up to; <c>CanPass</c> is
/// true after drawing. <c>Out</c> are seats that gave up.
/// </summary>
public sealed record MauMauView(
    IReadOnlyList<string> Hand, IReadOnlyList<string> Playable, IReadOnlyList<int> HandSizes, string Top, string? Suit,
    int PendingDraw, int Turn, int DeckSize, bool CanPass, string? Drawn, IReadOnlyList<int> Out, MauMauAction? Last,
    bool Over) : GameView;

/// <summary>
/// Mau-Mau for two to five players with a 32-card deck; everyone gets five cards. A card fits on one of the same
/// suit or rank. A seven makes the next player draw two (unless they play a seven too; it adds up), an eight skips
/// the next player, a jack fits on anything but a jack and lets the player wish for a suit. Who cannot or does not
/// want to play draws a card, may play it if it fits, or passes. Whoever is rid of all cards first wins.
/// </summary>
public sealed class MauMau : IGameRules
{
    public const int HandSize = 5;

    public static readonly string[] Suits = ["C", "S", "H", "D"];
    public static readonly string[] Ranks = ["7", "8", "9", "T", "J", "Q", "K", "A"];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string Game => MultiplayerGames.MauMau;

    public int MinPlayers => 2;

    public int MaxPlayers => 5;

    public static IReadOnlyList<string> Deck => Suits.SelectMany(s => Ranks.Select(r => s + r)).ToList();

    public string Start(int players, Random random)
    {
        var deck = Deck.OrderBy(_ => random.Next()).ToList();
        var hands = Enumerable.Range(0, players).Select(p => deck.Skip(p * HandSize).Take(HandSize).ToList()).ToList();
        deck = deck.Skip(players * HandSize).ToList();
        var top = deck[0];
        return Save(new State(hands, deck.Skip(1).ToList(), [top], null, random.Next(players), 0, null, [], null, false, random.Next()));
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
            MauMauPlay play => PlayCard(s, seat, play),
            MauMauDraw => Draw(s, seat),
            MauMauPass when s.Drawn is not null || s.DrewNothing => GameStep.Next(Save(Advance(s with { Last = new MauMauAction(seat, "pass") }, 1))),
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

        var hands = s.Hands.Select(h => h.ToList()).ToList();
        // The cards go under the pile, so the others can still draw them
        var deck = s.Deck.Concat(hands[seat]).ToList();
        hands[seat] = [];
        var next = s with { Hands = hands, Deck = deck, Out = [.. s.Out, seat], Last = new MauMauAction(seat, "resign") };
        var active = Active(next).ToList();
        if (active.Count == 1)
        {
            return GameStep.Won(Save(next with { Over = true }), active[0]);
        }

        return GameStep.Next(Save(s.Turn == seat ? Advance(next, 1) : next));
    }

    public IReadOnlyList<int> ToMove(string state)
    {
        var s = Load(state);
        return s.Over ? [] : [s.Turn];
    }

    public GameView View(string state, int seat)
    {
        var s = Load(state);
        var hand = s.Hands[seat].OrderBy(c => Array.IndexOf(Suits, c[..1])).ThenBy(c => Array.IndexOf(Ranks, c[1..])).ToList();
        var playable = !s.Over && s.Turn == seat
            ? hand.Where(c => Fits(s, c) && (s.Drawn is null || c == s.Drawn)).ToList()
            : [];
        return new MauMauView(
            hand, playable, s.Hands.Select(h => h.Count).ToList(), s.Discard[^1], s.Suit, s.PendingDraw, s.Turn, s.Deck.Count,
            !s.Over && s.Turn == seat && (s.Drawn is not null || s.DrewNothing), s.Turn == seat ? s.Drawn : null, s.Out, s.Last, s.Over);
    }

    /// <summary>Whether the card may go on the pile now.</summary>
    private static bool Fits(State s, string card)
    {
        var top = s.Discard[^1];
        if (s.PendingDraw > 0)
        {
            return Rank(card) == "7";
        }

        if (Rank(card) == "J")
        {
            return Rank(top) != "J";
        }

        // On a jack only the wished suit fits; a jack that started the game takes anything
        return s.Suit is { } wished ? Suit(card) == wished : Rank(top) == "J" || Suit(card) == Suit(top) || Rank(card) == Rank(top);
    }

    private static GameStep PlayCard(State s, int seat, MauMauPlay play)
    {
        if (!s.Hands[seat].Contains(play.Card) || (s.Drawn is not null && play.Card != s.Drawn) || !Fits(s, play.Card))
        {
            return GameStep.Refused(s.PendingDraw > 0 && s.Hands[seat].Contains(play.Card) ? GameErrors.MustDraw : GameErrors.CardNotPlayable);
        }

        var jack = Rank(play.Card) == "J";
        if (jack && (play.Suit is null || !Suits.Contains(play.Suit)))
        {
            return GameStep.Refused(GameErrors.MoveInvalid);
        }

        var hands = s.Hands.Select(h => h.ToList()).ToList();
        hands[seat].Remove(play.Card);
        var next = s with
        {
            Hands = hands,
            Discard = [.. s.Discard, play.Card],
            Suit = jack ? play.Suit : null,
            PendingDraw = Rank(play.Card) == "7" ? s.PendingDraw + 2 : 0,
            Last = new MauMauAction(seat, "play", play.Card, Suit: jack ? play.Suit : null),
        };

        if (hands[seat].Count == 0)
        {
            return GameStep.Won(Save(next with { Over = true, Drawn = null, DrewNothing = false }), seat);
        }

        return GameStep.Next(Save(Advance(next, Rank(play.Card) == "8" ? 2 : 1)));
    }

    private static GameStep Draw(State s, int seat)
    {
        if (s.Drawn is not null || s.DrewNothing)
        {
            return GameStep.Refused(GameErrors.MoveInvalid);
        }

        var count = Math.Max(1, s.PendingDraw);
        var (deck, discard, seed) = (s.Deck.ToList(), s.Discard.ToList(), s.Seed);
        var drawn = new List<string>();
        for (var i = 0; i < count; i++)
        {
            if (deck.Count == 0 && discard.Count > 1)
            {
                // The pile, except its top card, is shuffled and becomes the new deck
                var random = new Random(seed);
                deck = discard[..^1].OrderBy(_ => random.Next()).ToList();
                discard = [discard[^1]];
                seed = random.Next();
            }

            if (deck.Count == 0)
            {
                break;
            }

            drawn.Add(deck[0]);
            deck.RemoveAt(0);
        }

        var hands = s.Hands.Select(h => h.ToList()).ToList();
        hands[seat].AddRange(drawn);
        var next = s with { Hands = hands, Deck = deck, Discard = discard, Seed = seed, Last = new MauMauAction(seat, "draw", Count: drawn.Count) };

        // Drawing for sevens ends the turn; one card drawn by choice may still be played
        return s.PendingDraw > 0
            ? GameStep.Next(Save(Advance(next with { PendingDraw = 0 }, 1)))
            : GameStep.Next(Save(next with { Drawn = drawn.FirstOrDefault(), DrewNothing = drawn.Count == 0 }));
    }

    /// <summary>Moves the turn on by <paramref name="steps"/> players still in the game.</summary>
    private static State Advance(State s, int steps)
    {
        var turn = s.Turn;
        for (var i = 0; i < steps; i++)
        {
            do
            {
                turn = (turn + 1) % s.Hands.Count;
            }
            while (s.Out.Contains(turn));
        }

        return s with { Turn = turn, Drawn = null, DrewNothing = false };
    }

    private static IEnumerable<int> Active(State s) => Enumerable.Range(0, s.Hands.Count).Where(seat => !s.Out.Contains(seat));

    private static string Suit(string card) => card[..1];

    private static string Rank(string card) => card[1..];

    private static State Load(string state) => JsonSerializer.Deserialize<State>(state, Json)!;

    private static string Save(State state) => JsonSerializer.Serialize(state, Json);

    /// <summary><c>Seed</c>: for shuffling the pile when the deck runs out (moves bring no randomness of their own).</summary>
    private sealed record State(
        IReadOnlyList<List<string>> Hands, IReadOnlyList<string> Deck, IReadOnlyList<string> Discard, string? Suit, int Turn,
        int PendingDraw, string? Drawn, IReadOnlyList<int> Out, MauMauAction? Last, bool Over, int Seed, bool DrewNothing = false);
}
