using Lernkicker.Domain.Games;
using Lernkicker.Domain.Lessons;
using Lernkicker.Domain.Quizzes;
using Lernkicker.QuizFormat;
using Lernkicker.Tests;

namespace Lernkicker.Domain.Tests;

public class VocabularyTests
{
    private static readonly VocabularyWord[] Animals =
    [
        new("de hond", "der Hund"),
        new("de kat", "die Katze", TranslationAlternatives: ["der Kater"]),
        new("het paard", "das Pferd"),
    ];

    private static VocabularyExercise Test(VocabularyDirection direction) => new()
    {
        Id = "animals", Instruction = "Übersetze.", WordLanguage = "nl", Direction = direction, Words = Animals,
    };

    [Theory]
    [InlineData("der Hund", "der Hund", "Right")]
    [InlineData("  der   Hund. ", "der Hund", "Right")]
    [InlineData("Dog", "dog", "Right")]
    [InlineData("der hund", "der Hund", "Almost")]
    [InlineData("DER HUND", "der Hund", "Almost")]
    [InlineData("Hund", "der Hund", "Almost")]
    [InlineData("die Hund", "der Hund", "Almost")]
    [InlineData("het hond", "de hond", "Almost")]
    [InlineData("der Hunt", "der Hund", "Almost")]
    [InlineData("Kaninchn", "Kaninchen", "Almost")]
    [InlineData("Kanninchen", "Kaninchen", "Almost")]
    [InlineData("Kaninhcen", "Kaninchen", "Wrong")]
    [InlineData("vis", "vos", "Wrong")]
    [InlineData("die Katze", "der Hund", "Wrong")]
    [InlineData("", "der Hund", "Wrong")]
    public void Spelling_tells_right_from_almost_and_wrong(string typed, string answer, string expected) =>
        Assert.Equal(Enum.Parse<SpellingResult>(expected), Spelling.Check(typed, [answer]));

    [Fact]
    public void Both_directions_ask_every_word_twice()
    {
        var questions = Test(VocabularyDirection.Both).Questions;

        Assert.Equal(6, questions.Count);
        Assert.Equal(["de hond", "de kat", "het paard", "der Hund", "die Katze", "das Pferd"], questions.Select(q => q.Prompt));
        Assert.Equal(["die Katze", "der Kater"], questions[1].Accepted);
        Assert.Equal(["de kat"], questions[4].Accepted);
        Assert.Equal(6, Test(VocabularyDirection.Both).MaxPoints);
        Assert.Equal(3, Test(VocabularyDirection.TranslationToWord).MaxPoints);
        Assert.All(Test(VocabularyDirection.TranslationToWord).Questions, q => Assert.True(q.AsksWord));
    }

    [Fact]
    public void Only_right_spellings_get_points_and_the_solution_is_the_main_answer()
    {
        var result = Grader.GradeExercise(Test(VocabularyDirection.Both), new ExerciseAnswer
        {
            Texts = ["Der Hund", "der Kater", "das pferd", "de hond", null, "het paard"],
        });

        Assert.Equal([true, true, false, true, false, true], result.Parts.Select(p => p.Correct));
        Assert.Equal("das Pferd", result.Parts[2].Solution);
        Assert.Equal((4, 6), (result.Points, result.MaxPoints));
    }

    [Fact]
    public void The_sheet_shuffles_the_questions_and_hides_the_answers()
    {
        var quiz = new Quiz { Format = Quiz.FormatV1, Id = "dutch", Title = "Tiere", Subject = "other", Language = "de", Shuffle = false, Exercises = [Test(VocabularyDirection.Both)] };

        var sheet = Assert.IsType<VocabularySheet>(Assert.Single(QuizSheet.For(quiz, new Random(1)).Exercises));

        Assert.Equal(("nl", "de"), (sheet.WordLanguage, sheet.TranslationLanguage));
        Assert.Equal(Enumerable.Range(0, 6), sheet.Questions.Select(q => q.Index).Order());
        Assert.All(sheet.Questions, q => Assert.Equal(q.AsksWord, q.Index >= 3));
    }

    [Fact]
    public void The_example_quiz_and_lesson_read_with_their_directions()
    {
        var quiz = new QuizJson().Parse(File.ReadAllText(Examples.PathOf("other/dutch-animals-test.json")));
        Assert.True(quiz.IsValid, string.Join(Environment.NewLine, quiz.Issues));
        var exercise = Assert.IsType<VocabularyExercise>(Assert.Single(quiz.Quiz!.Exercises));
        Assert.Equal((VocabularyDirection.Both, 16), (exercise.Direction, exercise.MaxPoints));
        Assert.Equal(1, Grader.Grade(quiz.Quiz, Examples.AllCorrect(quiz.Quiz)).Percent / 100);

        var lesson = new LessonJson().Parse(File.ReadAllText(Examples.LessonPathOf("other/dutch-animals.json")));
        Assert.True(lesson.IsValid, string.Join(Environment.NewLine, lesson.Issues));
        var block = lesson.Lesson!.Blocks.OfType<VocabularyBlock>().Single();
        Assert.Equal(("nl", 12), (block.WordLanguage, block.Words.Count));
    }

    [Fact]
    public void A_one_way_direction_is_read_and_written_in_camel_case()
    {
        var quiz = new Quiz { Format = Quiz.FormatV1, Id = "dutch", Title = "Tiere", Subject = "other", Exercises = [Test(VocabularyDirection.WordToTranslation)] };

        var written = System.Text.Json.JsonSerializer.Serialize(quiz, QuizJson.Options);

        Assert.Contains("\"direction\":\"wordToTranslation\"", written);
        Assert.DoesNotContain("questions", written);
        var read = new QuizJson().Parse(written);
        Assert.True(read.IsValid, string.Join(Environment.NewLine, read.Issues));
    }

    [Fact]
    public void Vocabulary_words_are_game_pairs_with_the_word_to_find()
    {
        var lesson = new Lesson
        {
            Id = "dutch", Title = "Tiere", Subject = "other",
            Blocks = [new VocabularyBlock { Id = "animals", WordLanguage = "nl", Words = [.. Animals, new("de muis", "die Maus")] }],
        };

        Assert.Equal(new GamePair("de hond", "der Hund"), LessonGames.Pairs(lesson)[0]);
        Assert.Equal("hond", LessonGames.Word("de hond"));
        Assert.Equal("run", LessonGames.Word("to run"));
        Assert.Equal(GameKinds.All, LessonGames.Available(lesson));
    }
}
