using Lernkicker.Domain.Families;
using Lernkicker.Domain.Quizzes;

namespace Lernkicker.Domain.Practice;

/// <summary>A quiz a parent picked for a child, optionally with a due date (e.g. the day of the real test).</summary>
public sealed class Assignment : IFamilyOwned
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid FamilyId { get; private set; }
    public Guid ChildId { get; private set; }
    public Guid StoredQuizId { get; private set; }
    public DateOnly? DueOn { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }

    public Assignment(Guid familyId, Guid childId, Guid storedQuizId, DateTimeOffset assignedAt)
    {
        FamilyId = familyId;
        ChildId = childId;
        StoredQuizId = storedQuizId;
        AssignedAt = assignedAt;
    }

    public void SetDueOn(DateOnly? dueOn) => DueOn = dueOn;

    /// <summary>Open assignments follow a quiz to its newest version.</summary>
    public void MoveTo(Guid storedQuizId) => StoredQuizId = storedQuizId;
}

/// <summary>One completed round of a quiz by a child, with the answers and the full result.</summary>
public sealed class Attempt : IFamilyOwned
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid FamilyId { get; private set; }
    public Guid ChildId { get; private set; }
    public Guid StoredQuizId { get; private set; }

    /// <summary>Where the child practised: "pwa" or later "telegram".</summary>
    public string Channel { get; private set; }

    public DateTimeOffset SubmittedAt { get; private set; }
    public int Points { get; private set; }
    public int MaxPoints { get; private set; }
    public int Percent { get; private set; }
    public int? Mark { get; private set; }
    public IReadOnlyDictionary<string, ExerciseAnswer> Answers { get; private set; }
    public QuizResult Result { get; private set; }

    public Attempt(Guid familyId, Guid childId, Guid storedQuizId, string channel, DateTimeOffset submittedAt, IReadOnlyDictionary<string, ExerciseAnswer> answers, QuizResult result)
    {
        FamilyId = familyId;
        ChildId = childId;
        StoredQuizId = storedQuizId;
        Channel = channel;
        SubmittedAt = submittedAt;
        Answers = answers;
        Result = result;
        Points = result.Points;
        MaxPoints = result.MaxPoints;
        Percent = result.Percent;
        Mark = result.Mark;
    }

    // For the persistence adapter, which maps Answers and Result itself
    private Attempt()
    {
        Channel = "";
        Answers = new Dictionary<string, ExerciseAnswer>();
        Result = new QuizResult(0, 0, 0, 6, []);
    }
}

public static class Stars
{
    /// <summary>3 stars from 90 %, 2 from 70 %, 1 from 50 %.</summary>
    public static int For(int? percent) => percent switch
    {
        >= 90 => 3,
        >= 70 => 2,
        >= 50 => 1,
        _ => 0,
    };
}

public static class Channels
{
    public const string Pwa = "pwa";
    public const string Telegram = "telegram";
}
