using Microsoft.EntityFrameworkCore;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data;

public static partial class SeedData
{
    /// <summary>
    /// Seeds Light biome stage enemies if they don't already exist.
    /// Called during initialization — safe to run on existing databases.
    /// </summary>
    public static async Task SeedLightBiomeEnemiesAsync(ApplicationDbContext dbContext)
    {
        var hasLightEnemies = await dbContext.StageEnemies
            .AnyAsync(e => e.Region == RegionType.Light);

        if (hasLightEnemies) return;

        var basePath = "/sprites/games/my-tuno/enemies/light";

        // --- Normal enemies (10 types) ---
        var normalEnemies = new[]
        {
            StageEnemy.Create("Archer",    EnemyType.Normal, RegionType.Light, baseHP: 280, basePower: 52, baseSpeed: 18, baseDefense: 14, baseCriticalChance: 0.10, baseFidelisDrop: 3.5m, finoDropChance: 0.12, shotDropChance: 0.06, spritePath: $"{basePath}/archer.png"),
            StageEnemy.Create("Barbarian", EnemyType.Normal, RegionType.Light, baseHP: 320, basePower: 58, baseSpeed: 14, baseDefense: 18, baseCriticalChance: 0.08, baseFidelisDrop: 3.5m, finoDropChance: 0.12, shotDropChance: 0.06, spritePath: $"{basePath}/barbarian.png"),
            StageEnemy.Create("Bard",      EnemyType.Normal, RegionType.Light, baseHP: 240, basePower: 45, baseSpeed: 20, baseDefense: 12, baseCriticalChance: 0.12, baseFidelisDrop: 3.5m, finoDropChance: 0.12, shotDropChance: 0.06, spritePath: $"{basePath}/bard.png"),
            StageEnemy.Create("Duelist",   EnemyType.Normal, RegionType.Light, baseHP: 260, basePower: 55, baseSpeed: 22, baseDefense: 13, baseCriticalChance: 0.15, baseFidelisDrop: 3.5m, finoDropChance: 0.12, shotDropChance: 0.06, spritePath: $"{basePath}/duelist.png"),
            StageEnemy.Create("Fighter",   EnemyType.Normal, RegionType.Light, baseHP: 300, basePower: 54, baseSpeed: 16, baseDefense: 16, baseCriticalChance: 0.09, baseFidelisDrop: 3.5m, finoDropChance: 0.12, shotDropChance: 0.06, spritePath: $"{basePath}/fighter.png"),
            StageEnemy.Create("Hunter",    EnemyType.Normal, RegionType.Light, baseHP: 250, basePower: 50, baseSpeed: 21, baseDefense: 11, baseCriticalChance: 0.13, baseFidelisDrop: 3.5m, finoDropChance: 0.12, shotDropChance: 0.06, spritePath: $"{basePath}/hunter.png"),
            StageEnemy.Create("Pikeman",   EnemyType.Normal, RegionType.Light, baseHP: 310, basePower: 48, baseSpeed: 13, baseDefense: 20, baseCriticalChance: 0.07, baseFidelisDrop: 3.5m, finoDropChance: 0.12, shotDropChance: 0.06, spritePath: $"{basePath}/pikeman.png"),
            StageEnemy.Create("Templar",   EnemyType.Normal, RegionType.Light, baseHP: 340, basePower: 50, baseSpeed: 12, baseDefense: 22, baseCriticalChance: 0.06, baseFidelisDrop: 3.5m, finoDropChance: 0.12, shotDropChance: 0.06, spritePath: $"{basePath}/templar.png"),
            StageEnemy.Create("Warrior",   EnemyType.Normal, RegionType.Light, baseHP: 330, basePower: 56, baseSpeed: 15, baseDefense: 17, baseCriticalChance: 0.08, baseFidelisDrop: 3.5m, finoDropChance: 0.12, shotDropChance: 0.06, spritePath: $"{basePath}/warrior.png"),
            StageEnemy.Create("Wizard",    EnemyType.Normal, RegionType.Light, baseHP: 220, basePower: 60, baseSpeed: 17, baseDefense: 10, baseCriticalChance: 0.14, baseFidelisDrop: 3.5m, finoDropChance: 0.12, shotDropChance: 0.06, spritePath: $"{basePath}/wizard.png"),
        };

        // --- Bosses (10, one per 10 stages: 1010, 1020, ... 1100) ---
        var bosses = new[]
        {
            StageEnemy.Create("Angel",     EnemyType.Boss, RegionType.Light, baseHP: 800,  basePower: 90,  baseSpeed: 20, baseDefense: 30, baseCriticalChance: 0.12, baseFidelisDrop: 12m, finoDropChance: 0.25, shotDropChance: 0.12, spritePath: $"{basePath}/boss_1_angel.png",     bossStageNumber: 1010),
            StageEnemy.Create("Archangel", EnemyType.Boss, RegionType.Light, baseHP: 900,  basePower: 95,  baseSpeed: 22, baseDefense: 32, baseCriticalChance: 0.13, baseFidelisDrop: 14m, finoDropChance: 0.26, shotDropChance: 0.13, spritePath: $"{basePath}/boss_2_archangel.png", bossStageNumber: 1020),
            StageEnemy.Create("Paladin",   EnemyType.Boss, RegionType.Light, baseHP: 1000, basePower: 100, baseSpeed: 18, baseDefense: 38, baseCriticalChance: 0.10, baseFidelisDrop: 16m, finoDropChance: 0.27, shotDropChance: 0.13, spritePath: $"{basePath}/boss_3_paladin.png",   bossStageNumber: 1030),
            StageEnemy.Create("Dragon",    EnemyType.Boss, RegionType.Light, baseHP: 1100, basePower: 110, baseSpeed: 24, baseDefense: 35, baseCriticalChance: 0.15, baseFidelisDrop: 18m, finoDropChance: 0.28, shotDropChance: 0.14, spritePath: $"{basePath}/boss_4_dragon.png",    bossStageNumber: 1040),
            StageEnemy.Create("Mantis",    EnemyType.Boss, RegionType.Light, baseHP: 950,  basePower: 105, baseSpeed: 28, baseDefense: 28, baseCriticalChance: 0.18, baseFidelisDrop: 20m, finoDropChance: 0.29, shotDropChance: 0.14, spritePath: $"{basePath}/boss_5_mantis.png",    bossStageNumber: 1050),
            StageEnemy.Create("Lion",      EnemyType.Boss, RegionType.Light, baseHP: 1050, basePower: 108, baseSpeed: 25, baseDefense: 33, baseCriticalChance: 0.14, baseFidelisDrop: 22m, finoDropChance: 0.30, shotDropChance: 0.15, spritePath: $"{basePath}/boss_6_lion.png",      bossStageNumber: 1060),
            StageEnemy.Create("Phoenix",   EnemyType.Boss, RegionType.Light, baseHP: 1150, basePower: 112, baseSpeed: 26, baseDefense: 30, baseCriticalChance: 0.16, baseFidelisDrop: 24m, finoDropChance: 0.31, shotDropChance: 0.15, spritePath: $"{basePath}/boss_7_phoenix.png",   bossStageNumber: 1070),
            StageEnemy.Create("Golem",     EnemyType.Boss, RegionType.Light, baseHP: 1300, basePower: 95,  baseSpeed: 12, baseDefense: 45, baseCriticalChance: 0.08, baseFidelisDrop: 26m, finoDropChance: 0.32, shotDropChance: 0.16, spritePath: $"{basePath}/boss_8_golem.png",     bossStageNumber: 1080),
            StageEnemy.Create("Pegasus",   EnemyType.Boss, RegionType.Light, baseHP: 1200, basePower: 115, baseSpeed: 30, baseDefense: 32, baseCriticalChance: 0.17, baseFidelisDrop: 28m, finoDropChance: 0.33, shotDropChance: 0.16, spritePath: $"{basePath}/boss_9_pegasus.png",   bossStageNumber: 1090, placement: PlacementType.Aerial),
            StageEnemy.Create("Knight",    EnemyType.Boss, RegionType.Light, baseHP: 1400, basePower: 120, baseSpeed: 22, baseDefense: 40, baseCriticalChance: 0.15, baseFidelisDrop: 30m, finoDropChance: 0.35, shotDropChance: 0.18, spritePath: $"{basePath}/boss_10_knight.png",   bossStageNumber: 1100),
        };

        await dbContext.StageEnemies.AddRangeAsync(normalEnemies);
        await dbContext.StageEnemies.AddRangeAsync(bosses);
        await dbContext.SaveChangesAsync();
    }
}
