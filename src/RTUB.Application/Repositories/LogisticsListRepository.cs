using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for LogisticsList entity
/// </summary>
public class LogisticsListRepository : Repository<LogisticsList>, ILogisticsListRepository
{
    public LogisticsListRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<LogisticsList>> GetListsByBoardIdAsync(int boardId)
    {
        return await _dbSet
            .Where(l => l.BoardId == boardId)
            .OrderBy(l => l.Position)
            .ToListAsync();
    }

    public async Task<LogisticsList?> GetListWithCardsAsync(int id)
    {
        return await _dbSet
            .Include(l => l.Cards.OrderBy(c => c.Position))
            .ThenInclude(c => c.AssignedToUser)
            .Include(l => l.Cards)
            .ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(l => l.Id == id);
    }
}
