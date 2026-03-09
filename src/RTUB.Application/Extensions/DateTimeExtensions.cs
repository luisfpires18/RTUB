using System.Globalization;

namespace RTUB.Application.Extensions;

/// <summary>
/// Extension methods for DateTime formatting commonly used in Razor pages
/// </summary>
public static class DateTimeExtensions
{
    private static readonly CultureInfo PortugueseCulture = CultureInfo.GetCultureInfo("pt-PT");

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

    /// <summary>
    /// Gets the Portuguese weekday name with an initial capital letter
    /// </summary>
    public static string ToPortugueseWeekdayName(this DateTime date)
    {
        var dayName = PortugueseCulture.DateTimeFormat.GetDayName(date.DayOfWeek);
        return string.IsNullOrEmpty(dayName)
            ? string.Empty
            : $"{char.ToUpper(dayName[0], PortugueseCulture)}{dayName[1..]}";
    }
}
