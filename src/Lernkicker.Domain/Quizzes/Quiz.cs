using Lernkicker.Domain.Media;

namespace Lernkicker.Domain.Quizzes;

/// <summary>A practice quiz in the quiz/v1 format (see docs/quiz-format.md).</summary>
public sealed record Quiz
{
    public const string FormatV1 = "quiz/v1";
    public const string DefaultLanguage = "de";

    public required string Format { get; init; }
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Subject { get; init; }
    /// <summary>School year; null for material that is not for a school year (adults, work).</summary>
    public int? GradeLevel { get; init; }
    public string Language { get; init; } = DefaultLanguage;
    public string? Topic { get; init; }
    public string? Description { get; init; }
    public int? DurationMinutes { get; init; }
    public string? Source { get; init; }
    public bool Shuffle { get; init; } = true;
    public required IReadOnlyList<Exercise> Exercises { get; init; }

    public int MaxPoints => Exercises.Sum(e => e.MaxPoints);

    /// <summary>Every picture the quiz names, in file order.</summary>
    public IEnumerable<ImageUse> Images()
    {
        for (var i = 0; i < Exercises.Count; i++)
        {
            foreach (var use in Exercises[i].Images($"exercises/{i}"))
            {
                yield return use;
            }
        }
    }
}

/// <summary>Common fields of all exercise types. Every exercise consists of scored parts.</summary>
public abstract record Exercise
{
    public required string Id { get; init; }
    public required string Instruction { get; init; }
    public string? Hint { get; init; }
    public int? Points { get; init; }
    public string? Explanation { get; init; }
    public string? SolutionText { get; init; }

    /// <summary>A picture the whole exercise refers to, e.g. a map or a diagram.</summary>
    public ImageRef? Image { get; init; }

    /// <summary>Number of scored parts (gaps, rows, steps, statements, fields, questions).</summary>
    public abstract int PartCount { get; }

    public int MaxPoints => Points ?? PartCount;

    public IEnumerable<ImageUse> Images(string path)
    {
        if (Image is not null)
        {
            yield return new ImageUse($"{path}/image", Image);
        }

        foreach (var use in PartImages(path))
        {
            yield return use;
        }
    }

    protected virtual IEnumerable<ImageUse> PartImages(string path) => [];
}

public sealed record ClozeExercise : Exercise
{
    public const string GapMarker = "___";

    public required string Text { get; init; }
    public IReadOnlyList<string>? WordBank { get; init; }
    public required IReadOnlyList<Gap> Gaps { get; init; }

    public override int PartCount => Gaps.Count;

    public int GapsInText => Text.Split(GapMarker).Length - 1;
}

public sealed record MatchingExercise : Exercise
{
    public required IReadOnlyList<string> Options { get; init; }
    public required IReadOnlyList<MatchingRow> Rows { get; init; }

    public override int PartCount => Rows.Count;

    protected override IEnumerable<ImageUse> PartImages(string path) =>
        Rows.Select((r, i) => (r.Image, At: $"{path}/rows/{i}/image"))
            .Where(x => x.Image is not null)
            .Select(x => new ImageUse(x.At, x.Image!));
}

public sealed record OrderingExercise : Exercise
{
    /// <summary>Steps in the correct order.</summary>
    public required IReadOnlyList<string> Steps { get; init; }

    public override int PartCount => Steps.Count;
}

public sealed record TrueFalseExercise : Exercise
{
    public required IReadOnlyList<Statement> Statements { get; init; }

    public override int PartCount => Statements.Count;

    protected override IEnumerable<ImageUse> PartImages(string path) =>
        Statements.Select((s, i) => (s.Image, At: $"{path}/statements/{i}/image"))
            .Where(x => x.Image is not null)
            .Select(x => new ImageUse(x.At, x.Image!));
}

public sealed record FreeTextExercise : Exercise
{
    public bool AnyOrder { get; init; }
    public required IReadOnlyList<AnswerField> Fields { get; init; }

    public override int PartCount => Fields.Count;
}

public sealed record MultipleChoiceExercise : Exercise
{
    public required IReadOnlyList<ChoiceQuestion> Questions { get; init; }

    public override int PartCount => Questions.Count;

    protected override IEnumerable<ImageUse> PartImages(string path)
    {
        for (var i = 0; i < Questions.Count; i++)
        {
            var question = Questions[i];
            if (question.Image is not null)
            {
                yield return new ImageUse($"{path}/questions/{i}/image", question.Image);
            }

            for (var j = 0; j < question.Options.Count; j++)
            {
                if (question.Options[j].Image is { } image)
                {
                    yield return new ImageUse($"{path}/questions/{i}/options/{j}/image", image);
                }
            }
        }
    }
}

/// <summary>
/// A word search: the words are hidden in a grid of letters that is generated anew for every round (see
/// <see cref="WordSearchGrid"/>). Each word is a part.
/// </summary>
public sealed record WordSearchExercise : Exercise
{
    public const int MinSize = 5;
    public const int MaxSize = 14;

    public required IReadOnlyList<string> Words { get; init; }

    /// <summary>Rows and columns of the grid; null picks a size that fits the words.</summary>
    public int? Size { get; init; }

    /// <summary>Sets which directions are used and whether words may cross; the three switches below override it.</summary>
    public WordSearchLevel Level { get; init; } = WordSearchLevel.Easy;

    /// <summary>Words may also run diagonally; null takes it from <see cref="Level"/> (medium and up).</summary>
    public bool? Diagonal { get; init; }

    /// <summary>Words may also run backwards (right to left, bottom to top); null takes it from <see cref="Level"/> (hard).</summary>
    public bool? Backwards { get; init; }

    /// <summary>Words may cross and share a letter; null takes it from <see cref="Level"/> (medium and up).</summary>
    public bool? Overlap { get; init; }

    public bool AllowsDiagonal => Diagonal ?? Level >= WordSearchLevel.Medium;

    public bool AllowsBackwards => Backwards ?? Level >= WordSearchLevel.Hard;

    public bool AllowsOverlap => Overlap ?? Level >= WordSearchLevel.Medium;

    public override int PartCount => Words.Count;
}

/// <summary>
/// How hard a word search is. Easy: left to right and top to bottom, words never cross. Medium: also diagonal,
/// words may cross. Hard: also backwards.
/// </summary>
public enum WordSearchLevel
{
    Easy,
    Medium,
    Hard,
}

/// <summary>
/// A game: the words fall down one after the other, and the child taps the option (a category or a description) at
/// the bottom that the word belongs to before it hits the ground. Each word is a part.
/// </summary>
public sealed record FallingWordsExercise : Exercise
{
    /// <summary>The categories or descriptions; each round shows the right one and a few others.</summary>
    public required IReadOnlyList<string> Options { get; init; }

    /// <summary>The falling words, each with the option it belongs to.</summary>
    public required IReadOnlyList<MatchingRow> Words { get; init; }

    /// <summary>Sets how many options are shown at a time and how fast the words fall.</summary>
    public GameLevel Level { get; init; } = GameLevel.Easy;

    /// <summary>Options shown for each word: 2 (easy), 3 (medium) or 4 (hard), at most as many as there are.</summary>
    public int TargetCount => Math.Min(Options.Count, Level switch
    {
        GameLevel.Easy => 2,
        GameLevel.Medium => 3,
        _ => 4,
    });

    /// <summary>Seconds the first word takes from the top to the ground; later words get a little faster.</summary>
    public int FallSeconds => Level switch
    {
        GameLevel.Easy => 10,
        GameLevel.Medium => 8,
        _ => 6,
    };

    public override int PartCount => Words.Count;

    protected override IEnumerable<ImageUse> PartImages(string path) =>
        Words.Select((w, i) => (w.Image, At: $"{path}/words/{i}/image"))
            .Where(x => x.Image is not null)
            .Select(x => new ImageUse(x.At, x.Image!));
}

/// <summary>How hard a game (<see cref="FallingWordsExercise"/>) is.</summary>
public enum GameLevel
{
    Easy,
    Medium,
    Hard,
}

public sealed record Gap(IReadOnlyList<string> Answers);

public sealed record MatchingRow(string Text, string Answer, string? Explanation = null, ImageRef? Image = null);

public sealed record Statement(string Text, bool IsTrue, string? Explanation = null, ImageRef? Image = null);

public sealed record AnswerField(IReadOnlyList<string> Answers, string? Label = null);

/// <summary><c>Correct</c> holds the indices of the correct options; more than one means "select all that apply".</summary>
public sealed record ChoiceQuestion(string Text, IReadOnlyList<ChoiceOption> Options, IReadOnlyList<int> Correct, string? Explanation = null, ImageRef? Image = null);

/// <summary>An answer to pick: a text, a picture, or a picture with a text under it.</summary>
public sealed record ChoiceOption(string? Text, ImageRef? Image = null)
{
    /// <summary>The text, or the picture's description for a picture alone (e.g. in the solution).</summary>
    public string Label => Text ?? Image?.Alt ?? "";

    public static implicit operator ChoiceOption(string text) => new(text);
}

public static class QuizLimits
{
    /// <summary>Largest quiz file accepted on upload.</summary>
    public const int MaxBytes = 256 * 1024;
}
