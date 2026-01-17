namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for naipe comment
/// </summary>
public class NaipeCommentDto
{
    public int Id { get; set; }
    public int NaipeContentId { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorAvatarUrl { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool CanDelete { get; set; }
}
