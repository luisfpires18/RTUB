using Microsoft.EntityFrameworkCore;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using System;
using System.Collections.Generic;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing audit logs
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _auditLogRepository;
    private static readonly HashSet<string> HiddenEntityTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Message", "Conversation"
    };

    public AuditLogService(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    private static IQueryable<AuditLog> ExcludeHiddenEntities(IQueryable<AuditLog> query)
    {
        return query.Where(a => a.EntityType == null || !HiddenEntityTypes.Contains(a.EntityType));
    }

    /// <summary>
    /// Applies filters to an audit log query using WhereIf extension for cleaner conditional filtering
    /// </summary>
    private IQueryable<AuditLog> ApplyFilters(
        IQueryable<AuditLog> query,
        string? userName = null,
        string? excludeUserName = null,
        string? entityType = null,
        string? action = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        bool? criticalOnly = null)
    {
        query = ExcludeHiddenEntities(query);

        return query
            .WhereIf(!string.IsNullOrWhiteSpace(userName),
                a => a.UserName != null && a.UserName.Contains(userName!))
            .WhereIf(!string.IsNullOrWhiteSpace(excludeUserName),
                a => a.UserName == null || a.UserName != excludeUserName)
            .WhereIf(!string.IsNullOrWhiteSpace(entityType),
                a => a.EntityType == entityType)
            .WhereIf(!string.IsNullOrWhiteSpace(action), 
                a => a.Action == action)
            .WhereIf(fromDate.HasValue, 
                a => a.Timestamp >= fromDate!.Value)
            .WhereIf(toDate.HasValue, 
                a => a.Timestamp <= toDate!.Value)
            .WhereIf(criticalOnly.HasValue && criticalOnly.Value, 
                a => a.IsCriticalAction);
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
        var query = ApplyFilters(
            _auditLogRepository.Query(),
            userName,
            excludeUserName,
            entityType,
            action,
            fromDate,
            toDate,
            criticalOnly);

        return await query
            .OrderByDescending(a => a.Timestamp)
            .PaginateAsync(page, pageSize);
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
        var query = ApplyFilters(
            _auditLogRepository.Query(),
            userName,
            excludeUserName,
            entityType,
            action,
            fromDate,
            toDate,
            criticalOnly);

        return await query.CountAsync();
    }

    public async Task<IEnumerable<AuditLog>> GetEntityHistoryAsync(string entityType, int entityId)
    {
        return await ExcludeHiddenEntities(_auditLogRepository.Query())
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

        return await ExcludeHiddenEntities(_auditLogRepository.Query())
            .Where(a => a.Changes != null && a.Changes.Contains(searchTerm))
            .OrderByDescending(a => a.Timestamp)
            .PaginateAsync(page, pageSize);
    }

    public async Task<IEnumerable<string>> GetEntityTypesAsync()
    {
        return await ExcludeHiddenEntities(_auditLogRepository.Query())
            .Select(a => a.EntityType)
            .Distinct()
            .OrderBy(e => e)
            .ToListAsync();
    }

    public async Task<IEnumerable<string>> GetActionTypesAsync()
    {
        return await ExcludeHiddenEntities(_auditLogRepository.Query())
            .Select(a => a.Action)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();
    }

    public async Task<IEnumerable<string>> GetUserNamesAsync()
    {
        return await ExcludeHiddenEntities(_auditLogRepository.Query())
            .Where(a => a.UserName != null)
            .Select(a => a.UserName!)
            .Distinct()
            .OrderBy(u => u)
            .ToListAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await _auditLogRepository.DeleteAsync(id);
    }

    public async Task TruncateAsync()
    {
        // Use bulk delete for efficient single-command deletion
        await _auditLogRepository.DeleteAllAsync();
    }

    public async Task TruncateByUserAsync(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new ArgumentException("User name cannot be null or empty.", nameof(userName));
        }

        // Use bulk delete for efficient single-command deletion
        await _auditLogRepository.DeleteByUserAsync(userName);
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
        var query = ApplyFilters(
            _auditLogRepository.Query(),
            userName,
            excludeUserName,
            entityType,
            action,
            fromDate,
            toDate,
            criticalOnly);

        return await query
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();
    }
    
    public async Task<(IEnumerable<AuditLog> logs, int totalCount)> GetPagedWithCountAsync(
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
        var query = ApplyFilters(
            _auditLogRepository.Query(),
            userName,
            excludeUserName,
            entityType,
            action,
            fromDate,
            toDate,
            criticalOnly);

        // Get total count
        var totalCount = await query.CountAsync();
        
        // Get paged data
        var logs = await query
            .OrderByDescending(a => a.Timestamp)
            .PaginateAsync(page, pageSize);
        
        return (logs, totalCount);
    }
}
