namespace Lernkicker.Domain.Quizzes;

/// <summary>
/// What a child's device receives: the quiz without any solutions.
/// Parts carry their original <c>Index</c> so answers can be graded after shuffling.
/// </summary>
public sealed record QuizSheet(
    string Id,
    string Title,
    string Subject,
    int GradeLevel,
    string Language,
    string? Topic,
    int? DurationMinutes,
    int MaxPoints,
    IReadOnlyList<ExerciseSheet> Exercises)
{
    /// <summary>Builds the child's sheet. Pass a seeded <see cref="Random"/> for a reproducible order.</summary>
    public static QuizSheet For(Quiz quiz, Random random)
    {
        var exercises = quiz.Exercises.Select(e => ExerciseSheet.For(e, quiz.Shuffle, random)).ToList();
        return new QuizSheet(quiz.Id, quiz.Title, quiz.Subject, quiz.GradeLevel, quiz.Language, quiz.Topic, quiz.DurationMinutes, quiz.MaxPoints, exercises);
    }
}

/// <summary>A numbered part; <c>Index</c> is its position in the original quiz.</summary>
public sealed record SheetItem(int Index, string Text);

public abstract record ExerciseSheet(string Id, string Instruction, string? Hint, int MaxPoints)
{
    public static ExerciseSheet For(Exercise exercise, bool shuffle, Random random)
    {
        List<SheetItem> Numbered(IEnumerable<string> texts, bool mayShuffle)
        {
            var items = texts.Select((t, i) => new SheetItem(i, t)).ToList();
            return mayShuffle ? Shuffled(items, random) : items;
        }

        return exercise switch
        {
            ClozeExercise c => new ClozeSheet(
                c.Id, c.Instruction, c.Hint, c.MaxPoints,
                c.Text.Split(ClozeExercise.GapMarker),
                c.WordBank is null ? null : Shuffled(c.WordBank.ToList(), random)),
            MatchingExercise m => new MatchingSheet(
                m.Id, m.Instruction, m.Hint, m.MaxPoints,
                m.Options,
                Numbered(m.Rows.Select(r => r.Text), shuffle)),
            OrderingExercise o => new OrderingSheet(
                o.Id, o.Instruction, o.Hint, o.MaxPoints,
                // Always shuffled, otherwise the exercise solves itself
                NotInOriginalOrder(Numbered(o.Steps, true), random)),
            TrueFalseExercise t => new TrueFalseSheet(
                t.Id, t.Instruction, t.Hint, t.MaxPoints,
                Numbered(t.Statements.Select(s => s.Text), shuffle)),
            FreeTextExercise f => new FreeTextSheet(
                f.Id, f.Instruction, f.Hint, f.MaxPoints,
                f.Fields.Select(x => x.Label).ToList()),
            MultipleChoiceExercise mc => new MultipleChoiceSheet(
                mc.Id, mc.Instruction, mc.Hint, mc.MaxPoints,
                mc.Questions.Select((q, i) => new QuestionSheet(i, q.Text, Numbered(q.Options, shuffle), q.Correct.Count > 1)).ToList()),
            _ => throw new NotSupportedException($"Unknown exercise type {exercise.GetType().Name}"),
        };
    }

    private static List<T> Shuffled<T>(List<T> items, Random random)
    {
        var copy = items.ToArray();
        random.Shuffle(copy);
        return [.. copy];
    }

    private static List<SheetItem> NotInOriginalOrder(List<SheetItem> steps, Random random)
    {
        while (steps.Count > 1 && steps.Select((s, i) => s.Index == i).All(x => x))
        {
            steps = Shuffled(steps, random);
        }

        return steps;
    }
}

/// <summary><c>Segments</c> is the text split at each gap: gap i sits between Segments[i] and Segments[i + 1].</summary>
public sealed record ClozeSheet(string Id, string Instruction, string? Hint, int MaxPoints, IReadOnlyList<string> Segments, IReadOnlyList<string>? WordBank)
    : ExerciseSheet(Id, Instruction, Hint, MaxPoints);

public sealed record MatchingSheet(string Id, string Instruction, string? Hint, int MaxPoints, IReadOnlyList<string> Options, IReadOnlyList<SheetItem> Rows)
    : ExerciseSheet(Id, Instruction, Hint, MaxPoints);

public sealed record OrderingSheet(string Id, string Instruction, string? Hint, int MaxPoints, IReadOnlyList<SheetItem> Steps)
    : ExerciseSheet(Id, Instruction, Hint, MaxPoints);

public sealed record TrueFalseSheet(string Id, string Instruction, string? Hint, int MaxPoints, IReadOnlyList<SheetItem> Statements)
    : ExerciseSheet(Id, Instruction, Hint, MaxPoints);

public sealed record FreeTextSheet(string Id, string Instruction, string? Hint, int MaxPoints, IReadOnlyList<string?> FieldLabels)
    : ExerciseSheet(Id, Instruction, Hint, MaxPoints);

public sealed record MultipleChoiceSheet(string Id, string Instruction, string? Hint, int MaxPoints, IReadOnlyList<QuestionSheet> Questions)
    : ExerciseSheet(Id, Instruction, Hint, MaxPoints);

public sealed record QuestionSheet(int Index, string Text, IReadOnlyList<SheetItem> Options, bool MultipleAnswers);
