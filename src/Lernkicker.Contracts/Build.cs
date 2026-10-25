using System.Globalization;
using System.Reflection;

namespace Lernkicker.Contracts;

/// <summary>
/// Which build is running. <c>Version</c> is the release ("1.2.0"), the branch ("main") or "dev";
/// <c>Commit</c> is the git commit it was built from; <c>BuiltAt</c> is set by the Docker build.
/// </summary>
public sealed record BuildInfoDto(string Version, string? Commit, DateTimeOffset? BuiltAt)
{
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
