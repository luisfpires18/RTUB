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
    public LogisticsListRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<LogisticsList>> GetListsByBoardIdAsync(int boardId)
    {
        using var context = CreateContext();
        return await context.Set<LogisticsList>()
            .AsNoTracking()
            .Where(l => l.BoardId == boardId)
            .OrderBy(l => l.Position)
            .ToListAsync();
    }

    public async Task<LogisticsList?> GetListWithCardsAsync(int id)
    {
        using var context = CreateContext();
        return await context.Set<LogisticsList>()
            .AsNoTracking()
            .Include(l => l.Cards.OrderBy(c => c.Position))
            .ThenInclude(c => c.AssignedToUser)
            .Include(l => l.Cards)
            .ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(l => l.Id == id);
    }
}
