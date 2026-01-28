using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Service for date formatting operations
/// Extracted from Profile.razor to improve separation of concerns
/// </summary>
public class DateFormatter : IDateFormatter
{
    /// <summary>
    /// Converts year and month to a DateTime (first day of the month)
    /// </summary>
    /// <param name="year">The year</param>
    /// <param name="month">The month (1-12)</param>
    /// <returns>DateTime for the first day of the month, or null if year or month is null</returns>
    public DateTime? GetDateFromYearMonth(int? year, int? month)
    {
        if (!year.HasValue || !month.HasValue) return null;
        return new DateTime(year.Value, month.Value, 1);
    }

    /// <summary>
    /// Extracts year and month from a DateTime
    /// </summary>
    /// <param name="date">The DateTime to extract from</param>
    /// <returns>Tuple containing (year, month), or (null, null) if date is null</returns>
    public (int? year, int? month) GetYearMonthFromDate(DateTime? date)
    {
        if (date.HasValue)
        {
            return (date.Value.Year, date.Value.Month);
        }
        else
        {
            return (null, null);
        }
    }
}
