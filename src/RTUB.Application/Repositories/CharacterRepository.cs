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
}
