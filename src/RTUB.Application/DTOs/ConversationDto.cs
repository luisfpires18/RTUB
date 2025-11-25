namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for conversation information
/// </summary>
public class ConversationDto
{
    public int Id { get; set; }
    public List<string> ParticipantIds { get; set; } = new();
    public DateTime LastMessageAt { get; set; }
    public string? LastMessagePreview { get; set; }
    public string? LastMessageSenderId { get; set; }
    public int UnreadCount { get; set; }
    public string? Title { get; set; }
    public bool IsSystemConversation { get; set; }
    public bool IsGroup { get; set; }
    public string? CreatedByUserId { get; set; }
    
    /// <summary>
    /// Whether this is an announcement-only channel where only specific roles can send messages
    /// </summary>
    public bool IsAnnouncementOnly { get; set; }
    
    /// <summary>
    /// Whether the current user can send messages in this conversation
    /// </summary>
    public bool CanSendMessage { get; set; }
    
    /// <summary>
    /// Whether the current user can delete this conversation
    /// </summary>
    public bool CanDelete { get; set; }
    
    // Per-user conversation settings
    public bool IsMuted { get; set; }
    public bool IsPinned { get; set; }
    
    // For display purposes (1:1 conversations)
    public string? OtherParticipantId { get; set; }
    public string? OtherParticipantName { get; set; }
    public string? OtherParticipantNickname { get; set; }
    public string? OtherParticipantAvatar { get; set; }
    
    // For group conversations - participant info
    public List<GroupParticipantDto> GroupParticipants { get; set; } = new();
}
