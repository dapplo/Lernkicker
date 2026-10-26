namespace Lernkicker.Contracts;

/// <summary>
/// A date in a child's calendar. <c>Kind</c>: test, football or other. <c>Source</c>: manual (a parent may
/// change it, <c>EventId</c> is set), imported (from a subscription) or assignment (the due date of a quiz).
/// Dates and times are in the family's local time; no start time means the whole day.
/// </summary>
public sealed record CalendarEntryDto(
    string Uid, string Kind, string Title, DateOnly Date, TimeOnly? StartTime, TimeOnly? EndTime, string? Location, string Source, Guid? EventId,
    string? ItemId = null);

public sealed record EventRequest(string Kind, string Title, DateOnly Date, TimeOnly? StartTime, TimeOnly? EndTime, string? Location);

public sealed record NewSubscription(string Label, string Url, string Kind);

/// <summary><c>LastError</c> is an error code of the last failed import, null when it worked.</summary>
public sealed record SubscriptionOverview(Guid Id, string Label, string Host, string Kind, DateTimeOffset? LastSyncedAt, string? LastError, int EventCount);

/// <summary>The secret address of a child's ICS feed. It is only shown once; creating a new one replaces it.</summary>
public sealed record FeedResponse(string Link);
