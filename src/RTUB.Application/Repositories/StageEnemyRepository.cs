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
        // Void draws from ALL regions
        if (region == RegionType.Void)
            return await _context.StageEnemies.ToListAsync();

        return await _context.StageEnemies
            .Where(e => e.Region == region)
            .ToListAsync();
    }

    public async Task<List<StageEnemy>> GetByTypeAndRegionAsync(EnemyType type, RegionType region)
    {
        // Void draws from ALL regions
        if (region == RegionType.Void)
            return await _context.StageEnemies
                .Where(e => e.Type == type)
                .ToListAsync();

        return await _context.StageEnemies
            .Where(e => e.Type == type && e.Region == region)
            .ToListAsync();
    }

    public async Task<StageEnemy?> GetRandomEnemyAsync(EnemyType type, RegionType region)
    {
        var enemies = await GetByTypeAndRegionAsync(type, region);

        if (enemies.Count == 0)
        {
            // Fallback: try any enemy of that type
            enemies = await _context.StageEnemies
                .Where(e => e.Type == type)
                .ToListAsync();
        }

        if (enemies.Count == 0)
        {
            // Ultimate fallback: get any enemy
            enemies = await _context.StageEnemies.ToListAsync();
        }

        if (enemies.Count == 0)
            return null;

        var random = Random.Shared;
        return enemies[random.Next(enemies.Count)];
    }

    public async Task<StageEnemy?> GetBossForStageAsync(int stageNumber)
    {
        // Exact stage match first
        var boss = await _context.StageEnemies
            .FirstOrDefaultAsync(e => e.Type == EnemyType.Boss && e.BossStageNumber == stageNumber);

        if (boss != null) return boss;

        // In the Void (stage > 1000), grab a random boss from any region
        if (stageNumber > 1000)
        {
            var allBosses = await _context.StageEnemies
                .Where(e => e.Type == EnemyType.Boss)
                .ToListAsync();

            if (allBosses.Count > 0)
                return allBosses[Random.Shared.Next(allBosses.Count)];
        }

        return null;
    }

    public async Task<List<StageEnemy>> GetRandomEnemiesAsync(EnemyType type, RegionType region, int count)
    {
        var enemies = await GetByTypeAndRegionAsync(type, region);

        if (enemies.Count == 0)
        {
            // Fallback: try any enemy of that type
            enemies = await _context.StageEnemies
                .Where(e => e.Type == type)
                .ToListAsync();
        }

        if (enemies.Count == 0)
        {
            // Ultimate fallback: get any enemy
            enemies = await _context.StageEnemies.ToListAsync();
        }

        if (enemies.Count == 0)
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
