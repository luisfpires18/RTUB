using System.Globalization;
using RTUB.Application.Extensions;
using RTUB.Core.Entities;

namespace RTUB.Application.DTOs;

/// <summary>
/// An upcoming event on the React home preview (React track 010). Only what the Blazor /events
/// already shows every visitor on its cards: name, date, time, location, type and whether it was
/// cancelled. No description, cancellation reason, image, enrollments or ids. Dates are the stored
/// local (Portugal) values as plain text, so no time zone can shift them in the browser.
/// </summary>
public sealed record UpcomingEventDto(
    string Name,
    string Date,
    string? Time,
    string? EndDate,
    string Location,
    string Type,
    bool Cancelled)
{
    public static UpcomingEventDto From(Event e) => new(
        e.Name,
        e.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        e.Date.TimeOfDay == TimeSpan.Zero ? null : e.Date.ToString("HH:mm", CultureInfo.InvariantCulture),
        e.EndDate is { } end && end.Date > e.Date.Date ? end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null,
        e.Location,
        e.Type.GetDisplayName(),
        e.IsCancelled);
}
