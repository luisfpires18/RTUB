using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for AuditLog entity
/// </summary>
public class AuditLogRepository : Repository<AuditLog>, IAuditLogRepository
{
    public AuditLogRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<AuditLog>> GetRecentAsync(int page, int pageSize)
    {
        return await _dbSet
            .AsNoTracking()
            .OrderByDescending(a => a.Timestamp)
            .PaginateAsync(page, pageSize);
    }

    public async Task<IEnumerable<AuditLog>> GetCriticalAsync(int page, int pageSize)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(a => a.IsCriticalAction)
            .OrderByDescending(a => a.Timestamp)
            .PaginateAsync(page, pageSize);
    }

    public async Task<IEnumerable<AuditLog>> GetByEntityTypeAsync(string entityType, int page, int pageSize)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(a => a.EntityType == entityType)
            .OrderByDescending(a => a.Timestamp)
            .PaginateAsync(page, pageSize);
    }

    public async Task<IEnumerable<AuditLog>> GetByUserAsync(string userName, int page, int pageSize)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(a => a.UserName != null && a.UserName.Contains(userName))
            .OrderByDescending(a => a.Timestamp)
            .PaginateAsync(page, pageSize);
    }

    public async Task DeleteAllAsync()
    {
        using var context = CreateContext();
        var allLogs = await context.Set<AuditLog>().ToListAsync();
        context.Set<AuditLog>().RemoveRange(allLogs);
        await context.SaveChangesAsync();
    }

    public async Task DeleteByUserAsync(string userName)
    {
        using var context = CreateContext();
        var userLogs = await context.Set<AuditLog>().Where(a => a.UserName == userName).ToListAsync();
        context.Set<AuditLog>().RemoveRange(userLogs);
        await context.SaveChangesAsync();
    }
}
