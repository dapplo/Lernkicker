using System.Text.Json;
using Lernkicker.Domain.Multiplayer;

namespace Lernkicker.Domain.Tests;

public class ConnectFourTests
{
    private static readonly ConnectFour Rules = new();

    [Fact]
    public void Discs_fall_down_and_four_in_a_row_win()
    {
        var (state, first) = Start();
        var second = 1 - first;
        // first: columns 0, 1, 2, 3 on the bottom row; second stacks on column 6
        foreach (var column in new[] { 0, 1, 2 })
        {
            state = Next(Rules.Play(state, first, new ConnectFourDrop(column)));
            state = Next(Rules.Play(state, second, new ConnectFourDrop(6)));
        }

        var view = (ConnectFourView)Rules.View(state, first);
        Assert.Equal([first, first, first, -1, -1, -1, second], view.Cells[5]);
        Assert.Equal(second, view.Cells[3][6]);

        var won = Rules.Play(state, first, new ConnectFourDrop(3));
        Assert.Equal(first, won.Winner);
        Assert.Equal(4, ((ConnectFourView)Rules.View(won.State!, second)).Line.Count);
    }

    [Fact]
    public void Diagonals_win_too_and_full_columns_are_refused()
    {
        var (state, a) = Start();
        var b = 1 - a;
        // a builds the diagonal 0/5, 1/4, 2/3, 3/2 while b fills underneath
        foreach (var (player, column) in new[] { (a, 0), (b, 1), (a, 1), (b, 2), (a, 3), (b, 2), (a, 2), (b, 3), (a, 4), (b, 3) })
        {
            state = Next(Rules.Play(state, player, new ConnectFourDrop(column)));
        }

        Assert.Equal(a, Rules.Play(state, a, new ConnectFourDrop(3)).Winner);

        var (full, c) = Start();
        for (var i = 0; i < ConnectFour.Rows; i++)
        {
            full = Next(Rules.Play(full, i % 2 == 0 ? c : 1 - c, new ConnectFourDrop(0)));
        }

        Assert.Equal(GameErrors.ColumnFull, Rules.Play(full, c, new ConnectFourDrop(0)).Error);
        Assert.Equal(GameErrors.NotYourTurn, Rules.Play(full, 1 - c, new ConnectFourDrop(1)).Error);
    }

    [Fact]
    public void A_full_board_without_four_is_a_draw()
    {
        var (state, first) = Start();
        // Columns in pairs, so no line of four forms: 0,1 / 2,3 / 4,5 in blocks of two rows, then column 6
        var order = new[] { 0, 1, 0, 1, 1, 0, 1, 0, 0, 1, 0, 1, 2, 3, 2, 3, 3, 2, 3, 2, 2, 3, 2, 3, 4, 5, 4, 5, 5, 4, 5, 4, 4, 5, 4, 5, 6, 6, 6, 6, 6, 6 };
        GameStep step = GameStep.Next(state);
        var seat = first;
        foreach (var column in order)
        {
            step = Rules.Play(step.State!, seat, new ConnectFourDrop(column));
            Assert.True(step.IsValid, step.Error);
            Assert.Null(step.Winner);
            seat = 1 - seat;
        }

        Assert.True(step.Over);
    }

    private static (string State, int First) Start()
    {
        var state = Rules.Start(2, new Random(1));
        return (state, Rules.ToMove(state).Single());
    }

    private static string Next(GameStep step)
    {
        Assert.True(step.IsValid, step.Error);
        return step.State!;
    }
}

public class CheckersTests
{
    private static readonly Checkers Rules = new();

    [Fact]
    public void White_begins_with_seven_moves()
    {
        var state = Rules.Start(2, new Random(1));
        var white = Rules.ToMove(state).Single();
        var view = (CheckersView)Rules.View(state, white);
        Assert.True(view.IAmWhite);
        Assert.Equal(7, view.Moves.Count);
        Assert.Empty(((CheckersView)Rules.View(state, 1 - white)).Moves);
    }

    [Fact]
    public void Capturing_is_compulsory_and_continues()
    {
        // A white piece at c3 (2,5) can take b4... set up: black pieces at (3,4) and (3,2), white at (2,5)
        var state = With(
            "........",
            "........",
            "........",
            "...b....",
            "........",
            "...b....",
            "..w.....",
            ".......b");
        var view = (CheckersView)Rules.View(state, 0);
        var move = Assert.Single(view.Moves);
        Assert.Equal([new Cell(2, 6), new Cell(4, 4), new Cell(2, 2)], move);
        Assert.Equal(GameErrors.MustCapture, Rules.Play(state, 0, new CheckersMove([new Cell(2, 6), new Cell(1, 5)])).Error);

        var after = Rules.Play(state, 0, new CheckersMove(move));
        Assert.True(after.IsValid, after.Error);
        var board = ((CheckersView)Rules.View(after.State!, 0)).Board;
        Assert.Equal("..w.....", board[2]);
        Assert.Equal("........", board[3]);
        Assert.Equal("........", board[5]);
    }

    [Fact]
    public void Pieces_become_flying_kings_on_the_far_row()
    {
        var state = With(
            "........",
            "..w.....",
            "........",
            "........",
            "........",
            "b.......",
            "........",
            "........");
        var promoted = Rules.Play(state, 0, new CheckersMove([new Cell(2, 1), new Cell(1, 0)]));
        Assert.True(promoted.IsValid, promoted.Error);
        Assert.Equal(".W......", ((CheckersView)Rules.View(promoted.State!, 0)).Board[0]);

        // Black moves somewhere; then the king may fly across the board
        var black = Rules.Play(promoted.State!, 1, new CheckersMove([new Cell(0, 5), new Cell(1, 6)]));
        Assert.True(black.IsValid, black.Error);
        // Nothing to capture, so the king may fly along its diagonal
        var moves = ((CheckersView)Rules.View(black.State!, 0)).Moves;
        Assert.Contains(moves, m => m.SequenceEqual([new Cell(1, 0), new Cell(6, 5)]));
    }

    [Fact]
    public void Taking_the_last_piece_wins()
    {
        var state = With(
            "........",
            "........",
            "........",
            "........",
            "........",
            "...b....",
            "..w.....",
            "........");
        var won = Rules.Play(state, 0, new CheckersMove([new Cell(2, 6), new Cell(4, 4)]));
        Assert.Equal(0, won.Winner);
    }

    /// <summary>A position with white (seat 0) to move.</summary>
    private static string With(params string[] board) =>
        JsonSerializer.Serialize(new { board, whiteSeat = 0, whiteToMove = true, quietMoves = 0, last = Array.Empty<Cell>(), over = false },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
}

public class MauMauTests
{
    private static readonly MauMau Rules = new();

    [Fact]
    public void Everyone_gets_five_cards_and_only_sees_their_own()
    {
        var state = Rules.Start(4, new Random(5));
        var views = Enumerable.Range(0, 4).Select(seat => (MauMauView)Rules.View(state, seat)).ToList();
        Assert.All(views, v => Assert.Equal(5, v.Hand.Count));
        Assert.All(views, v => Assert.Equal([5, 5, 5, 5], v.HandSizes));
        Assert.Equal(32, views.SelectMany(v => v.Hand).Distinct().Count() + views[0].DeckSize + 1);
        var turn = Rules.ToMove(state).Single();
        Assert.All(views, v => Assert.Equal(turn, v.Turn));
        Assert.All(views.Where((_, seat) => seat != turn), v => Assert.Empty(v.Playable));
    }

    [Fact]
    public void Cards_fit_by_suit_or_rank_and_jacks_wish_a_suit()
    {
        var state = With(top: "H9", hands: [["H7", "S9", "CJ", "DK", "DA"], ["C8", "SQ", "DQ", "S7", "HA"]]);
        var view = (MauMauView)Rules.View(state, 0);
        Assert.Equal(["CJ", "S9", "H7"], view.Playable);
        Assert.Equal(GameErrors.CardNotPlayable, Rules.Play(state, 0, new MauMauPlay("DK")).Error);
        Assert.Equal(GameErrors.MoveInvalid, Rules.Play(state, 0, new MauMauPlay("CJ")).Error);

        var jack = Rules.Play(state, 0, new MauMauPlay("CJ", "D"));
        Assert.True(jack.IsValid, jack.Error);
        var next = (MauMauView)Rules.View(jack.State!, 1);
        Assert.Equal(("CJ", "D", 1), (next.Top, next.Suit, next.Turn));
        Assert.Equal(["DQ"], next.Playable);
    }

    [Fact]
    public void Sevens_add_up_and_eights_skip()
    {
        var state = With(top: "H9", hands: [["H7", "S9", "D8", "DK", "DA"], ["C8", "SQ", "DQ", "S7", "HA"], ["C9", "CQ", "DT", "S8", "HK"]]);
        var step = Rules.Play(state, 0, new MauMauPlay("H7"));
        var view = (MauMauView)Rules.View(step.State!, 1);
        Assert.Equal(2, view.PendingDraw);
        Assert.Equal(["S7"], view.Playable);

        step = Rules.Play(step.State!, 1, new MauMauPlay("S7"));
        Assert.Equal(GameErrors.MustDraw, Rules.Play(step.State!, 2, new MauMauPlay("S8")).Error);
        step = Rules.Play(step.State!, 2, new MauMauDraw());
        var after = (MauMauView)Rules.View(step.State!, 2);
        Assert.Equal((9, 0, 0), (after.Hand.Count, after.PendingDraw, after.Turn));

        // Seat 0 plays an eight on... the top is S7: no eight fits; draw and pass instead
        step = Rules.Play(step.State!, 0, new MauMauDraw());
        Assert.True(((MauMauView)Rules.View(step.State!, 0)).CanPass);
        step = Rules.Play(step.State!, 0, new MauMauPass());
        Assert.Equal([1], Rules.ToMove(step.State!));
    }

    [Fact]
    public void The_first_without_cards_wins_and_giving_up_leaves_the_others_playing()
    {
        var state = With(top: "H9", hands: [["H7"], ["C8", "SQ"], ["C9", "CQ"]]);
        var resigned = Rules.Resign(state, 1);
        Assert.False(resigned.Over);
        Assert.Equal([0], Rules.ToMove(resigned.State!));

        var won = Rules.Play(resigned.State!, 0, new MauMauPlay("H7"));
        Assert.Equal(0, won.Winner);
    }

    private static string With(string top, List<string>[] hands) =>
        JsonSerializer.Serialize(new
        {
            hands,
            deck = MauMau.Deck.Except(hands.SelectMany(h => h)).Where(c => c != top).ToList(),
            discard = new[] { top },
            suit = (string?)null,
            turn = 0,
            pendingDraw = 0,
            drawn = (string?)null,
            @out = Array.Empty<int>(),
            last = (object?)null,
            over = false,
            seed = 1,
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
}

public class ChessTests
{
    private static readonly Chess Rules = new();

    [Theory]
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq -", 3, 8902)]
    // "Kiwipete": castling, en passant, promotions and pins all at once
    [InlineData("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq -", 2, 2039)]
    [InlineData("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - -", 3, 2812)]
    public void Move_generation_counts_match_the_known_numbers(string fen, int depth, int expected) =>
        Assert.Equal(expected, Perft(Fen(fen), depth));

    [Fact]
    public void Fools_mate_ends_the_game()
    {
        var state = Rules.Start(2, new Random(1));
        var white = Rules.ToMove(state).Single();
        var black = 1 - white;
        foreach (var (seat, move) in new[] { (white, "f2f3"), (black, "e7e5"), (white, "g2g4") })
        {
            var step = Rules.Play(state, seat, Move(move));
            Assert.True(step.IsValid, step.Error);
            state = step.State!;
        }

        Assert.Equal(GameErrors.ChessMoveInvalid, Rules.Play(state, black, Move("d8d4")).Error);
        var mate = Rules.Play(state, black, Move("d8h4"));
        Assert.Equal(black, mate.Winner);
        var view = (ChessView)Rules.View(mate.State!, white);
        Assert.Equal(("checkmate", true), (view.Ending, view.Check));
        Assert.Empty(view.Moves);
    }

    [Fact]
    public void Only_the_player_to_move_sees_moves_and_white_is_drawn_by_lot()
    {
        var whites = Enumerable.Range(0, 20).Select(i => Rules.ToMove(Rules.Start(2, new Random(i))).Single()).Distinct().Count();
        Assert.Equal(2, whites);

        var state = Rules.Start(2, new Random(1));
        var white = Rules.ToMove(state).Single();
        Assert.Equal(20, ((ChessView)Rules.View(state, white)).Moves.Count);
        Assert.Empty(((ChessView)Rules.View(state, 1 - white)).Moves);
        Assert.Equal(GameErrors.NotYourTurn, Rules.Play(state, 1 - white, Move("e7e5")).Error);
    }

    private static ChessMove Move(string uci) =>
        new(new Cell(uci[0] - 'a', '8' - uci[1]), new Cell(uci[2] - 'a', '8' - uci[3]), uci.Length > 4 ? uci[4..].ToUpperInvariant() : null);

    private static long Perft(Chess.Position position, int depth)
    {
        var moves = Chess.LegalMoves(position);
        if (depth == 1)
        {
            return moves.Count;
        }

        return moves.Sum(m => Perft(Play(position, m), depth - 1));
    }

    /// <summary>The position after a legal move, through the rules (start from a saved state with this position).</summary>
    private static Chess.Position Play(Chess.Position position, ChessMove move)
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var state = JsonSerializer.Serialize(new { position, whiteSeat = 0, history = Array.Empty<string>(), last = (object?)null, ending = (string?)null, over = false }, json);
        var step = Rules.Play(state, position.WhiteToMove ? 0 : 1, move);
        return JsonSerializer.Deserialize<StateShape>(step.State!, json)!.Position;
    }

    private sealed record StateShape(Chess.Position Position);

    private static Chess.Position Fen(string fen)
    {
        var parts = fen.Split(' ');
        var board = parts[0].Split('/').Select(row => string.Concat(row.Select(c => char.IsDigit(c) ? new string('.', c - '0') : c.ToString()))).ToList();
        var enPassant = parts[3] == "-" ? null : new Cell(parts[3][0] - 'a', '8' - parts[3][1]);
        return new Chess.Position(board, parts[1] == "w", parts[2] == "-" ? "" : parts[2], enPassant, 0);
    }
}

public class NumberTowersTests
{
    private static readonly NumberTowers Rules = new();

    [Fact]
    public void Everyone_gets_a_stock_and_the_first_player_five_cards()
    {
        var state = Rules.Start(3, new Random(7));
        var turn = Rules.ToMove(state).Single();
        var views = Enumerable.Range(0, 3).Select(seat => (NumberTowersView)Rules.View(state, seat)).ToList();
        Assert.All(views, v => Assert.All(v.Seats, s => Assert.Equal(NumberTowers.StockSize, s.StockCount)));
        Assert.Equal(5, views[turn].Hand.Count);
        Assert.All(views.Where((_, seat) => seat != turn), v => Assert.Empty(v.Hand));
        Assert.Equal(162 - 3 * NumberTowers.StockSize - 5, views[0].DeckSize);
        Assert.Equal([0, 0, 0, 0], views[0].Towers);
    }

    [Fact]
    public void Towers_go_up_one_by_one_and_stars_fit_anywhere()
    {
        var state = With(stocks: [[9, 3], [5]], hands: [[1, 2, 0, 7, 7], []]);
        Assert.Equal(GameErrors.CardDoesNotFit, Rules.Play(state, 0, new NumberTowersPlay("hand", 0, Card: 2)).Error);
        Assert.Equal(GameErrors.MoveInvalid, Rules.Play(state, 0, new NumberTowersPlay("hand", 0, Card: 4)).Error);
        Assert.Equal(GameErrors.NotYourTurn, Rules.Play(state, 1, new NumberTowersPlay("stock", 0)).Error);

        state = Ok(Rules.Play(state, 0, new NumberTowersPlay("hand", 0, Card: 1)));
        state = Ok(Rules.Play(state, 0, new NumberTowersPlay("hand", 0, Card: 2)));
        state = Ok(Rules.Play(state, 0, new NumberTowersPlay("stock", 0)));
        state = Ok(Rules.Play(state, 0, new NumberTowersPlay("hand", 1, Card: 0)));
        var view = (NumberTowersView)Rules.View(state, 0);
        Assert.Equal([3, 1, 0, 0], view.Towers);
        Assert.Equal([3, 0, 0, 0], view.TowerTops);
        Assert.Equal(9, view.Seats[0].StockTop);
        Assert.Equal([7, 7], view.Hand);
    }

    [Fact]
    public void Discarding_ends_the_turn_and_the_next_player_fills_the_hand()
    {
        var state = With(stocks: [[9], [5]], hands: [[7, 8], [1]]);
        state = Ok(Rules.Play(state, 0, new NumberTowersDiscard(7, 2)));
        Assert.Equal([1], Rules.ToMove(state));
        var view = (NumberTowersView)Rules.View(state, 1);
        Assert.Equal(5, view.Hand.Count);
        Assert.Equal([7], view.Seats[0].Discards[2]);

        // The top of a discard pile can be played later
        state = With(stocks: [[9], [5]], hands: [[4], [1]], discards: [[[], [1], [], []], [[], [], [], []]]);
        state = Ok(Rules.Play(state, 0, new NumberTowersPlay("discard", 3, Pile: 1)));
        Assert.Equal(1, ((NumberTowersView)Rules.View(state, 0)).Towers[3]);
    }

    [Fact]
    public void A_full_tower_is_cleared_and_an_empty_hand_draws_five_new_cards()
    {
        var state = With(stocks: [[9], [5]], hands: [[12], []], towers: [[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11], [], [], []]);
        var step = Rules.Play(state, 0, new NumberTowersPlay("hand", 0, Card: 12));
        var view = (NumberTowersView)Rules.View(Ok(step), 0);
        Assert.Equal(0, view.Towers[0]);
        Assert.Equal(5, view.Hand.Count);
        Assert.True(view.Last!.Done);
    }

    [Fact]
    public void The_first_with_an_empty_stock_wins_and_giving_up_leaves_the_others_playing()
    {
        var state = With(stocks: [[1], [5], [6]], hands: [[3], [2], [4]]);
        var resigned = Rules.Resign(state, 2);
        Assert.False(resigned.Over);
        Assert.Equal([0], Rules.ToMove(resigned.State!));

        var won = Rules.Play(resigned.State!, 0, new NumberTowersPlay("stock", 0));
        Assert.Equal(0, won.Winner);
        Assert.True(won.Over);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    public void A_simple_strategy_plays_a_whole_game_to_the_end(int players, int seed)
    {
        var state = Rules.Start(players, new Random(seed));
        for (var moves = 0; moves < 5000; moves++)
        {
            var turn = Rules.ToMove(state).Single();
            var view = (NumberTowersView)Rules.View(state, turn);
            var tower = (int card) => Enumerable.Range(0, 4).FirstOrDefault(t => NumberTowers.Fits(card, view.Towers[t]), -1);
            var pile = view.Seats[turn].Discards.Select((p, i) => (p, i)).FirstOrDefault(x => x.p.Count > 0 && x.p[^1] != 0 && tower(x.p[^1]) >= 0, (null!, -1)).Item2;
            GameMove move = view.Seats[turn].StockTop is { } top && tower(top) >= 0 ? new NumberTowersPlay("stock", tower(top))
                : view.Hand.FirstOrDefault(c => c != 0 && tower(c) >= 0, -1) is var hand and >= 0 ? new NumberTowersPlay("hand", tower(hand), hand)
                : pile >= 0 ? new NumberTowersPlay("discard", tower(view.Seats[turn].Discards[pile][^1]), Pile: pile)
                : view.Hand.Count > 0 ? new NumberTowersDiscard(view.Hand.Max(), view.Hand.Max() % 4)
                : new NumberTowersPass();
            var step = Rules.Play(state, turn, move);
            state = Ok(step);
            if (step.Over)
            {
                Assert.NotNull(step.Winner);
                Assert.Equal(0, ((NumberTowersView)Rules.View(state, 0)).Seats[step.Winner!.Value].StockCount);
                return;
            }
        }

        Assert.Fail("The game did not end.");
    }

    private static string Ok(GameStep step)
    {
        Assert.True(step.IsValid, step.Error);
        return step.State!;
    }

    private static string With(List<int>[] stocks, List<int>[] hands, List<int>[][]? discards = null, List<int>[]? towers = null) =>
        JsonSerializer.Serialize(new
        {
            stocks,
            hands,
            discards = discards ?? stocks.Select(_ => new[] { new List<int>(), [], [], [] }).ToArray(),
            towers = towers ?? [[], [], [], []],
            deck = Enumerable.Range(1, 12).SelectMany(n => Enumerable.Repeat(n, 3)).ToList(),
            done = Array.Empty<int>(),
            turn = 0,
            @out = Array.Empty<int>(),
            last = (object?)null,
            over = false,
            seed = 1,
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
}
