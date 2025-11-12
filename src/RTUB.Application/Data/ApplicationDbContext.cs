using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using RTUB.Core.Entities;
using System.Text.Json;
using RTUB.Application.Services;

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
    public DbSet<LogisticsList> LogisticsLists { get; set; }
    public DbSet<LogisticsCard> LogisticsCards { get; set; }
    
    // Audit Log DbSet
    public DbSet<AuditLog> AuditLogs { get; set; }

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
                    
                    // Create audit log for modified entity
                    auditEntries.Add(CreateAuditLog(entry, "Modified", username, userId));
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
        foreach (var entry in ChangeTracker.Entries<IdentityUserRole<string>>())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Deleted)
            {
                var action = entry.State == EntityState.Added ? "Role Added" : "Role Removed";
                
                // Resolve username from UserId
                // Note: Using Local first to avoid DB queries, then Find() which checks cache before querying.
                // This is done during SaveChanges, so async operations are not possible here.
                var targetUser = Users.Local.FirstOrDefault(u => u.Id == entry.Entity.UserId)
                    ?? Users.Find(entry.Entity.UserId);
                var targetUsername = targetUser?.UserName ?? entry.Entity.UserId;
                
                // Resolve role name from RoleId
                var role = Roles.Local.FirstOrDefault(r => r.Id == entry.Entity.RoleId)
                    ?? Roles.Find(entry.Entity.RoleId);
                var roleName = role?.Name ?? entry.Entity.RoleId;
                
                auditEntries.Add(new AuditLog
                {
                    EntityType = "UserRole",
                    EntityId = null,
                    Action = action,
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

        var result = await base.SaveChangesAsync(cancellationToken);

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
            // For deletions, log all non-excluded fields for context
            foreach (var property in entry.Properties)
            {
                if (!excludedFields.Contains(property.Metadata.Name))
                {
                    var value = property.OriginalValue;
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

        var isCritical = IsCriticalAction(entityType, action);
        var displayName = GetEntityDisplayName(entry);

        return new AuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            UserId = userId,
            UserName = username,
            Timestamp = DateTime.UtcNow,
            Changes = changes.Any() ? JsonSerializer.Serialize(changes) : null,
            IsCriticalAction = isCritical,
            EntityDisplayName = displayName
        };
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
            "Categories", "Positions" // Legacy collection properties - using CategoriesJson/PositionsJson instead
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
    /// Compares two values for semantic equality, properly handling JSON strings and collections
    /// </summary>
    private bool AreValuesEqual(object? oldValue, object? newValue)
    {
        // Both null or same reference
        if (ReferenceEquals(oldValue, newValue))
            return true;
        
        // Special handling for strings that might be JSON (like CategoriesJson, PositionsJson)
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
                        // Only check Local cache to avoid DB queries
                        var evt2 = Events.Local.FirstOrDefault(e => e.Id == enrollment.EventId);
                        return evt2?.Name;
                    }
                    break;
                
                case "EventRepertoire":
                    if (entry.Entity is EventRepertoire repertoire)
                    {
                        // Only check Local cache to avoid DB queries
                        var evt3 = Events.Local.FirstOrDefault(e => e.Id == repertoire.EventId);
                        var song2 = Songs.Local.FirstOrDefault(s => s.Id == repertoire.SongId);
                        if (evt3 != null && song2 != null)
                            return $"{evt3.Name} - {song2.Title}";
                        return evt3?.Name ?? song2?.Title; // Return partial if one is missing
                    }
                    break;
                
                case "RehearsalAttendance":
                    if (entry.Entity is RehearsalAttendance attendance)
                    {
                        // Only check Local cache to avoid DB queries
                        var user = Users.Local.FirstOrDefault(u => u.Id == attendance.UserId);
                        var rehearsal = Rehearsals.Local.FirstOrDefault(r => r.Id == attendance.RehearsalId);
                        if (user != null && rehearsal != null)
                            return $"{user.UserName} - {rehearsal.Date:yyyy-MM-dd}";
                        if (user != null)
                            return user.UserName;
                        if (rehearsal != null)
                            return rehearsal.Date.ToString("yyyy-MM-dd");
                        return null; // Neither found in cache
                    }
                    break;
                
                case "RoleAssignment":
                    if (entry.Entity is RoleAssignment roleAssignment)
                    {
                        // Only check Local cache to avoid DB queries
                        var user2 = Users.Local.FirstOrDefault(u => u.Id == roleAssignment.UserId);
                        return $"{user2?.UserName ?? roleAssignment.UserId} - {roleAssignment.Position}";
                    }
                    break;
                
                case "SongYouTubeUrl":
                    if (entry.Entity is SongYouTubeUrl youtubeUrl)
                    {
                        // Only check Local cache to avoid DB queries
                        var song3 = Songs.Local.FirstOrDefault(s => s.Id == youtubeUrl.SongId);
                        return song3?.Title;
                    }
                    break;
                
                case "Transaction":
                    if (entry.Entity is Transaction transaction && transaction.ActivityId.HasValue)
                    {
                        // Only check Local cache to avoid DB queries
                        var activity = Activities.Local.FirstOrDefault(a => a.Id == transaction.ActivityId.Value);
                        return activity?.Name;
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
                
                case "LogisticsList":
                    if (entry.Entity is LogisticsList logisticsList)
                        return logisticsList.Name;
                    break;
                
                case "LogisticsCard":
                    if (entry.Entity is LogisticsCard logisticsCard)
                        return logisticsCard.Title;
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
}
