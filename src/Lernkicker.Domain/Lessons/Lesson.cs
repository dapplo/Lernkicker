using Lernkicker.Domain.Families;
using Lernkicker.Domain.Media;
using Lernkicker.Domain.Quizzes;

namespace Lernkicker.Domain.Lessons;

/// <summary>Learning material in the lesson/v1 format (docs/lesson-format.md): blocks to read and learn.</summary>
public sealed record Lesson
{
    public const string Format = "lesson/v1";

    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Subject { get; init; }
    /// <summary>School year; null for material that is not for a school year (adults, work).</summary>
    public int? GradeLevel { get; init; }
    public string Language { get; init; } = "de";
    public string? Topic { get; init; }
    public string? Description { get; init; }
    public string? Source { get; init; }

    /// <summary><see cref="LessonModes.Learn"/> (marked as known part by part) or <see cref="LessonModes.Reference"/> (looked up, e.g. game rules).</summary>
    public string Mode { get; init; } = LessonModes.Learn;

    public required IReadOnlyList<LessonBlock> Blocks { get; init; }

    /// <summary>Every picture the lesson names, in file order.</summary>
    public IEnumerable<ImageUse> Images() =>
        Blocks.SelectMany((block, i) => block.Images($"blocks/{i}"));
}

public static class LessonModes
{
    public const string Learn = "learn";
    public const string Reference = "reference";
}

public abstract record LessonBlock
{
    public required string Id { get; init; }
    public string? Title { get; init; }

    public virtual IEnumerable<ImageUse> Images(string path) => [];
}

/// <summary>Text to read; paragraphs, **bold**, *italic* and "- " lists; optionally a picture above it.</summary>
public sealed record TextBlock : LessonBlock
{
    public required string Text { get; init; }
    public ImageRef? Image { get; init; }

    public override IEnumerable<ImageUse> Images(string path) => Image is null ? [] : [new ImageUse($"{path}/image", Image)];
}

/// <summary>A picture on its own, shown large.</summary>
public sealed record ImageBlock : LessonBlock
{
    public required ImageRef Image { get; init; }

    public override IEnumerable<ImageUse> Images(string path) => [new ImageUse($"{path}/image", Image)];
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

    public override IEnumerable<ImageUse> Images(string path)
    {
        for (var i = 0; i < Cards.Count; i++)
        {
            if (Cards[i].FrontImage is { } front)
            {
                yield return new ImageUse($"{path}/cards/{i}/frontImage", front);
            }

            if (Cards[i].BackImage is { } back)
            {
                yield return new ImageUse($"{path}/cards/{i}/backImage", back);
            }
        }
    }
}

public sealed record Flashcard(string Front, string Back, ImageRef? FrontImage = null, ImageRef? BackImage = null);

/// <summary>Key facts to remember; optionally a picture above them.</summary>
public sealed record FactsBlock : LessonBlock
{
    public required IReadOnlyList<string> Items { get; init; }
    public ImageRef? Image { get; init; }

    public override IEnumerable<ImageUse> Images(string path) => Image is null ? [] : [new ImageUse($"{path}/image", Image)];
}

/// <summary>Things to look up, e.g. what each card of a game does: a name, a short text and a picture each; the app can filter them.</summary>
public sealed record EntriesBlock : LessonBlock
{
    public required IReadOnlyList<LessonEntry> Entries { get; init; }

    public override IEnumerable<ImageUse> Images(string path)
    {
        for (var i = 0; i < Entries.Count; i++)
        {
            if (Entries[i].Image is { } image)
            {
                yield return new ImageUse($"{path}/entries/{i}/image", image);
            }
        }
    }
}

public sealed record LessonEntry(string Name, string Text, ImageRef? Image = null);

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
    public int? GradeLevel { get; private set; }
    public string? Topic { get; private set; }

    /// <summary>The lesson's <see cref="LessonModes">mode</see>, so lists need not read the file.</summary>
    public string Mode { get; private set; } = LessonModes.Learn;

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
        Mode = lesson.Mode;
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
