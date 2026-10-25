namespace Lernkicker.Contracts;

public sealed record LessonOverview(string LessonId, string Title, string Subject, int GradeLevel, string? Topic, int Blocks, DateTimeOffset UploadedAt, bool Shared);

public sealed record NewLessonAssignment(string LessonId, DateOnly? DueOn);

/// <summary>A lesson given to a child, with progress: <c>KnownBlocks</c> are the block ids the child marked as known.</summary>
public sealed record LessonAssignmentOverview(
    Guid Id, string LessonId, string Title, string Subject, DateOnly? DueOn, DateTimeOffset AssignedAt,
    int PracticeCount, DateTimeOffset? LastPracticedAt, IReadOnlyList<string> KnownBlocks, int Blocks);

/// <summary>A lesson in the child's list.</summary>
public sealed record MyLesson(Guid AssignmentId, string Title, string Subject, string? Topic, DateOnly? DueOn, int KnownBlocks, int Blocks);

/// <summary>A lesson to learn. Each block has the fields of its <c>Type</c> (text, poem, flashcards, facts).</summary>
public sealed record LessonDto(
    Guid AssignmentId, string Title, string Subject, string Language, string? Topic, string? Description, DateOnly? DueOn,
    IReadOnlyList<string> KnownBlocks, IReadOnlyList<LessonBlockDto> Blocks);

public sealed record LessonBlockDto
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public string? Title { get; init; }
    public string? Text { get; init; }
    public string? Author { get; init; }
    public string? Hint { get; init; }
    public IReadOnlyList<IReadOnlyList<string>>? Stanzas { get; init; }
    public IReadOnlyList<FlashcardDto>? Cards { get; init; }
    public IReadOnlyList<string>? Items { get; init; }
}

public sealed record FlashcardDto(string Front, string Back);

/// <summary>The end of a practice session: the blocks the child knows now.</summary>
public sealed record LessonPractice(IReadOnlyList<string> KnownBlocks);
