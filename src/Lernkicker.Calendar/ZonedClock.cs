using Lernkicker.Application.Ports;
using NodaTime;

namespace Lernkicker.Calendar;

/// <summary>
/// Today in the family's time zone. Uses NodaTime's built-in time zone database, as the chiseled container
/// image has no system time zone data.
/// </summary>
public sealed class ZonedClock(TimeProvider time, string timeZone) : ILocalClock
{
    private readonly DateTimeZone _zone = DateTimeZoneProviders.Tzdb[timeZone];

    public DateOnly Today => Instant.FromDateTimeOffset(time.GetUtcNow()).InZone(_zone).Date.ToDateOnly();

    /// <summary>Whether NodaTime knows the zone, e.g. "Europe/Berlin".</summary>
    public static bool IsKnown(string timeZone) => DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZone) is not null;
}
