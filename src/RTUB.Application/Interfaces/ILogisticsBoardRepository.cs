using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for LogisticsBoard entity
/// </summary>
public interface ILogisticsBoardRepository : IRepository<LogisticsBoard>
{
    /// <summary>
    /// Get board with full nested lists and cards
    /// </summary>
    Task<LogisticsBoard?> GetBoardWithListsAndCardsAsync(int id);

    /// <summary>
    /// Get board by event ID
    /// </summary>
    Task<LogisticsBoard?> GetBoardByEventIdAsync(int eventId);

    /// <summary>
    /// Search boards with optional filter
    /// </summary>
    Task<IEnumerable<LogisticsBoard>> SearchBoardsAsync(string? searchTerm, int page, int pageSize);
}
