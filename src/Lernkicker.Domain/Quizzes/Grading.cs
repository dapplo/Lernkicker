using System.Text;

namespace Lernkicker.Domain.Quizzes;

/// <summary>
/// A child's answer to one exercise. Only the list matching the exercise type is used; all indices refer to
/// the original order in the quiz (<see cref="SheetItem.Index"/>), not the shuffled display order.
/// </summary>
public sealed record ExerciseAnswer
{
    /// <summary>cloze, matching, freeText: one entry per gap, row or field (position = index). vocabulary: one per question. wordSearch: per word the word if found. fallingWords: per word the option it landed on.</summary>
    public IReadOnlyList<string?>? Texts { get; init; }

    /// <summary>trueFalse: one entry per statement (position = index), null = not answered.</summary>
    public IReadOnlyList<bool?>? Truths { get; init; }

    /// <summary>ordering: step indices in the order the child tapped them.</summary>
    public IReadOnlyList<int>? Order { get; init; }

    /// <summary>multipleChoice: per question (position = index) the selected option indices.</summary>
    public IReadOnlyList<IReadOnlyList<int>?>? Choices { get; init; }
}

/// <summary>
/// The result of one part. <c>Solution</c> is quiz content (in the quiz's language); for trueFalse it is null and
/// <c>ExpectedTruth</c> holds the answer, so the UI can say "true"/"false" in the user's language.
/// </summary>
public sealed record PartResult(int Index, bool Correct, string? Solution, string? Explanation, bool? ExpectedTruth = null);

public sealed record ExerciseResult(string Id, string Type, string Instruction, int Points, int MaxPoints, IReadOnlyList<PartResult> Parts, string? Explanation, string? SolutionText);

/// <summary><c>Mark</c> is a rough German school mark (1 best, 6 worst); null for an adult, who gets no school marks.</summary>
public sealed record QuizResult(int Points, int MaxPoints, int Percent, int? Mark, IReadOnlyList<ExerciseResult> Exercises);

/// <summary>Grades answers as described in docs/quiz-format.md ("Grading").</summary>
public static class Grader
{
    public static QuizResult Grade(Quiz quiz, IReadOnlyDictionary<string, ExerciseAnswer> answers, bool withMark = true)
    {
        var exercises = quiz.Exercises
            .Select(e => GradeExercise(e, answers.GetValueOrDefault(e.Id) ?? new ExerciseAnswer()))
            .ToList();
        var points = exercises.Sum(e => e.Points);
        var max = exercises.Sum(e => e.MaxPoints);
        var share = max == 0 ? 0 : (double)points / max;
        return new QuizResult(points, max, (int)Math.Round(share * 100, MidpointRounding.AwayFromZero), withMark ? Mark(share) : null, exercises);
    }

    /// <summary>Rough German school mark from the share of points (orientation only).</summary>
    public static int Mark(double share) => share switch
    {
        >= 0.95 => 1,
        >= 0.83 => 2,
        >= 0.67 => 3,
        >= 0.50 => 4,
        >= 0.30 => 5,
        _ => 6,
    };

    /// <summary>Points for an exercise: 1 per correct part, or scaled to <c>points</c>, rounding half away from zero.</summary>
    public static int Points(int correct, int parts, int? points) =>
        points is { } p && parts > 0
            ? (int)Math.Round((double)correct / parts * p, MidpointRounding.AwayFromZero)
            : correct;

    public static ExerciseResult GradeExercise(Exercise exercise, ExerciseAnswer answer)
    {
        var (type, parts) = exercise switch
        {
            ClozeExercise c => ("cloze", c.Gaps
                .Select((gap, i) => new PartResult(i, Matches(At(answer.Texts, i), gap.Answers), gap.Answers[0], null))
                .ToList()),
            MatchingExercise m => ("matching", m.Rows
                .Select((row, i) => new PartResult(i, Matches(At(answer.Texts, i), [row.Answer]), row.Answer, row.Explanation))
                .ToList()),
            OrderingExercise o => ("ordering", o.Steps
                .Select((step, i) => new PartResult(i, answer.Order is { } order && i < order.Count && order[i] == i, step, null))
                .ToList()),
            TrueFalseExercise t => ("trueFalse", t.Statements
                .Select((statement, i) => new PartResult(i, At(answer.Truths, i) == statement.IsTrue, null, statement.Explanation, statement.IsTrue))
                .ToList()),
            FreeTextExercise f => ("freeText", GradeFreeText(f, answer.Texts ?? [])),
            MultipleChoiceExercise mc => ("multipleChoice", mc.Questions
                .Select((question, i) => new PartResult(
                    i,
                    At(answer.Choices, i) is { } selected && selected.ToHashSet().SetEquals(question.Correct),
                    string.Join(", ", question.Correct.Select(c => question.Options[c].Label)),
                    question.Explanation))
                .ToList()),
            WordSearchExercise w => ("wordSearch", w.Words
                .Select((word, i) => new PartResult(i, Matches(At(answer.Texts, i), [word]), word, null))
                .ToList()),
            FallingWordsExercise f => ("fallingWords", f.Words
                .Select((word, i) => new PartResult(i, Matches(At(answer.Texts, i), [word.Answer]), word.Answer, word.Explanation))
                .ToList()),
            VocabularyExercise v => ("vocabulary", v.Questions
                .Select(q => new PartResult(q.Index, Spelling.Check(At(answer.Texts, q.Index), q.Accepted) == SpellingResult.Right, q.Accepted[0], null))
                .ToList()),
            _ => throw new NotSupportedException($"Unknown exercise type {exercise.GetType().Name}"),
        };

        var points = Points(parts.Count(p => p.Correct), parts.Count, exercise.Points);
        return new ExerciseResult(exercise.Id, type, exercise.Instruction, points, exercise.MaxPoints, parts, exercise.Explanation, exercise.SolutionText);
    }

    private static List<PartResult> GradeFreeText(FreeTextExercise f, IReadOnlyList<string?> texts)
    {
        if (!f.AnyOrder)
        {
            return f.Fields
                .Select((field, i) => new PartResult(i, Matches(At(texts, i), field.Answers), field.Answers[0], null))
                .ToList();
        }

        // Any correct answer may sit in any field, but each expected answer and each typed word counts only once.
        var open = f.Fields.ToList();
        var typed = new List<string>();
        var parts = f.Fields.Select((_, i) =>
        {
            var input = At(texts, i);
            var hit = typed.Any(t => Matches(input, [t])) ? null : open.FirstOrDefault(o => Matches(input, o.Answers));
            if (hit is not null)
            {
                open.Remove(hit);
                typed.Add(input!);
            }

            return new PartResult(i, hit is not null, hit?.Answers[0], null);
        }).ToList();

        // Wrong fields show the answers that are still missing
        return parts
            .Select(p => p.Correct || open.Count == 0 ? p : p with { Solution = TakeMissing(open, typed) })
            .ToList();
    }

    private static string TakeMissing(List<AnswerField> open, List<string> typed)
    {
        var first = open[0];
        open.RemoveAt(0);
        var missing = first.Answers.FirstOrDefault(a => !typed.Any(t => Matches(t, [a]))) ?? first.Answers[0];
        typed.Add(missing);
        return missing;
    }

    private static T? At<T>(IReadOnlyList<T>? list, int i) =>
        list is not null && i < list.Count ? list[i] : default;

    /// <summary>Compares ignoring case, surrounding and repeated whitespace.</summary>
    public static bool Matches(string? input, IEnumerable<string> accepted)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var normal = Normalize(input);
        return accepted.Any(a => string.Equals(normal, Normalize(a), StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string text) =>
        string.Join(' ', text.Normalize(NormalizationForm.FormC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
