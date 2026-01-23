using Microsoft.EntityFrameworkCore;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Logistics List service implementation
/// Contains business logic for logistics list operations
/// Refactored to use only repository pattern - no direct DbContext access
/// </summary>
public class LogisticsListService : ILogisticsListService
{
    private readonly ILogisticsListRepository _listRepository;
    private readonly ILogisticsCardRepository _cardRepository;

    /// <summary>
    /// Initializes a new instance of the LogisticsListService
    /// </summary>
    /// <param name="listRepository">Repository for logistics list operations</param>
    /// <param name="cardRepository">Repository for logistics card operations</param>
    public LogisticsListService(ILogisticsListRepository listRepository, ILogisticsCardRepository cardRepository)
    {
        _listRepository = listRepository;
        _cardRepository = cardRepository;
    }

    /// <summary>
    /// Gets a logistics list by its ID
    /// </summary>
    /// <param name="id">The ID of the list to retrieve</param>
    /// <returns>The logistics list if found, null otherwise</returns>
    public async Task<LogisticsList?> GetListByIdAsync(int id)
    {
        return await _listRepository.GetByIdAsync(id);
    }

    /// <summary>
    /// Gets all logistics lists ordered by position
    /// </summary>
    /// <returns>Collection of all logistics lists</returns>
    public async Task<IEnumerable<LogisticsList>> GetAllListsAsync()
    {
        return await _listRepository.Query()
            .AsNoTracking()
            .OrderBy(l => l.Position)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all logistics lists with their cards, events, and assigned users
    /// Includes related data to avoid N+1 query issues
    /// </summary>
    /// <returns>Collection of logistics lists with related data</returns>
    public async Task<IEnumerable<LogisticsList>> GetListsWithCardsAsync()
    {
        return await _listRepository.Query()
            .AsNoTracking()
            .Include(l => l.Cards.OrderBy(c => c.Position))
                .ThenInclude(c => c.Event)
            .Include(l => l.Cards.OrderBy(c => c.Position))
                .ThenInclude(c => c.AssignedToUser)
            .OrderBy(l => l.Position)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all logistics lists for a specific board
    /// </summary>
    /// <param name="boardId">The ID of the board</param>
    /// <returns>Collection of logistics lists for the specified board</returns>
    public async Task<IEnumerable<LogisticsList>> GetListsByBoardIdAsync(int boardId)
    {
        return await _listRepository.GetListsByBoardIdAsync(boardId);
    }

    /// <summary>
    /// Gets all logistics lists for a specific board with their cards, events, and assigned users
    /// Includes related data to avoid N+1 query issues
    /// </summary>
    /// <param name="boardId">The ID of the board</param>
    /// <returns>Collection of logistics lists with related data for the specified board</returns>
    public async Task<IEnumerable<LogisticsList>> GetListsWithCardsByBoardIdAsync(int boardId)
    {
        return await _listRepository.Query()
            .AsNoTracking()
            .Include(l => l.Cards.OrderBy(c => c.Position))
                .ThenInclude(c => c.Event)
            .Include(l => l.Cards.OrderBy(c => c.Position))
                .ThenInclude(c => c.AssignedToUser)
            .Where(l => l.BoardId == boardId)
            .OrderBy(l => l.Position)
            .ToListAsync();
    }

    /// <summary>
    /// Creates a new logistics list
    /// </summary>
    /// <param name="name">The name of the list</param>
    /// <param name="boardId">The ID of the board this list belongs to</param>
    /// <param name="position">The position of the list within the board</param>
    /// <returns>The created logistics list</returns>
    public async Task<LogisticsList> CreateListAsync(string name, int boardId, int position)
    {
        var list = LogisticsList.Create(name, boardId, position);
        return await _listRepository.AddAsync(list);
    }

    /// <summary>
    /// Updates the name of a logistics list
    /// </summary>
    /// <param name="id">The ID of the list to update</param>
    /// <param name="name">The new name for the list</param>
    /// <exception cref="InvalidOperationException">Thrown when the list is not found</exception>
    public async Task UpdateListAsync(int id, string name)
    {
        var list = await _listRepository.GetByIdAsync(id);
        if (list == null)
            throw new InvalidOperationException($"Lista com ID {id} não encontrada");

        list.UpdateName(name);
        await _listRepository.UpdateAsync(list);
    }

    /// <summary>
    /// Updates the position of a logistics list within its board
    /// </summary>
    /// <param name="id">The ID of the list to update</param>
    /// <param name="position">The new position for the list</param>
    /// <exception cref="InvalidOperationException">Thrown when the list is not found</exception>
    public async Task UpdateListPositionAsync(int id, int position)
    {
        var list = await _listRepository.GetByIdAsync(id);
        if (list == null)
            throw new InvalidOperationException($"Lista com ID {id} não encontrada");

        list.UpdatePosition(position);
        await _listRepository.UpdateAsync(list);
    }

    /// <summary>
    /// Deletes a logistics list and all its cards
    /// </summary>
    /// <param name="id">The ID of the list to delete</param>
    /// <exception cref="InvalidOperationException">Thrown when the list is not found</exception>
    public async Task DeleteListAsync(int id)
    {
        var list = await _listRepository.Query()
            .Include(l => l.Cards)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (list == null)
            throw new InvalidOperationException($"Lista com ID {id} não encontrada");

        // Delete all cards in the list first
        // Create a copy of the collection to avoid "collection modified during enumeration" exception
        var cards = list.Cards.ToList();
        foreach (var card in cards)
        {
            await _cardRepository.DeleteAsync(card);
        }

        await _listRepository.DeleteAsync(list);
    }
}
