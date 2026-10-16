using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for AuditLog entity
/// </summary>
public interface IAuditLogRepository : IRepository<AuditLog>
{

    /// <summary>
    /// Delete all audit logs in bulk (single database operation)
    /// </summary>
    Task DeleteAllAsync();

    /// <summary>
    /// Delete all audit logs for a specific user in bulk (single database operation)
    /// </summary>
    Task DeleteByUserAsync(string userName);
}
