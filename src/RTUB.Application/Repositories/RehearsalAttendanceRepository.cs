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
        ResetChangeTracker();
        ClearUserNavigation(entity);
        ClearRehearsalNavigation(entity);
        NormalizeTrackedUsers();
        NormalizeTrackedAttendance(entity);

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
        ResetChangeTracker();
        ClearUserNavigation(entity);
        ClearRehearsalNavigation(entity);
        NormalizeTrackedUsers();
        NormalizeTrackedAttendance(entity);

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
        ResetChangeTracker();

        var entity = await _dbSet.FindAsync(id);
        if (entity != null)
        {
            ClearUserNavigation(entity);
            ClearRehearsalNavigation(entity);
            NormalizeTrackedUsers();
            NormalizeTrackedAttendance(entity);

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

    public async Task DeleteByRehearsalIdAsync(int rehearsalId)
    {
        var attendances = await _dbSet
            .Where(a => a.RehearsalId == rehearsalId)
            .ToListAsync();

        if (attendances.Count > 0)
        {
            _dbSet.RemoveRange(attendances);
            await _context.SaveChangesAsync();
        }
    }

    private void NormalizeTrackedUsers()
    {
        var userEntries = _context.ChangeTracker
            .Entries<ApplicationUser>()
            .Where(e => !string.IsNullOrEmpty(e.Entity.Id))
            .ToList();

        foreach (var grouping in userEntries.GroupBy(e => e.Entity.Id))
        {
            var primaryEntry = grouping
                .OrderBy(e => e.State == EntityState.Unchanged ? 0 : 1)
                .ThenBy(e => e.State == EntityState.Modified ? 0 : 1)
                .First();

            foreach (var entry in grouping)
            {
                if (entry == primaryEntry)
                {
                    if (entry.State == EntityState.Added)
                    {
                        entry.State = EntityState.Unchanged;
                    }
                    continue;
                }

                entry.State = EntityState.Detached;
            }
        }
    }

    private void ResetChangeTracker()
    {
        _context.ChangeTracker.Clear();
    }

    private void ClearUserNavigation(RehearsalAttendance entity)
    {
        if (entity.User != null)
        {
            var userEntry = _context.Entry(entity.User);
            if (userEntry.State != EntityState.Detached)
            {
                userEntry.State = EntityState.Detached;
            }

            entity.User = null;
        }
    }

    private void ClearRehearsalNavigation(RehearsalAttendance entity)
    {
        if (entity.Rehearsal != null)
        {
            var rehearsalEntry = _context.Entry(entity.Rehearsal);
            if (rehearsalEntry.State != EntityState.Detached)
            {
                rehearsalEntry.State = EntityState.Detached;
            }

            entity.Rehearsal = null;
        }
    }

    private void NormalizeTrackedAttendance(RehearsalAttendance entity)
    {
        if (entity.Id == 0)
        {
            return;
        }

        var attendanceEntries = _context.ChangeTracker
            .Entries<RehearsalAttendance>()
            .Where(e => e.Entity.Id == entity.Id)
            .ToList();

        foreach (var entry in attendanceEntries)
        {
            if (entry.Entity == entity)
            {
                continue;
            }

            entry.State = EntityState.Detached;
        }
    }
}
