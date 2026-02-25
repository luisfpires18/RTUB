using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for MemberDebt entity
/// </summary>
public class MemberDebtRepository : Repository<MemberDebt>, IMemberDebtRepository
{
    public MemberDebtRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<MemberDebt>> GetByFiscalYearIdAsync(int fiscalYearId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(md => md.User)
            .Include(md => md.FiscalYear)
            .Where(md => md.FiscalYearId == fiscalYearId)
            .OrderBy(md => md.User.FirstName)
            .ThenBy(md => md.User.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<MemberDebt>> GetByUserIdAsync(string userId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(md => md.User)
            .Include(md => md.FiscalYear)
            .Where(md => md.UserId == userId)
            .OrderByDescending(md => md.FiscalYear.StartYear)
            .ToListAsync();
    }

    public async Task<MemberDebt?> GetByUserIdAndFiscalYearIdAsync(string userId, int fiscalYearId)
    {
        return await _dbSet
            .Include(md => md.User)
            .Include(md => md.FiscalYear)
            .FirstOrDefaultAsync(md => md.UserId == userId && md.FiscalYearId == fiscalYearId);
    }
}
