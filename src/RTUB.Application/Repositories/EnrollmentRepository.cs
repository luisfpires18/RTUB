using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Enrollment entity.
/// Uses IDbContextFactory — each operation gets a fresh short-lived context.
/// </summary>
public class EnrollmentRepository : Repository<Enrollment>, IEnrollmentRepository
{
    public EnrollmentRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public override async Task<Enrollment> AddAsync(Enrollment entity)
    {
        using var context = CreateContext();

        // Preload User and Event into context Local cache for audit log display name resolution
        if (!string.IsNullOrEmpty(entity.UserId))
            await context.Users.FindAsync(entity.UserId);
        if (entity.EventId > 0)
            await context.Events.FindAsync(entity.EventId);

        // Clear navigation properties to avoid attaching stale related entities
        entity.User = null!;
        entity.Event = null!;

        await context.Enrollments.AddAsync(entity);
        await context.SaveChangesAsync();
        return entity;
    }

    public override async Task UpdateAsync(Enrollment entity)
    {
        using var context = CreateContext();

        // Preload User and Event into context Local cache for audit log display name resolution
        if (!string.IsNullOrEmpty(entity.UserId))
            await context.Users.FindAsync(entity.UserId);
        if (entity.EventId > 0)
            await context.Events.FindAsync(entity.EventId);

        // Clear navigation properties to avoid attaching stale related entities
        entity.User = null!;
        entity.Event = null!;

        context.Enrollments.Update(entity);
        await context.SaveChangesAsync();
    }

    public override async Task DeleteAsync(int id)
    {
        using var context = CreateContext();
        var entity = await context.Enrollments.FindAsync(id);
        if (entity == null) return;

        // Preload User and Event into context Local cache for audit log display name resolution
        if (!string.IsNullOrEmpty(entity.UserId))
            await context.Users.FindAsync(entity.UserId);
        if (entity.EventId > 0)
            await context.Events.FindAsync(entity.EventId);

        context.Enrollments.Remove(entity);
        await context.SaveChangesAsync();
    }

    public override async Task<IEnumerable<Enrollment>> GetAllAsync()
    {
        using var context = CreateContext();
        return await context.Enrollments
            .AsNoTracking()
            .Include(e => e.Event)
            .ToListAsync();
    }

    public async Task<IEnumerable<Enrollment>> GetByEventIdAsync(int eventId)
    {
        using var context = CreateContext();
        return await context.Enrollments
            .AsNoTracking()
            .Where(e => e.EventId == eventId)
            .Include(e => e.User)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Enrollment>> GetByUserIdAsync(string userId)
    {
        using var context = CreateContext();
        return await context.Enrollments
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .Include(e => e.Event)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();
    }

    public async Task<Enrollment?> GetByEventAndUserAsync(int eventId, string userId)
    {
        using var context = CreateContext();
        return await context.Enrollments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == eventId && e.UserId == userId);
    }

    public async Task<IEnumerable<Enrollment>> GetByAttendanceAsync(bool willAttend)
    {
        using var context = CreateContext();
        return await context.Enrollments
            .AsNoTracking()
            .Where(e => e.WillAttend == willAttend)
            .Include(e => e.User)
            .Include(e => e.Event)
            .ToListAsync();
    }

    public async Task DeleteByEventIdAsync(int eventId)
    {
        using var context = CreateContext();
        await context.Enrollments
            .Where(e => e.EventId == eventId)
            .ExecuteDeleteAsync();
    }
}
