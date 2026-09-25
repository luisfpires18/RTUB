namespace RTUB.Core.Helpers.AfterHours;

/// <summary>
/// Calendar days as experienced in Lisbon (the RTUB academic calendar). Uses the IANA id, which
/// .NET resolves on Linux and, through ICU, on Windows. Daylight saving is handled by the zone.
/// </summary>
public static class LisbonCalendar
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon");

    public static DateOnly DateOf(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone));

    /// <summary>A Lisbon wall-clock time (kind ignored) as a UTC instant.</summary>
    public static DateTime ToUtc(DateTime lisbonLocal) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(lisbonLocal, DateTimeKind.Unspecified), Zone);

    /// <summary>A UTC instant as Lisbon wall-clock time.</summary>
    public static DateTime ToLisbon(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    /// <summary>The UTC instant at which the Lisbon day after <paramref name="date"/> begins.</summary>
    public static DateTime NextMidnightUtc(DateOnly date) =>
        TimeZoneInfo.ConvertTimeToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue), Zone);
}
