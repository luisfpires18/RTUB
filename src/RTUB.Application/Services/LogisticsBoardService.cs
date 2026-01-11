using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using Microsoft.EntityFrameworkCore;

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

    public LogisticsBoardService(
        ILogisticsBoardRepository boardRepository,
        ILogisticsListRepository listRepository,
        ILogisticsCardRepository cardRepository)
    {
        _boardRepository = boardRepository;
        _listRepository = listRepository;
        _cardRepository = cardRepository;
    }

    public async Task<LogisticsBoard?> GetBoardByIdAsync(int id)
    {
        return await _boardRepository.Query()
            .Include(b => b.Event)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<LogisticsBoard?> GetBoardWithListsAndCardsAsync(int id)
    {
        return await _boardRepository.GetBoardWithListsAndCardsAsync(id);
    }

    public async Task<IEnumerable<LogisticsBoard>> GetAllBoardsAsync()
    {
        return await _boardRepository.Query()
            .Include(b => b.Event)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

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

    public async Task<LogisticsBoard> CreateBoardAsync(string name, string description = "")
    {
        var board = LogisticsBoard.Create(name, description);
        return await _boardRepository.AddAsync(board);
    }

    public async Task UpdateBoardAsync(int id, string name, string description)
    {
        var board = await _boardRepository.GetByIdAsync(id);
        if (board == null)
            throw new InvalidOperationException($"Quadro com ID {id} não encontrado");

        board.UpdateDetails(name, description);
        await _boardRepository.UpdateAsync(board);
    }

    public async Task AssociateBoardWithEventAsync(int id, int? eventId)
    {
        var board = await _boardRepository.GetByIdAsync(id);
        if (board == null)
            throw new InvalidOperationException($"Quadro com ID {id} não encontrado");

        board.AssociateWithEvent(eventId);
        await _boardRepository.UpdateAsync(board);
    }

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

    public async Task MarkBoardAsCompletedAsync(int id)
    {
        var board = await _boardRepository.GetByIdAsync(id);
        if (board == null)
            throw new InvalidOperationException($"Quadro com ID {id} não encontrado");

        board.MarkAsCompleted();
        await _boardRepository.UpdateAsync(board);
    }

    public async Task MarkBoardAsNotCompletedAsync(int id)
    {
        var board = await _boardRepository.GetByIdAsync(id);
        if (board == null)
            throw new InvalidOperationException($"Quadro com ID {id} não encontrado");

        board.MarkAsNotCompleted();
        await _boardRepository.UpdateAsync(board);
    }
}
