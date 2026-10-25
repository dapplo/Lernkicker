using Lernkicker.Application.Ports;
using Lernkicker.Domain.Families;

namespace Lernkicker.Application.UseCases;

public sealed record RedeemInvitation(string Code, string Email, string Password, string DisplayName, bool PrivacyAccepted, string? Language);

/// <summary>UI languages. Content of quizzes has its own language.</summary>
public static class Languages
{
    public const string Default = "de";

    public static readonly IReadOnlyList<string> Supported = ["de", "en"];

    public static string OrDefault(string? language) => language is not null && Supported.Contains(language) ? language : Default;
}

/// <summary>Parent accounts: joining through an invitation and the own profile. Login itself is the web adapter's job.</summary>
public sealed class AccountUseCases(IFamilyRepository families, IParentAccounts accounts, IUnitOfWork unitOfWork, TimeProvider time)
{
    public async Task<Result<Guid>> RedeemInvitationAsync(RedeemInvitation request)
    {
        if (!request.PrivacyAccepted)
        {
            return AppError.Invalid(ErrorCodes.ConsentRequired);
        }

        var invitation = await families.FindInvitationAsync(Secrets.Hash(request.Code));
        var now = time.GetUtcNow();
        if (invitation is null || !invitation.IsUsable(now))
        {
            return AppError.Invalid(ErrorCodes.InvitationInvalid);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync();
        var familyId = invitation.FamilyId;
        if (familyId is null)
        {
            var family = new Family(invitation.NewFamilyName ?? request.DisplayName.Trim(), now);
            families.Add(family);
            await unitOfWork.SaveChangesAsync();
            familyId = family.Id;
        }

        var parent = new NewParent(request.Email.Trim(), request.Password, request.DisplayName.Trim(), familyId.Value, Languages.OrDefault(request.Language));
        var created = await accounts.CreateAsync(parent);
        if (!created.IsSuccess)
        {
            // Disposing the transaction without commit also drops the new family
            return created.Error!;
        }

        invitation.Redeem(now);
        await unitOfWork.SaveChangesAsync();
        await transaction.CommitAsync();
        return created.Value;
    }

    public async Task<Result<ParentProfile>> ProfileAsync(Guid parentId)
    {
        var account = await accounts.FindAsync(parentId);
        var family = account is null ? null : await families.FindAsync(account.FamilyId);
        if (account is null || family is null)
        {
            return AppError.NotFound();
        }

        return new ParentProfile(account.DisplayName, account.Email, family.Name, account.IsAdmin, account.Language);
    }

    public async Task<Result> SetLanguageAsync(Guid parentId, string language)
    {
        if (!Languages.Supported.Contains(language))
        {
            return AppError.Invalid(ErrorCodes.LanguageInvalid);
        }

        await accounts.SetLanguageAsync(parentId, language);
        return Result.Ok();
    }
}
