using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for FiscalYear entity
/// Provides data access operations for fiscal years
/// </summary>
public interface IFiscalYearRepository : IRepository<FiscalYear>
{
    /// <summary>
    /// Gets fiscal year by start year
    /// </summary>
    Task<FiscalYear?> GetByStartYearAsync(int startYear);

    /// <summary>
    /// Gets all fiscal years ordered by start year descending
    /// </summary>
    Task<IEnumerable<FiscalYear>> GetAllOrderedAsync();
}
