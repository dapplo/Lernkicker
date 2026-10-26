using Lernkicker.Application.Ports;
using Lernkicker.Domain.Families;
using Lernkicker.Domain.Practice;
using Lernkicker.Domain.Quizzes;

namespace Lernkicker.Application.UseCases;

/// <summary>What a parent does for their children: profiles, PIN, paired devices, assignments and results.</summary>
public sealed class ChildrenUseCases(
    IChildRepository children,
    IQuizLibrary library,
    IPracticeRepository practice,
    IPinHasher pinHasher,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    public const int MaxAttemptsListed = 200;

    public Task<IReadOnlyList<ChildSummary>> ListAsync() => children.ListSummariesAsync();

    public async Task<Result<ChildSummary>> CreateAsync(Guid familyId, string name, int? gradeLevel, string pin, bool isAdult = false)
    {
        if (Validate(name, gradeLevel, isAdult) is { } error)
        {
            return error;
        }

        if (!Pin.IsValid(pin))
        {
            return AppError.Invalid(ErrorCodes.PinInvalid);
        }

        var child = new Child(familyId, name, gradeLevel, isAdult);
        child.SetPinHash(pinHasher.Hash(pin));
        children.Add(child);
        await unitOfWork.SaveChangesAsync();
        return new ChildSummary(child.Id, child.Name, child.GradeLevel, true, 0, child.IsAdult);
    }

    public async Task<Result> UpdateAsync(Guid childId, string name, int? gradeLevel, bool isAdult = false)
    {
        if (Validate(name, gradeLevel, isAdult) is { } error)
        {
            return error;
        }

        var child = await children.FindAsync(childId);
        if (child is null)
        {
            return AppError.NotFound(ErrorCodes.ChildNotFound);
        }

        child.Update(name, gradeLevel, isAdult);
        await unitOfWork.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> SetPinAsync(Guid childId, string pin)
    {
        if (!Pin.IsValid(pin))
        {
            return AppError.Invalid(ErrorCodes.PinInvalid);
        }

        var child = await children.FindAsync(childId);
        if (child is null)
        {
            return AppError.NotFound(ErrorCodes.ChildNotFound);
        }

        child.SetPinHash(pinHasher.Hash(pin));
        await unitOfWork.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(Guid childId) =>
        await children.DeleteAsync(childId) ? Result.Ok() : AppError.NotFound(ErrorCodes.ChildNotFound);

    /// <summary>A one-time code for the QR code that pairs the child's phone.</summary>
    public async Task<Result<PairingCodeCreated>> CreatePairingCodeAsync(Guid childId)
    {
        var child = await children.FindAsync(childId);
        if (child is null)
        {
            return AppError.NotFound(ErrorCodes.ChildNotFound);
        }

        var code = Secrets.NewShortCode();
        var pairing = new PairingCode(child.FamilyId, child.Id, Secrets.ShortCodeHash(code), time.GetUtcNow() + PairingCode.Lifetime);
        children.Add(pairing);
        await unitOfWork.SaveChangesAsync();
        return new PairingCodeCreated(code, pairing.ExpiresAt);
    }

    public async Task<IReadOnlyList<DeviceSummary>> ListDevicesAsync(Guid childId) =>
        (await children.ListDevicesAsync(childId)).OrderBy(d => d.PairedAt).Select(DeviceSummary.From).ToList();

    public async Task<Result> RemoveDeviceAsync(Guid childId, Guid deviceId)
    {
        var device = await children.FindDeviceAsync(childId, deviceId);
        if (device is null)
        {
            return AppError.NotFound();
        }

        children.Remove(device);
        await unitOfWork.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<Result> UnlockDeviceAsync(Guid childId, Guid deviceId)
    {
        var device = await children.FindDeviceAsync(childId, deviceId);
        if (device is null)
        {
            return AppError.NotFound();
        }

        device.Unlock();
        await unitOfWork.SaveChangesAsync();
        return Result.Ok();
    }

    public async Task<IReadOnlyList<AssignmentSummary>> ListAssignmentsAsync(Guid childId) =>
        (await practice.ListAssignmentSummariesAsync(childId))
            .OrderBy(a => a.DueOn ?? DateOnly.MaxValue).ThenBy(a => a.AssignedAt)
            .ToList();

    /// <summary>Assigns the current version of a quiz; assigning it again only changes the due date.</summary>
    public async Task<Result<AssignmentSummary>> AssignAsync(Guid childId, string quizId, DateOnly? dueOn)
    {
        var child = await children.FindAsync(childId);
        if (child is null)
        {
            return AppError.NotFound(ErrorCodes.ChildNotFound);
        }

        var quiz = await library.FindCurrentAsync(quizId);
        if (quiz is null)
        {
            return AppError.NotFound(ErrorCodes.QuizNotFound);
        }

        var assignment = await practice.FindAssignmentForQuizAsync(childId, quiz.Id);
        if (assignment is null)
        {
            assignment = new Assignment(child.FamilyId, childId, quiz.Id, time.GetUtcNow());
            practice.Add(assignment);
        }

        assignment.SetDueOn(dueOn);
        await unitOfWork.SaveChangesAsync();
        return new AssignmentSummary(assignment.Id, quiz.Id, quiz.QuizId, quiz.Version, quiz.Title, quiz.Subject, quiz.Topic, quiz.MaxPoints, assignment.DueOn, assignment.AssignedAt);
    }

    public async Task<Result> UnassignAsync(Guid childId, Guid assignmentId)
    {
        var assignment = await practice.FindAssignmentAsync(childId, assignmentId);
        if (assignment is null)
        {
            return AppError.NotFound();
        }

        practice.Remove(assignment);
        await unitOfWork.SaveChangesAsync();
        return Result.Ok();
    }

    public Task<IReadOnlyList<AttemptSummary>> ListAttemptsAsync(Guid childId) =>
        practice.ListAttemptSummariesAsync(childId, MaxAttemptsListed);

    public async Task<Result<QuizResult>> AttemptResultAsync(Guid childId, Guid attemptId) =>
        await practice.FindAttemptAsync(childId, attemptId) is { } attempt ? attempt.Result : AppError.NotFound();

    private static AppError? Validate(string? name, int? gradeLevel, bool isAdult)
    {
        if (!Child.IsValidName(name))
        {
            return AppError.Invalid(ErrorCodes.ChildNameInvalid);
        }

        return Child.IsValidGradeLevel(gradeLevel, isAdult) ? null : AppError.Invalid(ErrorCodes.GradeLevelInvalid);
    }
}
