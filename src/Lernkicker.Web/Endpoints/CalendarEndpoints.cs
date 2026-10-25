using System.Security.Claims;
using Lernkicker.Application;
using Lernkicker.Application.Ports;
using Lernkicker.Application.UseCases;
using Lernkicker.Contracts;
using Lernkicker.Web.Security;

namespace Lernkicker.Web.Endpoints;

/// <summary>A child's dates for parents, the next dates for the child, and the secret ICS feed.</summary>
public static class CalendarEndpoints
{
    /// <summary>Largest range one request may ask for.</summary>
    public const int MaxRangeDays = 400;

    public static void MapCalendar(this IEndpointRouteBuilder app)
    {
        var child = app.MapGroup("/api/children/{id:guid}").RequireAuthorization(Policies.Parent);

        child.MapGet("/calendar", async (Guid id, DateOnly? from, DateOnly? to, CalendarUseCases uc, ILocalClock clock) =>
        {
            var start = from ?? clock.Today.AddDays(-7);
            var end = to ?? start.AddDays(90);
            if (end < start || end.DayNumber - start.DayNumber > MaxRangeDays)
            {
                return ApiResults.Error(AppError.Invalid(ErrorCodes.CalendarRangeInvalid));
            }

            return (await uc.ListAsync(id, start, end)).ToHttp(entries => entries.Select(e => e.ToDto()));
        });

        child.MapPost("/calendar", async (Guid id, EventRequest request, CalendarUseCases uc) =>
        {
            var added = await uc.AddEventAsync(id, request.ToDomain());
            return added.IsSuccess
                ? Results.Json(added.Value.ToDto(), ApiJson.Options, statusCode: StatusCodes.Status201Created)
                : ApiResults.Error(added.Error!);
        });

        child.MapPut("/calendar/{eventId:guid}", async (Guid id, Guid eventId, EventRequest request, CalendarUseCases uc) =>
            (await uc.UpdateEventAsync(id, eventId, request.ToDomain())).ToHttp(e => e.ToDto()));

        child.MapDelete("/calendar/{eventId:guid}", async (Guid id, Guid eventId, CalendarUseCases uc) =>
            (await uc.DeleteEventAsync(id, eventId)).ToHttp());

        child.MapGet("/subscriptions", async (Guid id, CalendarUseCases uc) =>
            (await uc.ListSubscriptionsAsync(id)).ToHttp(list => list.Select(s => s.ToDto())));

        // Adding and updating fetch a URL from the internet, so they are rate limited
        child.MapPost("/subscriptions", async (Guid id, NewSubscription request, CalendarUseCases uc, CancellationToken cancellationToken) =>
        {
            var added = await uc.SubscribeAsync(id, request.Label, request.Url, request.Kind, cancellationToken);
            return added.IsSuccess
                ? Results.Json(added.Value.ToDto(), ApiJson.Options, statusCode: StatusCodes.Status201Created)
                : ApiResults.Error(added.Error!);
        }).RequireRateLimiting(RateLimits.Calendar);

        child.MapPost("/subscriptions/{subscriptionId:guid}/sync", async (Guid id, Guid subscriptionId, CalendarUseCases uc, CancellationToken cancellationToken) =>
            (await uc.SyncAsync(id, subscriptionId, cancellationToken)).ToHttp(s => s.ToDto()))
            .RequireRateLimiting(RateLimits.Calendar);

        child.MapDelete("/subscriptions/{subscriptionId:guid}", async (Guid id, Guid subscriptionId, CalendarUseCases uc) =>
            (await uc.UnsubscribeAsync(id, subscriptionId)).ToHttp());

        child.MapPost("/feed", async (Guid id, CalendarUseCases uc, HttpRequest http) =>
            (await uc.CreateFeedAsync(id)).ToHttp(f => new FeedResponse($"{http.Scheme}://{http.Host}/calendar/{f.Token}.ics")));

        child.MapDelete("/feed", async (Guid id, CalendarUseCases uc) =>
            (await uc.RemoveFeedAsync(id)).ToHttp());

        app.MapGet($"{DeviceEndpoints.Path}/calendar", async (ClaimsPrincipal user, CalendarUseCases uc) =>
            Results.Json((await uc.UpcomingAsync(user.ChildIdOf())).Select(e => e.ToDto()), ApiJson.Options))
            .RequireAuthorization(Policies.Child);

        // Calendar apps fetch this without a login; the token in the path is the only key
        app.MapGet("/calendar/{token}.ics", async (string token, CalendarUseCases uc, HttpResponse response) =>
        {
            var feed = await uc.FeedAsync(token);
            if (!feed.IsSuccess)
            {
                return Results.NotFound();
            }

            response.Headers.CacheControl = "private, max-age=900";
            return Results.Text(feed.Value, "text/calendar; charset=utf-8");
        }).RequireRateLimiting(RateLimits.Calendar);
    }
}
