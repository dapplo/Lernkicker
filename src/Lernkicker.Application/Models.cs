using Lernkicker.Domain.Families;
using Lernkicker.Domain.Library;

namespace Lernkicker.Application;

// Read models the use cases return. The web adapter maps them to the HTTP contracts.

public sealed record FamilySummary(Guid Id, string Name, int Parents, int Children, DateTimeOffset CreatedAt);

public sealed record InvitationCreated(string Code, DateTimeOffset ExpiresAt);

public sealed record ParentProfile(string Name, string Email, string FamilyName, bool IsAdmin, string Language);

public sealed record ChildSummary(Guid Id, string Name, int? GradeLevel, bool HasPin, int Devices);

public sealed record DeviceSummary(Guid Id, string Label, DateTimeOffset PairedAt, DateTimeOffset? LastUsedAt, bool Locked)
{
    public static DeviceSummary From(ChildDevice d) => new(d.Id, d.Label, d.PairedAt, d.LastUsedAt, d.Locked);
}

public sealed record PairingCodeCreated(string Code, DateTimeOffset ExpiresAt);

public sealed record PairedDevice(string Token, string ChildName);

public sealed record DeviceStatus(bool Paired, string? ChildName, bool Locked);

/// <summary>Who is logged in on a child's device after a correct PIN.</summary>
public sealed record ChildSession(Guid FamilyId, Guid ChildId, Guid DeviceId, string ChildName);

public sealed record QuizSummary(string QuizId, int Version, string Title, string Subject, int GradeLevel, string? Topic, int MaxPoints, DateTimeOffset UploadedAt)
{
    public static QuizSummary From(StoredQuiz q) => new(q.QuizId, q.Version, q.Title, q.Subject, q.GradeLevel, q.Topic, q.MaxPoints, q.UploadedAt);
}

/// <summary><c>Archived</c>: the quiz had results and was archived instead of removed.</summary>
public sealed record QuizDeleted(bool Archived);

public sealed record AssignmentSummary(
    Guid Id, Guid StoredQuizId, string QuizId, int Version, string Title, string Subject, string? Topic, int MaxPoints, DateOnly? DueOn, DateTimeOffset AssignedAt);

public sealed record AttemptSummary(
    Guid Id, Guid StoredQuizId, string QuizId, string Title, string Subject, string Channel, DateTimeOffset SubmittedAt, int Points, int MaxPoints, int Percent, int Mark);

/// <summary>An assignment as the child sees it, with progress.</summary>
public sealed record ChildAssignment(
    Guid AssignmentId, string Title, string Subject, string? Topic, int MaxPoints, DateOnly? DueOn, int Attempts, int? BestPercent, int Stars);

/// <summary>Where a calendar entry comes from.</summary>
public static class EntrySources
{
    public const string Manual = "manual";
    public const string Imported = "imported";
    public const string Assignment = "assignment";
}

/// <summary>
/// A date in a child's calendar: an event, or the due date of an assignment. <c>Uid</c> is stable across
/// requests (for the ICS feed); <c>EventId</c> is set for events a parent may change.
/// </summary>
public sealed record CalendarEntry(
    string Uid, string Kind, string Title, DateOnly Date, TimeOnly? StartTime, TimeOnly? EndTime, string? Location, string Source, Guid? EventId);

/// <summary>A subscription as parents see it. Only the host of the URL is shown, as private calendar URLs contain secrets.</summary>
public sealed record SubscriptionSummary(Guid Id, string Label, string Host, string Kind, DateTimeOffset? LastSyncedAt, string? LastError, int EventCount)
{
    public static SubscriptionSummary From(Domain.Calendar.CalendarSubscription s) =>
        new(s.Id, s.Label, Uri.TryCreate(s.Url, UriKind.Absolute, out var uri) ? uri.Host : "", s.Kind, s.LastSyncedAt, s.LastError, s.EventCount);
}

public sealed record FeedCreated(string Token);
