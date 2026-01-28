using RTUB.Application.DTOs;
using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing bet comments
/// </summary>
public interface IBetCommentService
{
    /// <summary>
    /// Get all comments for a specific bet (excluding deleted ones)
    /// </summary>
    Task<List<BetCommentDto>> GetCommentsForBetAsync(int betId, string? currentUserId, bool isAdmin = false);

    /// <summary>
    /// Get comment count for a specific bet (excluding deleted ones)
    /// </summary>
    Task<int> GetCommentCountForBetAsync(int betId);

    /// <summary>
    /// Get comment counts for multiple bets (batch operation to avoid N+1 queries)
    /// </summary>
    Task<Dictionary<int, int>> GetCommentCountsByBetIdsAsync(IEnumerable<int> betIds);

    /// <summary>
    /// Add a new comment to a bet
    /// </summary>
    Task<BetCommentDto> AddCommentAsync(int betId, string authorId, string text, string? mediaUrl = null, string? mediaType = null);

    /// <summary>
    /// Delete a comment (soft delete)
    /// </summary>
    Task DeleteCommentAsync(int commentId, string userId, bool isAdmin);

    /// <summary>
    /// Check if a user can delete a specific comment
    /// </summary>
    bool CanDeleteComment(BetComment comment, string userId, bool isAdmin);
}
