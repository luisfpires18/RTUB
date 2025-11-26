using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for AuditLog entity
/// </summary>
public interface IAuditLogRepository : IRepository<AuditLog>
{
    /// <summary>
    /// Get recent audit logs with pagination
    /// </summary>
    Task<IEnumerable<AuditLog>> GetRecentAsync(int page, int pageSize);

    /// <summary>
    /// Get critical audit logs
    /// </summary>
    Task<IEnumerable<AuditLog>> GetCriticalAsync(int page, int pageSize);

    /// <summary>
    /// Get audit logs by entity type
    /// </summary>
    Task<IEnumerable<AuditLog>> GetByEntityTypeAsync(string entityType, int page, int pageSize);

    /// <summary>
    /// Get audit logs by user
    /// </summary>
    Task<IEnumerable<AuditLog>> GetByUserAsync(string userName, int page, int pageSize);

    /// <summary>
    /// Delete all audit logs in bulk (single database operation)
    /// </summary>
    Task DeleteAllAsync();

    /// <summary>
    /// Delete all audit logs for a specific user in bulk (single database operation)
    /// </summary>
    Task DeleteByUserAsync(string userName);
}
