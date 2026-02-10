using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Character entity
/// </summary>
public class CharacterRepository : Repository<Character>, ICharacterRepository
{
    public CharacterRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Character?> GetByUserIdAsync(string userId)
    {
        return await _context.Characters
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == userId);
    }

    public async Task<List<Character>> GetAllOrderedByLevelAsync()
    {
        return await _context.Characters
            .Include(c => c.User)
            .OrderByDescending(c => c.Level)
            .ThenByDescending(c => c.XP)
            .ToListAsync();
    }

    public async Task<List<Character>> GetMemberCharactersAsync(int excludeCharacterId)
    {
        // Get all member user IDs
        var memberUserIds = await _context.Users
            .Where(u => u.Categories.Contains(MemberCategory.Caloiro) ||
                       u.Categories.Contains(MemberCategory.Tuno) ||
                       u.Categories.Contains(MemberCategory.Veterano) ||
                       u.Categories.Contains(MemberCategory.Tunossauro))
            .Select(u => u.Id)
            .ToListAsync();

        // Get characters for those users, excluding the specified character
        return await _context.Characters
            .Include(c => c.User)
            .Where(c => memberUserIds.Contains(c.UserId) && c.Id != excludeCharacterId)
            .OrderByDescending(c => c.Level)
            .ThenByDescending(c => c.XP)
            .ToListAsync();
    }

    public async Task<List<Character>> GetRandomOpponentsAsync(int excludeCharacterId, int count = 8)
    {
        // Get the excluded character to know its level
        var playerCharacter = await _context.Characters
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == excludeCharacterId);

        if (playerCharacter == null)
            return new List<Character>();

        // Get all member user IDs
        var memberUserIds = await _context.Users
            .Where(u => u.Categories.Contains(MemberCategory.Caloiro) ||
                       u.Categories.Contains(MemberCategory.Tuno) ||
                       u.Categories.Contains(MemberCategory.Veterano) ||
                       u.Categories.Contains(MemberCategory.Tunossauro))
            .Select(u => u.Id)
            .ToListAsync();

        // Get all eligible opponents (AsNoTracking since these are for display only)
        var allOpponents = await _context.Characters
            .AsNoTracking()
            .Include(c => c.User)
            .Where(c => memberUserIds.Contains(c.UserId) && c.Id != excludeCharacterId)
            .ToListAsync();

        if (!allOpponents.Any())
            return new List<Character>();

        var playerLevel = playerCharacter.Level;
        var halfCount = count / 2; // 4 below/equal, 4 above for count=8

        // Categorize opponents
        var belowOrEqual = allOpponents
            .Where(c => c.Level <= playerLevel)
            .OrderByDescending(c => c.Level) // Closest to player level first
            .ThenBy(_ => Guid.NewGuid()) // Randomize within same level
            .ToList();

        var above = allOpponents
            .Where(c => c.Level > playerLevel)
            .OrderBy(c => c.Level) // Closest to player level first
            .ThenBy(_ => Guid.NewGuid()) // Randomize within same level
            .ToList();

        // Build balanced list: up to half from below/equal, up to half from above
        var result = new List<Character>();

        // Take up to halfCount from below/equal
        var belowPortion = belowOrEqual.Take(halfCount).ToList();
        result.AddRange(belowPortion);

        // Take up to halfCount from above
        var abovePortion = above.Take(halfCount).ToList();
        result.AddRange(abovePortion);

        // If we don't have enough from one side, fill from the other
        var remaining = count - result.Count;
        if (remaining > 0)
        {
            if (belowPortion.Count < halfCount)
            {
                // Need more from above
                var additionalAbove = above.Skip(halfCount).Take(remaining);
                result.AddRange(additionalAbove);
            }
            else if (abovePortion.Count < halfCount)
            {
                // Need more from below
                var additionalBelow = belowOrEqual.Skip(halfCount).Take(remaining);
                result.AddRange(additionalBelow);
            }
        }

        // Sort final result: below/equal first (by level descending), then above (by level ascending)
        // This puts closest matches to the player at the top
        return result
            .OrderByDescending(c => c.Level <= playerLevel ? 1 : 0) // Below/equal first
            .ThenBy(c => c.Level <= playerLevel ? -c.Level : c.Level) // Sort within groups by proximity
            .Take(count)
            .ToList();
    }

    public async Task<List<MyTunoLeaderboardEntry>> GetTopLeaderboardAsync(int count)
    {
        // Get all characters with their user information and wins
        var characters = await _context.Characters
            .AsNoTracking()
            .Include(c => c.User)
            .Select(c => new
            {
                c.Id,
                c.UserId,
                DisplayName = c.User.Nickname ?? c.User.UserName ?? "Jogador",
                c.User.ImageUrl,
                c.Level,
                c.ArenaWins
            })
            .ToListAsync();

        // Get highest stage for each user from StageProgress
        var stageProgressData = await _context.StageProgresses
            .AsNoTracking()
            .ToDictionaryAsync(sp => sp.UserId, sp => sp.HighestStage);

        // Get highest boss stage for each user from BossModeProgress
        var bossModeProgressData = await _context.BossModeProgresses
            .AsNoTracking()
            .ToDictionaryAsync(bp => bp.UserId, bp => bp.HighestBossStage);

        // Get highest survive level for each user from SurviveModeProgress
        var surviveProgressData = await _context.SurviveModeProgresses
            .AsNoTracking()
            .ToDictionaryAsync(sp => sp.UserId, sp => sp.HighestLevel);

        // Combine characters with their wins and sort
        var leaderboard = characters
            .Select(c => new MyTunoLeaderboardEntry
            {
                CharacterId = c.Id,
                UserId = c.UserId,
                DisplayName = c.DisplayName,
                AvatarUrl = c.ImageUrl,
                Wins = c.ArenaWins,
                Level = c.Level,
                HighestStage = stageProgressData.ContainsKey(c.UserId) ? stageProgressData[c.UserId] : 0,
                HighestBossStage = bossModeProgressData.ContainsKey(c.UserId) ? bossModeProgressData[c.UserId] : 0,
                HighestSurviveLevel = surviveProgressData.ContainsKey(c.UserId) ? surviveProgressData[c.UserId] : 0
            })
            .OrderByDescending(entry => entry.Wins)
            .ThenByDescending(entry => entry.Level)
            .ThenBy(entry => entry.DisplayName)
            .Take(count)
            .ToList();

        return leaderboard;
    }

    public async Task<List<Character>> GetArenaOpponentsAsync(int excludeCharacterId)
    {
        // Get all member user IDs
        var memberUserIds = await _context.Users
            .Where(u => u.Categories.Contains(MemberCategory.Caloiro) ||
                       u.Categories.Contains(MemberCategory.Tuno) ||
                       u.Categories.Contains(MemberCategory.Veterano) ||
                       u.Categories.Contains(MemberCategory.Tunossauro))
            .Select(u => u.Id)
            .ToListAsync();

        // Get all member opponents ordered by level (no minimum games filter)
        return await _context.Characters
            .AsNoTracking()
            .Include(c => c.User)
            .Where(c => memberUserIds.Contains(c.UserId)
                     && c.Id != excludeCharacterId)
            .OrderByDescending(c => c.Level)
            .ThenByDescending(c => c.ArenaWins)
            .ToListAsync();
    }
}
