using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Json.Schema;
using Lernkicker.Application.Ports;
using Lernkicker.Domain.Lessons;
using Lernkicker.Domain.Quizzes;

namespace Lernkicker.QuizFormat;

/// <summary>Reads and validates lesson/v1 JSON against schema/lesson-v1.schema.json and <see cref="LessonRules"/>.</summary>
public sealed class LessonJson : ILessonFormat
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static readonly Lazy<JsonSchema> Schema = new(() => SchemaIssues.Load("lesson-v1.schema.json"));

    public LessonParseResult Parse(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            return new LessonParseResult(null, [QuizIssue.Of(QuizIssueCodes.InvalidJson, "", ("line", (e.LineNumber ?? 0) + 1), ("message", e.Message))]);
        }

        using (document)
        {
            var schemaIssues = SchemaIssues.Of(Schema.Value, document.RootElement);
            if (schemaIssues.Count > 0)
            {
                return new LessonParseResult(null, schemaIssues);
            }

            var lesson = document.RootElement.Deserialize<Lesson>(Options)
                ?? throw new InvalidOperationException("Schema-valid lesson deserialized to null.");
            var contentIssues = LessonRules.Check(lesson);
            return new LessonParseResult(contentIssues.Count > 0 ? null : lesson, contentIssues);
        }
    }

    public Lesson Read(string storedSource) =>
        JsonSerializer.Deserialize<Lesson>(storedSource, Options) ?? throw new InvalidOperationException("Stored lesson is empty.");

    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(BlockTypes);
        return new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            AllowOutOfOrderMetadataProperties = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = resolver,
        };
    }

    /// <summary>The "type" property selects the block class.</summary>
    private static void BlockTypes(JsonTypeInfo info)
    {
        if (info.Type != typeof(LessonBlock))
        {
            return;
        }

        info.PolymorphismOptions = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = "type",
            DerivedTypes =
            {
                new JsonDerivedType(typeof(TextBlock), "text"),
                new JsonDerivedType(typeof(PoemBlock), "poem"),
                new JsonDerivedType(typeof(FlashcardsBlock), "flashcards"),
                new JsonDerivedType(typeof(FactsBlock), "facts"),
            },
        };
    }
}
