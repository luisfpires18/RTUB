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
    public AuditLogRepository(ApplicationDbContext context) : base(context)
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
}
