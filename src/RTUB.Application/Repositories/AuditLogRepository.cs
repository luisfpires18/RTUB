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
        using var context = CreateContext();
        return await context.Set<AuditLog>()
            .AsNoTracking()
            .OrderByDescending(a => a.Timestamp)
            .PaginateAsync(page, pageSize);
    }

    public async Task<IEnumerable<AuditLog>> GetCriticalAsync(int page, int pageSize)
    {
        using var context = CreateContext();
        return await context.Set<AuditLog>()
            .AsNoTracking()
            .Where(a => a.IsCriticalAction)
            .OrderByDescending(a => a.Timestamp)
            .PaginateAsync(page, pageSize);
    }

    public async Task<IEnumerable<AuditLog>> GetByEntityTypeAsync(string entityType, int page, int pageSize)
    {
        using var context = CreateContext();
        return await context.Set<AuditLog>()
            .AsNoTracking()
            .Where(a => a.EntityType == entityType)
            .OrderByDescending(a => a.Timestamp)
            .PaginateAsync(page, pageSize);
    }

    public async Task<IEnumerable<AuditLog>> GetByUserAsync(string userName, int page, int pageSize)
    {
        using var context = CreateContext();
        return await context.Set<AuditLog>()
            .AsNoTracking()
            .Where(a => a.UserName != null && a.UserName.Contains(userName))
            .OrderByDescending(a => a.Timestamp)
            .PaginateAsync(page, pageSize);
    }

    public async Task DeleteAllAsync()
    {
        using var context = CreateContext();
        await context.Set<AuditLog>().ExecuteDeleteAsync();
    }

    public async Task DeleteByUserAsync(string userName)
    {
        using var context = CreateContext();
        await context.Set<AuditLog>()
            .Where(a => a.UserName == userName)
            .ExecuteDeleteAsync();
    }
}
