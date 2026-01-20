using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a comment on a bet with optional media attachment
/// </summary>
public class BetComment : BaseEntity
{
    [Required]
    public int BetId { get; set; }

    [Required]
    public string AuthorId { get; set; } = string.Empty;

    [MaxLength(1000, ErrorMessage = "O comentário não pode exceder 1000 caracteres")]
    public string Text { get; set; } = string.Empty;

    public string? MediaUrl { get; set; }

    [MaxLength(10)]
    public string? MediaType { get; set; } // "image" or "video"

    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public virtual Bet Bet { get; set; } = null!;
    public virtual ApplicationUser Author { get; set; } = null!;

    // Helper property to check if deleted
    public bool IsDeleted => DeletedAt.HasValue;

    // Private constructor for EF Core
    private BetComment() { }

    // Factory method
    public static BetComment Create(int betId, string authorId, string text, string? mediaUrl = null, string? mediaType = null)
    {
        if (betId <= 0)
            throw new ArgumentException("Bet ID must be greater than 0", nameof(betId));
        if (string.IsNullOrWhiteSpace(authorId))
            throw new ArgumentException("Author ID is required", nameof(authorId));
        // Only require text if no media is provided
        if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(mediaUrl))
            throw new ArgumentException("Text is required when no media is provided", nameof(text));
        if (!string.IsNullOrWhiteSpace(text) && text.Length > 1000)
            throw new ArgumentException("Text cannot exceed 1000 characters", nameof(text));
        if (!string.IsNullOrEmpty(mediaType) && mediaType != "image" && mediaType != "video")
            throw new ArgumentException("Media type must be 'image' or 'video'", nameof(mediaType));

        return new BetComment
        {
            BetId = betId,
            AuthorId = authorId,
            Text = text ?? string.Empty,
            MediaUrl = mediaUrl,
            MediaType = mediaType,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SoftDelete()
    {
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
