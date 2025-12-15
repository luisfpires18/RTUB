using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a video attached to an event
/// </summary>
public class EventVideo : BaseEntity
{
    [Required]
    public int EventId { get; set; }

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
    public virtual Event Event { get; set; } = null!;
    public virtual ApplicationUser CreatedByUser { get; set; } = null!;

    // Private constructor for EF Core
    private EventVideo() { }

    // Factory method for creating a video
    public static EventVideo CreateVideo(int eventId, string url, string mimeType, long sizeBytes, string createdByUserId, string? title = null, int sortOrder = 0)
    {
        if (eventId <= 0)
            throw new ArgumentException("Event ID must be positive", nameof(eventId));
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL is required", nameof(url));
        if (string.IsNullOrWhiteSpace(mimeType))
            throw new ArgumentException("MIME type is required", nameof(mimeType));
        if (string.IsNullOrWhiteSpace(createdByUserId))
            throw new ArgumentException("Created by user ID is required", nameof(createdByUserId));
        if (sizeBytes < 0)
            throw new ArgumentException("Size bytes cannot be negative", nameof(sizeBytes));

        return new EventVideo
        {
            EventId = eventId,
            Url = url,
            Title = title,
            MimeType = mimeType,
            SizeBytes = sizeBytes,
            CreatedByUserId = createdByUserId,
            SortOrder = sortOrder
        };
    }

    // Business method to update the title
    public void UpdateTitle(string? title)
    {
        if (title != null && title.Length > 200)
            throw new ArgumentException("Title cannot exceed 200 characters", nameof(title));
        
        Title = title;
    }
}
