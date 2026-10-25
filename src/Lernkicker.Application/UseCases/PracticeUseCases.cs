using Lernkicker.Application.Ports;
using Lernkicker.Domain.Practice;
using Lernkicker.Domain.Quizzes;

namespace Lernkicker.Application.UseCases;

/// <summary>A logged-in child practising: assigned quizzes, the sheet without solutions, handing in answers.</summary>
public sealed class PracticeUseCases(IPracticeRepository practice, IQuizLibrary library, IQuizFormat format, IUnitOfWork unitOfWork, TimeProvider time)
{
    public async Task<IReadOnlyList<ChildAssignment>> AssignmentsAsync(Guid childId)
    {
        var assignments = await practice.ListAssignmentSummariesAsync(childId);
        var attempts = await practice.ListAttemptSummariesAsync(childId, int.MaxValue);

        return assignments
            .OrderBy(a => a.DueOn ?? DateOnly.MaxValue).ThenBy(a => a.AssignedAt)
            .Select(a =>
            {
                // Progress counts over all versions of the quiz
                var own = attempts.Where(t => t.QuizId == a.QuizId).Select(t => t.Percent).ToList();
                int? best = own.Count == 0 ? null : own.Max();
                return new ChildAssignment(a.Id, a.Title, a.Subject, a.Topic, a.MaxPoints, a.DueOn, own.Count, best, Stars.For(best));
            })
            .ToList();
    }

    public async Task<Result<QuizSheet>> SheetAsync(Guid childId, Guid assignmentId, Random random)
    {
        var quiz = await QuizAsync(childId, assignmentId);
        return quiz is null ? AppError.NotFound() : QuizSheet.For(quiz.Value.Quiz, random);
    }

    public async Task<Result<QuizResult>> SubmitAsync(Guid childId, Guid assignmentId, IReadOnlyDictionary<string, ExerciseAnswer> answers, string channel)
    {
        var quiz = await QuizAsync(childId, assignmentId);
        if (quiz is not { } found)
        {
            return AppError.NotFound();
        }

        var result = Grader.Grade(found.Quiz, answers);
        practice.Add(new Attempt(found.Assignment.FamilyId, childId, found.Assignment.StoredQuizId, channel, time.GetUtcNow(), answers, result));
        await unitOfWork.SaveChangesAsync();
        return result;
    }

    private async Task<(Assignment Assignment, Quiz Quiz)?> QuizAsync(Guid childId, Guid assignmentId)
    {
        var assignment = await practice.FindAssignmentAsync(childId, assignmentId);
        var stored = assignment is null ? null : await library.FindAsync(assignment.StoredQuizId);
        return assignment is null || stored is null ? null : (assignment, format.Read(stored.Source));
    }
}
