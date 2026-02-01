using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Helper service for fiscal year date range calculations
/// Extracted from Requests.razor to improve separation of concerns
/// </summary>
public class FiscalYearHelperService : IFiscalYearHelper
{
    /// <summary>
    /// Gets the fiscal year date range from a fiscal year string (e.g., "2024-2025")
    /// </summary>
    /// <param name="fiscalYear">Fiscal year string in format "YYYY-YYYY"</param>
    /// <returns>A tuple with start and end dates, or null if invalid</returns>
    public (DateTime startDate, DateTime endDate)? GetFiscalYearDateRange(string fiscalYear)
    {
        if (string.IsNullOrEmpty(fiscalYear)) return null;

        var parts = fiscalYear.Split('-');
        if (parts.Length == 2 && int.TryParse(parts[0], out int startYear))
        {
            var startDate = new DateTime(startYear, 9, 1); // September 1st
            var endDate = new DateTime(startYear + 1, 8, 31, 23, 59, 59); // August 31st next year (end of day)
            return (startDate, endDate);
        }

        return null;
    }
}
