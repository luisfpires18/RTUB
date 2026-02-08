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

        // ===================
        // MOUNTAINS REGION (201-300)
        // ===================
        SeedMountainsNormals(existingNormalNames, enemies);
        SeedMountainsBosses(existingBossStages, enemies);

        // ===================
        // SNOWY REGION (301-400)
        // ===================
        SeedSnowyNormals(existingNormalNames, enemies);
        SeedSnowyBosses(existingBossStages, enemies);

        // ===================
        // TROPICAL REGION (401-500)
        // ===================
        SeedTropicalNormals(existingNormalNames, enemies);
        SeedTropicalBosses(existingBossStages, enemies);

        // ===================
        // CAVERNS REGION (501-600)
        // ===================
        SeedCavernsNormals(existingNormalNames, enemies);
        SeedCavernsBosses(existingBossStages, enemies);

        // ===================
        // RUINS REGION (801-900)
        // ===================
        SeedRuinsNormals(existingNormalNames, enemies);
        SeedRuinsBosses(existingBossStages, enemies);

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
            RegionType.Mountains => 200,
            RegionType.Snowy => 300,
            RegionType.Tropical => 400,
            RegionType.Caverns => 500,
            RegionType.Desert => 600,
            RegionType.Volcanic => 700,
            RegionType.Ruins => 800,
            RegionType.Dark => 900,
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

    private static void SeedMountainsNormals(HashSet<string> existingNormalNames, List<StageEnemy> enemies)
    {
        var basePath = "/sprites/games/my-tuno/enemies/mountains";
        var normals = new List<(string Name, int HP, int Power, int Speed, int Defense, PlacementType Placement)>
        {
            ("Goat", 70, 9, 6, 7, PlacementType.Terrestrial),
            ("Hound", 55, 12, 9, 4, PlacementType.Terrestrial),
            ("Hyena", 60, 13, 8, 3, PlacementType.Terrestrial),
            ("Lynx", 50, 14, 10, 3, PlacementType.Terrestrial),
            ("Monkey", 45, 10, 11, 3, PlacementType.Terrestrial),
            ("Elephant", 95, 11, 3, 10, PlacementType.Terrestrial),
            ("Rhino", 85, 15, 4, 9, PlacementType.Terrestrial),
            ("Bear", 80, 13, 5, 8, PlacementType.Terrestrial),
            ("Vulture", 40, 11, 12, 2, PlacementType.Aerial),
            ("Pigeon", 35, 7, 13, 1, PlacementType.Aerial)
        };

        foreach (var (name, hp, power, speed, defense, placement) in normals)
        {
            if (!existingNormalNames.Contains($"Mountains:{name}"))
            {
                enemies.Add(StageEnemy.Create(
                    name: name,
                    type: EnemyType.Normal,
                    region: RegionType.Mountains,
                    baseHP: hp,
                    basePower: power,
                    baseSpeed: speed,
                    baseDefense: defense,
                    spritePath: $"{basePath}/{name.ToLowerInvariant()}.png",
                    placement: placement
                ));
            }
        }
    }

    private static void SeedMountainsBosses(HashSet<int> existingBossStages, List<StageEnemy> enemies)
    {
        var bossWebBasePath = "/sprites/games/my-tuno/enemies/mountains";
        var bossStats = GetMountainsBossStats();
        var bossSeeds = new List<BossSeedInfo>
        {
            new(1, "Alpine Guardian", $"{bossWebBasePath}/boss_1_alpine.png"),
            new(2, "Alpaca Chief", $"{bossWebBasePath}/boss_2_alpaca.png"),
            new(3, "Bull Titan", $"{bossWebBasePath}/boss_3_bull.png"),
            new(4, "Giraffe Sage", $"{bossWebBasePath}/boss_4_giraffe.png"),
            new(5, "Snow Leopard", $"{bossWebBasePath}/boss_5_leopard.png"),
            new(6, "Mountain Fox", $"{bossWebBasePath}/boss_6_fox.png"),
            new(7, "Alpha Wolf", $"{bossWebBasePath}/boss_7_wolf.png"),
            new(8, "Harpy", $"{bossWebBasePath}/boss_8_harpy.png", PlacementType.Aerial),
            new(9, "Mountain Gorilla", $"{bossWebBasePath}/boss_9_gorilla.png"),
            new(10, "Stone Golem", $"{bossWebBasePath}/boss_10_golem.png")
        };

        SeedBosses(bossSeeds, bossStats, RegionType.Mountains, existingBossStages, enemies);
    }

    private static Dictionary<int, BossStats> GetMountainsBossStats()
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

    private static void SeedSnowyNormals(HashSet<string> existingNormalNames, List<StageEnemy> enemies)
    {
        var basePath = "/sprites/games/my-tuno/enemies/snowy";
        var normals = new List<(string Name, int HP, int Power, int Speed, int Defense, PlacementType Placement)>
        {
            ("Wolf", 65, 12, 8, 5, PlacementType.Terrestrial),
            ("Fox", 45, 10, 11, 3, PlacementType.Terrestrial),
            ("Bear", 90, 14, 4, 9, PlacementType.Terrestrial),
            ("Owl", 40, 9, 12, 2, PlacementType.Aerial),
            ("Panda", 85, 11, 5, 10, PlacementType.Terrestrial),
            ("Penguin", 50, 8, 7, 6, PlacementType.Terrestrial),
            ("Rabbit", 35, 7, 14, 2, PlacementType.Terrestrial),
            ("Reindeer", 75, 10, 8, 7, PlacementType.Terrestrial),
            ("Snowman", 70, 8, 3, 12, PlacementType.Terrestrial),
            ("Mammoth", 100, 13, 3, 11, PlacementType.Terrestrial)
        };

        foreach (var (name, hp, power, speed, defense, placement) in normals)
        {
            if (!existingNormalNames.Contains($"Snowy:{name}"))
            {
                enemies.Add(StageEnemy.Create(
                    name: name,
                    type: EnemyType.Normal,
                    region: RegionType.Snowy,
                    baseHP: hp,
                    basePower: power,
                    baseSpeed: speed,
                    baseDefense: defense,
                    spritePath: $"{basePath}/{name.ToLowerInvariant()}.png",
                    placement: placement
                ));
            }
        }
    }

    private static void SeedSnowyBosses(HashSet<int> existingBossStages, List<StageEnemy> enemies)
    {
        var bossWebBasePath = "/sprites/games/my-tuno/enemies/snowy";
        var bossStats = GetSnowyBossStats();
        var bossSeeds = new List<BossSeedInfo>
        {
            new(1, "Ice Golem", $"{bossWebBasePath}/boss_1_golem.png"),
            new(2, "Frost Snowman", $"{bossWebBasePath}/boss_2_snoman.png"),
            new(3, "Winter Owl", $"{bossWebBasePath}/boss_3_owl.png", PlacementType.Aerial),
            new(4, "Panda Warlord", $"{bossWebBasePath}/boss_4_panda.png"),
            new(5, "Saber Tiger", $"{bossWebBasePath}/boss_5_tiger.png"),
            new(6, "Emperor Penguin", $"{bossWebBasePath}/boss_6_penguin.png"),
            new(7, "Yeti", $"{bossWebBasePath}/boss_7_yeti.png"),
            new(8, "Dire Wolf", $"{bossWebBasePath}/boss_8_direwolf.png"),
            new(9, "Ancient Mammoth", $"{bossWebBasePath}/boss_9_mammoth.png"),
            new(10, "Frost Drake", $"{bossWebBasePath}/boss_10_drake.png")
        };

        SeedBosses(bossSeeds, bossStats, RegionType.Snowy, existingBossStages, enemies);
    }

    private static Dictionary<int, BossStats> GetSnowyBossStats()
    {
        return new Dictionary<int, BossStats>
        {
            { 1, new BossStats(650, 26, 5, 20, 0.10, 70, 0.5, 0.2) },
            { 2, new BossStats(750, 30, 7, 22, 0.12, 80, 0.5, 0.2) },
            { 3, new BossStats(700, 36, 10, 17, 0.15, 90, 0.5, 0.2) },
            { 4, new BossStats(730, 34, 12, 18, 0.14, 100, 0.5, 0.2) },
            { 5, new BossStats(850, 28, 6, 26, 0.08, 110, 0.5, 0.2) },
            { 6, new BossStats(800, 38, 8, 22, 0.12, 120, 0.5, 0.2) },
            { 7, new BossStats(900, 32, 9, 28, 0.10, 130, 0.5, 0.2) },
            { 8, new BossStats(830, 42, 11, 24, 0.18, 140, 0.5, 0.2) },
            { 9, new BossStats(1100, 36, 6, 30, 0.12, 150, 0.5, 0.2) },
            { 10, new BossStats(1500, 48, 10, 38, 0.20, 300, 0.8, 0.4) }
        };
    }

    // ===================
    // TROPICAL REGION (401-500)
    // ===================

    private static void SeedTropicalNormals(HashSet<string> existingNormalNames, List<StageEnemy> enemies)
    {
        var basePath = "/sprites/games/my-tuno/enemies/tropical";
        var normals = new List<(string Name, int HP, int Power, int Speed, int Defense, PlacementType Placement)>
        {
            ("Crab", 75, 12, 5, 9, PlacementType.Terrestrial),
            ("Dolphin", 60, 11, 12, 4, PlacementType.Terrestrial),
            ("Jellyfish", 40, 14, 8, 2, PlacementType.Aerial),
            ("Octopus", 70, 13, 7, 6, PlacementType.Terrestrial),
            ("Seagull", 35, 9, 14, 1, PlacementType.Aerial),
            ("Seal", 80, 10, 6, 8, PlacementType.Terrestrial),
            ("Shark", 85, 16, 9, 5, PlacementType.Terrestrial),
            ("Starfish", 55, 8, 3, 11, PlacementType.Terrestrial),
            ("Tadpole", 30, 7, 13, 1, PlacementType.Terrestrial),
            ("Turtle", 95, 9, 2, 14, PlacementType.Terrestrial)
        };

        foreach (var (name, hp, power, speed, defense, placement) in normals)
        {
            if (!existingNormalNames.Contains($"Tropical:{name}"))
            {
                enemies.Add(StageEnemy.Create(
                    name: name,
                    type: EnemyType.Normal,
                    region: RegionType.Tropical,
                    baseHP: hp,
                    basePower: power,
                    baseSpeed: speed,
                    baseDefense: defense,
                    spritePath: $"{basePath}/{name.ToLowerInvariant()}.png",
                    placement: placement
                ));
            }
        }
    }

    private static void SeedTropicalBosses(HashSet<int> existingBossStages, List<StageEnemy> enemies)
    {
        var bossWebBasePath = "/sprites/games/my-tuno/enemies/tropical";
        var bossStats = GetTropicalBossStats();
        var bossSeeds = new List<BossSeedInfo>
        {
            new(1, "King Crab", $"{bossWebBasePath}/boss_1_crab.png"),
            new(2, "Kraken", $"{bossWebBasePath}/boss_2_kraken.png"),
            new(3, "Great White", $"{bossWebBasePath}/boss_3_shark.png"),
            new(4, "Manta Ray", $"{bossWebBasePath}/boss_4_manta.png"),
            new(5, "Storm Eel", $"{bossWebBasePath}/boss_5_eel.png"),
            new(6, "Sea Serpent", $"{bossWebBasePath}/boss_6_snake.png"),
            new(7, "Jellyfish Queen", $"{bossWebBasePath}/boss_7_jellyfish.png"),
            new(8, "Ancient Turtle", $"{bossWebBasePath}/boss_8_turtle.png"),
            new(9, "Siren", $"{bossWebBasePath}/boss_9_siren.png"),
            new(10, "Leviathan", $"{bossWebBasePath}/boss_10_leviathan.png")
        };

        SeedBosses(bossSeeds, bossStats, RegionType.Tropical, existingBossStages, enemies);
    }

    private static Dictionary<int, BossStats> GetTropicalBossStats()
    {
        return new Dictionary<int, BossStats>
        {
            { 1, new BossStats(700, 28, 5, 22, 0.10, 80, 0.5, 0.2) },
            { 2, new BossStats(800, 32, 7, 24, 0.12, 90, 0.5, 0.2) },
            { 3, new BossStats(750, 38, 10, 19, 0.15, 100, 0.5, 0.2) },
            { 4, new BossStats(780, 36, 12, 20, 0.14, 110, 0.5, 0.2) },
            { 5, new BossStats(900, 30, 6, 28, 0.08, 120, 0.5, 0.2) },
            { 6, new BossStats(850, 40, 8, 24, 0.12, 130, 0.5, 0.2) },
            { 7, new BossStats(950, 34, 9, 30, 0.10, 140, 0.5, 0.2) },
            { 8, new BossStats(880, 44, 11, 26, 0.18, 150, 0.5, 0.2) },
            { 9, new BossStats(1150, 38, 6, 32, 0.12, 160, 0.5, 0.2) },
            { 10, new BossStats(1600, 50, 10, 40, 0.20, 350, 0.8, 0.4) }
        };
    }

    // ===================
    // CAVERNS REGION (501-600)
    // ===================

    private static void SeedCavernsNormals(HashSet<string> existingNormalNames, List<StageEnemy> enemies)
    {
        var basePath = "/sprites/games/my-tuno/enemies/caverns";
        var normals = new List<(string Name, int HP, int Power, int Speed, int Defense, PlacementType Placement)>
        {
            ("Armadillo", 85, 11, 4, 12, PlacementType.Terrestrial),
            ("Bat", 40, 12, 13, 2, PlacementType.Aerial),
            ("Beetle", 70, 10, 5, 10, PlacementType.Terrestrial),
            ("Imp", 50, 15, 11, 3, PlacementType.Terrestrial),
            ("Lizard", 60, 13, 9, 5, PlacementType.Terrestrial),
            ("Rat", 45, 11, 12, 3, PlacementType.Terrestrial),
            ("Shrimp", 55, 9, 8, 7, PlacementType.Terrestrial),
            ("Slug", 90, 7, 2, 14, PlacementType.Terrestrial),
            ("Spider", 50, 14, 10, 4, PlacementType.Terrestrial),
            ("Worm", 65, 8, 3, 11, PlacementType.Terrestrial)
        };

        foreach (var (name, hp, power, speed, defense, placement) in normals)
        {
            if (!existingNormalNames.Contains($"Caverns:{name}"))
            {
                enemies.Add(StageEnemy.Create(
                    name: name,
                    type: EnemyType.Normal,
                    region: RegionType.Caverns,
                    baseHP: hp,
                    basePower: power,
                    baseSpeed: speed,
                    baseDefense: defense,
                    spritePath: $"{basePath}/{name.ToLowerInvariant()}.png",
                    placement: placement
                ));
            }
        }
    }

    private static void SeedCavernsBosses(HashSet<int> existingBossStages, List<StageEnemy> enemies)
    {
        var bossWebBasePath = "/sprites/games/my-tuno/enemies/caverns";
        var bossStats = GetCavernsBossStats();
        var bossSeeds = new List<BossSeedInfo>
        {
            new(1, "Cave Spider", $"{bossWebBasePath}/boss_1_spider.png"),
            new(2, "Stone Golem", $"{bossWebBasePath}/boss_2_golem.png"),
            new(3, "Fungus Lord", $"{bossWebBasePath}/boss_3_fungus.png"),
            new(4, "Wyrm", $"{bossWebBasePath}/boss_4_wyrmm.png"),
            new(5, "Vampire Bat", $"{bossWebBasePath}/boss_5_bat.png", PlacementType.Aerial),
            new(6, "Giant Isopod", $"{bossWebBasePath}/boss_6_isopod.png"),
            new(7, "Crystal Beetle", $"{bossWebBasePath}/boss_7_beetle.png"),
            new(8, "Iron Armadillo", $"{bossWebBasePath}/boss_8_armadillo.png"),
            new(9, "Cavern Hydra", $"{bossWebBasePath}/boss_9_hydra.png"),
            new(10, "Basilisk", $"{bossWebBasePath}/boss_10_basilisk.png")
        };

        SeedBosses(bossSeeds, bossStats, RegionType.Caverns, existingBossStages, enemies);
    }

    private static Dictionary<int, BossStats> GetCavernsBossStats()
    {
        return new Dictionary<int, BossStats>
        {
            { 1, new BossStats(750, 30, 5, 24, 0.10, 90, 0.5, 0.2) },
            { 2, new BossStats(850, 34, 7, 26, 0.12, 100, 0.5, 0.2) },
            { 3, new BossStats(800, 40, 10, 21, 0.15, 110, 0.5, 0.2) },
            { 4, new BossStats(830, 38, 12, 22, 0.14, 120, 0.5, 0.2) },
            { 5, new BossStats(950, 32, 6, 30, 0.08, 130, 0.5, 0.2) },
            { 6, new BossStats(900, 42, 8, 26, 0.12, 140, 0.5, 0.2) },
            { 7, new BossStats(1000, 36, 9, 32, 0.10, 150, 0.5, 0.2) },
            { 8, new BossStats(930, 46, 11, 28, 0.18, 160, 0.5, 0.2) },
            { 9, new BossStats(1200, 40, 6, 34, 0.12, 170, 0.5, 0.2) },
            { 10, new BossStats(1700, 52, 10, 42, 0.20, 400, 0.8, 0.4) }
        };
    }

    // ===================
    // RUINS REGION (801-900)
    // ===================

    private static void SeedRuinsNormals(HashSet<string> existingNormalNames, List<StageEnemy> enemies)
    {
        var basePath = "/sprites/games/my-tuno/enemies/ruins";
        var normals = new List<(string Name, int HP, int Power, int Speed, int Defense, PlacementType Placement)>
        {
            ("Bat", 50, 14, 13, 3, PlacementType.Aerial),
            ("Ghost", 45, 16, 11, 2, PlacementType.Aerial),
            ("Hound", 75, 15, 9, 7, PlacementType.Terrestrial),
            ("Lantern", 55, 13, 7, 8, PlacementType.Aerial),
            ("Mummy", 95, 12, 3, 15, PlacementType.Terrestrial),
            ("Plant", 80, 14, 4, 12, PlacementType.Terrestrial),
            ("Rat", 50, 13, 12, 4, PlacementType.Terrestrial),
            ("Raven", 40, 11, 14, 2, PlacementType.Aerial),
            ("Scarab", 65, 10, 8, 10, PlacementType.Terrestrial),
            ("Trap", 100, 8, 1, 18, PlacementType.Terrestrial)
        };

        foreach (var (name, hp, power, speed, defense, placement) in normals)
        {
            if (!existingNormalNames.Contains($"Ruins:{name}"))
            {
                enemies.Add(StageEnemy.Create(
                    name: name,
                    type: EnemyType.Normal,
                    region: RegionType.Ruins,
                    baseHP: hp,
                    basePower: power,
                    baseSpeed: speed,
                    baseDefense: defense,
                    spritePath: $"{basePath}/{name.ToLowerInvariant()}.png",
                    placement: placement
                ));
            }
        }
    }

    private static void SeedRuinsBosses(HashSet<int> existingBossStages, List<StageEnemy> enemies)
    {
        var bossWebBasePath = "/sprites/games/my-tuno/enemies/ruins";
        var bossStats = GetRuinsBossStats();
        var bossSeeds = new List<BossSeedInfo>
        {
            new(1, "Sentinel", $"{bossWebBasePath}/boss_1_sentinel.png"),
            new(2, "Pharaoh", $"{bossWebBasePath}/boss_2_pharao.png"),
            new(3, "Medusa", $"{bossWebBasePath}/boss_3_medusa.png"),
            new(4, "Warden", $"{bossWebBasePath}/boss_4_warden.png"),
            new(5, "Hydra", $"{bossWebBasePath}/boss_5_hydra.png"),
            new(6, "Death Knight", $"{bossWebBasePath}/boss_6_knight.png"),
            new(7, "Ancient Golem", $"{bossWebBasePath}/boss_7_golem.png"),
            new(8, "Colossus", $"{bossWebBasePath}/boss_8_colossus.png"),
            new(9, "Guardian", $"{bossWebBasePath}/boss_9_guardian.png"),
            new(10, "Living Statue", $"{bossWebBasePath}/boss_10_statue.png")
        };

        SeedBosses(bossSeeds, bossStats, RegionType.Ruins, existingBossStages, enemies);
    }

    private static Dictionary<int, BossStats> GetRuinsBossStats()
    {
        return new Dictionary<int, BossStats>
        {
            { 1, new BossStats(900, 36, 5, 30, 0.10, 120, 0.5, 0.2) },
            { 2, new BossStats(1000, 40, 7, 32, 0.12, 130, 0.5, 0.2) },
            { 3, new BossStats(950, 46, 10, 27, 0.15, 140, 0.5, 0.2) },
            { 4, new BossStats(980, 44, 12, 28, 0.14, 150, 0.5, 0.2) },
            { 5, new BossStats(1100, 38, 6, 36, 0.08, 160, 0.5, 0.2) },
            { 6, new BossStats(1050, 48, 8, 32, 0.12, 170, 0.5, 0.2) },
            { 7, new BossStats(1150, 42, 9, 38, 0.10, 180, 0.5, 0.2) },
            { 8, new BossStats(1080, 52, 11, 34, 0.18, 190, 0.5, 0.2) },
            { 9, new BossStats(1350, 46, 6, 40, 0.12, 200, 0.5, 0.2) },
            { 10, new BossStats(1900, 58, 10, 48, 0.20, 500, 0.8, 0.4) }
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
