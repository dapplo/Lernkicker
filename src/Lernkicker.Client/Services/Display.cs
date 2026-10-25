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

    public string Date(DateOnly date) => date.ToString(l["Format.Date"], CultureInfo.CurrentCulture);

    public string Date(DateTimeOffset time) => time.ToLocalTime().ToString(l["Format.DateTime"], CultureInfo.CurrentCulture);

    public string Time(DateTimeOffset time) => time.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);

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
            return [l["Error.quiz.invalid"], .. issues.Select(Issue)];
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
