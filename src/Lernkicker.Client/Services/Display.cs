using System.Globalization;
using System.Net;
using Lernkicker.Client.Resources;
using Lernkicker.Contracts;
using Microsoft.Extensions.Localization;

namespace Lernkicker.Client.Services;

/// <summary>Turns data into text in the user's language: dates, subjects, stars and error codes from the server.</summary>
public sealed class Display(IStringLocalizer<Strings> l)
{
    public static readonly IReadOnlyList<string> Subjects = ["science", "german", "math", "english", "religion", "ethics", "music", "art", "sports", "crafts", "other"];

    /// <summary>The kinds of calendar dates (EventKinds on the server).</summary>
    public static readonly IReadOnlyList<string> Kinds = ["test", "football", "other"];

    public string Date(DateOnly date) => date.ToString(l["Format.Date"], CultureInfo.CurrentCulture);

    public string Date(DateTimeOffset time) => time.ToLocalTime().ToString(l["Format.DateTime"], CultureInfo.CurrentCulture);

    public string Time(DateTimeOffset time) => time.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);

    /// <summary>"main · 8f86d6d · 25.10.2026 21:40": version, short commit and build time, as far as known.</summary>
    public string Build(BuildInfoDto build) => string.Join(" · ", new[]
    {
        build.Version,
        build.Commit is { Length: > 7 } commit ? commit[..7] : build.Commit,
        build.BuiltAt is { } builtAt ? Date(builtAt) : null,
    }.Where(part => !string.IsNullOrEmpty(part)));

    /// <summary>"today", "tomorrow", "in 3 days" or the date, for a due date.</summary>
    public string Due(DateOnly date)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        return (date.DayNumber - today.DayNumber) switch
        {
            < 0 => l["Due.Past", Date(date)],
            0 => l["Due.Today"],
            1 => l["Due.Tomorrow"],
            var days and < 7 => l["Due.InDays", days],
            _ => Date(date),
        };
    }

    /// <summary>"10:00–11:30", "10:00" or empty for a whole day.</summary>
    public static string Times(TimeOnly? start, TimeOnly? end) => (start, end) switch
    {
        ({ } s, { } e) => $"{s.ToString("t", CultureInfo.CurrentCulture)}–{e.ToString("t", CultureInfo.CurrentCulture)}",
        ({ } s, null) => s.ToString("t", CultureInfo.CurrentCulture),
        _ => "",
    };

    public string Kind(string kind) => Text($"Calendar.Kind.{kind}", kind);

    public static string KindIcon(string kind) => kind switch
    {
        "test" => "📝",
        "football" => "⚽",
        _ => "📅",
    };

    public static string Stars(int count) => new string('★', count) + new string('☆', 3 - count);

    public string Subject(string subject) => Text($"Subject.{subject}", subject);

    /// <summary>CSS class per subject, for the colour of its card.</summary>
    public static string SubjectClass(string subject) => subject switch
    {
        "science" or "german" or "math" or "english" => $"subject-{subject}",
        _ => "subject-other",
    };

    public string Praise(int percent) => percent switch
    {
        >= 95 => l["Praise.Perfect"],
        >= 83 => l["Praise.Great"],
        >= 67 => l["Praise.Good"],
        >= 50 => l["Praise.Half"],
        _ => l["Praise.KeepGoing"],
    };

    /// <summary>The messages to show for a failed call, translated from the server's codes.</summary>
    public IReadOnlyList<string> Errors(ApiException e)
    {
        if (e.Error is not { } error)
        {
            return [e.Status == HttpStatusCode.TooManyRequests ? l["Error.tooManyRequests"] : l["Error.generic"]];
        }

        if (error.Issues is { Count: > 0 } issues)
        {
            return [Text($"Error.{error.Code}", l["Error.quiz.invalid"]), .. issues.Select(Issue)];
        }

        return [Text($"Error.{error.Code}", l["Error.generic"])];
    }

    /// <summary>A problem in an uploaded quiz, e.g. "exercises/0/gaps: The text has 8 gaps (___), but gaps has 7 entries."</summary>
    public string Issue(ApiIssue issue)
    {
        var message = Text($"Issue.{issue.Code}", issue.Code);
        foreach (var (key, value) in issue.Args ?? new Dictionary<string, string>())
        {
            message = message.Replace("{" + key + "}", value);
        }

        return issue.Path.Length == 0 ? message : $"{issue.Path}: {message}";
    }

    private string Text(string key, string fallback)
    {
        var text = l[key];
        return text.ResourceNotFound ? fallback : text;
    }
}
