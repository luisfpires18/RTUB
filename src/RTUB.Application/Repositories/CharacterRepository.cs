using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
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

        // Categorize opponents in a single pass for efficiency
        var higherLevel = new List<(Character character, int levelDiff)>();
        var sameLevel = new List<Character>();
        var lowerLevel = new List<(Character character, int levelDiff)>();

        foreach (var opponent in allOpponents)
        {
            if (opponent.Level > playerLevel)
            {
                higherLevel.Add((opponent, opponent.Level - playerLevel));
            }
            else if (opponent.Level == playerLevel)
            {
                sameLevel.Add(opponent);
            }
            else
            {
                lowerLevel.Add((opponent, playerLevel - opponent.Level));
            }
        }

        // Build priority list: higher level first (sorted by proximity), 
        // then same level, then lower level (sorted by proximity)
        var prioritizedOpponents = new List<Character>();

        // Add higher level opponents sorted by proximity (smallest diff first)
        prioritizedOpponents.AddRange(
            higherLevel.OrderBy(x => x.levelDiff).Select(x => x.character)
        );

        // Add same level opponents
        prioritizedOpponents.AddRange(sameLevel);

        // Add lower level opponents sorted by proximity (smallest diff first)
        prioritizedOpponents.AddRange(
            lowerLevel.OrderBy(x => x.levelDiff).Select(x => x.character)
        );

        // Return up to 'count' opponents
        return prioritizedOpponents.Take(count).ToList();
    }
}
