using Lernkicker.Domain.Quizzes;
using Lernkicker.Tests;

namespace Lernkicker.Domain.Tests;

public class GraderTests
{
    private readonly Quiz _quiz = Examples.Hsu();

    [Fact]
    public void All_correct_gives_full_points()
    {
        var result = Grader.Grade(_quiz, Examples.AllCorrect(_quiz));

        Assert.Equal(39, result.Points);
        Assert.Equal(39, result.MaxPoints);
        Assert.Equal(100, result.Percent);
        Assert.Equal(1, result.Mark);
        Assert.All(result.Exercises.SelectMany(e => e.Parts), p => Assert.True(p.Correct));
    }

    [Fact]
    public void No_answers_give_zero_points()
    {
        var result = Grader.Grade(_quiz, new Dictionary<string, ExerciseAnswer>());

        Assert.Equal(0, result.Points);
        Assert.Equal(6, result.Mark);
        Assert.Equal(8, result.Exercises.Count);
        Assert.Equal(["cloze", "matching", "ordering", "trueFalse", "matching", "freeText", "matching", "multipleChoice"], result.Exercises.Select(e => e.Type));
    }

    [Theory]
    [InlineData(7, 4)]
    [InlineData(6, 3)]
    [InlineData(5, 3)]
    [InlineData(4, 2)]
    [InlineData(3, 2)]
    [InlineData(2, 1)]
    [InlineData(1, 1)]
    [InlineData(0, 0)]
    public void Points_are_scaled_rounding_half_up(int correct, int expected) =>
        Assert.Equal(expected, Grader.Points(correct, 7, 4));

    [Fact]
    public void Without_points_every_part_gives_one_point() =>
        Assert.Equal(5, Grader.Points(5, 7, null));

    [Fact]
    public void Ordering_counts_steps_in_the_right_position()
    {
        var exercise = _quiz.Exercises.Single(e => e.Id == "korn-zum-brot");

        // Steps 3 and 4 swapped: 5 of 7 in place
        var result = Grader.GradeExercise(exercise, new ExerciseAnswer { Order = [0, 1, 3, 2, 4, 5, 6] });

        Assert.Equal(3, result.Points);
        Assert.Equal(4, result.MaxPoints);
        Assert.Equal([true, true, false, false, true, true, true], result.Parts.Select(p => p.Correct));
        Assert.Equal("Körner reinigen", result.Parts[2].Solution);
    }

    [Fact]
    public void Free_text_in_any_order_counts_each_answer_once()
    {
        var exercise = _quiz.Exercises.Single(e => e.Id == "wahlgrundsaetze");

        var result = Grader.GradeExercise(exercise, new ExerciseAnswer { Texts = ["  Geheim ", "frei", "FREIE", "allgemeine", "bunt"] });

        Assert.Equal([true, true, false, true, false], result.Parts.Select(p => p.Correct));
        Assert.Equal(3, result.Points);
        // Wrong fields show the principles still missing
        Assert.Equal(["unmittelbar", "gleich"], result.Parts.Where(p => !p.Correct).Select(p => p.Solution));
    }

    [Fact]
    public void Matching_ignores_case_and_whitespace()
    {
        Assert.True(Grader.Matches("  ährchen ", ["Ährchen"]));
        Assert.True(Grader.Matches("Vom   Korn", ["vom Korn"]));
        Assert.False(Grader.Matches("", ["Ähre"]));
        Assert.False(Grader.Matches(null, ["Ähre"]));
        Assert.False(Grader.Matches("Ähren", ["Ähre"]));
    }

    [Fact]
    public void True_false_without_answer_is_wrong_and_carries_the_expected_truth()
    {
        var exercise = _quiz.Exercises.Single(e => e.Id == "muehle-und-baeckerei");

        var result = Grader.GradeExercise(exercise, new ExerciseAnswer { Truths = [false, true, null] });

        Assert.Equal([true, true, false, false, false, false], result.Parts.Select(p => p.Correct));
        Assert.Equal("Getreide wird im Sommer geerntet.", result.Parts[5].Explanation);
        // No text in the quiz's language: the UI says "true"/"false" in the user's language
        Assert.Null(result.Parts[0].Solution);
        Assert.Equal([false, true, true, false, true, false], result.Parts.Select(p => p.ExpectedTruth));
    }

    [Fact]
    public void Multiple_answers_need_exactly_the_correct_options()
    {
        var exercise = new MultipleChoiceExercise
        {
            Id = "m",
            Instruction = "Kreuze an.",
            Questions = [new ChoiceQuestion("Was ist Getreide?", ["Weizen", "Apfel", "Roggen"], [0, 2])],
        };

        Assert.True(Grader.GradeExercise(exercise, new ExerciseAnswer { Choices = [[2, 0]] }).Parts[0].Correct);
        Assert.False(Grader.GradeExercise(exercise, new ExerciseAnswer { Choices = [[0]] }).Parts[0].Correct);
        Assert.False(Grader.GradeExercise(exercise, new ExerciseAnswer { Choices = [[0, 1, 2]] }).Parts[0].Correct);
        Assert.Equal("Weizen, Roggen", Grader.GradeExercise(exercise, new ExerciseAnswer()).Parts[0].Solution);
    }

    [Theory]
    [InlineData(1.0, 1)]
    [InlineData(0.95, 1)]
    [InlineData(0.9, 2)]
    [InlineData(0.7, 3)]
    [InlineData(0.5, 4)]
    [InlineData(0.3, 5)]
    [InlineData(0.29, 6)]
    public void Mark_by_share(double share, int mark) => Assert.Equal(mark, Grader.Mark(share));
}
