using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Logistics List operations
/// </summary>
public interface ILogisticsListService
{
    Task<LogisticsList?> GetListByIdAsync(int id);
    Task<IEnumerable<LogisticsList>> GetAllListsAsync();
    Task<IEnumerable<LogisticsList>> GetListsByBoardIdAsync(int boardId);
    Task<IEnumerable<LogisticsList>> GetListsWithCardsByBoardIdAsync(int boardId);
    Task<LogisticsList> CreateListAsync(string name, int boardId, int position);
    Task UpdateListAsync(int id, string name);
    Task UpdateListPositionAsync(int id, int position);
    Task DeleteListAsync(int id);
    Task<IEnumerable<LogisticsList>> GetListsWithCardsAsync();
}
