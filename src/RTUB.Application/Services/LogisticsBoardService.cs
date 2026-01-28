using Microsoft.EntityFrameworkCore;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Logistics Board service implementation
/// Contains business logic for logistics board operations
/// Refactored to use only repository pattern - no direct DbContext access
/// </summary>
public class LogisticsBoardService : ILogisticsBoardService
{
    private readonly ILogisticsBoardRepository _boardRepository;
    private readonly ILogisticsListRepository _listRepository;
    private readonly ILogisticsCardRepository _cardRepository;

    /// <summary>
    /// Initializes a new instance of the LogisticsBoardService
    /// </summary>
    /// <param name="boardRepository">Repository for logistics board operations</param>
    /// <param name="listRepository">Repository for logistics list operations</param>
    /// <param name="cardRepository">Repository for logistics card operations</param>
    public LogisticsBoardService(
        ILogisticsBoardRepository boardRepository,
        ILogisticsListRepository listRepository,
        ILogisticsCardRepository cardRepository)
    {
        _boardRepository = boardRepository;
        _listRepository = listRepository;
        _cardRepository = cardRepository;
    }

    /// <summary>
    /// Gets a logistics board by its ID with event information
    /// </summary>
    /// <param name="id">The ID of the board to retrieve</param>
    /// <returns>The logistics board if found, null otherwise</returns>
    public async Task<LogisticsBoard?> GetBoardByIdAsync(int id)
    {
        return await _boardRepository.Query()
            .Include(b => b.Event)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    /// <summary>
    /// Gets a logistics board with all its lists and cards
    /// </summary>
    /// <param name="id">The ID of the board to retrieve</param>
    /// <returns>The logistics board with lists and cards if found, null otherwise</returns>
    public async Task<LogisticsBoard?> GetBoardWithListsAndCardsAsync(int id)
    {
        return await _boardRepository.GetBoardWithListsAndCardsAsync(id);
    }

    /// <summary>
    /// Gets all logistics boards ordered by creation date descending
    /// </summary>
    /// <returns>Collection of all logistics boards</returns>
    public async Task<IEnumerable<LogisticsBoard>> GetAllBoardsAsync()
    {
        return await _boardRepository.Query()
            .Include(b => b.Event)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Gets logistics boards with pagination, search, and filter support
    /// </summary>
    /// <param name="page">The page number (1-based)</param>
    /// <param name="pageSize">The number of items per page</param>
    /// <param name="searchTerm">Optional search term to filter by name or description</param>
    /// <param name="isCompleted">Optional filter for completed status</param>
    /// <returns>A tuple containing the boards for the page and the total count</returns>
    public async Task<(IEnumerable<LogisticsBoard> Boards, int TotalCount)> GetBoardsPagedAsync(int page, int pageSize, string? searchTerm = null, bool? isCompleted = null)
    {
        var query = _boardRepository.Query()
            .Include(b => b.Event)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(b => b.Name.Contains(searchTerm!) || b.Description.Contains(searchTerm!));
        }

        if (isCompleted.HasValue)
        {
            query = query.Where(b => b.IsCompleted == isCompleted.Value);
        }

        var totalCount = await query.CountAsync();

        var boards = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (boards, totalCount);
    }

    /// <summary>
    /// Creates a new logistics board
    /// </summary>
    /// <param name="name">The name of the board</param>
    /// <param name="description">Optional description of the board</param>
    /// <returns>The created logistics board</returns>
    public async Task<LogisticsBoard> CreateBoardAsync(string name, string description = "")
    {
        var board = LogisticsBoard.Create(name, description);
        return await _boardRepository.AddAsync(board);
    }

    /// <summary>
    /// Updates the name and description of a logistics board
    /// </summary>
    /// <param name="id">The ID of the board to update</param>
    /// <param name="name">The new name for the board</param>
    /// <param name="description">The new description for the board</param>
    /// <exception cref="InvalidOperationException">Thrown when the board is not found</exception>
    public async Task UpdateBoardAsync(int id, string name, string description)
    {
        var board = await _boardRepository.GetByIdOrThrowAsync(id);

        board.UpdateDetails(name, description);
        await _boardRepository.UpdateAsync(board);
    }

    /// <summary>
    /// Associates a logistics board with an event
    /// </summary>
    /// <param name="id">The ID of the board</param>
    /// <param name="eventId">The ID of the event to associate (null to remove association)</param>
    /// <exception cref="InvalidOperationException">Thrown when the board is not found</exception>
    public async Task AssociateBoardWithEventAsync(int id, int? eventId)
    {
        var board = await _boardRepository.GetByIdOrThrowAsync(id);

        board.AssociateWithEvent(eventId);
        await _boardRepository.UpdateAsync(board);
    }

    /// <summary>
    /// Deletes a logistics board and all its lists and cards
    /// </summary>
    /// <param name="id">The ID of the board to delete</param>
    /// <exception cref="InvalidOperationException">Thrown when the board is not found</exception>
    public async Task DeleteBoardAsync(int id)
    {
        var board = await _boardRepository.Query()
            .Include(b => b.Lists)
            .ThenInclude(l => l.Cards)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (board == null)
            throw new InvalidOperationException($"Quadro com ID {id} não encontrado");

        // Delete all cards in all lists first, then lists, then board
        foreach (var list in board.Lists)
        {
            foreach (var card in list.Cards)
            {
                await _cardRepository.DeleteAsync(card);
            }
            await _listRepository.DeleteAsync(list);
        }

        await _boardRepository.DeleteAsync(board);
    }

    /// <summary>
    /// Marks a logistics board as completed
    /// </summary>
    /// <param name="id">The ID of the board to mark as completed</param>
    /// <exception cref="InvalidOperationException">Thrown when the board is not found</exception>
    public async Task MarkBoardAsCompletedAsync(int id)
    {
        var board = await _boardRepository.GetByIdOrThrowAsync(id);

        board.MarkAsCompleted();
        await _boardRepository.UpdateAsync(board);
    }

    /// <summary>
    /// Marks a logistics board as not completed
    /// </summary>
    /// <param name="id">The ID of the board to mark as not completed</param>
    /// <exception cref="InvalidOperationException">Thrown when the board is not found</exception>
    public async Task MarkBoardAsNotCompletedAsync(int id)
    {
        var board = await _boardRepository.GetByIdOrThrowAsync(id);

        board.MarkAsNotCompleted();
        await _boardRepository.UpdateAsync(board);
    }
}
