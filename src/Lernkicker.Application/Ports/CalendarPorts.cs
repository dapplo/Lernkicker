using Lernkicker.Domain.Calendar;

namespace Lernkicker.Application.Ports;

public interface ICalendarRepository
{
    void Add(CalendarEvent calendarEvent);

    void Remove(CalendarEvent calendarEvent);

    Task<CalendarEvent?> FindEventAsync(Guid childId, Guid eventId);

    Task<IReadOnlyList<CalendarEvent>> ListEventsAsync(Guid childId, DateOnly from, DateOnly to);

    /// <summary>Deletes all events imported from a subscription, right away (not on save).</summary>
    Task RemoveImportedAsync(Guid subscriptionId);

    void Add(CalendarSubscription subscription);

    void Remove(CalendarSubscription subscription);

    Task<CalendarSubscription?> FindSubscriptionAsync(Guid childId, Guid subscriptionId);

    Task<CalendarSubscription?> FindSubscriptionAsync(Guid subscriptionId);

    Task<IReadOnlyList<CalendarSubscription>> ListSubscriptionsAsync(Guid childId);

    /// <summary>Every subscription of every family, for the background sync.</summary>
    Task<IReadOnlyList<SubscriptionRef>> ListAllSubscriptionsAnyFamilyAsync();

    void Add(CalendarFeed feed);

    /// <summary>Deletes the child's feed addresses right away, so an old link stops working.</summary>
    Task RemoveFeedsAsync(Guid childId);

    /// <summary>The feed behind a secret link; the caller is not logged in, so across families.</summary>
    Task<CalendarFeed?> FindFeedAnyFamilyAsync(string tokenHash);
}

public sealed record SubscriptionRef(Guid Id, Guid FamilyId);

/// <summary>Fetches and reads an ICS calendar from the internet.</summary>
public interface ICalendarSource
{
    /// <summary>
    /// The events between <paramref name="from"/> and <paramref name="to"/> (repeating events expanded), in the
    /// family's local time. Fails with one of the calendar error codes, e.g. when the host is a private address.
    /// </summary>
    Task<Result<IReadOnlyList<ImportedEvent>>> FetchAsync(Uri url, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

public sealed record ImportedEvent(string ExternalId, string Title, DateOnly Date, TimeOnly? StartTime, TimeOnly? EndTime, string? Location);

/// <summary>Writes a child's dates as an ICS feed.</summary>
public interface ICalendarWriter
{
    string Write(string calendarName, IEnumerable<CalendarEntry> entries);
}

/// <summary>Today in the family's time zone (the server itself runs in UTC).</summary>
public interface ILocalClock
{
    DateOnly Today { get; }

    /// <summary>The time of day in the family's time zone, e.g. for daily reminders.</summary>
    TimeOnly TimeOfDay { get; }
}
