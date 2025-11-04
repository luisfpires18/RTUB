namespace RTUB.Application.Helpers;

/// <summary>
/// Helper methods for fiscal year calculations
/// Fiscal year runs from September to August (Sept-Aug cycle)
/// </summary>
public static class FiscalYearHelper
{
    /// <summary>
    /// Calculates the current fiscal year start year
    /// </summary>
    /// <returns>The start year of the current fiscal year</returns>
    public static int GetCurrentFiscalYearStartYear()
    {
        var today = DateTime.Today;
        return today.Month >= 9 ? today.Year : today.Year - 1;
    }

    /// <summary>
    /// Gets the current fiscal year string in "YYYY-YYYY" format
    /// </summary>
    /// <returns>Fiscal year string like "2024-2025"</returns>
    public static string GetCurrentFiscalYearString()
    {
        var startYear = GetCurrentFiscalYearStartYear();
        return $"{startYear}-{startYear + 1}";
    }

    /// <summary>
    /// Calculates the fiscal year start year for a given date
    /// </summary>
    /// <param name="date">The date to calculate the fiscal year for</param>
    /// <returns>The start year of the fiscal year containing the given date</returns>
    public static int GetFiscalYearStartYear(DateTime date)
    {
        return date.Month >= 9 ? date.Year : date.Year - 1;
    }
}
