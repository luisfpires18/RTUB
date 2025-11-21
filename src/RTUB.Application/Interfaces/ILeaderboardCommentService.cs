using RTUB.Application.DTOs;
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
