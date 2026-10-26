using System.Security.Claims;
using Lernkicker.Application;
using Lernkicker.Application.UseCases;
using Lernkicker.Contracts;
using Lernkicker.Domain.Lessons;
using Lernkicker.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Lernkicker.Web.Endpoints;

/// <summary>Learning material: the family's lessons, giving them to a child, and the child reading and practising them.</summary>
public static class LessonEndpoints
{
    public static void MapLessons(this IEndpointRouteBuilder app)
    {
        var lessons = app.MapGroup("/api/lessons").RequireAuthorization(Policies.Parent);

        lessons.MapGet("/", async (LessonUseCases uc) =>
            Results.Json((await uc.ListAsync()).Select(l => l.ToDto()), ApiJson.Options));

        // The raw lesson/v1 JSON as the body, or with its pictures: a zip, or a form with the JSON file and the pictures.
        lessons.MapPost("/", async (HttpRequest request, ClaimsPrincipal user, LessonUseCases uc) =>
        {
            var (upload, status, code) = await Uploads.ReadAsync(request, LessonLimits.MaxBytes, ErrorCodes.LessonTooLarge);
            if (upload is null)
            {
                return ApiResults.Error(status, code!);
            }

            var uploaded = await uc.UploadAsync(user.RequiredFamily(), upload.Json, upload.Files);
            return uploaded.IsSuccess
                ? Results.Json(uploaded.Value.ToDto(), ApiJson.Options, statusCode: StatusCodes.Status201Created)
                : ApiResults.Error(uploaded.Error!);
        }).WithMetadata(new RequestSizeLimitAttribute(ApiLimits.UploadMaxBytes));

        lessons.MapGet("/{lessonId}", async (string lessonId, LessonUseCases uc) =>
        {
            var source = await uc.SourceAsync(lessonId);
            return source.IsSuccess ? Results.Text(source.Value, "application/json") : ApiResults.Error(source.Error!);
        });

        // The file with its pictures as a zip, ready to change and upload again
        lessons.MapGet("/{lessonId}/zip", async (string lessonId, LessonUseCases uc) =>
        {
            var package = await uc.PackageAsync(lessonId);
            return package.IsSuccess ? Uploads.Zip(package.Value) : ApiResults.Error(package.Error!);
        });

        lessons.MapGet("/{lessonId}/preview", async (string lessonId, LessonUseCases uc) =>
            (await uc.PreviewAsync(lessonId)).ToHttp(l => l.ToDto()));

        lessons.MapPut("/{lessonId}/shared", async (string lessonId, ShareRequest request, LessonUseCases uc) =>
            (await uc.SetSharedAsync(lessonId, request.Shared)).ToHttp(l => l.ToDto()));

        lessons.MapDelete("/{lessonId}", async (string lessonId, LessonUseCases uc) =>
            (await uc.DeleteAsync(lessonId)).ToHttp());

        var children = app.MapGroup("/api/children/{id:guid}/lessons").RequireAuthorization(Policies.Parent);

        children.MapGet("/", async (Guid id, LessonUseCases uc) =>
            (await uc.ListAssignmentsAsync(id)).ToHttp(list => list.Select(a => a.ToDto())));

        children.MapPost("/", async (Guid id, NewLessonAssignment request, LessonUseCases uc) =>
            (await uc.AssignAsync(id, request.LessonId, request.DueOn)).ToHttp(a => a.ToDto()));

        children.MapDelete("/{assignmentId:guid}", async (Guid id, Guid assignmentId, LessonUseCases uc) =>
            (await uc.UnassignAsync(id, assignmentId)).ToHttp());

        var device = app.MapGroup(DeviceEndpoints.Path + "/lessons").RequireAuthorization(Policies.Child);

        device.MapGet("/", async (ClaimsPrincipal user, LessonUseCases uc) =>
            Results.Json((await uc.MyLessonsAsync(user.ChildIdOf())).Select(l => l.ToDto()), ApiJson.Options));

        device.MapGet("/{assignmentId:guid}", async (Guid assignmentId, ClaimsPrincipal user, LessonUseCases uc) =>
            (await uc.LessonAsync(user.ChildIdOf(), assignmentId)).ToHttp(l => l.ToDto()));

        device.MapGet("/{assignmentId:guid}/games/{kind}", async (Guid assignmentId, string kind, ClaimsPrincipal user, LessonUseCases uc) =>
            (await uc.GameAsync(user.ChildIdOf(), assignmentId, kind, Random.Shared)).ToHttp(g => g.ToDto()));

        device.MapPost("/{assignmentId:guid}/practice", async (Guid assignmentId, LessonPractice request, ClaimsPrincipal user, LessonUseCases uc) =>
            (await uc.PractisedAsync(user.ChildIdOf(), assignmentId, request.KnownBlocks ?? [])).ToHttp(l => l.ToDto()));
    }
}
