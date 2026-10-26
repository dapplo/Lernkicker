using Lernkicker.Domain.Games;
using Lernkicker.Domain.Lessons;
using Lernkicker.Domain.Quizzes;
using Lernkicker.QuizFormat;
using Lernkicker.Tests;

namespace Lernkicker.Domain.Tests;

public class LessonGamesTests
{
    private static readonly Lesson Grain = new LessonJson().Read(File.ReadAllText(Examples.LessonPathOf("science/grain-terms.json")));

    private static Lesson With(params LessonBlock[] blocks) => new() { Id = "test", Title = "Test", Subject = "science", Blocks = blocks };

    private static FlashcardsBlock Cards(params (string Front, string Back)[] cards) =>
        new() { Id = "cards", Cards = cards.Select(c => new Flashcard(c.Front, c.Back)).ToList() };

    [Fact]
    public void Flashcards_and_entries_are_pairs_without_formatting()
    {
        var lesson = With(
            Cards(("Weizen", "Getreide für **helles** Brot")),
            new EntriesBlock { Id = "list", Entries = [new LessonEntry("Bube", "Du *wünschst* dir eine Farbe.")] },
            new FactsBlock { Id = "facts", Items = ["Kein Paar."] });

        Assert.Equal([new GamePair("Weizen", "Getreide für helles Brot"), new GamePair("Bube", "Du wünschst dir eine Farbe.")], LessonGames.Pairs(lesson));
    }

    [Fact]
    public void A_lesson_with_short_single_words_unlocks_every_game()
    {
        Assert.Equal(GameKinds.All, LessonGames.Available(Grain));
    }

    [Fact]
    public void Too_few_or_too_long_pairs_unlock_nothing()
    {
        var few = With(Cards(("Weizen", "Getreide"), ("Roggen", "Getreide"), ("Hafer", "Flocken")));
        var poem = With(new PoemBlock { Id = "poem", Stanzas = [["Im Nebel ruhet noch die Welt"]] });

        Assert.Empty(LessonGames.Available(few));
        Assert.Empty(LessonGames.Available(poem));
    }

    [Fact]
    public void Long_answers_still_allow_word_games_but_no_falling_words()
    {
        var longText = new string('x', 80);
        var lesson = With(Cards(("Weizen", longText + "1"), ("Roggen", longText + "2"), ("Gerste", longText + "3"), ("Hafer", longText + "4")));

        Assert.Equal([GameKinds.Crossword, GameKinds.WordSearch], LessonGames.Available(lesson));
    }

    [Theory]
    [InlineData("der Weizen", "Weizen")]
    [InlineData("Die Ähre", "Ähre")]
    [InlineData("the wheat", "wheat")]
    [InlineData("Freie Wahl", null)]
    [InlineData("7", null)]
    [InlineData("Mau-Mau!", null)]
    [InlineData("Ei", null)]
    public void A_term_becomes_one_word_without_its_article(string term, string? word) =>
        Assert.Equal(word, LessonGames.Word(term));

    [Fact]
    public void Falling_words_drop_the_terms_onto_their_descriptions()
    {
        var round = Assert.IsType<ExerciseRound>(LessonGames.Round(Grain, GameKinds.FallingWords, new Random(1)));
        var sheet = Assert.IsType<FallingWordsSheet>(round.Sheet);

        var pairs = LessonGames.Pairs(Grain).ToDictionary(p => p.Term, p => p.Description);
        Assert.All(sheet.Words, w => Assert.Equal(pairs[w.Text], w.Answer));
        Assert.Equal(3, sheet.TargetCount);
    }

    [Fact]
    public void Memory_has_two_cards_per_pair()
    {
        var round = Assert.IsType<MemoryRound>(LessonGames.Round(Grain, GameKinds.Memory, new Random(2)));

        Assert.Equal(12, round.Cards.Count);
        Assert.All(round.Cards.GroupBy(c => c.Pair), g => Assert.Equal(2, g.Count()));
    }

    [Fact]
    public void Word_search_hides_the_words_without_articles()
    {
        var round = Assert.IsType<ExerciseRound>(LessonGames.Round(Grain, GameKinds.WordSearch, new Random(3)));
        var sheet = Assert.IsType<WordSearchSheet>(round.Sheet);

        Assert.DoesNotContain(sheet.Words, w => w.Text.Contains(' '));
        Assert.Equal(10, sheet.Words.Count);
    }

    [Fact]
    public void A_game_the_content_is_not_enough_for_has_no_round() =>
        Assert.Null(LessonGames.Round(With(Cards(("Weizen", "Getreide"))), GameKinds.Memory, new Random(1)));

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Crossword_words_cross_and_never_clash(int seed)
    {
        var round = Assert.IsType<CrosswordRound>(LessonGames.Round(Grain, GameKinds.Crossword, new Random(seed)));
        var grid = round.Grid;

        Assert.True(grid.Words.Count >= 5, $"only {grid.Words.Count} words");
        Assert.InRange(grid.Width, 1, Crossword.MaxSize);
        Assert.InRange(grid.Height, 1, Crossword.MaxSize);

        // Every cell holds one letter, whichever word it belongs to, and lies inside the grid
        var letters = new Dictionary<(int, int), char>();
        foreach (var word in grid.Words)
        {
            foreach (var (cell, letter) in word.Cells().Zip(word.Answer))
            {
                Assert.InRange(cell.Row, 0, grid.Height - 1);
                Assert.InRange(cell.Column, 0, grid.Width - 1);
                Assert.True(!letters.TryGetValue(cell, out var other) || other == letter);
                letters[cell] = letter;
            }
        }

        // Every word crosses another one
        Assert.All(grid.Words, w => Assert.Contains(grid.Words, o => o != w && o.Cells().Intersect(w.Cells()).Any()));
    }

    [Fact]
    public void Crossword_numbers_follow_reading_order()
    {
        var grid = Crossword.Generate([("WEIZEN", "a"), ("ROGGEN", "b"), ("GERSTE", "c"), ("HAFER", "d")], new Random(1));

        var starts = grid.Words.OrderBy(w => w.Row).ThenBy(w => w.Column).Select(w => w.Number).ToList();
        Assert.Equal(starts.Order(), starts);
        Assert.Equal(1, starts[0]);
    }
}
