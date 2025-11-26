using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a like on a leaderboard comment
/// </summary>
public class LeaderboardCommentLike : BaseEntity
{
    [Required]
    public int CommentId { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    // Navigation properties
    public virtual LeaderboardComment Comment { get; set; } = null!;
    public virtual ApplicationUser User { get; set; } = null!;

    // Private constructor for EF Core
    private LeaderboardCommentLike() { }

    // Factory method
    public static LeaderboardCommentLike Create(int commentId, string userId)
    {
        if (commentId <= 0)
            throw new ArgumentException("Comment ID must be positive", nameof(commentId));
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        return new LeaderboardCommentLike
        {
            CommentId = commentId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
