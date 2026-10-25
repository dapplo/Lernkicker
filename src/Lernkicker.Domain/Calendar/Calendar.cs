using Lernkicker.Domain.Families;

namespace Lernkicker.Domain.Calendar;

/// <summary>What a date is about; the client shows an icon and a translated name for each.</summary>
public static class EventKinds
{
    public const string Test = "test";
    public const string Football = "football";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = [Test, Football, Other];

    public static bool IsValid(string? kind) => kind is not null && All.Contains(kind);
}

/// <summary>
/// A date in a child's calendar, in the family's local time: a whole day when there is no start time.
/// Entered by a parent, or imported from a <see cref="CalendarSubscription"/> (then read-only).
/// </summary>
public sealed class CalendarEvent : IFamilyOwned
{
    public const int MaxTitleLength = 200;
    public const int MaxLocationLength = 200;

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid FamilyId { get; private set; }
    public Guid ChildId { get; private set; }
    public string Kind { get; private set; }
    public string Title { get; private set; }
    public DateOnly Date { get; private set; }
    public TimeOnly? StartTime { get; private set; }
    public TimeOnly? EndTime { get; private set; }
    public string? Location { get; private set; }

    /// <summary>Set for imported events; they are replaced on every sync.</summary>
    public Guid? SubscriptionId { get; private set; }

    /// <summary>UID of the imported event (plus the occurrence date for repeating events).</summary>
    public string? ExternalId { get; private set; }

    public CalendarEvent(Guid familyId, Guid childId, EventDetails details, Guid? subscriptionId = null, string? externalId = null)
    {
        FamilyId = familyId;
        ChildId = childId;
        SubscriptionId = subscriptionId;
        ExternalId = externalId;
        Kind = details.Kind;
        Title = "";
        Update(details);
    }

    // For the persistence adapter
    private CalendarEvent()
    {
        Kind = "";
        Title = "";
    }

    public bool IsImported => SubscriptionId is not null;

    public void Update(EventDetails details)
    {
        Kind = details.Kind;
        Title = Clip(details.Title, MaxTitleLength)!;
        Date = details.Date;
        StartTime = details.StartTime;
        EndTime = details.StartTime is null ? null : details.EndTime;
        Location = Clip(details.Location, MaxLocationLength);
    }

    private static string? Clip(string? text, int max) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim() is var t && t.Length > max ? t[..max] : text.Trim();
}

/// <summary>The editable part of a <see cref="CalendarEvent"/>.</summary>
public sealed record EventDetails(string Kind, string Title, DateOnly Date, TimeOnly? StartTime, TimeOnly? EndTime, string? Location)
{
    /// <summary>Checks what a parent typed; imported events are clipped instead.</summary>
    public string? Problem() => this switch
    {
        _ when !EventKinds.IsValid(Kind) => CalendarProblems.KindInvalid,
        _ when string.IsNullOrWhiteSpace(Title) || Title.Trim().Length > CalendarEvent.MaxTitleLength => CalendarProblems.TitleInvalid,
        _ when Location?.Trim().Length > CalendarEvent.MaxLocationLength => CalendarProblems.LocationInvalid,
        { StartTime: null, EndTime: not null } => CalendarProblems.TimeInvalid,
        { StartTime: { } start, EndTime: { } end } when end < start => CalendarProblems.TimeInvalid,
        _ => null,
    };
}

/// <summary>Codes for invalid calendar input; the application layer turns them into error codes.</summary>
public static class CalendarProblems
{
    public const string KindInvalid = "kindInvalid";
    public const string TitleInvalid = "titleInvalid";
    public const string LocationInvalid = "locationInvalid";
    public const string TimeInvalid = "timeInvalid";
    public const string UrlInvalid = "urlInvalid";
}

/// <summary>An ICS calendar (e.g. the football team's) whose dates are copied into a child's calendar.</summary>
public sealed class CalendarSubscription : IFamilyOwned
{
    public const int MaxLabelLength = 100;
    public const int MaxUrlLength = 2000;

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid FamilyId { get; private set; }
    public Guid ChildId { get; private set; }
    public string Label { get; private set; }
    public string Url { get; private set; }

    /// <summary>Kind given to every imported date.</summary>
    public string Kind { get; private set; }

    public DateTimeOffset? LastSyncedAt { get; private set; }

    /// <summary>Error code of the last failed sync, null after a successful one.</summary>
    public string? LastError { get; private set; }

    public int EventCount { get; private set; }

    public CalendarSubscription(Guid familyId, Guid childId, string label, string url, string kind)
    {
        FamilyId = familyId;
        ChildId = childId;
        Label = label.Trim();
        Url = url;
        Kind = kind;
    }

    /// <summary>
    /// Normalises what a parent pasted: webcal:// becomes https://, only http(s) URLs are accepted. Whether the
    /// host may be contacted at all is checked when fetching (it must not be a private address).
    /// </summary>
    public static Uri? ParseUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > MaxUrlLength)
        {
            return null;
        }

        var text = url.Trim();
        if (text.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase))
        {
            text = "https://" + text["webcal://".Length..];
        }

        return Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && uri.Scheme is "https" or "http"
            && string.IsNullOrEmpty(uri.UserInfo)
            ? uri
            : null;
    }

    public static bool IsValidLabel(string? label) => !string.IsNullOrWhiteSpace(label) && label.Trim().Length <= MaxLabelLength;

    public void Synced(DateTimeOffset at, int eventCount)
    {
        LastSyncedAt = at;
        LastError = null;
        EventCount = eventCount;
    }

    public void Failed(DateTimeOffset at, string errorCode)
    {
        LastSyncedAt = at;
        LastError = errorCode;
    }
}

/// <summary>The secret address of a child's ICS feed, for the parents' calendar app. Only the hash is stored.</summary>
public sealed class CalendarFeed : IFamilyOwned
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid FamilyId { get; private set; }
    public Guid ChildId { get; private set; }
    public string TokenHash { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public CalendarFeed(Guid familyId, Guid childId, string tokenHash, DateTimeOffset createdAt)
    {
        FamilyId = familyId;
        ChildId = childId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
    }
}
