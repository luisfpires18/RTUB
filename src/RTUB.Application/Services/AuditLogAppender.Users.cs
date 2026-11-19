using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RTUB.Core.Constants;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

// Audit log: user and role entries, and nickname resolution.

public partial class AuditLogAppender
{
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
            // The nickname members know, with the username that identifies the account.
            EntityDisplayName = Fit(string.IsNullOrWhiteSpace(modifiedUser.Nickname) || modifiedUser.Nickname == modifiedUser.UserName
                ? modifiedUser.UserName
                : $"{modifiedUser.Nickname} ({modifiedUser.UserName})")
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

    private static bool IsCriticalAction(string entityType, string action)
        => AuditConfiguration.CriticalEntityTypes.Contains(entityType) || action == "Deleted";
}
