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
}
