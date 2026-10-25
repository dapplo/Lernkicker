namespace Lernkicker.Contracts;

public sealed record ShareRequest(bool Shared);

/// <summary>
/// A quiz or lesson another family shared. <c>Kind</c> is "quiz" or "lesson"; <c>Size</c> is the points of a
/// quiz or the parts of a lesson; <c>InMyLibrary</c>: copying replaces the own one with the same id.
/// </summary>
public sealed record SharedOverview(
    Guid Id, string Kind, string ItemId, string Title, string Subject, int GradeLevel, string? Topic, int Size, DateTimeOffset SharedAt, bool InMyLibrary);

/// <summary>The kinds in <see cref="SharedOverview"/>; the same values as the application's SharedKinds.</summary>
public static class SharedKinds
{
    public const string Quiz = "quiz";
    public const string Lesson = "lesson";
}
