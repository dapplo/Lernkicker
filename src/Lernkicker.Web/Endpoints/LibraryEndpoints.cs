using System.Security.Claims;
using Lernkicker.Application;
using Lernkicker.Application.UseCases;
using Lernkicker.Contracts;
using Lernkicker.Web.Security;

namespace Lernkicker.Web.Endpoints;

/// <summary>The shared library: what other families made public, to preview and copy.</summary>
public static class LibraryEndpoints
{
    public static void MapLibrary(this IEndpointRouteBuilder app)
    {
        var library = app.MapGroup("/api/library").RequireAuthorization(Policies.Parent);

        library.MapGet("/", async (ClaimsPrincipal user, SharedLibraryUseCases uc) =>
            Results.Json((await uc.ListAsync(user.RequiredFamily())).Select(s => s.ToDto()), ApiJson.Options));

        library.MapGet("/quizzes/{id:guid}", async (Guid id, SharedLibraryUseCases uc) =>
            (await uc.PreviewQuizAsync(id, Random.Shared)).ToHttp(s => s.ToDto()));

        library.MapGet("/lessons/{id:guid}", async (Guid id, SharedLibraryUseCases uc) =>
            (await uc.PreviewLessonAsync(id)).ToHttp(l => l.ToDto()));

        library.MapPost("/quizzes/{id:guid}/copy", async (Guid id, ClaimsPrincipal user, SharedLibraryUseCases uc) =>
            Created(await uc.CopyQuizAsync(user.RequiredFamily(), id), q => q.ToDto()));

        library.MapPost("/lessons/{id:guid}/copy", async (Guid id, ClaimsPrincipal user, SharedLibraryUseCases uc) =>
            Created(await uc.CopyLessonAsync(user.RequiredFamily(), id), l => l.ToDto()));
    }

    private static IResult Created<T>(Result<T> result, Func<T, object> map) =>
        result.IsSuccess
            ? Results.Json(map(result.Value), ApiJson.Options, statusCode: StatusCodes.Status201Created)
            : ApiResults.Error(result.Error!);
}
