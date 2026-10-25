using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Json.Schema;
using Lernkicker.Application.Ports;
using Lernkicker.Domain.Quizzes;

namespace Lernkicker.QuizFormat;

/// <summary>
/// Reads and validates quiz/v1 JSON: first against schema/quiz-v1.schema.json, then the content rules the
/// schema cannot express (<see cref="QuizRules"/>). The JSON mapping is configured here, so the domain types
/// stay free of serialization attributes.
/// </summary>
public sealed class QuizJson : IQuizFormat
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static readonly Lazy<JsonSchema> Schema = new(LoadSchema);

    public QuizParseResult Parse(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            return Invalid(QuizIssue.Of(QuizIssueCodes.InvalidJson, "", ("line", (e.LineNumber ?? 0) + 1), ("message", e.Message)));
        }

        using (document)
        {
            var schemaIssues = SchemaIssues(document.RootElement);
            if (schemaIssues.Count > 0)
            {
                return new QuizParseResult(null, schemaIssues);
            }

            var quiz = document.RootElement.Deserialize<Quiz>(Options)
                ?? throw new InvalidOperationException("Schema-valid quiz deserialized to null.");
            var contentIssues = QuizRules.Check(quiz);
            return new QuizParseResult(contentIssues.Count > 0 ? null : quiz, contentIssues);
        }
    }

    public Quiz Read(string storedSource) =>
        JsonSerializer.Deserialize<Quiz>(storedSource, Options) ?? throw new InvalidOperationException("Stored quiz is empty.");

    private static QuizParseResult Invalid(QuizIssue issue) => new(null, [issue]);

    private static List<QuizIssue> SchemaIssues(JsonElement root)
    {
        var results = Schema.Value.Evaluate(root, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
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

    private static JsonSchema LoadSchema()
    {
        using var stream = typeof(QuizJson).Assembly.GetManifestResourceStream("quiz-v1.schema.json")
            ?? throw new InvalidOperationException("Embedded schema quiz-v1.schema.json is missing.");
        using var reader = new StreamReader(stream);
        return JsonSchema.FromText(reader.ReadToEnd());
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(ExerciseTypes);
        resolver.Modifiers.Add(FormatDetails);
        return new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            AllowOutOfOrderMetadataProperties = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = resolver,
        };
    }

    /// <summary>The "type" property selects the exercise class.</summary>
    private static void ExerciseTypes(JsonTypeInfo info)
    {
        if (info.Type != typeof(Exercise))
        {
            return;
        }

        info.PolymorphismOptions = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = "type",
            DerivedTypes =
            {
                new JsonDerivedType(typeof(ClozeExercise), "cloze"),
                new JsonDerivedType(typeof(MatchingExercise), "matching"),
                new JsonDerivedType(typeof(OrderingExercise), "ordering"),
                new JsonDerivedType(typeof(TrueFalseExercise), "trueFalse"),
                new JsonDerivedType(typeof(FreeTextExercise), "freeText"),
                new JsonDerivedType(typeof(MultipleChoiceExercise), "multipleChoice"),
            },
        };
    }

    private static void FormatDetails(JsonTypeInfo info)
    {
        if (info.Type == typeof(ChoiceQuestion))
        {
            info.Properties.Single(p => p.Name == "correct").CustomConverter = new IndexListConverter();
        }

        // Computed properties are not part of the format
        foreach (var computed in info.Properties.Where(p => p.Name is "maxPoints" or "partCount" or "gapsInText").ToList())
        {
            info.Properties.Remove(computed);
        }
    }
}

/// <summary>Reads "correct" as either a single index or a list of indices.</summary>
public sealed class IndexListConverter : JsonConverter<IReadOnlyList<int>>
{
    public override IReadOnlyList<int> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Number
            ? [reader.GetInt32()]
            : JsonSerializer.Deserialize<List<int>>(ref reader, options) ?? [];

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<int> value, JsonSerializerOptions options)
    {
        if (value.Count == 1)
        {
            writer.WriteNumberValue(value[0]);
        }
        else
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}
