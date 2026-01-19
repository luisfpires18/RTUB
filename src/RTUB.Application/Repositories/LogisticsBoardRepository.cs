using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for LogisticsBoard entity
/// </summary>
public class LogisticsBoardRepository : Repository<LogisticsBoard>, ILogisticsBoardRepository
{
    public LogisticsBoardRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<LogisticsBoard?> GetBoardWithListsAndCardsAsync(int id)
    {
        return await _dbSet
            .Include(b => b.Event)
            .Include(b => b.Lists.OrderBy(l => l.Position))
            .ThenInclude(l => l.Cards.OrderBy(c => c.Position))
            .ThenInclude(c => c.Event)
            .Include(b => b.Lists)
            .ThenInclude(l => l.Cards)
            .ThenInclude(c => c.AssignedToUser)
            .Include(b => b.Lists)
            .ThenInclude(l => l.Cards)
            .ThenInclude(c => c.Assignments)
            .ThenInclude(a => a.User)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<LogisticsBoard?> GetBoardByEventIdAsync(int eventId)
    {
        return await _dbSet
            .Include(b => b.Event)
            .FirstOrDefaultAsync(b => b.EventId == eventId);
    }

    public async Task<IEnumerable<LogisticsBoard>> SearchBoardsAsync(string? searchTerm, int page, int pageSize)
    {
        return await Query()
            .Include(b => b.Event)
            .WhereIf(!string.IsNullOrWhiteSpace(searchTerm),
                b => b.Name.Contains(searchTerm!) ||
                     (b.Description != null && b.Description.Contains(searchTerm!)))
            .OrderByDescending(b => b.CreatedAt)
            .PaginateAsync(page, pageSize);
    }
}
