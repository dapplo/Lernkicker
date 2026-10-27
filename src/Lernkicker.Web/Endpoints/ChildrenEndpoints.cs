using System.Security.Claims;
using Lernkicker.Application.UseCases;
using Lernkicker.Contracts;
using Lernkicker.Web.Security;

namespace Lernkicker.Web.Endpoints;

/// <summary>What a parent does for their children: profiles, PIN, paired devices, assignments and results.</summary>
public static class ChildrenEndpoints
{
    public static void MapChildren(this IEndpointRouteBuilder app)
    {
        var children = app.MapGroup("/api/children").RequireAuthorization(Policies.Parent);

        children.MapGet("/", async (ChildrenUseCases uc, ClaimsPrincipal user) =>
            Results.Json((await uc.ListAsync()).Select(c => c.ToDto(user.ParentId())), ApiJson.Options));

        // A parent learns with their own adult profile right here: the parent login stands in for device and PIN
        children.MapPost("/learn", async (ChildrenUseCases uc, ClaimsPrincipal user, HttpContext http) =>
        {
            var session = await uc.LearnMyselfAsync(user.RequiredFamily(), user.ParentId(), user.FindFirstValue(ClaimTypes.GivenName) ?? "");
            await DeviceEndpoints.SignInLearnerAsync(http, session, user.ParentId());
            return Results.NoContent();
        });

        children.MapPost("/{id:guid}/learn", async (Guid id, ChildrenUseCases uc, ClaimsPrincipal user, HttpContext http) =>
        {
            var session = await uc.LearnAsAsync(id, user.ParentId());
            if (!session.IsSuccess)
            {
                return ApiResults.Error(session.Error!);
            }

            await DeviceEndpoints.SignInLearnerAsync(http, session.Value, user.ParentId());
            return Results.NoContent();
        });

        children.MapPost("/", async (NewChild request, ClaimsPrincipal user, ChildrenUseCases uc) =>
        {
            var created = await uc.CreateAsync(user.RequiredFamily(), request.Name, request.GradeLevel, request.Pin ?? "", request.IsAdult, request.Theme);
            return created.IsSuccess
                ? Results.Json(created.Value.ToDto(user.ParentId()), ApiJson.Options, statusCode: StatusCodes.Status201Created)
                : ApiResults.Error(created.Error!);
        });

        children.MapPut("/{id:guid}", async (Guid id, ChildDetails request, ChildrenUseCases uc) =>
            (await uc.UpdateAsync(id, request.Name, request.GradeLevel, request.IsAdult, request.Theme)).ToHttp());

        children.MapPut("/{id:guid}/pin", async (Guid id, ChangePin request, ChildrenUseCases uc) =>
            (await uc.SetPinAsync(id, request.Pin)).ToHttp());

        children.MapDelete("/{id:guid}", async (Guid id, ChildrenUseCases uc) =>
            (await uc.DeleteAsync(id)).ToHttp());

        children.MapPost("/{id:guid}/pairing", async (Guid id, ChildrenUseCases uc, HttpRequest http) =>
            (await uc.CreatePairingCodeAsync(id))
                .ToHttp(p => new PairingCodeResponse(p.Code, $"{http.Scheme}://{http.Host}/pair#{p.Code}", p.ExpiresAt)));

        children.MapGet("/{id:guid}/devices", async (Guid id, ChildrenUseCases uc) =>
            Results.Json((await uc.ListDevicesAsync(id)).Select(d => d.ToDto()), ApiJson.Options));

        children.MapDelete("/{id:guid}/devices/{deviceId:guid}", async (Guid id, Guid deviceId, ChildrenUseCases uc) =>
            (await uc.RemoveDeviceAsync(id, deviceId)).ToHttp());

        children.MapPost("/{id:guid}/devices/{deviceId:guid}/unlock", async (Guid id, Guid deviceId, ChildrenUseCases uc) =>
            (await uc.UnlockDeviceAsync(id, deviceId)).ToHttp());

        children.MapGet("/{id:guid}/assignments", async (Guid id, ChildrenUseCases uc) =>
            Results.Json((await uc.ListAssignmentsAsync(id)).Select(a => a.ToDto()), ApiJson.Options));

        children.MapPost("/{id:guid}/assignments", async (Guid id, NewAssignment request, ChildrenUseCases uc) =>
            (await uc.AssignAsync(id, request.QuizId, request.DueOn)).ToHttp(a => a.ToDto()));

        children.MapDelete("/{id:guid}/assignments/{assignmentId:guid}", async (Guid id, Guid assignmentId, ChildrenUseCases uc) =>
            (await uc.UnassignAsync(id, assignmentId)).ToHttp());

        children.MapGet("/{id:guid}/attempts", async (Guid id, ChildrenUseCases uc) =>
            Results.Json((await uc.ListAttemptsAsync(id)).Select(a => a.ToDto()), ApiJson.Options));

        children.MapGet("/{id:guid}/attempts/{attemptId:guid}", async (Guid id, Guid attemptId, ChildrenUseCases uc) =>
            (await uc.AttemptResultAsync(id, attemptId)).ToHttp(r => r.ToDto()));
    }
}
