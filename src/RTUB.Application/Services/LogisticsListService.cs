using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;

namespace RTUB.Application.Services;

/// <summary>
/// Logistics List service implementation
/// Contains business logic for logistics list operations
/// </summary>
public class LogisticsListService : ILogisticsListService
{
    private readonly ApplicationDbContext _context;

    public LogisticsListService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<LogisticsList?> GetListByIdAsync(int id)
    {
        return await _context.LogisticsLists.FindAsync(id);
    }

    public async Task<IEnumerable<LogisticsList>> GetAllListsAsync()
    {
        return await _context.LogisticsLists
            .OrderBy(l => l.Position)
            .ToListAsync();
    }

    public async Task<IEnumerable<LogisticsList>> GetListsWithCardsAsync()
    {
        return await _context.LogisticsLists
            .Include(l => l.Cards.OrderBy(c => c.Position))
            .ThenInclude(c => c.Event)
            .Include(l => l.Cards)
            .ThenInclude(c => c.AssignedToUser)
            .OrderBy(l => l.Position)
            .ToListAsync();
    }

    public async Task<IEnumerable<LogisticsList>> GetListsByBoardIdAsync(int boardId)
    {
        return await _context.LogisticsLists
            .Where(l => l.BoardId == boardId)
            .OrderBy(l => l.Position)
            .ToListAsync();
    }

    public async Task<IEnumerable<LogisticsList>> GetListsWithCardsByBoardIdAsync(int boardId)
    {
        return await _context.LogisticsLists
            .Include(l => l.Cards.OrderBy(c => c.Position))
            .ThenInclude(c => c.Event)
            .Include(l => l.Cards)
            .ThenInclude(c => c.AssignedToUser)
            .Where(l => l.BoardId == boardId)
            .OrderBy(l => l.Position)
            .ToListAsync();
    }

    public async Task<LogisticsList> CreateListAsync(string name, int boardId, int position)
    {
        var list = LogisticsList.Create(name, boardId, position);
        _context.LogisticsLists.Add(list);
        await _context.SaveChangesAsync();
        return list;
    }

    public async Task UpdateListAsync(int id, string name)
    {
        var list = await _context.LogisticsLists.FindAsync(id);
        if (list == null)
            throw new InvalidOperationException($"Lista com ID {id} não encontrada");

        list.UpdateName(name);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateListPositionAsync(int id, int position)
    {
        var list = await _context.LogisticsLists.FindAsync(id);
        if (list == null)
            throw new InvalidOperationException($"Lista com ID {id} não encontrada");

        list.UpdatePosition(position);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteListAsync(int id)
    {
        var list = await _context.LogisticsLists
            .Include(l => l.Cards)
            .FirstOrDefaultAsync(l => l.Id == id);
            
        if (list == null)
            throw new InvalidOperationException($"Lista com ID {id} não encontrada");

        // Delete all cards in the list first
        _context.LogisticsCards.RemoveRange(list.Cards);
        _context.LogisticsLists.Remove(list);
        await _context.SaveChangesAsync();
    }
}
