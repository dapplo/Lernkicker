using System.Security.Claims;
using System.Text;
using Lernkicker.Application;
using Lernkicker.Application.UseCases;
using Lernkicker.Contracts;
using Lernkicker.Domain.Quizzes;
using Lernkicker.Web.Security;

namespace Lernkicker.Web.Endpoints;

/// <summary>A family's library of quizzes.</summary>
public static class QuizEndpoints
{
    public static void MapQuizzes(this IEndpointRouteBuilder app)
    {
        var quizzes = app.MapGroup("/api/quizzes").RequireAuthorization(Policies.Parent);

        quizzes.MapGet("/", async (QuizLibraryUseCases library) =>
            Results.Json((await library.ListAsync()).Select(q => q.ToDto()), ApiJson.Options));

        // The body is the raw quiz/v1 JSON, exactly as in a file.
        quizzes.MapPost("/", async (HttpRequest request, ClaimsPrincipal user, QuizLibraryUseCases library) =>
        {
            var json = await ReadBodyAsync(request);
            if (json is null)
            {
                return ApiResults.Error(StatusCodes.Status413PayloadTooLarge, ErrorCodes.QuizTooLarge);
            }

            var uploaded = await library.UploadAsync(user.RequiredFamily(), json);
            return uploaded.IsSuccess
                ? Results.Json(uploaded.Value.ToDto(), ApiJson.Options, statusCode: StatusCodes.Status201Created)
                : ApiResults.Error(uploaded.Error!);
        });

        quizzes.MapGet("/{quizId}", async (string quizId, QuizLibraryUseCases library) =>
        {
            var source = await library.SourceAsync(quizId);
            return source.IsSuccess ? Results.Text(source.Value, "application/json") : ApiResults.Error(source.Error!);
        });

        quizzes.MapDelete("/{quizId}", async (string quizId, QuizLibraryUseCases library) =>
            (await library.DeleteAsync(quizId)).ToHttp(d => new QuizDeletedResponse(d.Archived)));
    }

    /// <summary>Reads the body as UTF-8, or null when it exceeds the size limit.</summary>
    private static async Task<string?> ReadBodyAsync(HttpRequest request)
    {
        if (request.ContentLength > QuizLimits.MaxBytes)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        var block = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(block)) > 0)
        {
            if (buffer.Length + read > QuizLimits.MaxBytes)
            {
                return null;
            }

            buffer.Write(block, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
