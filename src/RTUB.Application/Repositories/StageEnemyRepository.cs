using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for StageEnemy entity.
/// Caches the entire (small) StageEnemies table in memory after first load
/// to avoid repeated DB roundtrips — the data is seeded once and never changes at runtime.
/// </summary>
public class StageEnemyRepository : Repository<StageEnemy>, IStageEnemyRepository
{
    // In-memory cache: loaded once, never expires (seed data is static)
    private static List<StageEnemy>? _allEnemiesCache;
    private static readonly SemaphoreSlim _cacheLock = new(1, 1);

    public StageEnemyRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory) { }

    /// <summary>
    /// Loads the full StageEnemies table into memory on first call.
    /// Subsequent calls return the cached list (no DB hit).
    /// </summary>
    private async Task<List<StageEnemy>> GetAllCachedAsync()
    {
        if (_allEnemiesCache != null)
            return _allEnemiesCache;

        await _cacheLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_allEnemiesCache != null)
                return _allEnemiesCache;

            _allEnemiesCache = await _contextFactory.CreateDbContext().StageEnemies
                .AsNoTracking()
                .ToListAsync();
            return _allEnemiesCache;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task<List<StageEnemy>> GetByRegionAsync(RegionType region)
    {
        var all = await GetAllCachedAsync();

        // Arena draws from ALL regions (random enemies from all previous stages)
        if (region == RegionType.Arena)
            return all;

        return all.Where(e => e.Region == region).ToList();
    }

    public async Task<List<StageEnemy>> GetByTypeAndRegionAsync(EnemyType type, RegionType region)
    {
        var all = await GetAllCachedAsync();

        // Arena draws from ALL regions
        if (region == RegionType.Arena)
            return all.Where(e => e.Type == type).ToList();

        return all.Where(e => e.Type == type && e.Region == region).ToList();
    }

    public async Task<StageEnemy?> GetRandomEnemyAsync(EnemyType type, RegionType region)
    {
        var enemies = await GetByTypeAndRegionAsync(type, region);

        if (enemies.Count == 0)
        {
            var all = await GetAllCachedAsync();
            // Fallback: try any enemy of that type
            enemies = all.Where(e => e.Type == type).ToList();
        }

        if (enemies.Count == 0)
        {
            // Ultimate fallback: get any enemy
            enemies = await GetAllCachedAsync();
        }

        if (enemies.Count == 0)
            return null;

        return enemies[Random.Shared.Next(enemies.Count)];
    }

    public async Task<StageEnemy?> GetBossForStageAsync(int stageNumber)
    {
        var all = await GetAllCachedAsync();

        // Exact stage match first
        var boss = all.FirstOrDefault(e => e.Type == EnemyType.Boss && e.BossStageNumber == stageNumber);
        if (boss != null) return boss;

        // No exact match — deterministic fallback based on stage number.
        // Uses modular indexing so the same stage always returns the same boss.
        var allBosses = all.Where(e => e.Type == EnemyType.Boss).ToList();
        if (allBosses.Count > 0)
            return allBosses[Math.Abs(stageNumber) % allBosses.Count];

        return null;
    }

    public async Task<List<StageEnemy>> GetRandomEnemiesAsync(EnemyType type, RegionType region, int count)
    {
        var enemies = await GetByTypeAndRegionAsync(type, region);

        if (enemies.Count == 0)
        {
            var all = await GetAllCachedAsync();
            // Fallback: try any enemy of that type
            enemies = all.Where(e => e.Type == type).ToList();
        }

        if (enemies.Count == 0)
        {
            // Ultimate fallback: get any enemy
            enemies = await GetAllCachedAsync();
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

    /// <summary>
    /// Invalidates the in-memory cache (call after re-seeding).
    /// </summary>
    public static void InvalidateCache()
    {
        _allEnemiesCache = null;
    }
}
