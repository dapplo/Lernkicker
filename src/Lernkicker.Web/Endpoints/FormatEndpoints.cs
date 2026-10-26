using System.Text;

namespace Lernkicker.Web.Endpoints;

/// <summary>
/// The file formats for parents' AI assistants: both JSON schemas and a ready-to-paste prompt (docs/ai-prompt)
/// with the schemas appended. Public, as they hold no family data.
/// </summary>
public static class FormatEndpoints
{
    private const string Path = "/api/format";
    private static readonly string[] Schemas = ["quiz-v1.schema.json", "lesson-v1.schema.json"];
    private static readonly string[] PromptLanguages = ["en", "de"];

    public static void MapFormat(this IEndpointRouteBuilder app)
    {
        foreach (var schema in Schemas)
        {
            app.MapGet($"{Path}/{schema}", (HttpResponse response) =>
            {
                response.Headers.CacheControl = "no-cache";
                return Results.Text(Resource(schema), "application/schema+json", Encoding.UTF8);
            });
        }

        app.MapGet($"{Path}/ai-prompt", (string? lang, HttpResponse response) =>
        {
            var language = PromptLanguages.Contains(lang) ? lang! : PromptLanguages[0];
            response.Headers.CacheControl = "no-cache";
            response.Headers.ContentDisposition = $"attachment; filename=\"lernkicker-ai-prompt-{language}.md\"";
            return Results.Text(Prompt(language), "text/markdown", Encoding.UTF8);
        });
    }

    /// <summary>The prompt followed by both schemas, so one file is everything the AI needs.</summary>
    public static string Prompt(string language)
    {
        var prompt = new StringBuilder(Resource($"ai-prompt.{language}.md").TrimEnd()).AppendLine().AppendLine();
        foreach (var schema in Schemas)
        {
            prompt.AppendLine($"## {schema}").AppendLine().AppendLine("```json").AppendLine(Resource(schema).TrimEnd()).AppendLine("```").AppendLine();
        }

        return prompt.ToString().TrimEnd() + "\n";
    }

    private static string Resource(string name)
    {
        using var stream = typeof(FormatEndpoints).Assembly.GetManifestResourceStream($"format/{name}")
            ?? throw new InvalidOperationException($"Missing embedded resource format/{name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
