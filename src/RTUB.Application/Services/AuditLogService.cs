using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing audit logs
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly ApplicationDbContext _context;

    public AuditLogService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<AuditLog>> GetAllAsync(
        string? userName = null,
        string? excludeUserName = null,
        string? entityType = null,
        string? action = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        bool? criticalOnly = null,
        int page = 1,
        int pageSize = 100)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(userName))
        {
            query = query.Where(a => a.UserName != null && a.UserName.Contains(userName));
        }

        if (!string.IsNullOrWhiteSpace(excludeUserName))
        {
            query = query.Where(a => a.UserName == null || a.UserName != excludeUserName);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(a => a.EntityType == entityType);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(a => a.Action == action);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.Timestamp <= toDate.Value);
        }

        if (criticalOnly.HasValue && criticalOnly.Value)
        {
            query = query.Where(a => a.IsCriticalAction);
        }

        return await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> GetCountAsync(
        string? userName = null,
        string? excludeUserName = null,
        string? entityType = null,
        string? action = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        bool? criticalOnly = null)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(userName))
        {
            query = query.Where(a => a.UserName != null && a.UserName.Contains(userName));
        }

        if (!string.IsNullOrWhiteSpace(excludeUserName))
        {
            query = query.Where(a => a.UserName == null || a.UserName != excludeUserName);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(a => a.EntityType == entityType);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(a => a.Action == action);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.Timestamp <= toDate.Value);
        }

        if (criticalOnly.HasValue && criticalOnly.Value)
        {
            query = query.Where(a => a.IsCriticalAction);
        }

        return await query.CountAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetEntityHistoryAsync(string entityType, int entityId)
    {
        return await _context.AuditLogs
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();
    }

    public async Task<IEnumerable<AuditLog>> SearchChangesAsync(string searchTerm, int page = 1, int pageSize = 100)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return Enumerable.Empty<AuditLog>();
        }

        return await _context.AuditLogs
            .Where(a => a.Changes != null && a.Changes.Contains(searchTerm))
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<IEnumerable<string>> GetEntityTypesAsync()
    {
        return await _context.AuditLogs
            .Select(a => a.EntityType)
            .Distinct()
            .OrderBy(e => e)
            .ToListAsync();
    }

    public async Task<IEnumerable<string>> GetActionTypesAsync()
    {
        return await _context.AuditLogs
            .Select(a => a.Action)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();
    }

    public async Task<IEnumerable<string>> GetUserNamesAsync()
    {
        return await _context.AuditLogs
            .Where(a => a.UserName != null)
            .Select(a => a.UserName!)
            .Distinct()
            .OrderBy(u => u)
            .ToListAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var auditLog = await _context.AuditLogs.FindAsync(id);
        if (auditLog != null)
        {
            _context.AuditLogs.Remove(auditLog);
            await _context.SaveChangesAsync();
        }
    }

    public async Task TruncateAsync()
    {
        // Remove all audit logs (works with both in-memory and real databases)
        var allLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(allLogs);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetAllForExportAsync(
        string? userName = null,
        string? excludeUserName = null,
        string? entityType = null,
        string? action = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        bool? criticalOnly = null)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(userName))
        {
            query = query.Where(a => a.UserName != null && a.UserName.Contains(userName));
        }

        if (!string.IsNullOrWhiteSpace(excludeUserName))
        {
            query = query.Where(a => a.UserName == null || a.UserName != excludeUserName);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(a => a.EntityType == entityType);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(a => a.Action == action);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(a => a.Timestamp <= toDate.Value);
        }

        if (criticalOnly.HasValue && criticalOnly.Value)
        {
            query = query.Where(a => a.IsCriticalAction);
        }

        return await query
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();
    }
}
