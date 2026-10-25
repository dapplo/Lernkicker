using System.Security.Claims;
using Lernkicker.Application;
using Lernkicker.Application.UseCases;
using Lernkicker.Contracts;
using Lernkicker.Persistence;
using Lernkicker.Web.Security;
using Microsoft.AspNetCore.Identity;

namespace Lernkicker.Web.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccount(this IEndpointRouteBuilder app)
    {
        var account = app.MapGroup("/api/account");

        account.MapPost("/login", async (Login login, SignInManager<ParentUser> signIn) =>
        {
            var result = await signIn.PasswordSignInAsync(login.Email, login.Password, isPersistent: true, lockoutOnFailure: true);
            if (result.IsLockedOut)
            {
                return ApiResults.Error(StatusCodes.Status423Locked, ErrorCodes.LoginLockedOut);
            }

            return result.Succeeded ? Results.NoContent() : ApiResults.Error(StatusCodes.Status401Unauthorized, ErrorCodes.LoginFailed);
        }).RequireRateLimiting(RateLimits.Login);

        account.MapPost("/logout", async (SignInManager<ParentUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return Results.NoContent();
        });

        account.MapGet("/me", async (ClaimsPrincipal user, AccountUseCases accounts) =>
            (await accounts.ProfileAsync(user.ParentId())).ToHttp(p => p.ToDto()))
            .RequireAuthorization(Policies.Parent);

        account.MapPut("/language", async (SetLanguage request, ClaimsPrincipal user, AccountUseCases accounts) =>
            (await accounts.SetLanguageAsync(user.ParentId(), request.Language)).ToHttp())
            .RequireAuthorization(Policies.Parent);

        account.MapPost("/invitation", async (RedeemInvitationRequest request, AccountUseCases accounts, UserManager<ParentUser> users, SignInManager<ParentUser> signIn) =>
        {
            var redeemed = await accounts.RedeemInvitationAsync(new RedeemInvitation(
                request.Code, request.Email, request.Password, request.DisplayName, request.PrivacyAccepted, request.Language));
            if (!redeemed.IsSuccess)
            {
                return ApiResults.Error(redeemed.Error!);
            }

            await signIn.SignInAsync((await users.FindByIdAsync(redeemed.Value.ToString()))!, isPersistent: true);
            return Results.NoContent();
        }).RequireRateLimiting(RateLimits.Login);
    }
}
