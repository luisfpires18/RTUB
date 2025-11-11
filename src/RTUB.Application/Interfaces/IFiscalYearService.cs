using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for FiscalYear operations
/// Abstracts business logic from presentation layer
/// </summary>
public interface IFiscalYearService
{
    Task<FiscalYear?> GetFiscalYearByIdAsync(int id);
    Task<IEnumerable<FiscalYear>> GetAllFiscalYearsAsync();
    Task<FiscalYear?> GetFiscalYearByStartYearAsync(int startYear);
    Task<FiscalYear> CreateFiscalYearAsync(int startYear);
    Task DeleteFiscalYearAsync(int id);
    Task<IEnumerable<int>> GetAvailableFiscalYearStartYearsAsync();
}
