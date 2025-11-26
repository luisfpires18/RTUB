using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Logistics List operations
/// Provides methods to manage lists in the logistics Kanban board system
/// </summary>
public interface ILogisticsListService
{
    /// <summary>
    /// Retrieves a logistics list by its unique identifier
    /// </summary>
    /// <param name="id">The unique identifier of the list</param>
    /// <returns>The logistics list if found; otherwise, null</returns>
    Task<LogisticsList?> GetListByIdAsync(int id);

    /// <summary>
    /// Retrieves all logistics lists ordered by position
    /// </summary>
    /// <returns>A collection of all logistics lists</returns>
    Task<IEnumerable<LogisticsList>> GetAllListsAsync();

    /// <summary>
    /// Retrieves all lists belonging to a specific board
    /// </summary>
    /// <param name="boardId">The unique identifier of the board</param>
    /// <returns>A collection of logistics lists ordered by position</returns>
    Task<IEnumerable<LogisticsList>> GetListsByBoardIdAsync(int boardId);

    /// <summary>
    /// Retrieves all lists with their associated cards for a specific board
    /// </summary>
    /// <param name="boardId">The unique identifier of the board</param>
    /// <returns>A collection of logistics lists with their cards included</returns>
    Task<IEnumerable<LogisticsList>> GetListsWithCardsByBoardIdAsync(int boardId);

    /// <summary>
    /// Creates a new logistics list
    /// </summary>
    /// <param name="name">The name of the list</param>
    /// <param name="boardId">The unique identifier of the board to add the list to</param>
    /// <param name="position">The position of the list on the board</param>
    /// <returns>The created logistics list</returns>
    Task<LogisticsList> CreateListAsync(string name, int boardId, int position);

    /// <summary>
    /// Updates the name of a logistics list
    /// </summary>
    /// <param name="id">The unique identifier of the list</param>
    /// <param name="name">The new name</param>
    /// <exception cref="InvalidOperationException">Thrown when the list is not found</exception>
    Task UpdateListAsync(int id, string name);

    /// <summary>
    /// Updates the position of a logistics list
    /// </summary>
    /// <param name="id">The unique identifier of the list</param>
    /// <param name="position">The new position</param>
    /// <exception cref="InvalidOperationException">Thrown when the list is not found</exception>
    Task UpdateListPositionAsync(int id, int position);

    /// <summary>
    /// Deletes a logistics list and all its associated cards
    /// </summary>
    /// <param name="id">The unique identifier of the list to delete</param>
    /// <exception cref="InvalidOperationException">Thrown when the list is not found</exception>
    Task DeleteListAsync(int id);

    /// <summary>
    /// Retrieves all logistics lists with their associated cards
    /// </summary>
    /// <returns>A collection of all logistics lists with their cards included</returns>
    Task<IEnumerable<LogisticsList>> GetListsWithCardsAsync();
}
