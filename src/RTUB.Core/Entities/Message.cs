namespace RTUB.Core.Entities;

/// <summary>
/// Represents a message in a conversation
/// </summary>
public class Message : BaseEntity
{
    /// <summary>
    /// The ID of the conversation this message belongs to
    /// </summary>
    public int ConversationId { get; set; }

    /// <summary>
    /// ID of the sender (nullable for system messages)
    /// </summary>
    public string? SenderId { get; set; }

    /// <summary>
    /// The message body/content
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a system message
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>
    /// Semicolon-separated list of user IDs who have read this message
    /// </summary>
    public string ReadBy { get; set; } = string.Empty;

    /// <summary>
    /// Optional link/URL associated with the message (for system messages)
    /// </summary>
    public string? Link { get; set; }

    /// <summary>
    /// Navigation property to the conversation
    /// </summary>
    public Conversation? Conversation { get; set; }

    /// <summary>
    /// Navigation property to the sender
    /// </summary>
    public ApplicationUser? Sender { get; set; }

    /// <summary>
    /// Gets the list of user IDs who have read this message
    /// </summary>
    public List<string> GetReadByIds()
    {
        return string.IsNullOrEmpty(ReadBy)
            ? new List<string>()
            : ReadBy.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    /// <summary>
    /// Checks if a user has read this message
    /// </summary>
    public bool IsReadBy(string userId)
    {
        return GetReadByIds().Contains(userId);
    }

    /// <summary>
    /// Marks the message as read by a user
    /// </summary>
    public void MarkAsReadBy(string userId)
    {
        var readByIds = GetReadByIds();
        if (!readByIds.Contains(userId))
        {
            readByIds.Add(userId);
            ReadBy = string.Join(";", readByIds);
        }
    }

    /// <summary>
    /// Marks the message as unread by a user
    /// </summary>
    public void MarkAsUnreadBy(string userId)
    {
        var readByIds = GetReadByIds();
        if (readByIds.Contains(userId))
        {
            readByIds.Remove(userId);
            ReadBy = string.Join(";", readByIds);
        }
    }
}
