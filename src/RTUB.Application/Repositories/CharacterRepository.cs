using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Character entity
/// </summary>
public class CharacterRepository : Repository<Character>, ICharacterRepository
{
    public CharacterRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory) { }

    public async Task<Character?> GetByUserIdAsync(string userId)
    {
        using var context = CreateContext();
        return await context.Characters
            .AsNoTracking()
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == userId);
    }

    public async Task<List<Character>> GetAllOrderedByLevelAsync()
    {
        using var context = CreateContext();
        return await context.Characters
            .AsNoTracking()
            .Include(c => c.User)
            .OrderByDescending(c => c.Level)
            .ThenByDescending(c => c.XP)
            .ToListAsync();
    }

    public async Task<List<Character>> GetArenaOpponentsAsync(int excludeCharacterId)
    {
        // Return all characters except the current player — no category filter so
        // every registered character is a valid arena opponent.
        using var context = CreateContext();
        return await context.Characters
            .AsNoTracking()
            .Include(c => c.User)
            .Where(c => c.Id != excludeCharacterId)
            .OrderByDescending(c => c.Level)
            .ThenByDescending(c => c.ArenaRating)
            .ToListAsync();
    }
}
