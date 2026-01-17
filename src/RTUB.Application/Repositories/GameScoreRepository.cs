using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for GameScore entity
/// </summary>
public class GameScoreRepository : Repository<GameScore>, IGameScoreRepository
{
    public GameScoreRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<GameScore>> GetTopScoresAsync(string gameId, int limit = 10)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(gs => gs.User)
            .Where(gs => gs.GameId == gameId)
            .OrderByDescending(gs => gs.Score)
            .ThenByDescending(gs => gs.Level)
            .ThenBy(gs => gs.PlayedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IEnumerable<GameScore>> GetUserScoresAsync(string userId, string gameId, int limit = 10)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(gs => gs.User)
            .Where(gs => gs.UserId == userId && gs.GameId == gameId)
            .OrderByDescending(gs => gs.PlayedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<GameScore?> GetUserBestScoreAsync(string userId, string gameId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(gs => gs.User)
            .Where(gs => gs.UserId == userId && gs.GameId == gameId)
            .OrderByDescending(gs => gs.Score)
            .ThenByDescending(gs => gs.Level)
            .ThenBy(gs => gs.PlayedAt)
            .FirstOrDefaultAsync();
    }
}
