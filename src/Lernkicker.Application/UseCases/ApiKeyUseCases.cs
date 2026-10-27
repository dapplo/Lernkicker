using Lernkicker.Application.Ports;
using Lernkicker.Domain.Families;

namespace Lernkicker.Application.UseCases;

/// <summary>
/// API keys parents create for programs such as AI assistants (the MCP endpoint): create (the key is shown once),
/// list, revoke, and checking a presented key.
/// </summary>
public sealed class ApiKeyUseCases(IApiKeyRepository keys, IUnitOfWork unitOfWork, TimeProvider time)
{
    private const int HintLength = 4;

    public async Task<IReadOnlyList<ApiKeySummary>> ListAsync() =>
        (await keys.ListAsync()).OrderBy(k => k.CreatedAt).Select(ApiKeySummary.From).ToList();

    public async Task<Result<ApiKeyCreated>> CreateAsync(Guid familyId, Guid parentId, string name, bool readOnly)
    {
        if (!ApiKey.IsValidName(name))
        {
            return AppError.Invalid(ErrorCodes.ApiKeyNameInvalid);
        }

        if ((await keys.ListAsync()).Count >= ApiKey.MaxPerFamily)
        {
            return AppError.Invalid(ErrorCodes.ApiKeyTooMany);
        }

        var token = ApiKey.Prefix + Secrets.NewToken();
        var key = new ApiKey(familyId, parentId, name, Secrets.Hash(token), token[^HintLength..], readOnly, time.GetUtcNow());
        keys.Add(key);
        await unitOfWork.SaveChangesAsync();
        return new ApiKeyCreated(ApiKeySummary.From(key), token);
    }

    /// <summary>The key stops working at once.</summary>
    public async Task<Result> RevokeAsync(Guid keyId)
    {
        var key = await keys.FindAsync(keyId);
        if (key is null)
        {
            return AppError.NotFound(ErrorCodes.ApiKeyNotFound);
        }

        keys.Remove(key);
        await unitOfWork.SaveChangesAsync();
        return Result.Ok();
    }

    /// <summary>Who a presented key belongs to, or null for an unknown or revoked key. Notes the use.</summary>
    public async Task<ApiKeySession?> AuthenticateAsync(string? token)
    {
        if (string.IsNullOrEmpty(token) || !token.StartsWith(ApiKey.Prefix, StringComparison.Ordinal) || token.Length > 100)
        {
            return null;
        }

        var key = await keys.FindByHashAnyFamilyAsync(Secrets.Hash(token));
        if (key is null)
        {
            return null;
        }

        if (key.Used(time.GetUtcNow()))
        {
            await unitOfWork.SaveChangesAsync();
        }

        return new ApiKeySession(key.FamilyId, key.ParentId, key.Id, key.ReadOnly);
    }
}
