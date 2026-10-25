using System.Globalization;
using System.Resources;
using System.Text;
using Lernkicker.Application;

namespace Lernkicker.Telegram;

/// <summary>
/// The bot's texts in the UI languages (<c>Resources/Messages*.resx</c>, German by default). Names, titles and
/// places come from the family's data and are sent as plain text, never as Markdown or HTML.
/// </summary>
public static class MessageTexts
{
    public const string DefaultLanguage = "de";

    public static readonly IReadOnlyList<string> Languages = ["de", "en"];

    private static readonly ResourceManager Texts = new("Lernkicker.Telegram.Resources.Messages", typeof(MessageTexts).Assembly);

    /// <summary>A supported language for a Telegram language code such as "en-GB", else German.</summary>
    public static string Language(string? code) =>
        Languages.FirstOrDefault(l => code?.StartsWith(l, StringComparison.OrdinalIgnoreCase) == true) ?? DefaultLanguage;

    public static IReadOnlyList<(string Command, string Description)> Commands(string language) =>
        [("next", Text("Command.Next", language)), ("stop", Text("Command.Stop", language)), ("help", Text("Command.Help", language))];

    public static string Format(Notification notification, string language) => notification switch
    {
        WelcomeNotification => Text("Welcome", language),
        LinkInvalidNotification => Text("LinkInvalid", language),
        LinkedNotification n => Text("Linked", language, n.ParentName),
        UnlinkedNotification => Text("Unlinked", language),
        HelpNotification => Text("Help", language),
        ResultNotification r => Text("Result", language, r.ChildName, r.QuizTitle, r.Points, r.MaxPoints, r.Percent, r.Mark),
        DatesNotification d => Dates(d, language),
        _ => throw new NotSupportedException($"No text for {notification.GetType().Name}"),
    };

    private static string Dates(DatesNotification dates, string language)
    {
        var tomorrow = dates.When == DatesNotification.Period.Tomorrow;
        if (dates.Children.Count == 0)
        {
            return Text("Dates.None", language);
        }

        var culture = Culture(language);
        var text = new StringBuilder(Text(tomorrow ? "Dates.Tomorrow" : "Dates.NextDays", language));
        foreach (var child in dates.Children)
        {
            text.Append("\n\n").Append(Text("Dates.Child", language, child.ChildName));
            foreach (var entry in child.Entries)
            {
                text.Append('\n').Append(Icon(entry.Kind)).Append(' ');
                if (!tomorrow)
                {
                    text.Append(entry.Date.ToString(Text("Format.Day", language), culture)).Append(' ');
                }

                if (entry.StartTime is { } start)
                {
                    text.Append(start.ToString("t", culture));
                    if (entry.EndTime is { } end)
                    {
                        text.Append('–').Append(end.ToString("t", culture));
                    }

                    text.Append(' ');
                }

                text.Append(entry.Title);
                if (entry.Location is { Length: > 0 } location)
                {
                    text.Append(" (").Append(location).Append(')');
                }
            }
        }

        return text.ToString();
    }

    private static string Icon(string kind) => kind switch
    {
        "test" => "📝",
        "football" => "⚽",
        _ => "📅",
    };

    private static string Text(string key, string language, params object[] args)
    {
        var culture = Culture(language);
        var text = Texts.GetString(key, culture) ?? throw new InvalidOperationException($"Missing text {key}");
        return args.Length == 0 ? text : string.Format(culture, text, args);
    }

    // The English texts are the neutral resources
    private static CultureInfo Culture(string language) => language == "en" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(language);
}
