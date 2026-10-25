namespace Lernkicker.Contracts;

/// <summary><c>Needed</c>: no admin exists yet, so the first visitor sets up the server.</summary>
public sealed record SetupStatus(bool Needed);

/// <summary><c>PublicUrl</c>: the address the browser used (<c>location.origin</c>).</summary>
public sealed record SetupRequest(string Email, string Password, string DisplayName, string FamilyName, string? Language, string? PublicUrl);
