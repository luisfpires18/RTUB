using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for LeaderboardComment entity
/// Provides data access operations for leaderboard comments
/// </summary>
public interface ILeaderboardCommentRepository : IRepository<LeaderboardComment>
{
    /// <summary>
    /// Gets comments for a specific user with author and likes
    /// </summary>
    Task<IEnumerable<LeaderboardComment>> GetCommentsForUserAsync(string targetUserId);

    /// <summary>
    /// Gets comment by ID with author and likes
    /// </summary>
    Task<LeaderboardComment?> GetByIdWithDetailsAsync(int id);

    /// <summary>
    /// Toggles a like on a comment within a single tracked DbContext.
    /// Returns true if liked, false if unliked, null if comment not found.
    /// </summary>
    Task<bool?> ToggleLikeAsync(int commentId, string userId);
}
