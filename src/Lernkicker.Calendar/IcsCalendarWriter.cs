using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Lernkicker.Application;
using Lernkicker.Application.Ports;
using IcalCalendar = Ical.Net.Calendar;
using IcalEvent = Ical.Net.CalendarComponents.CalendarEvent;

namespace Lernkicker.Calendar;

/// <summary>Writes a child's dates as ICS, in the family's time zone. The kind goes into CATEGORIES.</summary>
public sealed class IcsCalendarWriter(string timeZone) : ICalendarWriter
{
    public string Write(string calendarName, IEnumerable<CalendarEntry> entries)
    {
        var calendar = new IcalCalendar();
        calendar.AddProperty("X-WR-CALNAME", calendarName);
        calendar.AddProperty("X-WR-TIMEZONE", timeZone);
        calendar.AddTimeZone(timeZone);

        foreach (var entry in entries)
        {
            var e = new IcalEvent
            {
                Uid = $"{entry.Uid}@lernkicker",
                Summary = entry.Title,
                Location = entry.Location,
                Categories = [entry.Kind],
            };

            if (entry.StartTime is { } start)
            {
                e.DtStart = new CalDateTime(entry.Date.ToDateTime(start), timeZone);
                if (entry.EndTime is { } end)
                {
                    e.DtEnd = new CalDateTime(entry.Date.ToDateTime(end), timeZone);
                }
            }
            else
            {
                e.DtStart = new CalDateTime(entry.Date);
            }

            calendar.Events.Add(e);
        }

        return new CalendarSerializer().SerializeToString(calendar) ?? "";
    }
}
