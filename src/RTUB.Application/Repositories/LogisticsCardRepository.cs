using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for LogisticsCard entity
/// </summary>
public class LogisticsCardRepository : Repository<LogisticsCard>, ILogisticsCardRepository
{
    public LogisticsCardRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<LogisticsCard>> GetCardsByListIdAsync(int listId)
    {
        return await _dbSet
            .Include(c => c.AssignedToUser)
            .Include(c => c.Event)
            .Where(c => c.ListId == listId)
            .OrderBy(c => c.Position)
            .ToListAsync();
    }

    public async Task<IEnumerable<LogisticsCard>> GetCardsByUserIdAsync(string userId)
    {
        return await _dbSet
            .Include(c => c.List)
            .ThenInclude(l => l.Board)
            .ThenInclude(b => b.Event)
            .Include(c => c.Event)
            .Where(c => c.AssignedToUserId == userId)
            .OrderBy(c => c.DueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<LogisticsCard>> SearchCardsAsync(string? searchTerm, int page, int pageSize)
    {
        return await Query()
            .Include(c => c.AssignedToUser)
            .Include(c => c.List)
            .ThenInclude(l => l.Board)
            .WhereIf(!string.IsNullOrWhiteSpace(searchTerm),
                c => c.Title.Contains(searchTerm!) ||
                     (c.Description != null && c.Description.Contains(searchTerm!)))
            .OrderBy(c => c.Position)
            .PaginateAsync(page, pageSize);
    }
}
