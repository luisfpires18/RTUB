using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Logistics Board operations
/// </summary>
public interface ILogisticsBoardService
{
    Task<LogisticsBoard?> GetBoardByIdAsync(int id);
    Task<LogisticsBoard?> GetBoardWithListsAndCardsAsync(int id);
    Task<IEnumerable<LogisticsBoard>> GetAllBoardsAsync();
    Task<(IEnumerable<LogisticsBoard> Boards, int TotalCount)> GetBoardsPagedAsync(int page, int pageSize, string? searchTerm = null);
    Task<LogisticsBoard> CreateBoardAsync(string name, string description = "");
    Task UpdateBoardAsync(int id, string name, string description);
    Task AssociateBoardWithEventAsync(int id, int? eventId);
    Task DeleteBoardAsync(int id);
}
