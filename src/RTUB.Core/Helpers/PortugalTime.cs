namespace RTUB.Core.Helpers;

/// <summary>
/// The business calendar is Portugal's, whatever the server's own time zone is (Azure runs on UTC).
/// Use this for "today" boundaries on stored local dates; stored dates are never reinterpreted as UTC.
/// #226 F02: also the local clock for presentation (<see cref="Now"/>) and for the hosted jobs' daily times
/// (<see cref="NextUtc"/>). Persisted technical timestamps stay UTC (DateTime.UtcNow).
/// </summary>
public static class PortugalTime
{
    // IANA id first; "GMT Standard Time" is the Windows id with the same rules (WET/WEST) if ICU is unavailable.
    private static readonly TimeZoneInfo Zone =
        TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Lisbon", out var lisbon) ? lisbon
        : TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");

    /// <summary>The current calendar date in Portugal (time 00:00, Kind Unspecified).</summary>
    public static DateTime Today(TimeProvider time) => Now(time).Date;

    /// <summary>The current wall-clock time in Portugal (Kind Unspecified), for dates and times shown to people.</summary>
    public static DateTime Now(TimeProvider time) => TimeZoneInfo.ConvertTime(time.GetUtcNow(), Zone).DateTime;

    /// <summary>
    /// The first UTC instant after now at which Portugal's clock reads <paramref name="localTime"/> (on
    /// <paramref name="day"/> only, when given). DST-safe: a time skipped by the spring change (01:00-01:59 on the last
    /// Sunday of March) runs one hour later, at the same instant the clock jumps past it; a time that happens twice in
    /// autumn runs at its first occurrence only.
    /// </summary>
    public static DateTime NextUtc(TimeProvider time, TimeSpan localTime, DayOfWeek? day = null)
    {
        var utcNow = time.GetUtcNow().UtcDateTime;
        for (var date = Now(time).Date; ; date = date.AddDays(1))
        {
            if (day is { } only && date.DayOfWeek != only)
            {
                continue;
            }

            var next = ToUtc(date + localTime);
            if (next > utcNow)
            {
                return next;
            }
        }
    }

    /// <summary>A Portugal wall-clock time as UTC (see <see cref="NextUtc"/> for the two DST edge cases).</summary>
    public static DateTime ToUtc(DateTime local)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (Zone.IsInvalidTime(local))
        {
            local = local.AddHours(1); // Portugal's spring gap is one hour
        }

        if (Zone.IsAmbiguousTime(local))
        {
            // The first occurrence is the one with the larger (summer) offset.
            return DateTime.SpecifyKind(local - Zone.GetAmbiguousTimeOffsets(local).Max(), DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, Zone);
    }

    /// <summary>A stored UTC timestamp on Portugal's clock (Kind Unspecified); task 050 buckets by Portugal day.</summary>
    public static DateTime FromUtc(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
}
