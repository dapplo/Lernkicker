using Lernkicker.Application;
using Lernkicker.Application.Ports;
using Microsoft.AspNetCore.Identity;

namespace Lernkicker.Persistence;

public static class Roles
{
    public const string Admin = "Admin";
}

internal sealed class ParentAccounts(UserManager<ParentUser> users) : IParentAccounts
{
    public async Task<Result<Guid>> CreateAsync(NewParent parent)
    {
        var user = new ParentUser
        {
            UserName = parent.Email,
            Email = parent.Email,
            DisplayName = parent.DisplayName,
            FamilyId = parent.FamilyId,
            Language = parent.Language,
        };
        var created = await users.CreateAsync(user, parent.Password);
        if (created.Succeeded)
        {
            return user.Id;
        }

        // Identity's codes (DuplicateEmail, PasswordTooShort, ...) are stable; the UI translates them
        var code = created.Errors.Select(e => e.Code).FirstOrDefault(c => !c.StartsWith("DuplicateUserName", StringComparison.Ordinal))
            ?? created.Errors.First().Code;
        return AppError.Invalid(ErrorCodes.AccountPrefix + code);
    }

    public async Task<ParentAccount?> FindAsync(Guid id)
    {
        var user = await users.FindByIdAsync(id.ToString());
        return user is null
            ? null
            : new ParentAccount(user.Id, user.Email!, user.DisplayName, user.FamilyId, user.Language, await users.IsInRoleAsync(user, Roles.Admin));
    }

    public async Task<bool> AdminExistsAsync() => (await users.GetUsersInRoleAsync(Roles.Admin)).Count > 0;

    public async Task MakeAdminAsync(Guid id)
    {
        var user = await users.FindByIdAsync(id.ToString()) ?? throw new InvalidOperationException($"No parent {id}");
        var result = await users.AddToRoleAsync(user, Roles.Admin);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Making the admin failed: " + string.Join(" ", result.Errors.Select(e => e.Code)));
        }
    }

    public async Task SetLanguageAsync(Guid id, string language)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is not null)
        {
            user.Language = language;
            await users.UpdateAsync(user);
        }
    }
}

/// <summary>PINs get the same slow hash as passwords, so a leaked database does not reveal them quickly.</summary>
internal sealed class PinHasher : IPinHasher
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object User = new();

    public string Hash(string pin) => Hasher.HashPassword(User, pin);

    public bool Verify(string hash, string pin) => Hasher.VerifyHashedPassword(User, hash, pin) != PasswordVerificationResult.Failed;
}
