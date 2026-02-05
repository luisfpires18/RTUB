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

    public async Task<StageEnemy?> GetBossForStageAsync(int stageNumber)
    {
        return await _context.StageEnemies
            .FirstOrDefaultAsync(e => e.Type == EnemyType.Boss && e.BossStageNumber == stageNumber);
    }

    public async Task<List<StageEnemy>> GetRandomEnemiesAsync(EnemyType type, RegionType region, int count)
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
            return new List<StageEnemy>();

        // Randomly select enemies (allowing duplicates if not enough unique)
        var random = Random.Shared;
        var selected = new List<StageEnemy>();
        var available = new List<StageEnemy>(enemies);

        for (int i = 0; i < count; i++)
        {
            if (available.Count == 0)
            {
                // If we run out of unique enemies, allow reuse
                available = new List<StageEnemy>(enemies);
            }
            var index = random.Next(available.Count);
            selected.Add(available[index]);
            available.RemoveAt(index); // Try to avoid duplicates first
        }

        return selected;
    }
}
