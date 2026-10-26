using System.Security.Claims;
using Lernkicker.Application;
using Lernkicker.Application.UseCases;
using Lernkicker.Contracts;
using Lernkicker.Domain.Practice;
using Lernkicker.Web.Security;
using Microsoft.AspNetCore.Authentication;

namespace Lernkicker.Web.Endpoints;

/// <summary>Everything the child's phone talks to: pairing, PIN login, assigned quizzes, handing in answers.</summary>
public static class DeviceEndpoints
{
    public const string Path = "/api/device";

    public static void MapDevice(this IEndpointRouteBuilder app)
    {
        var device = app.MapGroup(Path);

        device.MapPost("/pair", async (PairDevice request, ChildDeviceUseCases uc, HttpContext http) =>
        {
            var paired = await uc.PairAsync(request.Code, request.Label);
            if (!paired.IsSuccess)
            {
                return ApiResults.Error(paired.Error!);
            }

            http.Response.Cookies.Append(Cookies.Device, paired.Value.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = Path,
                MaxAge = TimeSpan.FromDays(400),
                IsEssential = true,
            });
            return Results.Json(new DeviceStatusResponse(true, paired.Value.ChildName, false, paired.Value.Theme), ApiJson.Options);
        }).RequireRateLimiting(RateLimits.Child);

        device.MapGet("/status", async (ChildDeviceUseCases uc, HttpContext http) =>
        {
            var session = await http.AuthenticateAsync(Schemes.Child);
            var status = session.Principal?.FindFirst(Claims.Learner) is not null
                ? await uc.LearnerStatusAsync(session.Principal.ChildIdOf())
                : await uc.StatusAsync(DeviceToken(http));
            if (!status.Paired && DeviceToken(http) is not null)
            {
                // The parent removed this device: drop the stale cookie so it starts over cleanly
                DeleteDeviceCookie(http);
            }

            return Results.Json(status.ToDto(), ApiJson.Options);
        });

        device.MapPost("/login", async (PinLogin request, ChildDeviceUseCases uc, HttpContext http) =>
        {
            var session = await uc.LoginAsync(DeviceToken(http), request.Pin);
            if (!session.IsSuccess)
            {
                return ApiResults.Error(session.Error!);
            }

            var s = session.Value;
            var identity = new ClaimsIdentity(
                [
                    new Claim(Claims.FamilyId, s.FamilyId.ToString()),
                    new Claim(Claims.ChildId, s.ChildId.ToString()),
                    new Claim(Claims.DeviceId, s.DeviceId.ToString()),
                    new Claim(ClaimTypes.GivenName, s.ChildName),
                ],
                Schemes.Child);
            await http.SignInAsync(Schemes.Child, new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });
            return Results.NoContent();
        }).RequireRateLimiting(RateLimits.Child);

        device.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(Schemes.Child);
            return Results.NoContent();
        });

        // "Not me": the device was paired for the wrong child, so it forgets the pairing and shows the pairing form again
        device.MapPost("/unpair", async (ChildDeviceUseCases uc, HttpContext http) =>
        {
            await uc.UnpairAsync(DeviceToken(http));
            DeleteDeviceCookie(http);
            await http.SignOutAsync(Schemes.Child);
            return Results.NoContent();
        }).RequireRateLimiting(RateLimits.Child);

        var child = device.MapGroup("/").RequireAuthorization(Policies.Child);

        child.MapGet("/assignments", async (ClaimsPrincipal user, PracticeUseCases uc) =>
            Results.Json((await uc.AssignmentsAsync(user.ChildIdOf())).Select(a => a.ToDto()), ApiJson.Options));

        child.MapGet("/assignments/{assignmentId:guid}", async (Guid assignmentId, ClaimsPrincipal user, PracticeUseCases uc) =>
            (await uc.SheetAsync(user.ChildIdOf(), assignmentId, Random.Shared)).ToHttp(s => s.ToDto()));

        child.MapPost("/assignments/{assignmentId:guid}/submit", async (Guid assignmentId, Submission submission, ClaimsPrincipal user, PracticeUseCases uc) =>
            (await uc.SubmitAsync(user.ChildIdOf(), assignmentId, submission.Answers.ToDomain(), Channels.Pwa)).ToHttp(r => r.ToDto()));
    }

    private static string? DeviceToken(HttpContext http) => http.Request.Cookies[Cookies.Device];

    private static void DeleteDeviceCookie(HttpContext http) =>
        http.Response.Cookies.Delete(Cookies.Device, new CookieOptions { Secure = true, SameSite = SameSiteMode.Strict, Path = Path });

    /// <summary>A child session for a parent's own profile, tied to the parent login instead of a device (see Program).</summary>
    public static Task SignInLearnerAsync(HttpContext http, LearnerSession session, Guid parentId)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(Claims.FamilyId, session.FamilyId.ToString()),
                new Claim(Claims.ChildId, session.ChildId.ToString()),
                new Claim(Claims.Learner, parentId.ToString()),
                new Claim(ClaimTypes.GivenName, session.ChildName),
            ],
            Schemes.Child);
        return http.SignInAsync(Schemes.Child, new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });
    }
}
