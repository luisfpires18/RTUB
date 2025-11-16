using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing leaderboard comments and likes
/// </summary>
public interface ILeaderboardCommentService
{
    /// <summary>
    /// Get all comments for a specific user (excluding deleted ones)
    /// </summary>
    Task<List<LeaderboardCommentDto>> GetCommentsForUserAsync(string targetUserId, string? currentUserId);
    
    /// <summary>
    /// Add a new comment to a user's leaderboard profile
    /// </summary>
    Task<LeaderboardCommentDto> AddCommentAsync(string targetUserId, string authorId, string text);
    
    /// <summary>
    /// Delete a comment (soft delete)
    /// </summary>
    Task DeleteCommentAsync(int commentId, string userId, bool isAdmin);
    
    /// <summary>
    /// Toggle a like on a comment (add if doesn't exist, remove if exists)
    /// </summary>
    Task<bool> ToggleLikeAsync(int commentId, string userId);
    
    /// <summary>
    /// Check if a user can delete a specific comment
    /// </summary>
    bool CanDeleteComment(LeaderboardComment comment, string userId, bool isAdmin);
}

/// <summary>
/// DTO for leaderboard comment with like information
/// </summary>
public class LeaderboardCommentDto
{
    public int Id { get; set; }
    public string TargetUserId { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorAvatarUrl { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int LikesCount { get; set; }
    public bool IsLikedByCurrentUser { get; set; }
    public bool CanDelete { get; set; }
    public List<string> LikedByNames { get; set; } = new();
}
