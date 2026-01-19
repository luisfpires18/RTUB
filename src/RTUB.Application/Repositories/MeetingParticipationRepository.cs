using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for MeetingParticipation entity
/// </summary>
public class MeetingParticipationRepository : Repository<MeetingParticipation>, IMeetingParticipationRepository
{
    public MeetingParticipationRepository(ApplicationDbContext context) : base(context)
    {
    }

    public override async Task<MeetingParticipation> AddAsync(MeetingParticipation entity)
    {
        ResetChangeTracker();
        ClearUserNavigation(entity);
        ClearMeetingNavigation(entity);
        NormalizeTrackedUsers();
        NormalizeTrackedMeetingParticipation(entity);

        // Ensure the User is loaded into Local cache for audit log display name resolution
        if (!string.IsNullOrEmpty(entity.UserId))
        {
            var user = await _context.Users.FindAsync(entity.UserId);
            if (user != null)
            {
                // User is now in Local cache and can be accessed by GetEntityDisplayName
            }
        }

        // Ensure the Meeting is loaded into Local cache for audit log display name resolution
        if (entity.MeetingId > 0)
        {
            var meeting = await _context.Meetings.FindAsync(entity.MeetingId);
            if (meeting != null)
            {
                // Meeting is now in Local cache
            }
        }

        return await base.AddAsync(entity);
    }

    public override async Task UpdateAsync(MeetingParticipation entity)
    {
        ResetChangeTracker();
        ClearUserNavigation(entity);
        ClearMeetingNavigation(entity);
        NormalizeTrackedUsers();
        NormalizeTrackedMeetingParticipation(entity);

        // Ensure the User is loaded into Local cache for audit log display name resolution
        if (!string.IsNullOrEmpty(entity.UserId))
        {
            var user = await _context.Users.FindAsync(entity.UserId);
        }

        // Ensure the Meeting is loaded into Local cache for audit log display name resolution
        if (entity.MeetingId > 0)
        {
            var meeting = await _context.Meetings.FindAsync(entity.MeetingId);
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
            ClearMeetingNavigation(entity);
            NormalizeTrackedUsers();
            NormalizeTrackedMeetingParticipation(entity);

            // Ensure the User is loaded into Local cache for audit log display name resolution
            if (!string.IsNullOrEmpty(entity.UserId))
            {
                var user = await _context.Users.FindAsync(entity.UserId);
            }

            // Ensure the Meeting is loaded into Local cache for audit log display name resolution
            if (entity.MeetingId > 0)
            {
                var meeting = await _context.Meetings.FindAsync(entity.MeetingId);
            }
        }

        await base.DeleteAsync(id);
    }

    public override async Task<IEnumerable<MeetingParticipation>> GetAllAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .Include(mp => mp.Meeting)
            .ToListAsync();
    }

    public async Task<IEnumerable<MeetingParticipation>> GetByMeetingIdAsync(int meetingId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(mp => mp.MeetingId == meetingId)
            .Include(mp => mp.User)
            .OrderByDescending(mp => mp.ParticipatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<MeetingParticipation>> GetByUserIdAsync(string userId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(mp => mp.UserId == userId)
            .Include(mp => mp.Meeting)
            .OrderByDescending(mp => mp.ParticipatedAt)
            .ToListAsync();
    }

    public async Task<MeetingParticipation?> GetByMeetingAndUserAsync(int meetingId, string userId)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(mp => mp.MeetingId == meetingId && mp.UserId == userId);
    }

    public async Task<IEnumerable<MeetingParticipation>> GetByAttendanceAsync(bool willAttend)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(mp => mp.WillAttend == willAttend)
            .Include(mp => mp.User)
            .Include(mp => mp.Meeting)
            .ToListAsync();
    }

    public async Task DeleteByMeetingIdAsync(int meetingId)
    {
        var participations = await _dbSet
            .Where(mp => mp.MeetingId == meetingId)
            .ToListAsync();

        if (participations.Count > 0)
        {
            _dbSet.RemoveRange(participations);
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

    private void ClearUserNavigation(MeetingParticipation entity)
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

    private void ClearMeetingNavigation(MeetingParticipation entity)
    {
        if (entity.Meeting != null)
        {
            var meetingEntry = _context.Entry(entity.Meeting);
            if (meetingEntry.State != EntityState.Detached)
            {
                meetingEntry.State = EntityState.Detached;
            }

            entity.Meeting = null;
        }
    }

    private void NormalizeTrackedMeetingParticipation(MeetingParticipation entity)
    {
        if (entity.Id == 0)
        {
            return;
        }

        var participationEntries = _context.ChangeTracker
            .Entries<MeetingParticipation>()
            .Where(e => e.Entity.Id == entity.Id)
            .ToList();

        foreach (var entry in participationEntries)
        {
            if (entry.Entity == entity)
            {
                continue;
            }

            entry.State = EntityState.Detached;
        }
    }
}
