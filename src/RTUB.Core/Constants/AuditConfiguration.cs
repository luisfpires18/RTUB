namespace RTUB.Core.Constants;

/// <summary>
/// Configuration for audit logging behavior
/// Defines which entities should be excluded from audit logging
/// </summary>
public static class AuditConfiguration
{
    /// <summary>
    /// Entity types that should be excluded from audit logging.
    /// 
    /// These entities are excluded because they are either:
    /// - High-frequency, low-value changes (e.g., SongPlayCount)
    /// - Messaging/conversation entities that would spam audit logs (e.g., Message, Conversation)
    /// - Tag associations that are frequently created/deleted (e.g., GalleryMediaPersonTag)
    /// - Entities with custom manual audit logging (e.g., MemberStatus)
    /// 
    /// HOW TO ADD NEW ENTITIES TO EXCLUDE:
    /// 1. Add the entity class name as a string to the HashSet below
    /// 2. The entity name must match the class name exactly (case-insensitive comparison is used)
    /// 3. This will automatically:
    ///    - Prevent audit logs from being created in ApplicationDbContext.SaveChangesAsync()
    ///    - Filter out existing audit logs in AuditLogService queries
    /// 
    /// Example: To exclude a new entity called "UserSession", add:
    ///     "UserSession",
    /// 
    /// Note: Only add entities that inherit from BaseEntity and generate excessive audit logs.
    /// Critical entities like ApplicationUser, RoleAssignment, Report, etc. should NEVER be excluded.
    /// </summary>
    /// <summary>
    /// Entity types excluded from Modified audit logging only.
    /// Created and Deleted actions are still logged.
    /// </summary>
    public static readonly HashSet<string> ModifiedExcludedEntityTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ForgedWeapon",
    };

    public static readonly HashSet<string> ExcludedEntityTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Message",
        "Conversation",
        "ConversationUserSettings",
        "GalleryMediaPersonTag",
        "SongPlayCount",
        "NaipePlayCount",
        "MemberStatus",
        "BetOption",
        "MeetingAtaAgendaPoint",
        "InventoryItem",
        "Character",
        "StageProgress",
        "BossModeProgress",
    };
}
