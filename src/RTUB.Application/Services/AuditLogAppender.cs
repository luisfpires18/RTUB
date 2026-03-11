using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RTUB.Application.Interfaces;
using RTUB.Core.Constants;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for creating audit log entries from entity changes.
/// Extracted from ApplicationDbContext to follow Single Responsibility Principle.
/// </summary>
public class AuditLogAppender : IAuditLogAppender
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
        "LastLoginDate",  // Exclude login tracking — already logged separately
        "FidelisBalance"  // High-frequency balance updates — not business-critical
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
            EntityDisplayName = getEntityDisplayName(entry)
        };
    }

    public AuditLog? CreateAuditLogForUser(EntityEntry<ApplicationUser> entry, string action, string? username, string? userId)
    {
        var modifiedUser = entry.Entity;
        var changes = new Dictionary<string, object?>();
        var isCriticalChange = false;

        if (action == "Modified")
        {
            changes["_TargetUser"] = modifiedUser.UserName;

            var criticalFieldsModified = new List<string>();
            foreach (var property in entry.Properties)
            {
                if (!property.IsModified) continue;

                if (CriticalUserFields.Contains(property.Metadata.Name) &&
                    !AreValuesEqual(property.OriginalValue, property.CurrentValue))
                {
                    isCriticalChange = true;
                    criticalFieldsModified.Add(property.Metadata.Name);
                }
            }

            if (criticalFieldsModified.Count > 0)
                changes["_CriticalFieldsModified"] = criticalFieldsModified;

            foreach (var property in entry.Properties)
            {
                if (!property.IsModified || ExcludedUserFields.Contains(property.Metadata.Name))
                    continue;

                if (!AreValuesEqual(property.OriginalValue, property.CurrentValue))
                    changes[property.Metadata.Name] = BuildPropertyChange(property.Metadata.Name, property.OriginalValue, property.CurrentValue);
            }

            // _TargetUser is always present (count == 1); skip if nothing else meaningful logged
            if (changes.Count <= 1 && !isCriticalChange)
                return null;
        }
        else if (action == "Deleted")
        {
            isCriticalChange = true;
            changes["_TargetUser"] = new
            {
                UserId = modifiedUser.Id,
                UserName = modifiedUser.UserName,
                Email = modifiedUser.Email
            };

            foreach (var property in entry.Properties)
            {
                if (ExcludedUserFields.Contains(property.Metadata.Name))
                    continue;

                var value = property.CurrentValue;
                if (value != null && value is not byte[])
                    changes[property.Metadata.Name] = value;
            }
        }

        return new AuditLog
        {
            EntityType = "ApplicationUser",
            EntityId = null,
            Action = action,
            UserId = userId,
            UserName = username,
            Timestamp = DateTime.UtcNow,
            Changes = changes.Count > 0 ? JsonSerializer.Serialize(changes) : null,
            IsCriticalAction = isCriticalChange,
            EntityDisplayName = modifiedUser.UserName
        };
    }

    public AuditLog CreateRoleAuditLog(string action, string targetUserId, string roleId, string? targetUsername, string? roleName, string? username, string? userId)
    {
        return new AuditLog
        {
            EntityType = "UserRole",
            EntityId = null,
            Action = action,
            UserId = userId,
            UserName = username,
            Timestamp = DateTime.UtcNow,
            Changes = JsonSerializer.Serialize(new
            {
                Username = targetUsername ?? targetUserId,
                Role = roleName ?? roleId
            }),
            IsCriticalAction = true,
            EntityDisplayName = $"{targetUsername ?? targetUserId} - {roleName ?? roleId}"
        };
    }

    // --- Private helpers ---

    /// <summary>
    /// Builds a ChangeEntry for a modified property, with special handling for binary data.
    /// </summary>
    private ChangeEntry BuildPropertyChange(string fieldName, object? oldValue, object? newValue)
    {
        if (newValue is byte[] newBytes)
        {
            var oldDescription = oldValue is byte[] oldBytes && oldBytes.Length > 0
                ? GetBinaryDataDescription(fieldName, oldBytes.Length)
                : null;
            return new ChangeEntry(oldDescription, GetBinaryDataDescription(fieldName, newBytes.Length));
        }
        return new ChangeEntry(oldValue, newValue);
    }

    /// <summary>
    /// Replaces UserId fields with user nicknames for better readability in audit logs.
    /// </summary>
    private static void ResolveUserIdsToNicknames(
        Dictionary<string, object?> changes,
        string entityType,
        string action,
        Func<string?, string?> resolveUserIdToNickname)
    {
        if (!EntityUserIdFields.TryGetValue(entityType, out var fieldsToResolve))
            return;

        foreach (var fieldName in fieldsToResolve)
        {
            if (!changes.TryGetValue(fieldName, out var changeValue))
                continue;

            if (action == "Modified" && changeValue is ChangeEntry entry)
            {
                changes[fieldName] = new ChangeEntry(
                    resolveUserIdToNickname(entry.Old?.ToString()) ?? entry.Old?.ToString(),
                    resolveUserIdToNickname(entry.New?.ToString()) ?? entry.New?.ToString()
                );
            }
            else
            {
                var id = changeValue?.ToString();
                changes[fieldName] = resolveUserIdToNickname(id) ?? id;
            }
        }
    }

    private static string GetBinaryDataDescription(string? fieldName, int byteCount)
    {
        var lower = fieldName?.ToLowerInvariant() ?? string.Empty;

        var label = (lower.Contains("picture") || lower.Contains("photo") || lower.Contains("avatar")) ? "Picture" :
                    lower.Contains("image") ? "Image" :
                    (lower.Contains("file") || lower.Contains("document") || lower.Contains("pdf")) ? "File" :
                    "Binary data";

        return $"[{label} uploaded: {FormatBytes(byteCount)}]";
    }

    private static string FormatBytes(int bytes) => bytes switch
    {
        < 1024 => $"{bytes} bytes",
        < 1024 * 1024 => $"{bytes / 1024} KB",
        _ => $"{bytes / (1024 * 1024)} MB"
    };

    /// <summary>
    /// Compares two values for semantic equality, handling collections and JSON strings.
    /// </summary>
    private static bool AreValuesEqual(object? oldValue, object? newValue)
    {
        if (ReferenceEquals(oldValue, newValue)) return true;
        if (oldValue is null || newValue is null) return false;

        // Collection comparison (e.g. List<T> for primitive collections)
        if (oldValue is System.Collections.IEnumerable oldEnum &&
            newValue is System.Collections.IEnumerable newEnum &&
            oldValue is not string && newValue is not string)
        {
            var oldList = oldEnum.Cast<object>().ToList();
            var newList = newEnum.Cast<object>().ToList();
            return oldList.Count == newList.Count && oldList.SequenceEqual(newList);
        }

        // String comparison — only attempt JSON parse if both look like JSON objects/arrays
        if (oldValue is string oldStr && newValue is string newStr)
        {
            if (oldStr == newStr) return true;

            var oldIsJson = oldStr.Length > 0 && (oldStr[0] == '{' || oldStr[0] == '[');
            var newIsJson = newStr.Length > 0 && (newStr[0] == '{' || newStr[0] == '[');

            if (oldIsJson && newIsJson)
            {
                try
                {
                    var o = JsonSerializer.Deserialize<object>(oldStr);
                    var n = JsonSerializer.Deserialize<object>(newStr);
                    return JsonSerializer.Serialize(o) == JsonSerializer.Serialize(n);
                }
                catch
                {
                    // Fall through to false
                }
            }

            return false;
        }

        return Equals(oldValue, newValue);
    }

    private static bool IsCriticalAction(string entityType, string action)
        => AuditConfiguration.CriticalEntityTypes.Contains(entityType) || action == "Deleted";
}
