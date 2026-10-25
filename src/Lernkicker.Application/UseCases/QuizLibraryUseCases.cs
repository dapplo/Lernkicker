using System.Text;
using Lernkicker.Application.Ports;
using Lernkicker.Domain.Library;
using Lernkicker.Domain.Quizzes;

namespace Lernkicker.Application.UseCases;

/// <summary>A family's library of quizzes: upload (new version per id), list, read, delete.</summary>
public sealed class QuizLibraryUseCases(IQuizLibrary library, IPracticeRepository practice, IQuizFormat format, IUnitOfWork unitOfWork, TimeProvider time)
{
    public async Task<IReadOnlyList<QuizSummary>> ListAsync() =>
        (await library.ListCurrentAsync())
            .OrderBy(q => q.Subject).ThenBy(q => q.Title)
            .Select(QuizSummary.From)
            .ToList();

    public async Task<Result<QuizSummary>> UploadAsync(Guid familyId, string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > QuizLimits.MaxBytes)
        {
            return new AppError(ErrorKind.TooLarge, ErrorCodes.QuizTooLarge);
        }

        var parsed = format.Parse(json);
        if (!parsed.IsValid)
        {
            return AppError.Invalid(ErrorCodes.QuizInvalid, parsed.Issues);
        }

        var quiz = parsed.Quiz!;
        var previous = await library.VersionsAsync(quiz.Id);
        var stored = StoredQuiz.Create(familyId, quiz, json, previous.Count == 0 ? 1 : previous.Max(q => q.Version) + 1, time.GetUtcNow());
        library.Add(stored);

        // Open assignments move to the new version; results stay with the version they were made on.
        var assignments = await practice.AssignmentsForQuizzesAsync(previous.Select(q => q.Id).ToList());
        foreach (var perChild in assignments.GroupBy(a => a.ChildId))
        {
            perChild.First().MoveTo(stored.Id);
            foreach (var duplicate in perChild.Skip(1))
            {
                practice.Remove(duplicate);
            }
        }

        foreach (var old in previous)
        {
            old.Archive();
        }

        await unitOfWork.SaveChangesAsync();
        return QuizSummary.From(stored);
    }

    /// <summary>The current version's file, exactly as uploaded.</summary>
    public async Task<Result<string>> SourceAsync(string quizId) =>
        await library.FindCurrentAsync(quizId) is { } quiz ? quiz.Source : AppError.NotFound(ErrorCodes.QuizNotFound);

    /// <summary>Removes the quiz and its assignments. With results it is archived instead, so the history stays.</summary>
    public async Task<Result<QuizDeleted>> DeleteAsync(string quizId)
    {
        var versions = await library.VersionsAsync(quizId);
        if (versions.Count == 0)
        {
            return AppError.NotFound(ErrorCodes.QuizNotFound);
        }

        var ids = versions.Select(q => q.Id).ToList();
        foreach (var assignment in await practice.AssignmentsForQuizzesAsync(ids))
        {
            practice.Remove(assignment);
        }

        var hasResults = await practice.AnyAttemptsAsync(ids);
        foreach (var version in versions)
        {
            if (hasResults)
            {
                version.Archive();
            }
            else
            {
                library.Remove(version);
            }
        }

        await unitOfWork.SaveChangesAsync();
        return new QuizDeleted(hasResults);
    }
}
