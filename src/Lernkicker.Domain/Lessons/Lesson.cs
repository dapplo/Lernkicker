using Lernkicker.Domain.Families;
using Lernkicker.Domain.Quizzes;

namespace Lernkicker.Domain.Lessons;

/// <summary>Learning material in the lesson/v1 format (docs/lesson-format.md): blocks to read and learn.</summary>
public sealed record Lesson
{
    public const string Format = "lesson/v1";

    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Subject { get; init; }
    public required int GradeLevel { get; init; }
    public string Language { get; init; } = "de";
    public string? Topic { get; init; }
    public string? Description { get; init; }
    public string? Source { get; init; }
    public required IReadOnlyList<LessonBlock> Blocks { get; init; }
}

public abstract record LessonBlock
{
    public required string Id { get; init; }
    public string? Title { get; init; }
}

/// <summary>Text to read; paragraphs, **bold**, *italic* and "- " lists.</summary>
public sealed record TextBlock : LessonBlock
{
    public required string Text { get; init; }
}

/// <summary>A poem to learn by heart: stanzas of lines.</summary>
public sealed record PoemBlock : LessonBlock
{
    public string? Author { get; init; }
    public string? Hint { get; init; }
    public required IReadOnlyList<IReadOnlyList<string>> Stanzas { get; init; }
}

public sealed record FlashcardsBlock : LessonBlock
{
    public required IReadOnlyList<Flashcard> Cards { get; init; }
}

public sealed record Flashcard(string Front, string Back);

/// <summary>Key facts to remember.</summary>
public sealed record FactsBlock : LessonBlock
{
    public required IReadOnlyList<string> Items { get; init; }
}

public static class LessonLimits
{
    public const int MaxBytes = 256 * 1024;
}

/// <summary>Content rules that JSON Schema cannot express (same rules as tools/validate_quizzes.py).</summary>
public static class LessonRules
{
    public static IReadOnlyList<QuizIssue> Check(Lesson lesson)
    {
        var ids = new HashSet<string>();
        return lesson.Blocks
            .Select((block, i) => (block, i))
            .Where(b => !ids.Add(b.block.Id))
            .Select(b => QuizIssue.Of(QuizIssueCodes.DuplicateId, $"blocks/{b.i}/id", ("id", b.block.Id)))
            .ToList();
    }
}

/// <summary>A lesson in a family's library. Uploading the same <see cref="LessonId"/> again replaces it.</summary>
public sealed class StoredLesson : IFamilyOwned
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid FamilyId { get; private set; }
    public string LessonId { get; private set; }
    public string Title { get; private set; }
    public string Subject { get; private set; }
    public int GradeLevel { get; private set; }
    public string? Topic { get; private set; }

    /// <summary>Number of blocks, so lists need not read the file.</summary>
    public int BlockCount { get; private set; }

    public string Source { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }

    /// <summary>When the family shared the lesson with all families on this server; null when it is private.</summary>
    public DateTimeOffset? SharedAt { get; private set; }

    public bool IsShared => SharedAt is not null;

    public StoredLesson(Guid familyId, Lesson lesson, string source, DateTimeOffset uploadedAt)
    {
        FamilyId = familyId;
        LessonId = lesson.Id;
        Title = Subject = Source = "";
        Replace(lesson, source, uploadedAt);
    }

    // For the persistence adapter
    private StoredLesson()
    {
        LessonId = Title = Subject = Source = "";
    }

    /// <summary>A new upload with the same id; assignments and progress stay.</summary>
    public void Replace(Lesson lesson, string source, DateTimeOffset uploadedAt)
    {
        Title = lesson.Title;
        Subject = lesson.Subject;
        GradeLevel = lesson.GradeLevel;
        Topic = lesson.Topic;
        BlockCount = lesson.Blocks.Count;
        Source = source;
        UploadedAt = uploadedAt;
    }

    /// <summary>Other families can see, preview and copy the lesson; a replaced lesson stays shared.</summary>
    public void Share(DateTimeOffset at) => SharedAt ??= at;

    public void Unshare() => SharedAt = null;
}

/// <summary>
/// A lesson a parent gave a child to learn, optionally until a day (e.g. when the poem is recited), with the
/// child's progress: how often they practised and which blocks they say they know.
/// </summary>
public sealed class LessonAssignment : IFamilyOwned
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid FamilyId { get; private set; }
    public Guid ChildId { get; private set; }
    public Guid StoredLessonId { get; private set; }
    public DateOnly? DueOn { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }
    public int PracticeCount { get; private set; }
    public DateTimeOffset? LastPracticedAt { get; private set; }

    /// <summary>Ids of the blocks the child marked as "I know it".</summary>
    public IReadOnlyList<string> KnownBlocks { get; private set; } = [];

    public LessonAssignment(Guid familyId, Guid childId, Guid storedLessonId, DateOnly? dueOn, DateTimeOffset assignedAt)
    {
        FamilyId = familyId;
        ChildId = childId;
        StoredLessonId = storedLessonId;
        DueOn = dueOn;
        AssignedAt = assignedAt;
    }

    public void SetDueOn(DateOnly? dueOn) => DueOn = dueOn;

    /// <summary>One practice session; <paramref name="knownBlocks"/> replaces the previous marks.</summary>
    public void Practised(IEnumerable<string> knownBlocks, IReadOnlyCollection<string> existingBlocks, DateTimeOffset at)
    {
        PracticeCount++;
        LastPracticedAt = at;
        KnownBlocks = knownBlocks.Where(existingBlocks.Contains).Distinct().Order(StringComparer.Ordinal).ToList();
    }
}
