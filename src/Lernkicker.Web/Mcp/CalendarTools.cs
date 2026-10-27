using System.ComponentModel;
using Lernkicker.Application;
using Lernkicker.Application.Ports;
using Lernkicker.Application.UseCases;
using Lernkicker.Contracts;
using Lernkicker.Domain.Calendar;
using Lernkicker.Web.Endpoints;
using Lernkicker.Web.Security;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;

namespace Lernkicker.Web.Mcp;

[McpServerToolType]
public sealed class CalendarTools(CalendarUseCases calendar, ILocalClock clock)
{
    [McpServerTool(Name = "get_calendar", Title = "A child's calendar", ReadOnly = true, Idempotent = true)]
    [Description("A child's dates between two days: events (tests, football, other), imported dates and the due dates of quizzes and lessons. Without dates: from a week ago for 90 days.")]
    public async Task<IReadOnlyList<CalendarEntryDto>> GetCalendar(
        [Description("The learner's id (learnerId from list_family_members).")] Guid childId,
        [Description("First day, yyyy-MM-dd.")] DateOnly? from = null,
        [Description("Last day, yyyy-MM-dd (at most 400 days after the first).")] DateOnly? to = null)
    {
        var start = from ?? clock.Today.AddDays(-7);
        var end = to ?? start.AddDays(90);
        if (end < start || end.DayNumber - start.DayNumber > CalendarEndpoints.MaxRangeDays)
        {
            throw McpResults.Failure(ErrorCodes.CalendarRangeInvalid);
        }

        return (await calendar.ListAsync(childId, start, end)).Value().Select(e => e.ToDto()).ToList();
    }

    [McpServerTool(Name = "add_calendar_event", Title = "Add a date to a child's calendar")]
    [Authorize(Policy = Policies.ApiKeyWrite)]
    [Description("Adds a date to a child's calendar, e.g. the day of a test. Times are the family's local time; without a start time it is a whole day.")]
    public async Task<CalendarEntryDto> AddCalendarEvent(
        [Description("The learner's id (learnerId from list_family_members).")] Guid childId,
        [Description("test, football or other.")] string kind,
        [Description("Short title, e.g. \"HSU-Probe: Wahlen\".")] string title,
        [Description("The day, yyyy-MM-dd.")] DateOnly date,
        [Description("Start time, HH:mm.")] TimeOnly? startTime = null,
        [Description("End time, HH:mm (needs a start time).")] TimeOnly? endTime = null,
        [Description("Where, if it matters.")] string? location = null) =>
        (await calendar.AddEventAsync(childId, new EventDetails(kind, title, date, startTime, endTime, location))).Value().ToDto();

    [McpServerTool(Name = "update_calendar_event", Title = "Change a date in a child's calendar", Idempotent = true)]
    [Authorize(Policy = Policies.ApiKeyWrite)]
    [Description("Changes a date that was entered by hand or with add_calendar_event (its eventId from get_calendar). Imported dates can't be changed.")]
    public async Task<CalendarEntryDto> UpdateCalendarEvent(
        [Description("The learner's id.")] Guid childId,
        [Description("The eventId from get_calendar.")] Guid eventId,
        [Description("test, football or other.")] string kind,
        [Description("Short title.")] string title,
        [Description("The day, yyyy-MM-dd.")] DateOnly date,
        [Description("Start time, HH:mm.")] TimeOnly? startTime = null,
        [Description("End time, HH:mm (needs a start time).")] TimeOnly? endTime = null,
        [Description("Where, if it matters.")] string? location = null) =>
        (await calendar.UpdateEventAsync(childId, eventId, new EventDetails(kind, title, date, startTime, endTime, location))).Value().ToDto();

    [McpServerTool(Name = "delete_calendar_event", Title = "Remove a date from a child's calendar", Destructive = true, Idempotent = true)]
    [Authorize(Policy = Policies.ApiKeyWrite)]
    [Description("Removes a date entered by hand or with add_calendar_event (its eventId from get_calendar). Imported dates can't be removed.")]
    public async Task<string> DeleteCalendarEvent(
        [Description("The learner's id.")] Guid childId,
        [Description("The eventId from get_calendar.")] Guid eventId)
    {
        (await calendar.DeleteEventAsync(childId, eventId)).Check();
        return "deleted";
    }
}
