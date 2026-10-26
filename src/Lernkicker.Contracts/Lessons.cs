namespace Lernkicker.Contracts;

public sealed record LessonOverview(string LessonId, string Title, string Subject, int? GradeLevel, string? Topic, int Blocks, DateTimeOffset UploadedAt, bool Shared);

public sealed record NewLessonAssignment(string LessonId, DateOnly? DueOn);

/// <summary>A lesson given to a child, with progress: <c>KnownBlocks</c> are the block ids the child marked as known.</summary>
public sealed record LessonAssignmentOverview(
    Guid Id, string LessonId, string Title, string Subject, DateOnly? DueOn, DateTimeOffset AssignedAt,
    int PracticeCount, DateTimeOffset? LastPracticedAt, IReadOnlyList<string> KnownBlocks, int Blocks, string Mode);

/// <summary>A lesson in the child's list.</summary>
public sealed record MyLesson(Guid AssignmentId, string Title, string Subject, string? Topic, DateOnly? DueOn, int KnownBlocks, int Blocks, string Mode);

/// <summary>
/// A lesson to learn or look things up in (<c>Mode</c> learn or reference). Each block has the fields of its <c>Type</c>
/// (text, poem, flashcards, facts, image, entries, vocabulary). <c>Games</c> are the games its content is enough for (see <see cref="LessonGameDto"/>).
/// </summary>
public sealed record LessonDto(
    Guid AssignmentId, string Title, string Subject, string Language, string? Topic, string? Description, DateOnly? DueOn,
    IReadOnlyList<string> KnownBlocks, IReadOnlyList<LessonBlockDto> Blocks, string Mode = "learn")
{
    public IReadOnlyList<string> Games { get; init; } = [];
}

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
    public ImageDto? Image { get; init; }
    public IReadOnlyList<EntryDto>? Entries { get; init; }

    /// <summary>vocabulary: the words, their languages and which way they are asked (both, wordToTranslation, translationToWord).</summary>
    public IReadOnlyList<VocabularyWordDto>? Words { get; init; }
    public string? WordLanguage { get; init; }
    public string? TranslationLanguage { get; init; }
    public string? Direction { get; init; }
}

/// <summary>A word to learn and its translation; the alternatives count as right too.</summary>
public sealed record VocabularyWordDto(
    string Word, string Translation, IReadOnlyList<string>? WordAlternatives = null, IReadOnlyList<string>? TranslationAlternatives = null, ImageDto? Image = null);

public sealed record EntryDto(string Name, string Text, ImageDto? Image = null);

public sealed record FlashcardDto(string Front, string Back, ImageDto? FrontImage = null, ImageDto? BackImage = null);

/// <summary>The end of a practice session: the blocks the child knows now.</summary>
public sealed record LessonPractice(IReadOnlyList<string> KnownBlocks);

/// <summary>
/// A round of a game made from a lesson (<c>Kind</c>: fallingWords, wordSearch, memory, crossword). Falling words and
/// word search come as an <c>Exercise</c>, like in a quiz; memory and crossword have their own fields.
/// </summary>
public sealed record LessonGameDto(Guid AssignmentId, string Title, string Language, string Kind)
{
    public ExerciseSheetDto? Exercise { get; init; }
    public IReadOnlyList<MemoryCardDto>? Cards { get; init; }
    public CrosswordDto? Crossword { get; init; }
}

/// <summary>A memory card; the two cards of a pair share <c>Pair</c>.</summary>
public sealed record MemoryCardDto(int Pair, string Text, ImageDto? Image = null);

/// <summary>A crossword of <c>Width</c> × <c>Height</c> cells; each word starts at <c>Row</c>, <c>Column</c> (from 0, top left).</summary>
public sealed record CrosswordDto(int Width, int Height, IReadOnlyList<CrosswordWordDto> Words);

public sealed record CrosswordWordDto(int Number, int Row, int Column, bool Down, string Answer, string Clue);
