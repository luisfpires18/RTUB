using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Services;
using RTUB.Core.Constants;
using RTUB.Core.Entities;

namespace RTUB.Application.Data;

// SaveChanges with audit entries and SQLite lock retry.

public partial class ApplicationDbContext
{
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Skip audit logging if disabled (to prevent infinite loop when saving audit logs)
        if (!_isAuditingEnabled)
        {
            return await base.SaveChangesAsync(cancellationToken);
        }

        // Try to get user from HttpContext first (for HTTP requests)
        var username = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        // Fallback to AuditContext for operations outside HTTP context (e.g., background services)
        // or when user is not yet authenticated (e.g., during login before SignInAsync is called)
        if (string.IsNullOrEmpty(username))
        {
            username = _auditContext.UserName;
            userId = _auditContext.UserId;
        }

        // Collect audit entries before saving
        var auditEntries = new List<AuditLog>();

        // Track Created entities to update EntityId after SaveChanges
        // (EntityId is 0/null before save, gets assigned after)
        var pendingCreatedAuditLogs = new List<(AuditLog auditLog, BaseEntity entity)>();

        // Track role changes (critical action)
        // Collect IDs during change tracking, then resolve names asynchronously after save
        var pendingRoleAudits = new List<(string Action, string TargetUserId, string RoleId, string? CachedUsername, string? CachedRoleName)>();

        // #220: names the audited rows need (members, events, ...) and the tracker lacks, in at most one batched query
        // per table and only when needed; before the save, so deleted rows are still there to be named.
        await LoadAuditNamesAsync(ChangeTracker.Entries<BaseEntity>().Where(WillBeAudited).ToList(), cancellationToken);

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            // Skip audit logging for excluded entities (high-frequency, low-value changes)
            var entityTypeName = entry.Entity.GetType().Name;
            if (AuditConfiguration.ExcludedEntityTypes.Contains(entityTypeName))
                continue;

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    if (!string.IsNullOrEmpty(username))
                    {
                        entry.Entity.CreatedBy = username;
                    }

                    // Create audit log for new entity
                    var createdLog = _auditLogAppender.CreateAuditLog(entry, "Created", username, userId, ResolveUserIdToNickname, GetEntityDisplayName);
                    if (createdLog != null)
                    {
                        auditEntries.Add(createdLog);
                        // Track for EntityId update after SaveChanges
                        pendingCreatedAuditLogs.Add((createdLog, entry.Entity));
                    }
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    if (!string.IsNullOrEmpty(username))
                    {
                        entry.Entity.UpdatedBy = username;
                    }

                    // Skip audit logging for entities excluded from Modified-only tracking
                    if (AuditConfiguration.ModifiedExcludedEntityTypes.Contains(entityTypeName))
                        break;

                    // Check if this is a soft delete (DeletedAt field changed from null to a value)
                    var deletedAtProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "DeletedAt");
                    var isSoftDelete = deletedAtProperty != null
                        && deletedAtProperty.IsModified
                        && deletedAtProperty.OriginalValue == null
                        && deletedAtProperty.CurrentValue != null;

                    // Create audit log - use "Deleted" action for soft deletes, "Modified" otherwise
                    var action = isSoftDelete ? "Deleted" : "Modified";
                    var auditLog = _auditLogAppender.CreateAuditLog(entry, action, username, userId, ResolveUserIdToNickname, GetEntityDisplayName);
                    if (auditLog != null)
                    {
                        auditEntries.Add(auditLog);
                    }
                    break;

                case EntityState.Deleted:
                    // Create audit log for deleted entity
                    var deletedLog = _auditLogAppender.CreateAuditLog(entry, "Deleted", username, userId, ResolveUserIdToNickname, GetEntityDisplayName);
                    if (deletedLog != null)
                    {
                        auditEntries.Add(deletedLog);
                    }
                    break;
            }
        }

        // Also track ApplicationUser changes (not BaseEntity)
        foreach (var entry in ChangeTracker.Entries<ApplicationUser>())
        {
            if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
            {
                var action = entry.State == EntityState.Deleted ? "Deleted" : "Modified";
                var auditLog = _auditLogAppender.CreateAuditLogForUser(entry, action, username, userId);

                // Only add audit entry if there are meaningful changes to log
                if (auditLog != null)
                {
                    auditEntries.Add(auditLog);
                }
            }
        }

        // Track role changes (critical action)
        // Collect IDs during change tracking, then resolve names asynchronously after save

        foreach (var entry in ChangeTracker.Entries<IdentityUserRole<string>>())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Deleted)
            {
                var action = entry.State == EntityState.Added ? "Role Added" : "Role Removed";

                // Try to resolve from Local cache only (no database queries)
                var targetUser = Users.Local.FirstOrDefault(u => u.Id == entry.Entity.UserId);
                var role = Roles.Local.FirstOrDefault(r => r.Id == entry.Entity.RoleId);

                // Store IDs and any cached values for async resolution after save
                pendingRoleAudits.Add((
                    action,
                    entry.Entity.UserId,
                    entry.Entity.RoleId,
                    targetUser?.UserName,
                    role?.Name
                ));
            }
        }

        var result = await SaveChangesWithRetryAsync(cancellationToken);

        // Update EntityId for Created audit logs now that IDs are assigned
        foreach (var (auditLog, entity) in pendingCreatedAuditLogs)
        {
            if (entity.Id > 0)
            {
                auditLog.EntityId = entity.Id;
            }
        }

        // Resolve any missing user/role names asynchronously after the main save
        if (pendingRoleAudits.Any())
        {
            // Collect IDs that need to be resolved
            var userIdsToResolve = pendingRoleAudits
                .Where(p => p.CachedUsername == null)
                .Select(p => p.TargetUserId)
                .Distinct()
                .ToList();

            var roleIdsToResolve = pendingRoleAudits
                .Where(p => p.CachedRoleName == null)
                .Select(p => p.RoleId)
                .Distinct()
                .ToList();

            // Asynchronously fetch missing users and roles in batch
            var resolvedUsers = userIdsToResolve.Any()
                ? await Users.Where(u => userIdsToResolve.Contains(u.Id))
                    .Select(u => new { u.Id, u.UserName })
                    .ToDictionaryAsync(u => u.Id, u => u.UserName, cancellationToken)
                : new Dictionary<string, string?>();

            var resolvedRoles = roleIdsToResolve.Any()
                ? await Roles.Where(r => roleIdsToResolve.Contains(r.Id))
                    .Select(r => new { r.Id, r.Name })
                    .ToDictionaryAsync(r => r.Id, r => r.Name, cancellationToken)
                : new Dictionary<string, string?>();

            // Create audit logs with resolved names
            foreach (var pending in pendingRoleAudits)
            {
                var targetUsername = pending.CachedUsername
                    ?? (resolvedUsers.TryGetValue(pending.TargetUserId, out var resolvedUser) ? resolvedUser : null)
                    ?? pending.TargetUserId;

                var roleName = pending.CachedRoleName
                    ?? (resolvedRoles.TryGetValue(pending.RoleId, out var resolvedRole) ? resolvedRole : null)
                    ?? pending.RoleId;

                auditEntries.Add(_auditLogAppender.CreateRoleAuditLog(
                    pending.Action,
                    pending.TargetUserId,
                    pending.RoleId,
                    targetUsername,
                    roleName,
                    username,
                    userId));
            }
        }

        // Save audit logs after successful save (disable auditing to prevent infinite loop)
        if (auditEntries.Any())
        {
            _isAuditingEnabled = false;
            try
            {
                AuditLogs.AddRange(auditEntries);
                await SaveChangesWithRetryAsync(cancellationToken);
            }
            finally
            {
                _isAuditingEnabled = true;
            }
        }

        return result;
    }

    /// <summary>
    /// Maximum number of retry attempts for SQLite "database table is locked" errors.
    /// </summary>
    private const int SqliteRetryCount = 3;

    /// <summary>
    /// Wraps base.SaveChangesAsync with automatic retry for SQLite "database table is locked"
    /// (Error 6). This transient error occurs when multiple connections contend for write access.
    /// </summary>
    private async Task<int> SaveChangesWithRetryAsync(CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return await base.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (attempt < SqliteRetryCount && IsSqliteTableLocked(ex))
            {
                // Exponential backoff: 50ms, 150ms, ...
                await Task.Delay(50 * (int)Math.Pow(2, attempt - 1), cancellationToken);
            }
        }
    }

    /// <summary>
    /// Checks whether a DbUpdateException was caused by SQLite Error 6 ("database table is locked").
    /// </summary>
    private static bool IsSqliteTableLocked(DbUpdateException ex)
    {
        return ex.InnerException is SqliteException { SqliteErrorCode: 6 };
    }
}
