using Microsoft.EntityFrameworkCore;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data;

/// <summary>
/// Truncates and re-seeds the StageEnemies table with all enemies and bosses.
/// TODO: Remove after next release (Feb 2026) — one-time full re-seed to update sprites and add Desert/Volcanic/Dark regions.
/// After it runs once in prod, delete this file and the call in SeedData.cs.
/// </summary>
public static partial class SeedData
{
    public static async Task SeedStageEnemiesAsync(ApplicationDbContext dbContext)
    {
        Console.WriteLine("Truncating and re-seeding stage enemies...");

        // Truncate: remove ALL existing stage enemies so we can re-insert with updated sprites
        var existing = await dbContext.StageEnemies.ToListAsync();
        if (existing.Count > 0)
        {
            dbContext.StageEnemies.RemoveRange(existing);
            await dbContext.SaveChangesAsync();
            Console.WriteLine($"  Removed {existing.Count} existing stage enemies.");
        }

        var enemies = new List<StageEnemy>();

        // =====================
        // FOREST (1-100)
        // =====================
        SeedForestNormals(enemies);
        SeedForestBosses(enemies);

        // =====================
        // SWAMP (101-200)
        // =====================
        SeedSwampNormals(enemies);
        SeedSwampBosses(enemies);

        // =====================
        // MOUNTAINS (201-300)
        // =====================
        SeedMountainsNormals(enemies);
        SeedMountainsBosses(enemies);

        // =====================
        // SNOWY (301-400)
        // =====================
        SeedSnowyNormals(enemies);
        SeedSnowyBosses(enemies);

        // =====================
        // TROPICAL (401-500)
        // =====================
        SeedTropicalNormals(enemies);
        SeedTropicalBosses(enemies);

        // =====================
        // CAVERNS (501-600)
        // =====================
        SeedCavernsNormals(enemies);
        SeedCavernsBosses(enemies);

        // =====================
        // DESERT (601-700)
        // =====================
        SeedDesertNormals(enemies);
        SeedDesertBosses(enemies);

        // =====================
        // VOLCANIC (701-800)
        // =====================
        SeedVolcanicNormals(enemies);
        SeedVolcanicBosses(enemies);

        // =====================
        // RUINS (801-900)
        // =====================
        SeedRuinsNormals(enemies);
        SeedRuinsBosses(enemies);

        // =====================
        // DARK (901-1000)
        // =====================
        SeedDarkNormals(enemies);
        SeedDarkBosses(enemies);

        // NOTE: Void (1001+) has no seeded enemies.
        // Void draws randomly from ALL regions at runtime via StageEnemyRepository.

        await dbContext.StageEnemies.AddRangeAsync(enemies);
        await dbContext.SaveChangesAsync();

        var bosses = enemies.Count(e => e.Type == EnemyType.Boss);
        Console.WriteLine($"Seeded {enemies.Count} stage enemies ({bosses} bosses, {enemies.Count - bosses} normal)");
    }

    // ────────────────────────────────────────────
    // Helper: add normals for a region
    // ────────────────────────────────────────────
    private static void AddNormals(
        List<StageEnemy> enemies,
        RegionType region,
        string basePath,
        List<(string Name, int HP, int Power, int Speed, int Defense, PlacementType Placement)> normals)
    {
        foreach (var (name, hp, power, speed, defense, placement) in normals)
        {
            enemies.Add(StageEnemy.Create(
                name: name,
                type: EnemyType.Normal,
                region: region,
                baseHP: hp,
                basePower: power,
                baseSpeed: speed,
                baseDefense: defense,
                spritePath: $"{basePath}/{name.ToLowerInvariant()}.png",
                placement: placement
            ));
        }
    }

    // ────────────────────────────────────────────
    // Helper: add bosses for a region
    // ────────────────────────────────────────────
    private static void AddBosses(
        List<StageEnemy> enemies,
        RegionType region,
        int stageOffset,
        List<BossSeedInfo> bossSeeds,
        Dictionary<int, BossStats> bossStats)
    {
        foreach (var boss in bossSeeds)
        {
            var bossStageNumber = stageOffset + boss.Index * 10;
            var stats = bossStats.TryGetValue(boss.Index, out var s) ? s : BossStats.Default;

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

    // ════════════════════════════════════════════
    //  FOREST (1-100)
    // ════════════════════════════════════════════

    private static void SeedForestNormals(List<StageEnemy> enemies)
    {
        var p = "/sprites/games/my-tuno/enemies/forest";
        AddNormals(enemies, RegionType.Forest, p, new()
        {
            ("Bee",      30,  6, 10, 1, PlacementType.Aerial),
            ("Beetle",   60,  7,  3, 8, PlacementType.Aerial),
            ("Cheetah",  55, 13, 12, 3, PlacementType.Terrestrial),
            ("Eagle",    55, 11,  9, 3, PlacementType.Aerial),
            ("Toucan",    55, 11,  9, 3, PlacementType.Aerial),
            ("Monkey",   45,  9,  9, 3, PlacementType.Terrestrial),
            ("Panther",  65, 14,  8, 4, PlacementType.Terrestrial),
            ("Snake",    45,  9,  7, 2, PlacementType.Terrestrial),
            ("Spider",   40, 12,  8, 2, PlacementType.Terrestrial),
            ("Stag",     75,  9,  7, 6, PlacementType.Terrestrial),
        });
    }

    private static void SeedForestBosses(List<StageEnemy> enemies)
    {
        var p = "/sprites/games/my-tuno/enemies/forest";
        AddBosses(enemies, RegionType.Forest, 0, new()
        {
            new(1,  "Bear",      $"{p}/boss_1_bear.png"),
            new(2,  "Tiger",     $"{p}/boss_2_tiger.png"),
            new(3,  "Jaguar",    $"{p}/boss_3_jaguar.png"),
            new(4,  "Falcon",    $"{p}/boss_4_falcon.png", PlacementType.Aerial),
            new(5,  "Leecher",   $"{p}/boss_5_leecher.png"),
            new(6,  "Python",    $"{p}/boss_6_python.png"),
            new(7,  "Centipede", $"{p}/boss_7_centipede.png"),
            new(8,  "Mantis",    $"{p}/boss_8_mantis.png"),
            new(9,  "Gorilla",   $"{p}/boss_9_gorilla.png"),
            new(10, "Lion",      $"{p}/boss_10_lion.png"),
        }, GetForestBossStats());
    }

    private static Dictionary<int, BossStats> GetForestBossStats() => new()
    {
        { 1,  new(165, 20,  5, 15, 0.10,  50, 0.5, 0.2) },
        { 2,  new(180, 25,  7, 18, 0.12,  60, 0.5, 0.2) },
        { 3,  new(170, 30, 10, 12, 0.15,  70, 0.5, 0.2) },
        { 4,  new(175, 28, 12, 14, 0.14,  80, 0.5, 0.2) },
        { 5,  new(210, 22,  6, 20, 0.08,  90, 0.5, 0.2) },
        { 6,  new(195, 32,  8, 16, 0.12, 100, 0.5, 0.2) },
        { 7,  new(220, 26,  9, 22, 0.10, 110, 0.5, 0.2) },
        { 8,  new(200, 35, 11, 18, 0.18, 120, 0.5, 0.2) },
        { 9,  new(250, 30,  6, 25, 0.12, 130, 0.5, 0.2) },
        { 10, new(300, 40, 10, 30, 0.20, 200, 0.8, 0.4) },
    };

    // ════════════════════════════════════════════
    //  SWAMP (101-200)
    // ════════════════════════════════════════════

    private static void SeedSwampNormals(List<StageEnemy> enemies)
    {
        var p = "/sprites/games/my-tuno/enemies/swamp";
        AddNormals(enemies, RegionType.Swamp, p, new()
        {
            ("Crab",       65, 10,  4, 8, PlacementType.Terrestrial),
            ("Crocodile",  90, 14,  3, 7, PlacementType.Terrestrial),
            ("Crow",       40,  9, 11, 2, PlacementType.Aerial),
            ("Frog",       35,  7,  9, 3, PlacementType.Terrestrial),
            ("Leech",      45, 12,  6, 2, PlacementType.Terrestrial),
            ("Mosquito",   30,  8, 13, 1, PlacementType.Aerial),
            ("Salamander", 55, 11,  7, 5, PlacementType.Terrestrial),
            ("Slime",      80,  6,  2,10, PlacementType.Terrestrial),
            ("Snake",      50, 10,  8, 3, PlacementType.Terrestrial),
            ("Stalker",    60, 13, 10, 4, PlacementType.Terrestrial),
        });
    }

    private static void SeedSwampBosses(List<StageEnemy> enemies)
    {
        var p = "/sprites/games/my-tuno/enemies/swamp";
        AddBosses(enemies, RegionType.Swamp, 100, new()
        {
            new(1,  "Frog King",     $"{p}/boss_1_frog.png"),
            new(2,  "Pelican",       $"{p}/boss_2_pelican.png", PlacementType.Aerial),
            new(3,  "Leech Lord",    $"{p}/boss_3_leech.png"),
            new(4,  "Hydra",         $"{p}/boss_4_hydra.png"),
            new(5,  "Anaconda",      $"{p}/boss_5_anaconda.png"),
            new(6,  "Crayfish",      $"{p}/boss_6_crayfish.png"),
            new(7,  "Darner",        $"{p}/boss_7_darner.png"),
            new(8,  "Hippopotamus",  $"{p}/boss_8_hippopotamus.png"),
            new(9,  "Troll",         $"{p}/boss_9_troll.png"),
            new(10, "Aligator",      $"{p}/boss_10_aligator.png"),
        }, GetSwampBossStats());
    }

    private static Dictionary<int, BossStats> GetSwampBossStats() => new()
    {
        { 1,  new(170, 24,  5, 18, 0.10,  60, 0.5, 0.2) },
        { 2,  new(185, 28,  7, 20, 0.12,  70, 0.5, 0.2) },
        { 3,  new(175, 34, 10, 15, 0.15,  80, 0.5, 0.2) },
        { 4,  new(180, 32, 12, 16, 0.14,  90, 0.5, 0.2) },
        { 5,  new(215, 26,  6, 24, 0.08, 100, 0.5, 0.2) },
        { 6,  new(200, 36,  8, 20, 0.12, 110, 0.5, 0.2) },
        { 7,  new(230, 30,  9, 26, 0.10, 120, 0.5, 0.2) },
        { 8,  new(210, 40, 11, 22, 0.18, 130, 0.5, 0.2) },
        { 9,  new(260, 34,  6, 28, 0.12, 140, 0.5, 0.2) },
        { 10, new(320, 45, 10, 35, 0.20, 250, 0.8, 0.4) },
    };

    // ════════════════════════════════════════════
    //  MOUNTAINS (201-300)
    // ════════════════════════════════════════════

    private static void SeedMountainsNormals(List<StageEnemy> enemies)
    {
        var p = "/sprites/games/my-tuno/enemies/mountains";
        AddNormals(enemies, RegionType.Mountains, p, new()
        {
            ("Bear",     80, 13,  5, 8, PlacementType.Terrestrial),
            ("Boar",     70, 10,  4, 5, PlacementType.Terrestrial),
            ("Elephant", 95, 11,  3,10, PlacementType.Terrestrial),
            ("Goat",     70,  9,  6, 7, PlacementType.Terrestrial),
            ("Fox",    60, 13,  8, 3, PlacementType.Terrestrial),
            ("Hyena",    60, 13,  8, 3, PlacementType.Terrestrial),
            ("Pigeon",   35,  7, 13, 1, PlacementType.Aerial),
            ("Rhino",    85, 15,  4, 9, PlacementType.Terrestrial),
            ("Vulture",  40, 11, 12, 2, PlacementType.Aerial),
            ("Wolf",     55, 12,  9, 4, PlacementType.Terrestrial),
        });
    }

    private static void SeedMountainsBosses(List<StageEnemy> enemies)
    {
        var p = "/sprites/games/my-tuno/enemies/mountains";
        AddBosses(enemies, RegionType.Mountains, 200, new()
        {
            new(1,  "Alpine Guardian",  $"{p}/boss_1_alpine.png"),
            new(2,  "Hawk",             $"{p}/boss_2_hawk.png", PlacementType.Aerial),
            new(3,  "Alpaca Chief",     $"{p}/boss_3_alpaca.png"),
            new(4,  "Mountain Fox",     $"{p}/boss_4_fox.png"),
            new(5,  "Giraffe Sage",     $"{p}/boss_5_giraffe.png"),
            new(6,  "Harpy",            $"{p}/boss_6_harpy.png", PlacementType.Aerial),
            new(7,  "Alpha Wolf",       $"{p}/boss_7_wolf.png"),
            new(8,  "Bull Titan",       $"{p}/boss_8_bull.png"),
            new(9,  "Mountain Gorilla", $"{p}/boss_9_gorilla.png"),
            new(10, "Stone Golem",      $"{p}/boss_10_golem.png"),
        }, GetMountainsBossStats());
    }

    private static Dictionary<int, BossStats> GetMountainsBossStats() => new()
    {
        { 1,  new(185, 24,  5, 18, 0.10,  60, 0.5, 0.2) },
        { 2,  new(200, 28,  7, 20, 0.12,  70, 0.5, 0.2) },
        { 3,  new(190, 34, 10, 15, 0.15,  80, 0.5, 0.2) },
        { 4,  new(195, 32, 12, 16, 0.14,  90, 0.5, 0.2) },
        { 5,  new(235, 26,  6, 24, 0.08, 100, 0.5, 0.2) },
        { 6,  new(220, 36,  8, 20, 0.12, 110, 0.5, 0.2) },
        { 7,  new(250, 30,  9, 26, 0.10, 120, 0.5, 0.2) },
        { 8,  new(230, 40, 11, 22, 0.18, 130, 0.5, 0.2) },
        { 9,  new(280, 34,  6, 28, 0.12, 140, 0.5, 0.2) },
        { 10, new(340, 45, 10, 35, 0.20, 250, 0.8, 0.4) },
    };

    // ════════════════════════════════════════════
    //  SNOWY (301-400)
    // ════════════════════════════════════════════

    private static void SeedSnowyNormals(List<StageEnemy> enemies)
    {
        var p = "/sprites/games/my-tuno/enemies/snowy";
        AddNormals(enemies, RegionType.Snowy, p, new()
        {
            ("Bear",     90, 14,  4, 9, PlacementType.Terrestrial),
            ("Fox",      45, 10, 11, 3, PlacementType.Terrestrial),
            ("Mammoth", 100, 13,  3,11, PlacementType.Terrestrial),
            ("Owl",      40,  9, 12, 2, PlacementType.Aerial),
            ("Penguin",  50,  8,  7, 6, PlacementType.Terrestrial),
            ("Rabbit",   35,  7, 14, 2, PlacementType.Terrestrial),
            ("Reindeer", 75, 10,  8, 7, PlacementType.Terrestrial),
            ("Snowman",  70,  8,  3,12, PlacementType.Terrestrial),
            ("Wolf",     65, 12,  8, 5, PlacementType.Terrestrial),
            ("Yeti",     95, 15,  4,10, PlacementType.Terrestrial),
        });
    }

    private static void SeedSnowyBosses(List<StageEnemy> enemies)
    {
        var p = "/sprites/games/my-tuno/enemies/snowy";
        AddBosses(enemies, RegionType.Snowy, 300, new()
        {
            new(1,  "Ice Golem",       $"{p}/boss_1_golem.png"),
            new(2,  "Frost Snowman",   $"{p}/boss_2_snoman.png"),
            new(3,  "Winter Owl",      $"{p}/boss_3_owl.png", PlacementType.Aerial),
            new(4,  "Panda Warlord",   $"{p}/boss_4_panda.png"),
            new(5,  "Saber Tiger",     $"{p}/boss_5_tiger.png"),
            new(6,  "Emperor Penguin", $"{p}/boss_6_penguin.png"),
            new(7,  "Yeti",            $"{p}/boss_7_yeti.png"),
            new(8,  "Dire Wolf",       $"{p}/boss_8_direwolf.png"),
            new(9,  "Ancient Mammoth", $"{p}/boss_9_mammoth.png"),
            new(10, "Frost Drake",     $"{p}/boss_10_drake.png"),
        }, GetSnowyBossStats());
    }

    private static Dictionary<int, BossStats> GetSnowyBossStats() => new()
    {
        { 1,  new(195, 26,  5, 20, 0.10,  70, 0.5, 0.2) },
        { 2,  new(215, 30,  7, 22, 0.12,  80, 0.5, 0.2) },
        { 3,  new(200, 36, 10, 17, 0.15,  90, 0.5, 0.2) },
        { 4,  new(210, 34, 12, 18, 0.14, 100, 0.5, 0.2) },
        { 5,  new(250, 28,  6, 26, 0.08, 110, 0.5, 0.2) },
        { 6,  new(235, 38,  8, 22, 0.12, 120, 0.5, 0.2) },
        { 7,  new(265, 32,  9, 28, 0.10, 130, 0.5, 0.2) },
        { 8,  new(245, 42, 11, 24, 0.18, 140, 0.5, 0.2) },
        { 9,  new(300, 36,  6, 30, 0.12, 150, 0.5, 0.2) },
        { 10, new(365, 48, 10, 38, 0.20, 300, 0.8, 0.4) },
    };

    // ════════════════════════════════════════════
    //  TROPICAL (401-500)
    // ════════════════════════════════════════════

    private static void SeedTropicalNormals(List<StageEnemy> enemies)
    {
        var p = "/sprites/games/my-tuno/enemies/tropical";
        AddNormals(enemies, RegionType.Tropical, p, new()
        {
            ("Crab",      75, 12,  5, 9, PlacementType.Terrestrial),
            ("Dolphin",   60, 11, 12, 4, PlacementType.Terrestrial),
            ("Duck",      40,  8, 11, 2, PlacementType.Aerial),
            ("Jellyfish", 40, 14,  8, 2, PlacementType.Aerial),
            ("Octopus",   70, 13,  7, 6, PlacementType.Terrestrial),
            ("Seagull",   35,  9, 14, 1, PlacementType.Aerial),
            ("Seal",      80, 10,  6, 8, PlacementType.Terrestrial),
            ("Shark",     85, 16,  9, 5, PlacementType.Terrestrial),
            ("Starfish",  55,  8,  3,11, PlacementType.Terrestrial),
            ("Turtle",    95,  9,  2,14, PlacementType.Terrestrial),
        });
    }

    private static void SeedTropicalBosses(List<StageEnemy> enemies)
    {
        var p = "/sprites/games/my-tuno/enemies/tropical";
        AddBosses(enemies, RegionType.Tropical, 400, new()
        {
            new(1,  "King Crab",       $"{p}/boss_1_crab.png"),
            new(2,  "Kraken",          $"{p}/boss_2_kraken.png"),
            new(3,  "Great White",     $"{p}/boss_3_shark.png"),
            new(4,  "Manta Ray",       $"{p}/boss_4_manta.png"),
            new(5,  "Storm Eel",       $"{p}/boss_5_eel.png"),
            new(6,  "Sea Serpent",     $"{p}/boss_6_snake.png"),
            new(7,  "Jellyfish Queen", $"{p}/boss_7_jellyfish.png"),
            new(8,  "Ancient Turtle",  $"{p}/boss_8_turtle.png"),
            new(9,  "Siren",           $"{p}/boss_9_siren.png"),
            new(10, "Leviathan",       $"{p}/boss_10_leviathan.png"),
        }, GetTropicalBossStats());
    }

    private static Dictionary<int, BossStats> GetTropicalBossStats() => new()
    {
        { 1,  new(190, 28,  5, 22, 0.10,  80, 0.5, 0.2) },
        { 2,  new(205, 32,  7, 24, 0.12,  90, 0.5, 0.2) },
        { 3,  new(195, 38, 10, 19, 0.15, 100, 0.5, 0.2) },
        { 4,  new(200, 36, 12, 20, 0.14, 110, 0.5, 0.2) },
        { 5,  new(240, 30,  6, 28, 0.08, 120, 0.5, 0.2) },
        { 6,  new(225, 40,  8, 24, 0.12, 130, 0.5, 0.2) },
        { 7,  new(255, 34,  9, 30, 0.10, 140, 0.5, 0.2) },
        { 8,  new(235, 44, 11, 26, 0.18, 150, 0.5, 0.2) },
        { 9,  new(290, 38,  6, 32, 0.12, 160, 0.5, 0.2) },
        { 10, new(350, 50, 10, 40, 0.20, 350, 0.8, 0.4) },
    };

    // ════════════════════════════════════════════
    //  CAVERNS (501-600)
    // ════════════════════════════════════════════

    private static void SeedCavernsNormals(List<StageEnemy> enemies)
    {
        var p = "/sprites/games/my-tuno/enemies/caverns";
        AddNormals(enemies, RegionType.Caverns, p, new()
        {
            ("Armadillo", 85, 11,  4,12, PlacementType.Terrestrial),
            ("Bat",       40, 12, 13, 2, PlacementType.Aerial),
            ("Beetle",    70, 10,  5,10, PlacementType.Terrestrial),
            ("Imp",       50, 15, 11, 3, PlacementType.Terrestrial),
            ("Lizard",    60, 13,  9, 5, PlacementType.Terrestrial),
            ("Rat",       45, 11, 12, 3, PlacementType.Terrestrial),
            ("Shrimp",    55,  9,  8, 7, PlacementType.Terrestrial),
            ("Slug",      90,  7,  2,14, PlacementType.Terrestrial),
            ("Spider",    50, 14, 10, 4, PlacementType.Terrestrial),
            ("Worm",      65,  8,  3,11, PlacementType.Terrestrial),
        });
    }

    private static void SeedCavernsBosses(List<StageEnemy> enemies)
    {
        var p = "/sprites/games/my-tuno/enemies/caverns";
        AddBosses(enemies, RegionType.Caverns, 500, new()
        {
            new(1,  "Cave Spider",    $"{p}/boss_1_spider.png"),
            new(2,  "Stone Golem",    $"{p}/boss_2_golem.png"),
            new(3,  "Fungus Lord",    $"{p}/boss_3_fungus.png"),
            new(4,  "Wyrm",           $"{p}/boss_4_wyrmm.png"),
            new(5,  "Vampire Bat",    $"{p}/boss_5_bat.png", PlacementType.Aerial),
            new(6,  "Giant Isopod",   $"{p}/boss_6_isopod.png"),
            new(7,  "Crystal Beetle", $"{p}/boss_7_beetle.png"),
            new(8,  "Iron Armadillo", $"{p}/boss_8_armadillo.png"),
            new(9,  "Cavern Hydra",   $"{p}/boss_9_hydra.png"),
            new(10, "Basilisk",       $"{p}/boss_10_basilisk.png"),
        }, GetCavernsBossStats());
    }

    private static Dictionary<int, BossStats> GetCavernsBossStats() => new()
    {
        { 1,  new(185, 30,  5, 24, 0.10,  90, 0.5, 0.2) },
        { 2,  new(200, 34,  7, 26, 0.12, 100, 0.5, 0.2) },
        { 3,  new(190, 40, 10, 21, 0.15, 110, 0.5, 0.2) },
        { 4,  new(195, 38, 12, 22, 0.14, 120, 0.5, 0.2) },
        { 5,  new(230, 32,  6, 30, 0.08, 130, 0.5, 0.2) },
        { 6,  new(215, 42,  8, 26, 0.12, 140, 0.5, 0.2) },
        { 7,  new(245, 36,  9, 32, 0.10, 150, 0.5, 0.2) },
        { 8,  new(225, 46, 11, 28, 0.18, 160, 0.5, 0.2) },
        { 9,  new(285, 40,  6, 34, 0.12, 170, 0.5, 0.2) },
        { 10, new(340, 52, 10, 42, 0.20, 400, 0.8, 0.4) },
    };

    // ════════════════════════════════════════════
    //  DESERT (601-700)
    // ════════════════════════════════════════════

    private static void SeedDesertNormals(List<StageEnemy> enemies)
    {
        var p = "/sprites/games/my-tuno/enemies/desert";
        AddNormals(enemies, RegionType.Desert, p, new()
        {
            ("Beetle",   70, 11,  5, 10, PlacementType.Terrestrial),
            ("Cactus",   80,  9,  2, 14, PlacementType.Terrestrial),
            ("Eagle",    45, 12, 12,  2, PlacementType.Aerial),
            ("Lion",     85, 16,  8,  6, PlacementType.Terrestrial),
            ("Lizard",   55, 13, 10,  4, PlacementType.Terrestrial),
            ("Scarab",   60, 10,  7,  9, PlacementType.Terrestrial),
            ("Scorpion", 65, 14,  6,  7, PlacementType.Terrestrial),
            ("Vulture",  40, 11, 13,  2, PlacementType.Aerial),
            ("Wisp",     35, 15, 11,  1, PlacementType.Aerial),
            ("Worm",     90,  8,  2, 12, PlacementType.Terrestrial),
        });
    }

    private static void SeedDesertBosses(List<StageEnemy> enemies)
    {
        var p = "/sprites/games/my-tuno/enemies/desert";
        AddBosses(enemies, RegionType.Desert, 600, new()
        {
            new(1,  "Djinn",         $"{p}/boss_1_djinn.png", PlacementType.Aerial),
            new(2,  "Sand Scorpion", $"{p}/boss_2_scorpion.png"),
            new(3,  "Roc",           $"{p}/boss_3_roc.png", PlacementType.Aerial),
            new(4,  "Persian",       $"{p}/boss_4_persian.png"),
            new(5,  "Vulture King",  $"{p}/boss_5_vulture.png", PlacementType.Aerial),
            new(6,  "Sphinx",        $"{p}/boss_6_sphinx.png"),
            new(7,  "Cactus Titan",  $"{p}/boss_7_cactus.png"),
            new(8,  "Anubis",        $"{p}/boss_8_anubis.png"),
            new(9,  "War Camel",     $"{p}/boss_9_camel.png"),
            new(10, "Sand Worm",     $"{p}/boss_10_worm.png"),
        }, GetDesertBossStats());
    }

    private static Dictionary<int, BossStats> GetDesertBossStats() => new()
    {
        { 1,  new(195, 32,  5, 26, 0.10, 100, 0.5, 0.2) },
        { 2,  new(210, 36,  7, 28, 0.12, 110, 0.5, 0.2) },
        { 3,  new(200, 42, 10, 23, 0.15, 120, 0.5, 0.2) },
        { 4,  new(205, 40, 12, 24, 0.14, 130, 0.5, 0.2) },
        { 5,  new(245, 34,  6, 32, 0.08, 140, 0.5, 0.2) },
        { 6,  new(230, 44,  8, 28, 0.12, 150, 0.5, 0.2) },
        { 7,  new(260, 38,  9, 34, 0.10, 160, 0.5, 0.2) },
        { 8,  new(240, 48, 11, 30, 0.18, 170, 0.5, 0.2) },
        { 9,  new(300, 42,  6, 36, 0.12, 180, 0.5, 0.2) },
        { 10, new(360, 55, 10, 44, 0.20, 450, 0.8, 0.4) },
    };

    // ════════════════════════════════════════════
    //  VOLCANIC (701-800)
    // ════════════════════════════════════════════

    private static void SeedVolcanicNormals(List<StageEnemy> enemies)
    {
        var p = "/sprites/games/my-tuno/enemies/volcanic";
        AddNormals(enemies, RegionType.Volcanic, p, new()
        {
            ("Badger",          55, 14,  9,  5, PlacementType.Terrestrial),
            ("Bat",             45, 13, 13,  2, PlacementType.Aerial),
            ("Beetle",          75, 11,  4, 11, PlacementType.Terrestrial),
            ("Dragon",          95, 18,  7,  8, PlacementType.Aerial),
            ("Drake",           85, 16,  8,  7, PlacementType.Aerial),
            ("Duck",            40,  9, 12,  2, PlacementType.Aerial),
            ("Lizard",          60, 14, 10,  5, PlacementType.Terrestrial),
            ("Salamander",      70, 12,  6,  9, PlacementType.Terrestrial),
            ("TwoHeadedDragon",100, 20,  5, 10, PlacementType.Terrestrial),
            ("Wolf",            65, 15,  9,  6, PlacementType.Terrestrial),
        });
    }

    private static void SeedVolcanicBosses(List<StageEnemy> enemies)
    {
        var p = "/sprites/games/my-tuno/enemies/volcanic";
        AddBosses(enemies, RegionType.Volcanic, 700, new()
        {
            new(1,  "Lava Scorpion",  $"{p}/boss_1_scorpion.png"),
            new(2,  "Fire Snake",     $"{p}/boss_2_snake.png"),
            new(3,  "Magma Rhino",    $"{p}/boss_3_rhino.png"),
            new(4,  "Flame Wisp",     $"{p}/boss_4_wisp.png", PlacementType.Aerial),
            new(5,  "Hell Hound",     $"{p}/boss_5_dog.png"),
            new(6,  "Lava Turtle",    $"{p}/boss_6_turtle.png"),
            new(7,  "Inferno Gorilla",$"{p}/boss_7_gorilla.png"),
            new(8,  "Cerberus",       $"{p}/boss_8_cerebrus.png"),
            new(9,  "Phoenix",        $"{p}/boss_9_phoenix.png", PlacementType.Aerial),
            new(10, "Elder Dragon",   $"{p}/boss_10_dragon.png", PlacementType.Aerial),
        }, GetVolcanicBossStats());
    }

    private static Dictionary<int, BossStats> GetVolcanicBossStats() => new()
    {
        { 1,  new(205, 34,  5, 28, 0.10, 110, 0.5, 0.2) },
        { 2,  new(225, 38,  7, 30, 0.12, 120, 0.5, 0.2) },
        { 3,  new(215, 44, 10, 25, 0.15, 130, 0.5, 0.2) },
        { 4,  new(220, 42, 12, 26, 0.14, 140, 0.5, 0.2) },
        { 5,  new(260, 36,  6, 34, 0.08, 150, 0.5, 0.2) },
        { 6,  new(245, 46,  8, 30, 0.12, 160, 0.5, 0.2) },
        { 7,  new(275, 40,  9, 36, 0.10, 170, 0.5, 0.2) },
        { 8,  new(255, 50, 11, 32, 0.18, 180, 0.5, 0.2) },
        { 9,  new(315, 44,  6, 38, 0.12, 190, 0.5, 0.2) },
        { 10, new(385, 58, 10, 46, 0.20, 500, 0.8, 0.4) },
    };

    // ════════════════════════════════════════════
    //  RUINS (801-900)
    // ════════════════════════════════════════════

    private static void SeedRuinsNormals(List<StageEnemy> enemies)
    {
        var p = "/sprites/games/my-tuno/enemies/ruins";
        AddNormals(enemies, RegionType.Ruins, p, new()
        {
            ("Bat",     50, 14, 13, 3, PlacementType.Aerial),
            ("Ghost",   45, 16, 11, 2, PlacementType.Aerial),
            ("Hound",   75, 15,  9, 7, PlacementType.Terrestrial),
            ("Lantern", 55, 13,  7, 8, PlacementType.Aerial),
            ("Mummy",   95, 12,  3,15, PlacementType.Terrestrial),
            ("Plant",   80, 14,  4,12, PlacementType.Terrestrial),
            ("Rat",     50, 13, 12, 4, PlacementType.Terrestrial),
            ("Raven",   40, 11, 14, 2, PlacementType.Aerial),
            ("Scarab",  65, 10,  8,10, PlacementType.Terrestrial),
            ("Trap",   100,  8,  1,18, PlacementType.Terrestrial),
        });
    }

    private static void SeedRuinsBosses(List<StageEnemy> enemies)
    {
        var p = "/sprites/games/my-tuno/enemies/ruins";
        AddBosses(enemies, RegionType.Ruins, 800, new()
        {
            new(1,  "Sentinel",     $"{p}/boss_1_sentinel.png"),
            new(2,  "Pharaoh",      $"{p}/boss_2_pharao.png"),
            new(3,  "Medusa",       $"{p}/boss_3_medusa.png"),
            new(4,  "Warden",       $"{p}/boss_4_warden.png"),
            new(5,  "Hydra",        $"{p}/boss_5_hydra.png"),
            new(6,  "Death Knight", $"{p}/boss_6_knight.png"),
            new(7,  "Ancient Golem",$"{p}/boss_7_golem.png"),
            new(8,  "Colossus",     $"{p}/boss_8_colossus.png"),
            new(9,  "Guardian",     $"{p}/boss_9_guardian.png"),
            new(10, "Living Statue",$"{p}/boss_10_statue.png"),
        }, GetRuinsBossStats());
    }

    private static Dictionary<int, BossStats> GetRuinsBossStats() => new()
    {
        { 1,  new(200, 36,  5, 30, 0.10, 120, 0.5, 0.2) },
        { 2,  new(220, 40,  7, 32, 0.12, 130, 0.5, 0.2) },
        { 3,  new(210, 46, 10, 27, 0.15, 140, 0.5, 0.2) },
        { 4,  new(215, 44, 12, 28, 0.14, 150, 0.5, 0.2) },
        { 5,  new(255, 38,  6, 36, 0.08, 160, 0.5, 0.2) },
        { 6,  new(240, 48,  8, 32, 0.12, 170, 0.5, 0.2) },
        { 7,  new(270, 42,  9, 38, 0.10, 180, 0.5, 0.2) },
        { 8,  new(250, 52, 11, 34, 0.18, 190, 0.5, 0.2) },
        { 9,  new(310, 46,  6, 40, 0.12, 200, 0.5, 0.2) },
        { 10, new(380, 58, 10, 48, 0.20, 500, 0.8, 0.4) },
    };

    // ════════════════════════════════════════════
    //  DARK (901-1000)
    // ════════════════════════════════════════════

    private static void SeedDarkNormals(List<StageEnemy> enemies)
    {
        var p = "/sprites/games/my-tuno/enemies/dark";
        AddNormals(enemies, RegionType.Dark, p, new()
        {
            ("Banshee",  45, 17, 12,  2, PlacementType.Aerial),
            ("Demon",    90, 18,  7,  8, PlacementType.Terrestrial),
            ("Ghost",    40, 16, 13,  2, PlacementType.Aerial),
            ("Mantis",   55, 15, 11,  4, PlacementType.Terrestrial),
            ("Troll",    95, 14,  4, 12, PlacementType.Terrestrial),
            ("Undead",   80, 13,  5, 11, PlacementType.Terrestrial),
            ("Vampire",  70, 17,  9,  6, PlacementType.Terrestrial),
            ("Werewolf", 75, 16, 10,  7, PlacementType.Terrestrial),
            ("Wolf",     60, 14, 10,  5, PlacementType.Terrestrial),
            ("Zombie",   85, 12,  3, 13, PlacementType.Terrestrial),
        });
    }

    private static void SeedDarkBosses(List<StageEnemy> enemies)
    {
        var p = "/sprites/games/my-tuno/enemies/dark";
        AddBosses(enemies, RegionType.Dark, 900, new()
        {
            new(1,  "Colossus",    $"{p}/boss_1_colossus.png"),
            new(2,  "Wraith King", $"{p}/boss_2_wraithking.png"),
            new(3,  "Ogre",        $"{p}/boss_3_ogre.png"),
            new(4,  "Dark Mantis", $"{p}/boss_4_mantis.png"),
            new(5,  "Phantom",     $"{p}/boss_5_ghost.png", PlacementType.Aerial),
            new(6,  "Lich",        $"{p}/boss_6_undead.png"),
            new(7,  "Revenant",    $"{p}/boss_7_zombie.png"),
            new(8,  "Minotaur",    $"{p}/boss_8_minotaur.png"),
            new(9,  "Dinosaur",    $"{p}/boss_9_dinossaur.png"),
            new(10, "Shadow Dragon",$"{p}/boss_10_dragon.png", PlacementType.Aerial),
        }, GetDarkBossStats());
    }

    private static Dictionary<int, BossStats> GetDarkBossStats() => new()
    {
        { 1,  new(220, 40,  5, 34, 0.12, 140, 0.5, 0.2) },
        { 2,  new(240, 44,  7, 36, 0.14, 150, 0.5, 0.2) },
        { 3,  new(230, 50, 10, 31, 0.17, 160, 0.5, 0.2) },
        { 4,  new(235, 48, 12, 32, 0.16, 170, 0.5, 0.2) },
        { 5,  new(275, 42,  6, 40, 0.10, 180, 0.5, 0.2) },
        { 6,  new(260, 52,  8, 36, 0.14, 190, 0.5, 0.2) },
        { 7,  new(290, 46,  9, 42, 0.12, 200, 0.5, 0.2) },
        { 8,  new(270, 56, 11, 38, 0.20, 210, 0.5, 0.2) },
        { 9,  new(330, 50,  6, 44, 0.14, 220, 0.5, 0.2) },
        { 10, new(400, 65, 10, 52, 0.22, 600, 0.8, 0.4) },
    };

    // ════════════════════════════════════════════
    //  Helper records
    // ════════════════════════════════════════════

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
