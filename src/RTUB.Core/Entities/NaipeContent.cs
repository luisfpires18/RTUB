using System.ComponentModel.DataAnnotations;
using RTUB.Core.Enums;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents educational content (video or image) for a specific instrument type
/// Used for the Naipes feature where users can access learning materials organized by instrument
/// </summary>
public class NaipeContent : BaseEntity
{
    [Required]
    public InstrumentType InstrumentType { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(2048)]
    public string Url { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string MimeType { get; set; } = string.Empty;

    [Required]
    public bool IsVideo { get; set; }

    [Required]
    public decimal SortOrder { get; set; }

    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;

    // Navigation properties
    public virtual ApplicationUser CreatedByUser { get; set; } = null!;
    public virtual ICollection<NaipeComment> Comments { get; set; } = new List<NaipeComment>();
    public virtual ICollection<NaipePlayCount> PlayCounts { get; set; } = new List<NaipePlayCount>();

    // Private constructor for EF Core
    private NaipeContent() { }

    // Factory method for creating content
    public static NaipeContent Create(InstrumentType instrumentType, string title, string url, string mimeType, bool isVideo, decimal sortOrder, string createdByUserId, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));
        if (title.Length > 200)
            throw new ArgumentException("Title cannot exceed 200 characters", nameof(title));
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL is required", nameof(url));
        if (string.IsNullOrWhiteSpace(mimeType))
            throw new ArgumentException("MIME type is required", nameof(mimeType));
        if (string.IsNullOrWhiteSpace(createdByUserId))
            throw new ArgumentException("Created by user ID is required", nameof(createdByUserId));
        if (!string.IsNullOrWhiteSpace(description) && description.Length > 1000)
            throw new ArgumentException("Description cannot exceed 1000 characters", nameof(description));

        return new NaipeContent
        {
            InstrumentType = instrumentType,
            Title = title,
            Description = description,
            Url = url,
            MimeType = mimeType,
            IsVideo = isVideo,
            SortOrder = sortOrder,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };
    }

    // Update method
    public void Update(string title, string? description, decimal sortOrder)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));
        if (title.Length > 200)
            throw new ArgumentException("Title cannot exceed 200 characters", nameof(title));
        if (!string.IsNullOrWhiteSpace(description) && description.Length > 1000)
            throw new ArgumentException("Description cannot exceed 1000 characters", nameof(description));

        Title = title;
        Description = description;
        SortOrder = sortOrder;
        UpdatedAt = DateTime.UtcNow;
    }
}
