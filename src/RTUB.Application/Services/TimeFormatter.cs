using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Service for formatting time values
/// Extracted from Games.razor and TomatoThrower.razor to improve separation of concerns
/// </summary>
public class TimeFormatter : ITimeFormatter
{
    /// <summary>
    /// Formats a TimeSpan as a human-readable string (e.g., "1m 30s" or "45s")
    /// </summary>
    /// <param name="time">The TimeSpan to format</param>
    /// <returns>Formatted time string</returns>
    public string FormatTimeSurvived(TimeSpan time)
    {
        if (time.TotalMinutes >= 1)
            return $"{(int)time.TotalMinutes}m {time.Seconds}s";
        return $"{time.Seconds}s";
    }

    /// <summary>
    /// Formats seconds as a time string (e.g., "1:30" or "0:45")
    /// </summary>
    /// <param name="seconds">The number of seconds</param>
    /// <returns>Formatted time string</returns>
    public string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalMinutes >= 1) return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
        return $"0:{(int)seconds:D2}";
    }
}
