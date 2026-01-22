using RTUB.Core.Enums;

namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for naipe content with aggregated information
/// </summary>
public class NaipeContentDto
{
    public int Id { get; set; }
    public InstrumentType InstrumentType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Url { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public bool IsVideo { get; set; }
    public decimal SortOrder { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedByUserName { get; set; } = string.Empty;
    public string? UpdatedByUserName { get; set; }
    public DateTime CreatedAt { get; set; }
    public int PlayCount { get; set; }
    public int CommentCount { get; set; }
}
