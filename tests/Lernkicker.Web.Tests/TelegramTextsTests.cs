using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Lernkicker.Application;
using Lernkicker.Telegram;

namespace Lernkicker.Web.Tests;

/// <summary>The bot's texts: every notification has a text in every language, and both resx files agree.</summary>
public class TelegramTextsTests
{
    private static readonly CalendarEntry Test = new("a", "test", "HSU-Probe", new DateOnly(2026, 10, 27), new TimeOnly(8, 0), new TimeOnly(8, 45), null, "manual", null);
    private static readonly CalendarEntry Match = new("b", "football", "ASV Zirndorf – SpVgg", new DateOnly(2026, 10, 28), new TimeOnly(17, 30), null, "Sportpark", "ics", null);

    public static TheoryData<string> Languages() => [.. MessageTexts.Languages];

    private static readonly Notification[] All =
    [
        new WelcomeNotification(), new LinkInvalidNotification(), new LinkedNotification("Robin"), new UnlinkedNotification(),
        new HelpNotification(), new ResultNotification("Calvin", "Wasser", 17, 20, 85, 2),
        new DatesNotification(DatesNotification.Period.Tomorrow, [new ChildDates("Calvin", [Test])]),
        new DatesNotification(DatesNotification.Period.NextDays, [new ChildDates("Calvin", [Test, Match])]),
        new DatesNotification(DatesNotification.Period.NextDays, []),
    ];

    [Theory]
    [MemberData(nameof(Languages))]
    public void Every_notification_has_a_text(string language)
    {
        foreach (var notification in All)
        {
            var text = MessageTexts.Format(notification, language);
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.DoesNotMatch(@"\{\d\}", text);
        }

        Assert.All(MessageTexts.Commands(language), c => Assert.False(string.IsNullOrWhiteSpace(c.Description)));
    }

    [Fact]
    public void Texts_fill_in_names_numbers_and_dates()
    {
        var result = MessageTexts.Format(new ResultNotification("Calvin", "Wasser", 17, 20, 85, 2), "de");
        Assert.Contains("Calvin", result);
        Assert.Contains("Wasser", result);
        Assert.Contains("17", result);

        var dates = MessageTexts.Format(new DatesNotification(DatesNotification.Period.NextDays, [new ChildDates("Calvin", [Test, Match])]), "de");
        Assert.Contains("📝", dates);
        Assert.Contains("08:00–08:45 HSU-Probe", dates);
        Assert.Contains("⚽", dates);
        Assert.Contains("17:30 ASV Zirndorf – SpVgg (Sportpark)", dates);
    }

    [Fact]
    public void The_admin_token_turns_the_bot_on_and_off_unless_a_file_sets_it()
    {
        var fromApp = new TelegramMessenger(null);
        Assert.False(fromApp.IsEnabled);
        Assert.False(fromApp.TokenFromConfiguration);
        fromApp.UseToken("1:abc");
        Assert.True(fromApp.IsEnabled);
        fromApp.UseToken(null);
        Assert.False(fromApp.IsEnabled);

        var fromFile = new TelegramMessenger("1:abc");
        Assert.True(fromFile.TokenFromConfiguration);
        fromFile.UseToken(null);
        Assert.True(fromFile.IsEnabled);
    }

    [Theory]
    [InlineData("de-DE", "de")]
    [InlineData("en-GB", "en")]
    [InlineData("fr", "de")]
    [InlineData(null, "de")]
    public void Telegram_languages_map_to_ui_languages(string? code, string expected) =>
        Assert.Equal(expected, MessageTexts.Language(code));

    [Fact]
    public void Both_resx_files_have_the_same_keys_and_placeholders()
    {
        var english = Read(CultureInfo.InvariantCulture);
        var german = Read(CultureInfo.GetCultureInfo("de"));

        Assert.Equal(english.Keys.Order(), german.Keys.Order());
        foreach (var (key, value) in english)
        {
            Assert.Equal(Placeholders(value), Placeholders(german[key]));
        }
    }

    private static Dictionary<string, string> Read(CultureInfo culture) =>
        new ResourceManager("Lernkicker.Telegram.Resources.Messages", typeof(MessageTexts).Assembly)
            .GetResourceSet(culture, createIfNotExists: true, tryParents: false)!
            .Cast<DictionaryEntry>()
            .ToDictionary(e => (string)e.Key, e => (string)e.Value!);

    private static List<string> Placeholders(string text) =>
        Regex.Matches(text, @"\{\d+[^}]*\}").Select(m => m.Value).Distinct().Order().ToList();
}
