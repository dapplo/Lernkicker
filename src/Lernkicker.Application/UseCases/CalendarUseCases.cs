using Lernkicker.Application.Ports;
using Lernkicker.Domain.Calendar;

namespace Lernkicker.Application.UseCases;

/// <summary>
/// A child's dates: events parents enter, events imported from subscribed ICS calendars (e.g. the football
/// team) and the due dates of assignments. Parents can subscribe to all of it with a secret ICS feed.
/// </summary>
public sealed class CalendarUseCases(
    ICalendarRepository calendar,
    IChildRepository children,
    IPracticeRepository practice,
    ILessonRepository lessons,
    ICalendarSource source,
    ICalendarWriter writer,
    IFamilyScope familyScope,
    ILocalClock clock,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    public const int MaxSubscriptionsPerChild = 5;
    public const int MaxImportedEvents = 1000;

    /// <summary>How far imports and the feed reach into the past and the future.</summary>
    public const int PastDays = 60;
    public const int FutureDays = 400;

    /// <summary>What the child sees on the start page.</summary>
    public const int UpcomingDays = 14;

    public async Task<Result<IReadOnlyList<CalendarEntry>>> ListAsync(Guid childId, DateOnly from, DateOnly to)
    {
        if (await children.FindAsync(childId) is null)
        {
            return AppError.NotFound(ErrorCodes.ChildNotFound);
        }

        return Result<IReadOnlyList<CalendarEntry>>.Ok(await EntriesAsync(childId, from, to));
    }

    /// <summary>The next two weeks, for the child.</summary>
    public async Task<IReadOnlyList<CalendarEntry>> UpcomingAsync(Guid childId) =>
        await EntriesAsync(childId, clock.Today, clock.Today.AddDays(UpcomingDays));

    public async Task<Result<CalendarEntry>> AddEventAsync(Guid childId, EventDetails details)
    {
        if (Check(details) is { } error)
        {
            return error;
        }

        var child = await children.FindAsync(childId);
        if (child is null)
        {
            return AppError.NotFound(ErrorCodes.ChildNotFound);
        }

        var calendarEvent = new CalendarEvent(child.FamilyId, child.Id, details);
        calendar.Add(calendarEvent);
        await unitOfWork.SaveChangesAsync();
        return Entry(calendarEvent);
    }

    public async Task<Result<CalendarEntry>> UpdateEventAsync(Guid childId, Guid eventId, EventDetails details)
    {
        if (Check(details) is { } error)
        {
            return error;
        }

        var calendarEvent = await calendar.FindEventAsync(childId, eventId);
        if (calendarEvent is null)
        {
            return AppError.NotFound(ErrorCodes.EventNotFound);
        }

        if (calendarEvent.IsImported)
        {
            return AppError.Invalid(ErrorCodes.EventReadOnly);
        }

        calendarEvent.Update(details);
        await unitOfWork.SaveChangesAsync();
        return Entry(calendarEvent);
    }

    public async Task<Result> DeleteEventAsync(Guid childId, Guid eventId)
    {
        var calendarEvent = await calendar.FindEventAsync(childId, eventId);
        if (calendarEvent is null)
        {
            return AppError.NotFound(ErrorCodes.EventNotFound);
        }

        if (calendarEvent.IsImported)
        {
            return AppError.Invalid(ErrorCodes.EventReadOnly);
        }

        calendar.Remove(calendarEvent);
        await unitOfWork.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result<IReadOnlyList<SubscriptionSummary>>> ListSubscriptionsAsync(Guid childId)
    {
        if (await children.FindAsync(childId) is null)
        {
            return AppError.NotFound(ErrorCodes.ChildNotFound);
        }

        return Result<IReadOnlyList<SubscriptionSummary>>.Ok((await calendar.ListSubscriptionsAsync(childId)).OrderBy(s => s.Label).Select(SubscriptionSummary.From).ToList());
    }

    /// <summary>Adds a subscription and imports it right away, so the parent sees at once whether the URL works.</summary>
    public async Task<Result<SubscriptionSummary>> SubscribeAsync(Guid childId, string label, string url, string kind, CancellationToken cancellationToken = default)
    {
        if (!CalendarSubscription.IsValidLabel(label))
        {
            return AppError.Invalid(ErrorCodes.SubscriptionLabelInvalid);
        }

        if (!EventKinds.IsValid(kind))
        {
            return AppError.Invalid(ErrorCodes.EventKindInvalid);
        }

        if (CalendarSubscription.ParseUrl(url) is not { } uri)
        {
            return AppError.Invalid(ErrorCodes.SubscriptionUrlInvalid);
        }

        var child = await children.FindAsync(childId);
        if (child is null)
        {
            return AppError.NotFound(ErrorCodes.ChildNotFound);
        }

        if ((await calendar.ListSubscriptionsAsync(childId)).Count >= MaxSubscriptionsPerChild)
        {
            return AppError.Invalid(ErrorCodes.TooManySubscriptions);
        }

        // Fetch before saving: a URL that does not work is not kept
        var fetched = await FetchAsync(uri, cancellationToken);
        if (!fetched.IsSuccess)
        {
            return fetched.Error!;
        }

        var subscription = new CalendarSubscription(child.FamilyId, child.Id, label, uri.ToString(), kind);
        calendar.Add(subscription);
        Import(subscription, fetched.Value);
        await unitOfWork.SaveChangesAsync();
        return SubscriptionSummary.From(subscription);
    }

    public async Task<Result> UnsubscribeAsync(Guid childId, Guid subscriptionId)
    {
        var subscription = await calendar.FindSubscriptionAsync(childId, subscriptionId);
        if (subscription is null)
        {
            return AppError.NotFound(ErrorCodes.SubscriptionNotFound);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync();
        await calendar.RemoveImportedAsync(subscription.Id);
        calendar.Remove(subscription);
        await unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
        return Result.Ok();
    }

    /// <summary>Imports a subscription again now (a parent pressed "update").</summary>
    public async Task<Result<SubscriptionSummary>> SyncAsync(Guid childId, Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        var subscription = await calendar.FindSubscriptionAsync(childId, subscriptionId);
        if (subscription is null)
        {
            return AppError.NotFound(ErrorCodes.SubscriptionNotFound);
        }

        await SyncAsync(subscription, cancellationToken);
        return SubscriptionSummary.From(subscription);
    }

    public Task<IReadOnlyList<SubscriptionRef>> ListSubscriptionsToSyncAsync() => calendar.ListAllSubscriptionsAnyFamilyAsync();

    /// <summary>For the background sync, which has no login: runs in the subscription's family.</summary>
    public async Task SyncAsync(SubscriptionRef reference, CancellationToken cancellationToken = default)
    {
        familyScope.Enter(reference.FamilyId);
        if (await calendar.FindSubscriptionAsync(reference.Id) is { } subscription)
        {
            await SyncAsync(subscription, cancellationToken);
        }
    }

    /// <summary>A new secret feed address for the child; the previous one stops working.</summary>
    public async Task<Result<FeedCreated>> CreateFeedAsync(Guid childId)
    {
        var child = await children.FindAsync(childId);
        if (child is null)
        {
            return AppError.NotFound(ErrorCodes.ChildNotFound);
        }

        var token = Secrets.NewToken();
        await using var transaction = await unitOfWork.BeginTransactionAsync();
        await calendar.RemoveFeedsAsync(child.Id);
        calendar.Add(new CalendarFeed(child.FamilyId, child.Id, Secrets.Hash(token), time.GetUtcNow()));
        await unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
        return new FeedCreated(token);
    }

    public async Task<Result> RemoveFeedAsync(Guid childId)
    {
        if (await children.FindAsync(childId) is null)
        {
            return AppError.NotFound(ErrorCodes.ChildNotFound);
        }

        await calendar.RemoveFeedsAsync(childId);
        return Result.Ok();
    }

    /// <summary>The ICS feed behind a secret link. The token is the only authentication.</summary>
    public async Task<Result<string>> FeedAsync(string token)
    {
        var feed = await calendar.FindFeedAnyFamilyAsync(Secrets.Hash(token));
        if (feed is null)
        {
            return AppError.NotFound();
        }

        familyScope.Enter(feed.FamilyId);
        var child = await children.FindAsync(feed.ChildId);
        if (child is null)
        {
            return AppError.NotFound();
        }

        var entries = await EntriesAsync(child.Id, clock.Today.AddDays(-PastDays), clock.Today.AddDays(FutureDays));
        return writer.Write($"{child.Name} · Lernkicker", entries);
    }

    private async Task SyncAsync(CalendarSubscription subscription, CancellationToken cancellationToken)
    {
        var fetched = await FetchAsync(new Uri(subscription.Url), cancellationToken);
        if (!fetched.IsSuccess)
        {
            // Keep the dates from the last good import; the parent sees the error
            subscription.Failed(time.GetUtcNow(), fetched.Error!.Code);
            await unitOfWork.SaveChangesAsync();
            return;
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync();
        await calendar.RemoveImportedAsync(subscription.Id);
        Import(subscription, fetched.Value);
        await unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private Task<Result<IReadOnlyList<ImportedEvent>>> FetchAsync(Uri url, CancellationToken cancellationToken) =>
        source.FetchAsync(url, clock.Today.AddDays(-PastDays), clock.Today.AddDays(FutureDays), cancellationToken);

    private void Import(CalendarSubscription subscription, IReadOnlyList<ImportedEvent> events)
    {
        var imported = events.Where(e => !string.IsNullOrWhiteSpace(e.Title)).Take(MaxImportedEvents).ToList();
        foreach (var e in imported)
        {
            calendar.Add(new CalendarEvent(
                subscription.FamilyId,
                subscription.ChildId,
                new EventDetails(subscription.Kind, e.Title, e.Date, e.StartTime, e.EndTime, e.Location),
                subscription.Id,
                e.ExternalId));
        }

        subscription.Synced(time.GetUtcNow(), imported.Count);
    }

    private async Task<IReadOnlyList<CalendarEntry>> EntriesAsync(Guid childId, DateOnly from, DateOnly to)
    {
        var events = await calendar.ListEventsAsync(childId, from, to);
        var dueDates = (await practice.ListAssignmentSummariesAsync(childId))
            .Where(a => a.DueOn is { } due && due >= from && due <= to)
            .Select(a => new CalendarEntry($"assignment-{a.Id}", EventKinds.Test, a.Title, a.DueOn!.Value, null, null, null, EntrySources.Assignment, null, a.QuizId));
        var lessonDates = await LessonDatesAsync(childId, from, to);

        return events
            .Select(Entry)
            .Concat(dueDates)
            .Concat(lessonDates)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.StartTime ?? TimeOnly.MinValue)
            .ThenBy(e => e.Title, StringComparer.CurrentCulture)
            .ToList();
    }

    // E.g. the day a poem is recited
    private async Task<IEnumerable<CalendarEntry>> LessonDatesAsync(Guid childId, DateOnly from, DateOnly to)
    {
        var due = (await lessons.ListAssignmentsAsync(childId)).Where(a => a.DueOn is { } d && d >= from && d <= to).ToList();
        if (due.Count == 0)
        {
            return [];
        }

        var stored = (await lessons.ListAsync()).ToDictionary(l => l.Id);
        return due
            .Where(a => stored.ContainsKey(a.StoredLessonId))
            .Select(a => (Assignment: a, Lesson: stored[a.StoredLessonId]))
            .Select(x => new CalendarEntry($"lesson-{x.Assignment.Id}", EventKinds.Lesson, x.Lesson.Title, x.Assignment.DueOn!.Value,
                null, null, null, EntrySources.Lesson, null, x.Lesson.LessonId));
    }

    private static CalendarEntry Entry(CalendarEvent e) =>
        new($"event-{e.Id}", e.Kind, e.Title, e.Date, e.StartTime, e.EndTime, e.Location,
            e.IsImported ? EntrySources.Imported : EntrySources.Manual,
            e.IsImported ? null : e.Id);

    private static AppError? Check(EventDetails details) => details.Problem() switch
    {
        null => null,
        CalendarProblems.KindInvalid => AppError.Invalid(ErrorCodes.EventKindInvalid),
        CalendarProblems.TitleInvalid => AppError.Invalid(ErrorCodes.EventTitleInvalid),
        CalendarProblems.LocationInvalid => AppError.Invalid(ErrorCodes.EventLocationInvalid),
        _ => AppError.Invalid(ErrorCodes.EventTimeInvalid),
    };
}
