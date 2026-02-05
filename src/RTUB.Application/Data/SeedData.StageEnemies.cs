using Microsoft.EntityFrameworkCore;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data;

/// <summary>
/// Seeds the StageEnemies table with normal enemies and bosses
/// </summary>
public static partial class SeedData
{
    public static async Task SeedStageEnemiesAsync(ApplicationDbContext dbContext)
    {
        Console.WriteLine("Seeding stage enemies...");

        var existingEnemies = await dbContext.StageEnemies.AsNoTracking().ToListAsync();
        var existingNormalNames = new HashSet<string>(
            existingEnemies
                .Where(e => e.Type == EnemyType.Normal && e.Region == RegionType.Forest)
                .Select(e => e.Name),
            StringComparer.OrdinalIgnoreCase);

        var existingBossStages = new HashSet<int>(
            existingEnemies
                .Where(e => e.Type == EnemyType.Boss && e.BossStageNumber.HasValue)
                .Select(e => e.BossStageNumber!.Value));

        var enemies = new List<StageEnemy>();

        // ===================
        // FOREST REGION (1-100)
        // ===================
        
        // Normal enemies (for random selection in stages 1-9, 11-19, etc.)
        if (!existingNormalNames.Contains("Wolf"))
        {
            enemies.Add(StageEnemy.Create(
            name: "Wolf",
            type: EnemyType.Normal,
            region: RegionType.Forest,
            baseHP: 50,
            basePower: 8,
            baseSpeed: 6,
            baseDefense: 3,
            spritePath: "/sprites/games/my-tuno/enemies/forest/wolf.png"
            ));
        }

        if (!existingNormalNames.Contains("Boar"))
        {
            enemies.Add(StageEnemy.Create(
            name: "Boar",
            type: EnemyType.Normal,
            region: RegionType.Forest,
            baseHP: 70,
            basePower: 10,
            baseSpeed: 4,
            baseDefense: 5,
            spritePath: "/sprites/games/my-tuno/enemies/forest/boar.png"
            ));
        }

        if (!existingNormalNames.Contains("Spider"))
        {
            enemies.Add(StageEnemy.Create(
            name: "Spider",
            type: EnemyType.Normal,
            region: RegionType.Forest,
            baseHP: 40,
            basePower: 12,
            baseSpeed: 8,
            baseDefense: 2,
            spritePath: "/sprites/games/my-tuno/enemies/forest/spider.png"
            ));
        }

        if (!existingNormalNames.Contains("Snake"))
        {
            enemies.Add(StageEnemy.Create(
            name: "Snake",
            type: EnemyType.Normal,
            region: RegionType.Forest,
            baseHP: 45,
            basePower: 9,
            baseSpeed: 7,
            baseDefense: 2,
            spritePath: "/sprites/games/my-tuno/enemies/forest/snake.png"
            ));
        }

        if (!existingNormalNames.Contains("Bee"))
        {
            enemies.Add(StageEnemy.Create(
            name: "Bee",
            type: EnemyType.Normal,
            region: RegionType.Forest,
            baseHP: 30,
            basePower: 6,
            baseSpeed: 10,
            baseDefense: 1,
            spritePath: "/sprites/games/my-tuno/enemies/forest/bee.png"
            ));
        }

        if (!existingNormalNames.Contains("Beetle"))
        {
            enemies.Add(StageEnemy.Create(
            name: "Beetle",
            type: EnemyType.Normal,
            region: RegionType.Forest,
            baseHP: 60,
            basePower: 7,
            baseSpeed: 3,
            baseDefense: 8,
            spritePath: "/sprites/games/my-tuno/enemies/forest/beetle.png"
            ));
        }

        if (!existingNormalNames.Contains("Eagle"))
        {
            enemies.Add(StageEnemy.Create(
            name: "Eagle",
            type: EnemyType.Normal,
            region: RegionType.Forest,
            baseHP: 55,
            basePower: 11,
            baseSpeed: 9,
            baseDefense: 3,
            spritePath: "/sprites/games/my-tuno/enemies/forest/eagle.png"
            ));
        }

        if (!existingNormalNames.Contains("Panther"))
        {
            enemies.Add(StageEnemy.Create(
            name: "Panther",
            type: EnemyType.Normal,
            region: RegionType.Forest,
            baseHP: 65,
            basePower: 14,
            baseSpeed: 8,
            baseDefense: 4,
            spritePath: "/sprites/games/my-tuno/enemies/forest/panther.png"
            ));
        }

        // ===================
        // FOREST BOSSES (stages 10, 20, 30, ... 100)
        // Uses existing boss sprite files: boss_1_bear.png, boss_2_tiger.png, etc.
        // ===================

        var bossWebBasePath = "/sprites/games/my-tuno/enemies/forest";
        var bossStats = GetForestBossStats();
        var bossSeeds = new List<BossSeedInfo>
        {
            new(1, "Bear", $"{bossWebBasePath}/boss_1_bear.png"),
            new(2, "Tiger", $"{bossWebBasePath}/boss_2_tiger.png"),
            new(3, "Mantis", $"{bossWebBasePath}/boss_3_mantis.png"),
            new(4, "Falcon", $"{bossWebBasePath}/boss_4_falcon.png"),
            new(5, "Leecher", $"{bossWebBasePath}/boss_5_leecher.png"),
            new(6, "Python", $"{bossWebBasePath}/boss_6_python.png"),
            new(7, "Centipede", $"{bossWebBasePath}/boss_7_centipede.png"),
            new(8, "Jaguar", $"{bossWebBasePath}/boss_8_jaguar.png"),
            new(9, "Gorilla", $"{bossWebBasePath}/boss_9_gorilla.png"),
            new(10, "Basilisk", $"{bossWebBasePath}/boss_10_basilisk.png")
        };

        foreach (var boss in bossSeeds)
        {
            var bossStageNumber = boss.Index * 10;
            if (existingBossStages.Contains(bossStageNumber))
            {
                continue;
            }

            var stats = bossStats.TryGetValue(boss.Index, out var foundStats)
                ? foundStats
                : BossStats.Default;

            enemies.Add(StageEnemy.Create(
                name: boss.Name,
                type: EnemyType.Boss,
                region: RegionType.Forest,
                baseHP: stats.BaseHP,
                basePower: stats.BasePower,
                baseSpeed: stats.BaseSpeed,
                baseDefense: stats.BaseDefense,
                baseCriticalChance: stats.BaseCriticalChance,
                baseFidelisDrop: stats.BaseFidelisDrop,
                beerDropChance: stats.BeerDropChance,
                shotDropChance: stats.ShotDropChance,
                spritePath: boss.SpritePath,
                bossStageNumber: bossStageNumber
            ));
        }

        if (enemies.Count == 0)
        {
            Console.WriteLine("No new stage enemies to seed.");
            return;
        }

        await dbContext.StageEnemies.AddRangeAsync(enemies);
        await dbContext.SaveChangesAsync();

        Console.WriteLine($"Seeded {enemies.Count} stage enemies ({enemies.Count(e => e.Type == EnemyType.Boss)} bosses, {enemies.Count(e => e.Type == EnemyType.Normal)} normal)");
    }

    private static Dictionary<int, BossStats> GetForestBossStats()
    {
        return new Dictionary<int, BossStats>
        {
            { 1, new BossStats(500, 20, 5, 15, 0.10, 50, 0.5, 0.2) },
            { 2, new BossStats(600, 25, 7, 18, 0.12, 60, 0.5, 0.2) },
            { 3, new BossStats(550, 30, 10, 12, 0.15, 70, 0.5, 0.2) },
            { 4, new BossStats(580, 28, 12, 14, 0.14, 80, 0.5, 0.2) },
            { 5, new BossStats(700, 22, 6, 20, 0.08, 90, 0.5, 0.2) },
            { 6, new BossStats(650, 32, 8, 16, 0.12, 100, 0.5, 0.2) },
            { 7, new BossStats(750, 26, 9, 22, 0.10, 110, 0.5, 0.2) },
            { 8, new BossStats(680, 35, 11, 18, 0.18, 120, 0.5, 0.2) },
            { 9, new BossStats(900, 30, 6, 25, 0.12, 130, 0.5, 0.2) },
            { 10, new BossStats(1200, 40, 10, 30, 0.20, 200, 0.8, 0.4) }
        };
    }

    private sealed record BossSeedInfo(int Index, string Name, string SpritePath);

    private sealed record BossStats(
        int BaseHP,
        int BasePower,
        int BaseSpeed,
        int BaseDefense,
        double BaseCriticalChance,
        decimal BaseFidelisDrop,
        double BeerDropChance,
        double ShotDropChance)
    {
        public static BossStats Default { get; } = new(500, 20, 5, 15, 0.10, 50, 0.5, 0.2);
    }
}
