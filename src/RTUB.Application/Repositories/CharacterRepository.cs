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
    public CharacterRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Character?> GetByUserIdAsync(string userId)
    {
        return await _context.Characters
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == userId);
    }

    /// <summary>
    /// Gets a character by user ID, forcing a DB reload if the entity is already tracked.
    /// Use on page-load paths to pick up external changes from another circuit.
    /// </summary>
    public async Task<Character?> GetByUserIdFreshAsync(string userId)
    {
        var tracked = _context.Characters.Local.FirstOrDefault(c => c.UserId == userId);
        if (tracked != null)
        {
            await _context.Entry(tracked).ReloadAsync();
            if (tracked.User == null)
                await _context.Entry(tracked).Reference(c => c.User).LoadAsync();
            return tracked;
        }

        return await _context.Characters
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == userId);
    }

    public async Task<List<Character>> GetAllOrderedByLevelAsync()
    {
        return await _context.Characters
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
        return await _context.Characters
            .AsNoTracking()
            .Include(c => c.User)
            .Where(c => c.Id != excludeCharacterId)
            .OrderByDescending(c => c.Level)
            .ThenByDescending(c => c.ArenaRating)
            .ToListAsync();
    }
}
