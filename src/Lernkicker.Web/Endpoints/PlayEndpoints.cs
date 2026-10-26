using System.Security.Claims;
using Lernkicker.Application;
using Lernkicker.Application.UseCases;
using Lernkicker.Contracts;
using Lernkicker.Web.Security;

namespace Lernkicker.Web.Endpoints;

/// <summary>Playing with others: what parents unlock, and the lobby and games on the child's device.</summary>
public static class PlayEndpoints
{
    public static void MapPlay(this IEndpointRouteBuilder app)
    {
        var parents = app.MapGroup("/api/children/{id:guid}/play").RequireAuthorization(Policies.Parent);

        parents.MapGet("/", async (Guid id, PlaySettingsUseCases uc) =>
            (await uc.GetAsync(id)).ToHttp(s => s.ToDto()));

        parents.MapPut("/", async (Guid id, PlaySettingsDto request, PlaySettingsUseCases uc) =>
            (await uc.UpdateAsync(id, request.Games ?? [], request.Reach, request.Families ?? [], request.Children ?? [])).ToHttp(s => s.ToDto()));

        parents.MapGet("/partners", async (Guid id, ClaimsPrincipal user, PlaySettingsUseCases uc) =>
            (await uc.PartnersAsync(user.RequiredFamily(), id)).ToHttp(p => p.ToDto()));

        var play = app.MapGroup("/api/play").RequireAuthorization(Policies.Child);

        play.MapGet("/lobby", async (ClaimsPrincipal user, MatchUseCases uc) =>
            Results.Json((await uc.LobbyAsync(user.ChildIdOf())).ToDto(), ApiJson.Options));

        play.MapPost("/tables", async (NewTable request, ClaimsPrincipal user, MatchUseCases uc) =>
            (await uc.OpenTableAsync(user.ChildIdOf(), request.Game, request.Seats)).ToHttp(m => m.ToDto()));

        play.MapPost("/tables/{matchId:guid}/join", async (Guid matchId, ClaimsPrincipal user, MatchUseCases uc) =>
            (await uc.JoinAsync(user.ChildIdOf(), matchId, Random.Shared)).ToHttp(m => m.ToDto()));

        play.MapDelete("/tables/{matchId:guid}", async (Guid matchId, ClaimsPrincipal user, MatchUseCases uc) =>
            (await uc.LeaveTableAsync(user.ChildIdOf(), matchId)).ToHttp());

        play.MapGet("/matches/{matchId:guid}", async (Guid matchId, ClaimsPrincipal user, MatchUseCases uc) =>
            (await uc.MatchAsync(user.ChildIdOf(), matchId)).ToHttp(m => m.ToDto()));

        play.MapPost("/matches/{matchId:guid}/moves", async (Guid matchId, MoveDto move, ClaimsPrincipal user, MatchUseCases uc) =>
            move.ToDomain() is { } domainMove
                ? (await uc.MoveAsync(user.ChildIdOf(), matchId, domainMove)).ToHttp(m => m.ToDto())
                : ApiResults.Error(AppError.Invalid(ErrorCodes.MoveInvalid)));

        play.MapPost("/matches/{matchId:guid}/resign", async (Guid matchId, ClaimsPrincipal user, MatchUseCases uc) =>
            (await uc.ResignAsync(user.ChildIdOf(), matchId)).ToHttp(m => m.ToDto()));

        play.MapGet("/battleship/random-fleet", () =>
            Results.Json(MatchUseCases.RandomFleet(Random.Shared).Select(s => s.ToDto()), ApiJson.Options));
    }
}
