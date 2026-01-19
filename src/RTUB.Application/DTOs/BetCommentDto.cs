namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for BetComment with author details and media
/// </summary>
public class BetCommentDto
{
    public int Id { get; set; }
    public int BetId { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorAvatarUrl { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? MediaUrl { get; set; }
    public string? MediaType { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool CanDelete { get; set; }
}
