using System.Text.RegularExpressions;
using Lernkicker.Domain.Lessons;
using Lernkicker.Domain.Media;
using Lernkicker.Domain.Quizzes;

namespace Lernkicker.Domain.Games;

public static class GameKinds
{
    public const string FallingWords = "fallingWords";
    public const string WordSearch = "wordSearch";
    public const string Memory = "memory";
    public const string Crossword = "crossword";

    public static readonly IReadOnlyList<string> All = [FallingWords, Memory, Crossword, WordSearch];
}

/// <summary>A term and what it means, taken from a lesson: a flashcard (front, back), an entry (name, text) or a vocabulary word (word, translation).</summary>
public sealed record GamePair(string Term, string Description, ImageRef? Image = null);

/// <summary>One round of a game, new every time it is asked for.</summary>
public abstract record GameRound(string Kind);

/// <summary>A game that is a quiz exercise type (falling words, word search), played without grading.</summary>
public sealed record ExerciseRound(string Kind, ExerciseSheet Sheet) : GameRound(Kind);

/// <summary>Memory: every pair is two cards, the term and its description, face down in random order.</summary>
public sealed record MemoryRound(IReadOnlyList<MemoryCard> Cards) : GameRound(GameKinds.Memory);

/// <summary>A memory card; the two cards of a pair share <c>Pair</c>.</summary>
public sealed record MemoryCard(int Pair, string Text, ImageRef? Image = null);

public sealed record CrosswordRound(Crossword Grid) : GameRound(GameKinds.Crossword);

/// <summary>
/// The games a lesson's content allows, without anything written for them: flashcards and entries are pairs of a
/// term and its meaning, and each game needs pairs of a certain kind (short texts, single words). A game is
/// offered when the lesson has enough fitting pairs.
/// </summary>
public static partial class LessonGames
{
    /// <summary>Fewest fitting pairs (or words) a game needs to be worth playing.</summary>
    public const int MinPairs = 4;

    private const int MaxTermLength = 40;
    private const int MaxDescriptionLength = 60;
    private const int MaxMemoryTermLength = 30;
    private const int MaxClueLength = 120;
    private const int MinWordLength = 3;
    private const int MaxWordLength = 12;
    private const int FallingWordsPerRound = 12;
    private const int MemoryPairsPerRound = 6;
    private const int WordSearchWordsPerRound = 10;
    private const int CrosswordWordsPerRound = 10;

    /// <summary>All pairs of the lesson, in file order. Formatting marks (<c>**</c>, <c>*</c>) are removed.</summary>
    public static IReadOnlyList<GamePair> Pairs(Lesson lesson) => lesson.Blocks
        .SelectMany(b => b switch
        {
            FlashcardsBlock f => f.Cards.Select(c => new GamePair(Plain(c.Front), Plain(c.Back), c.FrontImage)),
            EntriesBlock e => e.Entries.Select(x => new GamePair(Plain(x.Name), Plain(x.Text), x.Image)),
            // The word in the language being learned is what the child finds in the grids
            VocabularyBlock v => v.Words.Select(w => new GamePair(Plain(w.Word), Plain(w.Translation), w.Image)),
            _ => [],
        })
        .Where(p => p.Term.Length > 0 && p.Description.Length > 0)
        .ToList();

    /// <summary>The games the lesson's content is enough for, in <see cref="GameKinds.All"/> order.</summary>
    public static IReadOnlyList<string> Available(Lesson lesson)
    {
        var pairs = Pairs(lesson);
        return GameKinds.All.Where(kind => Fitting(pairs, kind).Count >= MinPairs).ToList();
    }

    /// <summary>A new round of the game, or null when the lesson's content is not enough for it.</summary>
    public static GameRound? Round(Lesson lesson, string kind, Random random)
    {
        var fitting = Fitting(Pairs(lesson), kind);
        if (fitting.Count < MinPairs)
        {
            return null;
        }

        switch (kind)
        {
            case GameKinds.FallingWords:
            {
                var chosen = Pick(fitting, FallingWordsPerRound, random);
                var exercise = new FallingWordsExercise
                {
                    Id = kind, Instruction = "",
                    Options = chosen.Select(p => p.Description).ToList(),
                    Words = chosen.Select(p => new MatchingRow(p.Term, p.Description, null, p.Image)).ToList(),
                    Level = GameLevel.Medium,
                };
                return new ExerciseRound(kind, ExerciseSheet.For(exercise, true, random));
            }
            case GameKinds.WordSearch:
            {
                var words = Pick(fitting, WordSearchWordsPerRound, random).Select(p => Word(p.Term)!).ToList();
                var exercise = new WordSearchExercise { Id = kind, Instruction = "", Words = words, Level = WordSearchLevel.Medium };
                return new ExerciseRound(kind, ExerciseSheet.For(exercise, true, random));
            }
            case GameKinds.Memory:
            {
                var chosen = Pick(fitting, MemoryPairsPerRound, random);
                var cards = chosen.SelectMany((p, i) => new[] { new MemoryCard(i, p.Term, p.Image), new MemoryCard(i, p.Description) });
                return new MemoryRound(cards.OrderBy(_ => random.Next()).ToList());
            }
            case GameKinds.Crossword:
            {
                var chosen = Pick(fitting, CrosswordWordsPerRound, random);
                var grid = Crossword.Generate(chosen.Select(p => (Word(p.Term)!, p.Description)).ToList(), random);
                return grid.Words.Count >= MinPairs - 1 ? new CrosswordRound(grid) : null;
            }
            default:
                return null;
        }
    }

    /// <summary>
    /// The term as a single word for a grid of letters, without a leading article ("der Weizen" → "Weizen", "de hond"
    /// → "hond", "to run" → "run"), or
    /// null when it is not one word of letters of a fitting length.
    /// </summary>
    public static string? Word(string term)
    {
        var word = Article().Replace(term.Trim(), "");
        return word.Length is >= MinWordLength and <= MaxWordLength && WordSearchGrid.Letters(word).All(char.IsLetter) ? word : null;
    }

    private static List<GamePair> Fitting(IReadOnlyList<GamePair> pairs, string kind)
    {
        IEnumerable<GamePair> fitting = kind switch
        {
            // The descriptions are the targets at the bottom, so they must be short and tell the pairs apart
            GameKinds.FallingWords => pairs.Where(p => p.Term.Length <= MaxTermLength && p.Description.Length <= MaxDescriptionLength),
            GameKinds.Memory => pairs.Where(p => p.Term.Length <= MaxMemoryTermLength && p.Description.Length <= MaxDescriptionLength),
            GameKinds.WordSearch => pairs.Where(p => Word(p.Term) is not null),
            GameKinds.Crossword => pairs.Where(p => Word(p.Term) is not null && p.Description.Length <= MaxClueLength),
            _ => [],
        };

        // A term or a description that appears twice would make two right answers
        return fitting
            .GroupBy(p => kind is GameKinds.WordSearch or GameKinds.Crossword ? WordSearchGrid.Letters(Word(p.Term)!) : p.Term.ToUpperInvariant())
            .Select(g => g.First())
            .GroupBy(p => p.Description.ToUpperInvariant())
            .Select(g => g.First())
            .ToList();
    }

    private static List<GamePair> Pick(List<GamePair> pairs, int count, Random random) =>
        pairs.OrderBy(_ => random.Next()).Take(count).ToList();

    private static string Plain(string text) => text.Replace("**", "").Replace("*", "").Trim();

    [GeneratedRegex(@"^(der|die|das|ein|eine|the|a|an|to|de|het|een)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex Article();
}
