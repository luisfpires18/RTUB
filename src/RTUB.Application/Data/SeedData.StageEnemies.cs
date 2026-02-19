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
            E("Bee",      N, RegionType.Forest, $"{p}/bee.png"),
            E("Beetle",   N, RegionType.Forest, $"{p}/beetle.png"),
            E("Cheetah",  N, RegionType.Forest, $"{p}/cheetah.png"),
            E("Eagle",    N, RegionType.Forest, $"{p}/eagle.png", placement: PlacementType.Aerial),
            E("Monkey",   N, RegionType.Forest, $"{p}/monkey.png"),
            E("Panther",  N, RegionType.Forest, $"{p}/panther.png"),
            E("Snake",    N, RegionType.Forest, $"{p}/snake.png"),
            E("Spider",   N, RegionType.Forest, $"{p}/spider.png"),
            E("Stag",     N, RegionType.Forest, $"{p}/stag.png"),
            E("Toucan",   N, RegionType.Forest, $"{p}/toucan.png", placement: PlacementType.Aerial),
            // Bosses
            B("Bear",      RegionType.Forest, $"{p}/boss_1_bear.png", 10),
            B("Tiger",     RegionType.Forest, $"{p}/boss_2_tiger.png", 20),
            B("Jaguar",    RegionType.Forest, $"{p}/boss_3_jaguar.png", 30),
            B("Falcon",    RegionType.Forest, $"{p}/boss_4_falcon.png", 40, PlacementType.Aerial),
            B("Leecher",   RegionType.Forest, $"{p}/boss_5_leecher.png", 50),
            B("Python",    RegionType.Forest, $"{p}/boss_6_python.png", 60),
            B("Centipede", RegionType.Forest, $"{p}/boss_7_centipede.png", 70),
            B("Mantis",    RegionType.Forest, $"{p}/boss_8_mantis.png", 80),
            B("Gorilla",   RegionType.Forest, $"{p}/boss_9_gorilla.png", 90),
            B("Lion",      RegionType.Forest, $"{p}/boss_10_lion.png", 100),
        };
    }

    // ───────────────────────────── Swamp (101-200) ─────────────────────────────
    private static List<StageEnemy> SeedSwampEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/swamp";
        return new List<StageEnemy>
        {
            E("Crab",       N, RegionType.Swamp, $"{p}/crab.png"),
            E("Crocodile",  N, RegionType.Swamp, $"{p}/crocodile.png"),
            E("Crow",       N, RegionType.Swamp, $"{p}/crow.png", placement: PlacementType.Aerial),
            E("Frog",       N, RegionType.Swamp, $"{p}/frog.png"),
            E("Leech",      N, RegionType.Swamp, $"{p}/leech.png"),
            E("Mosquito",   N, RegionType.Swamp, $"{p}/mosquito.png", placement: PlacementType.Aerial),
            E("Salamander", N, RegionType.Swamp, $"{p}/salamander.png"),
            E("Slime",      N, RegionType.Swamp, $"{p}/slime.png"),
            E("Swamp Snake",N, RegionType.Swamp, $"{p}/snake.png"),
            E("Stalker",    N, RegionType.Swamp, $"{p}/stalker.png"),
            // Bosses
            B("Frog King",     RegionType.Swamp, $"{p}/boss_1_frog.png", 110),
            B("Pelican",       RegionType.Swamp, $"{p}/boss_2_pelican.png", 120, PlacementType.Aerial),
            B("Giant Leech",   RegionType.Swamp, $"{p}/boss_3_leech.png", 130),
            B("Hydra",         RegionType.Swamp, $"{p}/boss_4_hydra.png", 140),
            B("Anaconda",      RegionType.Swamp, $"{p}/boss_5_anaconda.png", 150),
            B("Crayfish",      RegionType.Swamp, $"{p}/boss_6_crayfish.png", 160),
            B("Darner",        RegionType.Swamp, $"{p}/boss_7_darner.png", 170, PlacementType.Aerial),
            B("Hippopotamus",  RegionType.Swamp, $"{p}/boss_8_hippopotamus.png", 180),
            B("Troll",         RegionType.Swamp, $"{p}/boss_9_troll.png", 190),
            B("Aligator",     RegionType.Swamp, $"{p}/boss_10_aligator.png", 200),
        };
    }

    // ───────────────────────────── Mountains (201-300) ─────────────────────────────
    private static List<StageEnemy> SeedMountainsEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/mountains";
        return new List<StageEnemy>
        {
            E("Bear",     N, RegionType.Mountains, $"{p}/bear.png"),
            E("Boar",     N, RegionType.Mountains, $"{p}/boar.png"),
            E("Elephant", N, RegionType.Mountains, $"{p}/elephant.png"),
            E("Fox",      N, RegionType.Mountains, $"{p}/fox.png"),
            E("Goat",     N, RegionType.Mountains, $"{p}/goat.png"),
            E("Hyena",    N, RegionType.Mountains, $"{p}/hyena.png"),
            E("Pigeon",   N, RegionType.Mountains, $"{p}/pigeon.png", placement: PlacementType.Aerial),
            E("Rhino",    N, RegionType.Mountains, $"{p}/rhino.png"),
            E("Vulture",  N, RegionType.Mountains, $"{p}/vulture.png", placement: PlacementType.Aerial),
            E("Wolf",     N, RegionType.Mountains, $"{p}/wolf.png"),
            // Bosses
            B("Alpine",  RegionType.Mountains, $"{p}/boss_1_alpine.png", 210),
            B("Hawk",    RegionType.Mountains, $"{p}/boss_2_hawk.png", 220, PlacementType.Aerial),
            B("Alpaca",  RegionType.Mountains, $"{p}/boss_3_alpaca.png", 230),
            B("Fox",     RegionType.Mountains, $"{p}/boss_4_fox.png", 240),
            B("Giraffe", RegionType.Mountains, $"{p}/boss_5_giraffe.png", 250),
            B("Harpy",   RegionType.Mountains, $"{p}/boss_6_harpy.png", 260, PlacementType.Aerial),
            B("Wolf",    RegionType.Mountains, $"{p}/boss_7_wolf.png", 270),
            B("Bull",    RegionType.Mountains, $"{p}/boss_8_bull.png", 280),
            B("Gorilla", RegionType.Mountains, $"{p}/boss_9_gorilla.png", 290),
            B("Golem",   RegionType.Mountains, $"{p}/boss_10_golem.png", 300),
        };
    }

    // ───────────────────────────── Snowy (301-400) ─────────────────────────────
    private static List<StageEnemy> SeedSnowyEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/snowy";
        return new List<StageEnemy>
        {
            E("Polar Bear", N, RegionType.Snowy, $"{p}/bear.png"),
            E("Arctic Fox", N, RegionType.Snowy, $"{p}/fox.png"),
            E("Mammoth",    N, RegionType.Snowy, $"{p}/mammoth.png"),
            E("Owl",        N, RegionType.Snowy, $"{p}/owl.png", placement: PlacementType.Aerial),
            E("Penguin",    N, RegionType.Snowy, $"{p}/penguin.png"),
            E("Rabbit",     N, RegionType.Snowy, $"{p}/rabbit.png"),
            E("Reindeer",   N, RegionType.Snowy, $"{p}/reindeer.png"),
            E("Snowman",    N, RegionType.Snowy, $"{p}/snowman.png"),
            E("Snow Wolf",  N, RegionType.Snowy, $"{p}/wolf.png"),
            E("Yeti",       N, RegionType.Snowy, $"{p}/yeti.png"),
            // Bosses
            B("Ice Golem",  RegionType.Snowy, $"{p}/boss_1_golem.png", 310),
            B("Snowman",    RegionType.Snowy, $"{p}/boss_2_snoman.png", 320),
            B("Great Owl",  RegionType.Snowy, $"{p}/boss_3_owl.png", 330, PlacementType.Aerial),
            B("Panda",      RegionType.Snowy, $"{p}/boss_4_panda.png", 340),
            B("Snow Tiger", RegionType.Snowy, $"{p}/boss_5_tiger.png", 350),
            B("King Penguin",RegionType.Snowy, $"{p}/boss_6_penguin.png", 360),
            B("Elder Yeti", RegionType.Snowy, $"{p}/boss_7_yeti.png", 370),
            B("Direwolf",   RegionType.Snowy, $"{p}/boss_8_direwolf.png", 380),
            B("Mammoth",    RegionType.Snowy, $"{p}/boss_9_mammoth.png", 390),
            B("Frost Drake",RegionType.Snowy, $"{p}/boss_10_drake.png", 400),
        };
    }

    // ───────────────────────────── Tropical (401-500) ─────────────────────────────
    private static List<StageEnemy> SeedTropicalEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/tropical";
        return new List<StageEnemy>
        {
            E("Crab",      N, RegionType.Tropical, $"{p}/crab.png"),
            E("Dolphin",   N, RegionType.Tropical, $"{p}/dolphin.png"),
            E("Duck",      N, RegionType.Tropical, $"{p}/duck.png", placement: PlacementType.Aerial),
            E("Jellyfish", N, RegionType.Tropical, $"{p}/jellyfish.png"),
            E("Octopus",   N, RegionType.Tropical, $"{p}/octopus.png"),
            E("Seagull",   N, RegionType.Tropical, $"{p}/seagull.png", placement: PlacementType.Aerial),
            E("Seal",      N, RegionType.Tropical, $"{p}/seal.png"),
            E("Shark",     N, RegionType.Tropical, $"{p}/shark.png"),
            E("Starfish",  N, RegionType.Tropical, $"{p}/starfish.png"),
            E("Turtle",    N, RegionType.Tropical, $"{p}/turtle.png"),
            // Bosses
            B("Giant Crab",  RegionType.Tropical, $"{p}/boss_1_crab.png", 410),
            B("Kraken",      RegionType.Tropical, $"{p}/boss_2_kraken.png", 420),
            B("Shark Lord",  RegionType.Tropical, $"{p}/boss_3_shark.png", 430),
            B("Manta",       RegionType.Tropical, $"{p}/boss_4_manta.png", 440),
            B("Electric Eel",RegionType.Tropical, $"{p}/boss_5_eel.png", 450),
            B("Sea Snake",   RegionType.Tropical, $"{p}/boss_6_snake.png", 460),
            B("Jellyfish",   RegionType.Tropical, $"{p}/boss_7_jellyfish.png", 470),
            B("Sea Turtle",  RegionType.Tropical, $"{p}/boss_8_turtle.png", 480),
            B("Siren",       RegionType.Tropical, $"{p}/boss_9_siren.png", 490),
            B("Leviathan",   RegionType.Tropical, $"{p}/boss_10_leviathan.png", 500),
        };
    }

    // ───────────────────────────── Caverns (501-600) ─────────────────────────────
    private static List<StageEnemy> SeedCavernsEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/caverns";
        return new List<StageEnemy>
        {
            E("Armadillo", N, RegionType.Caverns, $"{p}/armadillo.png"),
            E("Bat",       N, RegionType.Caverns, $"{p}/bat.png", placement: PlacementType.Aerial),
            E("Beetle",    N, RegionType.Caverns, $"{p}/beetle.png"),
            E("Imp",       N, RegionType.Caverns, $"{p}/imp.png"),
            E("Lizard",    N, RegionType.Caverns, $"{p}/lizard.png"),
            E("Rat",       N, RegionType.Caverns, $"{p}/rat.png"),
            E("Shrimp",    N, RegionType.Caverns, $"{p}/shrimp.png"),
            E("Slug",      N, RegionType.Caverns, $"{p}/slug.png"),
            E("Spider",    N, RegionType.Caverns, $"{p}/spider.png"),
            E("Worm",      N, RegionType.Caverns, $"{p}/worm.png"),
            // Bosses
            B("Spider Queen", RegionType.Caverns, $"{p}/boss_1_spider.png", 510),
            B("Stone Golem",  RegionType.Caverns, $"{p}/boss_2_golem.png", 520),
            B("Fungus",       RegionType.Caverns, $"{p}/boss_3_fungus.png", 530),
            B("Wyrm",         RegionType.Caverns, $"{p}/boss_4_wyrmm.png", 540),
            B("Giant Bat",    RegionType.Caverns, $"{p}/boss_5_bat.png", 550, PlacementType.Aerial),
            B("Isopod",       RegionType.Caverns, $"{p}/boss_6_isopod.png", 560),
            B("Titan Beetle", RegionType.Caverns, $"{p}/boss_7_beetle.png", 570),
            B("Armadillo",    RegionType.Caverns, $"{p}/boss_8_armadillo.png", 580),
            B("Cave Hydra",   RegionType.Caverns, $"{p}/boss_9_hydra.png", 590),
            B("Basilisk",     RegionType.Caverns, $"{p}/boss_10_basilisk.png", 600),
        };
    }

    // ───────────────────────────── Desert (601-700) ─────────────────────────────
    private static List<StageEnemy> SeedDesertEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/desert";
        return new List<StageEnemy>
        {
            E("Desert Beetle", N, RegionType.Desert, $"{p}/beetle.png"),
            E("Cactus",        N, RegionType.Desert, $"{p}/cactus.png"),
            E("Desert Eagle",  N, RegionType.Desert, $"{p}/eagle.png", placement: PlacementType.Aerial),
            E("Lion",          N, RegionType.Desert, $"{p}/lion.png"),
            E("Lizard",        N, RegionType.Desert, $"{p}/lizard.png"),
            E("Scarab",        N, RegionType.Desert, $"{p}/scarab.png"),
            E("Scorpion",      N, RegionType.Desert, $"{p}/scorpion.png"),
            E("Desert Vulture", N, RegionType.Desert, $"{p}/vulture.png", placement: PlacementType.Aerial),
            E("Wisp",          N, RegionType.Desert, $"{p}/wisp.png"),
            E("Sandworm",      N, RegionType.Desert, $"{p}/worm.png"),
            // Bosses
            B("Djinn",       RegionType.Desert, $"{p}/boss_1_djinn.png", 610),
            B("Scorpion King",RegionType.Desert, $"{p}/boss_2_scorpion.png", 620),
            B("Roc",          RegionType.Desert, $"{p}/boss_3_roc.png", 630, PlacementType.Aerial),
            B("Persian",      RegionType.Desert, $"{p}/boss_4_persian.png", 640),
            B("Vulture Lord", RegionType.Desert, $"{p}/boss_5_vulture.png", 650, PlacementType.Aerial),
            B("Sphinx",       RegionType.Desert, $"{p}/boss_6_sphinx.png", 660),
            B("Cactus",       RegionType.Desert, $"{p}/boss_7_cactus.png", 670),
            B("Anubis",       RegionType.Desert, $"{p}/boss_8_anubis.png", 680),
            B("Camel",        RegionType.Desert, $"{p}/boss_9_camel.png", 690),
            B("Great Worm",   RegionType.Desert, $"{p}/boss_10_worm.png", 700),
        };
    }

    // ───────────────────────────── Volcanic (701-800) ─────────────────────────────
    private static List<StageEnemy> SeedVolcanicEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/volcanic";
        return new List<StageEnemy>
        {
            E("Badger",          N, RegionType.Volcanic, $"{p}/badger.png"),
            E("Lava Bat",       N, RegionType.Volcanic, $"{p}/bat.png", placement: PlacementType.Aerial),
            E("Fire Beetle",    N, RegionType.Volcanic, $"{p}/beetle.png"),
            E("Dragon",         N, RegionType.Volcanic, $"{p}/dragon.png"),
            E("Drake",          N, RegionType.Volcanic, $"{p}/drake.png"),
            E("Lava Duck",      N, RegionType.Volcanic, $"{p}/duck.png"),
            E("Fire Lizard",    N, RegionType.Volcanic, $"{p}/lizard.png"),
            E("Salamander",     N, RegionType.Volcanic, $"{p}/salamander.png"),
            E("Two-Headed Dragon",N,RegionType.Volcanic, $"{p}/twoheadeddragon.png"),
            E("Magma Wolf",     N, RegionType.Volcanic, $"{p}/wolf.png"),
            // Bosses
            B("Fire Scorpion",RegionType.Volcanic, $"{p}/boss_1_scorpion.png", 710),
            B("Lava Snake",   RegionType.Volcanic, $"{p}/boss_2_snake.png", 720),
            B("Fire Rhino",   RegionType.Volcanic, $"{p}/boss_3_rhino.png", 730),
            B("Infernal Wisp",RegionType.Volcanic, $"{p}/boss_4_wisp.png", 740),
            B("Hellhound",    RegionType.Volcanic, $"{p}/boss_5_dog.png", 750),
            B("Lava Turtle",  RegionType.Volcanic, $"{p}/boss_6_turtle.png", 760),
            B("Fire Gorilla", RegionType.Volcanic, $"{p}/boss_7_gorilla.png", 770),
            B("Cerberus",     RegionType.Volcanic, $"{p}/boss_8_cerebrus.png", 780),
            B("Phoenix",      RegionType.Volcanic, $"{p}/boss_9_phoenix.png", 790, PlacementType.Aerial),
            B("Inferno Dragon",RegionType.Volcanic, $"{p}/boss_10_dragon.png", 800),
        };
    }

    // ───────────────────────────── Ruins (801-900) ─────────────────────────────
    private static List<StageEnemy> SeedRuinsEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/ruins";
        return new List<StageEnemy>
        {
            E("Bat",     N, RegionType.Ruins, $"{p}/bat.png", placement: PlacementType.Aerial),
            E("Ghost",   N, RegionType.Ruins, $"{p}/ghost.png"),
            E("Hound",   N, RegionType.Ruins, $"{p}/hound.png"),
            E("Lantern", N, RegionType.Ruins, $"{p}/lantern.png"),
            E("Mummy",   N, RegionType.Ruins, $"{p}/mummy.png"),
            E("Plant",   N, RegionType.Ruins, $"{p}/plant.png"),
            E("Rat",     N, RegionType.Ruins, $"{p}/rat.png"),
            E("Raven",   N, RegionType.Ruins, $"{p}/raven.png", placement: PlacementType.Aerial),
            E("Scarab",  N, RegionType.Ruins, $"{p}/scarab.png"),
            E("Trap",    N, RegionType.Ruins, $"{p}/trap.png"),
            // Bosses
            B("Sentinel",  RegionType.Ruins, $"{p}/boss_1_sentinel.png", 810),
            B("Pharaoh",   RegionType.Ruins, $"{p}/boss_2_pharao.png", 820),
            B("Medusa",    RegionType.Ruins, $"{p}/boss_3_medusa.png", 830),
            B("Warden",    RegionType.Ruins, $"{p}/boss_4_warden.png", 840),
            B("Hydra",     RegionType.Ruins, $"{p}/boss_5_hydra.png", 850),
            B("Knight",    RegionType.Ruins, $"{p}/boss_6_knight.png", 860),
            B("Golem",     RegionType.Ruins, $"{p}/boss_7_golem.png", 870),
            B("Colossus",  RegionType.Ruins, $"{p}/boss_8_colossus.png", 880),
            B("Guardian",  RegionType.Ruins, $"{p}/boss_9_guardian.png", 890),
            B("Living Statue",RegionType.Ruins, $"{p}/boss_10_statue.png", 900),
        };
    }

    // ───────────────────────────── Sky (901-1000) — incomplete: only 6 bosses ─────────────────────────────
    private static List<StageEnemy> SeedSkyEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/sky";
        return new List<StageEnemy>
        {
            E("Albatross", N, RegionType.Sky, $"{p}/albatross.png", placement: PlacementType.Aerial),
            E("Balloon",   N, RegionType.Sky, $"{p}/baloon.png"),
            E("Birdman",   N, RegionType.Sky, $"{p}/birdman.png", placement: PlacementType.Aerial),
            E("Cloud",     N, RegionType.Sky, $"{p}/cloud.png"),
            E("Gull",      N, RegionType.Sky, $"{p}/gull.png", placement: PlacementType.Aerial),
            E("Harpy",     N, RegionType.Sky, $"{p}/harpy.png", placement: PlacementType.Aerial),
            E("Heron",     N, RegionType.Sky, $"{p}/heron.png", placement: PlacementType.Aerial),
            E("Kestrel",   N, RegionType.Sky, $"{p}/kestrel.png", placement: PlacementType.Aerial),
            E("Raven",     N, RegionType.Sky, $"{p}/raven.png", placement: PlacementType.Aerial),
            E("Swift",     N, RegionType.Sky, $"{p}/swift.png", placement: PlacementType.Aerial),
            // Bosses (6 available, 4 placeholder — fallback will resolve to random from previous stages)
            B("Goose",    RegionType.Sky, $"{p}/boss_1_goose.png", 910, PlacementType.Aerial),
            B("Kestrel",  RegionType.Sky, $"{p}/boss_2_kestrel.png", 920, PlacementType.Aerial),
            B("Osprey",   RegionType.Sky, $"{p}/boss_3_osprey.png", 930, PlacementType.Aerial),
            B("Raven",    RegionType.Sky, $"{p}/boss_4_raven.png", 940, PlacementType.Aerial),
            B("Condor",   RegionType.Sky, $"{p}/boss_5_condor.png", 950, PlacementType.Aerial),
            B("Behemoth", RegionType.Sky, $"{p}/boss_6_behemoth.png", 960),
            B("Cyclone",  RegionType.Sky, $"{p}/boss_7_cyclone.png", 970),
            B("Wyvern",   RegionType.Sky, $"{p}/boss_8_wyvern.png", 980, PlacementType.Aerial),
            B("Serpent",   RegionType.Sky, $"{p}/boss_9_serpent.png", 990),
            B("Starbird",  RegionType.Sky, $"{p}/boss_10_starbird.png", 1000, PlacementType.Aerial),
        };
    }

    // ───────────────────────────── Underwater (1001-1100) ─────────────────────────────
    private static List<StageEnemy> SeedUnderwaterEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/underwater";
        return new List<StageEnemy>
        {
            E("Anglerfish", N, RegionType.Underwater, $"{p}/anglerfish.png"),
            E("Barracuda",  N, RegionType.Underwater, $"{p}/barracuda.png"),
            E("Eel",        N, RegionType.Underwater, $"{p}/eel.png"),
            E("Knight",     N, RegionType.Underwater, $"{p}/knight.png"),
            E("Nautilus",   N, RegionType.Underwater, $"{p}/nautilus.png"),
            E("Piranha",    N, RegionType.Underwater, $"{p}/piranha.png"),
            E("Salmon",     N, RegionType.Underwater, $"{p}/salmon.png"),
            E("Sardine",    N, RegionType.Underwater, $"{p}/sardine.png"),
            E("Seahorse",   N, RegionType.Underwater, $"{p}/seahorse.png"),
            E("Shark",      N, RegionType.Underwater, $"{p}/shark.png"),
            // Bosses
            B("Kraken",    RegionType.Underwater, $"{p}/boss_1_kraken.png", 1010),
            B("Leviathan", RegionType.Underwater, $"{p}/boss_2_leviathan.png", 1020),
            B("Megalodon", RegionType.Underwater, $"{p}/boss_3_megalodon.png", 1030),
            B("Serpent",   RegionType.Underwater, $"{p}/boss_4_serpent.png", 1040),
            B("Whale",     RegionType.Underwater, $"{p}/boss_5_whale.png", 1050),
            B("Abyssal",   RegionType.Underwater, $"{p}/boss_6_abyssal.png", 1060),
            B("Trident",   RegionType.Underwater, $"{p}/boss_7_trident.png", 1070),
            B("Poseidon",  RegionType.Underwater, $"{p}/boss_8_poseidon.png", 1080),
            B("Hydra",     RegionType.Underwater, $"{p}/boss_9_hydra.png", 1090),
            B("Charybdis", RegionType.Underwater, $"{p}/boss_10_charybdis.png", 1100),
        };
    }

    // ───────────────────────────── Underground (1101-1200) ─────────────────────────────
    private static List<StageEnemy> SeedUndergroundEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/underground";
        return new List<StageEnemy>
        {
            E("Beetle",     N, RegionType.Underground, $"{p}/beetle.png"),
            E("Centipede",  N, RegionType.Underground, $"{p}/centipede.png"),
            E("Crystal",    N, RegionType.Underground, $"{p}/crystal.png"),
            E("Glowworm",   N, RegionType.Underground, $"{p}/glowworm.png"),
            E("Goblin",     N, RegionType.Underground, $"{p}/goblin.png"),
            E("Golem",      N, RegionType.Underground, $"{p}/golem.png"),
            E("Mole",       N, RegionType.Underground, $"{p}/mole.png"),
            E("Mushroom",   N, RegionType.Underground, $"{p}/mushroom.png"),
            E("Stalactite", N, RegionType.Underground, $"{p}/stalactite.png"),
            E("Worm",       N, RegionType.Underground, $"{p}/worm.png"),
            // Bosses
            B("Worm",          RegionType.Underground, $"{p}/boss_1_worm.png", 1110),
            B("King",          RegionType.Underground, $"{p}/boss_2_king.png", 1120),
            B("Crystal",       RegionType.Underground, $"{p}/boss_3_crystal.png", 1130),
            B("Spider Queen",  RegionType.Underground, $"{p}/boss_4_spiderqueen.png", 1140),
            B("Earth Titan",   RegionType.Underground, $"{p}/boss_5_earthtitan.png", 1150),
            B("Magma Dweller", RegionType.Underground, $"{p}/boss_6_magmadweller.png", 1160),
            B("Drake",         RegionType.Underground, $"{p}/boss_7_drake.png", 1170),
            B("Shadow",        RegionType.Underground, $"{p}/boss_8_shadow.png", 1180),
            B("Tunnel Horror", RegionType.Underground, $"{p}/boss_9_tunnelhorror.png", 1190),
            B("Ancient One",   RegionType.Underground, $"{p}/boss_10_ancientone.png", 1200),
        };
    }

    // ───────────────────────────── Mechanical (1201-1300) ─────────────────────────────
    private static List<StageEnemy> SeedMechanicalEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/mechanical";
        return new List<StageEnemy>
        {
            E("Automaton",  N, RegionType.Mechanical, $"{p}/automaton.png"),
            E("Circuit",    N, RegionType.Mechanical, $"{p}/circuit.png"),
            E("Drone",      N, RegionType.Mechanical, $"{p}/drone.png", placement: PlacementType.Aerial),
            E("Engine",     N, RegionType.Mechanical, $"{p}/engine.png"),
            E("Gear",       N, RegionType.Mechanical, $"{p}/gear.png"),
            E("Piston",     N, RegionType.Mechanical, $"{p}/piston.png"),
            E("Robot",      N, RegionType.Mechanical, $"{p}/robot.png"),
            E("Spark",      N, RegionType.Mechanical, $"{p}/spark.png"),
            E("Steambot",   N, RegionType.Mechanical, $"{p}/steambot.png"),
            E("Wire",       N, RegionType.Mechanical, $"{p}/wire.png"),
            // Bosses
            B("Titan",       RegionType.Mechanical, $"{p}/boss_1_titan.png", 1210),
            B("Spider",      RegionType.Mechanical, $"{p}/boss_2_spider.png", 1220),
            B("Iron Giant",  RegionType.Mechanical, $"{p}/boss_3_irongiant.png", 1230),
            B("Dragon",      RegionType.Mechanical, $"{p}/boss_4_dragon.png", 1240),
            B("Steam Lord",  RegionType.Mechanical, $"{p}/boss_5_steamlord.png", 1250),
            B("Hydra",       RegionType.Mechanical, $"{p}/boss_6_hydra.png", 1260),
            B("Beast",       RegionType.Mechanical, $"{p}/boss_7_beast.png", 1270),
            B("Sentinel",    RegionType.Mechanical, $"{p}/boss_8_sentinel.png", 1280),
            B("Humanoid",    RegionType.Mechanical, $"{p}/boss_9_humanoid.png", 1290),
            B("Android",     RegionType.Mechanical, $"{p}/boss_10_android.png", 1300),
        };
    }

    // ───────────────────────────── Frostfire (1301-1400) ─────────────────────────────
    private static List<StageEnemy> SeedFrostfireEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/frostfire";
        return new List<StageEnemy>
        {
            E("Beast",        N, RegionType.Frostfire, $"{p}/beast.png"),
            E("Frostbird",    N, RegionType.Frostfire, $"{p}/bird.png", placement: PlacementType.Aerial),
            E("Wolf",       N, RegionType.Frostfire, $"{p}/wolf.png"),
            E("Crystal",      N, RegionType.Frostfire, $"{p}/crystal.png"),
            E("Dinosaur",     N, RegionType.Frostfire, $"{p}/dinossaur.png"),
            E("Icy Flame",    N, RegionType.Frostfire, $"{p}/icyflame.png"),
            E("Frost Lizard", N, RegionType.Frostfire, $"{p}/lizard.png"),
            E("Penguin",      N, RegionType.Frostfire, $"{p}/penguin.png"),
            E("Warrior",      N, RegionType.Frostfire, $"{p}/warrior.png"),
            E("Frost Wisp",   N, RegionType.Frostfire, $"{p}/wisp.png"),
            // Bosses
            B("Fighter",     RegionType.Frostfire, $"{p}/boss_1_fighter.png", 1310),
            B("Phoenix",     RegionType.Frostfire, $"{p}/boss_2_phoenix.png", 1320, PlacementType.Aerial),
            B("Demon",       RegionType.Frostfire, $"{p}/boss_3_demon.png", 1330),
            B("Frost Titan", RegionType.Frostfire, $"{p}/boss_4_titan.png", 1340),
            B("Samurai",     RegionType.Frostfire, $"{p}/boss_5_samurai.png", 1350),
            B("Dinosaur",    RegionType.Frostfire, $"{p}/boss_6_dinossaur.png", 1360),
            B("Boreal",      RegionType.Frostfire, $"{p}/boss_7_boreal.png", 1370),
            B("Warden",      RegionType.Frostfire, $"{p}/boss_8_warden.png", 1380),
            B("Wyrm",        RegionType.Frostfire, $"{p}/boss_9_wrymm.png", 1390),
            B("Frost Rex",   RegionType.Frostfire, $"{p}/boss_10_rex.png", 1400),
        };
    }

    // ───────────────────────────── Corruption (1401-1500) ─────────────────────────────
    private static List<StageEnemy> SeedCorruptionEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/corruption";
        return new List<StageEnemy>
        {
            E("Blight",    N, RegionType.Corruption, $"{p}/blight.png"),
            E("Decay",     N, RegionType.Corruption, $"{p}/decay.png"),
            E("Infected",  N, RegionType.Corruption, $"{p}/infected.png"),
            E("Knight",    N, RegionType.Corruption, $"{p}/knight.png"),
            E("Miasma",    N, RegionType.Corruption, $"{p}/miasma.png"),
            E("Parasite",  N, RegionType.Corruption, $"{p}/parasite.png"),
            E("Plague",    N, RegionType.Corruption, $"{p}/plague.png"),
            E("Rot",       N, RegionType.Corruption, $"{p}/rot.png"),
            E("Tainted",   N, RegionType.Corruption, $"{p}/tainted.png"),
            E("Toxic",     N, RegionType.Corruption, $"{p}/toxic.png"),
            // Bosses
            B("Lord",      RegionType.Corruption, $"{p}/boss_1_lord.png", 1410),
            B("Beast",     RegionType.Corruption, $"{p}/boss_2_beast.png", 1420),
            B("Titan",     RegionType.Corruption, $"{p}/boss_3_titan.png", 1430),
            B("Drake",     RegionType.Corruption, $"{p}/boss_4_drake.png", 1440),
            B("Guardian",  RegionType.Corruption, $"{p}/boss_5_guardian.png", 1450),
            B("Hydra",     RegionType.Corruption, $"{p}/boss_6_hydra.png", 1460),
            B("Wurm",      RegionType.Corruption, $"{p}/boss_7_wurm.png", 1470),
            B("Golem",     RegionType.Corruption, $"{p}/boss_8_golem.png", 1480),
            B("King",      RegionType.Corruption, $"{p}/boss_9_king.png", 1490),
            B("Entropy",   RegionType.Corruption, $"{p}/boss_10_entropy.png", 1500),
        };
    }

    // ───────────────────────────── Dark (1501-1600) ─────────────────────────────
    private static List<StageEnemy> SeedDarkEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/dark";
        return new List<StageEnemy>
        {
            E("Banshee",  N, RegionType.Dark, $"{p}/banshee.png"),
            E("Demon",    N, RegionType.Dark, $"{p}/demon.png"),
            E("Ghost",    N, RegionType.Dark, $"{p}/ghost.png"),
            E("Mantis",   N, RegionType.Dark, $"{p}/mantis.png"),
            E("Troll",    N, RegionType.Dark, $"{p}/troll.png"),
            E("Undead",   N, RegionType.Dark, $"{p}/undead.png"),
            E("Vampire",  N, RegionType.Dark, $"{p}/vampire.png"),
            E("Werewolf", N, RegionType.Dark, $"{p}/werewolf.png"),
            E("Dark Wolf",N, RegionType.Dark, $"{p}/wolf.png"),
            E("Zombie",   N, RegionType.Dark, $"{p}/zombie.png"),
            // Bosses
            B("Colossus",     RegionType.Dark, $"{p}/boss_1_colossus.png", 1510),
            B("Wraith King",  RegionType.Dark, $"{p}/boss_2_wraithking.png", 1520),
            B("Ogre",         RegionType.Dark, $"{p}/boss_3_ogre.png", 1530),
            B("Dark Mantis",  RegionType.Dark, $"{p}/boss_4_mantis.png", 1540),
            B("Phantom",      RegionType.Dark, $"{p}/boss_5_ghost.png", 1550),
            B("Undead Lord",   RegionType.Dark, $"{p}/boss_6_undead.png", 1560),
            B("Zombie King",   RegionType.Dark, $"{p}/boss_7_zombie.png", 1570),
            B("Minotaur",      RegionType.Dark, $"{p}/boss_8_minotaur.png", 1580),
            B("Dinosaur",      RegionType.Dark, $"{p}/boss_9_dinossaur.png", 1590),
            B("Shadow Dragon", RegionType.Dark, $"{p}/boss_10_dragon.png", 1600),
        };
    }

    // ───────────────────────────── Alien (1601-1700) ─────────────────────────────
    private static List<StageEnemy> SeedAlienEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/alien";
        return new List<StageEnemy>
        {
            E("Astral",     N, RegionType.Alien, $"{p}/astral.png"),
            E("Cosmic",     N, RegionType.Alien, $"{p}/cosmic.png"),
            E("Hivemind",   N, RegionType.Alien, $"{p}/hivemind.png"),
            E("Larva",      N, RegionType.Alien, $"{p}/larva.png"),
            E("Mothership", N, RegionType.Alien, $"{p}/mothership.png"),
            E("Nebula",     N, RegionType.Alien, $"{p}/nebula.png"),
            E("Probe",      N, RegionType.Alien, $"{p}/probe.png", placement: PlacementType.Aerial),
            E("Tentacle",   N, RegionType.Alien, $"{p}/tentacle.png"),
            E("Voidwalker", N, RegionType.Alien, $"{p}/voidwalker.png"),
            E("Xenomorph",  N, RegionType.Alien, $"{p}/xenomorph.png"),
            // Bosses
            B("Queen",      RegionType.Alien, $"{p}/boss_1_queen.png", 1610),
            B("Cosmic",     RegionType.Alien, $"{p}/boss_2_cosmic.png", 1620),
            B("Lord",       RegionType.Alien, $"{p}/boss_3_lord.png", 1630),
            B("Starbeast",  RegionType.Alien, $"{p}/boss_4_starbeast.png", 1640),
            B("Horror",     RegionType.Alien, $"{p}/boss_5_horror.png", 1650),
            B("Tyrant",     RegionType.Alien, $"{p}/boss_6_tyrant.png", 1660),
            B("Dragon",     RegionType.Alien, $"{p}/boss_7_dragon.png", 1670),
            B("Hydra",      RegionType.Alien, $"{p}/boss_8_hydra.png", 1680),
            B("Titan",      RegionType.Alien, $"{p}/boss_9_titan.png", 1690),
            B("Threat",     RegionType.Alien, $"{p}/boss_10_threat.png", 1700),
        };
    }

    // ───────────────────────────── Void (1701-1800) ─────────────────────────────
    private static List<StageEnemy> SeedVoidEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/void";
        return new List<StageEnemy>
        {
            E("Void Alien",   N, RegionType.Void, $"{p}/alien.png"),
            E("Void Armor",   N, RegionType.Void, $"{p}/armor.png"),
            E("Void Bat",     N, RegionType.Void, $"{p}/bat.png", placement: PlacementType.Aerial),
            E("Void Beast",   N, RegionType.Void, $"{p}/beast.png"),
            E("Void Hound",   N, RegionType.Void, $"{p}/hound.png"),
            E("Void Panther", N, RegionType.Void, $"{p}/panther.png"),
            E("Void Serpent", N, RegionType.Void, $"{p}/serpent.png"),
            E("Void Slime",   N, RegionType.Void, $"{p}/slime.png"),
            E("Void Warrior", N, RegionType.Void, $"{p}/warrior.png"),
            E("Void Whisp",   N, RegionType.Void, $"{p}/whisp.png"),
            // Bosses
            B("Matriarch",      RegionType.Void, $"{p}/boss_1_matriarch.png", 1710),
            B("Void Slime",     RegionType.Void, $"{p}/boss_2_slime.png", 1720),
            B("Void Snail",     RegionType.Void, $"{p}/boss_3_snail.png", 1730),
            B("Void Bird",      RegionType.Void, $"{p}/boss_4_bird.png", 1740, PlacementType.Aerial),
            B("Void Octopus",   RegionType.Void, $"{p}/boss_5_octopus.png", 1750),
            B("Monstrosity",    RegionType.Void, $"{p}/boss_6_monstrosity.png", 1760),
            B("Orbital Queen",  RegionType.Void, $"{p}/boss_7_orbitalqueen.png", 1770, PlacementType.Aerial),
            B("Death",          RegionType.Void, $"{p}/boss_8_death.png", 1780),
            B("Abomination",    RegionType.Void, $"{p}/boss_9_abomination.png", 1790),
            B("Black Hole",     RegionType.Void, $"{p}/boss_10_blackhole.png", 1800),
        };
    }

    // ───────────────────────────── Timerift (1801-1900) ─────────────────────────────
    private static List<StageEnemy> SeedTimeriftEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/timerift";
        return new List<StageEnemy>
        {
            E("Anomaly",        N, RegionType.Timerift, $"{p}/anomaly.png"),
            E("Chronos",        N, RegionType.Timerift, $"{p}/chronos.png"),
            E("Echo",           N, RegionType.Timerift, $"{p}/echo.png"),
            E("Flux",           N, RegionType.Timerift, $"{p}/flux.png"),
            E("Future Specter", N, RegionType.Timerift, $"{p}/futurespecter.png"),
            E("Paradox",        N, RegionType.Timerift, $"{p}/paradox.png"),
            E("Rift",           N, RegionType.Timerift, $"{p}/rift.png"),
            E("Shadow",         N, RegionType.Timerift, $"{p}/shadow.png"),
            E("Temporal",       N, RegionType.Timerift, $"{p}/temporal.png"),
            E("Timeloop",       N, RegionType.Timerift, $"{p}/timeloop.png"),
            // Bosses
            B("Dragon",          RegionType.Timerift, $"{p}/boss_1_dragon.png", 1810),
            B("Time Lord",       RegionType.Timerift, $"{p}/boss_2_timelord.png", 1820),
            B("Titan",           RegionType.Timerift, $"{p}/boss_3_titan.png", 1830),
            B("Beast",           RegionType.Timerift, $"{p}/boss_4_beast.png", 1840),
            B("Hydra",           RegionType.Timerift, $"{p}/boss_5_hydra.png", 1850),
            B("Guardian",        RegionType.Timerift, $"{p}/boss_6_guardian.png", 1860),
            B("Demon",           RegionType.Timerift, $"{p}/boss_7_demon.png", 1870),
            B("Wurm",            RegionType.Timerift, $"{p}/boss_8_wurm.png", 1880),
            B("Reality Breaker", RegionType.Timerift, $"{p}/boss_9_realitybreaker.png", 1890),
            B("Infinity",        RegionType.Timerift, $"{p}/boss_10_infinity.png", 1900),
        };
    }

    // ───────────────────────────── Light (1901-2000) ─────────────────────────────
    private static List<StageEnemy> SeedLightEnemies()
    {
        var p = "/sprites/games/my-tuno/enemies/light";
        return new List<StageEnemy>
        {
            E("Archer",    N, RegionType.Light, $"{p}/archer.png"),
            E("Construct", N, RegionType.Light, $"{p}/construct.png"),
            E("Guard",     N, RegionType.Light, $"{p}/guard.png"),
            E("Hound",     N, RegionType.Light, $"{p}/hound.png"),
            E("Monk",      N, RegionType.Light, $"{p}/monk.png"),
            E("Moth",      N, RegionType.Light, $"{p}/moth.png", placement: PlacementType.Aerial),
            E("Purifier",  N, RegionType.Light, $"{p}/purifier.png"),
            E("Sentinel",  N, RegionType.Light, $"{p}/sentinel.png"),
            E("Templar",   N, RegionType.Light, $"{p}/templar.png"),
            E("Wisp",      N, RegionType.Light, $"{p}/wisp.png"),
            // Bosses
            B("Angel",     RegionType.Light, $"{p}/boss_1_angel.png", 1910),
            B("Archangel", RegionType.Light, $"{p}/boss_2_archangel.png", 1920),
            B("Paladin",   RegionType.Light, $"{p}/boss_3_paladin.png", 1930),
            B("Dragon",    RegionType.Light, $"{p}/boss_4_dragon.png", 1940),
            B("Mantis",    RegionType.Light, $"{p}/boss_5_mantis.png", 1950),
            B("Lion",      RegionType.Light, $"{p}/boss_6_lion.png", 1960),
            B("Phoenix",   RegionType.Light, $"{p}/boss_7_phoenix.png", 1970, PlacementType.Aerial),
            B("Golem",     RegionType.Light, $"{p}/boss_8_golem.png", 1980),
            B("Pegasus",   RegionType.Light, $"{p}/boss_9_pegasus.png", 1990, PlacementType.Aerial),
            B("Knight",    RegionType.Light, $"{p}/boss_10_knight.png", 2000),
        };
    }

    // ───────────────────────────── Shorthand helpers ─────────────────────────────

    private const EnemyType N = EnemyType.Normal;

    private static StageEnemy E(
        string name, EnemyType type, RegionType region,
        string sprite, PlacementType placement = PlacementType.Terrestrial)
        => StageEnemy.Create(name, type, region, sprite, placement: placement);

    private static StageEnemy B(
        string name, RegionType region,
        string sprite, int bossStage, PlacementType placement = PlacementType.Terrestrial)
        => StageEnemy.Create(name, EnemyType.Boss, region, sprite, bossStage, placement);
}
