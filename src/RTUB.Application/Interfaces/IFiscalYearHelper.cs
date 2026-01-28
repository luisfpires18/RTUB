namespace RTUB.Application.Interfaces;

/// <summary>
/// Helper service for fiscal year date range calculations
/// Extracted from Requests.razor to improve separation of concerns
/// </summary>
public interface IFiscalYearHelper
{
    /// <summary>
    /// Gets the fiscal year date range from a fiscal year string (e.g., "2024-2025")
    /// </summary>
    /// <param name="fiscalYear">Fiscal year string in format "YYYY-YYYY"</param>
    /// <returns>A tuple with start and end dates, or null if invalid</returns>
    (DateTime startDate, DateTime endDate)? GetFiscalYearDateRange(string fiscalYear);
}
