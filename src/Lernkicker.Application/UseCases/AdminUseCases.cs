using Lernkicker.Application.Ports;
using Lernkicker.Domain.Families;

namespace Lernkicker.Application.UseCases;

/// <summary>Administration of the installation. Never shows children's results.</summary>
public sealed class AdminUseCases(IFamilyRepository families, ImageUseCases images, IUnitOfWork unitOfWork, TimeProvider time)
{
    /// <summary>Invites a parent into an existing family, or into a new family when <paramref name="familyId"/> is null.</summary>
    public async Task<Result<InvitationCreated>> CreateInvitationAsync(Guid? familyId, string? newFamilyName)
    {
        if (familyId is { } id && await families.FindAsync(id) is null)
        {
            return AppError.NotFound(ErrorCodes.FamilyNotFound);
        }

        var code = Secrets.NewToken();
        var invitation = new Invitation(Secrets.Hash(code), familyId, newFamilyName, time.GetUtcNow() + Invitation.Lifetime);
        families.Add(invitation);
        await unitOfWork.SaveChangesAsync();
        return new InvitationCreated(code, invitation.ExpiresAt);
    }

    public Task<IReadOnlyList<FamilySummary>> ListFamiliesAsync() => families.ListSummariesAsync();

    public async Task<Result> DeleteFamilyAsync(Guid adminFamilyId, Guid familyId)
    {
        if (adminFamilyId == familyId)
        {
            return AppError.Invalid(ErrorCodes.FamilyIsOwn);
        }

        if (!await families.DeleteAsync(familyId))
        {
            return AppError.NotFound(ErrorCodes.FamilyNotFound);
        }

        await images.RemoveUnusedAsync();
        return Result.Ok();
    }
}
