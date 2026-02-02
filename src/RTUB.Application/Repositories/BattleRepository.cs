using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
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

    public async Task<List<MyTunoLeaderboardEntry>> GetTopLeaderboardAsync(int count)
    {
        // First, get all characters with their user information
        var characters = await _context.Characters
            .AsNoTracking()
            .Include(c => c.User)
            .Select(c => new
            {
                c.Id,
                c.UserId,
                DisplayName = c.User.Nickname ?? c.User.UserName ?? "Jogador",
                c.User.ImageUrl,
                c.Level
            })
            .ToListAsync();

        // Second, calculate wins for each character from battles
        var winsData = await _context.Battles
            .Where(b => b.Outcome == BattleOutcome.AttackerWon || b.Outcome == BattleOutcome.DefenderWon)
            .Select(b => b.Outcome == BattleOutcome.AttackerWon ? b.AttackerCharacterId : b.DefenderCharacterId)
            .ToListAsync();

        // Group and count wins in memory
        var winsByCharacter = winsData
            .GroupBy(characterId => characterId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Third, get highest stage for each user from StageProgress
        var stageProgressData = await _context.StageProgresses
            .AsNoTracking()
            .ToDictionaryAsync(sp => sp.UserId, sp => sp.HighestStage);

        // Combine characters with their wins and sort
        var leaderboard = characters
            .Select(c => new MyTunoLeaderboardEntry
            {
                CharacterId = c.Id,
                UserId = c.UserId,
                DisplayName = c.DisplayName,
                AvatarUrl = c.ImageUrl,
                Wins = winsByCharacter.ContainsKey(c.Id) ? winsByCharacter[c.Id] : 0,
                Level = c.Level,
                HighestStage = stageProgressData.ContainsKey(c.UserId) ? stageProgressData[c.UserId] : 0
            })
            .OrderByDescending(entry => entry.Wins)
            .ThenByDescending(entry => entry.Level)
            .ThenBy(entry => entry.DisplayName)
            .Take(count)
            .ToList();

        return leaderboard;
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
