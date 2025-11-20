using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for LogisticsList entity
/// </summary>
public interface ILogisticsListRepository : IRepository<LogisticsList>
{
    /// <summary>
    /// Get all lists for a board with cards
    /// </summary>
    Task<IEnumerable<LogisticsList>> GetListsByBoardIdAsync(int boardId);
    
    /// <summary>
    /// Get list with all cards
    /// </summary>
    Task<LogisticsList?> GetListWithCardsAsync(int id);
}
