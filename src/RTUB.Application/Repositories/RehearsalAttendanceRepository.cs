using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for RehearsalAttendance entity
/// </summary>
public class RehearsalAttendanceRepository : Repository<RehearsalAttendance>, IRehearsalAttendanceRepository
{
    public RehearsalAttendanceRepository(ApplicationDbContext context) : base(context)
    {
    }

    public override async Task<RehearsalAttendance> AddAsync(RehearsalAttendance entity)
    {
        // Ensure the User is loaded into Local cache for audit log display name resolution
        if (!string.IsNullOrEmpty(entity.UserId))
        {
            var user = await _context.Users.FindAsync(entity.UserId);
            if (user != null)
            {
                // User is now in Local cache and can be accessed by GetEntityDisplayName
            }
        }

        // Ensure the Rehearsal is loaded into Local cache for audit log display name resolution
        if (entity.RehearsalId > 0)
        {
            var rehearsal = await _context.Rehearsals.FindAsync(entity.RehearsalId);
            if (rehearsal != null)
            {
                // Rehearsal is now in Local cache
            }
        }

        return await base.AddAsync(entity);
    }

    public override async Task UpdateAsync(RehearsalAttendance entity)
    {
        // Ensure the User is loaded into Local cache for audit log display name resolution
        if (!string.IsNullOrEmpty(entity.UserId))
        {
            var user = await _context.Users.FindAsync(entity.UserId);
        }

        // Ensure the Rehearsal is loaded into Local cache for audit log display name resolution
        if (entity.RehearsalId > 0)
        {
            var rehearsal = await _context.Rehearsals.FindAsync(entity.RehearsalId);
        }

        await base.UpdateAsync(entity);
    }

    public override async Task DeleteAsync(int id)
    {
        var entity = await _dbSet.FindAsync(id);
        if (entity != null)
        {
            // Ensure the User is loaded into Local cache for audit log display name resolution
            if (!string.IsNullOrEmpty(entity.UserId))
            {
                var user = await _context.Users.FindAsync(entity.UserId);
            }

            // Ensure the Rehearsal is loaded into Local cache for audit log display name resolution
            if (entity.RehearsalId > 0)
            {
                var rehearsal = await _context.Rehearsals.FindAsync(entity.RehearsalId);
            }
        }

        await base.DeleteAsync(id);
    }

    public async Task<IEnumerable<RehearsalAttendance>> GetAttendancesByRehearsalIdAsync(int rehearsalId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(a => a.Rehearsal)
            .Where(a => a.RehearsalId == rehearsalId)
            .OrderBy(a => a.CheckedInAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<RehearsalAttendance>> GetAttendancesByUserIdAsync(string userId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(a => a.Rehearsal)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Rehearsal!.Date)
            .ToListAsync();
    }

    public async Task<RehearsalAttendance?> GetAttendanceByRehearsalAndUserAsync(int rehearsalId, string userId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(a => a.RehearsalId == rehearsalId && a.UserId == userId);
    }

    public async Task<(int TotalRehearsals, int Attended)> GetAttendanceStatsAsync(string userId)
    {
        var attendances = await _dbSet
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .ToListAsync();

        var totalRehearsals = attendances.Count;
        var attended = attendances.Count(a => a.WillAttend);

        return (totalRehearsals, attended);
    }
}
