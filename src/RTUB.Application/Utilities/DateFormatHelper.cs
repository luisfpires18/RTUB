using System.Globalization;

namespace RTUB.Application.Utilities;

/// <summary>
/// Helper class for formatting dates consistently across the application
/// </summary>
public static class DateFormatHelper
{
    private static readonly CultureInfo PortugueseCulture = new("pt-PT");

    /// <summary>
    /// Formats a date range for display in pt-PT culture
    /// </summary>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">Optional end date</param>
    /// <returns>Formatted date string</returns>
    public static string FormatEventDateRange(DateTime startDate, DateTime? endDate = null)
    {
        // Single day event
        if (!endDate.HasValue || endDate.Value.Date == startDate.Date)
        {
            return startDate.ToString("dd 'de' MMMM 'de' yyyy", PortugueseCulture);
        }

        var startDay = startDate.ToString("dd", PortugueseCulture);
        var startMonth = startDate.ToString("MMMM", PortugueseCulture);
        var startYear = startDate.ToString("yyyy", PortugueseCulture);

        var endDay = endDate.Value.ToString("dd", PortugueseCulture);
        var endMonth = endDate.Value.ToString("MMMM", PortugueseCulture);
        var endYear = endDate.Value.ToString("yyyy", PortugueseCulture);

        // Same month and year: "04–06 de dezembro de 2025"
        if (startMonth == endMonth && startYear == endYear)
        {
            return $"{startDay}–{endDay} de {endMonth} de {endYear}";
        }

        // Same year, different months: "04 de dezembro – 06 de janeiro de 2025"
        if (startYear == endYear)
        {
            return $"{startDay} de {startMonth} – {endDay} de {endMonth} de {endYear}";
        }

        // Different years: "04 de dezembro de 2025 – 02 de janeiro de 2026"
        return $"{startDay} de {startMonth} de {startYear} – {endDay} de {endMonth} de {endYear}";
    }

    /// <summary>
    /// Formats a date range for email templates (shorter format)
    /// </summary>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">Optional end date</param>
    /// <returns>Formatted date string</returns>
    public static string FormatEventDateRangeForEmail(DateTime startDate, DateTime? endDate = null)
    {
        // Single day event - include day of week and time if present
        if (!endDate.HasValue || endDate.Value.Date == startDate.Date)
        {
            // Check if the start date has a time component
            if (startDate.TimeOfDay != TimeSpan.Zero)
            {
                return startDate.ToString("dddd, dd 'de' MMMM 'de' yyyy, HH:mm", PortugueseCulture);
            }
            return startDate.ToString("dddd, dd 'de' MMMM 'de' yyyy", PortugueseCulture);
        }

        var startDay = startDate.ToString("dd", PortugueseCulture);
        var startMonth = startDate.ToString("MMMM", PortugueseCulture);
        var startYear = startDate.ToString("yyyy", PortugueseCulture);

        var endDay = endDate.Value.ToString("dd", PortugueseCulture);
        var endMonth = endDate.Value.ToString("MMMM", PortugueseCulture);
        var endYear = endDate.Value.ToString("yyyy", PortugueseCulture);

        // Same month and year: "20 - 22 de novembro de 2025"
        if (startMonth == endMonth && startYear == endYear)
        {
            return $"{startDay} - {endDay} de {endMonth} de {endYear}";
        }

        // Same year, different months: "20 de novembro - 22 de dezembro de 2025"
        if (startYear == endYear)
        {
            return $"{startDay} de {startMonth} - {endDay} de {endMonth} de {endYear}";
        }

        // Different years: "20 de novembro de 2025 - 02 de janeiro de 2026"
        return $"{startDay} de {startMonth} de {startYear} - {endDay} de {endMonth} de {endYear}";
    }
}
