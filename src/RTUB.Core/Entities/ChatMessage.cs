namespace RTUB.Core.Entities;

/// <summary>
/// Represents a chat message in an event enrollment page
/// </summary>
public class ChatMessage : BaseEntity
{
    public int EventId { get; set; }
    public Event? Event { get; set; }
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    
    public string Message { get; set; } = string.Empty;
    
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
