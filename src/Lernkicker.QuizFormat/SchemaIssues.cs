using System.Text.Json;
using Json.Schema;
using Lernkicker.Domain.Quizzes;

namespace Lernkicker.QuizFormat;

/// <summary>Validates JSON against one of the embedded schemas and reports the failures as issues.</summary>
internal static class SchemaIssues
{
    public static JsonSchema Load(string name)
    {
        using var stream = typeof(SchemaIssues).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded schema {name} is missing.");
        using var reader = new StreamReader(stream);
        return JsonSchema.FromText(reader.ReadToEnd());
    }

    public static List<QuizIssue> Of(JsonSchema schema, JsonElement root)
    {
        var results = schema.Evaluate(root, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        var issues = new List<QuizIssue>();
        Collect(results, issues);
        return issues.DistinctBy(i => (i.Path, i.Args?["message"])).ToList();
    }

    // Keywords whose error only summarises failures that are reported further down the tree.
    private static readonly HashSet<string> SummaryKeywords =
        ["properties", "additionalProperties", "items", "prefixItems", "allOf", "anyOf", "oneOf", "if", "then", "else", "$ref"];

    // Walks only failing nodes, so failed "if" conditions and the passing branch of oneOf stay out of the report.
    private static void Collect(EvaluationResults node, List<QuizIssue> issues)
    {
        if (node.IsValid)
        {
            return;
        }

        foreach (var (keyword, message) in node.Errors ?? [])
        {
            if (!SummaryKeywords.Contains(keyword))
            {
                issues.Add(QuizIssue.Of(QuizIssueCodes.Schema, node.InstanceLocation.ToString().TrimStart('/'), ("keyword", keyword), ("message", Clarify(message))));
            }
        }

        foreach (var child in node.Details ?? [])
        {
            Collect(child, issues);
        }
    }

    private static string Clarify(string message) =>
        message.StartsWith("All values fail against the false schema", StringComparison.Ordinal)
            ? "This property is not allowed here"
            : message;
}
