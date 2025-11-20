using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Rehearsal entity
/// </summary>
public class RehearsalRepository : Repository<Rehearsal>, IRehearsalRepository
{
    public RehearsalRepository(ApplicationDbContext context) : base(context)
    {
    }

    public override async Task<Rehearsal?> GetByIdAsync(int id)
    {
        return await _dbSet
            .Include(r => r.Attendances)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<Rehearsal?> GetRehearsalByDateAsync(DateTime date)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(r => r.Attendances)
            .FirstOrDefaultAsync(r => r.Date.Date == date.Date);
    }

    public async Task<IEnumerable<Rehearsal>> GetRehearsalsAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(r => r.Attendances)
            .Where(r => r.Date >= startDate.Date && r.Date <= endDate.Date)
            .OrderBy(r => r.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Rehearsal>> GetUpcomingRehearsalsAsync(int count = 10)
    {
        var today = DateTime.Today;
        return await _dbSet
            .AsNoTracking()
            .Include(r => r.Attendances)
            .Where(r => r.Date >= today)
            .OrderBy(r => r.Date)
            .Take(count)
            .ToListAsync();
    }
}
