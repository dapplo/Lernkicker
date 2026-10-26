using Lernkicker.Domain.Families;

namespace Lernkicker.Application.Ports;

/// <summary>The family's API keys (filtered by family), and the lookup of a presented key.</summary>
public interface IApiKeyRepository
{
    void Add(ApiKey key);

    void Remove(ApiKey key);

    Task<IReadOnlyList<ApiKey>> ListAsync();

    Task<ApiKey?> FindAsync(Guid id);

    /// <summary>The key behind a presented token; the caller is not logged in yet, so across families.</summary>
    Task<ApiKey?> FindByHashAnyFamilyAsync(string tokenHash);
}
