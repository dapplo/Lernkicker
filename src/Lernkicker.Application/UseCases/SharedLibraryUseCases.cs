using Lernkicker.Application.Ports;
using Lernkicker.Domain.Lessons;
using Lernkicker.Domain.Quizzes;

namespace Lernkicker.Application.UseCases;

/// <summary>
/// The shared library: quizzes and lessons that families made public for everyone on this server. Parents
/// preview them and copy them into their own library; the copy is theirs, and the original family's changes
/// or deletion do not touch it. Only the content crosses families, never assignments or results.
/// </summary>
public sealed class SharedLibraryUseCases(
    IQuizLibrary quizzes, ILessonRepository lessons, IQuizFormat quizFormat, ILessonFormat lessonFormat,
    QuizLibraryUseCases quizLibrary, LessonUseCases lessonLibrary)
{
    /// <summary>What other families shared, newest first.</summary>
    public async Task<IReadOnlyList<SharedSummary>> ListAsync(Guid familyId)
    {
        var ownQuizzes = (await quizzes.ListCurrentAsync()).Select(q => q.QuizId).ToHashSet();
        var ownLessons = (await lessons.ListAsync()).Select(l => l.LessonId).ToHashSet();

        var sharedQuizzes = (await quizzes.ListSharedAnyFamilyAsync())
            .Where(q => q.FamilyId != familyId)
            .Select(q => new SharedSummary(q.Id, SharedKinds.Quiz, q.QuizId, q.Title, q.Subject, q.GradeLevel, q.Topic, q.MaxPoints, q.SharedAt!.Value, ownQuizzes.Contains(q.QuizId)));
        var sharedLessons = (await lessons.ListSharedAnyFamilyAsync())
            .Where(l => l.FamilyId != familyId)
            .Select(l => new SharedSummary(l.Id, SharedKinds.Lesson, l.LessonId, l.Title, l.Subject, l.GradeLevel, l.Topic, l.BlockCount, l.SharedAt!.Value, ownLessons.Contains(l.LessonId)));

        return sharedQuizzes.Concat(sharedLessons).OrderByDescending(s => s.SharedAt).ToList();
    }

    public async Task<Result<QuizSheet>> PreviewQuizAsync(Guid id, Random random) =>
        await quizzes.FindSharedAnyFamilyAsync(id) is { } quiz
            ? QuizSheet.For(quizFormat.Read(quiz.Source), random)
            : AppError.NotFound(ErrorCodes.SharedNotFound);

    public async Task<Result<Lesson>> PreviewLessonAsync(Guid id) =>
        await lessons.FindSharedAnyFamilyAsync(id) is { } lesson
            ? lessonFormat.Read(lesson.Source)
            : AppError.NotFound(ErrorCodes.SharedNotFound);

    /// <summary>Copies the quiz into the own library, as a new version if a quiz with the same id is there already.</summary>
    public async Task<Result<QuizSummary>> CopyQuizAsync(Guid familyId, Guid id) =>
        await quizzes.FindSharedAnyFamilyAsync(id) is { } quiz && quiz.FamilyId != familyId
            ? await quizLibrary.UploadAsync(familyId, quiz.Source)
            : AppError.NotFound(ErrorCodes.SharedNotFound);

    /// <summary>Copies the lesson into the own library, replacing one with the same id (progress stays).</summary>
    public async Task<Result<LessonSummary>> CopyLessonAsync(Guid familyId, Guid id) =>
        await lessons.FindSharedAnyFamilyAsync(id) is { } lesson && lesson.FamilyId != familyId
            ? await lessonLibrary.UploadAsync(familyId, lesson.Source)
            : AppError.NotFound(ErrorCodes.SharedNotFound);
}
