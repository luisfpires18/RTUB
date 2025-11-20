using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;
using Microsoft.EntityFrameworkCore;

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

    public LogisticsListService(ILogisticsListRepository listRepository, ILogisticsCardRepository cardRepository)
    {
        _listRepository = listRepository;
        _cardRepository = cardRepository;
    }

    public async Task<LogisticsList?> GetListByIdAsync(int id)
    {
        return await _listRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<LogisticsList>> GetAllListsAsync()
    {
        return await _listRepository.Query()
            .OrderBy(l => l.Position)
            .ToListAsync();
    }

    public async Task<IEnumerable<LogisticsList>> GetListsWithCardsAsync()
    {
        return await _listRepository.Query()
            .Include(l => l.Cards.OrderBy(c => c.Position))
            .ThenInclude(c => c.Event)
            .Include(l => l.Cards)
            .ThenInclude(c => c.AssignedToUser)
            .OrderBy(l => l.Position)
            .ToListAsync();
    }

    public async Task<IEnumerable<LogisticsList>> GetListsByBoardIdAsync(int boardId)
    {
        return await _listRepository.GetListsByBoardIdAsync(boardId);
    }

    public async Task<IEnumerable<LogisticsList>> GetListsWithCardsByBoardIdAsync(int boardId)
    {
        return await _listRepository.Query()
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
        return await _listRepository.AddAsync(list);
    }

    public async Task UpdateListAsync(int id, string name)
    {
        var list = await _listRepository.GetByIdAsync(id);
        if (list == null)
            throw new InvalidOperationException($"Lista com ID {id} não encontrada");

        list.UpdateName(name);
        await _listRepository.UpdateAsync(list);
    }

    public async Task UpdateListPositionAsync(int id, int position)
    {
        var list = await _listRepository.GetByIdAsync(id);
        if (list == null)
            throw new InvalidOperationException($"Lista com ID {id} não encontrada");

        list.UpdatePosition(position);
        await _listRepository.UpdateAsync(list);
    }

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
