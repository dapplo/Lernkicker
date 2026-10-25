using Lernkicker.Application;
using Lernkicker.Application.UseCases;
using Lernkicker.Contracts;
using Lernkicker.Persistence;
using Lernkicker.Web.Security;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;

namespace Lernkicker.Web.Endpoints;

/// <summary>The first-run setup: creating the admin is only possible inside the tailnet and only once.</summary>
public static class SetupEndpoints
{
    public static void MapSetup(this IEndpointRouteBuilder app)
    {
        var setup = app.MapGroup("/api/setup");

        setup.MapGet("", async (SetupUseCases setups) => Results.Json(new SetupStatus(await setups.NeededAsync()), ApiJson.Options));

        setup.MapPost("", async (SetupRequest request, SetupUseCases setups, PublicAddress address, UserManager<ParentUser> users, SignInManager<ParentUser> signIn) =>
        {
            var completed = await setups.CompleteAsync(new FirstAdmin(request.Email, request.Password, request.DisplayName, request.FamilyName, request.Language, request.PublicUrl));
            if (!completed.IsSuccess)
            {
                return ApiResults.Error(completed.Error!);
            }

            if (completed.Value is { } publicUrl)
            {
                address.UseStored(publicUrl);
            }

            await signIn.SignInAsync((await users.FindByEmailAsync(request.Email.Trim()))!, isPersistent: true);
            return Results.NoContent();
        })
        .AddEndpointFilter<TailnetOnlyFilter>()
        .RequireRateLimiting(RateLimits.Login);

        // A fresh server can start from a backup instead (raw zip body). The app restarts to apply it.
        setup.MapPost("/restore", async (HttpContext http, SetupUseCases setups, DataBackup backup, IAppRestart restart, CancellationToken cancellationToken) =>
        {
            if (!await setups.NeededAsync())
            {
                return ApiResults.Error(AppError.Invalid(ErrorCodes.SetupDone));
            }

            if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            {
                limit.MaxRequestBodySize = DataBackup.MaxBytes;
            }

            if (!await backup.StageAsync(http.Request.Body, cancellationToken))
            {
                return ApiResults.Error(AppError.Invalid(ErrorCodes.BackupInvalid));
            }

            restart.AfterResponse(http.Response);
            return Results.Accepted();
        })
        .AddEndpointFilter<TailnetOnlyFilter>()
        .RequireRateLimiting(RateLimits.Login);
    }
}
