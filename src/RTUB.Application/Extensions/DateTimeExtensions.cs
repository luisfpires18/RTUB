namespace RTUB.Application.Extensions;

/// <summary>
/// Extension methods for DateTime formatting commonly used in Razor pages
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// Formats date as dd/MM/yyyy (Portuguese short date format)
    /// </summary>
    public static string ToPortugueseShortDate(this DateTime date)
    {
        return date.ToString("dd/MM/yyyy");
    }

    /// <summary>
    /// Formats date as dd/MM/yyyy HH:mm (Portuguese date and time format)
    /// </summary>
    public static string ToPortugueseDateTime(this DateTime date)
    {
        return date.ToString("dd/MM/yyyy HH:mm");
    }

    /// <summary>
    /// Formats date as dd/MM/yyyy HH:mm:ss (Portuguese full date and time format)
    /// </summary>
    public static string ToPortugueseFullDateTime(this DateTime date)
    {
        return date.ToString("dd/MM/yyyy HH:mm:ss");
    }

    /// <summary>
    /// Formats nullable date as dd/MM/yyyy or returns empty string if null
    /// </summary>
    public static string ToPortugueseShortDate(this DateTime? date)
    {
        return date?.ToString("dd/MM/yyyy") ?? string.Empty;
    }

    /// <summary>
    /// Formats nullable date as dd/MM/yyyy HH:mm or returns empty string if null
    /// </summary>
    public static string ToPortugueseDateTime(this DateTime? date)
    {
        return date?.ToString("dd/MM/yyyy HH:mm") ?? string.Empty;
    }
}
