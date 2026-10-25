using System.Security.Claims;
using Lernkicker.Application.UseCases;
using Lernkicker.Contracts;
using Lernkicker.Web.Security;

namespace Lernkicker.Web.Endpoints;

/// <summary>Administration: only for the admin role and only from inside the tailnet.</summary>
public static class AdminEndpoints
{
    public static void MapAdmin(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin")
            .RequireAuthorization(Policies.Admin)
            .AddEndpointFilter<TailnetOnlyFilter>();

        admin.MapPost("/invitations", async (NewInvitation request, AdminUseCases admins, HttpRequest http) =>
            (await admins.CreateInvitationAsync(request.FamilyId, request.NewFamilyName))
                .ToHttp(i => new InvitationCreatedResponse(i.Code, $"{http.Scheme}://{http.Host}/invitation#{i.Code}", i.ExpiresAt)));

        admin.MapGet("/families", async (AdminUseCases admins) =>
            Results.Json((await admins.ListFamiliesAsync()).Select(f => f.ToDto()), ApiJson.Options));

        admin.MapDelete("/families/{id:guid}", async (Guid id, ClaimsPrincipal user, AdminUseCases admins) =>
            (await admins.DeleteFamilyAsync(user.RequiredFamily(), id)).ToHttp());
    }
}
