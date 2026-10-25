using System.Security.Cryptography;
using System.Text;

namespace Lernkicker.Application;

/// <summary>Random codes and their hashes. Codes are only stored as hash, so a leaked database does not leak logins.</summary>
public static class Secrets
{
    private const string ShortCodeAlphabet = "ABCDEFGHJKMNPQRSTVWXYZ23456789";

    /// <summary>256-bit random token, URL-safe.</summary>
    public static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Short code a child can type if the QR code does not scan (8 characters, no 0/O/1/I).</summary>
    public static string NewShortCode() =>
        string.Create(8, 0, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = ShortCodeAlphabet[RandomNumberGenerator.GetInt32(ShortCodeAlphabet.Length)];
            }
        });

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Hash of a short code, ignoring case, spaces and dashes as typed by a child.</summary>
    public static string ShortCodeHash(string code) =>
        Hash(code.Replace(" ", "").Replace("-", "").ToUpperInvariant());
}
