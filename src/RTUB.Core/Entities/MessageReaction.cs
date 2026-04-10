namespace RTUB.Core.Entities;

/// <summary>
/// Represents an emoji reaction to a message (one per user per message)
/// </summary>
public class MessageReaction : BaseEntity
{
    public int MessageId { get; set; }
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The emoji character (e.g. "👍", "❤️")
    /// </summary>
    public string Emoji { get; set; } = string.Empty;

    public Message? Message { get; set; }
    public ApplicationUser? User { get; set; }
}
