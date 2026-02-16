using Microsoft.EntityFrameworkCore;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data;

public static partial class SeedData
{
    public static async Task SeedAllBiomeEnemiesAsync(ApplicationDbContext dbContext)
    {
        await ReseedAllBiomeEnemiesAsync(dbContext);
    }

    /// <summary>
    /// Deletes ALL stage enemies and re-seeds every biome from scratch.
    /// Use when migrating from the old 12-biome layout to the new 21-biome layout,
    /// or whenever a full refresh is needed.
    /// </summary>
    public static async Task ReseedAllBiomeEnemiesAsync(ApplicationDbContext dbContext)
    {
        dbContext.StageEnemies.RemoveRange(dbContext.StageEnemies);
        await dbContext.SaveChangesAsync();

        await SeedBiomeIfMissing(dbContext, RegionType.Forest, SeedForestEnemies);
        await SeedBiomeIfMissing(dbContext, RegionType.Swamp, SeedSwampEnemies);
        await SeedBiomeIfMissing(dbContext, RegionType.Mountains, SeedMountainsEnemies);
        await SeedBiomeIfMissing(dbContext, RegionType.Snowy, SeedSnowyEnemies);
        await SeedBiomeIfMissing(dbContext, RegionType.Tropical, SeedTropicalEnemies);
        await SeedBiomeIfMissing(dbContext, RegionType.Caverns, SeedCavernsEnemies);
        await SeedBiomeIfMissing(dbContext, RegionType.Desert, SeedDesertEnemies);
        await SeedBiomeIfMissing(dbContext, RegionType.Volcanic, SeedVolcanicEnemies);
        await SeedBiomeIfMissing(dbContext, RegionType.Ruins, SeedRuinsEnemies);
        await SeedBiomeIfMissing(dbContext, RegionType.Sky, SeedSkyEnemies);
        await SeedBiomeIfMissing(dbContext, RegionType.Underwater, SeedUnderwaterEnemies);
        await SeedBiomeIfMissing(dbContext, RegionType.Underground, SeedUndergroundEnemies);
        await SeedBiomeIfMissing(dbContext, RegionType.Mechanical, SeedMechanicalEnemies);
        await SeedBiomeIfMissing(dbContext, RegionType.Frostfire, SeedFrostfireEnemies);
        await SeedBiomeIfMissing(dbContext, RegionType.Corruption, SeedCorruptionEnemies);
        await SeedBiomeIfMissing(dbContext, RegionType.Dark, SeedDarkEnemies);
        await SeedBiomeIfMissing(dbContext, RegionType.Alien, SeedAlienEnemies);
        await SeedBiomeIfMissing(dbContext, RegionType.Void, SeedVoidEnemies);
        await SeedBiomeIfMissing(dbContext, RegionType.Timerift, SeedTimeriftEnemies);
        await SeedBiomeIfMissing(dbContext, RegionType.Light, SeedLightEnemies);
    }

    private static async Task SeedBiomeIfMissing(ApplicationDbContext db, RegionType region, Func<List<StageEnemy>> factory)
    {
        if (await db.StageEnemies.AnyAsync(e => e.Region == region)) return;
        var enemies = factory();
        await db.StageEnemies.AddRangeAsync(enemies);
        await db.SaveChangesAsync();
    }

    // ───────────────────────────── Forest (1-100) ─────────────────────────────
    private static List<StageEnemy> SeedForestEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/forest";
        return new List<StageEnemy>
        {
            // Normal enemies
            E("Bee",      N, RegionType.Forest, 80,  12, 10, 3,  0.04, 1.0m, 0.05, 0.02, $"{p}/bee.png"),
            E("Beetle",   N, RegionType.Forest, 95,  14, 8,  5,  0.03, 1.0m, 0.05, 0.02, $"{p}/beetle.png"),
            E("Cheetah",  N, RegionType.Forest, 100, 16, 14, 4,  0.06, 1.0m, 0.05, 0.02, $"{p}/cheetah.png"),
            E("Eagle",    N, RegionType.Forest, 85,  15, 12, 3,  0.05, 1.0m, 0.05, 0.02, $"{p}/eagle.png", placement: PlacementType.Aerial),
            E("Monkey",   N, RegionType.Forest, 90,  13, 13, 4,  0.05, 1.0m, 0.05, 0.02, $"{p}/monkey.png"),
            E("Panther",  N, RegionType.Forest, 105, 17, 14, 5,  0.06, 1.0m, 0.05, 0.02, $"{p}/panther.png"),
            E("Snake",    N, RegionType.Forest, 88,  14, 11, 3,  0.04, 1.0m, 0.05, 0.02, $"{p}/snake.png"),
            E("Spider",   N, RegionType.Forest, 75,  13, 9,  3,  0.05, 1.0m, 0.05, 0.02, $"{p}/spider.png"),
            E("Stag",     N, RegionType.Forest, 110, 15, 10, 6,  0.03, 1.0m, 0.05, 0.02, $"{p}/stag.png"),
            E("Toucan",   N, RegionType.Forest, 70,  12, 12, 2,  0.04, 1.0m, 0.05, 0.02, $"{p}/toucan.png", placement: PlacementType.Aerial),
            // Bosses
            B("Bear",      RegionType.Forest, 400,  50, 10, 18, 0.06, 5.0m,  0.20, 0.10, $"{p}/boss_1_bear.png",      10),
            B("Tiger",     RegionType.Forest, 440,  54, 12, 20, 0.07, 6.0m,  0.21, 0.10, $"{p}/boss_2_tiger.png",     20),
            B("Jaguar",    RegionType.Forest, 480,  58, 14, 18, 0.08, 7.0m,  0.22, 0.11, $"{p}/boss_3_jaguar.png",    30),
            B("Falcon",    RegionType.Forest, 420,  52, 18, 15, 0.09, 8.0m,  0.22, 0.11, $"{p}/boss_4_falcon.png",    40, PlacementType.Aerial),
            B("Leecher",   RegionType.Forest, 520,  56, 10, 22, 0.06, 9.0m,  0.23, 0.11, $"{p}/boss_5_leecher.png",   50),
            B("Python",    RegionType.Forest, 560,  60, 8,  24, 0.07, 10.0m, 0.24, 0.12, $"{p}/boss_6_python.png",    60),
            B("Centipede", RegionType.Forest, 500,  55, 14, 20, 0.08, 11.0m, 0.25, 0.12, $"{p}/boss_7_centipede.png", 70),
            B("Mantis",    RegionType.Forest, 540,  62, 16, 18, 0.10, 12.0m, 0.26, 0.12, $"{p}/boss_8_mantis.png",    80),
            B("Gorilla",   RegionType.Forest, 600,  65, 10, 26, 0.08, 13.0m, 0.27, 0.13, $"{p}/boss_9_gorilla.png",   90),
            B("Lion",      RegionType.Forest, 650,  70, 12, 28, 0.09, 14.0m, 0.28, 0.13, $"{p}/boss_10_lion.png",     100),
        };
    }

    // ───────────────────────────── Swamp (101-200) ─────────────────────────────
    private static List<StageEnemy> SeedSwampEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/swamp";
        return new List<StageEnemy>
        {
            E("Crab",       N, RegionType.Swamp, 100, 16, 9,  6,  0.04, 1.2m, 0.06, 0.03, $"{p}/crab.png"),
            E("Crocodile",  N, RegionType.Swamp, 120, 18, 8,  8,  0.04, 1.2m, 0.06, 0.03, $"{p}/crocodile.png"),
            E("Crow",       N, RegionType.Swamp, 85,  14, 12, 4,  0.05, 1.2m, 0.06, 0.03, $"{p}/crow.png", placement: PlacementType.Aerial),
            E("Frog",       N, RegionType.Swamp, 90,  13, 11, 5,  0.04, 1.2m, 0.06, 0.03, $"{p}/frog.png"),
            E("Leech",      N, RegionType.Swamp, 80,  15, 7,  4,  0.05, 1.2m, 0.06, 0.03, $"{p}/leech.png"),
            E("Mosquito",   N, RegionType.Swamp, 70,  12, 14, 3,  0.06, 1.2m, 0.06, 0.03, $"{p}/mosquito.png", placement: PlacementType.Aerial),
            E("Salamander", N, RegionType.Swamp, 95,  15, 10, 6,  0.04, 1.2m, 0.06, 0.03, $"{p}/salamander.png"),
            E("Slime",      N, RegionType.Swamp, 110, 12, 6,  8,  0.03, 1.2m, 0.06, 0.03, $"{p}/slime.png"),
            E("Swamp Snake",N, RegionType.Swamp, 95,  16, 10, 5,  0.05, 1.2m, 0.06, 0.03, $"{p}/snake.png"),
            E("Stalker",    N, RegionType.Swamp, 105, 17, 11, 6,  0.05, 1.2m, 0.06, 0.03, $"{p}/stalker.png"),
            // Bosses
            B("Frog King",     RegionType.Swamp, 460,  55, 12, 20, 0.07, 7.0m,  0.22, 0.11, $"{p}/boss_1_frog.png",          110),
            B("Pelican",       RegionType.Swamp, 500,  58, 14, 22, 0.07, 8.0m,  0.23, 0.11, $"{p}/boss_2_pelican.png",        120, PlacementType.Aerial),
            B("Giant Leech",   RegionType.Swamp, 540,  60, 8,  24, 0.06, 9.0m,  0.24, 0.12, $"{p}/boss_3_leech.png",          130),
            B("Hydra",         RegionType.Swamp, 580,  65, 10, 26, 0.08, 10.0m, 0.25, 0.12, $"{p}/boss_4_hydra.png",          140),
            B("Anaconda",      RegionType.Swamp, 620,  62, 9,  28, 0.07, 11.0m, 0.26, 0.12, $"{p}/boss_5_anaconda.png",       150),
            B("Crayfish",      RegionType.Swamp, 560,  60, 8,  30, 0.06, 12.0m, 0.27, 0.13, $"{p}/boss_6_crayfish.png",       160),
            B("Darner",        RegionType.Swamp, 500,  58, 18, 20, 0.09, 13.0m, 0.28, 0.13, $"{p}/boss_7_darner.png",         170, PlacementType.Aerial),
            B("Hippopotamus",  RegionType.Swamp, 700,  70, 6,  32, 0.06, 14.0m, 0.29, 0.13, $"{p}/boss_8_hippopotamus.png",   180),
            B("Troll",         RegionType.Swamp, 660,  72, 8,  30, 0.08, 15.0m, 0.30, 0.14, $"{p}/boss_9_troll.png",          190),
            B("Aligator",     RegionType.Swamp, 750,  75, 10, 34, 0.08, 16.0m, 0.31, 0.14, $"{p}/boss_10_aligator.png",      200),
        };
    }

    // ───────────────────────────── Mountains (201-300) ─────────────────────────────
    private static List<StageEnemy> SeedMountainsEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/mountains";
        return new List<StageEnemy>
        {
            E("Bear",     N, RegionType.Mountains, 130, 20, 9,  9,  0.04, 1.4m, 0.06, 0.03, $"{p}/bear.png"),
            E("Boar",     N, RegionType.Mountains, 125, 22, 10, 8,  0.05, 1.4m, 0.06, 0.03, $"{p}/boar.png"),
            E("Elephant", N, RegionType.Mountains, 160, 24, 6,  12, 0.03, 1.4m, 0.06, 0.03, $"{p}/elephant.png"),
            E("Fox",      N, RegionType.Mountains, 100, 18, 14, 6,  0.06, 1.4m, 0.06, 0.03, $"{p}/fox.png"),
            E("Goat",     N, RegionType.Mountains, 115, 16, 12, 8,  0.04, 1.4m, 0.06, 0.03, $"{p}/goat.png"),
            E("Hyena",    N, RegionType.Mountains, 110, 20, 12, 7,  0.05, 1.4m, 0.06, 0.03, $"{p}/hyena.png"),
            E("Pigeon",   N, RegionType.Mountains, 80,  14, 14, 4,  0.05, 1.4m, 0.06, 0.03, $"{p}/pigeon.png", placement: PlacementType.Aerial),
            E("Rhino",    N, RegionType.Mountains, 150, 25, 7,  11, 0.03, 1.4m, 0.06, 0.03, $"{p}/rhino.png"),
            E("Vulture",  N, RegionType.Mountains, 95,  17, 13, 5,  0.06, 1.4m, 0.06, 0.03, $"{p}/vulture.png", placement: PlacementType.Aerial),
            E("Wolf",     N, RegionType.Mountains, 120, 21, 12, 8,  0.05, 1.4m, 0.06, 0.03, $"{p}/wolf.png"),
            // Bosses
            B("Alpine",  RegionType.Mountains, 520,  60, 10, 24, 0.07, 8.0m,  0.23, 0.11, $"{p}/boss_1_alpine.png",  210),
            B("Hawk",    RegionType.Mountains, 480,  58, 18, 20, 0.09, 9.0m,  0.24, 0.12, $"{p}/boss_2_hawk.png",    220, PlacementType.Aerial),
            B("Alpaca",  RegionType.Mountains, 560,  55, 8,  26, 0.06, 10.0m, 0.25, 0.12, $"{p}/boss_3_alpaca.png",  230),
            B("Fox",     RegionType.Mountains, 500,  62, 16, 22, 0.10, 11.0m, 0.26, 0.12, $"{p}/boss_4_fox.png",     240),
            B("Giraffe", RegionType.Mountains, 600,  64, 10, 28, 0.07, 12.0m, 0.27, 0.13, $"{p}/boss_5_giraffe.png", 250),
            B("Harpy",   RegionType.Mountains, 540,  66, 18, 22, 0.09, 13.0m, 0.28, 0.13, $"{p}/boss_6_harpy.png",   260, PlacementType.Aerial),
            B("Wolf",    RegionType.Mountains, 580,  68, 14, 26, 0.08, 14.0m, 0.29, 0.13, $"{p}/boss_7_wolf.png",    270),
            B("Bull",    RegionType.Mountains, 650,  70, 8,  30, 0.07, 15.0m, 0.30, 0.14, $"{p}/boss_8_bull.png",    280),
            B("Gorilla", RegionType.Mountains, 700,  72, 10, 32, 0.08, 16.0m, 0.31, 0.14, $"{p}/boss_9_gorilla.png", 290),
            B("Golem",   RegionType.Mountains, 800,  75, 6,  38, 0.06, 17.0m, 0.32, 0.15, $"{p}/boss_10_golem.png",  300),
        };
    }

    // ───────────────────────────── Snowy (301-400) ─────────────────────────────
    private static List<StageEnemy> SeedSnowyEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/snowy";
        return new List<StageEnemy>
        {
            E("Polar Bear", N, RegionType.Snowy, 150, 26, 9,  11, 0.04, 1.6m, 0.07, 0.03, $"{p}/bear.png"),
            E("Arctic Fox", N, RegionType.Snowy, 110, 20, 14, 7,  0.06, 1.6m, 0.07, 0.03, $"{p}/fox.png"),
            E("Mammoth",    N, RegionType.Snowy, 180, 28, 6,  14, 0.03, 1.6m, 0.07, 0.03, $"{p}/mammoth.png"),
            E("Owl",        N, RegionType.Snowy, 100, 18, 13, 6,  0.07, 1.6m, 0.07, 0.03, $"{p}/owl.png", placement: PlacementType.Aerial),
            E("Penguin",    N, RegionType.Snowy, 120, 16, 10, 10, 0.04, 1.6m, 0.07, 0.03, $"{p}/penguin.png"),
            E("Rabbit",     N, RegionType.Snowy, 85,  14, 16, 5,  0.06, 1.6m, 0.07, 0.03, $"{p}/rabbit.png"),
            E("Reindeer",   N, RegionType.Snowy, 130, 22, 12, 9,  0.05, 1.6m, 0.07, 0.03, $"{p}/reindeer.png"),
            E("Snowman",    N, RegionType.Snowy, 140, 20, 6,  12, 0.03, 1.6m, 0.07, 0.03, $"{p}/snowman.png"),
            E("Snow Wolf",  N, RegionType.Snowy, 125, 24, 13, 8,  0.05, 1.6m, 0.07, 0.03, $"{p}/wolf.png"),
            E("Yeti",       N, RegionType.Snowy, 170, 30, 7,  13, 0.04, 1.6m, 0.07, 0.03, $"{p}/yeti.png"),
            // Bosses
            B("Ice Golem",  RegionType.Snowy, 580,  62, 6,  30, 0.06, 10.0m, 0.25, 0.12, $"{p}/boss_1_golem.png",    310),
            B("Snowman",    RegionType.Snowy, 540,  58, 8,  26, 0.07, 11.0m, 0.26, 0.12, $"{p}/boss_2_snoman.png",   320),
            B("Great Owl",  RegionType.Snowy, 500,  60, 16, 24, 0.09, 12.0m, 0.27, 0.13, $"{p}/boss_3_owl.png",      330, PlacementType.Aerial),
            B("Panda",      RegionType.Snowy, 620,  64, 8,  28, 0.07, 13.0m, 0.28, 0.13, $"{p}/boss_4_panda.png",    340),
            B("Snow Tiger", RegionType.Snowy, 660,  68, 12, 26, 0.09, 14.0m, 0.29, 0.13, $"{p}/boss_5_tiger.png",    350),
            B("King Penguin",RegionType.Snowy, 600,  60, 10, 30, 0.06, 15.0m, 0.30, 0.14, $"{p}/boss_6_penguin.png",  360),
            B("Elder Yeti", RegionType.Snowy, 700,  72, 8,  32, 0.08, 16.0m, 0.31, 0.14, $"{p}/boss_7_yeti.png",     370),
            B("Direwolf",   RegionType.Snowy, 650,  70, 14, 28, 0.10, 17.0m, 0.32, 0.15, $"{p}/boss_8_direwolf.png", 380),
            B("Mammoth",    RegionType.Snowy, 800,  75, 6,  36, 0.07, 18.0m, 0.33, 0.15, $"{p}/boss_9_mammoth.png",  390),
            B("Frost Drake",RegionType.Snowy, 850,  80, 10, 38, 0.09, 19.0m, 0.34, 0.16, $"{p}/boss_10_drake.png",   400),
        };
    }

    // ───────────────────────────── Tropical (401-500) ─────────────────────────────
    private static List<StageEnemy> SeedTropicalEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/tropical";
        return new List<StageEnemy>
        {
            E("Crab",      N, RegionType.Tropical, 130, 22, 10, 10, 0.05, 1.8m, 0.07, 0.04, $"{p}/crab.png"),
            E("Dolphin",   N, RegionType.Tropical, 120, 20, 16, 7,  0.06, 1.8m, 0.07, 0.04, $"{p}/dolphin.png"),
            E("Duck",      N, RegionType.Tropical, 100, 16, 12, 6,  0.05, 1.8m, 0.07, 0.04, $"{p}/duck.png", placement: PlacementType.Aerial),
            E("Jellyfish", N, RegionType.Tropical, 90,  18, 8,  5,  0.07, 1.8m, 0.07, 0.04, $"{p}/jellyfish.png"),
            E("Octopus",   N, RegionType.Tropical, 140, 24, 9,  9,  0.06, 1.8m, 0.07, 0.04, $"{p}/octopus.png"),
            E("Seagull",   N, RegionType.Tropical, 85,  15, 14, 4,  0.05, 1.8m, 0.07, 0.04, $"{p}/seagull.png", placement: PlacementType.Aerial),
            E("Seal",      N, RegionType.Tropical, 145, 21, 8,  11, 0.04, 1.8m, 0.07, 0.04, $"{p}/seal.png"),
            E("Shark",     N, RegionType.Tropical, 160, 28, 12, 8,  0.06, 1.8m, 0.07, 0.04, $"{p}/shark.png"),
            E("Starfish",  N, RegionType.Tropical, 110, 14, 6,  12, 0.03, 1.8m, 0.07, 0.04, $"{p}/starfish.png"),
            E("Turtle",    N, RegionType.Tropical, 170, 18, 5,  14, 0.03, 1.8m, 0.07, 0.04, $"{p}/turtle.png"),
            // Bosses
            B("Giant Crab",  RegionType.Tropical, 620,  65, 8,  30, 0.07, 12.0m, 0.27, 0.13, $"{p}/boss_1_crab.png",      410),
            B("Kraken",      RegionType.Tropical, 700,  72, 10, 32, 0.08, 13.0m, 0.28, 0.13, $"{p}/boss_2_kraken.png",    420),
            B("Shark Lord",  RegionType.Tropical, 660,  70, 14, 28, 0.10, 14.0m, 0.29, 0.13, $"{p}/boss_3_shark.png",     430),
            B("Manta",       RegionType.Tropical, 600,  62, 16, 26, 0.08, 15.0m, 0.30, 0.14, $"{p}/boss_4_manta.png",     440),
            B("Electric Eel",RegionType.Tropical, 580,  68, 18, 22, 0.12, 16.0m, 0.31, 0.14, $"{p}/boss_5_eel.png",       450),
            B("Sea Snake",   RegionType.Tropical, 640,  66, 12, 28, 0.09, 17.0m, 0.32, 0.15, $"{p}/boss_6_snake.png",     460),
            B("Jellyfish",   RegionType.Tropical, 550,  60, 10, 24, 0.10, 18.0m, 0.33, 0.15, $"{p}/boss_7_jellyfish.png", 470),
            B("Sea Turtle",  RegionType.Tropical, 750,  68, 6,  36, 0.06, 19.0m, 0.34, 0.16, $"{p}/boss_8_turtle.png",    480),
            B("Siren",       RegionType.Tropical, 680,  74, 14, 30, 0.11, 20.0m, 0.35, 0.16, $"{p}/boss_9_siren.png",     490),
            B("Leviathan",   RegionType.Tropical, 900,  80, 10, 40, 0.09, 21.0m, 0.36, 0.17, $"{p}/boss_10_leviathan.png",500),
        };
    }

    // ───────────────────────────── Caverns (501-600) ─────────────────────────────
    private static List<StageEnemy> SeedCavernsEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/caverns";
        return new List<StageEnemy>
        {
            E("Armadillo", N, RegionType.Caverns, 150, 22, 8,  14, 0.04, 2.0m, 0.08, 0.04, $"{p}/armadillo.png"),
            E("Bat",       N, RegionType.Caverns, 100, 18, 14, 6,  0.06, 2.0m, 0.08, 0.04, $"{p}/bat.png", placement: PlacementType.Aerial),
            E("Beetle",    N, RegionType.Caverns, 130, 20, 8,  10, 0.04, 2.0m, 0.08, 0.04, $"{p}/beetle.png"),
            E("Imp",       N, RegionType.Caverns, 110, 24, 12, 7,  0.07, 2.0m, 0.08, 0.04, $"{p}/imp.png"),
            E("Lizard",    N, RegionType.Caverns, 120, 21, 11, 8,  0.05, 2.0m, 0.08, 0.04, $"{p}/lizard.png"),
            E("Rat",       N, RegionType.Caverns, 90,  16, 13, 5,  0.05, 2.0m, 0.08, 0.04, $"{p}/rat.png"),
            E("Shrimp",    N, RegionType.Caverns, 85,  15, 10, 6,  0.04, 2.0m, 0.08, 0.04, $"{p}/shrimp.png"),
            E("Slug",      N, RegionType.Caverns, 140, 17, 5,  12, 0.03, 2.0m, 0.08, 0.04, $"{p}/slug.png"),
            E("Spider",    N, RegionType.Caverns, 105, 22, 12, 7,  0.06, 2.0m, 0.08, 0.04, $"{p}/spider.png"),
            E("Worm",      N, RegionType.Caverns, 125, 19, 7,  9,  0.04, 2.0m, 0.08, 0.04, $"{p}/worm.png"),
            // Bosses
            B("Spider Queen", RegionType.Caverns, 680,  68, 12, 28, 0.09, 14.0m, 0.29, 0.13, $"{p}/boss_1_spider.png",    510),
            B("Stone Golem",  RegionType.Caverns, 750,  65, 6,  36, 0.06, 15.0m, 0.30, 0.14, $"{p}/boss_2_golem.png",     520),
            B("Fungus",       RegionType.Caverns, 620,  60, 8,  30, 0.07, 16.0m, 0.31, 0.14, $"{p}/boss_3_fungus.png",    530),
            B("Wyrm",         RegionType.Caverns, 700,  72, 10, 32, 0.08, 17.0m, 0.32, 0.15, $"{p}/boss_4_wyrmm.png",     540),
            B("Giant Bat",    RegionType.Caverns, 640,  66, 18, 24, 0.10, 18.0m, 0.33, 0.15, $"{p}/boss_5_bat.png",       550, PlacementType.Aerial),
            B("Isopod",       RegionType.Caverns, 720,  64, 7,  34, 0.06, 19.0m, 0.34, 0.16, $"{p}/boss_6_isopod.png",    560),
            B("Titan Beetle", RegionType.Caverns, 780,  70, 8,  32, 0.07, 20.0m, 0.35, 0.16, $"{p}/boss_7_beetle.png",    570),
            B("Armadillo",    RegionType.Caverns, 760,  68, 6,  36, 0.06, 21.0m, 0.36, 0.17, $"{p}/boss_8_armadillo.png", 580),
            B("Cave Hydra",   RegionType.Caverns, 850,  76, 10, 34, 0.09, 22.0m, 0.37, 0.17, $"{p}/boss_9_hydra.png",     590),
            B("Basilisk",     RegionType.Caverns, 950,  82, 12, 40, 0.10, 23.0m, 0.38, 0.18, $"{p}/boss_10_basilisk.png", 600),
        };
    }

    // ───────────────────────────── Desert (601-700) ─────────────────────────────
    private static List<StageEnemy> SeedDesertEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/desert";
        return new List<StageEnemy>
        {
            E("Desert Beetle", N, RegionType.Desert, 140, 24, 9,  10, 0.05, 2.2m, 0.08, 0.04, $"{p}/beetle.png"),
            E("Cactus",        N, RegionType.Desert, 160, 20, 4,  16, 0.03, 2.2m, 0.08, 0.04, $"{p}/cactus.png"),
            E("Desert Eagle",  N, RegionType.Desert, 110, 22, 16, 6,  0.07, 2.2m, 0.08, 0.04, $"{p}/eagle.png", placement: PlacementType.Aerial),
            E("Lion",          N, RegionType.Desert, 155, 28, 12, 10, 0.06, 2.2m, 0.08, 0.04, $"{p}/lion.png"),
            E("Lizard",        N, RegionType.Desert, 125, 22, 11, 8,  0.05, 2.2m, 0.08, 0.04, $"{p}/lizard.png"),
            E("Scarab",        N, RegionType.Desert, 130, 20, 10, 10, 0.04, 2.2m, 0.08, 0.04, $"{p}/scarab.png"),
            E("Scorpion",      N, RegionType.Desert, 135, 26, 9,  9,  0.06, 2.2m, 0.08, 0.04, $"{p}/scorpion.png"),
            E("Desert Vulture", N, RegionType.Desert, 105, 21, 14, 6,  0.06, 2.2m, 0.08, 0.04, $"{p}/vulture.png", placement: PlacementType.Aerial),
            E("Wisp",          N, RegionType.Desert, 90,  24, 16, 4,  0.08, 2.2m, 0.08, 0.04, $"{p}/wisp.png"),
            E("Sandworm",      N, RegionType.Desert, 170, 25, 6,  12, 0.04, 2.2m, 0.08, 0.04, $"{p}/worm.png"),
            // Bosses
            B("Djinn",       RegionType.Desert, 720,  72, 14, 28, 0.10, 16.0m, 0.31, 0.14, $"{p}/boss_1_djinn.png",      610),
            B("Scorpion King",RegionType.Desert, 760,  70, 10, 32, 0.08, 17.0m, 0.32, 0.15, $"{p}/boss_2_scorpion.png",  620),
            B("Roc",          RegionType.Desert, 700,  68, 20, 26, 0.10, 18.0m, 0.33, 0.15, $"{p}/boss_3_roc.png",       630, PlacementType.Aerial),
            B("Persian",      RegionType.Desert, 740,  74, 14, 30, 0.09, 19.0m, 0.34, 0.16, $"{p}/boss_4_persian.png",   640),
            B("Vulture Lord", RegionType.Desert, 680,  66, 18, 24, 0.11, 20.0m, 0.35, 0.16, $"{p}/boss_5_vulture.png",   650, PlacementType.Aerial),
            B("Sphinx",       RegionType.Desert, 800,  78, 10, 34, 0.09, 21.0m, 0.36, 0.17, $"{p}/boss_6_sphinx.png",    660),
            B("Cactus",       RegionType.Desert, 850,  70, 4,  40, 0.06, 22.0m, 0.37, 0.17, $"{p}/boss_7_cactus.png",    670),
            B("Anubis",       RegionType.Desert, 900,  82, 12, 36, 0.10, 23.0m, 0.38, 0.18, $"{p}/boss_8_anubis.png",    680),
            B("Camel",        RegionType.Desert, 780,  72, 8,  38, 0.07, 24.0m, 0.39, 0.18, $"{p}/boss_9_camel.png",     690),
            B("Great Worm",   RegionType.Desert, 1000, 88, 6,  42, 0.08, 25.0m, 0.40, 0.19, $"{p}/boss_10_worm.png",     700),
        };
    }

    // ───────────────────────────── Volcanic (701-800) ─────────────────────────────
    private static List<StageEnemy> SeedVolcanicEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/volcanic";
        return new List<StageEnemy>
        {
            E("Badger",          N, RegionType.Volcanic, 145, 26, 11, 10, 0.05, 2.4m, 0.09, 0.04, $"{p}/badger.png"),
            E("Lava Bat",       N, RegionType.Volcanic, 110, 22, 16, 6,  0.07, 2.4m, 0.09, 0.04, $"{p}/bat.png", placement: PlacementType.Aerial),
            E("Fire Beetle",    N, RegionType.Volcanic, 140, 24, 8,  12, 0.05, 2.4m, 0.09, 0.04, $"{p}/beetle.png"),
            E("Dragon",         N, RegionType.Volcanic, 180, 32, 12, 12, 0.07, 2.4m, 0.09, 0.04, $"{p}/dragon.png"),
            E("Drake",          N, RegionType.Volcanic, 160, 28, 14, 10, 0.06, 2.4m, 0.09, 0.04, $"{p}/drake.png"),
            E("Lava Duck",      N, RegionType.Volcanic, 100, 18, 12, 6,  0.05, 2.4m, 0.09, 0.04, $"{p}/duck.png"),
            E("Fire Lizard",    N, RegionType.Volcanic, 130, 25, 11, 9,  0.06, 2.4m, 0.09, 0.04, $"{p}/lizard.png"),
            E("Salamander",     N, RegionType.Volcanic, 135, 24, 10, 10, 0.05, 2.4m, 0.09, 0.04, $"{p}/salamander.png"),
            E("Two-Headed Dragon",N,RegionType.Volcanic,200, 35, 8,  14, 0.06, 2.4m, 0.09, 0.04, $"{p}/twoheadeddragon.png"),
            E("Magma Wolf",     N, RegionType.Volcanic, 140, 27, 13, 9,  0.06, 2.4m, 0.09, 0.04, $"{p}/wolf.png"),
            // Bosses
            B("Fire Scorpion",RegionType.Volcanic, 780,  74, 10, 32, 0.08, 18.0m, 0.33, 0.15, $"{p}/boss_1_scorpion.png",       710),
            B("Lava Snake",   RegionType.Volcanic, 740,  72, 14, 28, 0.09, 19.0m, 0.34, 0.16, $"{p}/boss_2_snake.png",          720),
            B("Fire Rhino",   RegionType.Volcanic, 820,  76, 8,  36, 0.07, 20.0m, 0.35, 0.16, $"{p}/boss_3_rhino.png",          730),
            B("Infernal Wisp",RegionType.Volcanic, 700,  70, 20, 24, 0.12, 21.0m, 0.36, 0.17, $"{p}/boss_4_wisp.png",           740),
            B("Hellhound",    RegionType.Volcanic, 760,  78, 16, 30, 0.10, 22.0m, 0.37, 0.17, $"{p}/boss_5_dog.png",            750),
            B("Lava Turtle",  RegionType.Volcanic, 900,  72, 4,  42, 0.06, 23.0m, 0.38, 0.18, $"{p}/boss_6_turtle.png",         760),
            B("Fire Gorilla", RegionType.Volcanic, 850,  80, 10, 34, 0.08, 24.0m, 0.39, 0.18, $"{p}/boss_7_gorilla.png",        770),
            B("Cerberus",     RegionType.Volcanic, 950,  85, 12, 38, 0.10, 25.0m, 0.40, 0.19, $"{p}/boss_8_cerebrus.png",       780),
            B("Phoenix",      RegionType.Volcanic, 880,  82, 18, 30, 0.12, 26.0m, 0.41, 0.19, $"{p}/boss_9_phoenix.png",        790, PlacementType.Aerial),
            B("Inferno Dragon",RegionType.Volcanic,1100, 92, 14, 44, 0.10, 27.0m, 0.42, 0.20, $"{p}/boss_10_dragon.png",        800),
        };
    }

    // ───────────────────────────── Ruins (801-900) ─────────────────────────────
    private static List<StageEnemy> SeedRuinsEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/ruins";
        return new List<StageEnemy>
        {
            E("Bat",     N, RegionType.Ruins, 120, 22, 14, 7,  0.06, 2.6m, 0.09, 0.05, $"{p}/bat.png", placement: PlacementType.Aerial),
            E("Ghost",   N, RegionType.Ruins, 110, 26, 12, 6,  0.07, 2.6m, 0.09, 0.05, $"{p}/ghost.png"),
            E("Hound",   N, RegionType.Ruins, 140, 24, 13, 9,  0.06, 2.6m, 0.09, 0.05, $"{p}/hound.png"),
            E("Lantern", N, RegionType.Ruins, 100, 20, 8,  8,  0.05, 2.6m, 0.09, 0.05, $"{p}/lantern.png"),
            E("Mummy",   N, RegionType.Ruins, 160, 22, 6,  14, 0.04, 2.6m, 0.09, 0.05, $"{p}/mummy.png"),
            E("Plant",   N, RegionType.Ruins, 150, 20, 4,  16, 0.03, 2.6m, 0.09, 0.05, $"{p}/plant.png"),
            E("Rat",     N, RegionType.Ruins, 95,  18, 14, 5,  0.06, 2.6m, 0.09, 0.05, $"{p}/rat.png"),
            E("Raven",   N, RegionType.Ruins, 90,  20, 15, 5,  0.07, 2.6m, 0.09, 0.05, $"{p}/raven.png", placement: PlacementType.Aerial),
            E("Scarab",  N, RegionType.Ruins, 130, 21, 10, 10, 0.05, 2.6m, 0.09, 0.05, $"{p}/scarab.png"),
            E("Trap",    N, RegionType.Ruins, 145, 25, 5,  13, 0.04, 2.6m, 0.09, 0.05, $"{p}/trap.png"),
            // Bosses
            B("Sentinel",  RegionType.Ruins, 820,  78, 10, 34, 0.08, 20.0m, 0.35, 0.16, $"{p}/boss_1_sentinel.png",  810),
            B("Pharaoh",   RegionType.Ruins, 860,  80, 8,  38, 0.09, 21.0m, 0.36, 0.17, $"{p}/boss_2_pharao.png",    820),
            B("Medusa",    RegionType.Ruins, 800,  82, 14, 30, 0.12, 22.0m, 0.37, 0.17, $"{p}/boss_3_medusa.png",    830),
            B("Warden",    RegionType.Ruins, 880,  76, 8,  36, 0.08, 23.0m, 0.38, 0.18, $"{p}/boss_4_warden.png",    840),
            B("Hydra",     RegionType.Ruins, 920,  84, 10, 34, 0.09, 24.0m, 0.39, 0.18, $"{p}/boss_5_hydra.png",     850),
            B("Knight",    RegionType.Ruins, 850,  80, 12, 36, 0.10, 25.0m, 0.40, 0.19, $"{p}/boss_6_knight.png",    860),
            B("Golem",     RegionType.Ruins, 1000, 78, 6,  44, 0.06, 26.0m, 0.41, 0.19, $"{p}/boss_7_golem.png",     870),
            B("Colossus",  RegionType.Ruins, 1100, 88, 8,  42, 0.08, 27.0m, 0.42, 0.20, $"{p}/boss_8_colossus.png",  880),
            B("Guardian",  RegionType.Ruins, 950,  86, 10, 38, 0.09, 28.0m, 0.43, 0.20, $"{p}/boss_9_guardian.png",   890),
            B("Living Statue",RegionType.Ruins,1200,92, 6,  48, 0.08, 29.0m, 0.44, 0.21, $"{p}/boss_10_statue.png",   900),
        };
    }

    // ───────────────────────────── Sky (901-1000) — incomplete: only 6 bosses ─────────────────────────────
    private static List<StageEnemy> SeedSkyEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/sky";
        return new List<StageEnemy>
        {
            E("Albatross", N, RegionType.Sky, 130, 24, 18, 6,  0.07, 2.8m, 0.10, 0.05, $"{p}/albatross.png", placement: PlacementType.Aerial),
            E("Balloon",   N, RegionType.Sky, 150, 18, 8,  12, 0.04, 2.8m, 0.10, 0.05, $"{p}/baloon.png"),
            E("Birdman",   N, RegionType.Sky, 140, 26, 16, 8,  0.07, 2.8m, 0.10, 0.05, $"{p}/birdman.png", placement: PlacementType.Aerial),
            E("Cloud",     N, RegionType.Sky, 170, 20, 6,  14, 0.03, 2.8m, 0.10, 0.05, $"{p}/cloud.png"),
            E("Gull",      N, RegionType.Sky, 100, 22, 16, 5,  0.06, 2.8m, 0.10, 0.05, $"{p}/gull.png", placement: PlacementType.Aerial),
            E("Harpy",     N, RegionType.Sky, 135, 28, 18, 7,  0.08, 2.8m, 0.10, 0.05, $"{p}/harpy.png", placement: PlacementType.Aerial),
            E("Heron",     N, RegionType.Sky, 120, 22, 14, 7,  0.06, 2.8m, 0.10, 0.05, $"{p}/heron.png", placement: PlacementType.Aerial),
            E("Kestrel",   N, RegionType.Sky, 110, 25, 20, 5,  0.08, 2.8m, 0.10, 0.05, $"{p}/kestrel.png", placement: PlacementType.Aerial),
            E("Raven",     N, RegionType.Sky, 115, 24, 16, 6,  0.07, 2.8m, 0.10, 0.05, $"{p}/raven.png", placement: PlacementType.Aerial),
            E("Swift",     N, RegionType.Sky, 90,  20, 22, 4,  0.08, 2.8m, 0.10, 0.05, $"{p}/swift.png", placement: PlacementType.Aerial),
            // Bosses (6 available, 4 placeholder — fallback will resolve to random from previous stages)
            B("Goose",    RegionType.Sky, 860,  80, 14, 30, 0.09, 22.0m, 0.37, 0.17, $"{p}/boss_1_goose.png",   910, PlacementType.Aerial),
            B("Kestrel",  RegionType.Sky, 900,  84, 20, 28, 0.11, 23.0m, 0.38, 0.18, $"{p}/boss_2_kestrel.png", 920, PlacementType.Aerial),
            B("Osprey",   RegionType.Sky, 940,  86, 18, 32, 0.10, 24.0m, 0.39, 0.18, $"{p}/boss_3_osprey.png",  930, PlacementType.Aerial),
            B("Raven",    RegionType.Sky, 880,  82, 22, 26, 0.12, 25.0m, 0.40, 0.19, $"{p}/boss_4_raven.png",   940, PlacementType.Aerial),
            B("Condor",   RegionType.Sky, 980,  88, 16, 34, 0.10, 26.0m, 0.41, 0.19, $"{p}/boss_5_condor.png",  950, PlacementType.Aerial),
            B("Behemoth", RegionType.Sky, 1050, 92, 12, 38, 0.09, 27.0m, 0.42, 0.20, $"{p}/boss_6_behemoth.png",960),
            B("Cyclone",  RegionType.Sky, 1100, 94, 14, 40, 0.10, 28.0m, 0.43, 0.20, $"{p}/boss_7_cyclone.png", 970),
            B("Wyvern",   RegionType.Sky, 1150, 96, 16, 42, 0.11, 29.0m, 0.44, 0.21, $"{p}/boss_8_wyvern.png",  980, PlacementType.Aerial),
            B("Serpent",   RegionType.Sky, 1200, 98, 12, 44, 0.10, 30.0m, 0.45, 0.21, $"{p}/boss_9_serpent.png", 990),
            B("Starbird",  RegionType.Sky, 1300,100, 14, 46, 0.11, 31.0m, 0.46, 0.22, $"{p}/boss_10_starbird.png",1000, PlacementType.Aerial),
        };
    }

    // ───────────────────────────── Underwater (1001-1100) ─────────────────────────────
    private static List<StageEnemy> SeedUnderwaterEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/underwater";
        return new List<StageEnemy>
        {
            E("Anglerfish", N, RegionType.Underwater, 170, 28, 10, 14, 0.06, 3.0m, 0.11, 0.05, $"{p}/anglerfish.png"),
            E("Barracuda",  N, RegionType.Underwater, 140, 30, 18, 8,  0.08, 3.0m, 0.11, 0.05, $"{p}/barracuda.png"),
            E("Eel",        N, RegionType.Underwater, 120, 26, 16, 6,  0.07, 3.0m, 0.11, 0.05, $"{p}/eel.png"),
            E("Knight",     N, RegionType.Underwater, 180, 24, 8,  16, 0.05, 3.0m, 0.11, 0.05, $"{p}/knight.png"),
            E("Nautilus",   N, RegionType.Underwater, 160, 22, 6,  14, 0.04, 3.0m, 0.11, 0.05, $"{p}/nautilus.png"),
            E("Piranha",    N, RegionType.Underwater, 100, 26, 20, 4,  0.09, 3.0m, 0.11, 0.05, $"{p}/piranha.png"),
            E("Salmon",     N, RegionType.Underwater, 110, 22, 16, 5,  0.06, 3.0m, 0.11, 0.05, $"{p}/salmon.png"),
            E("Sardine",    N, RegionType.Underwater,  90, 20, 18, 4,  0.05, 3.0m, 0.11, 0.05, $"{p}/sardine.png"),
            E("Seahorse",   N, RegionType.Underwater, 105, 20, 14, 6,  0.05, 3.0m, 0.11, 0.05, $"{p}/seahorse.png"),
            E("Shark",      N, RegionType.Underwater, 200, 32, 12, 16, 0.08, 3.0m, 0.11, 0.05, $"{p}/shark.png"),
            // Bosses
            B("Kraken",    RegionType.Underwater,  950,  86, 10, 32, 0.09, 25.0m, 0.40, 0.19, $"{p}/boss_1_kraken.png",     1010),
            B("Leviathan", RegionType.Underwater, 1000,  90, 12, 34, 0.10, 26.0m, 0.41, 0.20, $"{p}/boss_2_leviathan.png",  1020),
            B("Megalodon", RegionType.Underwater, 1050,  92,  8, 38, 0.08, 27.0m, 0.42, 0.20, $"{p}/boss_3_megalodon.png",  1030),
            B("Serpent",   RegionType.Underwater,  980,  88, 14, 30, 0.11, 28.0m, 0.43, 0.21, $"{p}/boss_4_serpent.png",    1040),
            B("Whale",     RegionType.Underwater, 1100,  84,  6, 40, 0.07, 29.0m, 0.44, 0.21, $"{p}/boss_5_whale.png",     1050),
            B("Abyssal",   RegionType.Underwater, 1150,  94, 10, 36, 0.09, 30.0m, 0.45, 0.22, $"{p}/boss_6_abyssal.png",   1060),
            B("Trident",   RegionType.Underwater, 1200,  96, 12, 42, 0.10, 31.0m, 0.46, 0.22, $"{p}/boss_7_trident.png",   1070),
            B("Poseidon",  RegionType.Underwater, 1250, 100, 14, 44, 0.11, 32.0m, 0.47, 0.23, $"{p}/boss_8_poseidon.png",  1080),
            B("Hydra",     RegionType.Underwater, 1300,  98, 10, 46, 0.10, 33.0m, 0.48, 0.23, $"{p}/boss_9_hydra.png",     1090),
            B("Charybdis", RegionType.Underwater, 1400, 102,  8, 48, 0.09, 34.0m, 0.49, 0.24, $"{p}/boss_10_charybdis.png",1100),
        };
    }

    // ───────────────────────────── Underground (1101-1200) ─────────────────────────────
    private static List<StageEnemy> SeedUndergroundEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/underground";
        return new List<StageEnemy>
        {
            E("Beetle",     N, RegionType.Underground, 150, 28, 14, 10, 0.07, 3.5m, 0.12, 0.06, $"{p}/beetle.png"),
            E("Centipede",  N, RegionType.Underground, 130, 30, 18,  8, 0.08, 3.5m, 0.12, 0.06, $"{p}/centipede.png"),
            E("Crystal",    N, RegionType.Underground, 190, 24,  6, 16, 0.04, 3.5m, 0.12, 0.06, $"{p}/crystal.png"),
            E("Glowworm",   N, RegionType.Underground, 100, 26, 16,  6, 0.06, 3.5m, 0.12, 0.06, $"{p}/glowworm.png"),
            E("Goblin",     N, RegionType.Underground, 140, 32, 16, 10, 0.08, 3.5m, 0.12, 0.06, $"{p}/goblin.png"),
            E("Golem",      N, RegionType.Underground, 220, 26,  6, 18, 0.04, 3.5m, 0.12, 0.06, $"{p}/golem.png"),
            E("Mole",       N, RegionType.Underground, 160, 24, 10, 12, 0.05, 3.5m, 0.12, 0.06, $"{p}/mole.png"),
            E("Mushroom",   N, RegionType.Underground, 170, 22,  8, 14, 0.05, 3.5m, 0.12, 0.06, $"{p}/mushroom.png"),
            E("Stalactite", N, RegionType.Underground, 200, 28,  4, 16, 0.03, 3.5m, 0.12, 0.06, $"{p}/stalactite.png"),
            E("Worm",       N, RegionType.Underground, 120, 26, 14,  8, 0.06, 3.5m, 0.12, 0.06, $"{p}/worm.png"),
            // Bosses
            B("Worm",          RegionType.Underground, 1050,  90,  8, 34, 0.08, 27.0m, 0.42, 0.20, $"{p}/boss_1_worm.png",        1110),
            B("King",          RegionType.Underground, 1100,  92, 10, 36, 0.09, 28.0m, 0.43, 0.20, $"{p}/boss_2_king.png",        1120),
            B("Crystal",       RegionType.Underground, 1050,  88,  6, 38, 0.07, 29.0m, 0.44, 0.21, $"{p}/boss_3_crystal.png",     1130),
            B("Spider Queen",  RegionType.Underground, 1150,  94, 12, 34, 0.10, 30.0m, 0.45, 0.21, $"{p}/boss_4_spiderqueen.png", 1140),
            B("Earth Titan",   RegionType.Underground, 1200,  96,  8, 40, 0.08, 31.0m, 0.46, 0.22, $"{p}/boss_5_earthtitan.png",  1150),
            B("Magma Dweller", RegionType.Underground, 1250,  98, 10, 36, 0.09, 32.0m, 0.47, 0.22, $"{p}/boss_6_magmadweller.png",1160),
            B("Drake",         RegionType.Underground, 1150,  92, 14, 38, 0.10, 33.0m, 0.48, 0.23, $"{p}/boss_7_drake.png",       1170),
            B("Shadow",        RegionType.Underground, 1300, 100,  8, 42, 0.09, 34.0m, 0.49, 0.23, $"{p}/boss_8_shadow.png",      1180),
            B("Tunnel Horror", RegionType.Underground, 1350, 104, 10, 44, 0.10, 35.0m, 0.50, 0.24, $"{p}/boss_9_tunnelhorror.png", 1190),
            B("Ancient One",   RegionType.Underground, 1500, 108,  6, 48, 0.09, 36.0m, 0.51, 0.25, $"{p}/boss_10_ancientone.png",  1200),
        };
    }

    // ───────────────────────────── Mechanical (1201-1300) ─────────────────────────────
    private static List<StageEnemy> SeedMechanicalEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/mechanical";
        return new List<StageEnemy>
        {
            E("Automaton",  N, RegionType.Mechanical, 200, 32, 10, 14, 0.06, 3.6m, 0.12, 0.06, $"{p}/automaton.png"),
            E("Circuit",    N, RegionType.Mechanical, 160, 28, 16,  8, 0.07, 3.6m, 0.12, 0.06, $"{p}/circuit.png"),
            E("Drone",      N, RegionType.Mechanical, 150, 30, 18,  8, 0.08, 3.6m, 0.12, 0.06, $"{p}/drone.png", placement: PlacementType.Aerial),
            E("Engine",     N, RegionType.Mechanical, 240, 26,  4, 18, 0.03, 3.6m, 0.12, 0.06, $"{p}/engine.png"),
            E("Gear",       N, RegionType.Mechanical, 180, 30, 12, 12, 0.05, 3.6m, 0.12, 0.06, $"{p}/gear.png"),
            E("Piston",     N, RegionType.Mechanical, 190, 34, 10, 14, 0.06, 3.6m, 0.12, 0.06, $"{p}/piston.png"),
            E("Robot",      N, RegionType.Mechanical, 210, 32,  8, 16, 0.05, 3.6m, 0.12, 0.06, $"{p}/robot.png"),
            E("Spark",      N, RegionType.Mechanical, 130, 28, 20,  6, 0.09, 3.6m, 0.12, 0.06, $"{p}/spark.png"),
            E("Steambot",   N, RegionType.Mechanical, 220, 30,  6, 16, 0.04, 3.6m, 0.12, 0.06, $"{p}/steambot.png"),
            E("Wire",       N, RegionType.Mechanical, 170, 26, 14, 10, 0.06, 3.6m, 0.12, 0.06, $"{p}/wire.png"),
            // Bosses
            B("Titan",       RegionType.Mechanical, 1100,  92,  8, 36, 0.08, 29.0m, 0.44, 0.21, $"{p}/boss_1_titan.png",      1210),
            B("Spider",      RegionType.Mechanical, 1150,  94, 12, 34, 0.10, 30.0m, 0.45, 0.21, $"{p}/boss_2_spider.png",     1220),
            B("Iron Giant",  RegionType.Mechanical, 1200,  96,  6, 40, 0.07, 31.0m, 0.46, 0.22, $"{p}/boss_3_irongiant.png",  1230),
            B("Dragon",      RegionType.Mechanical, 1250, 100, 14, 38, 0.11, 32.0m, 0.47, 0.22, $"{p}/boss_4_dragon.png",     1240),
            B("Steam Lord",  RegionType.Mechanical, 1300,  98,  8, 42, 0.08, 33.0m, 0.48, 0.23, $"{p}/boss_5_steamlord.png",  1250),
            B("Hydra",       RegionType.Mechanical, 1350, 102, 10, 40, 0.09, 34.0m, 0.49, 0.23, $"{p}/boss_6_hydra.png",      1260),
            B("Beast",       RegionType.Mechanical, 1300, 104, 14, 38, 0.10, 35.0m, 0.50, 0.24, $"{p}/boss_7_beast.png",      1270),
            B("Sentinel",    RegionType.Mechanical, 1400, 106,  8, 44, 0.09, 36.0m, 0.51, 0.24, $"{p}/boss_8_sentinel.png",   1280),
            B("Humanoid",    RegionType.Mechanical, 1450, 108, 12, 46, 0.10, 37.0m, 0.52, 0.25, $"{p}/boss_9_humanoid.png",   1290),
            B("Android",     RegionType.Mechanical, 1550, 110,  6, 50, 0.10, 38.0m, 0.53, 0.26, $"{p}/boss_10_android.png",   1300),
        };
    }

    // ───────────────────────────── Frostfire (1301-1400) ─────────────────────────────
    private static List<StageEnemy> SeedFrostfireEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/frostfire";
        return new List<StageEnemy>
        {
            E("Beast",        N, RegionType.Frostfire, 220, 34, 12, 14, 0.06, 3.8m, 0.13, 0.06, $"{p}/beast.png"),
            E("Frostbird",    N, RegionType.Frostfire, 140, 28, 18,  8, 0.08, 3.8m, 0.13, 0.06, $"{p}/bird.png", placement: PlacementType.Aerial),
            E("Wolf",       N, RegionType.Frostfire, 180, 30, 10, 12, 0.05, 3.8m, 0.13, 0.06, $"{p}/wolf.png"),
            E("Crystal",      N, RegionType.Frostfire, 250, 26,  4, 18, 0.03, 3.8m, 0.13, 0.06, $"{p}/crystal.png"),
            E("Dinosaur",     N, RegionType.Frostfire, 230, 36,  8, 16, 0.06, 3.8m, 0.13, 0.06, $"{p}/dinossaur.png"),
            E("Icy Flame",    N, RegionType.Frostfire, 160, 32, 16, 10, 0.07, 3.8m, 0.13, 0.06, $"{p}/icyflame.png"),
            E("Frost Lizard", N, RegionType.Frostfire, 190, 30, 12, 12, 0.06, 3.8m, 0.13, 0.06, $"{p}/lizard.png"),
            E("Penguin",      N, RegionType.Frostfire, 170, 28, 14, 10, 0.05, 3.8m, 0.13, 0.06, $"{p}/penguin.png"),
            E("Warrior",      N, RegionType.Frostfire, 210, 34, 10, 14, 0.06, 3.8m, 0.13, 0.06, $"{p}/warrior.png"),
            E("Frost Wisp",   N, RegionType.Frostfire, 150, 32, 18,  8, 0.09, 3.8m, 0.13, 0.06, $"{p}/wisp.png"),
            // Bosses
            B("Fighter",     RegionType.Frostfire, 1150,  94, 10, 38, 0.09, 31.0m, 0.46, 0.22, $"{p}/boss_1_fighter.png",   1310),
            B("Phoenix",     RegionType.Frostfire, 1200,  98, 16, 34, 0.11, 32.0m, 0.47, 0.22, $"{p}/boss_2_phoenix.png",   1320, PlacementType.Aerial),
            B("Demon",       RegionType.Frostfire, 1250,  96, 12, 40, 0.09, 33.0m, 0.48, 0.23, $"{p}/boss_3_demon.png",     1330),
            B("Frost Titan", RegionType.Frostfire, 1300, 100,  8, 42, 0.08, 34.0m, 0.49, 0.23, $"{p}/boss_4_titan.png",     1340),
            B("Samurai",     RegionType.Frostfire, 1350, 102, 12, 44, 0.10, 35.0m, 0.50, 0.24, $"{p}/boss_5_samurai.png",   1350),
            B("Dinosaur",    RegionType.Frostfire, 1400, 104,  8, 46, 0.08, 36.0m, 0.51, 0.24, $"{p}/boss_6_dinossaur.png", 1360),
            B("Boreal",      RegionType.Frostfire, 1350, 106, 14, 40, 0.10, 37.0m, 0.52, 0.25, $"{p}/boss_7_boreal.png",    1370),
            B("Warden",      RegionType.Frostfire, 1400, 108,  8, 44, 0.09, 38.0m, 0.53, 0.25, $"{p}/boss_8_warden.png",    1380),
            B("Wyrm",        RegionType.Frostfire, 1450, 110, 10, 46, 0.10, 39.0m, 0.54, 0.26, $"{p}/boss_9_wrymm.png",     1390),
            B("Frost Rex",   RegionType.Frostfire, 1550, 114,  6, 50, 0.11, 40.0m, 0.55, 0.27, $"{p}/boss_10_rex.png",      1400),
        };
    }

    // ───────────────────────────── Corruption (1401-1500) ─────────────────────────────
    private static List<StageEnemy> SeedCorruptionEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/corruption";
        return new List<StageEnemy>
        {
            E("Blight",    N, RegionType.Corruption, 200, 34, 10, 14, 0.06, 3.9m, 0.13, 0.07, $"{p}/blight.png"),
            E("Decay",     N, RegionType.Corruption, 230, 30,  6, 16, 0.04, 3.9m, 0.13, 0.07, $"{p}/decay.png"),
            E("Infected",  N, RegionType.Corruption, 190, 32, 12, 12, 0.06, 3.9m, 0.13, 0.07, $"{p}/infected.png"),
            E("Knight",    N, RegionType.Corruption, 220, 36, 10, 16, 0.07, 3.9m, 0.13, 0.07, $"{p}/knight.png"),
            E("Miasma",    N, RegionType.Corruption, 160, 30, 16, 10, 0.08, 3.9m, 0.13, 0.07, $"{p}/miasma.png"),
            E("Parasite",  N, RegionType.Corruption, 170, 28, 14, 10, 0.06, 3.9m, 0.13, 0.07, $"{p}/parasite.png"),
            E("Plague",    N, RegionType.Corruption, 180, 32, 12, 12, 0.07, 3.9m, 0.13, 0.07, $"{p}/plague.png"),
            E("Rot",       N, RegionType.Corruption, 240, 28,  4, 18, 0.03, 3.9m, 0.13, 0.07, $"{p}/rot.png"),
            E("Tainted",   N, RegionType.Corruption, 210, 34,  8, 14, 0.05, 3.9m, 0.13, 0.07, $"{p}/tainted.png"),
            E("Toxic",     N, RegionType.Corruption, 175, 30, 14, 10, 0.07, 3.9m, 0.13, 0.07, $"{p}/toxic.png"),
            // Bosses
            B("Lord",      RegionType.Corruption, 1200,  96, 10, 40, 0.09, 32.0m, 0.47, 0.22, $"{p}/boss_1_lord.png",     1410),
            B("Beast",     RegionType.Corruption, 1250, 100, 14, 38, 0.10, 33.0m, 0.48, 0.23, $"{p}/boss_2_beast.png",    1420),
            B("Titan",     RegionType.Corruption, 1300,  98,  8, 42, 0.08, 34.0m, 0.49, 0.23, $"{p}/boss_3_titan.png",    1430),
            B("Drake",     RegionType.Corruption, 1250, 102, 14, 38, 0.11, 35.0m, 0.50, 0.24, $"{p}/boss_4_drake.png",    1440),
            B("Guardian",  RegionType.Corruption, 1350, 104,  8, 44, 0.08, 36.0m, 0.51, 0.24, $"{p}/boss_5_guardian.png",  1450),
            B("Hydra",     RegionType.Corruption, 1400, 106, 10, 42, 0.09, 37.0m, 0.52, 0.25, $"{p}/boss_6_hydra.png",    1460),
            B("Wurm",      RegionType.Corruption, 1350, 108, 12, 40, 0.10, 38.0m, 0.53, 0.25, $"{p}/boss_7_wurm.png",     1470),
            B("Golem",     RegionType.Corruption, 1450, 102,  6, 48, 0.07, 39.0m, 0.54, 0.26, $"{p}/boss_8_golem.png",    1480),
            B("King",      RegionType.Corruption, 1500, 110, 10, 46, 0.10, 40.0m, 0.55, 0.26, $"{p}/boss_9_king.png",     1490),
            B("Entropy",   RegionType.Corruption, 1600, 114,  8, 52, 0.10, 41.0m, 0.56, 0.27, $"{p}/boss_10_entropy.png", 1500),
        };
    }

    // ───────────────────────────── Dark (1501-1600) ─────────────────────────────
    private static List<StageEnemy> SeedDarkEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/dark";
        return new List<StageEnemy>
        {
            E("Banshee",  N, RegionType.Dark, 180, 34, 14, 12, 0.07, 4.0m, 0.13, 0.07, $"{p}/banshee.png"),
            E("Demon",    N, RegionType.Dark, 200, 36, 12, 14, 0.08, 4.0m, 0.13, 0.07, $"{p}/demon.png"),
            E("Ghost",    N, RegionType.Dark, 140, 30, 16, 8,  0.08, 4.0m, 0.13, 0.07, $"{p}/ghost.png"),
            E("Mantis",   N, RegionType.Dark, 160, 34, 18, 10, 0.09, 4.0m, 0.13, 0.07, $"{p}/mantis.png"),
            E("Troll",    N, RegionType.Dark, 230, 30, 6,  18, 0.05, 4.0m, 0.13, 0.07, $"{p}/troll.png"),
            E("Undead",   N, RegionType.Dark, 190, 28, 8,  16, 0.05, 4.0m, 0.13, 0.07, $"{p}/undead.png"),
            E("Vampire",  N, RegionType.Dark, 170, 32, 16, 10, 0.09, 4.0m, 0.13, 0.07, $"{p}/vampire.png"),
            E("Werewolf", N, RegionType.Dark, 185, 36, 14, 12, 0.08, 4.0m, 0.13, 0.07, $"{p}/werewolf.png"),
            E("Dark Wolf",N, RegionType.Dark, 165, 30, 14, 10, 0.07, 4.0m, 0.13, 0.07, $"{p}/wolf.png"),
            E("Zombie",   N, RegionType.Dark, 210, 26, 6,  16, 0.04, 4.0m, 0.13, 0.07, $"{p}/zombie.png"),
            // Bosses
            B("Colossus",     RegionType.Dark, 1300,102, 8,  46, 0.08, 34.0m, 0.49, 0.23, $"{p}/boss_1_colossus.png",    1510),
            B("Wraith King",  RegionType.Dark, 1250,106,12, 42, 0.10, 35.0m, 0.50, 0.24, $"{p}/boss_2_wraithking.png",  1520),
            B("Ogre",         RegionType.Dark, 1400, 98, 6,  50, 0.07, 36.0m, 0.51, 0.24, $"{p}/boss_3_ogre.png",       1530),
            B("Dark Mantis",  RegionType.Dark, 1200,108,18, 38, 0.12, 37.0m, 0.52, 0.25, $"{p}/boss_4_mantis.png",     1540),
            B("Phantom",      RegionType.Dark, 1150,104,20, 36, 0.12, 38.0m, 0.53, 0.25, $"{p}/boss_5_ghost.png",      1550),
            B("Undead Lord",   RegionType.Dark, 1350,100, 8,  48, 0.08, 39.0m, 0.54, 0.26, $"{p}/boss_6_undead.png",     1560),
            B("Zombie King",   RegionType.Dark, 1400,102, 6,  50, 0.07, 40.0m, 0.55, 0.26, $"{p}/boss_7_zombie.png",     1570),
            B("Minotaur",      RegionType.Dark, 1350,110,10, 44, 0.10, 41.0m, 0.56, 0.27, $"{p}/boss_8_minotaur.png",   1580),
            B("Dinosaur",      RegionType.Dark, 1500,108, 8,  52, 0.09, 42.0m, 0.57, 0.27, $"{p}/boss_9_dinossaur.png",  1590),
            B("Shadow Dragon", RegionType.Dark, 1600,115,12, 54, 0.11, 43.0m, 0.58, 0.28, $"{p}/boss_10_dragon.png",    1600),
        };
    }

    // ───────────────────────────── Alien (1601-1700) ─────────────────────────────
    private static List<StageEnemy> SeedAlienEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/alien";
        return new List<StageEnemy>
        {
            E("Astral",     N, RegionType.Alien, 190, 34, 14, 12, 0.07, 4.2m, 0.14, 0.07, $"{p}/astral.png"),
            E("Cosmic",     N, RegionType.Alien, 175, 36, 16, 10, 0.08, 4.2m, 0.14, 0.07, $"{p}/cosmic.png"),
            E("Hivemind",   N, RegionType.Alien, 220, 32, 10, 16, 0.06, 4.2m, 0.14, 0.07, $"{p}/hivemind.png"),
            E("Larva",      N, RegionType.Alien, 150, 28, 18,  8, 0.08, 4.2m, 0.14, 0.07, $"{p}/larva.png"),
            E("Mothership", N, RegionType.Alien, 250, 30,  4, 20, 0.04, 4.2m, 0.14, 0.07, $"{p}/mothership.png"),
            E("Nebula",     N, RegionType.Alien, 170, 34, 16, 10, 0.08, 4.2m, 0.14, 0.07, $"{p}/nebula.png"),
            E("Probe",      N, RegionType.Alien, 160, 30, 20,  8, 0.09, 4.2m, 0.14, 0.07, $"{p}/probe.png", placement: PlacementType.Aerial),
            E("Tentacle",   N, RegionType.Alien, 210, 36, 10, 14, 0.06, 4.2m, 0.14, 0.07, $"{p}/tentacle.png"),
            E("Voidwalker", N, RegionType.Alien, 200, 38, 12, 14, 0.08, 4.2m, 0.14, 0.07, $"{p}/voidwalker.png"),
            E("Xenomorph",  N, RegionType.Alien, 230, 40, 14, 12, 0.09, 4.2m, 0.14, 0.07, $"{p}/xenomorph.png"),
            // Bosses
            B("Queen",      RegionType.Alien, 1350, 102, 12, 42, 0.10, 36.0m, 0.51, 0.24, $"{p}/boss_1_queen.png",     1610),
            B("Cosmic",     RegionType.Alien, 1400, 106, 14, 40, 0.11, 37.0m, 0.52, 0.25, $"{p}/boss_2_cosmic.png",    1620),
            B("Lord",       RegionType.Alien, 1450, 104,  8, 44, 0.09, 38.0m, 0.53, 0.25, $"{p}/boss_3_lord.png",      1630),
            B("Starbeast",  RegionType.Alien, 1500, 108, 16, 42, 0.12, 39.0m, 0.54, 0.26, $"{p}/boss_4_starbeast.png", 1640),
            B("Horror",     RegionType.Alien, 1450, 110, 10, 46, 0.10, 40.0m, 0.55, 0.26, $"{p}/boss_5_horror.png",    1650),
            B("Tyrant",     RegionType.Alien, 1550, 112,  8, 48, 0.09, 41.0m, 0.56, 0.27, $"{p}/boss_6_tyrant.png",    1660),
            B("Dragon",     RegionType.Alien, 1500, 114, 14, 44, 0.11, 42.0m, 0.57, 0.27, $"{p}/boss_7_dragon.png",    1670),
            B("Hydra",      RegionType.Alien, 1600, 116, 10, 48, 0.10, 43.0m, 0.58, 0.28, $"{p}/boss_8_hydra.png",     1680),
            B("Titan",      RegionType.Alien, 1650, 118, 12, 50, 0.11, 44.0m, 0.59, 0.28, $"{p}/boss_9_titan.png",     1690),
            B("Threat",     RegionType.Alien, 1750, 122,  8, 54, 0.10, 45.0m, 0.60, 0.29, $"{p}/boss_10_threat.png",   1700),
        };
    }

    // ───────────────────────────── Void (1701-1800) ─────────────────────────────
    private static List<StageEnemy> SeedVoidEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/void";
        return new List<StageEnemy>
        {
            E("Void Alien",   N, RegionType.Void, 200, 36, 14, 14, 0.08, 4.4m, 0.14, 0.07, $"{p}/alien.png"),
            E("Void Armor",   N, RegionType.Void, 220, 30, 8,  18, 0.05, 4.4m, 0.14, 0.07, $"{p}/armor.png"),
            E("Void Bat",     N, RegionType.Void, 150, 28, 18, 8,  0.08, 4.4m, 0.14, 0.07, $"{p}/bat.png", placement: PlacementType.Aerial),
            E("Void Beast",   N, RegionType.Void, 210, 34, 12, 14, 0.07, 4.4m, 0.14, 0.07, $"{p}/beast.png"),
            E("Void Hound",   N, RegionType.Void, 180, 32, 14, 12, 0.07, 4.4m, 0.14, 0.07, $"{p}/hound.png"),
            E("Void Panther", N, RegionType.Void, 190, 36, 16, 10, 0.09, 4.4m, 0.14, 0.07, $"{p}/panther.png"),
            E("Void Serpent", N, RegionType.Void, 175, 34, 12, 12, 0.07, 4.4m, 0.14, 0.07, $"{p}/serpent.png"),
            E("Void Slime",   N, RegionType.Void, 230, 24, 4,  20, 0.03, 4.4m, 0.14, 0.07, $"{p}/slime.png"),
            E("Void Warrior", N, RegionType.Void, 200, 38, 12, 14, 0.08, 4.4m, 0.14, 0.07, $"{p}/warrior.png"),
            E("Void Whisp",   N, RegionType.Void, 130, 30, 20, 6,  0.10, 4.4m, 0.14, 0.07, $"{p}/whisp.png"),
            // Bosses
            B("Matriarch",      RegionType.Void, 1400,110,12, 44, 0.10, 38.0m, 0.53, 0.25, $"{p}/boss_1_matriarch.png",     1710),
            B("Void Slime",     RegionType.Void, 1500,104, 6,  52, 0.07, 39.0m, 0.54, 0.26, $"{p}/boss_2_slime.png",        1720),
            B("Void Snail",     RegionType.Void, 1450,100, 4,  54, 0.06, 40.0m, 0.55, 0.26, $"{p}/boss_3_snail.png",        1730),
            B("Void Bird",      RegionType.Void, 1350,108,18, 40, 0.12, 41.0m, 0.56, 0.27, $"{p}/boss_4_bird.png",          1740, PlacementType.Aerial),
            B("Void Octopus",   RegionType.Void, 1500,112,10, 46, 0.09, 42.0m, 0.57, 0.27, $"{p}/boss_5_octopus.png",      1750),
            B("Monstrosity",    RegionType.Void, 1600,116, 8,  50, 0.10, 43.0m, 0.58, 0.28, $"{p}/boss_6_monstrosity.png",  1760),
            B("Orbital Queen",  RegionType.Void, 1550,114,12, 48, 0.11, 44.0m, 0.59, 0.28, $"{p}/boss_7_orbitalqueen.png", 1770, PlacementType.Aerial),
            B("Death",          RegionType.Void, 1500,120,14, 44, 0.12, 45.0m, 0.60, 0.29, $"{p}/boss_8_death.png",        1780),
            B("Abomination",    RegionType.Void, 1650,118,10, 52, 0.10, 46.0m, 0.61, 0.29, $"{p}/boss_9_abomination.png",  1790),
            B("Black Hole",     RegionType.Void, 1800,125, 8,  58, 0.11, 47.0m, 0.62, 0.30, $"{p}/boss_10_blackhole.png",  1800),
        };
    }

    // ───────────────────────────── Timerift (1801-1900) ─────────────────────────────
    private static List<StageEnemy> SeedTimeriftEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/timerift";
        return new List<StageEnemy>
        {
            E("Anomaly",        N, RegionType.Timerift, 190, 36, 16, 10, 0.08, 4.6m, 0.15, 0.08, $"{p}/anomaly.png"),
            E("Chronos",        N, RegionType.Timerift, 230, 38, 10, 16, 0.06, 4.6m, 0.15, 0.08, $"{p}/chronos.png"),
            E("Echo",           N, RegionType.Timerift, 170, 34, 18,  8, 0.09, 4.6m, 0.15, 0.08, $"{p}/echo.png"),
            E("Flux",           N, RegionType.Timerift, 180, 36, 16, 10, 0.07, 4.6m, 0.15, 0.08, $"{p}/flux.png"),
            E("Future Specter", N, RegionType.Timerift, 200, 40, 14, 12, 0.08, 4.6m, 0.15, 0.08, $"{p}/futurespecter.png"),
            E("Paradox",        N, RegionType.Timerift, 210, 42, 12, 14, 0.07, 4.6m, 0.15, 0.08, $"{p}/paradox.png"),
            E("Rift",           N, RegionType.Timerift, 250, 34,  4, 20, 0.04, 4.6m, 0.15, 0.08, $"{p}/rift.png"),
            E("Shadow",         N, RegionType.Timerift, 195, 38, 14, 12, 0.08, 4.6m, 0.15, 0.08, $"{p}/shadow.png"),
            E("Temporal",       N, RegionType.Timerift, 185, 36, 18,  8, 0.09, 4.6m, 0.15, 0.08, $"{p}/temporal.png"),
            E("Timeloop",       N, RegionType.Timerift, 220, 40, 10, 14, 0.06, 4.6m, 0.15, 0.08, $"{p}/timeloop.png"),
            // Bosses
            B("Dragon",          RegionType.Timerift, 1500, 110, 14, 46, 0.11, 40.0m, 0.55, 0.26, $"{p}/boss_1_dragon.png",          1810),
            B("Time Lord",       RegionType.Timerift, 1550, 112, 16, 44, 0.12, 41.0m, 0.56, 0.27, $"{p}/boss_2_timelord.png",        1820),
            B("Titan",           RegionType.Timerift, 1600, 114,  8, 50, 0.09, 42.0m, 0.57, 0.27, $"{p}/boss_3_titan.png",           1830),
            B("Beast",           RegionType.Timerift, 1550, 116, 14, 46, 0.11, 43.0m, 0.58, 0.28, $"{p}/boss_4_beast.png",           1840),
            B("Hydra",           RegionType.Timerift, 1650, 118, 10, 48, 0.10, 44.0m, 0.59, 0.28, $"{p}/boss_5_hydra.png",           1850),
            B("Guardian",        RegionType.Timerift, 1700, 120,  8, 52, 0.09, 45.0m, 0.60, 0.29, $"{p}/boss_6_guardian.png",        1860),
            B("Demon",           RegionType.Timerift, 1650, 122, 16, 48, 0.12, 46.0m, 0.61, 0.29, $"{p}/boss_7_demon.png",           1870),
            B("Wurm",            RegionType.Timerift, 1750, 124, 10, 52, 0.10, 47.0m, 0.62, 0.30, $"{p}/boss_8_wurm.png",            1880),
            B("Reality Breaker", RegionType.Timerift, 1800, 128, 12, 54, 0.11, 48.0m, 0.63, 0.30, $"{p}/boss_9_realitybreaker.png",  1890),
            B("Infinity",        RegionType.Timerift, 1900, 132,  8, 58, 0.10, 49.0m, 0.64, 0.31, $"{p}/boss_10_infinity.png",       1900),
        };
    }

    // ───────────────────────────── Light (1901-2000) ─────────────────────────────
    private static List<StageEnemy> SeedLightEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/light";
        return new List<StageEnemy>
        {
            E("Archer",    N, RegionType.Light, 280, 52, 18, 14, 0.10, 4.8m, 0.15, 0.08, $"{p}/archer.png"),
            E("Construct", N, RegionType.Light, 320, 58, 14, 18, 0.08, 4.8m, 0.15, 0.08, $"{p}/construct.png"),
            E("Guard",     N, RegionType.Light, 300, 54, 16, 16, 0.09, 4.8m, 0.15, 0.08, $"{p}/guard.png"),
            E("Hound",     N, RegionType.Light, 250, 50, 21, 11, 0.13, 4.8m, 0.15, 0.08, $"{p}/hound.png"),
            E("Monk",      N, RegionType.Light, 240, 45, 20, 12, 0.12, 4.8m, 0.15, 0.08, $"{p}/monk.png"),
            E("Moth",      N, RegionType.Light, 220, 48, 22, 10, 0.14, 4.8m, 0.15, 0.08, $"{p}/moth.png", placement: PlacementType.Aerial),
            E("Purifier",  N, RegionType.Light, 310, 48, 13, 20, 0.07, 4.8m, 0.15, 0.08, $"{p}/purifier.png"),
            E("Sentinel",  N, RegionType.Light, 330, 56, 15, 17, 0.08, 4.8m, 0.15, 0.08, $"{p}/sentinel.png"),
            E("Templar",   N, RegionType.Light, 340, 50, 12, 22, 0.06, 4.8m, 0.15, 0.08, $"{p}/templar.png"),
            E("Wisp",      N, RegionType.Light, 260, 55, 17, 13, 0.15, 4.8m, 0.15, 0.08, $"{p}/wisp.png"),
            // Bosses
            B("Angel",     RegionType.Light, 1600, 120, 20, 48, 0.12, 42.0m, 0.57, 0.27, $"{p}/boss_1_angel.png",     1910),
            B("Archangel", RegionType.Light, 1650, 124, 22, 50, 0.13, 43.0m, 0.58, 0.28, $"{p}/boss_2_archangel.png", 1920),
            B("Paladin",   RegionType.Light, 1700, 118, 18, 54, 0.10, 44.0m, 0.59, 0.28, $"{p}/boss_3_paladin.png",   1930),
            B("Dragon",    RegionType.Light, 1750, 128, 24, 52, 0.15, 45.0m, 0.60, 0.29, $"{p}/boss_4_dragon.png",    1940),
            B("Mantis",    RegionType.Light, 1600, 126, 28, 46, 0.18, 46.0m, 0.61, 0.29, $"{p}/boss_5_mantis.png",    1950),
            B("Lion",      RegionType.Light, 1700, 130, 25, 50, 0.14, 47.0m, 0.62, 0.30, $"{p}/boss_6_lion.png",      1960),
            B("Phoenix",   RegionType.Light, 1750, 132, 26, 48, 0.16, 48.0m, 0.63, 0.30, $"{p}/boss_7_phoenix.png",   1970, PlacementType.Aerial),
            B("Golem",     RegionType.Light, 1850, 120, 12, 58, 0.08, 49.0m, 0.64, 0.31, $"{p}/boss_8_golem.png",     1980),
            B("Pegasus",   RegionType.Light, 1800, 135, 30, 50, 0.17, 50.0m, 0.65, 0.31, $"{p}/boss_9_pegasus.png",   1990, PlacementType.Aerial),
            B("Knight",    RegionType.Light, 1900, 140, 22, 56, 0.15, 51.0m, 0.66, 0.32, $"{p}/boss_10_knight.png",   2000),
        };
    }

    // ───────────────────────────── Shorthand helpers ─────────────────────────────

    private const EnemyType N = EnemyType.Normal;

    private static StageEnemy E(
        string name, EnemyType type, RegionType region,
        int hp, int power, int speed, int defense, double crit,
        decimal fidelisDrop, double finoDrop, double shotDrop,
        string sprite, PlacementType placement = PlacementType.Terrestrial)
        => StageEnemy.Create(name, type, region, hp, power, speed, defense, crit,
            fidelisDrop, finoDrop, shotDrop, sprite, placement: placement);

    private static StageEnemy B(
        string name, RegionType region,
        int hp, int power, int speed, int defense, double crit,
        decimal fidelisDrop, double finoDrop, double shotDrop,
        string sprite, int bossStage, PlacementType placement = PlacementType.Terrestrial)
        => StageEnemy.Create(name, EnemyType.Boss, region, hp, power, speed, defense, crit,
            fidelisDrop, finoDrop, shotDrop, sprite, bossStage, placement);
}
