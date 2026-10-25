using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lernkicker.Contracts;

public static class ApiJson
{
    /// <summary>Options both sides use: camelCase, nulls omitted, "type" may come anywhere in an object.</summary>
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        AllowOutOfOrderMetadataProperties = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

/// <summary>
/// Body of every API error. <c>Code</c> is stable and translated by the client; quiz uploads list the
/// individual <c>Issues</c>. The server never sends user-facing text.
/// </summary>
public sealed record ApiError(string Code, IReadOnlyList<ApiIssue>? Issues = null);

/// <summary>One problem in an uploaded quiz; <c>Args</c> fill the placeholders of the translated message.</summary>
public sealed record ApiIssue(string Code, string Path, IReadOnlyDictionary<string, string>? Args);

public static class ApiLimits
{
    /// <summary>Largest quiz file the server accepts; the client checks it before uploading. Equals the domain's QuizLimits.MaxBytes.</summary>
    public const int QuizMaxBytes = 256 * 1024;

    /// <summary>Largest lesson file the server accepts. Equals the domain's LessonLimits.MaxBytes.</summary>
    public const int LessonMaxBytes = 256 * 1024;
}

// Account

public sealed record Login(string Email, string Password);

public sealed record RedeemInvitationRequest(string Code, string Email, string Password, string DisplayName, bool PrivacyAccepted, string? Language);

public sealed record Me(string Name, string Email, string Family, bool IsAdmin, string Language);

public sealed record SetLanguage(string Language);

// Admin

public sealed record NewInvitation(Guid? FamilyId, string? NewFamilyName);

public sealed record InvitationCreatedResponse(string Code, string Link, DateTimeOffset ExpiresAt);

public sealed record FamilyOverview(Guid Id, string Name, int Parents, int Children, DateTimeOffset CreatedAt);

// Quizzes

/// <summary><c>Shared</c>: all families on this server can see, preview and copy it.</summary>
public sealed record QuizOverview(string QuizId, int Version, string Title, string Subject, int GradeLevel, string? Topic, int MaxPoints, DateTimeOffset UploadedAt, bool Shared);

public sealed record QuizDeletedResponse(bool Archived);

// Children (for parents)

public sealed record NewChild(string Name, int? GradeLevel, string Pin);

public sealed record ChildDetails(string Name, int? GradeLevel);

public sealed record ChangePin(string Pin);

public sealed record ChildOverview(Guid Id, string Name, int? GradeLevel, bool HasPin, int Devices);

public sealed record PairingCodeResponse(string Code, string Link, DateTimeOffset ExpiresAt);

public sealed record DeviceOverview(Guid Id, string Label, DateTimeOffset PairedAt, DateTimeOffset? LastUsedAt, bool Locked);

public sealed record NewAssignment(string QuizId, DateOnly? DueOn);

public sealed record AssignmentOverview(Guid Id, string QuizId, int Version, string Title, string Subject, DateOnly? DueOn, DateTimeOffset AssignedAt);

public sealed record AttemptOverview(Guid Id, string QuizId, string Title, string Subject, string Channel, DateTimeOffset SubmittedAt, int Points, int MaxPoints, int Percent, int Mark);

// Child's device

public sealed record PairDevice(string Code, string? Label);

public sealed record DeviceStatusResponse(bool Paired, string? ChildName, bool Locked);

public sealed record PinLogin(string Pin);

public sealed record MyAssignment(Guid AssignmentId, string Title, string Subject, string? Topic, int MaxPoints, DateOnly? DueOn, int Attempts, int? BestPercent, int Stars);

public sealed record Submission(Dictionary<string, AnswerDto> Answers);
