using Lernkicker.Domain.Quizzes;

namespace Lernkicker.Domain.Tests;

public class WordSearchTests
{
    private static readonly WordSearchExercise Grains = new()
    {
        Id = "grains",
        Instruction = "Finde die Getreidesorten.",
        Words = ["Weizen", "Roggen", "Gerste", "Hafer", "Mais", "Dinkel", "Gräser"],
    };

    private static Quiz QuizWith(Exercise exercise) => new()
    {
        Format = Quiz.FormatV1, Id = "word-search", Title = "Suchsel", Subject = "science", Exercises = [exercise],
    };

    [Theory]
    [InlineData(1, false, false)]
    [InlineData(2, true, false)]
    [InlineData(3, true, true)]
    [InlineData(4, false, true)]
    public void Every_word_can_be_read_where_it_was_placed(int seed, bool diagonal, bool backwards)
    {
        var exercise = Grains with { Diagonal = diagonal, Backwards = backwards };
        var grid = WordSearchGrid.Generate(exercise, new Random(seed));

        Assert.Equal(exercise.Words.Count, grid.Placements.Count);
        Assert.All(grid.Rows, row => Assert.Equal(grid.Size, row.Length));
        foreach (var p in grid.Placements)
        {
            var word = WordSearchGrid.Letters(exercise.Words[p.Word]);
            var read = string.Concat(Enumerable.Range(0, word.Length).Select(i => grid.Rows[p.Row + i * p.RowStep][p.Column + i * p.ColumnStep]));
            Assert.Equal(word, read);
            Assert.True(diagonal || p.RowStep == 0 || p.ColumnStep == 0);
            Assert.True(backwards || (p.RowStep >= 0 && p.ColumnStep >= 0) || (p.RowStep == -1 && p.ColumnStep == 1));
        }
    }

    [Fact]
    public void Without_size_the_grid_fits_a_phone()
    {
        var grid = WordSearchGrid.Generate(Grains, new Random(1));

        Assert.InRange(grid.Size, WordSearchGrid.DefaultMinSize, WordSearchGrid.DefaultMaxSize);
    }

    [Fact]
    public void Umlauts_stay_one_letter_in_upper_case()
    {
        var grid = WordSearchGrid.Generate(Grains, new Random(5));

        Assert.Contains(grid.Rows, r => r.Contains('Ä'));
        Assert.DoesNotContain(grid.Rows, r => r.Any(char.IsLower));
    }

    [Fact]
    public void A_grid_that_is_too_small_grows()
    {
        var crowded = Grains with { Size = 5, Words = ["ABCDE", "FGHIJ", "KLMNO", "PQRST", "UVWXY", "ZZZZZ"] };

        var grid = WordSearchGrid.Generate(crowded, new Random(1));

        Assert.True(grid.Size > 5);
        Assert.Equal(6, grid.Placements.Count);
    }

    [Fact]
    public void Every_round_gets_a_new_grid()
    {
        var first = Assert.IsType<WordSearchSheet>(QuizSheet.For(QuizWith(Grains), new Random(1)).Exercises[0]);
        var second = Assert.IsType<WordSearchSheet>(QuizSheet.For(QuizWith(Grains), new Random(2)).Exercises[0]);

        Assert.NotEqual(first.Rows, second.Rows);
        Assert.Equal(Grains.Words.Order(), first.Words.Select(w => w.Text).Order());
        Assert.All(first.Words, w => Assert.Equal(Grains.Words[w.Index], w.Text));
    }

    [Fact]
    public void Each_found_word_is_a_point()
    {
        var answer = new ExerciseAnswer { Texts = ["Weizen", null, "GERSTE", null, "Mais", null, null] };

        var result = Grader.GradeExercise(Grains, answer);

        Assert.Equal("wordSearch", result.Type);
        Assert.Equal(3, result.Points);
        Assert.Equal(7, result.MaxPoints);
        Assert.Equal("Roggen", result.Parts[1].Solution);
    }

    [Fact]
    public void Words_must_be_letters_fit_the_grid_and_appear_once()
    {
        var exercise = Grains with { Size = 6, Words = ["Mäh-drescher", "Sonnenblume", "Hafer", "hafer"] };

        var issues = QuizRules.Check(QuizWith(exercise));

        Assert.Equal(
            [(QuizIssueCodes.WordNotLetters, "exercises/0/words/0"), (QuizIssueCodes.WordTooLong, "exercises/0/words/1"), (QuizIssueCodes.DuplicateWord, "exercises/0/words/3")],
            issues.Select(i => (i.Code, i.Path)));
        Assert.Equal("6", issues[1].Args!["size"]);
    }
}
