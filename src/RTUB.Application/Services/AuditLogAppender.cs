using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for creating audit log entries from entity changes.
/// Extracted from ApplicationDbContext to follow Single Responsibility Principle.
/// </summary>
public partial class AuditLogAppender : IAuditLogAppender
{
    // Typed change record — replaces anonymous types, eliminates reflection in ResolveUserIdsToNicknames.
    // Serializes identically to the previous anonymous { Old, New } shape.
    private record ChangeEntry(object? Old, object? New);

    // --- Static readonly config (allocated once, not per SaveChangesAsync call) ---

    private static readonly HashSet<string> ExcludedBaseFields = new(StringComparer.Ordinal)
    {
        "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy", "Id",
        "LastNotificationSent", // Question notification tracking — not business data
        "IsAwaitingUserReply",  // Question workflow state — not business data
        "Status"                // Question status changes handled via entity display name
    };

    // Same as above plus "DeletedAt" — used for soft-delete Modified entries
    private static readonly HashSet<string> SoftDeleteExcludedFields = new(ExcludedBaseFields, StringComparer.Ordinal)
    {
        "DeletedAt"
    };

    private static readonly HashSet<string> ExcludedUserFields = new(StringComparer.Ordinal)
    {
        "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "NormalizedUserName",
        "NormalizedEmail", "LockoutEnd", "AccessFailedCount", "TwoFactorEnabled",
        "PhoneNumberConfirmed", "EmailConfirmed", "LockoutEnabled",
        "LastLoginDate"   // Exclude login tracking — already logged separately
    };

    private static readonly HashSet<string> CriticalUserFields = new(StringComparer.Ordinal)
    {
        "PasswordHash", "SecurityStamp", "Email", "UserName", "PhoneNumber"
    };

    // Entity types and their UserId fields that should be resolved to nicknames
    private static readonly Dictionary<string, string[]> EntityUserIdFields =
        new(StringComparer.Ordinal)
        {
            ["LeaderboardComment"]     = ["AuthorId", "TargetUserId"],
            ["LeaderboardCommentLike"] = ["UserId"],
            ["Post"]                   = ["AuthorId"],
            ["Comment"]                = ["AuthorId"],
            ["PushSubscription"]       = ["UserId"],
            ["Question"]               = ["AuthorId", "AssignedMemberId"]
        };

    // ---------------------------------------------------------------------------

    public AuditLog? CreateAuditLog(EntityEntry<BaseEntity> entry, string action, string? username, string? userId, Func<string?, string?> resolveUserIdToNickname, Func<EntityEntry<BaseEntity>, string?> getEntityDisplayName)
    {
        var entityType = entry.Entity.GetType().Name;
        var entityId = entry.Entity.Id == 0 ? (int?)null : entry.Entity.Id;
        var changes = new Dictionary<string, object?>();

        var excludedFields = action == "Deleted" && entry.State == EntityState.Modified
            ? SoftDeleteExcludedFields
            : ExcludedBaseFields;

        if (action == "Modified")
        {
            foreach (var property in entry.Properties)
            {
                if (!property.IsModified || excludedFields.Contains(property.Metadata.Name))
                    continue;

                if (!AreValuesEqual(property.OriginalValue, property.CurrentValue))
                    changes[property.Metadata.Name] = BuildPropertyChange(property.Metadata.Name, property.OriginalValue, property.CurrentValue);
            }

            if (changes.Count == 0)
                return null;
        }
        else if (action == "Deleted")
        {
            foreach (var property in entry.Properties)
            {
                if (excludedFields.Contains(property.Metadata.Name))
                    continue;

                // For soft deletes use CurrentValue; for hard deletes use OriginalValue
                var value = entry.State == EntityState.Modified ? property.CurrentValue : property.OriginalValue;
                if (value != null && value is not byte[])
                    changes[property.Metadata.Name] = value;
            }
        }
        else // Created
        {
            foreach (var property in entry.Properties)
            {
                if (excludedFields.Contains(property.Metadata.Name))
                    continue;

                var value = property.CurrentValue;
                if (value == null) continue;
                if (value is string str && string.IsNullOrWhiteSpace(str)) continue;

                changes[property.Metadata.Name] = value is byte[] bytes && bytes.Length > 0
                    ? GetBinaryDataDescription(property.Metadata.Name, bytes.Length)
                    : value;
            }
        }

        ResolveUserIdsToNicknames(changes, entityType, action, resolveUserIdToNickname);
        Redact(changes, entityType);

        return new AuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            UserId = userId,
            UserName = username,
            Timestamp = DateTime.UtcNow,
            Changes = changes.Count > 0 ? JsonSerializer.Serialize(changes) : null,
            IsCriticalAction = IsCriticalAction(entityType, action),
            EntityDisplayName = Fit(getEntityDisplayName(entry))
        };
    }
}
