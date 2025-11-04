using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing audit logs
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Gets all audit logs with optional filtering
    /// </summary>
    Task<IEnumerable<AuditLog>> GetAllAsync(
        string? userName = null,
        string? excludeUserName = null,
        string? entityType = null,
        string? action = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        bool? criticalOnly = null,
        int page = 1,
        int pageSize = 100);
    
    /// <summary>
    /// Gets total count of audit logs with optional filtering
    /// </summary>
    Task<int> GetCountAsync(
        string? userName = null,
        string? excludeUserName = null,
        string? entityType = null,
        string? action = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        bool? criticalOnly = null);
    
    /// <summary>
    /// Gets audit logs for a specific entity
    /// </summary>
    Task<IEnumerable<AuditLog>> GetEntityHistoryAsync(string entityType, int entityId);
    
    /// <summary>
    /// Searches audit logs by changes content
    /// </summary>
    Task<IEnumerable<AuditLog>> SearchChangesAsync(string searchTerm, int page = 1, int pageSize = 100);
    
    /// <summary>
    /// Gets distinct entity types from audit logs
    /// </summary>
    Task<IEnumerable<string>> GetEntityTypesAsync();
    
    /// <summary>
    /// Gets distinct action types from audit logs
    /// </summary>
    Task<IEnumerable<string>> GetActionTypesAsync();
    
    /// <summary>
    /// Gets distinct user names from audit logs
    /// </summary>
    Task<IEnumerable<string>> GetUserNamesAsync();
    
    /// <summary>
    /// Deletes a specific audit log entry
    /// </summary>
    Task DeleteAsync(int id);
    
    /// <summary>
    /// Truncates (deletes all) audit logs
    /// </summary>
    Task TruncateAsync();
    
    /// <summary>
    /// Gets all audit logs for export (without pagination)
    /// </summary>
    Task<IEnumerable<AuditLog>> GetAllForExportAsync(
        string? userName = null,
        string? excludeUserName = null,
        string? entityType = null,
        string? action = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        bool? criticalOnly = null);
}
