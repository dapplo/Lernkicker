using Lernkicker.Domain.Families;
using Lernkicker.Domain.Quizzes;

namespace Lernkicker.Domain.Library;

/// <summary>
/// One uploaded version of a quiz in a family's library. Uploading the same <see cref="QuizId"/> again adds a
/// new version; the original file is kept verbatim in <see cref="Source"/>.
/// </summary>
public sealed class StoredQuiz : IFamilyOwned
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid FamilyId { get; private set; }

    /// <summary>The "id" from the file, unique per family.</summary>
    public string QuizId { get; private set; }

    public int Version { get; private set; }
    public string Title { get; private set; }
    public string Subject { get; private set; }
    public int GradeLevel { get; private set; }
    public string? Topic { get; private set; }
    public int MaxPoints { get; private set; }
    public string Source { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }

    /// <summary>Archived versions keep their results but are no longer listed or assignable.</summary>
    public bool Archived { get; private set; }

    /// <summary>When the family shared the quiz with all families on this server; null when it is private.</summary>
    public DateTimeOffset? SharedAt { get; private set; }

    public bool IsShared => SharedAt is not null;

    // For the persistence adapter
    private StoredQuiz(Guid familyId, string quizId, int version, string title, string subject, int gradeLevel, string? topic, int maxPoints, string source, DateTimeOffset uploadedAt)
    {
        FamilyId = familyId;
        QuizId = quizId;
        Version = version;
        Title = title;
        Subject = subject;
        GradeLevel = gradeLevel;
        Topic = topic;
        MaxPoints = maxPoints;
        Source = source;
        UploadedAt = uploadedAt;
    }

    public static StoredQuiz Create(Guid familyId, Quiz quiz, string source, int version, DateTimeOffset uploadedAt) =>
        new(familyId, quiz.Id, version, quiz.Title, quiz.Subject, quiz.GradeLevel, quiz.Topic, quiz.MaxPoints, source, uploadedAt);

    public void Archive() => Archived = true;

    /// <summary>Other families can see, preview and copy the quiz; a new version stays shared.</summary>
    public void Share(DateTimeOffset at) => SharedAt ??= at;

    public void Unshare() => SharedAt = null;

    /// <summary>A new version keeps the sharing of the version it replaces.</summary>
    public void KeepSharingOf(StoredQuiz previous) => SharedAt = previous.SharedAt;
}
