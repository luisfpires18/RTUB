namespace RTUB.Core.Entities;

/// <summary>
/// Per-user settings for a conversation (mute, pin)
/// Allows users to customize their experience for each conversation independently
/// </summary>
public class ConversationUserSettings : BaseEntity
{
    /// <summary>
    /// The ID of the conversation these settings apply to
    /// </summary>
    public int ConversationId { get; set; }

    /// <summary>
    /// The ID of the user these settings belong to
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Whether this user has muted this conversation
    /// When muted, push notifications are suppressed but messages still appear in inbox
    /// </summary>
    public bool IsMuted { get; set; }

    /// <summary>
    /// Whether this user has pinned this conversation
    /// Pinned conversations appear at the top of the conversation list
    /// </summary>
    public bool IsPinned { get; set; }

    /// <summary>
    /// Navigation property to the conversation
    /// </summary>
    public Conversation? Conversation { get; set; }

    /// <summary>
    /// Navigation property to the user
    /// </summary>
    public ApplicationUser? User { get; set; }
}
