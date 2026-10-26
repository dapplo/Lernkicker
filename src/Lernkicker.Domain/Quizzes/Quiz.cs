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
