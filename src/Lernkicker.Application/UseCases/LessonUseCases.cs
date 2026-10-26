using System.Text;
using Lernkicker.Application.Ports;
using Lernkicker.Domain.Lessons;

namespace Lernkicker.Application.UseCases;

/// <summary>
/// Learning material: parents upload lessons (lesson/v1) and give them to a child; the child reads and practises
/// them and marks the blocks they know, which parents see as progress.
/// </summary>
public sealed class LessonUseCases(
    ILessonRepository lessons, IChildRepository children, ILessonFormat format, ImageUseCases images, IUnitOfWork unitOfWork, TimeProvider time)
{
    public async Task<IReadOnlyList<LessonSummary>> ListAsync() =>
        (await lessons.ListAsync())
            .OrderBy(l => l.Subject).ThenBy(l => l.Title)
            .Select(Summary)
            .ToList();

    /// <summary>
    /// Adds a lesson, or replaces the one with the same id (assignments and progress stay); <paramref name="files"/>
    /// are the pictures uploaded with it.
    /// </summary>
    public async Task<Result<LessonSummary>> UploadAsync(Guid familyId, string json, IReadOnlyList<UploadedFile>? files = null)
    {
        if (Encoding.UTF8.GetByteCount(json) > LessonLimits.MaxBytes)
        {
            return new AppError(ErrorKind.TooLarge, ErrorCodes.LessonTooLarge);
        }

        var parsed = format.Parse(json);
        if (!parsed.IsValid)
        {
            return AppError.Invalid(ErrorCodes.LessonInvalid, parsed.Issues);
        }

        var lesson = parsed.Lesson!;
        var (attached, imageIssues) = images.Attach(lesson.Images(), files ?? []);
        if (imageIssues.Count > 0)
        {
            return AppError.Invalid(ErrorCodes.LessonInvalid, imageIssues);
        }

        var stored = await lessons.FindAsync(lesson.Id);
        if (stored is null)
        {
            stored = new StoredLesson(familyId, lesson, json, time.GetUtcNow());
            lessons.Add(stored);
        }
        else
        {
            stored.Replace(lesson, json, time.GetUtcNow());
        }

        await images.AttachToLessonAsync(familyId, stored.Id, attached);
        await unitOfWork.SaveChangesAsync();
        await images.RemoveUnusedAsync();
        return Summary(stored);
    }

    /// <summary>The file exactly as uploaded.</summary>
    public async Task<Result<string>> SourceAsync(string lessonId) =>
        await lessons.FindAsync(lessonId) is { } lesson ? lesson.Source : AppError.NotFound(ErrorCodes.LessonNotFound);

    /// <summary>The file with its pictures, to download it as a zip.</summary>
    public async Task<Result<DocumentPackage>> PackageAsync(string lessonId) =>
        await lessons.FindAsync(lessonId) is { } lesson
            ? new DocumentPackage(lesson.LessonId, lesson.Source, await images.LessonFilesAsync(lesson.Id))
            : AppError.NotFound(ErrorCodes.LessonNotFound);

    /// <summary>The lesson as a child sees it.</summary>
    public async Task<Result<LessonToLearn>> PreviewAsync(string lessonId) =>
        await lessons.FindAsync(lessonId) is { } lesson
            ? LessonToLearn.Preview(format.Read(lesson.Source), await images.ForLessonAsync(lesson.Id))
            : AppError.NotFound(ErrorCodes.LessonNotFound);

    /// <summary>Shares the lesson with all families on this server, or makes it private again (copies stay).</summary>
    public async Task<Result<LessonSummary>> SetSharedAsync(string lessonId, bool shared)
    {
        var lesson = await lessons.FindAsync(lessonId);
        if (lesson is null)
        {
            return AppError.NotFound(ErrorCodes.LessonNotFound);
        }

        if (shared)
        {
            lesson.Share(time.GetUtcNow());
        }
        else
        {
            lesson.Unshare();
        }

        await unitOfWork.SaveChangesAsync();
        return Summary(lesson);
    }

    /// <summary>Removes the lesson together with its assignments and the children's progress.</summary>
    public async Task<Result> DeleteAsync(string lessonId)
    {
        var lesson = await lessons.FindAsync(lessonId);
        if (lesson is null)
        {
            return AppError.NotFound(ErrorCodes.LessonNotFound);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync();
        await lessons.RemoveAssignmentsAsync(lesson.Id);
        lessons.Remove(lesson);
        await unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
        await images.RemoveUnusedAsync();
        return Result.Ok();
    }

    public async Task<Result<LessonAssignmentSummary>> AssignAsync(Guid childId, string lessonId, DateOnly? dueOn)
    {
        var child = await children.FindAsync(childId);
        if (child is null)
        {
            return AppError.NotFound(ErrorCodes.ChildNotFound);
        }

        var lesson = await lessons.FindAsync(lessonId);
        if (lesson is null)
        {
            return AppError.NotFound(ErrorCodes.LessonNotFound);
        }

        var assignment = await lessons.FindAssignmentForLessonAsync(childId, lesson.Id);
        if (assignment is null)
        {
            assignment = new LessonAssignment(child.FamilyId, childId, lesson.Id, dueOn, time.GetUtcNow());
            lessons.Add(assignment);
        }

        assignment.SetDueOn(dueOn);
        await unitOfWork.SaveChangesAsync();
        return Summary(assignment, lesson);
    }

    public async Task<Result> UnassignAsync(Guid childId, Guid assignmentId)
    {
        var assignment = await lessons.FindAssignmentAsync(childId, assignmentId);
        if (assignment is null)
        {
            return AppError.NotFound();
        }

        lessons.Remove(assignment);
        await unitOfWork.SaveChangesAsync();
        return Result.Ok();
    }

    /// <summary>A child's lessons with progress, for parents.</summary>
    public async Task<Result<IReadOnlyList<LessonAssignmentSummary>>> ListAssignmentsAsync(Guid childId)
    {
        if (await children.FindAsync(childId) is null)
        {
            return AppError.NotFound(ErrorCodes.ChildNotFound);
        }

        var list = new List<LessonAssignmentSummary>();
        foreach (var (assignment, lesson) in await WithLessonsAsync(childId))
        {
            list.Add(Summary(assignment, lesson));
        }

        return Result<IReadOnlyList<LessonAssignmentSummary>>.Ok(list);
    }

    /// <summary>The child's lessons, the ones due soonest first.</summary>
    public async Task<IReadOnlyList<ChildLesson>> MyLessonsAsync(Guid childId) =>
        (await WithLessonsAsync(childId))
            .Select(x => new ChildLesson(x.Assignment.Id, x.Lesson.Title, x.Lesson.Subject, x.Lesson.Topic, x.Assignment.DueOn, x.Assignment.KnownBlocks.Count, x.Lesson.BlockCount))
            .OrderBy(l => l.DueOn ?? DateOnly.MaxValue)
            .ThenBy(l => l.Title, StringComparer.CurrentCulture)
            .ToList();

    public async Task<Result<LessonToLearn>> LessonAsync(Guid childId, Guid assignmentId)
    {
        var assignment = await lessons.FindAssignmentAsync(childId, assignmentId);
        if (assignment is null || await lessons.FindAsync(assignment.StoredLessonId) is not { } stored)
        {
            return AppError.NotFound(ErrorCodes.LessonNotFound);
        }

        return new LessonToLearn(assignment.Id, format.Read(stored.Source), assignment.DueOn, assignment.KnownBlocks, await images.ForLessonAsync(stored.Id));
    }

    /// <summary>The child finished a practice session and says which blocks they know.</summary>
    public async Task<Result<LessonToLearn>> PractisedAsync(Guid childId, Guid assignmentId, IReadOnlyList<string> knownBlocks)
    {
        var assignment = await lessons.FindAssignmentAsync(childId, assignmentId);
        if (assignment is null || await lessons.FindAsync(assignment.StoredLessonId) is not { } stored)
        {
            return AppError.NotFound(ErrorCodes.LessonNotFound);
        }

        var lesson = format.Read(stored.Source);
        assignment.Practised(knownBlocks, lesson.Blocks.Select(b => b.Id).ToHashSet(), time.GetUtcNow());
        await unitOfWork.SaveChangesAsync();
        return new LessonToLearn(assignment.Id, lesson, assignment.DueOn, assignment.KnownBlocks, await images.ForLessonAsync(stored.Id));
    }

    private async Task<IReadOnlyList<(LessonAssignment Assignment, StoredLesson Lesson)>> WithLessonsAsync(Guid childId)
    {
        var assignments = await lessons.ListAssignmentsAsync(childId);
        if (assignments.Count == 0)
        {
            return [];
        }

        var library = (await lessons.ListAsync()).ToDictionary(l => l.Id);
        return assignments
            .Where(a => library.ContainsKey(a.StoredLessonId))
            .Select(a => (a, library[a.StoredLessonId]))
            .ToList();
    }

    private static LessonSummary Summary(StoredLesson lesson) =>
        new(lesson.LessonId, lesson.Title, lesson.Subject, lesson.GradeLevel, lesson.Topic, lesson.BlockCount, lesson.UploadedAt, lesson.IsShared);

    private static LessonAssignmentSummary Summary(LessonAssignment assignment, StoredLesson lesson) =>
        new(assignment.Id, lesson.LessonId, lesson.Title, lesson.Subject, assignment.DueOn, assignment.AssignedAt,
            assignment.PracticeCount, assignment.LastPracticedAt, assignment.KnownBlocks, lesson.BlockCount);
}
