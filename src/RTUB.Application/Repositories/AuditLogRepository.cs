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
        // Fetch all audit logs and remove them in bulk
        // Note: Using RemoveRange instead of ExecuteDeleteAsync for compatibility
        // with in-memory database provider used in tests
        var allLogs = await _dbSet.ToListAsync();
        _dbSet.RemoveRange(allLogs);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteByUserAsync(string userName)
    {
        // Fetch all audit logs for the specified user and remove them in bulk
        // Note: Using RemoveRange instead of ExecuteDeleteAsync for compatibility
        // with in-memory database provider used in tests
        var userLogs = await _dbSet.Where(a => a.UserName == userName).ToListAsync();
        _dbSet.RemoveRange(userLogs);
        await _context.SaveChangesAsync();
    }
}
