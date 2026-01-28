namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for date formatting operations
/// Extracted from Profile.razor to improve separation of concerns
/// </summary>
public interface IDateFormatter
{
    /// <summary>
    /// Converts year and month to a DateTime (first day of the month)
    /// </summary>
    /// <param name="year">The year</param>
    /// <param name="month">The month (1-12)</param>
    /// <returns>DateTime for the first day of the month, or null if year or month is null</returns>
    DateTime? GetDateFromYearMonth(int? year, int? month);

    /// <summary>
    /// Extracts year and month from a DateTime
    /// </summary>
    /// <param name="date">The DateTime to extract from</param>
    /// <returns>Tuple containing (year, month), or (null, null) if date is null</returns>
    (int? year, int? month) GetYearMonthFromDate(DateTime? date);
}
