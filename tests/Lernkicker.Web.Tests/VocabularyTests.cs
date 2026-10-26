using System.Net.Http.Json;
using Lernkicker.Client.Services;
using Lernkicker.Contracts;

namespace Lernkicker.Web.Tests;

/// <summary>Vocabulary lists: a lesson block to practise and a quiz exercise to test, both ways.</summary>
public sealed class VocabularyTests(LernkickerFactory factory) : IClassFixture<LernkickerFactory>
{
    private static string Example(string folder, string file) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, folder, file));

    [Fact]
    public async Task The_child_gets_the_words_with_their_languages()
    {
        var parent = await factory.NewFamilyAsync("vocabulary-lesson@example.org");
        var child = await TestData.CreateChildAsync(parent);
        var lesson = await LernkickerFactory.Json<LessonOverview>(await parent.PostAsync("/api/lessons", TestData.AsJson(Example("lessons", "other/dutch-animals.json"))));
        var assignment = await TestData.AssignLessonAsync(parent, child.Id, lesson.LessonId);
        var phone = await TestData.ChildPhoneAsync(factory, parent, child.Id);

        var toLearn = await LernkickerFactory.Json<LessonDto>(await phone.GetAsync($"/api/device/lessons/{assignment.Id}"));

        var block = toLearn.Blocks.Single(b => b.Type == "vocabulary");
        Assert.Equal(("nl", "de", "both", 12), (block.WordLanguage, block.TranslationLanguage, block.Direction, block.Words!.Count));
        Assert.Equal(("de kat", "die Katze"), (block.Words[1].Word, block.Words[1].Translation));
        Assert.Equal(["der Kater"], block.Words[1].TranslationAlternatives);
        Assert.Contains("crossword", toLearn.Games);
    }

    [Fact]
    public async Task The_test_asks_both_ways_without_sending_the_answers()
    {
        var parent = await factory.NewFamilyAsync("vocabulary-quiz@example.org");
        var quiz = await LernkickerFactory.Json<QuizOverview>(await parent.PostAsync("/api/quizzes", TestData.AsJson(Example("quizzes", "other/dutch-animals-test.json"))));
        var child = await TestData.CreateChildAsync(parent);
        await TestData.AssignAsync(parent, child.Id, quiz.QuizId);
        var phone = await TestData.ChildPhoneAsync(factory, parent, child.Id);
        var assignment = Assert.Single(await LernkickerFactory.Json<List<MyAssignment>>(await phone.GetAsync("/api/device/assignments")));

        var sheetJson = await (await phone.GetAsync($"/api/device/assignments/{assignment.AssignmentId}")).Content.ReadAsStringAsync();
        var sheet = System.Text.Json.JsonSerializer.Deserialize<SheetDto>(sheetJson, ApiJson.Options)!;
        var vocabulary = Assert.IsType<VocabularySheetDto>(Assert.Single(sheet.Exercises));
        Assert.Equal(("nl", "de", 16), (vocabulary.WordLanguage, vocabulary.TranslationLanguage, vocabulary.Questions.Count));
        Assert.Equal(("de hond", "der Hund"), (vocabulary.Questions.Single(q => q.Index == 0).Prompt, vocabulary.Questions.Single(q => q.Index == 8).Prompt));
        Assert.DoesNotContain("alternatives", sheetJson, StringComparison.OrdinalIgnoreCase);

        // One answer per question, by index: the first eight ask the German word, the last eight the Dutch one
        string?[] texts = ["der Hund", "Die Katze", "das pferd", null, "das Schwein", "der Vogel", "der Fisch", "das Kaninchen",
            "de hond", "de kat", "het paard", "de koe", "het varken", "de vogel", "de vis", "het konijn"];
        var result = await LernkickerFactory.Json<QuizResultDto>(await phone.PostAsJsonAsync(
            $"/api/device/assignments/{assignment.AssignmentId}/submit",
            new Submission(new Dictionary<string, AnswerDto> { ["animals"] = new() { Texts = texts } }), ApiJson.Options));

        Assert.Equal((14, 16), (result.Points, result.MaxPoints));
        Assert.Equal("das Pferd", result.Exercises[0].Parts[2].Solution);
    }

    [Theory]
    [InlineData("die Katze", AnswerCheck.Right)]
    [InlineData("der Kater", AnswerCheck.Right)]
    [InlineData("die katze", AnswerCheck.Almost)]
    [InlineData("Katze", AnswerCheck.Almost)]
    [InlineData("der Hund", AnswerCheck.Wrong)]
    public void Lesson_practice_checks_spelling_like_the_grader(string typed, AnswerCheck expected) =>
        Assert.Equal(expected, VocabularyPractice.Check(typed, new VocabularyCard(new VocabularyWordDto("de kat", "die Katze", null, ["der Kater"]), AsksWord: false)));

    [Fact]
    public void Practice_asks_each_word_once_and_brings_back_the_ones_not_known()
    {
        VocabularyWordDto[] words = [new("de hond", "der Hund"), new("de kat", "die Katze"), new("het paard", "das Pferd")];
        var practice = new VocabularyPractice(words, "translationToWord", new Random(3));

        Assert.All(Enumerable.Range(0, 3), _ => Assert.True(practice.Current!.AsksWord));
        var first = practice.Current!.Word;
        practice.Next(known: false);
        practice.Next(known: true);
        practice.Next(known: true);

        Assert.Equal((1, first), (practice.Left, practice.Current!.Word));
        practice.Next(known: true);
        Assert.Null(practice.Current);

        var both = new VocabularyPractice(words, "both", new Random(1));
        var ways = Enumerable.Range(0, 20).Select(_ => { var asks = both.Current!.AsksWord; both.Next(known: false); return asks; }).ToList();
        Assert.Contains(true, ways);
        Assert.Contains(false, ways);
    }
}
