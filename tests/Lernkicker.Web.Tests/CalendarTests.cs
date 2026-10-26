using System.Net;
using System.Net.Http.Json;
using Lernkicker.Application;
using Lernkicker.Application.Ports;
using Lernkicker.Application.UseCases;
using Lernkicker.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Lernkicker.Web.Tests;

public sealed class CalendarTests(LernkickerFactory factory) : IClassFixture<LernkickerFactory>
{
    // A few days ahead, so the family's "today" (Europe/Berlin) and UTC never disagree about it
    private static readonly DateOnly Soon = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3);

    [Fact]
    public async Task Parent_adds_changes_and_deletes_an_event()
    {
        var parent = await factory.NewFamilyAsync("events@example.org");
        var child = await TestData.CreateChildAsync(parent);
        var url = $"/api/children/{child.Id}/calendar";

        var response = await parent.PostAsJsonAsync(url, new EventRequest("test", "  Probe HSU ", Soon, null, null, null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var added = await LernkickerFactory.Json<CalendarEntryDto>(response);
        Assert.Equal(("Probe HSU", "manual"), (added.Title, added.Source));

        var changed = await LernkickerFactory.Json<CalendarEntryDto>(await parent.PutAsJsonAsync($"{url}/{added.EventId}",
            new EventRequest("football", "Spiel", Soon, new TimeOnly(10, 0), new TimeOnly(11, 30), "Zirndorf")));
        Assert.Equal(("football", new TimeOnly(10, 0), "Zirndorf"), (changed.Kind, changed.StartTime, changed.Location));

        var list = await LernkickerFactory.Json<List<CalendarEntryDto>>(await parent.GetAsync(url));
        Assert.Equal(["Spiel"], list.Select(e => e.Title));

        Assert.Equal(HttpStatusCode.NoContent, (await parent.DeleteAsync($"{url}/{added.EventId}")).StatusCode);
        Assert.Empty(await LernkickerFactory.Json<List<CalendarEntryDto>>(await parent.GetAsync(url)));
        Assert.Equal(ErrorCodes.EventNotFound, (await LernkickerFactory.Error(await parent.DeleteAsync($"{url}/{added.EventId}"), HttpStatusCode.NotFound)).Code);
    }

    [Theory]
    [InlineData("party", "Title", null, null, ErrorCodes.EventKindInvalid)]
    [InlineData("test", " ", null, null, ErrorCodes.EventTitleInvalid)]
    [InlineData("test", "Title", null, "10:00", ErrorCodes.EventTimeInvalid)]
    [InlineData("test", "Title", "10:00", "09:00", ErrorCodes.EventTimeInvalid)]
    public async Task Invalid_events_are_rejected_with_a_code(string kind, string title, string? start, string? end, string code)
    {
        var parent = await factory.NewFamilyAsync($"invalid-{Guid.NewGuid():N}@example.org");
        var child = await TestData.CreateChildAsync(parent);
        var request = new EventRequest(kind, title, Soon, start is null ? null : TimeOnly.Parse(start), end is null ? null : TimeOnly.Parse(end), null);

        var response = await parent.PostAsJsonAsync($"/api/children/{child.Id}/calendar", request);

        Assert.Equal(code, (await LernkickerFactory.Error(response, HttpStatusCode.BadRequest)).Code);
    }

    [Fact]
    public async Task Range_is_limited()
    {
        var parent = await factory.NewFamilyAsync("range@example.org");
        var child = await TestData.CreateChildAsync(parent);

        var backwards = await parent.GetAsync($"/api/children/{child.Id}/calendar?from=2026-10-10&to=2026-10-01");
        var tooLong = await parent.GetAsync($"/api/children/{child.Id}/calendar?from=2026-01-01&to=2028-01-01");

        Assert.Equal(ErrorCodes.CalendarRangeInvalid, (await LernkickerFactory.Error(backwards, HttpStatusCode.BadRequest)).Code);
        Assert.Equal(ErrorCodes.CalendarRangeInvalid, (await LernkickerFactory.Error(tooLong, HttpStatusCode.BadRequest)).Code);
    }

    [Fact]
    public async Task Due_dates_of_assignments_are_in_the_calendar()
    {
        var parent = await factory.NewFamilyAsync("due@example.org");
        var child = await TestData.CreateChildAsync(parent);
        var quiz = await TestData.UploadHsuAsync(parent);
        var assignment = await TestData.AssignAsync(parent, child.Id, quiz.QuizId, Soon);

        var entry = Assert.Single(await LernkickerFactory.Json<List<CalendarEntryDto>>(await parent.GetAsync($"/api/children/{child.Id}/calendar")));

        Assert.Equal(($"assignment-{assignment.Id}", "test", "assignment", Soon, quiz.Title, quiz.QuizId), (entry.Uid, entry.Kind, entry.Source, entry.Date, entry.Title, entry.ItemId));
        Assert.Null(entry.EventId);
    }

    [Fact]
    public async Task Subscription_imports_read_only_dates_and_keeps_them_when_an_update_fails()
    {
        var parent = await factory.NewFamilyAsync("subscribe@example.org");
        var child = await TestData.CreateChildAsync(parent);
        var url = $"/api/children/{child.Id}";
        factory.Calendars.Set("https://team.example/games.ics",
            new ImportedEvent("game-1", "ASV Zirndorf - SpVgg", Soon, new TimeOnly(10, 0), new TimeOnly(11, 0), "Zirndorf"),
            new ImportedEvent("game-2", "TSV - ASV Zirndorf", Soon.AddDays(7), new TimeOnly(9, 30), null, null),
            new ImportedEvent("empty", " ", Soon, null, null, null));

        var response = await parent.PostAsJsonAsync($"{url}/subscriptions", new NewSubscription("E1 games", "webcal://team.example/games.ics", "football"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var subscription = await LernkickerFactory.Json<SubscriptionOverview>(response);
        Assert.Equal(("E1 games", "team.example", 2, null), (subscription.Label, subscription.Host, subscription.EventCount, subscription.LastError));

        var entries = await LernkickerFactory.Json<List<CalendarEntryDto>>(await parent.GetAsync($"{url}/calendar"));
        Assert.Equal(["ASV Zirndorf - SpVgg", "TSV - ASV Zirndorf"], entries.Select(e => e.Title));
        Assert.All(entries, e => Assert.Equal(("football", "imported", (Guid?)null), (e.Kind, e.Source, e.EventId)));

        // The address went offline: the last good dates stay, the parent sees why
        factory.Calendars.Fail("https://team.example/games.ics", ErrorCodes.NotACalendar);
        var failed = await LernkickerFactory.Json<SubscriptionOverview>(await parent.PostAsync($"{url}/subscriptions/{subscription.Id}/sync", null));
        Assert.Equal((ErrorCodes.NotACalendar, 2), (failed.LastError, failed.EventCount));
        Assert.Equal(2, (await LernkickerFactory.Json<List<CalendarEntryDto>>(await parent.GetAsync($"{url}/calendar"))).Count);

        // Back again with a moved game: the import is replaced
        factory.Calendars.Set("https://team.example/games.ics", new ImportedEvent("game-1", "ASV Zirndorf - SpVgg", Soon.AddDays(1), null, null, null));
        var synced = await LernkickerFactory.Json<SubscriptionOverview>(await parent.PostAsync($"{url}/subscriptions/{subscription.Id}/sync", null));
        Assert.Equal((null, 1), (synced.LastError, synced.EventCount));
        var moved = Assert.Single(await LernkickerFactory.Json<List<CalendarEntryDto>>(await parent.GetAsync($"{url}/calendar")));
        Assert.Equal(Soon.AddDays(1), moved.Date);

        Assert.Equal(HttpStatusCode.NoContent, (await parent.DeleteAsync($"{url}/subscriptions/{subscription.Id}")).StatusCode);
        Assert.Empty(await LernkickerFactory.Json<List<SubscriptionOverview>>(await parent.GetAsync($"{url}/subscriptions")));
        Assert.Empty(await LernkickerFactory.Json<List<CalendarEntryDto>>(await parent.GetAsync($"{url}/calendar")));
    }

    [Fact]
    public async Task Imported_dates_cannot_be_changed()
    {
        var parent = await factory.NewFamilyAsync("readonly@example.org");
        var child = await TestData.CreateChildAsync(parent);
        factory.Calendars.Set("https://school.example/dates.ics", new ImportedEvent("x", "Wandertag", Soon, null, null, null));
        await LernkickerFactory.Json<SubscriptionOverview>(await parent.PostAsJsonAsync($"/api/children/{child.Id}/subscriptions", new NewSubscription("School", "https://school.example/dates.ics", "other")));
        var imported = Assert.Single(await LernkickerFactory.Json<List<CalendarEntryDto>>(await parent.GetAsync($"/api/children/{child.Id}/calendar")));
        var eventId = Guid.Parse(imported.Uid["event-".Length..]);

        var update = await parent.PutAsJsonAsync($"/api/children/{child.Id}/calendar/{eventId}", new EventRequest("test", "Changed", Soon, null, null, null));
        var delete = await parent.DeleteAsync($"/api/children/{child.Id}/calendar/{eventId}");

        Assert.Equal(ErrorCodes.EventReadOnly, (await LernkickerFactory.Error(update, HttpStatusCode.BadRequest)).Code);
        Assert.Equal(ErrorCodes.EventReadOnly, (await LernkickerFactory.Error(delete, HttpStatusCode.BadRequest)).Code);
    }

    [Theory]
    [InlineData("ftp://team.example/games.ics", ErrorCodes.SubscriptionUrlInvalid)]
    [InlineData("https://user:secret@team.example/games.ics", ErrorCodes.SubscriptionUrlInvalid)]
    [InlineData("https://offline.example/games.ics", ErrorCodes.CalendarUnreachable)]
    public async Task Subscription_that_does_not_work_is_not_kept(string address, string code)
    {
        var parent = await factory.NewFamilyAsync($"bad-sub-{Guid.NewGuid():N}@example.org");
        var child = await TestData.CreateChildAsync(parent);

        var response = await parent.PostAsJsonAsync($"/api/children/{child.Id}/subscriptions", new NewSubscription("Team", address, "football"));

        Assert.Equal(code, (await LernkickerFactory.Error(response, HttpStatusCode.BadRequest)).Code);
        Assert.Empty(await LernkickerFactory.Json<List<SubscriptionOverview>>(await parent.GetAsync($"/api/children/{child.Id}/subscriptions")));
    }

    [Fact]
    public async Task Subscriptions_per_child_are_limited()
    {
        var parent = await factory.NewFamilyAsync("many@example.org");
        var child = await TestData.CreateChildAsync(parent);
        factory.Calendars.Set("https://many.example/a.ics");
        for (var i = 0; i < CalendarUseCases.MaxSubscriptionsPerChild; i++)
        {
            await LernkickerFactory.Json<SubscriptionOverview>(await parent.PostAsJsonAsync($"/api/children/{child.Id}/subscriptions", new NewSubscription($"Calendar {i}", "https://many.example/a.ics", "other")));
        }

        var response = await parent.PostAsJsonAsync($"/api/children/{child.Id}/subscriptions", new NewSubscription("One more", "https://many.example/a.ics", "other"));

        Assert.Equal(ErrorCodes.TooManySubscriptions, (await LernkickerFactory.Error(response, HttpStatusCode.BadRequest)).Code);
    }

    [Fact]
    public async Task Feed_link_works_without_a_login_until_it_is_replaced_or_removed()
    {
        var parent = await factory.NewFamilyAsync("feed@example.org");
        var child = await TestData.CreateChildAsync(parent);
        await LernkickerFactory.Json<CalendarEntryDto>(await parent.PostAsJsonAsync($"/api/children/{child.Id}/calendar",
            new EventRequest("test", "Probe Mathe", Soon, new TimeOnly(8, 0), new TimeOnly(9, 0), null)));

        var first = await LernkickerFactory.Json<FeedResponse>(await parent.PostAsync($"/api/children/{child.Id}/feed", null));
        Assert.StartsWith("https://localhost/calendar/", first.Link);
        Assert.EndsWith(".ics", first.Link);

        var calendarApp = factory.Client();
        var response = await calendarApp.GetAsync(first.Link);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/calendar", response.Content.Headers.ContentType?.MediaType);
        var ics = await response.Content.ReadAsStringAsync();
        Assert.Contains("BEGIN:VCALENDAR", ics);
        Assert.Contains("SUMMARY:Probe Mathe", ics);
        Assert.Contains("Calvin", ics);

        var second = await LernkickerFactory.Json<FeedResponse>(await parent.PostAsync($"/api/children/{child.Id}/feed", null));
        Assert.Equal(HttpStatusCode.NotFound, (await calendarApp.GetAsync(first.Link)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await calendarApp.GetAsync(second.Link)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await parent.DeleteAsync($"/api/children/{child.Id}/feed")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await calendarApp.GetAsync(second.Link)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await calendarApp.GetAsync("/calendar/not-a-token.ics")).StatusCode);
    }

    [Fact]
    public async Task Child_sees_the_next_two_weeks()
    {
        var parent = await factory.NewFamilyAsync("upcoming@example.org");
        var child = await TestData.CreateChildAsync(parent);
        foreach (var (title, date) in new[] { ("Soon", Soon), ("Later", Soon.AddDays(30)), ("Past", Soon.AddDays(-10)) })
        {
            await LernkickerFactory.Json<CalendarEntryDto>(await parent.PostAsJsonAsync($"/api/children/{child.Id}/calendar", new EventRequest("other", title, date, null, null, null)));
        }

        var phone = await TestData.ChildPhoneAsync(factory, parent, child.Id);
        var upcoming = await LernkickerFactory.Json<List<CalendarEntryDto>>(await phone.GetAsync("/api/device/calendar"));

        Assert.Equal(["Soon"], upcoming.Select(e => e.Title));
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.Client().GetAsync("/api/device/calendar")).StatusCode);
    }

    [Fact]
    public async Task Other_family_sees_and_changes_no_dates()
    {
        var a = await factory.NewFamilyAsync("cal-a@example.org");
        var b = await factory.NewFamilyAsync("cal-b@example.org");
        var child = await TestData.CreateChildAsync(a);
        var url = $"/api/children/{child.Id}";
        var entry = await LernkickerFactory.Json<CalendarEntryDto>(await a.PostAsJsonAsync($"{url}/calendar", new EventRequest("test", "Secret", Soon, null, null, null)));
        factory.Calendars.Set("https://isolation.example/a.ics");
        var subscription = await LernkickerFactory.Json<SubscriptionOverview>(await a.PostAsJsonAsync($"{url}/subscriptions", new NewSubscription("A", "https://isolation.example/a.ics", "other")));

        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"{url}/calendar")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync($"{url}/calendar", new EventRequest("test", "Planted", Soon, null, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PutAsJsonAsync($"{url}/calendar/{entry.EventId}", new EventRequest("test", "Changed", Soon, null, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"{url}/calendar/{entry.EventId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"{url}/subscriptions")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsync($"{url}/subscriptions/{subscription.Id}/sync", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"{url}/subscriptions/{subscription.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsync($"{url}/feed", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"{url}/feed")).StatusCode);

        var titles = await LernkickerFactory.Json<List<CalendarEntryDto>>(await a.GetAsync($"{url}/calendar"));
        Assert.Equal(["Secret"], titles.Select(e => e.Title));
        Assert.Single(await LernkickerFactory.Json<List<SubscriptionOverview>>(await a.GetAsync($"{url}/subscriptions")));
    }

    [Fact]
    public async Task Background_sync_updates_every_family_without_a_login()
    {
        var a = await factory.NewFamilyAsync("sync-a@example.org");
        var b = await factory.NewFamilyAsync("sync-b@example.org");
        var childA = await TestData.CreateChildAsync(a);
        var childB = await TestData.CreateChildAsync(b);
        factory.Calendars.Set("https://sync.example/a.ics");
        factory.Calendars.Set("https://sync.example/b.ics");
        await LernkickerFactory.Json<SubscriptionOverview>(await a.PostAsJsonAsync($"/api/children/{childA.Id}/subscriptions", new NewSubscription("A", "https://sync.example/a.ics", "football")));
        await LernkickerFactory.Json<SubscriptionOverview>(await b.PostAsJsonAsync($"/api/children/{childB.Id}/subscriptions", new NewSubscription("B", "https://sync.example/b.ics", "football")));
        factory.Calendars.Set("https://sync.example/a.ics", new ImportedEvent("1", "Game A", Soon, null, null, null));
        factory.Calendars.Set("https://sync.example/b.ics", new ImportedEvent("1", "Game B", Soon, null, null, null));

        using (var listScope = factory.Services.CreateScope())
        {
            var references = await listScope.ServiceProvider.GetRequiredService<CalendarUseCases>().ListSubscriptionsToSyncAsync();
            foreach (var reference in references)
            {
                // What CalendarSync does: one scope per subscription, no HTTP request
                using var scope = factory.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<CalendarUseCases>().SyncAsync(reference);
            }
        }

        Assert.Equal(["Game A"], (await LernkickerFactory.Json<List<CalendarEntryDto>>(await a.GetAsync($"/api/children/{childA.Id}/calendar"))).Select(e => e.Title));
        Assert.Equal(["Game B"], (await LernkickerFactory.Json<List<CalendarEntryDto>>(await b.GetAsync($"/api/children/{childB.Id}/calendar"))).Select(e => e.Title));
    }
}
