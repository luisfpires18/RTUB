namespace RTUB.Core.Entities;

/// <summary>
/// Represents a conversation between members
/// Supports both one-to-one and group conversations
/// </summary>
public class Conversation : BaseEntity
{
    /// <summary>
    /// Participants in the conversation (semicolon-separated user IDs)
    /// For one-to-one: "userId1;userId2"
    /// For groups: "userId1;userId2;userId3;..."
    /// </summary>
    public string Participants { get; set; } = string.Empty;

    /// <summary>
    /// Last message timestamp for sorting
    /// </summary>
    public DateTime LastMessageAt { get; set; }

    /// <summary>
    /// ID of the last message for preview
    /// </summary>
    public int? LastMessageId { get; set; }

    /// <summary>
    /// Optional conversation title (used for group conversations)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Whether this is a system conversation
    /// </summary>
    public bool IsSystemConversation { get; set; }

    /// <summary>
    /// Whether this conversation is archived/deleted
    /// </summary>
    public bool IsArchived { get; set; }
    
    /// <summary>
    /// Whether this is a group conversation (multiple participants)
    /// </summary>
    public bool IsGroup { get; set; }
    
    /// <summary>
    /// The user ID of the group creator (null for direct chats, "system" for auto-created groups)
    /// </summary>
    public string? CreatedByUserId { get; set; }
    
    /// <summary>
    /// Whether this is an announcement-only channel where only specific roles can send messages
    /// </summary>
    public bool IsAnnouncementOnly { get; set; }

    /// <summary>
    /// Navigation property for messages in this conversation
    /// </summary>
    public ICollection<Message> Messages { get; set; } = new List<Message>();

    /// <summary>
    /// Gets the list of participant user IDs
    /// </summary>
    public List<string> GetParticipantIds()
    {
        return string.IsNullOrEmpty(Participants)
            ? new List<string>()
            : Participants.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    /// <summary>
    /// Gets the other participant ID in a one-to-one conversation
    /// </summary>
    public string? GetOtherParticipantId(string currentUserId)
    {
        var participantIds = GetParticipantIds();
        return participantIds.FirstOrDefault(id => id != currentUserId);
    }

    /// <summary>
    /// Checks if a user is a participant in this conversation
    /// </summary>
    public bool HasParticipant(string userId)
    {
        return GetParticipantIds().Contains(userId);
    }
    
    /// <summary>
    /// Adds a participant to the conversation
    /// </summary>
    public void AddParticipant(string userId)
    {
        if (string.IsNullOrEmpty(userId) || HasParticipant(userId))
            return;
            
        Participants = string.IsNullOrEmpty(Participants) 
            ? userId 
            : $"{Participants};{userId}";
    }
    
    /// <summary>
    /// Removes a participant from the conversation
    /// </summary>
    public void RemoveParticipant(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return;
            
        var participants = GetParticipantIds();
        participants.Remove(userId);
        Participants = string.Join(";", participants);
    }
    
    /// <summary>
    /// Updates participants list (replaces all participants)
    /// </summary>
    public void SetParticipants(IEnumerable<string> userIds)
    {
        Participants = string.Join(";", userIds.Distinct());
    }
}
