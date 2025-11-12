using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Logistics List operations
/// </summary>
public interface ILogisticsListService
{
    Task<LogisticsList?> GetListByIdAsync(int id);
    Task<IEnumerable<LogisticsList>> GetAllListsAsync();
    Task<LogisticsList> CreateListAsync(string name, int position);
    Task UpdateListAsync(int id, string name);
    Task UpdateListPositionAsync(int id, int position);
    Task DeleteListAsync(int id);
    Task<IEnumerable<LogisticsList>> GetListsWithCardsAsync();
}
