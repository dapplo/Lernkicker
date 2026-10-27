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
    IParentAccounts parents,
    IPinHasher pinHasher,
    IUnitOfWork unitOfWork,
    TimeProvider time)
{
    public const int MaxAttemptsListed = 200;

    public Task<IReadOnlyList<ChildSummary>> ListAsync() => children.ListSummariesAsync();

    /// <summary>The parents (with their own learner profile, if they learn) and the other learner profiles.</summary>
    public async Task<IReadOnlyList<FamilyMemberSummary>> ListMembersAsync(Guid familyId)
    {
        var learners = await children.ListSummariesAsync();
        var members = new List<FamilyMemberSummary>();
        var linked = new HashSet<Guid>();
        foreach (var parent in await parents.ListFamilyAsync(familyId))
        {
            var own = learners.FirstOrDefault(l => l.LearnerParentId == parent.Id);
            if (own is not null)
            {
                linked.Add(own.Id);
            }

            members.Add(new FamilyMemberSummary(FamilyRoles.Parent, parent.DisplayName, parent.Id, own?.Id, null, own?.Theme));
        }

        members.AddRange(learners.Where(l => !linked.Contains(l.Id)).Select(l =>
            new FamilyMemberSummary(l.IsAdult ? FamilyRoles.Adult : FamilyRoles.Child, l.Name, null, l.Id, l.GradeLevel, l.Theme)));
        return members;
    }

    /// <summary>Lets a parent of the family learn: gives them their own adult profile, like "learn myself" does.</summary>
    public async Task<Result<FamilyMemberSummary>> EnableLearningAsync(Guid familyId, Guid parentId)
    {
        var parent = await parents.FindAsync(parentId);
        if (parent is null || parent.FamilyId != familyId)
        {
            return AppError.NotFound();
        }

        var child = await LearnerProfileAsync(familyId, parentId, parent.DisplayName);
        return new FamilyMemberSummary(FamilyRoles.Parent, parent.DisplayName, parentId, child.Id, null, child.Theme);
    }

    /// <summary>
    /// A null <paramref name="pin"/> leaves the profile without one: a parent sets it before pairing a phone, or a
    /// parent learns with an adult profile through their own login, which needs no PIN.
    /// </summary>
    public async Task<Result<ChildSummary>> CreateAsync(Guid familyId, string name, int? gradeLevel, string? pin, bool isAdult = false, string? theme = null)
    {
        if (Validate(name, gradeLevel, isAdult, theme) is { } error)
        {
            return error;
        }

        if (pin is not null && !Pin.IsValid(pin))
        {
            return AppError.Invalid(ErrorCodes.PinInvalid);
        }

        var child = new Child(familyId, name, gradeLevel, isAdult, theme);
        if (pin is not null)
        {
            child.SetPinHash(pinHasher.Hash(pin));
        }

        children.Add(child);
        await unitOfWork.SaveChangesAsync();
        return new ChildSummary(child.Id, child.Name, child.GradeLevel, pin is not null, 0, child.IsAdult, child.Theme);
    }

    /// <summary>A null <paramref name="theme"/> keeps the profile's look.</summary>
    public async Task<Result> UpdateAsync(Guid childId, string name, int? gradeLevel, bool isAdult = false, string? theme = null)
    {
        if (Validate(name, gradeLevel, isAdult, theme) is { } error)
        {
            return error;
        }

        var child = await children.FindAsync(childId);
        if (child is null)
        {
            return AppError.NotFound(ErrorCodes.ChildNotFound);
        }

        child.Update(name, gradeLevel, isAdult, theme);
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

    /// <summary>
    /// The parent's own adult profile, created on first use and named after the parent. The web adapter logs the
    /// parent into it; their own login stands in for a paired device and PIN.
    /// </summary>
    public async Task<LearnerSession> LearnMyselfAsync(Guid familyId, Guid parentId, string parentName)
    {
        var child = await LearnerProfileAsync(familyId, parentId, parentName);
        return new LearnerSession(child.FamilyId, child.Id, child.Name);
    }

    /// <summary>Makes an existing adult profile the parent's own (instead of any other) and logs the parent into it.</summary>
    public async Task<Result<LearnerSession>> LearnAsAsync(Guid childId, Guid parentId)
    {
        var child = await children.FindAsync(childId);
        if (child is null)
        {
            return AppError.NotFound(ErrorCodes.ChildNotFound);
        }

        if (!child.CanBeLearnedBy(parentId))
        {
            return AppError.Invalid(ErrorCodes.ChildNotOwnProfile);
        }

        if (await children.FindLearnerProfileAsync(parentId) is { } previous && previous.Id != child.Id)
        {
            previous.SetLearner(null);
        }

        child.SetLearner(parentId);
        await unitOfWork.SaveChangesAsync();
        return new LearnerSession(child.FamilyId, child.Id, child.Name);
    }

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

    private async Task<Child> LearnerProfileAsync(Guid familyId, Guid parentId, string parentName)
    {
        var child = await children.FindLearnerProfileAsync(parentId);
        if (child is null)
        {
            var name = parentName.Trim();
            child = new Child(familyId, name[..Math.Min(name.Length, Child.MaxNameLength)], null, isAdult: true);
            child.SetLearner(parentId);
            children.Add(child);
            await unitOfWork.SaveChangesAsync();
        }

        return child;
    }

    private static AppError? Validate(string? name, int? gradeLevel, bool isAdult, string? theme)
    {
        if (!Child.IsValidName(name))
        {
            return AppError.Invalid(ErrorCodes.ChildNameInvalid);
        }

        if (!ChildThemes.IsValid(theme))
        {
            return AppError.Invalid(ErrorCodes.ThemeInvalid);
        }

        return Child.IsValidGradeLevel(gradeLevel, isAdult) ? null : AppError.Invalid(ErrorCodes.GradeLevelInvalid);
    }
}
