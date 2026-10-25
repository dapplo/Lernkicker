using Lernkicker.Domain.Lessons;
using Lernkicker.Domain.Quizzes;

namespace Lernkicker.Application.Ports;

/// <summary>Reads lesson/v1 JSON (docs/lesson-format.md).</summary>
public interface ILessonFormat
{
    /// <summary>Parses and fully validates an uploaded file (schema and <see cref="LessonRules"/>).</summary>
    LessonParseResult Parse(string json);

    /// <summary>Reads a file that was validated on upload.</summary>
    Lesson Read(string storedSource);
}

public sealed record LessonParseResult(Lesson? Lesson, IReadOnlyList<QuizIssue> Issues)
{
    public bool IsValid => Lesson is not null && Issues.Count == 0;
}

/// <summary>A family's lessons and the children's lesson assignments (filtered by family).</summary>
public interface ILessonRepository
{
    void Add(StoredLesson lesson);
    void Remove(StoredLesson lesson);
    Task<IReadOnlyList<StoredLesson>> ListAsync();
    Task<StoredLesson?> FindAsync(string lessonId);
    Task<StoredLesson?> FindAsync(Guid storedLessonId);

    void Add(LessonAssignment assignment);
    void Remove(LessonAssignment assignment);
    Task<LessonAssignment?> FindAssignmentAsync(Guid childId, Guid assignmentId);
    Task<LessonAssignment?> FindAssignmentForLessonAsync(Guid childId, Guid storedLessonId);
    Task<IReadOnlyList<LessonAssignment>> ListAssignmentsAsync(Guid childId);
    Task RemoveAssignmentsAsync(Guid storedLessonId);
}
