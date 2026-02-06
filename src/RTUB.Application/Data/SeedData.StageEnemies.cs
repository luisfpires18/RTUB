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
                .Where(e => e.Type == EnemyType.Normal)
                .Select(e => $"{e.Region}:{e.Name}"),
            StringComparer.OrdinalIgnoreCase);

        var existingBossStages = new HashSet<int>(
            existingEnemies
                .Where(e => e.Type == EnemyType.Boss && e.BossStageNumber.HasValue)
                .Select(e => e.BossStageNumber!.Value));

        var enemies = new List<StageEnemy>();

        // ===================
        // FOREST REGION (1-100)
        // ===================
        SeedForestNormals(existingNormalNames, enemies);
        SeedForestBosses(existingBossStages, enemies);

        // ===================
        // SWAMP REGION (101-200)
        // ===================
        SeedSwampNormals(existingNormalNames, enemies);
        SeedSwampBosses(existingBossStages, enemies);

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

    private static void SeedForestNormals(HashSet<string> existingNormalNames, List<StageEnemy> enemies)
    {
        var forestBasePath = "/sprites/games/my-tuno/enemies/forest";
        var normals = new List<(string Name, int HP, int Power, int Speed, int Defense, PlacementType Placement)>
        {
            ("Wolf", 50, 8, 6, 3, PlacementType.Terrestrial),
            ("Boar", 70, 10, 4, 5, PlacementType.Terrestrial),
            ("Spider", 40, 12, 8, 2, PlacementType.Terrestrial),
            ("Snake", 45, 9, 7, 2, PlacementType.Terrestrial),
            ("Bee", 30, 6, 10, 1, PlacementType.Aerial),
            ("Beetle", 60, 7, 3, 8, PlacementType.Aerial),
            ("Eagle", 55, 11, 9, 3, PlacementType.Aerial),
            ("Panther", 65, 14, 8, 4, PlacementType.Terrestrial),
            ("Cheetah", 55, 13, 12, 3, PlacementType.Terrestrial),
            ("Stag", 75, 9, 7, 6, PlacementType.Terrestrial)
        };

        foreach (var (name, hp, power, speed, defense, placement) in normals)
        {
            if (!existingNormalNames.Contains($"Forest:{name}"))
            {
                enemies.Add(StageEnemy.Create(
                    name: name,
                    type: EnemyType.Normal,
                    region: RegionType.Forest,
                    baseHP: hp,
                    basePower: power,
                    baseSpeed: speed,
                    baseDefense: defense,
                    spritePath: $"{forestBasePath}/{name.ToLowerInvariant()}.png",
                    placement: placement
                ));
            }
        }
    }

    private static void SeedForestBosses(HashSet<int> existingBossStages, List<StageEnemy> enemies)
    {
        var bossWebBasePath = "/sprites/games/my-tuno/enemies/forest";
        var bossStats = GetForestBossStats();
        var bossSeeds = new List<BossSeedInfo>
        {
            new(1, "Bear", $"{bossWebBasePath}/boss_1_bear.png"),
            new(2, "Tiger", $"{bossWebBasePath}/boss_2_tiger.png"),
            new(3, "Mantis", $"{bossWebBasePath}/boss_3_mantis.png"),
            new(4, "Falcon", $"{bossWebBasePath}/boss_4_falcon.png", PlacementType.Aerial),
            new(5, "Leecher", $"{bossWebBasePath}/boss_5_leecher.png"),
            new(6, "Python", $"{bossWebBasePath}/boss_6_python.png"),
            new(7, "Centipede", $"{bossWebBasePath}/boss_7_centipede.png"),
            new(8, "Jaguar", $"{bossWebBasePath}/boss_8_jaguar.png"),
            new(9, "Gorilla", $"{bossWebBasePath}/boss_9_gorilla.png"),
            new(10, "Basilisk", $"{bossWebBasePath}/boss_10_basilisk.png")
        };

        SeedBosses(bossSeeds, bossStats, RegionType.Forest, existingBossStages, enemies);
    }

    private static void SeedSwampNormals(HashSet<string> existingNormalNames, List<StageEnemy> enemies)
    {
        var swampBasePath = "/sprites/games/my-tuno/enemies/swamp";
        var normals = new List<(string Name, int HP, int Power, int Speed, int Defense, PlacementType Placement)>
        {
            ("Crab", 65, 10, 4, 8, PlacementType.Terrestrial),
            ("Crocodile", 90, 14, 3, 7, PlacementType.Terrestrial),
            ("Crow", 40, 9, 11, 2, PlacementType.Aerial),
            ("Frog", 35, 7, 9, 3, PlacementType.Terrestrial),
            ("Leech", 45, 12, 6, 2, PlacementType.Terrestrial),
            ("Mosquito", 30, 8, 13, 1, PlacementType.Aerial),
            ("Salamander", 55, 11, 7, 5, PlacementType.Terrestrial),
            ("Slime", 80, 6, 2, 10, PlacementType.Terrestrial),
            ("Snake", 50, 10, 8, 3, PlacementType.Terrestrial),
            ("Stalker", 60, 13, 10, 4, PlacementType.Terrestrial)
        };

        foreach (var (name, hp, power, speed, defense, placement) in normals)
        {
            if (!existingNormalNames.Contains($"Swamp:{name}"))
            {
                enemies.Add(StageEnemy.Create(
                    name: name,
                    type: EnemyType.Normal,
                    region: RegionType.Swamp,
                    baseHP: hp,
                    basePower: power,
                    baseSpeed: speed,
                    baseDefense: defense,
                    spritePath: $"{swampBasePath}/{name.ToLowerInvariant()}.png",
                    placement: placement
                ));
            }
        }
    }

    private static void SeedSwampBosses(HashSet<int> existingBossStages, List<StageEnemy> enemies)
    {
        var bossWebBasePath = "/sprites/games/my-tuno/enemies/swamp";
        var bossStats = GetSwampBossStats();

        // All bosses have sprites
        var bossSeeds = new List<BossSeedInfo>
        {
            new(1, "Frog King", $"{bossWebBasePath}/boss_1_frog.png"),
            new(2, "Pelican", $"{bossWebBasePath}/boss_2_pelican.png", PlacementType.Aerial),
            new(3, "Leech Lord", $"{bossWebBasePath}/boss_3_leech.png"),
            new(4, "Hydra", $"{bossWebBasePath}/boss_4_hydra.png"),
            new(5, "Anaconda", $"{bossWebBasePath}/boss_5_anaconda.png"),
            new(6, "Crayfish", $"{bossWebBasePath}/boss_6_crayfish.png"),
            new(7, "Darner", $"{bossWebBasePath}/boss_7_darner.png"),
            new(8, "Hippopotamus", $"{bossWebBasePath}/boss_8_hippopotamus.png"),
            new(9, "Troll", $"{bossWebBasePath}/boss_9_troll.png"),
            new(10, "Aligator", $"{bossWebBasePath}/boss_10_aligator.png")
        };

        SeedBosses(bossSeeds, bossStats, RegionType.Swamp, existingBossStages, enemies);
    }

    private static void SeedBosses(
        List<BossSeedInfo> bossSeeds,
        Dictionary<int, BossStats> bossStats,
        RegionType region,
        HashSet<int> existingBossStages,
        List<StageEnemy> enemies)
    {
        var stageOffset = region switch
        {
            RegionType.Forest => 0,
            RegionType.Swamp => 100,
            _ => 0
        };

        foreach (var boss in bossSeeds)
        {
            var bossStageNumber = stageOffset + boss.Index * 10;
            if (existingBossStages.Contains(bossStageNumber))
                continue;

            var stats = bossStats.TryGetValue(boss.Index, out var foundStats)
                ? foundStats
                : BossStats.Default;

            enemies.Add(StageEnemy.Create(
                name: boss.Name,
                type: EnemyType.Boss,
                region: region,
                baseHP: stats.BaseHP,
                basePower: stats.BasePower,
                baseSpeed: stats.BaseSpeed,
                baseDefense: stats.BaseDefense,
                baseCriticalChance: stats.BaseCriticalChance,
                baseFidelisDrop: stats.BaseFidelisDrop,
                beerDropChance: stats.BeerDropChance,
                shotDropChance: stats.ShotDropChance,
                spritePath: boss.SpritePath,
                bossStageNumber: bossStageNumber,
                placement: boss.Placement
            ));
        }
    }

    private static Dictionary<int, BossStats> GetSwampBossStats()
    {
        return new Dictionary<int, BossStats>
        {
            { 1, new BossStats(600, 24, 5, 18, 0.10, 60, 0.5, 0.2) },
            { 2, new BossStats(700, 28, 7, 20, 0.12, 70, 0.5, 0.2) },
            { 3, new BossStats(650, 34, 10, 15, 0.15, 80, 0.5, 0.2) },
            { 4, new BossStats(680, 32, 12, 16, 0.14, 90, 0.5, 0.2) },
            { 5, new BossStats(800, 26, 6, 24, 0.08, 100, 0.5, 0.2) },
            { 6, new BossStats(750, 36, 8, 20, 0.12, 110, 0.5, 0.2) },
            { 7, new BossStats(850, 30, 9, 26, 0.10, 120, 0.5, 0.2) },
            { 8, new BossStats(780, 40, 11, 22, 0.18, 130, 0.5, 0.2) },
            { 9, new BossStats(1000, 34, 6, 28, 0.12, 140, 0.5, 0.2) },
            { 10, new BossStats(1400, 45, 10, 35, 0.20, 250, 0.8, 0.4) }
        };
    }

    private sealed record BossSeedInfo(int Index, string Name, string? SpritePath, PlacementType Placement = PlacementType.Terrestrial);

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
