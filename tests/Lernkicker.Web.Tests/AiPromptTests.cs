using System.Net;
using System.Text.RegularExpressions;
using Lernkicker.QuizFormat;

namespace Lernkicker.Web.Tests;

/// <summary>Parents get the formats and a prompt for their AI assistant (docs/ai-prompt), without logging in.</summary>
public sealed partial class AiPromptTests(LernkickerFactory factory) : IClassFixture<LernkickerFactory>
{
    [Theory]
    [InlineData("quiz-v1.schema.json")]
    [InlineData("lesson-v1.schema.json")]
    public async Task The_schemas_are_served_as_they_are_in_the_repository(string schema)
    {
        var response = await factory.Client().GetAsync($"/api/format/{schema}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/schema+json", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, schema)), await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("de", "Mein Auftrag")]
    [InlineData("en", "My request")]
    [InlineData(null, "My request")]
    [InlineData("fr", "My request")]
    public async Task The_prompt_comes_in_the_ui_language_with_both_schemas(string? language, string heading)
    {
        var response = await factory.Client().GetAsync("/api/format/ai-prompt" + (language is null ? "" : $"?lang={language}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/markdown", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        var prompt = await response.Content.ReadAsStringAsync();
        Assert.Contains($"## {heading}", prompt);
        Assert.Contains(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "quiz-v1.schema.json")).TrimEnd(), prompt);
        Assert.Contains(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "lesson-v1.schema.json")).TrimEnd(), prompt);
    }

    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    public void The_examples_in_the_prompt_are_valid(string language)
    {
        var examples = JsonBlock().Matches(Endpoints.FormatEndpoints.Prompt(language))
            .Select(m => m.Groups[1].Value)
            .Where(json => json.Contains("\"format\": \"quiz/v1\"") || json.Contains("\"format\": \"lesson/v1\""))
            .ToList();

        Assert.Equal(2, examples.Count);
        var quiz = new QuizJson().Parse(examples.Single(e => e.Contains("quiz/v1")));
        Assert.True(quiz.IsValid, string.Join(Environment.NewLine, quiz.Issues));
        var lesson = new LessonJson().Parse(examples.Single(e => e.Contains("lesson/v1")));
        Assert.True(lesson.IsValid, string.Join(Environment.NewLine, lesson.Issues));
    }

    [GeneratedRegex(@"```json\n(\{.*?\n\})\n```", RegexOptions.Singleline)]
    private static partial Regex JsonBlock();
}
