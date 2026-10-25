using Lernkicker.Application;
using Lernkicker.Application.Ports;

namespace Lernkicker.Calendar.Tests;

public class IcsTests
{
    private const string Berlin = "Europe/Berlin";
    private static readonly DateOnly From = new(2026, 10, 1);
    private static readonly DateOnly To = new(2026, 12, 31);

    private static string Ics(params string[] events) =>
        string.Join("\r\n", ["BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//test//EN", .. events, "END:VCALENDAR"]) + "\r\n";

    private static string Event(params string[] lines) => string.Join("\r\n", ["BEGIN:VEVENT", .. lines, "END:VEVENT"]);

    [Fact]
    public void Text_that_is_no_calendar_is_refused()
    {
        Assert.Null(IcsCalendarSource.Read("<html>Login</html>", From, To, Berlin));
        Assert.Null(IcsCalendarSource.Read("", From, To, Berlin));
    }

    [Fact]
    public void Utc_times_are_shown_in_the_family_time_zone()
    {
        var ics = Ics(
            Event("UID:summer", "DTSTART:20261024T080000Z", "DTEND:20261024T093000Z", "SUMMARY:Spiel vor der Zeitumstellung", "LOCATION:Zirndorf"),
            Event("UID:winter", "DTSTART:20261107T090000Z", "DTEND:20261107T103000Z", "SUMMARY:Spiel nach der Zeitumstellung"));

        var events = IcsCalendarSource.Read(ics, From, To, Berlin)!;

        Assert.Equal(
            [
                new ImportedEvent("summer/202610241000", "Spiel vor der Zeitumstellung", new DateOnly(2026, 10, 24), new TimeOnly(10, 0), new TimeOnly(11, 30), "Zirndorf"),
                new ImportedEvent("winter/202611071000", "Spiel nach der Zeitumstellung", new DateOnly(2026, 11, 7), new TimeOnly(10, 0), new TimeOnly(11, 30), null),
            ],
            events);
    }

    [Fact]
    public void Times_with_a_time_zone_and_floating_times_are_read()
    {
        var ics = Ics(
            Event("UID:a", "DTSTART;TZID=America/New_York:20261110T040000", "SUMMARY:New York"),
            Event("UID:b", "DTSTART:20261111T170000", "SUMMARY:Floating"));

        var events = IcsCalendarSource.Read(ics, From, To, Berlin)!;

        Assert.Equal([(new DateOnly(2026, 11, 10), (TimeOnly?)new TimeOnly(10, 0)), (new DateOnly(2026, 11, 11), new TimeOnly(17, 0))],
            events.Select(e => (e.Date, e.StartTime)));
    }

    [Fact]
    public void Whole_days_and_several_days_have_no_times()
    {
        var ics = Ics(
            Event("UID:day", "DTSTART;VALUE=DATE:20261103", "DTEND;VALUE=DATE:20261104", "SUMMARY:Wandertag"),
            Event("UID:camp", "DTSTART:20261120T150000Z", "DTEND:20261122T120000Z", "SUMMARY:Trainingslager"));

        var events = IcsCalendarSource.Read(ics, From, To, Berlin)!;

        Assert.Equal(("Wandertag", null, null), (events[0].Title, events[0].StartTime, events[0].EndTime));
        Assert.Equal(("Trainingslager", (TimeOnly?)new TimeOnly(16, 0), (TimeOnly?)null), (events[1].Title, events[1].StartTime, events[1].EndTime));
    }

    [Fact]
    public void Repeating_events_become_one_date_each_within_the_range()
    {
        var ics = Ics(
            Event("UID:training", "DTSTART;TZID=Europe/Berlin:20260915T170000", "DTEND;TZID=Europe/Berlin:20260915T183000",
                "RRULE:FREQ=WEEKLY;BYDAY=TU", "EXDATE;TZID=Europe/Berlin:20261027T170000", "SUMMARY:Training"));

        var events = IcsCalendarSource.Read(ics, From, new DateOnly(2026, 11, 3), Berlin)!;

        // Every Tuesday in October and the 3rd of November, except the holiday on the 27th
        Assert.Equal([new DateOnly(2026, 10, 6), new DateOnly(2026, 10, 13), new DateOnly(2026, 10, 20), new DateOnly(2026, 11, 3)], events.Select(e => e.Date));
        Assert.All(events, e => Assert.Equal((new TimeOnly(17, 0), new TimeOnly(18, 30)), (e.StartTime!.Value, e.EndTime!.Value)));
        Assert.Equal(events.Count, events.Select(e => e.ExternalId).Distinct().Count());
    }

    [Fact]
    public void Cancelled_and_untitled_events_and_events_outside_the_range_are_skipped()
    {
        var ics = Ics(
            Event("UID:cancelled", "DTSTART:20261107T090000Z", "STATUS:CANCELLED", "SUMMARY:Abgesagt"),
            Event("UID:untitled", "DTSTART:20261108T090000Z"),
            Event("UID:old", "DTSTART:20250101T090000Z", "SUMMARY:Alt"),
            Event("UID:far", "DTSTART:20280101T090000Z", "SUMMARY:Weit weg"),
            Event("UID:ok", "DTSTART:20261109T090000Z", "SUMMARY:Bleibt"));

        var events = IcsCalendarSource.Read(ics, From, To, Berlin)!;

        Assert.Equal(["Bleibt"], events.Select(e => e.Title));
    }

    [Fact]
    public void Written_feed_reads_back_the_same_dates()
    {
        var entries = new[]
        {
            new CalendarEntry("event-1", "football", "Spiel, mit Komma; und Semikolon", new DateOnly(2026, 11, 7), new TimeOnly(10, 0), new TimeOnly(11, 30), "Zirndorf", EntrySources.Manual, null),
            new CalendarEntry("assignment-2", "test", "Probe HSU", new DateOnly(2026, 11, 12), null, null, null, EntrySources.Assignment, null),
            new CalendarEntry("event-3", "other", "Sommerzeit", new DateOnly(2026, 10, 20), new TimeOnly(17, 0), null, null, EntrySources.Imported, null),
        };

        var ics = new IcsCalendarWriter(Berlin).Write("Calvin · Lernkicker", entries);
        var read = IcsCalendarSource.Read(ics, From, To, Berlin)!;

        Assert.Contains("X-WR-CALNAME:Calvin · Lernkicker", ics);
        Assert.Contains("BEGIN:VTIMEZONE", ics);
        Assert.Contains("CATEGORIES:football", ics);
        Assert.Equal(
            [
                ("Sommerzeit", new DateOnly(2026, 10, 20), (TimeOnly?)new TimeOnly(17, 0), (string?)null),
                ("Spiel, mit Komma; und Semikolon", new DateOnly(2026, 11, 7), new TimeOnly(10, 0), "Zirndorf"),
                ("Probe HSU", new DateOnly(2026, 11, 12), null, null),
            ],
            read.Select(e => (e.Title, e.Date, e.StartTime, e.Location)));
        Assert.Equal(new TimeOnly(11, 30), read[1].EndTime);
        Assert.StartsWith("event-1@lernkicker/", read[1].ExternalId);
    }

    [Fact]
    public void Clock_gives_the_date_in_the_family_time_zone()
    {
        // 23:30 UTC is already the next day in Berlin
        var clock = new ZonedClock(new FixedTime(new DateTimeOffset(2026, 10, 24, 23, 30, 0, TimeSpan.Zero)), Berlin);

        Assert.Equal(new DateOnly(2026, 10, 25), clock.Today);
        Assert.True(ZonedClock.IsKnown(Berlin));
        Assert.False(ZonedClock.IsKnown("Mars/Olympus"));
    }

    [Fact]
    public async Task Private_addresses_are_not_fetched()
    {
        using var source = new IcsCalendarSource(Berlin);

        var result = await source.FetchAsync(new Uri("http://127.0.0.1:9/calendar.ics"), From, To);

        Assert.Equal(ErrorCodes.CalendarAddressNotAllowed, result.Error?.Code);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
