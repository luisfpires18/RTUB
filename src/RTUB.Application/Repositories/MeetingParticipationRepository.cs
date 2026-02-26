using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for MeetingParticipation entity.
/// Uses IDbContextFactory — each operation gets a fresh short-lived context.
/// </summary>
public class MeetingParticipationRepository : Repository<MeetingParticipation>, IMeetingParticipationRepository
{
    public MeetingParticipationRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public override async Task<MeetingParticipation> AddAsync(MeetingParticipation entity)
    {
        using var context = CreateContext();

        // Preload User and Meeting into context Local cache for audit log display name resolution
        if (!string.IsNullOrEmpty(entity.UserId))
            await context.Users.FindAsync(entity.UserId);
        if (entity.MeetingId > 0)
            await context.Meetings.FindAsync(entity.MeetingId);

        // Clear navigation properties to avoid attaching stale related entities
        entity.User = null!;
        entity.Meeting = null!;

        await context.MeetingParticipations.AddAsync(entity);
        await context.SaveChangesAsync();
        return entity;
    }

    public override async Task UpdateAsync(MeetingParticipation entity)
    {
        using var context = CreateContext();

        // Preload User and Meeting into context Local cache for audit log display name resolution
        if (!string.IsNullOrEmpty(entity.UserId))
            await context.Users.FindAsync(entity.UserId);
        if (entity.MeetingId > 0)
            await context.Meetings.FindAsync(entity.MeetingId);

        var tracked = await context.MeetingParticipations.FindAsync(entity.Id)
            ?? throw new InvalidOperationException(
                $"MeetingParticipation with Id {entity.Id} not found in the database.");
        context.Entry(tracked).CurrentValues.SetValues(entity);
        await context.SaveChangesAsync();
    }

    public override async Task DeleteAsync(int id)
    {
        using var context = CreateContext();
        var entity = await context.MeetingParticipations.FindAsync(id);
        if (entity == null) return;

        // Preload User and Meeting into context Local cache for audit log display name resolution
        if (!string.IsNullOrEmpty(entity.UserId))
            await context.Users.FindAsync(entity.UserId);
        if (entity.MeetingId > 0)
            await context.Meetings.FindAsync(entity.MeetingId);

        context.MeetingParticipations.Remove(entity);
        await context.SaveChangesAsync();
    }

    public override async Task<IEnumerable<MeetingParticipation>> GetAllAsync()
    {
        using var context = CreateContext();
        return await context.MeetingParticipations
            .AsNoTracking()
            .Include(mp => mp.Meeting)
            .ToListAsync();
    }

    public async Task<IEnumerable<MeetingParticipation>> GetByMeetingIdAsync(int meetingId)
    {
        using var context = CreateContext();
        return await context.MeetingParticipations
            .AsNoTracking()
            .Where(mp => mp.MeetingId == meetingId)
            .Include(mp => mp.User)
            .OrderByDescending(mp => mp.ParticipatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<MeetingParticipation>> GetByUserIdAsync(string userId)
    {
        using var context = CreateContext();
        return await context.MeetingParticipations
            .AsNoTracking()
            .Where(mp => mp.UserId == userId)
            .Include(mp => mp.Meeting)
            .OrderByDescending(mp => mp.ParticipatedAt)
            .ToListAsync();
    }

    public async Task<MeetingParticipation?> GetByMeetingAndUserAsync(int meetingId, string userId)
    {
        using var context = CreateContext();
        return await context.MeetingParticipations
            .AsNoTracking()
            .FirstOrDefaultAsync(mp => mp.MeetingId == meetingId && mp.UserId == userId);
    }

    public async Task<IEnumerable<MeetingParticipation>> GetByAttendanceAsync(bool willAttend)
    {
        using var context = CreateContext();
        return await context.MeetingParticipations
            .AsNoTracking()
            .Where(mp => mp.WillAttend == willAttend)
            .Include(mp => mp.User)
            .Include(mp => mp.Meeting)
            .ToListAsync();
    }

    public async Task DeleteByMeetingIdAsync(int meetingId)
    {
        using var context = CreateContext();
        await context.MeetingParticipations
            .Where(mp => mp.MeetingId == meetingId)
            .ExecuteDeleteAsync();
    }
}
