using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for StageEnemy entity
/// </summary>
public class StageEnemyRepository : Repository<StageEnemy>, IStageEnemyRepository
{
    public StageEnemyRepository(ApplicationDbContext context) : base(context) { }

    public async Task<List<StageEnemy>> GetByRegionAsync(RegionType region)
    {
        return await _context.StageEnemies
            .Where(e => e.Region == region)
            .ToListAsync();
    }

    public async Task<List<StageEnemy>> GetByTypeAndRegionAsync(EnemyType type, RegionType region)
    {
        return await _context.StageEnemies
            .Where(e => e.Type == type && e.Region == region)
            .ToListAsync();
    }

    public async Task<StageEnemy?> GetRandomEnemyAsync(EnemyType type, RegionType region)
    {
        var enemies = await GetByTypeAndRegionAsync(type, region);
        
        if (!enemies.Any())
        {
            // Fallback: try any enemy of that type
            enemies = await _context.StageEnemies
                .Where(e => e.Type == type)
                .ToListAsync();
        }

        if (!enemies.Any())
        {
            // Ultimate fallback: get any enemy
            enemies = await _context.StageEnemies.ToListAsync();
        }

        if (!enemies.Any())
            return null;

        var random = Random.Shared;
        return enemies[random.Next(enemies.Count)];
    }
}
