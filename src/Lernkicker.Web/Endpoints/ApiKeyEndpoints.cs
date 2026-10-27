using System.Security.Claims;
using Lernkicker.Application.UseCases;
using Lernkicker.Contracts;
using Lernkicker.Web.Mcp;
using Lernkicker.Web.Security;

namespace Lernkicker.Web.Endpoints;

/// <summary>The family's API keys for AI assistants and scripts (see docs/mcp.md). Only a logged-in parent manages them.</summary>
public static class ApiKeyEndpoints
{
    public static void MapApiKeys(this IEndpointRouteBuilder app)
    {
        var keys = app.MapGroup("/api/api-keys").RequireAuthorization(Policies.Parent);

        keys.MapGet("/", async (ApiKeyUseCases uc) =>
            Results.Json((await uc.ListAsync()).Select(k => k.ToDto()), ApiJson.Options));

        keys.MapPost("/", async (NewApiKey request, ClaimsPrincipal user, ApiKeyUseCases uc, HttpRequest http) =>
        {
            var created = await uc.CreateAsync(user.RequiredFamily(), user.ParentId(), request.Name, request.ReadOnly);
            return created.IsSuccess
                ? Results.Json(
                    new ApiKeyCreatedResponse(created.Value.Key.ToDto(), created.Value.Token, $"{http.Scheme}://{http.Host}{McpSetup.Path}"),
                    ApiJson.Options,
                    statusCode: StatusCodes.Status201Created)
                : ApiResults.Error(created.Error!);
        });

        keys.MapDelete("/{id:guid}", async (Guid id, ApiKeyUseCases uc) =>
            (await uc.RevokeAsync(id)).ToHttp());
    }
}
