namespace RTUB.Application.Helpers;

/// <summary>
/// The business calendar is Portugal's, whatever the server's own time zone is (Azure runs on UTC).
/// Use this for "today" boundaries on stored local dates; stored dates are never reinterpreted as UTC.
/// </summary>
public static class PortugalTime
{
    // IANA id first; "GMT Standard Time" is the Windows id with the same rules (WET/WEST) if ICU is unavailable.
    private static readonly TimeZoneInfo Zone =
        TimeZoneInfo.TryFindSystemTimeZoneById("Europe/Lisbon", out var lisbon) ? lisbon
        : TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");

    /// <summary>The current calendar date in Portugal (time 00:00, Kind Unspecified).</summary>
    public static DateTime Today(TimeProvider time) => TimeZoneInfo.ConvertTime(time.GetUtcNow(), Zone).Date;
}
