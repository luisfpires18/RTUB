namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for formatting time values
/// Extracted from Games.razor and TomatoThrower.razor to improve separation of concerns
/// </summary>
public interface ITimeFormatter
{
    /// <summary>
    /// Formats a TimeSpan as a human-readable string (e.g., "1m 30s" or "45s")
    /// </summary>
    /// <param name="time">The TimeSpan to format</param>
    /// <returns>Formatted time string</returns>
    string FormatTimeSurvived(TimeSpan time);

    /// <summary>
    /// Formats seconds as a time string (e.g., "1:30" or "0:45")
    /// </summary>
    /// <param name="seconds">The number of seconds</param>
    /// <returns>Formatted time string</returns>
    string FormatTime(double seconds);
}
