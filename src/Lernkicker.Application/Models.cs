using Lernkicker.Domain.Families;
using Lernkicker.Domain.Lessons;
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

public sealed record QuizSummary(string QuizId, int Version, string Title, string Subject, int GradeLevel, string? Topic, int MaxPoints, DateTimeOffset UploadedAt, bool Shared)
{
    public static QuizSummary From(StoredQuiz q) => new(q.QuizId, q.Version, q.Title, q.Subject, q.GradeLevel, q.Topic, q.MaxPoints, q.UploadedAt, q.IsShared);
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

public sealed record LessonSummary(string LessonId, string Title, string Subject, int GradeLevel, string? Topic, int Blocks, DateTimeOffset UploadedAt, bool Shared);

public static class SharedKinds
{
    public const string Quiz = "quiz";
    public const string Lesson = "lesson";
}

/// <summary>
/// A quiz or lesson another family shared. <c>Size</c> is the points of a quiz or the parts of a lesson;
/// <c>InMyLibrary</c> says the own library already has one with the same id (copying it replaces that one).
/// </summary>
public sealed record SharedSummary(
    Guid Id, string Kind, string ItemId, string Title, string Subject, int GradeLevel, string? Topic, int Size, DateTimeOffset SharedAt, bool InMyLibrary);

/// <summary>A lesson given to a child, with the child's progress, for parents.</summary>
public sealed record LessonAssignmentSummary(
    Guid Id, string LessonId, string Title, string Subject, DateOnly? DueOn, DateTimeOffset AssignedAt,
    int PracticeCount, DateTimeOffset? LastPracticedAt, IReadOnlyList<string> KnownBlocks, int Blocks);

/// <summary>A lesson as the child sees it in the list.</summary>
public sealed record ChildLesson(Guid AssignmentId, string Title, string Subject, string? Topic, DateOnly? DueOn, int KnownBlocks, int Blocks);

/// <summary>
/// A lesson to learn, with what the child already marked as known. <c>Images</c> maps each picture <c>src</c> to
/// the hash of the stored picture.
/// </summary>
public sealed record LessonToLearn(Guid AssignmentId, Lesson Lesson, DateOnly? DueOn, IReadOnlyList<string> KnownBlocks, IReadOnlyDictionary<string, string> Images)
{
    /// <summary>A lesson outside an assignment, for the parents' preview.</summary>
    public static LessonToLearn Preview(Lesson lesson, IReadOnlyDictionary<string, string> images) => new(Guid.Empty, lesson, null, [], images);
}

/// <summary>Where a calendar entry comes from.</summary>
public static class EntrySources
{
    public const string Manual = "manual";
    public const string Imported = "imported";
    public const string Assignment = "assignment";
    public const string Lesson = "lesson";
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

/// <summary><c>Enabled</c>: a bot is configured on the server. <c>LinkedAt</c>: when this parent linked a chat.</summary>
public sealed record TelegramStatus(bool Enabled, string? BotUsername, DateTimeOffset? LinkedAt);

/// <summary>The link that opens the bot with a one-time code.</summary>
public sealed record TelegramLinkCreated(string Url, string Code, DateTimeOffset ExpiresAt);

/// <summary>What the bot answers, in the parent's (or the Telegram user's) language.</summary>
public sealed record BotReply(string Language, Notification Notification);

/// <summary>The bot as the admin sees it. <c>FromFile</c>: the token is configured outside the app and cannot be changed here.</summary>
public sealed record TelegramBotSettings(bool Configured, bool FromFile, string? BotUsername, DateTimeOffset? SetAt);
