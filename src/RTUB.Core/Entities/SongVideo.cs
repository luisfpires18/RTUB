using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a video attached to a song
/// </summary>
public class SongVideo : BaseEntity
{
    [Required]
    public int SongId { get; set; }

    [Required]
    [MaxLength(2048)]
    public string Url { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Title { get; set; }

    [Required]
    [MaxLength(100)]
    public string MimeType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public int SortOrder { get; set; }

    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;

    // Navigation properties
    public virtual Song Song { get; set; } = null!;
    public virtual ApplicationUser CreatedByUser { get; set; } = null!;

    // Private constructor for EF Core
    private SongVideo() { }

    // Factory method for creating a video
    public static SongVideo CreateVideo(int songId, string url, string mimeType, long sizeBytes, string createdByUserId, string? title = null, int sortOrder = 0)
    {
        if (songId <= 0)
            throw new ArgumentException("Song ID must be positive", nameof(songId));
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL is required", nameof(url));
        if (string.IsNullOrWhiteSpace(mimeType))
            throw new ArgumentException("MIME type is required", nameof(mimeType));
        if (string.IsNullOrWhiteSpace(createdByUserId))
            throw new ArgumentException("Created by user ID is required", nameof(createdByUserId));
        if (sizeBytes < 0)
            throw new ArgumentException("Size bytes cannot be negative", nameof(sizeBytes));

        return new SongVideo
        {
            SongId = songId,
            Url = url,
            Title = title,
            MimeType = mimeType,
            SizeBytes = sizeBytes,
            CreatedByUserId = createdByUserId,
            SortOrder = sortOrder
        };
    }
}
