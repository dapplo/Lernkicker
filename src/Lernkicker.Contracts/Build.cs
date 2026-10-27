using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Lernkicker.Contracts;

/// <summary>
/// Which build is running. <c>Version</c> is the release ("1.2.0"), the branch ("main") or "dev";
/// <c>Commit</c> is the git commit it was built from; <c>BuiltAt</c> is set by the Docker build.
/// </summary>
public sealed record BuildInfoDto(string Version, string? Commit, DateTimeOffset? BuiltAt)
{
    /// <summary>The response header with the server's <see cref="Id"/>; the app compares it with its own build.</summary>
    public const string Header = "X-Lernkicker-Build";

    /// <summary>
    /// One short code for the build (version, commit and build time). Server and app are built together, so they share it.
    /// A hash, so the header that everyone gets does not tell which version runs; that stays on the admin page.
    /// </summary>
    [JsonIgnore]
    public string Id => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
        $"{Version}+{Commit}@{BuiltAt?.ToUnixTimeSeconds()}")))[..16];

    /// <summary>Reads what the build stamped into the assembly: the informational version and the BuildDate metadata.</summary>
    public static BuildInfoDto Of(Assembly assembly) => Parse(
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "BuildDate")?.Value);

    /// <summary>Splits "1.2.0+commit" into version and commit.</summary>
    public static BuildInfoDto Parse(string? informationalVersion, string? buildDate)
    {
        var parts = (string.IsNullOrWhiteSpace(informationalVersion) ? "dev" : informationalVersion).Split('+', 2);
        var commit = parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null;
        DateTimeOffset? builtAt = DateTimeOffset.TryParse(buildDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date : null;
        return new(parts[0], commit, builtAt);
    }
}
