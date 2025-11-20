using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for LogisticsCard entity
/// </summary>
public interface ILogisticsCardRepository : IRepository<LogisticsCard>
{
    /// <summary>
    /// Get all cards for a list
    /// </summary>
    Task<IEnumerable<LogisticsCard>> GetCardsByListIdAsync(int listId);
    
    /// <summary>
    /// Get cards assigned to a user
    /// </summary>
    Task<IEnumerable<LogisticsCard>> GetCardsByUserIdAsync(string userId);
    
    /// <summary>
    /// Search cards across all boards
    /// </summary>
    Task<IEnumerable<LogisticsCard>> SearchCardsAsync(string? searchTerm, int page, int pageSize);
}
