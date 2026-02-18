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
    /// Gets the number of enemies for a given stage.
    /// Boss and miniboss stages always have 1 enemy.
    /// Normal stages: stage offset 1-9 within each 10-stage block = 1-9 enemies.
    /// </summary>
    public int GetEnemyCountForStage(int stageNumber)
    {
        // Boss stages (every 100) and miniboss stages (every 10) have 1 enemy
        if (IsBossStage(stageNumber) || IsMiniBossStage(stageNumber))
        {
            return 1;
        }

        var encounterRules = _config.StageMode.EncounterRules;
        if (encounterRules?.EnemyCountByStageOffset == null || encounterRules.EnemyCountByStageOffset.Count == 0)
        {
            // Fallback: offset within 10-stage block directly = enemy count (1-9)
            var fallbackOffset = ((stageNumber - 1) % 10) + 1;
            return Math.Min(fallbackOffset, 9);
        }

        // Calculate stage offset within the current miniboss cycle (10-stage block)
        var minibossInterval = encounterRules.MinibossEveryNStages;
        var offset = ((stageNumber - 1) % minibossInterval) + 1;

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
            "No matching enemy count rule for stage {StageNumber} (offset {Offset}), defaulting to offset value",
            stageNumber, offset);
        return Math.Min(offset, 9);
    }

    /// <summary>
    /// Determines if a stage is a boss stage (every 100 stages)
    /// </summary>
    public bool IsBossStage(int stageNumber)
    {
        var bossInterval = _config.StageMode.EncounterRules?.BossEveryNStages ?? 100;
        return stageNumber % bossInterval == 0;
    }

    /// <summary>
    /// Determines if a stage is a miniboss stage (every 10 stages, but NOT boss stages)
    /// </summary>
    public bool IsMiniBossStage(int stageNumber)
    {
        if (IsBossStage(stageNumber)) return false;
        var minibossInterval = _config.StageMode.EncounterRules?.MinibossEveryNStages ?? 10;
        return stageNumber % minibossInterval == 0;
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
            var index = Random.Shared.Next(availableSprites.Count);
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
            "swamp" => RegionType.Swamp,
            "mountains" => RegionType.Mountains,
            "snowy" => RegionType.Snowy,
            "tropical" => RegionType.Tropical,
            "caverns" => RegionType.Caverns,
            "desert" => RegionType.Desert,
            "volcanic" => RegionType.Volcanic,
            "ruins" => RegionType.Ruins,
            "sky" => RegionType.Sky,
            "underwater" => RegionType.Underwater,
            "underground" => RegionType.Underground,
            "mechanical" => RegionType.Mechanical,
            "frostfire" => RegionType.Frostfire,
            "corruption" => RegionType.Corruption,
            "dark" => RegionType.Dark,
            "alien" => RegionType.Alien,
            "void" => RegionType.Void,
            "timerift" => RegionType.Timerift,
            "light" => RegionType.Light,
            "arena" => RegionType.Arena,
            _ => RegionType.Forest
        };
    }

    /// <summary>
    /// Gets the background image path for a given stage number
    /// </summary>
    public string GetBackgroundForStage(int stageNumber)
    {
        var biomeName = GetBiomeForStage(stageNumber);
        // Map biome names to background file names (some differ)
        var fileName = biomeName.ToLowerInvariant() switch
        {
            "sky" => "clouds",
            "corruption" => "corrupted",
            _ => biomeName.ToLowerInvariant()
        };
        return $"/sprites/games/my-tuno/backgrounds/{fileName}.png";
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

        // Fallback: pick a random existing boss from any region
        var fallbackBoss = await _stageEnemyRepository.GetRandomEnemyAsync(EnemyType.Boss, GetRegionForBiome(GetBiomeForStage(stageNumber)));
        if (fallbackBoss != null && !string.IsNullOrEmpty(fallbackBoss.SpritePath))
        {
            _logger.LogWarning("No boss configured for stage {StageNumber}, using fallback boss: {BossName}", stageNumber, fallbackBoss.Name);
            return fallbackBoss.SpritePath;
        }

        // Last resort: file system based approach
        var biomeName = GetBiomeForStage(stageNumber);
        var biomeConfig = _config.StageMode.Biomes?.FirstOrDefault(b => b.Name == biomeName);
        
        if (biomeConfig == null)
        {
            _logger.LogWarning("No biome configured for {BiomeName}, using default boss sprite", biomeName);
            return "/sprites/games/my-tuno/enemies/forest/boss_1_bear.png";
        }

        var bossSprites = await GetBossSpritesForBiomeAsync(biomeConfig);
        
        if (bossSprites.Count == 0)
        {
            _logger.LogWarning("No boss sprites found for biome {BiomeName}, using default boss sprite", biomeName);
            return "/sprites/games/my-tuno/enemies/forest/boss_1_bear.png";
        }

        // Calculate which boss this is within the current biome (10 bosses per 1000-floor biome)
        int bossIndex = ((stageNumber - 1) % 1000) / 100;
        bossIndex = Math.Max(0, Math.Min(bossIndex, bossSprites.Count - 1));
        
        return bossSprites[bossIndex];
    }

    /// <summary>
    /// Gets a boss sprite for an Arena stage directly from the filesystem.
    /// Deterministic pick based on stage number so retries show the same boss.
    /// </summary>
    public async Task<string> GetBossSpriteForArenaAsync(int stageNumber)
    {
        var biomeConfig = _config.StageMode.Biomes?.FirstOrDefault(b =>
            stageNumber >= b.StageMin && stageNumber <= b.StageMax);

        if (biomeConfig != null)
        {
            var bossSprites = await GetBossSpritesForBiomeAsync(biomeConfig);
            if (bossSprites.Count > 0)
            {
                // Deterministic selection so the same boss appears on retries
                var index = Math.Abs(stageNumber) % bossSprites.Count;
                return bossSprites[index];
            }
        }

        _logger.LogWarning("No arena boss sprites found for stage {StageNumber}, using fallback", stageNumber);
        return "/sprites/games/my-tuno/enemies/arena/boss_calhau.png";
    }

    /// <summary>
    /// Gets boss sprite path with placement info for a given boss stage
    /// </summary>
    public async Task<(string SpritePath, int Placement)> GetBossSpriteWithPlacementAsync(int stageNumber)
    {
        var boss = await _stageEnemyRepository.GetBossForStageAsync(stageNumber);
        if (boss != null && !string.IsNullOrEmpty(boss.SpritePath))
        {
            return (boss.SpritePath, (int)boss.Placement);
        }

        // Fallback: use GetBossSpriteAsync and default to terrestrial
        var sprite = await GetBossSpriteAsync(stageNumber);
        return (sprite, 0);
    }

    /// <summary>
    /// Gets the difficulty multiplier for regular enemies — always 1.0 (unified curve handles scaling).
    /// </summary>
    public double GetEnemiesDifficultyMultiplier(int stageNumber) => 1.0;

    /// <summary>
    /// Gets the difficulty multiplier for bosses — always 1.0 (unified curve handles scaling).
    /// </summary>
    public double GetBossesDifficultyMultiplier(int stageNumber) => 1.0;

    /// <summary>
    /// Gets the biome reward multiplier for a given stage (unified scaling).
    /// </summary>
    public double GetRewardMultiplierForStage(int stageNumber)
    {
        var biome = GetBiomeConfigForStage(stageNumber);
        return biome?.RewardMultiplier ?? 1.0;
    }

    /// <summary>
    /// Looks up the enemy tier configuration for a given stage number.
    /// Returns the tier whose [MinStage, MaxStage] range contains the stage,
    /// or the highest tier if the stage exceeds all defined ranges.
    /// </summary>
    public StageEnemyTierConfig GetEnemyTierForStage(int stageNumber)
    {
        var tiers = _config.StageMode.EnemyTiers;
        foreach (var tier in tiers)
        {
            if (stageNumber >= tier.MinStage && stageNumber <= tier.MaxStage)
                return tier;
        }

        // Fallback: return the highest tier
        return tiers[^1];
    }

    /// <summary>
    /// Calculates scaled enemy stats for a given stage using the tiered enemy system.
    /// </summary>
    public (long hp, long damage) CalculateScaledStats(int stageNumber, int baseHp, int baseDamage, bool isBoss)
    {
        var tier = GetEnemyTierForStage(stageNumber);
        var bossMult = isBoss ? _config.StageMode.BossMultiplier : 1.0;

        // Use tier stats as the scaling reference; template base stats are ratios
        var tierFactor = tier.HP / 150.0; // 150 = tier-1 baseline HP
        var scaledHp = Math.Max(1, (long)(baseHp * tierFactor * bossMult));
        var scaledDamage = Math.Max(1, (long)(baseDamage * tierFactor * bossMult));

        return (scaledHp, scaledDamage);
    }

    /// <summary>
    /// Gets the BiomeConfig for a given stage number. Returns null if no biome configured.
    /// </summary>
    private BiomeConfig? GetBiomeConfigForStage(int stageNumber)
    {
        var biomes = _config.StageMode.Biomes;
        if (biomes == null || biomes.Count == 0)
            return null;

        foreach (var biome in biomes)
        {
            if (stageNumber >= biome.StageMin && stageNumber <= biome.StageMax)
                return biome;
        }

        return biomes.OrderByDescending(b => b.StageMax).First();
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
    /// Loads sprite files from a folder with filtering.
    /// For the Void biome the search includes all sub-directories so that
    /// enemies / bosses organised in themed folders are aggregated.
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

        // Void biome searches all sub-directories; other biomes stay top-level
        var isVoid = normalizedRelativePath.Contains("/void", StringComparison.OrdinalIgnoreCase)
                  || normalizedRelativePath.Contains("\\void", StringComparison.OrdinalIgnoreCase)
                  || normalizedRelativePath.EndsWith("void", StringComparison.OrdinalIgnoreCase);
        var searchOption = isVoid ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        // Load all image files
        var extensions = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp", "*.svg" };
        var allFiles = new List<string>();
        
        await Task.Run(() =>
        {
            foreach (var extension in extensions)
            {
                allFiles.AddRange(Directory.GetFiles(fullPath, extension, searchOption));
            }
        });

        // Filter based on boss prefix
        var sprites = allFiles
            .Select(f =>
            {
                var relPath = Path.GetRelativePath(fullPath, f).Replace("\\", "/");
                return (FileName: Path.GetFileName(f), WebPath: $"{webPath}/{relPath}");
            })
            .Where(item =>
            {
                var isBoss = item.FileName.StartsWith(bossPrefix, StringComparison.OrdinalIgnoreCase);
                return excludeBoss ? !isBoss : isBoss;
            })
            .OrderBy(item => ExtractBossNumber(item.FileName, bossPrefix))
            .Select(item => item.WebPath)
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
