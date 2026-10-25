using Lernkicker.Domain.Quizzes;
using Lernkicker.Tests;

namespace Lernkicker.Domain.Tests;

public class QuizSheetTests
{
    private readonly Quiz _quiz = Examples.Hsu();

    [Fact]
    public void Sheet_types_have_no_solution_members()
    {
        // The sheet goes to the child's phone; solutions must not be reachable there
        var sheetTypes = typeof(QuizSheet).Assembly.GetTypes().Where(t => t.Name.EndsWith("Sheet") || t == typeof(SheetItem));
        var members = sheetTypes.SelectMany(t => t.GetProperties()).Select(p => p.Name).ToHashSet();

        Assert.DoesNotContain("Answers", members);
        Assert.DoesNotContain("Answer", members);
        Assert.DoesNotContain("Correct", members);
        Assert.DoesNotContain("IsTrue", members);
        Assert.DoesNotContain("Explanation", members);
        Assert.DoesNotContain("SolutionText", members);
    }

    [Fact]
    public void Cloze_text_is_split_at_the_gaps()
    {
        var cloze = Assert.IsType<ClozeSheet>(QuizSheet.For(_quiz, new Random(1)).Exercises[0]);

        Assert.Equal(9, cloze.Segments.Count);
        Assert.StartsWith("Bei Weizen", cloze.Segments[0]);
        Assert.Equal(8, cloze.WordBank!.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Ordering_is_never_already_solved(int seed)
    {
        var steps = Assert.IsType<OrderingSheet>(QuizSheet.For(_quiz, new Random(seed)).Exercises[2]).Steps;

        Assert.NotEqual(Enumerable.Range(0, 7), steps.Select(s => s.Index));
        Assert.Equal(Enumerable.Range(0, 7), steps.Select(s => s.Index).Order());
    }

    [Fact]
    public void Indices_point_to_the_original_parts()
    {
        var sheet = Assert.IsType<MatchingSheet>(QuizSheet.For(_quiz, new Random(7)).Exercises[1]);
        var original = (MatchingExercise)_quiz.Exercises[1];

        Assert.All(sheet.Rows, r => Assert.Equal(original.Rows[r.Index].Text, r.Text));
    }

    [Fact]
    public void Without_shuffle_the_order_stays()
    {
        var quiz = _quiz with { Shuffle = false };
        var statements = Assert.IsType<TrueFalseSheet>(QuizSheet.For(quiz, new Random(1)).Exercises[3]).Statements;

        Assert.Equal(Enumerable.Range(0, 6), statements.Select(s => s.Index));
    }

    [Fact]
    public void Sheet_carries_language_and_max_points()
    {
        var sheet = QuizSheet.For(_quiz, new Random(1));

        Assert.Equal("de", sheet.Language);
        Assert.Equal(39, sheet.MaxPoints);
    }
}
