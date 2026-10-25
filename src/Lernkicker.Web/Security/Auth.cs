using System.Security.Claims;
using Lernkicker.Application.Ports;
using Lernkicker.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Lernkicker.Web.Security;

public static class Claims
{
    public const string FamilyId = "lk:family";
    public const string ChildId = "lk:child";
    public const string DeviceId = "lk:device";

    public static Guid? Family(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(FamilyId), out var id) ? id : null;

    /// <summary>The family of a user who passed the parent or child policy (the policy requires the claim).</summary>
    public static Guid RequiredFamily(this ClaimsPrincipal user) =>
        user.Family() ?? throw new InvalidOperationException("No family claim.");

    public static Guid ParentId(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static Guid ChildIdOf(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ChildId)!);
}

public static class Policies
{
    public const string Parent = nameof(Parent);
    public const string Admin = nameof(Admin);
    public const string Child = nameof(Child);
}

public static class Schemes
{
    /// <summary>Cookie scheme for a child logged in with PIN on a paired device.</summary>
    public const string Child = "Child";
}

public static class Cookies
{
    public const string Parent = "lk_parent";
    public const string Child = "lk_child";

    /// <summary>Holds the device token of a paired phone; only sent to the device endpoints.</summary>
    public const string Device = "lk_device";
}

/// <summary>The family from the logged-in user's claims; drives the persistence adapter's query filters.</summary>
public sealed class HttpFamilyScope(IHttpContextAccessor accessor) : IFamilyScope
{
    public Guid? FamilyId => accessor.HttpContext?.User.Family();
}

/// <summary>Adds the family id to a parent's login, so query filters and endpoints know the family.</summary>
public sealed class ParentClaimsFactory(
    UserManager<ParentUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ParentUser, IdentityRole<Guid>>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ParentUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(Claims.FamilyId, user.FamilyId.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.GivenName, user.DisplayName));
        return identity;
    }
}

/// <summary>
/// Requests through Tailscale Funnel carry the Tailscale-Funnel-Request header; admin endpoints
/// refuse them so administration is only possible from inside the tailnet.
/// </summary>
public sealed class TailnetOnlyFilter : IEndpointFilter
{
    public const string FunnelHeader = "Tailscale-Funnel-Request";

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        context.HttpContext.Request.Headers.ContainsKey(FunnelHeader)
            ? ValueTask.FromResult<object?>(Results.NotFound())
            : next(context);
}

public static class RateLimits
{
    public const string Login = nameof(Login);
    public const string Child = nameof(Child);
}
