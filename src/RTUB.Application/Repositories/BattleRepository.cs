using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Battle entity
/// </summary>
public class BattleRepository : Repository<Battle>, IBattleRepository
{
    public BattleRepository(ApplicationDbContext context) : base(context) { }

    public async Task<List<Battle>> GetByAttackerCharacterIdAsync(int characterId)
    {
        return await _context.Battles
            .Include(b => b.Defender)
                .ThenInclude(d => d.User)
            .Where(b => b.AttackerCharacterId == characterId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Battle>> GetByDefenderCharacterIdAsync(int characterId)
    {
        return await _context.Battles
            .Where(b => b.DefenderCharacterId == characterId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Battle>> GetByCharacterIdAsync(int characterId)
    {
        return await _context.Battles
            .Where(b => b.AttackerCharacterId == characterId || b.DefenderCharacterId == characterId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Battle>> GetByCharacterIdAndOutcomeAsync(int characterId, BattleOutcome outcome)
    {
        return await _context.Battles
            .Where(b => (b.AttackerCharacterId == characterId && b.Outcome == BattleOutcome.AttackerWon) ||
                       (b.DefenderCharacterId == characterId && b.Outcome == BattleOutcome.DefenderWon) ||
                       (b.AttackerCharacterId == characterId || b.DefenderCharacterId == characterId) && b.Outcome == BattleOutcome.Draw)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Battle>> GetRecentBattlesByCharacterIdAsync(int characterId, int count)
    {
        return await _context.Battles
            .Where(b => b.AttackerCharacterId == characterId || b.DefenderCharacterId == characterId)
            .OrderByDescending(b => b.CreatedAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<Battle>> GetBattlesBetweenCharactersAsync(int attackerId, int defenderId, TimeSpan? withinTimeSpan = null)
    {
        var query = _context.Battles
            .Where(b => b.AttackerCharacterId == attackerId && b.DefenderCharacterId == defenderId);

        if (withinTimeSpan.HasValue)
        {
            var cutoffDate = DateTime.UtcNow - withinTimeSpan.Value;
            query = query.Where(b => b.CreatedAt >= cutoffDate);
        }

        return await query
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }
}
