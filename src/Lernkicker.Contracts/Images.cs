namespace Lernkicker.Contracts;

/// <summary>
/// A picture in a quiz or lesson. <c>Url</c> is relative to the app (e.g. <c>api/media/…</c>); it is null when the
/// picture is missing, and the client shows <c>Alt</c> instead.
/// </summary>
public sealed record ImageDto(string? Url, string Alt, string? Caption, string? Credit);
