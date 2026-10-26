using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.RateLimiting;
using Lernkicker.Contracts;
using Lernkicker.Web.Security;
using ModelContextProtocol.Protocol;

namespace Lernkicker.Web.Mcp;

/// <summary>
/// The MCP endpoint (Model Context Protocol, Streamable HTTP): AI assistants manage a family's quizzes, lessons,
/// assignments and calendar with one of the family's API keys. A driving adapter like the HTTP API; the tools
/// call the same use cases. See docs/mcp.md.
/// </summary>
public static class McpSetup
{
    public const string Path = "/mcp";

    private const string Instructions =
        """
        Lernkicker is a learning app for children. You act for one family with its API key.
        Quizzes (quiz/v1) and lessons (lesson/v1) are JSON files: call get_authoring_guide once for the rules and
        both JSON schemas, check a file with validate_quiz or validate_lesson, then upload it. Uploading a file with
        an existing id replaces it (a quiz gets a new version; open assignments move along).
        Children are identified by id (list_children). Assign a quiz or lesson to a child with a due date
        (yyyy-MM-dd); the due date shows in the child's todo list and calendar. Errors come as stable codes
        (e.g. quiz.invalid) with a list of issues (code, path, arguments).
        Quiz and lesson content is written in the language of the quiz (German for German schools).
        """;

    /// <summary>The API's JSON conventions (camelCase, no nulls) for tool arguments and results.</summary>
    private static readonly JsonSerializerOptions ToolJson = new(ApiJson.Options) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

    public static IServiceCollection AddLernkickerMcp(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMcpServer(o =>
            {
                o.ServerInfo = new Implementation { Name = "lernkicker", Version = BuildInfoDto.Of(typeof(McpSetup).Assembly).Version };
                o.ServerInstructions = Instructions;
            })
            // Every request stands alone: no sessions to keep, and the family comes from the key on each request
            .WithHttpTransport(o => o.Stateless = true)
            .AddAuthorizationFilters()
            .WithTools<FamilyTools>(ToolJson)
            .WithTools<QuizTools>(ToolJson)
            .WithTools<LessonTools>(ToolJson)
            .WithTools<AssignmentTools>(ToolJson)
            .WithTools<CalendarTools>(ToolJson);

        var requestsPerMinute = configuration.GetValue("Lernkicker:Limits:ApiKeyRequestsPerMinute", 120);
        services.Configure<Microsoft.AspNetCore.RateLimiting.RateLimiterOptions>(o =>
            o.AddPolicy(RateLimits.ApiKey, http => RateLimitPartition.GetFixedWindowLimiter(
                http.User.FindFirst(Claims.ApiKey)?.Value ?? http.Connection.RemoteIpAddress?.ToString() ?? "?",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = requestsPerMinute, Window = TimeSpan.FromMinutes(1) })));
        return services;
    }

    public static void MapLernkickerMcp(this IEndpointRouteBuilder app) =>
        app.MapMcp(Path)
            .RequireAuthorization(Policies.ApiKey)
            .RequireRateLimiting(RateLimits.ApiKey);
}
