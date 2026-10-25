using System.Text.Json.Nodes;
using Lernkicker.Domain.Quizzes;
using Lernkicker.Tests;

namespace Lernkicker.QuizFormat.Tests;

public class QuizJsonTests
{
    private readonly QuizJson _format = new();

    public static TheoryData<string> Files() => [.. Examples.AllFiles().Select(p => Path.GetRelativePath(Examples.PathOf(""), p))];

    [Theory]
    [MemberData(nameof(Files))]
    public void Example_quizzes_are_valid(string file)
    {
        var result = _format.Parse(File.ReadAllText(Examples.PathOf(file)));
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
    }

    [Fact]
    public void Hsu_quiz_is_read_completely()
    {
        var quiz = Examples.Hsu();

        Assert.Equal("hsu-grain-and-elections", quiz.Id);
        Assert.Equal("science", quiz.Subject);
        Assert.Equal("de", quiz.Language);
        Assert.Equal(8, quiz.Exercises.Count);
        Assert.Equal(39, quiz.MaxPoints);
        var cloze = Assert.IsType<ClozeExercise>(quiz.Exercises[0]);
        Assert.Equal(8, cloze.Gaps.Count);
        var choice = Assert.IsType<MultipleChoiceExercise>(quiz.Exercises[7]);
        Assert.Equal([1], choice.Questions[1].Correct);
    }

    [Fact]
    public void Stored_source_reads_back_the_same_quiz()
    {
        var quiz = _format.Read(Examples.HsuJson());

        Assert.Equal(Examples.Hsu().MaxPoints, quiz.MaxPoints);
        Assert.Equal(Examples.Hsu().Exercises.Select(e => e.GetType()), quiz.Exercises.Select(e => e.GetType()));
    }

    [Fact]
    public void Language_defaults_to_german() =>
        Assert.Equal("de", Parse(q => q.Remove("language")).Quiz!.Language);

    [Fact]
    public void Broken_json_is_rejected()
    {
        var issue = Assert.Single(_format.Parse("{ \"format\": ").Issues);

        Assert.Equal(QuizIssueCodes.InvalidJson, issue.Code);
        Assert.Equal("1", issue.Args!["line"]);
    }

    [Theory]
    [InlineData("subject", "physics")]
    [InlineData("format", "quiz/v2")]
    [InlineData("id", "With spaces")]
    [InlineData("language", "German")]
    public void Schema_errors_are_reported_at_the_field(string field, string value)
    {
        var issue = Assert.Single(Parse(q => q[field] = value).Issues);

        Assert.Equal(QuizIssueCodes.Schema, issue.Code);
        Assert.Equal(field, issue.Path);
    }

    [Fact]
    public void Field_of_another_type_is_rejected()
    {
        var issues = Parse(q => q["exercises"]![2]!["type"] = "cloze").Issues;

        // Only the real problems, not the failed "if" checks of the other exercise types
        Assert.Equal(2, issues.Count);
        Assert.Contains(issues, i => i.Path == "exercises/2" && i.Args!["message"].Contains("text"));
        Assert.Contains(issues, i => i.Path == "exercises/2/steps" && i.Args!["message"] == "This property is not allowed here");
    }

    [Fact]
    public void Unknown_field_is_reported()
    {
        var issue = Assert.Single(Parse(q => q["colour"] = "blue").Issues);

        Assert.Equal("colour", issue.Path);
        Assert.Equal("This property is not allowed here", issue.Args!["message"]);
    }

    [Fact]
    public void Content_rules_run_after_the_schema()
    {
        var issues = Parse(q =>
        {
            q["exercises"]![0]!["gaps"]!.AsArray().RemoveAt(7);
            q["exercises"]![1]!["rows"]![0]!["answer"] = "Mais";
        }).Issues;

        Assert.Equal([QuizIssueCodes.GapCount, QuizIssueCodes.AnswerNotInOptions], issues.Select(i => i.Code));
    }

    [Fact]
    public void Several_correct_answers_as_list()
    {
        var quiz = Parse(q => q["exercises"]![7]!["questions"]![0]!["correct"] = new JsonArray(0, 2)).Quiz!;

        Assert.Equal([0, 2], ((MultipleChoiceExercise)quiz.Exercises[7]).Questions[0].Correct);
    }

    private Application.Ports.QuizParseResult Parse(Action<JsonObject> change)
    {
        var node = JsonNode.Parse(Examples.HsuJson())!.AsObject();
        change(node);
        return _format.Parse(node.ToJsonString());
    }
}
