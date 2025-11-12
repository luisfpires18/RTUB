using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;

namespace RTUB.Application.Services;

/// <summary>
/// Logistics Board service implementation
/// Contains business logic for logistics board operations
/// </summary>
public class LogisticsBoardService : ILogisticsBoardService
{
    private readonly ApplicationDbContext _context;

    public LogisticsBoardService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<LogisticsBoard?> GetBoardByIdAsync(int id)
    {
        return await _context.LogisticsBoards
            .Include(b => b.Event)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<LogisticsBoard?> GetBoardWithListsAndCardsAsync(int id)
    {
        return await _context.LogisticsBoards
            .Include(b => b.Event)
            .Include(b => b.Lists.OrderBy(l => l.Position))
            .ThenInclude(l => l.Cards.OrderBy(c => c.Position))
            .ThenInclude(c => c.Event)
            .Include(b => b.Lists)
            .ThenInclude(l => l.Cards)
            .ThenInclude(c => c.AssignedToUser)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<IEnumerable<LogisticsBoard>> GetAllBoardsAsync()
    {
        return await _context.LogisticsBoards
            .Include(b => b.Event)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    public async Task<(IEnumerable<LogisticsBoard> Boards, int TotalCount)> GetBoardsPagedAsync(int page, int pageSize, string? searchTerm = null)
    {
        var query = _context.LogisticsBoards
            .Include(b => b.Event)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(b => b.Name.Contains(searchTerm) || b.Description.Contains(searchTerm));
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
        _context.LogisticsBoards.Add(board);
        await _context.SaveChangesAsync();
        return board;
    }

    public async Task UpdateBoardAsync(int id, string name, string description)
    {
        var board = await _context.LogisticsBoards.FindAsync(id);
        if (board == null)
            throw new InvalidOperationException($"Quadro com ID {id} não encontrado");

        board.UpdateDetails(name, description);
        await _context.SaveChangesAsync();
    }

    public async Task AssociateBoardWithEventAsync(int id, int? eventId)
    {
        var board = await _context.LogisticsBoards.FindAsync(id);
        if (board == null)
            throw new InvalidOperationException($"Quadro com ID {id} não encontrado");

        board.AssociateWithEvent(eventId);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteBoardAsync(int id)
    {
        var board = await _context.LogisticsBoards
            .Include(b => b.Lists)
            .ThenInclude(l => l.Cards)
            .FirstOrDefaultAsync(b => b.Id == id);
            
        if (board == null)
            throw new InvalidOperationException($"Quadro com ID {id} não encontrado");

        // Delete all cards in all lists, then all lists, then the board
        foreach (var list in board.Lists)
        {
            _context.LogisticsCards.RemoveRange(list.Cards);
        }
        _context.LogisticsLists.RemoveRange(board.Lists);
        _context.LogisticsBoards.Remove(board);
        await _context.SaveChangesAsync();
    }
}
