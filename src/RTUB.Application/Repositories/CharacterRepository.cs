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

        // Get all eligible opponents
        var allOpponents = await _context.Characters
            .Include(c => c.User)
            .Where(c => memberUserIds.Contains(c.UserId) && c.Id != excludeCharacterId)
            .ToListAsync();

        if (!allOpponents.Any())
            return new List<Character>();

        var playerLevel = playerCharacter.Level;

        // Categorize opponents by level relationship
        var higherLevel = allOpponents
            .Where(o => o.Level > playerLevel)
            .OrderBy(o => o.Level - playerLevel) // Closest to player level first
            .ToList();

        var sameLevel = allOpponents
            .Where(o => o.Level == playerLevel)
            .ToList();

        var lowerLevel = allOpponents
            .Where(o => o.Level < playerLevel)
            .OrderBy(o => playerLevel - o.Level) // Closest to player level first
            .ToList();

        // Build priority list: higher level first, then same level, then lower level
        var prioritizedOpponents = new List<Character>();
        prioritizedOpponents.AddRange(higherLevel);
        prioritizedOpponents.AddRange(sameLevel);
        prioritizedOpponents.AddRange(lowerLevel);

        // Return up to 'count' opponents
        return prioritizedOpponents.Take(count).ToList();
    }
}
