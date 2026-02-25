using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Game entity
/// </summary>
public class GameRepository : Repository<Game>, IGameRepository
{
    public GameRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory) { }

    public async Task<Game?> GetByKeyAsync(string key)
    {
        return await _context.Games
            .FirstOrDefaultAsync(g => g.Key == key);
    }

    public async Task<List<Game>> GetActiveGamesAsync()
    {
        return await _context.Games
            .Where(g => g.IsActive)
            .OrderBy(g => g.Title)
            .ToListAsync();
    }
}
