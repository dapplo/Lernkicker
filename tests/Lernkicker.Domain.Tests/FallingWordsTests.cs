using Lernkicker.Domain.Quizzes;

namespace Lernkicker.Domain.Tests;

public class FallingWordsTests
{
    private static readonly FallingWordsExercise Food = new()
    {
        Id = "food",
        Instruction = "Fang die Wörter!",
        Options = ["Getreide", "Gemüse", "Obst"],
        Words =
        [
            new MatchingRow("Weizen", "Getreide"),
            new MatchingRow("Karotte", "Gemüse"),
            new MatchingRow("Apfel", "Obst"),
            new MatchingRow("Mais", "Getreide", "Mais ist ein Getreide."),
        ],
    };

    private static Quiz QuizWith(Exercise exercise) => new()
    {
        Format = Quiz.FormatV1, Id = "falling-words", Title = "Fang die Wörter", Subject = "science", Exercises = [exercise], Shuffle = false,
    };

    [Theory]
    [InlineData(GameLevel.Easy, 2, 10)]
    [InlineData(GameLevel.Medium, 3, 8)]
    [InlineData(GameLevel.Hard, 3, 6)]
    public void The_level_sets_the_options_shown_and_the_speed(GameLevel level, int targets, int seconds)
    {
        var exercise = Food with { Level = level };

        // Hard shows four, but there are only three options
        Assert.Equal((targets, seconds), (exercise.TargetCount, exercise.FallSeconds));
    }

    [Fact]
    public void The_sheet_carries_the_answers_and_shuffles_the_words_even_without_shuffle()
    {
        var orders = Enumerable.Range(1, 10)
            .Select(seed => Assert.IsType<FallingWordsSheet>(QuizSheet.For(QuizWith(Food), new Random(seed)).Exercises[0]))
            .ToList();

        Assert.All(orders, sheet => Assert.All(sheet.Words, w => Assert.Equal(Food.Words[w.Index].Answer, w.Answer)));
        Assert.True(orders.Select(s => string.Join(",", s.Words.Select(w => w.Index))).Distinct().Count() > 1);
        Assert.Equal(Food.Options, orders[0].Options);
    }

    [Fact]
    public void Each_word_on_its_option_is_a_point()
    {
        var answer = new ExerciseAnswer { Texts = ["Getreide", "Obst", null, "getreide"] };

        var result = Grader.GradeExercise(Food, answer);

        Assert.Equal("fallingWords", result.Type);
        Assert.Equal(2, result.Points);
        Assert.Equal(4, result.MaxPoints);
        Assert.Equal("Gemüse", result.Parts[1].Solution);
        Assert.Equal("Mais ist ein Getreide.", result.Parts[3].Explanation);
    }

    [Fact]
    public void Every_answer_must_be_an_option()
    {
        var exercise = Food with { Words = [.. Food.Words, new MatchingRow("Tomate", "Frucht")] };

        var issues = QuizRules.Check(QuizWith(exercise));

        Assert.Equal([(QuizIssueCodes.AnswerNotInOptions, "exercises/0/words/4/answer")], issues.Select(i => (i.Code, i.Path)));
    }
}
