namespace Lernkicker.Application;

/// <summary>
/// What the bot tells a parent. Each kind has a text per UI language in the messaging adapter; only names,
/// titles and numbers are filled in.
/// </summary>
public abstract record Notification;

/// <summary>Answer to /start without a valid code from a chat that is not linked.</summary>
public sealed record WelcomeNotification : Notification;

public sealed record LinkInvalidNotification : Notification;

public sealed record LinkedNotification(string ParentName) : Notification;

public sealed record UnlinkedNotification : Notification;

public sealed record HelpNotification : Notification;

/// <summary>A child handed in a quiz.</summary>
public sealed record ResultNotification(string ChildName, string QuizTitle, int Points, int MaxPoints, int Percent, int? Mark) : Notification;

/// <summary>The children's dates: tomorrow's (the evening reminder) or the next days (/next).</summary>
public sealed record DatesNotification(DatesNotification.Period When, IReadOnlyList<ChildDates> Children) : Notification
{
    public enum Period { Tomorrow, NextDays }
}

public sealed record ChildDates(string ChildName, IReadOnlyList<CalendarEntry> Entries);
