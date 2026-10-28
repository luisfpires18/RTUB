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
    /// <summary>
    /// Entity types that mark an audit log entry as a critical action.
    /// Deletions of any entity are also critical regardless of this list.
    /// </summary>
    /// <summary>
    /// Defence in depth for private conversations (task 041): if a messaging entity is ever audited (taken out of
    /// <see cref="ExcludedEntityTypes"/> by mistake), these properties are written as <see cref="RedactedValue"/>, never
    /// their content. Targeted per entity, so generic names such as Title or Body stay readable on other entities.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, HashSet<string>> RedactedProperties =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Message"] = new(StringComparer.Ordinal) { "Body", "ReplyToBody", "ReplyToSenderName", "ReadBy", "Link" },
            ["Conversation"] = new(StringComparer.Ordinal) { "Title", "Participants" },
            ["MessageReaction"] = new(StringComparer.Ordinal) { "Emoji" },
            // #220: a push subscription's endpoint and keys address and decrypt the member's device; never logged.
            ["PushSubscription"] = new(StringComparer.Ordinal) { "Endpoint", "Auth", "P256dh" },
        };

    public const string RedactedValue = "[redacted]";

    public static readonly HashSet<string> CriticalEntityTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "RoleAssignment", "Report", "ApplicationUser"
    };

    public static readonly HashSet<string> ModifiedExcludedEntityTypes = new(StringComparer.OrdinalIgnoreCase)
    {
    };

    public static readonly HashSet<string> ExcludedEntityTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Messaging (v1 tables, Messaging v2 since task 041): private member chats never reach /owner/tracing.
        "Message",
        "Conversation",
        "ConversationUserSettings",
        "MessageReaction",
        "GalleryMediaPersonTag",
        "SongPlayCount",
        "NaipePlayCount",
        "LoginEvent",
        "MemberStatus",
        "MeetingAtaAgendaPoint",
        "MeetingAtaConfirmation",
    };
}
