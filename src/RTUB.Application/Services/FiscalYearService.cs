using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;


namespace RTUB.Application.Services;

/// <summary>
/// FiscalYear service implementation
/// Contains business logic for fiscal year operations
/// Follows Single Responsibility and Dependency Inversion principles
/// </summary>
public class FiscalYearService : IFiscalYearService
{
    private readonly ApplicationDbContext _context;

    public FiscalYearService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<FiscalYear?> GetFiscalYearByIdAsync(int id)
    {
        return await _context.FiscalYears.FindAsync(id);
    }

    public async Task<IEnumerable<FiscalYear>> GetAllFiscalYearsAsync()
    {
        return await _context.FiscalYears.ToListAsync();
    }

    public async Task<FiscalYear?> GetFiscalYearByStartYearAsync(int startYear)
    {
        var allFiscalYears = await _context.FiscalYears.ToListAsync();
        return allFiscalYears.FirstOrDefault(fy => fy.StartYear == startYear);
    }

    public async Task<FiscalYear> CreateFiscalYearAsync(int startYear)
    {
        // Validate that fiscal year doesn't already exist
        var existing = await GetFiscalYearByStartYearAsync(startYear);
        if (existing != null)
            throw new InvalidOperationException($"Fiscal year starting in {startYear} already exists");

        // Get current fiscal start year
        var today = DateTime.Today;
        var currentMonth = today.Month;
        var currentYear = today.Year;
        int currentFiscalStartYear = currentMonth >= 9 ? currentYear : currentYear - 1;

        // Don't allow creating future fiscal years
        if (startYear > currentFiscalStartYear)
            throw new InvalidOperationException($"Cannot create future fiscal years. Current fiscal year is {currentFiscalStartYear}-{currentFiscalStartYear + 1}");

        // Auto-increment the end year
        var endYear = startYear + 1;

        var fiscalYear = FiscalYear.Create(startYear, endYear);
        _context.FiscalYears.Add(fiscalYear);
        await _context.SaveChangesAsync();
        return fiscalYear;
    }

    public async Task DeleteFiscalYearAsync(int id)
    {
        var fiscalYear = await _context.FiscalYears.FindAsync(id);
        if (fiscalYear == null)
            throw new InvalidOperationException($"Fiscal year with ID {id} not found");

        _context.FiscalYears.Remove(fiscalYear);
        await _context.SaveChangesAsync();
    }
}
