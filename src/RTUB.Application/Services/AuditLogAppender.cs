using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for creating audit log entries from entity changes
/// Extracted from ApplicationDbContext to follow Single Responsibility Principle
/// </summary>
public class AuditLogAppender : IAuditLogAppender
{
    // Critical entities that should always be flagged in audit logs
    private static readonly string[] CriticalEntities = { "RoleAssignment", "Report", "ApplicationUser", "FiscalYear" };

    // Constants for audit logging
    private const int BinaryDataTruncateThreshold = 100;

    public AuditLog? CreateAuditLog(EntityEntry<BaseEntity> entry, string action, string? username, string? userId, Func<string?, string?> resolveUserIdToNickname, Func<EntityEntry<BaseEntity>, string?> getEntityDisplayName)
    {
        var entityType = entry.Entity.GetType().Name;
        // For created entities, EntityId will be 0 and will be updated after SaveChanges
        var entityId = entry.Entity.Id == 0 ? (int?)null : entry.Entity.Id;
        var changes = new Dictionary<string, object?>();

        // Fields to exclude from logging (metadata fields)
        var excludedFields = new HashSet<string>
        {
            "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy", "Id",
            "LastNotificationSent", // Question notification tracking - not business data
            "IsAwaitingUserReply", // Question workflow state - not business data
            "Status" // Question status changes are handled via entity display name
        };

        // For soft deletes (action = "Deleted" but state = Modified), also exclude DeletedAt field
        if (action == "Deleted" && entry.State == EntityState.Modified)
        {
            excludedFields.Add("DeletedAt");
        }

        if (action == "Modified")
        {
            // Only log properties that actually changed
            foreach (var property in entry.Properties)
            {
                if (property.IsModified && !excludedFields.Contains(property.Metadata.Name))
                {
                    var oldValue = property.OriginalValue;
                    var newValue = property.CurrentValue;

                    // Only include if values are actually different
                    if (!AreValuesEqual(oldValue, newValue))
                    {
                        // Handle all binary data generically (not just large ones)
                        if (newValue is byte[] newBytes)
                        {
                            var oldDescription = oldValue is byte[] oldBytes && oldBytes.Length > 0
                                ? GetBinaryDataDescription(property.Metadata.Name, oldBytes.Length)
                                : null;
                            var newDescription = GetBinaryDataDescription(property.Metadata.Name, newBytes.Length);

                            changes[property.Metadata.Name] = new
                            {
                                Old = oldDescription,
                                New = newDescription
                            };
                        }
                        else
                        {
                            changes[property.Metadata.Name] = new
                            {
                                Old = oldValue,
                                New = newValue
                            };
                        }
                    }
                }
            }

            // Skip audit log creation if there are no meaningful changes for Modified actions
            if (!changes.Any())
            {
                return null;
            }
        }
        else if (action == "Deleted")
        {
            // For deletions (both hard and soft), log all non-excluded fields for context
            foreach (var property in entry.Properties)
            {
                if (!excludedFields.Contains(property.Metadata.Name))
                {
                    // For soft deletes, use CurrentValue; for hard deletes, use OriginalValue
                    var value = entry.State == EntityState.Modified ? property.CurrentValue : property.OriginalValue;
                    // Skip null and binary data
                    if (value != null && !(value is byte[]))
                    {
                        changes[property.Metadata.Name] = value;
                    }
                }
            }
        }
        else // Created
        {
            // For created entities, log all non-excluded fields
            foreach (var property in entry.Properties)
            {
                if (!excludedFields.Contains(property.Metadata.Name))
                {
                    var value = property.CurrentValue;

                    // Skip empty strings and null values
                    if (value != null)
                    {
                        if (value is string str && string.IsNullOrWhiteSpace(str))
                            continue;

                        // Truncate binary data with descriptive message
                        if (value is byte[] bytes && bytes.Length > 0)
                        {
                            changes[property.Metadata.Name] = GetBinaryDataDescription(property.Metadata.Name, bytes.Length);
                        }
                        else
                        {
                            changes[property.Metadata.Name] = value;
                        }
                    }
                }
            }
        }

        // Replace UserId with Nickname for specific entity types
        ResolveUserIdsToNicknames(entry, changes, entityType, action, resolveUserIdToNickname);

        var isCritical = IsCriticalAction(entityType, action);
        var displayName = getEntityDisplayName(entry);

        // Determine target member for Enrollment, RehearsalAttendance, and MeetingParticipation
        string? targetMemberId = null;
        string? targetMemberName = null;

        if (entityType == "Enrollment" && entry.Entity is Enrollment enrollment)
        {
            targetMemberId = enrollment.UserId;
            targetMemberName = resolveUserIdToNickname(enrollment.UserId);
        }
        else if (entityType == "RehearsalAttendance" && entry.Entity is RehearsalAttendance attendance)
        {
            targetMemberId = attendance.UserId;
            targetMemberName = resolveUserIdToNickname(attendance.UserId);
        }
        else if (entityType == "MeetingParticipation" && entry.Entity is MeetingParticipation meetingParticipation)
        {
            targetMemberId = meetingParticipation.UserId;
            targetMemberName = resolveUserIdToNickname(meetingParticipation.UserId);
        }

        return new AuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            UserId = userId,
            UserName = username,
            TargetMemberName = targetMemberName,
            Timestamp = DateTime.UtcNow,
            Changes = changes.Any() ? JsonSerializer.Serialize(changes) : null,
            IsCriticalAction = isCritical,
            EntityDisplayName = displayName
        };
    }

    public AuditLog? CreateAuditLogForUser(EntityEntry<ApplicationUser> entry, string action, string? username, string? userId)
    {
        var changes = new Dictionary<string, object?>();
        var isCriticalChange = false;

        // Get the modified user's information for identification
        var modifiedUser = entry.Entity;
        var modifiedUserId = modifiedUser.Id;
        var modifiedUserName = modifiedUser.UserName;
        var modifiedUserEmail = modifiedUser.Email;

        // Fields to exclude from logging (sensitive or infrastructure fields)
        var excludedFields = new HashSet<string>
        {
            "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "NormalizedUserName",
            "NormalizedEmail", "LockoutEnd", "AccessFailedCount", "TwoFactorEnabled",
            "PhoneNumberConfirmed", "EmailConfirmed", "LockoutEnabled",
            "LastLoginDate", // Exclude login tracking - already logged separately
            "FidelisBalance", // High-frequency balance updates - not business-critical
            "LastDailyRewardClaim" // Daily reward tracking - not business-critical
        };

        // Critical fields that should mark the action as critical (even if not logged)
        var criticalFields = new HashSet<string>
        {
            "PasswordHash", "SecurityStamp", "Email", "UserName", "PhoneNumber"
        };

        if (action == "Modified")
        {
            // Add target user identification at the beginning for easy reference
            changes["_TargetUser"] = $"{modifiedUserName}";

            // Track which critical fields were modified (for transparency without exposing values)
            var criticalFieldsModified = new List<string>();

            // First pass: Check if any modified property is a critical field
            foreach (var property in entry.Properties)
            {
                if (property.IsModified && criticalFields.Contains(property.Metadata.Name))
                {
                    var oldValue = property.OriginalValue;
                    var newValue = property.CurrentValue;

                    // Only mark as critical if values actually changed
                    if (!AreValuesEqual(oldValue, newValue))
                    {
                        isCriticalChange = true;
                        criticalFieldsModified.Add(property.Metadata.Name);
                    }
                }
            }

            // Add critical fields metadata if any were modified
            if (criticalFieldsModified.Any())
            {
                changes["_CriticalFieldsModified"] = criticalFieldsModified;
            }

            // Second pass: Log properties that actually changed (excluding sensitive fields)
            foreach (var property in entry.Properties)
            {
                if (property.IsModified && !excludedFields.Contains(property.Metadata.Name))
                {
                    var oldValue = property.OriginalValue;
                    var newValue = property.CurrentValue;

                    // Only include if values are actually different
                    if (!AreValuesEqual(oldValue, newValue))
                    {
                        // Handle binary data (e.g., ProfilePictureData)
                        if (newValue is byte[] newBytes)
                        {
                            var oldDescription = oldValue is byte[] oldBytes && oldBytes.Length > 0
                                ? GetBinaryDataDescription(property.Metadata.Name, oldBytes.Length)
                                : null;
                            var newDescription = GetBinaryDataDescription(property.Metadata.Name, newBytes.Length);

                            changes[property.Metadata.Name] = new
                            {
                                Old = oldDescription,
                                New = newDescription
                            };
                        }
                        else
                        {
                            changes[property.Metadata.Name] = new
                            {
                                Old = oldValue,
                                New = newValue
                            };
                        }
                    }
                }
            }
        }
        else if (action == "Deleted")
        {
            // Add target user identification for deletions
            changes["_TargetUser"] = new
            {
                UserId = modifiedUserId,
                UserName = modifiedUserName,
                Email = modifiedUserEmail
            };

            // For deletions, log all non-excluded fields for context
            // User deletions are always critical
            isCriticalChange = true;
            foreach (var property in entry.Properties)
            {
                if (!excludedFields.Contains(property.Metadata.Name))
                {
                    var value = property.CurrentValue;
                    // Skip null and binary data
                    if (value != null && !(value is byte[]))
                    {
                        changes[property.Metadata.Name] = value;
                    }
                }
            }
        }

        // Skip audit log if there are no meaningful changes to log (for Modified actions only)
        // Note: changes.Count > 1 because _TargetUser is always added
        // However, we still create an audit log if it's a critical change even if no values are logged
        if (action == "Modified" && changes.Count <= 1 && !isCriticalChange)
        {
            return null;
        }

        // Use the modified user's username as the display name
        var displayName = modifiedUserName;

        return new AuditLog
        {
            EntityType = "ApplicationUser",
            EntityId = null, // ApplicationUser uses string IDs (GUID), EntityId is int? for BaseEntity only
            Action = action,
            UserId = userId,
            UserName = username,
            Timestamp = DateTime.UtcNow,
            Changes = changes.Any() ? JsonSerializer.Serialize(changes) : null,
            IsCriticalAction = isCriticalChange,
            EntityDisplayName = displayName
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
    /// Applies to LeaderboardComment, Post, Comment, and PushSubscription entities.
    /// </summary>
    private void ResolveUserIdsToNicknames(
        EntityEntry entry,
        Dictionary<string, object?> changes,
        string entityType,
        string action,
        Func<string?, string?> resolveUserIdToNickname)
    {
        // List of entity types and their UserId fields that should be resolved to nicknames
        var entityUserIdFields = new Dictionary<string, List<string>>
        {
            ["LeaderboardComment"] = new List<string> { "AuthorId", "TargetUserId" },
            ["LeaderboardCommentLike"] = new List<string> { "UserId" },
            ["Post"] = new List<string> { "AuthorId" },
            ["Comment"] = new List<string> { "AuthorId" },
            ["PushSubscription"] = new List<string> { "UserId" },
            ["Question"] = new List<string> { "AuthorId", "AssignedMemberId" }
        };

        if (!entityUserIdFields.ContainsKey(entityType))
            return;

        var fieldsToResolve = entityUserIdFields[entityType];

        foreach (var fieldName in fieldsToResolve)
        {
            if (!changes.ContainsKey(fieldName))
                continue;

            var changeValue = changes[fieldName];

            if (action == "Modified")
            {
                // For modified entities, changeValue is an object with Old and New properties
                if (changeValue is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
                {
                    var oldUserId = jsonElement.GetProperty("Old").GetString();
                    var newUserId = jsonElement.GetProperty("New").GetString();

                    var oldNickname = resolveUserIdToNickname(oldUserId);
                    var newNickname = resolveUserIdToNickname(newUserId);

                    changes[fieldName] = new
                    {
                        Old = oldNickname ?? oldUserId,
                        New = newNickname ?? newUserId
                    };
                }
                else
                {
                    // Handle as anonymous type (most common case)
                    try
                    {
                        var oldProp = changeValue?.GetType().GetProperty("Old");
                        var newProp = changeValue?.GetType().GetProperty("New");

                        if (oldProp != null && newProp != null)
                        {
                            var oldUserId = oldProp.GetValue(changeValue)?.ToString();
                            var newUserId = newProp.GetValue(changeValue)?.ToString();

                            var oldNickname = resolveUserIdToNickname(oldUserId);
                            var newNickname = resolveUserIdToNickname(newUserId);

                            changes[fieldName] = new
                            {
                                Old = oldNickname ?? oldUserId,
                                New = newNickname ?? newUserId
                            };
                        }
                    }
                    catch
                    {
                        // If we can't parse it, leave as is
                    }
                }
            }
            else
            {
                // For created/deleted entities, changeValue is a string (the UserId)
                var userId = changeValue?.ToString();
                var nickname = resolveUserIdToNickname(userId);
                changes[fieldName] = nickname ?? userId;
            }
        }
    }

    private string GetBinaryDataDescription(string fieldName, int byteCount)
    {
        // Provide user-friendly descriptions for common binary field types
        var lowerFieldName = fieldName.ToLowerInvariant();

        if (lowerFieldName.Contains("picture") || lowerFieldName.Contains("photo") || lowerFieldName.Contains("avatar"))
        {
            return $"[Picture uploaded: {FormatBytes(byteCount)}]";
        }
        else if (lowerFieldName.Contains("image"))
        {
            return $"[Image uploaded: {FormatBytes(byteCount)}]";
        }
        else if (lowerFieldName.Contains("file") || lowerFieldName.Contains("document") || lowerFieldName.Contains("pdf"))
        {
            return $"[File uploaded: {FormatBytes(byteCount)}]";
        }
        else
        {
            return $"[Binary data: {FormatBytes(byteCount)}]";
        }
    }

    private string FormatBytes(int bytes)
    {
        if (bytes < 1024)
            return $"{bytes} bytes";
        else if (bytes < 1024 * 1024)
            return $"{bytes / 1024} KB";
        else
            return $"{bytes / (1024 * 1024)} MB";
    }

    /// <summary>
    /// Compares two values for semantic equality, properly handling collections
    /// </summary>
    private bool AreValuesEqual(object? oldValue, object? newValue)
    {
        // Both null or same reference
        if (ReferenceEquals(oldValue, newValue))
            return true;

        // Handle collection comparisons (e.g., List<T> for primitive collections)
        if (oldValue is System.Collections.IEnumerable oldEnumerable &&
            newValue is System.Collections.IEnumerable newEnumerable &&
            !(oldValue is string) && !(newValue is string))
        {
            var oldList = oldEnumerable.Cast<object>().ToList();
            var newList = newEnumerable.Cast<object>().ToList();

            // Compare counts first
            if (oldList.Count != newList.Count)
                return false;

            // Compare elements
            return oldList.SequenceEqual(newList);
        }

        // Special handling for strings that might be JSON
        if (oldValue is string || newValue is string)
        {
            // Convert null to empty string for comparison
            var oldStr = oldValue as string ?? string.Empty;
            var newStr = newValue as string ?? string.Empty;

            // Standard string equality first
            if (oldStr == newStr)
                return true;

            // Try to parse as JSON and compare the deserialized objects
            try
            {
                // Handle the case where one is null/empty and the other is an empty JSON array/object
                var oldIsEmpty = string.IsNullOrEmpty(oldStr);
                var newIsEmpty = string.IsNullOrEmpty(newStr);
                if (oldIsEmpty && newIsEmpty)
                    return true;

                var oldJson = JsonSerializer.Deserialize<object>(oldStr);
                var newJson = JsonSerializer.Deserialize<object>(newStr);
                return JsonSerializer.Serialize(oldJson) == JsonSerializer.Serialize(newJson);
            }
            catch
            {
                // If JSON parsing fails, fall back to string comparison
                return false;
            }
        }

        // Default equality comparison
        return Equals(oldValue, newValue);
    }

    private static bool IsCriticalAction(string entityType, string action)
    {
        return CriticalEntities.Contains(entityType) || action == "Deleted";
    }

}
