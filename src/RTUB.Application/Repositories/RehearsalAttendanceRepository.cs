using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for RehearsalAttendance entity.
/// Uses IDbContextFactory — each operation gets a fresh short-lived context.
/// </summary>
public class RehearsalAttendanceRepository : Repository<RehearsalAttendance>, IRehearsalAttendanceRepository
{
    public RehearsalAttendanceRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public override async Task<RehearsalAttendance> AddAsync(RehearsalAttendance entity)
    {
        using var context = CreateContext();

        // Preload User and Rehearsal into context Local cache for audit log display name resolution
        if (!string.IsNullOrEmpty(entity.UserId))
            await context.Users.FindAsync(entity.UserId);
        if (entity.RehearsalId > 0)
            await context.Rehearsals.FindAsync(entity.RehearsalId);

        // Clear navigation properties to avoid attaching stale related entities
        entity.User = null!;
        entity.Rehearsal = null!;

        await context.RehearsalAttendances.AddAsync(entity);
        await context.SaveChangesAsync();
        return entity;
    }

    public override async Task UpdateAsync(RehearsalAttendance entity)
    {
        using var context = CreateContext();

        // Preload User and Rehearsal into context Local cache for audit log display name resolution
        if (!string.IsNullOrEmpty(entity.UserId))
            await context.Users.FindAsync(entity.UserId);
        if (entity.RehearsalId > 0)
            await context.Rehearsals.FindAsync(entity.RehearsalId);

        var tracked = await context.RehearsalAttendances.FindAsync(entity.Id)
            ?? throw new InvalidOperationException(
                $"RehearsalAttendance with Id {entity.Id} not found in the database.");
        context.Entry(tracked).CurrentValues.SetValues(entity);
        await context.SaveChangesAsync();
    }

    public override async Task DeleteAsync(int id)
    {
        using var context = CreateContext();
        var entity = await context.RehearsalAttendances.FindAsync(id);
        if (entity == null) return;

        // Preload User and Rehearsal into context Local cache for audit log display name resolution
        if (!string.IsNullOrEmpty(entity.UserId))
            await context.Users.FindAsync(entity.UserId);
        if (entity.RehearsalId > 0)
            await context.Rehearsals.FindAsync(entity.RehearsalId);

        context.RehearsalAttendances.Remove(entity);
        await context.SaveChangesAsync();
    }

    public async Task<IEnumerable<RehearsalAttendance>> GetAttendancesByRehearsalIdAsync(int rehearsalId)
    {
        using var context = CreateContext();
        return await context.RehearsalAttendances
            .AsNoTracking()
            .Include(a => a.Rehearsal)
            .Include(a => a.User)
            .Where(a => a.RehearsalId == rehearsalId)
            .OrderBy(a => a.CheckedInAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<RehearsalAttendance>> GetAttendancesByRehearsalIdsAsync(IEnumerable<int> rehearsalIds)
    {
        var rehearsalIdsList = rehearsalIds.ToList();
        if (!rehearsalIdsList.Any())
            return Enumerable.Empty<RehearsalAttendance>();

        using var context = CreateContext();
        return await context.RehearsalAttendances
            .AsNoTracking()
            .Include(a => a.Rehearsal)
            .Include(a => a.User)
            .Where(a => rehearsalIdsList.Contains(a.RehearsalId))
            .OrderBy(a => a.CheckedInAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<RehearsalAttendance>> GetAttendancesByUserIdAsync(string userId)
    {
        using var context = CreateContext();
        return await context.RehearsalAttendances
            .AsNoTracking()
            .Include(a => a.Rehearsal)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.Rehearsal!.Date)
            .ToListAsync();
    }

    public async Task<RehearsalAttendance?> GetAttendanceByRehearsalAndUserAsync(int rehearsalId, string userId)
    {
        using var context = CreateContext();
        return await context.RehearsalAttendances
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.RehearsalId == rehearsalId && a.UserId == userId);
    }

    public async Task<(int TotalRehearsals, int Attended)> GetAttendanceStatsAsync(string userId)
    {
        using var context = CreateContext();
        var attendances = await context.RehearsalAttendances
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .ToListAsync();

        var totalRehearsals = attendances.Count;
        var attended = attendances.Count(a => a.WillAttend);

        return (totalRehearsals, attended);
    }

    public async Task DeleteByRehearsalIdAsync(int rehearsalId)
    {
        using var context = CreateContext();
        var items = await context.RehearsalAttendances
            .Where(a => a.RehearsalId == rehearsalId)
            .ToListAsync();
        context.RehearsalAttendances.RemoveRange(items);
        await context.SaveChangesAsync();
    }
}
