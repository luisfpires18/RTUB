using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents an image attached to a comment
/// </summary>
public class CommentImage : BaseEntity
{
    [Required]
    public int CommentId { get; set; }

    [Required]
    [MaxLength(2048)]
    public string Url { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string MimeType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public int SortOrder { get; set; }

    // Navigation properties
    public virtual Comment Comment { get; set; } = null!;

    // Private constructor for EF Core
    private CommentImage() { }

    // Factory method
    public static CommentImage Create(int commentId, string url, string mimeType, long sizeBytes, int sortOrder = 0)
    {
        if (commentId <= 0)
            throw new ArgumentException("Comment ID must be positive", nameof(commentId));
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL is required", nameof(url));
        if (string.IsNullOrWhiteSpace(mimeType))
            throw new ArgumentException("MIME type is required", nameof(mimeType));

        return new CommentImage
        {
            CommentId = commentId,
            Url = url,
            MimeType = mimeType,
            SizeBytes = sizeBytes,
            SortOrder = sortOrder,
            CreatedAt = DateTime.UtcNow
        };
    }
}
