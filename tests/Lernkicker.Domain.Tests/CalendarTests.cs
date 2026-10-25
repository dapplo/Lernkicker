using Lernkicker.Domain.Calendar;

namespace Lernkicker.Domain.Tests;

public class CalendarTests
{
    private static readonly DateOnly Day = new(2026, 11, 7);

    [Theory]
    [InlineData("webcal://team.example/a.ics", "https://team.example/a.ics")]
    [InlineData("  https://team.example/a.ics?key=1 ", "https://team.example/a.ics?key=1")]
    [InlineData("http://team.example/a.ics", "http://team.example/a.ics")]
    public void Subscription_urls_are_normalised(string input, string expected) =>
        Assert.Equal(expected, CalendarSubscription.ParseUrl(input)?.ToString());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("team.example/a.ics")]
    [InlineData("ftp://team.example/a.ics")]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://user:password@team.example/a.ics")]
    public void Other_urls_are_refused(string? input) =>
        Assert.Null(CalendarSubscription.ParseUrl(input));

    [Fact]
    public void Too_long_urls_are_refused() =>
        Assert.Null(CalendarSubscription.ParseUrl("https://team.example/" + new string('a', CalendarSubscription.MaxUrlLength)));

    [Fact]
    public void Valid_details_have_no_problem() =>
        Assert.Null(new EventDetails(EventKinds.Football, "Spiel", Day, new TimeOnly(10, 0), new TimeOnly(10, 0), "Platz 2").Problem());

    [Fact]
    public void Details_problems_are_named()
    {
        Assert.Equal(CalendarProblems.KindInvalid, new EventDetails("party", "x", Day, null, null, null).Problem());
        Assert.Equal(CalendarProblems.TitleInvalid, new EventDetails(EventKinds.Test, "  ", Day, null, null, null).Problem());
        Assert.Equal(CalendarProblems.TitleInvalid, new EventDetails(EventKinds.Test, new string('x', 201), Day, null, null, null).Problem());
        Assert.Equal(CalendarProblems.LocationInvalid, new EventDetails(EventKinds.Test, "x", Day, null, null, new string('x', 201)).Problem());
        Assert.Equal(CalendarProblems.TimeInvalid, new EventDetails(EventKinds.Test, "x", Day, null, new TimeOnly(9, 0), null).Problem());
        Assert.Equal(CalendarProblems.TimeInvalid, new EventDetails(EventKinds.Test, "x", Day, new TimeOnly(9, 0), new TimeOnly(8, 0), null).Problem());
    }

    [Fact]
    public void Imported_events_are_clipped_and_trimmed()
    {
        var details = new EventDetails(EventKinds.Other, "  " + new string('t', 300), Day, null, new TimeOnly(9, 0), "  ");

        var e = new CalendarEvent(Guid.NewGuid(), Guid.NewGuid(), details, Guid.NewGuid(), "uid");

        Assert.Equal(CalendarEvent.MaxTitleLength, e.Title.Length);
        Assert.Null(e.Location);
        Assert.Null(e.EndTime);
        Assert.True(e.IsImported);
    }

    [Fact]
    public void Failed_sync_keeps_the_count_and_a_good_one_clears_the_error()
    {
        var subscription = new CalendarSubscription(Guid.NewGuid(), Guid.NewGuid(), " Team ", "https://team.example/a.ics", EventKinds.Football);
        var now = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

        subscription.Synced(now, 12);
        subscription.Failed(now.AddHours(6), "calendar.unreachable");
        Assert.Equal(("Team", 12, "calendar.unreachable"), (subscription.Label, subscription.EventCount, subscription.LastError));

        subscription.Synced(now.AddHours(12), 13);
        Assert.Equal((13, (string?)null), (subscription.EventCount, subscription.LastError));
    }
}
