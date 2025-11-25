namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for message information
/// </summary>
public class MessageDto
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public string? SenderId { get; set; }
    public string? SenderName { get; set; }
    public string? SenderNickname { get; set; }
    public string? SenderAvatar { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public string? Link { get; set; }
}
