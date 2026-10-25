using System.Text.Json.Nodes;
using Lernkicker.Application.Ports;
using Lernkicker.Domain.Lessons;
using Lernkicker.Domain.Quizzes;
using Lernkicker.Tests;

namespace Lernkicker.QuizFormat.Tests;

public class LessonJsonTests
{
    private readonly LessonJson _format = new();

    public static TheoryData<string> Files() => [.. Examples.AllLessonFiles().Select(p => Path.GetRelativePath(Examples.LessonPathOf(""), p))];

    [Theory]
    [MemberData(nameof(Files))]
    public void Example_lessons_are_valid(string file)
    {
        var result = _format.Parse(File.ReadAllText(Examples.LessonPathOf(file)));
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
    }

    [Fact]
    public void Poem_lesson_is_read_completely()
    {
        var lesson = _format.Parse(Examples.PoemJson()).Lesson!;

        Assert.Equal(("poem-septembermorgen", "german", 4, "de"), (lesson.Id, lesson.Subject, lesson.GradeLevel, lesson.Language));
        Assert.Equal(["tips", "poem", "terms", "facts"], lesson.Blocks.Select(b => b.Id));
        Assert.Contains("**zwei Zeilen**", Assert.IsType<TextBlock>(lesson.Blocks[0]).Text);
        var poem = Assert.IsType<PoemBlock>(lesson.Blocks[1]);
        Assert.Equal("Eduard Mörike", poem.Author);
        Assert.Equal(6, Assert.Single(poem.Stanzas).Count);
        Assert.Equal("Vers", Assert.IsType<FlashcardsBlock>(lesson.Blocks[2]).Cards[0].Front);
        Assert.Equal(3, Assert.IsType<FactsBlock>(lesson.Blocks[3]).Items.Count);
    }

    [Fact]
    public void Stored_source_reads_back_the_same_lesson()
    {
        var lesson = _format.Read(Examples.PoemJson());

        Assert.Equal(4, lesson.Blocks.Count);
        Assert.IsType<PoemBlock>(lesson.Blocks[1]);
    }

    [Fact]
    public void Broken_json_is_reported()
    {
        var issue = Assert.Single(_format.Parse("{ \"format\": ").Issues);

        Assert.Equal(QuizIssueCodes.InvalidJson, issue.Code);
    }

    [Fact]
    public void A_quiz_is_not_a_lesson()
    {
        var result = _format.Parse(Examples.HsuJson());

        Assert.False(result.IsValid);
        Assert.All(result.Issues, i => Assert.Equal(QuizIssueCodes.Schema, i.Code));
    }

    [Fact]
    public void Unknown_block_fields_are_schema_errors()
    {
        var issues = Parse(l => l["blocks"]![1]!["lines"] = new JsonArray("a")).Issues;

        Assert.NotEmpty(issues);
        Assert.All(issues, i => Assert.Equal(QuizIssueCodes.Schema, i.Code));
    }

    [Fact]
    public void Duplicate_block_ids_are_reported()
    {
        var issue = Assert.Single(Parse(l => l["blocks"]![3]!["id"] = "poem").Issues);

        Assert.Equal((QuizIssueCodes.DuplicateId, "blocks/3/id"), (issue.Code, issue.Path));
    }

    private LessonParseResult Parse(Action<JsonObject> change)
    {
        var json = JsonNode.Parse(Examples.PoemJson())!.AsObject();
        change(json);
        return _format.Parse(json.ToJsonString());
    }
}
