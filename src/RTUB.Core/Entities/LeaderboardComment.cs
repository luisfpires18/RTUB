using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a comment on a user's leaderboard profile
/// </summary>
public class LeaderboardComment : BaseEntity
{
    [Required]
    public string TargetUserId { get; set; } = string.Empty;

    [Required]
    public string AuthorId { get; set; } = string.Empty;

    [Required]
    [MinLength(1, ErrorMessage = "O comentário deve ter pelo menos 1 caractere")]
    [MaxLength(1000, ErrorMessage = "O comentário não pode exceder 1000 caracteres")]
    public string Text { get; set; } = string.Empty;

    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public virtual ApplicationUser TargetUser { get; set; } = null!;
    public virtual ApplicationUser Author { get; set; } = null!;
    public virtual ICollection<LeaderboardCommentLike> Likes { get; set; } = new List<LeaderboardCommentLike>();

    // Helper property to check if deleted
    public bool IsDeleted => DeletedAt.HasValue;

    // Private constructor for EF Core
    private LeaderboardComment() { }

    // Factory method
    public static LeaderboardComment Create(string targetUserId, string authorId, string text)
    {
        if (string.IsNullOrWhiteSpace(targetUserId))
            throw new ArgumentException("Target User ID is required", nameof(targetUserId));
        if (string.IsNullOrWhiteSpace(authorId))
            throw new ArgumentException("Author ID is required", nameof(authorId));
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text is required", nameof(text));
        if (text.Length > 1000)
            throw new ArgumentException("Text cannot exceed 1000 characters", nameof(text));

        return new LeaderboardComment
        {
            TargetUserId = targetUserId,
            AuthorId = authorId,
            Text = text,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SoftDelete()
    {
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
