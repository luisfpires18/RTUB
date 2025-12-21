using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using System.Linq;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Enrollment entity
/// </summary>
public class EnrollmentRepository : Repository<Enrollment>, IEnrollmentRepository
{
    public EnrollmentRepository(ApplicationDbContext context) : base(context)
    {
    }

    public override async Task<Enrollment> AddAsync(Enrollment entity)
    {
        ResetChangeTracker();
        ClearUserNavigation(entity);
        ClearEventNavigation(entity);
        NormalizeTrackedUsers();
        NormalizeTrackedEnrollment(entity);

        // Ensure the User is loaded into Local cache for audit log display name resolution
        if (!string.IsNullOrEmpty(entity.UserId))
        {
            var user = await _context.Users.FindAsync(entity.UserId);
            if (user != null)
            {
                // User is now in Local cache and can be accessed by GetEntityDisplayName
            }
        }

        // Ensure the Event is loaded into Local cache for audit log display name resolution
        if (entity.EventId > 0)
        {
            var evt = await _context.Events.FindAsync(entity.EventId);
            if (evt != null)
            {
                // Event is now in Local cache
            }
        }

        return await base.AddAsync(entity);
    }

    public override async Task UpdateAsync(Enrollment entity)
    {
        ResetChangeTracker();
        ClearUserNavigation(entity);
        ClearEventNavigation(entity);
        NormalizeTrackedUsers();
        NormalizeTrackedEnrollment(entity);

        // Ensure the User is loaded into Local cache for audit log display name resolution
        if (!string.IsNullOrEmpty(entity.UserId))
        {
            var user = await _context.Users.FindAsync(entity.UserId);
        }

        // Ensure the Event is loaded into Local cache for audit log display name resolution
        if (entity.EventId > 0)
        {
            var evt = await _context.Events.FindAsync(entity.EventId);
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
            ClearEventNavigation(entity);
            NormalizeTrackedUsers();
            NormalizeTrackedEnrollment(entity);

            // Ensure the User is loaded into Local cache for audit log display name resolution
            if (!string.IsNullOrEmpty(entity.UserId))
            {
                var user = await _context.Users.FindAsync(entity.UserId);
            }

            // Ensure the Event is loaded into Local cache for audit log display name resolution
            if (entity.EventId > 0)
            {
                var evt = await _context.Events.FindAsync(entity.EventId);
            }
        }

        await base.DeleteAsync(id);
    }

    public override async Task<IEnumerable<Enrollment>> GetAllAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .Include(e => e.Event)
            .ToListAsync();
    }

    public async Task<IEnumerable<Enrollment>> GetByEventIdAsync(int eventId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(e => e.EventId == eventId)
            .Include(e => e.User)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Enrollment>> GetByUserIdAsync(string userId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .Include(e => e.Event)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();
    }

    public async Task<Enrollment?> GetByEventAndUserAsync(int eventId, string userId)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == eventId && e.UserId == userId);
    }

    public async Task<IEnumerable<Enrollment>> GetByAttendanceAsync(bool willAttend)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(e => e.WillAttend == willAttend)
            .Include(e => e.User)
            .Include(e => e.Event)
            .ToListAsync();
    }

    public async Task DeleteByEventIdAsync(int eventId)
    {
        var enrollments = await _dbSet
            .Where(e => e.EventId == eventId)
            .ToListAsync();

        if (enrollments.Count > 0)
        {
            _dbSet.RemoveRange(enrollments);
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

    private void ClearUserNavigation(Enrollment entity)
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

    private void ClearEventNavigation(Enrollment entity)
    {
        if (entity.Event != null)
        {
            var eventEntry = _context.Entry(entity.Event);
            if (eventEntry.State != EntityState.Detached)
            {
                eventEntry.State = EntityState.Detached;
            }

            entity.Event = null;
        }
    }

    private void NormalizeTrackedEnrollment(Enrollment entity)
    {
        if (entity.Id == 0)
        {
            return;
        }

        var enrollmentEntries = _context.ChangeTracker
            .Entries<Enrollment>()
            .Where(e => e.Entity.Id == entity.Id)
            .ToList();

        foreach (var entry in enrollmentEntries)
        {
            if (entry.Entity == entity)
            {
                continue;
            }

            entry.State = EntityState.Detached;
        }
    }
}
