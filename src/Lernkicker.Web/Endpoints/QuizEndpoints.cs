using System.Security.Claims;
using Lernkicker.Application;
using Lernkicker.Application.UseCases;
using Lernkicker.Contracts;
using Lernkicker.Domain.Quizzes;
using Lernkicker.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Lernkicker.Web.Endpoints;

/// <summary>A family's library of quizzes.</summary>
public static class QuizEndpoints
{
    public static void MapQuizzes(this IEndpointRouteBuilder app)
    {
        var quizzes = app.MapGroup("/api/quizzes").RequireAuthorization(Policies.Parent);

        quizzes.MapGet("/", async (QuizLibraryUseCases library) =>
            Results.Json((await library.ListAsync()).Select(q => q.ToDto()), ApiJson.Options));

        // The raw quiz/v1 JSON as the body, or with its pictures: a zip, or a form with the JSON file and the pictures.
        quizzes.MapPost("/", async (HttpRequest request, ClaimsPrincipal user, QuizLibraryUseCases library) =>
        {
            var (upload, status, code) = await Uploads.ReadAsync(request, QuizLimits.MaxBytes, ErrorCodes.QuizTooLarge);
            if (upload is null)
            {
                return ApiResults.Error(status, code!);
            }

            var uploaded = await library.UploadAsync(user.RequiredFamily(), upload.Json, upload.Files);
            return uploaded.IsSuccess
                ? Results.Json(uploaded.Value.ToDto(), ApiJson.Options, statusCode: StatusCodes.Status201Created)
                : ApiResults.Error(uploaded.Error!);
        }).WithMetadata(new RequestSizeLimitAttribute(ApiLimits.UploadMaxBytes));

        quizzes.MapGet("/{quizId}", async (string quizId, QuizLibraryUseCases library) =>
        {
            var source = await library.SourceAsync(quizId);
            return source.IsSuccess ? Results.Text(source.Value, "application/json") : ApiResults.Error(source.Error!);
        });

        // The file with its pictures as a zip, ready to change and upload again
        quizzes.MapGet("/{quizId}/zip", async (string quizId, QuizLibraryUseCases library) =>
        {
            var package = await library.PackageAsync(quizId);
            return package.IsSuccess ? Uploads.Zip(package.Value) : ApiResults.Error(package.Error!);
        });

        quizzes.MapGet("/{quizId}/preview", async (string quizId, QuizLibraryUseCases library) =>
            (await library.PreviewAsync(quizId, Random.Shared)).ToHttp(s => s.ToDto()));

        quizzes.MapPut("/{quizId}/shared", async (string quizId, ShareRequest request, QuizLibraryUseCases library) =>
            (await library.SetSharedAsync(quizId, request.Shared)).ToHttp(q => q.ToDto()));

        quizzes.MapDelete("/{quizId}", async (string quizId, QuizLibraryUseCases library) =>
            (await library.DeleteAsync(quizId)).ToHttp(d => new QuizDeletedResponse(d.Archived)));
    }
}
