using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Interfaces;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing stage biomes and enemy sprite selection
/// Implements config-driven infinite stage progression with biome system
/// </summary>
public class StageBiomeService : IStageBiomeService
{
    private readonly MyTunoScalingConfiguration _config;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<StageBiomeService> _logger;
    private readonly IStageEnemyRepository _stageEnemyRepository;
    private readonly Random _random = new();
    private readonly Dictionary<string, List<string>> _spriteCache = new();
    private readonly object _cacheLock = new();

    public StageBiomeService(
        IOptions<MyTunoScalingConfiguration> config,
        IWebHostEnvironment environment,
        ILogger<StageBiomeService> logger,
        IStageEnemyRepository stageEnemyRepository)
    {
        _config = config.Value;
        _environment = environment;
        _logger = logger;
        _stageEnemyRepository = stageEnemyRepository;
    }

    /// <summary>
    /// Gets the biome for a given stage number
    /// </summary>
    public string GetBiomeForStage(int stageNumber)
    {
        var biomes = _config.StageMode.Biomes;
        if (biomes == null || biomes.Count == 0)
        {
            _logger.LogWarning("No biomes configured, using default 'Forest'");
            return "Forest";
        }

        foreach (var biome in biomes)
        {
            if (stageNumber >= biome.StageMin && stageNumber <= biome.StageMax)
            {
                return biome.Name;
            }
        }

        // If stage is beyond configured biomes, use the last biome
        var lastBiome = biomes.OrderByDescending(b => b.StageMax).First();
        _logger.LogWarning(
            "Stage {StageNumber} exceeds configured biome range, using last biome: {BiomeName}",
            stageNumber, lastBiome.Name);
        return lastBiome.Name;
    }

    /// <summary>
    /// Gets the number of enemies for a given stage
    /// </summary>
    public int GetEnemyCountForStage(int stageNumber)
    {
        var encounterRules = _config.StageMode.EncounterRules;
        if (encounterRules?.EnemyCountByStageOffset == null || encounterRules.EnemyCountByStageOffset.Count == 0)
        {
            // Fallback to default: 1 enemy per stage
            _logger.LogWarning("No encounter rules configured, defaulting to 1 enemy");
            return 1;
        }

        // Boss stages have 1 enemy (the boss)
        if (IsBossStage(stageNumber))
        {
            return 1;
        }

        // Calculate stage offset within the current "decade"
        var bossInterval = encounterRules.BossEveryNStages;
        var offset = ((stageNumber - 1) % bossInterval) + 1;

        // Find matching rule
        foreach (var rule in encounterRules.EnemyCountByStageOffset)
        {
            if (offset >= rule.From && offset <= rule.To)
            {
                return rule.Count;
            }
        }

        // Default fallback
        _logger.LogWarning(
            "No matching enemy count rule for stage {StageNumber} (offset {Offset}), defaulting to 1",
            stageNumber, offset);
        return 1;
    }

    /// <summary>
    /// Determines if a stage is a boss stage
    /// </summary>
    public bool IsBossStage(int stageNumber)
    {
        var bossInterval = _config.StageMode.EncounterRules?.BossEveryNStages ?? 10;
        return stageNumber % bossInterval == 0;
    }

    /// <summary>
    /// Gets random enemy sprite paths for a given stage
    /// Excludes boss sprites and ensures no duplicates per encounter
    /// </summary>
    public async Task<List<string>> GetRandomEnemySpritesAsync(int stageNumber, int count)
    {
        var biomeName = GetBiomeForStage(stageNumber);
        var biomeConfig = _config.StageMode.Biomes?.FirstOrDefault(b => b.Name == biomeName);
        
        if (biomeConfig == null)
        {
            _logger.LogError("Biome configuration not found for {BiomeName}", biomeName);
            return new List<string>();
        }

        var allSprites = await GetNonBossSpritesForBiomeAsync(biomeConfig);
        
        if (allSprites.Count == 0)
        {
            _logger.LogError("No enemy sprites found for biome {BiomeName}", biomeName);
            return new List<string>();
        }

        // Randomly select unique sprites
        var selectedSprites = new List<string>();
        var availableSprites = new List<string>(allSprites);

        for (int i = 0; i < count && availableSprites.Count > 0; i++)
        {
            var index = _random.Next(availableSprites.Count);
            selectedSprites.Add(availableSprites[index]);
            
            // Remove selected sprite to prevent duplicates
            availableSprites.RemoveAt(index);

            // If we run out of unique sprites, allow reuse
            if (availableSprites.Count == 0 && selectedSprites.Count < count)
            {
                availableSprites = new List<string>(allSprites);
                _logger.LogWarning(
                    "Not enough unique sprites for biome {BiomeName}, reusing sprites",
                    biomeName);
            }
        }

        return selectedSprites;
    }

    /// <summary>
    /// Gets random enemy sprite paths with placement info for a given stage
    /// Uses database enemies with their placement types
    /// </summary>
    public async Task<List<(string SpritePath, int Placement)>> GetRandomEnemySpritesWithPlacementAsync(int stageNumber, int count)
    {
        var biomeName = GetBiomeForStage(stageNumber);
        var region = GetRegionForBiome(biomeName);
        
        // Get random enemies from database with their placement info
        var enemies = await _stageEnemyRepository.GetRandomEnemiesAsync(EnemyType.Normal, region, count);
        
        if (enemies.Count == 0)
        {
            var sprites = await GetRandomEnemySpritesAsync(stageNumber, count);
            return sprites.Select(s => (s, 0)).ToList(); // All terrestrial by default
        }

        var result = enemies.Select(e => (
            SpritePath: e.SpritePath ?? GetDefaultSpriteForBiome(biomeName),
            Placement: (int)e.Placement
        )).ToList();
        
        return result;
    }

    private string GetDefaultSpriteForBiome(string biomeName)
    {
        return $"/sprites/games/my-tuno/enemies/{biomeName.ToLowerInvariant()}/wolf.png";
    }

    private RegionType GetRegionForBiome(string biomeName)
    {
        return biomeName.ToLowerInvariant() switch
        {
            "forest" => RegionType.Forest,
            "desert" => RegionType.Desert,
            "mountains" => RegionType.Mountains,
            "swamp" => RegionType.Swamp,
            "tundra" => RegionType.Tundra,
            "volcano" => RegionType.Volcano,
            "ocean" => RegionType.Ocean,
            "sky" => RegionType.Sky,
            "underground" => RegionType.Underground,
            "cursedlands" => RegionType.CursedLands,
            _ => RegionType.Forest
        };
    }

    /// <summary>
    /// Gets boss sprite path for a given boss stage from the database
    /// Bosses are explicitly defined per stage (10, 20, 30, etc.)
    /// </summary>
    public async Task<string> GetBossSpriteAsync(int stageNumber)
    {
        // First, try to get the boss from the database (preferred method)
        var boss = await _stageEnemyRepository.GetBossForStageAsync(stageNumber);
        
        if (boss != null && !string.IsNullOrEmpty(boss.SpritePath))
        {
            return boss.SpritePath;
        }

        // Fallback: use file system based approach
        var biomeName = GetBiomeForStage(stageNumber);
        var biomeConfig = _config.StageMode.Biomes?.FirstOrDefault(b => b.Name == biomeName);
        
        if (biomeConfig == null)
        {
            _logger.LogError("Biome configuration not found for {BiomeName}", biomeName);
            return string.Empty;
        }

        var bossSprites = await GetBossSpritesForBiomeAsync(biomeConfig);
        
        if (bossSprites.Count == 0)
        {
            _logger.LogError("No boss sprites found for biome {BiomeName}", biomeName);
            return string.Empty;
        }

        // Calculate which boss this is (1st boss = stage 10, 2nd boss = stage 20, etc.)
        int bossIndex = (stageNumber / 10) - 1;
        bossIndex = Math.Max(0, Math.Min(bossIndex, bossSprites.Count - 1));
        
        return bossSprites[bossIndex];
    }

    /// <summary>
    /// Calculates scaled enemy stats for a given stage
    /// </summary>
    public (int hp, int damage) CalculateScaledStats(int stageNumber, int baseHp, int baseDamage, bool isBoss)
    {
        var scaling = _config.StageMode.Scaling;
        if (scaling == null)
        {
            _logger.LogWarning("No scaling configuration found, using base stats");
            return (baseHp, baseDamage);
        }

        // Calculate growth multiplier based on stage number
        // Formula: stat = baseStat * (1 + growthRate)^stage
        var hpMultiplier = Math.Pow(1 + scaling.HpGrowthPerStage, stageNumber - 1);
        var damageMultiplier = Math.Pow(1 + scaling.DamageGrowthPerStage, stageNumber - 1);

        var scaledHp = (int)(baseHp * hpMultiplier);
        var scaledDamage = (int)(baseDamage * damageMultiplier);

        // Apply boss multiplier if applicable
        if (isBoss)
        {
            scaledHp = (int)(scaledHp * scaling.BossMultiplier);
            scaledDamage = (int)(scaledDamage * scaling.BossMultiplier);
        }

        return (scaledHp, scaledDamage);
    }

    /// <summary>
    /// Gets all non-boss sprites for a biome (cached)
    /// </summary>
    private async Task<List<string>> GetNonBossSpritesForBiomeAsync(BiomeConfig biomeConfig)
    {
        var cacheKey = $"{biomeConfig.Name}_nonboss";
        
        lock (_cacheLock)
        {
            if (_spriteCache.ContainsKey(cacheKey))
            {
                return _spriteCache[cacheKey];
            }
        }

        var sprites = await LoadSpritesFromFolderAsync(biomeConfig.EnemySpritePath, biomeConfig.BossSpritePrefix, excludeBoss: true);
        
        lock (_cacheLock)
        {
            _spriteCache[cacheKey] = sprites;
        }

        return sprites;
    }

    /// <summary>
    /// Gets all boss sprites for a biome (cached)
    /// </summary>
    private async Task<List<string>> GetBossSpritesForBiomeAsync(BiomeConfig biomeConfig)
    {
        var cacheKey = $"{biomeConfig.Name}_boss";
        
        lock (_cacheLock)
        {
            if (_spriteCache.ContainsKey(cacheKey))
            {
                return _spriteCache[cacheKey];
            }
        }

        var sprites = await LoadSpritesFromFolderAsync(biomeConfig.EnemySpritePath, biomeConfig.BossSpritePrefix, excludeBoss: false);
        
        lock (_cacheLock)
        {
            _spriteCache[cacheKey] = sprites;
        }

        return sprites;
    }

    /// <summary>
    /// Loads sprite files from a folder with filtering
    /// </summary>
    private async Task<List<string>> LoadSpritesFromFolderAsync(string relativePath, string bossPrefix, bool excludeBoss)
    {
        var normalizedRelativePath = relativePath.TrimStart('/', '\\');
        var fullPath = Path.Combine(_environment.WebRootPath, normalizedRelativePath);
        var webPath = $"/{normalizedRelativePath}".Replace("\\", "/");
        
        if (!Directory.Exists(fullPath))
        {
            _logger.LogError("Sprite directory not found: {Path}", fullPath);
            return new List<string>();
        }

        // Load all image files
        var extensions = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp", "*.svg" };
        var allFiles = new List<string>();
        
        await Task.Run(() =>
        {
            foreach (var extension in extensions)
            {
                allFiles.AddRange(Directory.GetFiles(fullPath, extension, SearchOption.TopDirectoryOnly));
            }
        });

        // Filter based on boss prefix
        var sprites = allFiles
            .Select(f => Path.GetFileName(f))
            .Where(filename =>
            {
                var isBoss = filename.StartsWith(bossPrefix, StringComparison.OrdinalIgnoreCase);
                return excludeBoss ? !isBoss : isBoss;
            })
            .OrderBy(filename => ExtractBossNumber(filename, bossPrefix))
            .Select(filename => $"{webPath}/{filename}")
            .ToList();

        return sprites;
    }

    /// <summary>
    /// Extracts the numeric boss index from a boss sprite filename
    /// Handles formats like "boss_1_bear.png", "boss_10_basilisk.png"
    /// </summary>
    private static int ExtractBossNumber(string filename, string bossPrefix)
    {
        // Remove the boss prefix (e.g., "boss_")
        var withoutPrefix = filename.Substring(bossPrefix.Length);
        
        // Extract the number before the next underscore or non-digit
        var numberPart = string.Empty;
        foreach (var c in withoutPrefix)
        {
            if (char.IsDigit(c))
            {
                numberPart += c;
            }
            else
            {
                break;
            }
        }
        
        return int.TryParse(numberPart, out var number) ? number : int.MaxValue;
    }
}
