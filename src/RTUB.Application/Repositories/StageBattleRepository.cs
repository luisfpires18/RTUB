using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for StageBattle entity
/// </summary>
public class StageBattleRepository : Repository<StageBattle>, IStageBattleRepository
{
    public StageBattleRepository(ApplicationDbContext context) : base(context) { }

    public async Task<List<StageBattle>> GetByCharacterIdAsync(int characterId)
    {
        return await _context.StageBattles
            .Include(sb => sb.Character)
            .Include(sb => sb.StageEnemy)
            .Where(sb => sb.CharacterId == characterId)
            .OrderByDescending(sb => sb.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<StageBattle>> GetRecentByCharacterIdAsync(int characterId, int count)
    {
        return await _context.StageBattles
            .Include(sb => sb.Character)
            .Include(sb => sb.StageEnemy)
            .Where(sb => sb.CharacterId == characterId)
            .OrderByDescending(sb => sb.CreatedAt)
            .Take(count)
            .ToListAsync();
    }
}
