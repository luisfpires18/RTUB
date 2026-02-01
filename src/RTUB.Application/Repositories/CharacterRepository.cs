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

    public async Task<List<Character>> GetRandomOpponentsAsync(int excludeCharacterId, int count = 4)
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

        // Prioritize opponents within ±3 levels
        var closeOpponents = allOpponents
            .Where(o => Math.Abs(o.Level - playerCharacter.Level) <= 3)
            .ToList();

        // If not enough close opponents, expand to ±5 levels
        List<Character> candidateOpponents;
        if (closeOpponents.Count >= count)
        {
            candidateOpponents = closeOpponents;
        }
        else
        {
            var mediumOpponents = allOpponents
                .Where(o => Math.Abs(o.Level - playerCharacter.Level) <= 5)
                .ToList();
            candidateOpponents = mediumOpponents.Count >= count ? mediumOpponents : allOpponents;
        }

        // Fisher-Yates shuffle for efficient O(n) randomization
        // This is significantly faster than LINQ OrderBy with Guid.NewGuid() which is O(n log n)
        // and creates unnecessary GUID objects for each comparison
        var random = Random.Shared;
        var shuffled = candidateOpponents.ToList();
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return shuffled.Take(count).ToList();
    }
}
