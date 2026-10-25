using Lernkicker.Application.Ports;
using Lernkicker.Domain.Families;
using Lernkicker.Domain.Settings;

namespace Lernkicker.Application.UseCases;

/// <summary><c>PublicUrl</c>: the address the browser used, kept for links when it is https.</summary>
public sealed record FirstAdmin(string Email, string Password, string DisplayName, string FamilyName, string? Language, string? PublicUrl);

/// <summary>
/// The first-run setup: while no admin exists, the first visitor (inside the tailnet, the web adapter checks
/// that) creates the admin account and the own family, like most self-hosted apps.
/// </summary>
public sealed class SetupUseCases(IParentAccounts accounts, IFamilyRepository families, IServerSettings settings, IUnitOfWork unitOfWork, TimeProvider time)
{
    public const int MaxNameLength = 100;

    // Two people opening the fresh server at the same moment must not both become admin
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<bool> NeededAsync() => !await accounts.AdminExistsAsync();

    /// <summary>Creates the admin; returns the public address that was stored, if any.</summary>
    public async Task<Result<Uri?>> CompleteAsync(FirstAdmin request)
    {
        var name = request.DisplayName?.Trim() ?? "";
        var familyName = request.FamilyName?.Trim() ?? "";
        if (name.Length is 0 or > MaxNameLength)
        {
            return AppError.Invalid(ErrorCodes.SetupNameInvalid);
        }

        if (familyName.Length is 0 or > Family.MaxNameLength)
        {
            return AppError.Invalid(ErrorCodes.SetupFamilyInvalid);
        }

        await Gate.WaitAsync();
        try
        {
            if (await accounts.AdminExistsAsync())
            {
                return AppError.Invalid(ErrorCodes.SetupDone);
            }

            await using var transaction = await unitOfWork.BeginTransactionAsync();
            var now = time.GetUtcNow();
            var family = new Family(familyName, now);
            families.Add(family);
            await unitOfWork.SaveChangesAsync();

            var created = await accounts.CreateAsync(new NewParent(request.Email?.Trim() ?? "", request.Password ?? "", name, family.Id, Languages.OrDefault(request.Language)));
            if (!created.IsSuccess)
            {
                return created.Error!;
            }

            await accounts.MakeAdminAsync(created.Value);
            var publicUrl = PublicUrl(request.PublicUrl);
            if (publicUrl is not null && await settings.FindAsync(ServerSetting.PublicUrl) is null)
            {
                settings.Add(new ServerSetting(ServerSetting.PublicUrl, publicUrl.ToString(), now));
            }

            await unitOfWork.SaveChangesAsync();
            await transaction.CommitAsync();
            return publicUrl;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>The stored public address, for links in QR codes, invitations and the feed.</summary>
    public async Task<Uri?> StoredPublicUrlAsync() =>
        await settings.FindAsync(ServerSetting.PublicUrl) is { } setting ? PublicUrl(setting.Value) : null;

    // Only an https origin is kept: http means someone opened the app over the LAN port, not the real address
    private static Uri? PublicUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps && url.UserInfo.Length == 0
            ? new Uri(url.GetLeftPart(UriPartial.Authority))
            : null;
}
