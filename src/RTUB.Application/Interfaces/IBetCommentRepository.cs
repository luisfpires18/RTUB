using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for BetComment entity
/// Provides data access operations for bet comments
/// </summary>
public interface IBetCommentRepository : IRepository<BetComment>
{
    /// <summary>
    /// Gets comments for a specific bet with author
    /// </summary>
    Task<IEnumerable<BetComment>> GetCommentsForBetAsync(int betId);

    /// <summary>
    /// Gets comment by ID with author
    /// </summary>
    Task<BetComment?> GetByIdWithDetailsAsync(int id);

    /// <summary>
    /// Deletes all comments for a specific bet
    /// </summary>
    /// <param name="betId">Bet ID</param>
    Task DeleteByBetIdAsync(int betId);
}
