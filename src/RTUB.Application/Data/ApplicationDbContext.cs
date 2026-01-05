using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using RTUB.Core.Entities;
using System.Text.Json;
using RTUB.Application.Services;
using RTUB.Core.Constants;

namespace RTUB.Application.Data;

/// <summary>
/// Database context for the application
/// Extends IdentityDbContext for ASP.NET Core Identity support
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuditContext _auditContext;
    private bool _isAuditingEnabled = true;

    // Critical entities that should always be flagged in audit logs
    private static readonly string[] CriticalEntities = { "RoleAssignment", "Report", "ApplicationUser", "FiscalYear" };

    // Constants for audit logging
    private const int BinaryDataTruncateThreshold = 100;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor httpContextAccessor, AuditContext auditContext)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
        _auditContext = auditContext;
    }

    // Domain entity DbSets
    public DbSet<Event> Events { get; set; }
    public DbSet<Enrollment> Enrollments { get; set; }
    public DbSet<Album> Albums { get; set; }
    public DbSet<Song> Songs { get; set; }
    public DbSet<SongYouTubeUrl> SongYouTubeUrls { get; set; }
    public DbSet<SongVideo> SongVideos { get; set; } = null!;
    public DbSet<SongPlayCount> SongPlayCounts { get; set; }
    public DbSet<EventVideo> EventVideos { get; set; } = null!;
    public DbSet<EventRepertoire> EventRepertoires { get; set; }
    public DbSet<Report> Reports { get; set; }
    public DbSet<Activity> Activities { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<FiscalYear> FiscalYears { get; set; }
    public DbSet<Slideshow> Slideshows { get; set; }
    public DbSet<Label> Labels { get; set; }
    public DbSet<Request> Requests { get; set; }
    public DbSet<RoleAssignment> RoleAssignments { get; set; }
    public DbSet<Rehearsal> Rehearsals { get; set; }
    public DbSet<RehearsalAttendance> RehearsalAttendances { get; set; }

    // Inventory & Shop DbSets
    public DbSet<Instrument> Instruments { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductReservation> ProductReservations { get; set; }
    public DbSet<Trophy> Trophies { get; set; }

    // Logistics Board DbSets
    public DbSet<LogisticsBoard> LogisticsBoards { get; set; }
    public DbSet<LogisticsList> LogisticsLists { get; set; }
    public DbSet<LogisticsCard> LogisticsCards { get; set; }

    // Audit Log DbSet
    public DbSet<AuditLog> AuditLogs { get; set; }

    // Discussion DbSets
    public DbSet<Discussion> Discussions { get; set; }
    public DbSet<Post> Posts { get; set; }
    public DbSet<Comment> Comments { get; set; }
    public DbSet<PostMedia> PostMedia { get; set; }
    public DbSet<CommentImage> CommentImages { get; set; }

    // Meeting DbSet
    public DbSet<Meeting> Meetings { get; set; }
    public DbSet<MeetingRequest> MeetingRequests { get; set; }

    // Leaderboard Comments DbSets
    public DbSet<LeaderboardComment> LeaderboardComments { get; set; }
    public DbSet<LeaderboardCommentLike> LeaderboardCommentLikes { get; set; }

    // Member Instruments DbSet
    public DbSet<MemberInstrument> MemberInstruments { get; set; }

    // Member Status DbSet
    public DbSet<MemberStatus> MemberStatuses { get; set; }

    // Geocoding Cache DbSet
    public DbSet<GeocodingCache> GeocodingCaches { get; set; }

    // Push Notifications DbSet
    public DbSet<PushSubscription> PushSubscriptions { get; set; }

    // Messaging DbSets
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<ConversationUserSettings> ConversationUserSettings { get; set; }

    // Gallery DbSets
    public DbSet<GalleryMedia> GalleryMedia { get; set; }
    public DbSet<GalleryMediaPersonTag> GalleryMediaPersonTags { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply EF Core configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

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

        // Fallback to AuditContext for operations outside HTTP context (e.g., Blazor InteractiveServer)  
        // or when user is not yet authenticated (e.g., during login before SignInAsync is called)
        if (string.IsNullOrEmpty(username))
        {
            username = _auditContext.UserName;
            userId = _auditContext.UserId;
        }

        // Collect audit entries before saving
        var auditEntries = new List<AuditLog>();

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
                    auditEntries.Add(CreateAuditLog(entry, "Created", username, userId));
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    if (!string.IsNullOrEmpty(username))
                    {
                        entry.Entity.UpdatedBy = username;
                    }

                    // Check if this is a soft delete (DeletedAt field changed from null to a value)
                    var deletedAtProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "DeletedAt");
                    var isSoftDelete = deletedAtProperty != null
                        && deletedAtProperty.IsModified
                        && deletedAtProperty.OriginalValue == null
                        && deletedAtProperty.CurrentValue != null;

                    // Create audit log - use "Deleted" action for soft deletes, "Modified" otherwise
                    var action = isSoftDelete ? "Deleted" : "Modified";
                    auditEntries.Add(CreateAuditLog(entry, action, username, userId));
                    break;

                case EntityState.Deleted:
                    // Create audit log for deleted entity
                    auditEntries.Add(CreateAuditLog(entry, "Deleted", username, userId));
                    break;
            }
        }

        // Also track ApplicationUser changes (not BaseEntity)
        foreach (var entry in ChangeTracker.Entries<ApplicationUser>())
        {
            if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
            {
                var action = entry.State == EntityState.Deleted ? "Deleted" : "Modified";
                var auditLog = CreateAuditLogForUser(entry, action, username, userId);

                // Only add audit entry if there are meaningful changes to log
                if (auditLog != null)
                {
                    auditEntries.Add(auditLog);
                }
            }
        }

        // Track role changes (critical action)
        // Collect IDs during change tracking, then resolve names asynchronously after save
        var pendingRoleAudits = new List<(string Action, string TargetUserId, string RoleId, string? CachedUsername, string? CachedRoleName)>();

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

        var result = await base.SaveChangesAsync(cancellationToken);

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

                auditEntries.Add(new AuditLog
                {
                    EntityType = "UserRole",
                    EntityId = null,
                    Action = pending.Action,
                    UserId = userId,
                    UserName = username,
                    Timestamp = DateTime.UtcNow,
                    Changes = JsonSerializer.Serialize(new
                    {
                        Username = targetUsername,
                        Role = roleName
                    }),
                    IsCriticalAction = true,
                    EntityDisplayName = $"{targetUsername} - {roleName}"
                });
            }
        }

        // Save audit logs after successful save (disable auditing to prevent infinite loop)
        if (auditEntries.Any())
        {
            _isAuditingEnabled = false;
            try
            {
                AuditLogs.AddRange(auditEntries);
                await base.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                _isAuditingEnabled = true;
            }
        }

        return result;
    }

    private AuditLog CreateAuditLog(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<BaseEntity> entry, string action, string? username, string? userId)
    {
        var entityType = entry.Entity.GetType().Name;
        // For created entities, EntityId will be 0 and will be updated after SaveChanges
        var entityId = entry.Entity.Id == 0 ? (int?)null : entry.Entity.Id;
        var changes = new Dictionary<string, object?>();

        // Fields to exclude from logging (metadata fields)
        var excludedFields = new HashSet<string>
        {
            "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy", "Id"
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
        ResolveUserIdsToNicknames(entry, changes, entityType, action);

        var isCritical = IsCriticalAction(entityType, action);
        var displayName = GetEntityDisplayName(entry);

        // Determine target member for Enrollment, RehearsalAttendance, and PushSubscription
        string? targetMemberId = null;
        string? targetMemberName = null;

        if (entityType == "Enrollment" && entry.Entity is Enrollment enrollment)
        {
            targetMemberId = enrollment.UserId;
            // Try to get the user's name from navigation property or Local cache
            var targetUser = enrollment.User
                ?? Users.Local.FirstOrDefault(u => u.Id == enrollment.UserId);
            targetMemberName = targetUser?.Nickname ?? targetUser?.UserName;
        }
        else if (entityType == "RehearsalAttendance" && entry.Entity is RehearsalAttendance attendance)
        {
            targetMemberId = attendance.UserId;
            // Try to get the user's name from navigation property or Local cache
            var targetUser = attendance.User
                ?? Users.Local.FirstOrDefault(u => u.Id == attendance.UserId);
            targetMemberName = targetUser?.Nickname ?? targetUser?.UserName;
        }
        else if (entityType == "PushSubscription" && entry.Entity is PushSubscription pushSubscription)
        {
            targetMemberId = pushSubscription.UserId;
            // Try to get the user's name from navigation property or Local cache
            var targetUser = pushSubscription.User
                ?? Users.Local.FirstOrDefault(u => u.Id == pushSubscription.UserId);
            targetMemberName = targetUser?.Nickname ?? targetUser?.UserName;
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

    /// <summary>
    /// Replaces UserId fields with user nicknames for better readability in audit logs.
    /// Applies to LeaderboardComment, Post, and Comment entities.
    /// </summary>
    private void ResolveUserIdsToNicknames(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
        Dictionary<string, object?> changes,
        string entityType,
        string action)
    {
        // List of entity types and their UserId fields that should be resolved to nicknames
        var entityUserIdFields = new Dictionary<string, List<string>>
        {
            ["LeaderboardComment"] = new List<string> { "AuthorId", "TargetUserId" },
            ["LeaderboardCommentLike"] = new List<string> { "UserId" },
            ["Post"] = new List<string> { "AuthorId" },
            ["Comment"] = new List<string> { "AuthorId" }
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

                    var oldNickname = ResolveUserIdToNickname(oldUserId);
                    var newNickname = ResolveUserIdToNickname(newUserId);

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

                            var oldNickname = ResolveUserIdToNickname(oldUserId);
                            var newNickname = ResolveUserIdToNickname(newUserId);

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
                var nickname = ResolveUserIdToNickname(userId);
                changes[fieldName] = nickname ?? userId;
            }
        }
    }

    /// <summary>
    /// Resolves a UserId to a user's nickname from the local cache.
    /// Returns null if user is not found in cache.
    /// </summary>
    private string? ResolveUserIdToNickname(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return null;

        var user = Users.Local.FirstOrDefault(u => u.Id == userId);
        return user?.Nickname ?? user?.UserName;
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

    private AuditLog? CreateAuditLogForUser(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<ApplicationUser> entry, string action, string? username, string? userId)
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
            "LastLoginDate" // Exclude login tracking - already logged separately
        };

        // Critical fields that should mark the action as critical (even if not logged)
        var criticalFields = new HashSet<string>
        {
            "PasswordHash", "SecurityStamp", "Email", "UserName", "PhoneNumber"
        };

        if (action == "Modified")
        {
            // Add target user identification at the beginning for easy reference
            // Store as a simple readable string instead of JSON object
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

                // If one is empty/null, check if the other is an empty JSON collection
                if (oldIsEmpty || newIsEmpty)
                {
                    var nonEmptyStr = oldIsEmpty ? newStr : oldStr;
                    var trimmed = nonEmptyStr.TrimStart();

                    if (trimmed.StartsWith("[") || trimmed.StartsWith("{"))
                    {
                        var json = JsonSerializer.Deserialize<JsonElement>(nonEmptyStr);
                        if (IsEmptyJsonCollection(json))
                            return true; // null/empty and empty JSON collection are semantically equal
                    }
                }

                // Both are non-empty strings, check if they're JSON
                if (!oldIsEmpty && !newIsEmpty)
                {
                    var oldTrimmed = oldStr.TrimStart();
                    var newTrimmed = newStr.TrimStart();

                    if ((oldTrimmed.StartsWith("[") || oldTrimmed.StartsWith("{")) &&
                        (newTrimmed.StartsWith("[") || newTrimmed.StartsWith("{")))
                    {
                        var oldJson = JsonSerializer.Deserialize<JsonElement>(oldStr);
                        var newJson = JsonSerializer.Deserialize<JsonElement>(newStr);

                        // Check if both are empty arrays or objects
                        if (IsEmptyJsonCollection(oldJson) && IsEmptyJsonCollection(newJson))
                            return true;

                        // Use JsonElement's equality which properly compares structure
                        return oldJson.Equals(newJson);
                    }
                }
            }
            catch (JsonException)
            {
                // Not valid JSON, fall back to string comparison
            }

            return false;
        }

        // One is null, the other isn't (and neither is a string)
        if (oldValue == null || newValue == null)
            return false;

        // Use standard Equals for most types
        return Equals(oldValue, newValue);
    }

    /// <summary>
    /// Checks if a JsonElement represents an empty collection (empty array or empty object)
    /// </summary>
    private bool IsEmptyJsonCollection(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.GetArrayLength() == 0;
        }
        if (element.ValueKind == JsonValueKind.Object)
        {
            return !element.EnumerateObject().Any();
        }
        return false;
    }

    private bool IsCriticalAction(string entityType, string action)
    {
        // Check if entity type is in the critical entities list
        if (CriticalEntities.Contains(entityType))
        {
            return true;
        }

        // Deletions are always critical
        if (action == "Deleted")
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves the display name for an entity based on its type and ID
    /// Optimized to only use Local cache to avoid database queries during SaveChanges
    /// </summary>
    private string? GetEntityDisplayName(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        var entityType = entry.Entity.GetType().Name;

        try
        {
            switch (entityType)
            {
                case "Song":
                    if (entry.Entity is Song song)
                        return song.Title;
                    break;

                case "Event":
                    if (entry.Entity is Event evt)
                        return evt.Name;
                    break;

                case "Enrollment":
                    if (entry.Entity is Enrollment enrollment)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var userName = enrollment.User?.Nickname
                            ?? enrollment.User?.UserName
                            ?? ResolveUserIdToNickname(enrollment.UserId);
                        var eventName = enrollment.Event?.Name
                            ?? Events.Local.FirstOrDefault(e => e.Id == enrollment.EventId)?.Name;

                        if (userName != null && eventName != null)
                            return $"{userName} - {eventName}";
                        return eventName ?? userName; // Return partial if one is missing
                    }
                    break;

                case "EventRepertoire":
                    if (entry.Entity is EventRepertoire repertoire)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var eventName = repertoire.Event?.Name
                            ?? Events.Local.FirstOrDefault(e => e.Id == repertoire.EventId)?.Name;
                        var songTitle = repertoire.Song?.Title
                            ?? Songs.Local.FirstOrDefault(s => s.Id == repertoire.SongId)?.Title;

                        if (eventName != null && songTitle != null)
                            return $"{eventName} - {songTitle}";
                        return eventName ?? songTitle; // Return partial if one is missing
                    }
                    break;

                case "RehearsalAttendance":
                    if (entry.Entity is RehearsalAttendance attendance)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var userName = attendance.User?.Nickname
                            ?? attendance.User?.UserName
                            ?? ResolveUserIdToNickname(attendance.UserId);
                        var rehearsal = attendance.Rehearsal
                            ?? Rehearsals.Local.FirstOrDefault(r => r.Id == attendance.RehearsalId);

                        if (userName != null && rehearsal != null)
                            return $"{userName} - {rehearsal.Date:yyyy-MM-dd}";
                        if (userName != null)
                            return userName;
                        if (rehearsal != null)
                            return rehearsal.Date.ToString("yyyy-MM-dd");
                        return null; // Neither user name nor rehearsal found - will fall back to entity ID display
                    }
                    break;

                case "RoleAssignment":
                    if (entry.Entity is RoleAssignment roleAssignment)
                    {
                        // Try navigation property first (if loaded), then fall back to Local cache
                        var userName = roleAssignment.User?.Nickname
                            ?? roleAssignment.User?.UserName
                            ?? ResolveUserIdToNickname(roleAssignment.UserId)
                            ?? roleAssignment.UserId;
                        return $"{userName} - {roleAssignment.Position}";
                    }
                    break;

                case "SongYouTubeUrl":
                    if (entry.Entity is SongYouTubeUrl youtubeUrl)
                    {
                        // Try navigation property first (if loaded), then fall back to Local cache
                        var songTitle = youtubeUrl.Song?.Title
                            ?? Songs.Local.FirstOrDefault(s => s.Id == youtubeUrl.SongId)?.Title;
                        return songTitle;
                    }
                    break;

                case "Transaction":
                    if (entry.Entity is Transaction transaction && transaction.ActivityId.HasValue)
                    {
                        // Try navigation property first (if loaded), then fall back to Local cache
                        var activityName = transaction.Activity?.Name
                            ?? Activities.Local.FirstOrDefault(a => a.Id == transaction.ActivityId.Value)?.Name;
                        return activityName;
                    }
                    break;

                case "Activity":
                    if (entry.Entity is Activity activity2)
                        return activity2.Name;
                    break;

                case "Album":
                    if (entry.Entity is Album album)
                        return album.Title;
                    break;

                case "Instrument":
                    if (entry.Entity is Instrument instrument)
                        return $"{instrument.Category} - {instrument.Name}";
                    break;

                case "Label":
                    if (entry.Entity is Label label && !string.IsNullOrEmpty(label.Content))
                    {
                        return label.Content.Length > 100
                            ? label.Content[..100] + "..."
                            : label.Content;
                    }
                    break;

                case "Product":
                    if (entry.Entity is Product product)
                        return product.Name;
                    break;

                case "Rehearsal":
                    if (entry.Entity is Rehearsal rehearsal2)
                        return rehearsal2.Date.ToString("yyyy-MM-dd");
                    break;

                case "Report":
                    if (entry.Entity is Report report)
                        return report.Title;
                    break;

                case "Request":
                    // Request doesn't have a specific name field, use ID
                    return null;

                case "Slideshow":
                    // Slideshow can use Title
                    if (entry.Entity is Slideshow slideshow)
                        return slideshow.Title;
                    break;

                case "LogisticsBoard":
                    if (entry.Entity is LogisticsBoard logisticsBoard)
                        return logisticsBoard.Name;
                    break;

                case "LogisticsList":
                    if (entry.Entity is LogisticsList logisticsList)
                        return logisticsList.Name;
                    break;

                case "LogisticsCard":
                    if (entry.Entity is LogisticsCard logisticsCard)
                        return logisticsCard.Title;
                    break;

                case "Meeting":
                    if (entry.Entity is Meeting meeting)
                        return meeting.Title;
                    break;

                case "MeetingRequest":
                    if (entry.Entity is MeetingRequest meetingRequest)
                        return meetingRequest.Title;
                    break;

                case "LeaderboardComment":
                    if (entry.Entity is LeaderboardComment leaderboardComment)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var targetName = leaderboardComment.TargetUser?.Nickname
                            ?? leaderboardComment.TargetUser?.UserName
                            ?? ResolveUserIdToNickname(leaderboardComment.TargetUserId)
                            ?? leaderboardComment.TargetUserId;
                        var authorName = leaderboardComment.Author?.Nickname
                            ?? leaderboardComment.Author?.UserName
                            ?? ResolveUserIdToNickname(leaderboardComment.AuthorId)
                            ?? leaderboardComment.AuthorId;
                        return $"{authorName} → {targetName}";
                    }
                    break;

                case "LeaderboardCommentLike":
                    if (entry.Entity is LeaderboardCommentLike commentLike)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var userName = commentLike.User?.Nickname
                            ?? commentLike.User?.UserName
                            ?? ResolveUserIdToNickname(commentLike.UserId)
                            ?? commentLike.UserId;
                        var likedComment = commentLike.Comment
                            ?? LeaderboardComments.Local.FirstOrDefault(c => c.Id == commentLike.CommentId);
                        if (likedComment != null)
                        {
                            var targetName = likedComment.TargetUser?.Nickname
                                ?? likedComment.TargetUser?.UserName
                                ?? ResolveUserIdToNickname(likedComment.TargetUserId)
                                ?? likedComment.TargetUserId;
                            var commentPreview = likedComment.Text.Length > 30
                                ? likedComment.Text[..30] + "..."
                                : likedComment.Text;
                            return $"{userName} liked {targetName}'s comment: {commentPreview}";
                        }
                        return $"{userName} liked comment";
                    }
                    break;

                case "Post":
                    if (entry.Entity is Post post)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var postDiscussion = post.Discussion
                            ?? Discussions.Local.FirstOrDefault(d => d.Id == post.DiscussionId);
                        if (postDiscussion != null)
                        {
                            var postEvent = postDiscussion.Event
                                ?? Events.Local.FirstOrDefault(e => e.Id == postDiscussion.EventId);
                            if (postEvent != null)
                                return $"{postEvent.Name} - {post.Title}";
                        }
                        return post.Title;
                    }
                    break;

                case "Comment":
                    if (entry.Entity is Comment comment)
                    {
                        // Try navigation properties first (if loaded), then fall back to Local cache
                        var authorName = comment.Author?.Nickname
                            ?? comment.Author?.UserName
                            ?? ResolveUserIdToNickname(comment.AuthorId)
                            ?? comment.AuthorId;
                        var bodyPreview = comment.Body.Length > 50
                            ? comment.Body[..50] + "..."
                            : comment.Body;

                        // Try to get event name through Post -> Discussion -> Event
                        var commentPost = comment.Post
                            ?? Posts.Local.FirstOrDefault(p => p.Id == comment.PostId);
                        if (commentPost != null)
                        {
                            var commentDiscussion = commentPost.Discussion
                                ?? Discussions.Local.FirstOrDefault(d => d.Id == commentPost.DiscussionId);
                            if (commentDiscussion != null)
                            {
                                var commentEvent = commentDiscussion.Event
                                    ?? Events.Local.FirstOrDefault(e => e.Id == commentDiscussion.EventId);
                                if (commentEvent != null)
                                    return $"{commentEvent.Name} - {authorName}: {bodyPreview}";
                            }
                        }

                        return $"{authorName}: {bodyPreview}";
                    }
                    break;

                case "MemberInstrument":
                    if (entry.Entity is MemberInstrument memberInstrument)
                    {
                        // Try navigation property first (if loaded), then fall back to Local cache
                        var userName = memberInstrument.Member?.Nickname
                            ?? memberInstrument.Member?.UserName
                            ?? ResolveUserIdToNickname(memberInstrument.MemberId)
                            ?? memberInstrument.MemberId;
                        var instrumentName = RTUB.Core.Helpers.InstrumentTypeHelper.GetDisplayName(memberInstrument.InstrumentType);
                        return $"{userName} - {instrumentName}";
                    }
                    break;

                case "PushSubscription":
                    if (entry.Entity is PushSubscription pushSubscription)
                    {
                        // Try navigation property first (if loaded), then fall back to Local cache
                        var userName = pushSubscription.User?.Nickname
                            ?? pushSubscription.User?.UserName
                            ?? ResolveUserIdToNickname(pushSubscription.UserId)
                            ?? pushSubscription.UserId;
                        return userName;
                    }
                    break;
            }
        }
        catch (InvalidOperationException)
        {
            // If resolution fails due to database query issues, return null (will fall back to ID display)
            return null;
        }

        return null;
    }

    /// <summary>
    /// Temporarily disables audit logging for operations that need to save multiple times
    /// but should only create a single audit log entry.
    /// Use with caution and always re-enable auditing afterwards.
    /// </summary>
    public void DisableAuditing()
    {
        _isAuditingEnabled = false;
    }

    /// <summary>
    /// Re-enables audit logging after it was temporarily disabled.
    /// </summary>
    public void EnableAuditing()
    {
        _isAuditingEnabled = true;
    }
}
