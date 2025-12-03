using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents media (image or video) attached to a post
/// </summary>
public class PostMedia : BaseEntity
{
    [Required]
    public int PostId { get; set; }

    [Required]
    [MaxLength(2048)]
    public string Url { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string MediaType { get; set; } = string.Empty; // "Image" or "Video"

    [Required]
    [MaxLength(100)]
    public string MimeType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public int SortOrder { get; set; }

    // Navigation properties
    public virtual Post Post { get; set; } = null!;

    // Private constructor for EF Core
    private PostMedia() { }

    // Factory method for images
    public static PostMedia CreateImage(int postId, string url, string mimeType, long sizeBytes, int sortOrder = 0)
    {
        if (postId <= 0)
            throw new ArgumentException("Post ID must be positive", nameof(postId));
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL is required", nameof(url));
        if (string.IsNullOrWhiteSpace(mimeType))
            throw new ArgumentException("MIME type is required", nameof(mimeType));

        return new PostMedia
        {
            PostId = postId,
            Url = url,
            MediaType = "Image",
            MimeType = mimeType,
            SizeBytes = sizeBytes,
            SortOrder = sortOrder,
            CreatedAt = DateTime.UtcNow
        };
    }

    // Factory method for videos
    public static PostMedia CreateVideo(int postId, string url, string mimeType, long sizeBytes, int sortOrder = 0)
    {
        if (postId <= 0)
            throw new ArgumentException("Post ID must be positive", nameof(postId));
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL is required", nameof(url));
        if (string.IsNullOrWhiteSpace(mimeType))
            throw new ArgumentException("MIME type is required", nameof(mimeType));

        return new PostMedia
        {
            PostId = postId,
            Url = url,
            MediaType = "Video",
            MimeType = mimeType,
            SizeBytes = sizeBytes,
            SortOrder = sortOrder,
            CreatedAt = DateTime.UtcNow
        };
    }
}
