using Microsoft.EntityFrameworkCore.ChangeTracking;
using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for creating audit log entries from entity changes
/// Separates audit log creation logic from DbContext
/// </summary>
public interface IAuditLogAppender
{
    /// <summary>
    /// Creates an audit log entry for a BaseEntity change
    /// </summary>
    AuditLog? CreateAuditLog(EntityEntry<BaseEntity> entry, string action, string? username, string? userId, Func<string?, string?> resolveUserIdToNickname, Func<EntityEntry<BaseEntity>, string?> getEntityDisplayName);

    /// <summary>
    /// Creates an audit log entry for an ApplicationUser change
    /// </summary>
    AuditLog? CreateAuditLogForUser(EntityEntry<ApplicationUser> entry, string action, string? username, string? userId);

    /// <summary>
    /// Creates an audit log entry for a role assignment change
    /// </summary>
    AuditLog CreateRoleAuditLog(string action, string targetUserId, string roleId, string? targetUsername, string? roleName, string? username, string? userId);
}
