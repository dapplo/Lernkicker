using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lernkicker.Application;
using Lernkicker.Domain.Quizzes;

namespace Lernkicker.Web.Tests;

/// <summary>
/// The server only sends codes; the client must have a text for each of them in every UI language, and both
/// languages must have the same texts with the same placeholders.
/// </summary>
public partial class LocalizationTests
{
    private static readonly ResourceManager Strings = new("Lernkicker.Client.Resources.Strings", typeof(Client.Resources.Strings).Assembly);

    private static readonly Dictionary<string, string> English = Texts(CultureInfo.InvariantCulture);
    private static readonly Dictionary<string, string> German = Texts(CultureInfo.GetCultureInfo("de"));

    [Fact]
    public void German_and_english_have_the_same_texts() =>
        Assert.Equal(English.Keys.Order(), German.Keys.Order());

    [Fact]
    public void Translations_keep_their_placeholders()
    {
        var mismatches = English.Keys
            .Where(key => !Placeholders(English[key]).SetEquals(Placeholders(German[key])))
            .ToList();

        Assert.Empty(mismatches);
    }

    [Fact]
    public void Every_error_code_has_a_text() =>
        Assert.DoesNotContain(Constants(typeof(ErrorCodes)), c => c != ErrorCodes.AccountPrefix && !English.ContainsKey($"Error.{c}"));

    [Fact]
    public void Every_quiz_issue_has_a_text() =>
        Assert.DoesNotContain(Constants(typeof(QuizIssueCodes)), c => !English.ContainsKey($"Issue.{c}"));

    [Fact]
    public void Every_subject_of_the_format_has_a_name()
    {
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "quiz-v1.schema.json")));
        var subjects = schema.RootElement.GetProperty("properties").GetProperty("subject").GetProperty("enum").EnumerateArray().Select(s => s.GetString()!);

        Assert.DoesNotContain(subjects, s => !English.ContainsKey($"Subject.{s}"));
    }

    [Fact]
    public void Every_lesson_block_type_and_subject_has_a_name()
    {
        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "lesson-v1.schema.json")));
        var types = schema.RootElement.GetProperty("$defs").GetProperty("block").GetProperty("properties").GetProperty("type").GetProperty("enum")
            .EnumerateArray().Select(t => t.GetString()!);
        var subjects = schema.RootElement.GetProperty("properties").GetProperty("subject").GetProperty("enum").EnumerateArray().Select(s => s.GetString()!);

        Assert.DoesNotContain(types, t => !English.ContainsKey($"Learn.Type.{t}"));
        Assert.DoesNotContain(subjects, s => !English.ContainsKey($"Subject.{s}"));
    }

    [Fact]
    public void Every_calendar_source_has_a_name() =>
        Assert.DoesNotContain(Constants(typeof(EntrySources)), s => s != EntrySources.Manual && !English.ContainsKey($"Calendar.Source.{s}"));

    [Fact]
    public void Every_calendar_kind_has_a_name()
    {
        Assert.Equal(Lernkicker.Domain.Calendar.EventKinds.All, Client.Services.Display.Kinds);
        Assert.DoesNotContain(Lernkicker.Domain.Calendar.EventKinds.All, k => !English.ContainsKey($"Calendar.Kind.{k}"));
    }

    [Fact]
    public void Every_theme_has_a_name_and_a_cheer()
    {
        Assert.Equal(Lernkicker.Domain.Families.ChildThemes.All, Client.Services.Theme.All.Select(t => t.Id));
        Assert.DoesNotContain(Lernkicker.Domain.Families.ChildThemes.All, t => !English.ContainsKey($"Theme.{t}") || !English.ContainsKey($"Game.Caught.{t}"));
    }

    private static Dictionary<string, string> Texts(CultureInfo culture) =>
        Strings.GetResourceSet(culture, createIfNotExists: true, tryParents: false)!
            .Cast<DictionaryEntry>()
            .ToDictionary(e => (string)e.Key, e => (string)e.Value!);

    private static IEnumerable<string> Constants(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);

    private static HashSet<string> Placeholders(string text) => Placeholder().Matches(text).Select(m => m.Value).ToHashSet();

    [GeneratedRegex(@"\{[A-Za-z0-9]+\}")]
    private static partial Regex Placeholder();
}
