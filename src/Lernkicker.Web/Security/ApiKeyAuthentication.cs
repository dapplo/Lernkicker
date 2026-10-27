using System.Security.Claims;
using System.Text.Encodings.Web;
using Lernkicker.Application.UseCases;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Lernkicker.Web.Security;

/// <summary>
/// Logs in a program with a family's API key, sent as <c>Authorization: Bearer lk_…</c> (or <c>X-Api-Key</c>).
/// It acts for the family like a parent, but only on the endpoints that ask for this scheme (the MCP endpoint).
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ApiKeyUseCases keys)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string HeaderName = "X-Api-Key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = Token(Request);
        if (token is null)
        {
            return AuthenticateResult.NoResult();
        }

        var session = await keys.AuthenticateAsync(token);
        if (session is null)
        {
            return AuthenticateResult.Fail("Unknown API key.");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, session.ParentId.ToString()),
                new Claim(Claims.FamilyId, session.FamilyId.ToString()),
                new Claim(Claims.ApiKey, session.KeyId.ToString()),
                new Claim(Claims.Access, session.ReadOnly ? Claims.ReadAccess : Claims.WriteAccess),
            ],
            Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer";
        return Task.CompletedTask;
    }

    private static string? Token(HttpRequest request)
    {
        if (request.Headers.Authorization.ToString() is { Length: > 0 } authorization
            && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authorization["Bearer ".Length..].Trim();
        }

        return request.Headers[HeaderName].ToString() is { Length: > 0 } key ? key.Trim() : null;
    }
}
