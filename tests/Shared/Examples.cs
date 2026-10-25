using Lernkicker.Domain.Quizzes;
using Lernkicker.QuizFormat;

namespace Lernkicker.Tests;

/// <summary>The example quizzes from /quizzes and lessons from /lessons, copied next to the test assembly.</summary>
internal static class Examples
{
    public const string HsuFile = "hsu/grain-and-elections.json";

    public static string PathOf(string relative) => Path.Combine(AppContext.BaseDirectory, "quizzes", relative);

    public static IEnumerable<string> AllFiles() =>
        Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "quizzes"), "*.json", SearchOption.AllDirectories);

    public static string HsuJson() => File.ReadAllText(PathOf(HsuFile));

    public const string PoemFile = "german/poem-septembermorgen.json";

    /// <summary>The example lessons from /lessons.</summary>
    public static string LessonPathOf(string relative) => Path.Combine(AppContext.BaseDirectory, "lessons", relative);

    public static IEnumerable<string> AllLessonFiles() =>
        Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "lessons"), "*.json", SearchOption.AllDirectories);

    public static string PoemJson() => File.ReadAllText(LessonPathOf(PoemFile));

    public static Quiz Hsu()
    {
        var result = new QuizJson().Parse(HsuJson());
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        return result.Quiz!;
    }

    /// <summary>The answers a child would give if everything is right.</summary>
    public static Dictionary<string, ExerciseAnswer> AllCorrect(Quiz quiz) =>
        quiz.Exercises.ToDictionary(e => e.Id, e => e switch
        {
            ClozeExercise c => new ExerciseAnswer { Texts = c.Gaps.Select(x => (string?)x.Answers[0]).ToList() },
            MatchingExercise m => new ExerciseAnswer { Texts = m.Rows.Select(x => (string?)x.Answer).ToList() },
            OrderingExercise o => new ExerciseAnswer { Order = Enumerable.Range(0, o.Steps.Count).ToList() },
            TrueFalseExercise t => new ExerciseAnswer { Truths = t.Statements.Select(x => (bool?)x.IsTrue).ToList() },
            FreeTextExercise f => new ExerciseAnswer { Texts = f.Fields.Select(x => (string?)x.Answers[0]).ToList() },
            MultipleChoiceExercise q => new ExerciseAnswer { Choices = q.Questions.Select(x => (IReadOnlyList<int>?)x.Correct).ToList() },
            _ => throw new NotSupportedException(),
        });
}
