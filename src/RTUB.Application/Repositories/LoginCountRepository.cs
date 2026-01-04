using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for LoginCount entity
/// </summary>
public class LoginCountRepository : Repository<LoginCount>, ILoginCountRepository
{
    public LoginCountRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<LoginCount?> GetByUserAndDateAsync(string userId, DateTime date)
    {
        var dateOnly = date.Date;
        return await _dbSet
            .FirstOrDefaultAsync(lc => lc.UserId == userId && lc.LoginDate == dateOnly);
    }

    public async Task<List<LoginCount>> GetByUserIdAsync(string userId)
    {
        return await _dbSet
            .Where(lc => lc.UserId == userId)
            .OrderByDescending(lc => lc.LoginDate)
            .ToListAsync();
    }

    public async Task<List<LoginCount>> GetByUserIdAndDateRangeAsync(string userId, DateTime startDate, DateTime endDate)
    {
        var startDateOnly = startDate.Date;
        var endDateOnly = endDate.Date;
        
        return await _dbSet
            .Where(lc => lc.UserId == userId 
                && lc.LoginDate >= startDateOnly 
                && lc.LoginDate <= endDateOnly)
            .OrderByDescending(lc => lc.LoginDate)
            .ToListAsync();
    }

    public async Task<int> GetTotalLoginCountByUserIdAsync(string userId)
    {
        return await _dbSet
            .Where(lc => lc.UserId == userId)
            .SumAsync(lc => lc.Count);
    }
}
