using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;


namespace RTUB.Application.Services;

/// <summary>
/// FiscalYear service implementation
/// Contains business logic for fiscal year operations
/// Follows Single Responsibility and Dependency Inversion principles
/// </summary>
public class FiscalYearService : IFiscalYearService
{
    private readonly TimeProvider _time;

    private readonly IFiscalYearRepository _fiscalYearRepository;

    public FiscalYearService(IFiscalYearRepository fiscalYearRepository, TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _fiscalYearRepository = fiscalYearRepository;
    }

    public async Task<IEnumerable<FiscalYear>> GetAllFiscalYearsAsync()
    {
        return await _fiscalYearRepository.GetAllAsync();
    }

    public async Task<FiscalYear?> GetFiscalYearByStartYearAsync(int startYear)
    {
        return await _fiscalYearRepository.GetByStartYearAsync(startYear);
    }

    public async Task<FiscalYear> CreateFiscalYearAsync(int startYear)
    {
        // Validate that fiscal year doesn't already exist
        var existing = await GetFiscalYearByStartYearAsync(startYear);
        if (existing != null)
            throw new InvalidOperationException($"Fiscal year starting in {startYear} already exists");

        // Get current fiscal start year
        var today = PortugalTime.Today(_time);
        var currentMonth = today.Month;
        var currentYear = today.Year;
        int currentFiscalStartYear = currentMonth >= 9 ? currentYear : currentYear - 1;

        // Don't allow creating future fiscal years
        if (startYear > currentFiscalStartYear)
            throw new InvalidOperationException($"Cannot create future fiscal years. Current fiscal year is {currentFiscalStartYear}-{currentFiscalStartYear + 1}");

        // Auto-increment the end year
        var endYear = startYear + 1;

        var fiscalYear = FiscalYear.Create(startYear, endYear);
        return await _fiscalYearRepository.AddAsync(fiscalYear);
    }

}
