using System.Net;
using Ical.Net.DataTypes;
using Lernkicker.Application;
using Lernkicker.Application.Ports;
using IcalCalendar = Ical.Net.Calendar;
using IcalEvent = Ical.Net.CalendarComponents.CalendarEvent;

namespace Lernkicker.Calendar;

/// <summary>Fetches ICS calendars over HTTP(S), only from public addresses, and reads their events.</summary>
public sealed class IcsCalendarSource : ICalendarSource, IDisposable
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    // Guard against endless repeating events
    private const int MaxOccurrences = 5000;

    private readonly HttpClient _http;
    private readonly string _timeZone;

    public IcsCalendarSource(string timeZone)
    {
        _timeZone = timeZone;
        _http = new HttpClient(new SocketsHttpHandler
        {
            ConnectCallback = AddressGuard.ConnectAsync,
            // A proxy would hide the real target from the guard
            UseProxy = false,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 3,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        })
        {
            Timeout = Timeout,
            MaxResponseContentBufferSize = MaxBytes,
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Lernkicker/1.0 (calendar import)");
    }

    public async Task<Result<IReadOnlyList<ImportedEvent>>> FetchAsync(Uri url, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        string text;
        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return AppError.Invalid(ErrorCodes.CalendarUnreachable);
            }

            if (response.Content.Headers.ContentLength > MaxBytes)
            {
                return AppError.Invalid(ErrorCodes.CalendarTooLarge);
            }

            text = await ReadLimitedAsync(response.Content, cancellationToken);
        }
        catch (HttpRequestException e) when (e.InnerException is AddressNotAllowedException || e.GetBaseException() is AddressNotAllowedException)
        {
            return AppError.Invalid(ErrorCodes.CalendarAddressNotAllowed);
        }
        catch (TooLargeException)
        {
            return AppError.Invalid(ErrorCodes.CalendarTooLarge);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
        {
            return AppError.Invalid(ErrorCodes.CalendarUnreachable);
        }

        return Read(text, from, to, _timeZone) is { } events
            ? Result<IReadOnlyList<ImportedEvent>>.Ok(events)
            : AppError.Invalid(ErrorCodes.NotACalendar);
    }

    /// <summary>The events of an ICS text between two dates, in the given time zone; null if it is no calendar.</summary>
    public static IReadOnlyList<ImportedEvent>? Read(string ics, DateOnly from, DateOnly to, string timeZone)
    {
        if (!ics.TrimStart('﻿', ' ', '\r', '\n', '\t').StartsWith("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        IcalCalendar? calendar;
        try
        {
            calendar = IcalCalendar.Load(ics);
        }
        catch (Exception)
        {
            return null;
        }

        if (calendar is null)
        {
            return null;
        }

        var events = new List<ImportedEvent>();
        try
        {
            foreach (var occurrence in calendar.GetOccurrences(new CalDateTime(from)).Take(MaxOccurrences))
            {
                if (occurrence.Source is not IcalEvent source || string.Equals(source.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var (date, start) = Local(occurrence.Period.StartTime, timeZone);
                if (date > to)
                {
                    break;
                }

                if (date < from)
                {
                    continue;
                }

                TimeOnly? end = null;
                if (start is not null && occurrence.Period.EffectiveEndTime is { } effectiveEnd)
                {
                    var (endDate, endTime) = Local(effectiveEnd, timeZone);
                    // Events over several days keep only their start
                    end = endDate == date ? endTime : null;
                }

                var title = source.Summary?.Trim();
                if (string.IsNullOrEmpty(title))
                {
                    continue;
                }

                var uid = string.IsNullOrWhiteSpace(source.Uid) ? title : source.Uid;
                events.Add(new ImportedEvent($"{uid}/{date:yyyyMMdd}{start:HHmm}", title, date, start, end, Clean(source.Location)));
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Broken repeat rules and the like: whatever was read so far is kept
        }

        return events;
    }

    public void Dispose() => _http.Dispose();

    private static (DateOnly Date, TimeOnly? Time) Local(CalDateTime value, string timeZone)
    {
        if (!value.HasTime)
        {
            return (value.Date, null);
        }

        // Floating times (no zone) are already local
        var local = value.IsFloating ? value : value.ToTimeZone(timeZone);
        return (local.Date, local.Time);
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static async Task<string> ReadLimitedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxBytes)
            {
                throw new TooLargeException();
            }

            buffer.Write(chunk, 0, read);
        }

        return System.Text.Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private sealed class TooLargeException : Exception;
}
