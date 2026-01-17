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
    public GameScoreRepository(ApplicationDbContext context) : base(context) { }

    public async Task<List<GameScore>> GetTopScoresAsync(string gameKey, int count = 10)
    {
        return await _context.GameScores
            .AsNoTracking()
            .Include(s => s.User)
            .Where(s => s.GameKey == gameKey)
            .OrderByDescending(s => s.Points)
            .ThenByDescending(s => s.MaxLevel)
            .ThenByDescending(s => s.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<GameScore?> GetUserBestScoreAsync(string userId, string gameKey)
    {
        return await _context.GameScores
            .AsNoTracking()
            .Include(s => s.User)
            .Where(s => s.UserId == userId && s.GameKey == gameKey)
            .OrderByDescending(s => s.Points)
            .ThenByDescending(s => s.MaxLevel)
            .FirstOrDefaultAsync();
    }

    public async Task<GameScore?> GetUserScoreAsync(string userId, string gameKey)
    {
        return await _context.GameScores
            .Include(s => s.User)
            .Where(s => s.UserId == userId && s.GameKey == gameKey)
            .FirstOrDefaultAsync();
    }
}
