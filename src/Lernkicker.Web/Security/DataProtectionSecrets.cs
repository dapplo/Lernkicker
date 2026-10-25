using System.Security.Cryptography;
using Lernkicker.Application.Ports;
using Microsoft.AspNetCore.DataProtection;

namespace Lernkicker.Web.Security;

/// <summary>Encrypts server secrets with the same keys as the cookies (in <c>data/keys</c>).</summary>
public sealed class DataProtectionSecrets(IDataProtectionProvider provider) : ISecretProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("Lernkicker.ServerSecrets");

    public string Protect(string value) => _protector.Protect(value);

    public string? Unprotect(string protectedValue)
    {
        try
        {
            return _protector.Unprotect(protectedValue);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
