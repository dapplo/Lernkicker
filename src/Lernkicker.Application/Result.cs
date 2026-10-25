using Lernkicker.Domain.Quizzes;

namespace Lernkicker.Application;

/// <summary>How an inbound adapter should report an error (the web adapter maps it to an HTTP status).</summary>
public enum ErrorKind
{
    Invalid,
    NotFound,
    Unauthorized,
    Locked,
    TooLarge,
}

/// <summary>
/// A failed use case. <c>Code</c> is stable and translated by the UI (see <see cref="ErrorCodes"/>); quiz
/// uploads add the individual <c>Issues</c>.
/// </summary>
public sealed record AppError(ErrorKind Kind, string Code, IReadOnlyList<QuizIssue>? Issues = null)
{
    public static AppError Invalid(string code, IReadOnlyList<QuizIssue>? issues = null) => new(ErrorKind.Invalid, code, issues);

    public static AppError NotFound(string code = ErrorCodes.NotFound) => new(ErrorKind.NotFound, code);
}

public readonly struct Result
{
    public AppError? Error { get; }

    public bool IsSuccess => Error is null;

    private Result(AppError? error) => Error = error;

    public static Result Ok() => default;

    public static implicit operator Result(AppError error) => new(error);
}

public readonly struct Result<T>
{
    private readonly T? _value;

    public AppError? Error { get; }

    public bool IsSuccess => Error is null;

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"No value: {Error!.Code}");

    private Result(T? value, AppError? error)
    {
        _value = value;
        Error = error;
    }

    /// <summary>For values of an interface type, which the implicit conversion cannot take.</summary>
    public static Result<T> Ok(T value) => new(value, null);

    public static implicit operator Result<T>(T value) => new(value, null);

    public static implicit operator Result<T>(AppError error) => new(default, error);
}

/// <summary>Error codes the UI translates. Keep in sync with the client's resource files (Errors.*).</summary>
public static class ErrorCodes
{
    public const string NotFound = "notFound";
    public const string FamilyNotFound = "family.notFound";
    public const string FamilyIsOwn = "family.isOwn";
    public const string InvitationInvalid = "invitation.invalid";
    public const string ConsentRequired = "invitation.consentRequired";
    public const string LoginFailed = "login.failed";
    public const string LoginLockedOut = "login.lockedOut";
    public const string ChildNotFound = "child.notFound";
    public const string ChildNameInvalid = "child.nameInvalid";
    public const string GradeLevelInvalid = "child.gradeLevelInvalid";
    public const string PinInvalid = "pin.invalid";
    public const string PinWrong = "pin.wrong";
    public const string DeviceNotPaired = "device.notPaired";
    public const string DeviceLocked = "device.locked";
    public const string PairingCodeInvalid = "pairing.invalid";
    public const string QuizNotFound = "quiz.notFound";
    public const string QuizInvalid = "quiz.invalid";
    public const string QuizTooLarge = "quiz.tooLarge";
    public const string LanguageInvalid = "language.invalid";

    public const string EventKindInvalid = "calendar.kindInvalid";
    public const string EventTitleInvalid = "calendar.titleInvalid";
    public const string EventLocationInvalid = "calendar.locationInvalid";
    public const string EventTimeInvalid = "calendar.timeInvalid";
    public const string EventNotFound = "calendar.eventNotFound";
    public const string EventReadOnly = "calendar.eventReadOnly";
    public const string SubscriptionUrlInvalid = "calendar.urlInvalid";
    public const string SubscriptionLabelInvalid = "calendar.labelInvalid";
    public const string SubscriptionNotFound = "calendar.subscriptionNotFound";
    public const string TooManySubscriptions = "calendar.tooManySubscriptions";
    public const string CalendarUnreachable = "calendar.unreachable";
    public const string CalendarAddressNotAllowed = "calendar.addressNotAllowed";
    public const string CalendarTooLarge = "calendar.tooLarge";
    public const string NotACalendar = "calendar.notACalendar";
    public const string CalendarRangeInvalid = "calendar.rangeInvalid";

    /// <summary>Prefix for account errors from the identity adapter, e.g. "account.DuplicateEmail".</summary>
    public const string AccountPrefix = "account.";
}
