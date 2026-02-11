using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

/// <summary>
/// Service for Boss Mode — an endless boss-only mode.
/// Every stage is a boss fight. Bosses start at stage mode 500+ difficulty.
/// Requires 1 FITAB to enter.
/// </summary>
public class BossModeService : IBossModeService
{
    private readonly IBossModeProgressRepository _bossModeProgressRepository;
    private readonly ICharacterRepository _characterRepository;
    private readonly ICombatEngine _combatEngine;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<BossModeService> _logger;
    private readonly MyTunoScalingConfiguration _config;
    private readonly IStageBiomeService _biomeService;
    private readonly IStageProgressRepository _stageProgressRepository;
    private readonly IWebHostEnvironment _environment;
    private readonly ApplicationDbContext _context;
    private readonly Random _random = new();

    public BossModeService(
        IBossModeProgressRepository bossModeProgressRepository,
        ICharacterRepository characterRepository,
        ICombatEngine combatEngine,
        IInventoryRepository inventoryRepository,
        UserManager<ApplicationUser> userManager,
        ILogger<BossModeService> logger,
        IOptions<MyTunoScalingConfiguration> config,
        IStageBiomeService biomeService,
        IStageProgressRepository stageProgressRepository,
        IWebHostEnvironment environment,
        ApplicationDbContext context)
    {
        _bossModeProgressRepository = bossModeProgressRepository;
        _characterRepository = characterRepository;
        _combatEngine = combatEngine;
        _inventoryRepository = inventoryRepository;
        _userManager = userManager;
        _logger = logger;
        _config = config.Value;
        _biomeService = biomeService;
        _stageProgressRepository = stageProgressRepository;
        _environment = environment;
        _context = context;
    }

    /// <summary>
    /// Resets any Modified ApplicationUser entries in the change tracker to Unchanged.
    /// This prevents stale ConcurrencyStamp values from poisoning subsequent SaveChangesAsync calls.
    /// In Blazor Server, the DbContext is long-lived (scoped per circuit), so a failed
    /// UserManager.UpdateAsync can leave the user entity Modified with a stale ConcurrencyStamp,
    /// causing every subsequent SaveChangesAsync to fail with DbUpdateConcurrencyException.
    /// </summary>
    private void ResetStaleUserEntries()
    {
        foreach (var entry in _context.ChangeTracker.Entries<ApplicationUser>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.State = EntityState.Unchanged;
            }
        }
    }

    /// <inheritdoc />
    public async Task<BossModeProgress> GetOrCreateBossModeProgressAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        var progress = await _bossModeProgressRepository.GetByUserIdAsync(userId);
        if (progress != null)
        {
            // Check and apply daily reset if a new day has started
            if (progress.CheckAndApplyDailyReset())
            {
                ResetStaleUserEntries();
                await _bossModeProgressRepository.UpdateAsync(progress);
            }
            return progress;
        }

        progress = BossModeProgress.Create(userId);
        await _bossModeProgressRepository.AddAsync(progress);
        return progress;
    }

    /// <inheritdoc />
    public async Task<BossModeProgress?> GetBossModeProgressAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required", nameof(userId));

        return await _bossModeProgressRepository.GetByUserIdAsync(userId);
    }

    /// <inheritdoc />
    public async Task<bool> CanEnterBossModeAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        return user != null && user.FitabBalance >= 1;
    }

    /// <inheritdoc />
    public async Task<BossModeProgress> StartBossModeRunAsync(string userId)
    {
        // Guard: if the user already has a run in progress, return it without
        // deducting FITAB.  This prevents double-charge caused by Blazor Server
        // prerender (OnInitializedAsync fires twice — once during prerender and
        // once when the SignalR circuit connects, with a NEW component instance).
        var existingProgress = await GetOrCreateBossModeProgressAsync(userId);
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            throw new Core.Exceptions.EntityNotFoundException(nameof(ApplicationUser), userId);

        if (existingProgress.CurrentBossStage > 0)
        {
            _logger.LogInformation(
                "User {UserName} already has a Boss Mode run in progress at stage {Stage}. Skipping FITAB deduction.",
                user.UserName, existingProgress.CurrentBossStage);
            return existingProgress;
        }

        // Check level requirement
        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null || character.Level < 100)
            throw new InvalidOperationException("Level 100 required to enter Boss Mode.");

        if (user.FitabBalance < 1)
            throw new InvalidOperationException("Not enough FITAB to enter Boss Mode. You need at least 1 FITAB.");

        // Deduct 1 FITAB — reload user first to get fresh ConcurrencyStamp
        // (in Blazor Server the tracked entity may have a stale stamp from another circuit/tab)
        await _context.Entry(user).ReloadAsync();
        if (user.FitabBalance < 1)
            throw new InvalidOperationException("Not enough FITAB to enter Boss Mode. You need at least 1 FITAB.");
        user.FitabBalance -= 1;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            _logger.LogWarning("Failed to update user FITAB balance: {Errors}",
                string.Join(", ", updateResult.Errors.Select(e => e.Description)));
            // Reset the stale user entry so it doesn't poison subsequent saves
            ResetStaleUserEntries();
            throw new InvalidOperationException("Failed to deduct FITAB. Please try again.");
        }

        // Start new run — reset stale user entries to prevent ConcurrencyStamp conflicts
        ResetStaleUserEntries();
        existingProgress.StartRun();
        await _bossModeProgressRepository.UpdateAsync(existingProgress);

        _logger.LogInformation(
            "User {UserName} started Boss Mode run #{Run}. FITAB balance: {Balance}",
            user.UserName, existingProgress.TotalRunsAttempted, user.FitabBalance);

        return existingProgress;
    }

    /// <inheritdoc />
    public async Task<BossModeBattleResult> ExecuteBossBattleAsync(int characterId)
    {
        var character = await _characterRepository.GetByIdAsync(characterId);
        if (character == null)
            throw new Core.Exceptions.EntityNotFoundException(nameof(Character), characterId);

        var progress = await GetOrCreateBossModeProgressAsync(character.UserId);

        if (progress.CurrentBossStage <= 0)
            throw new InvalidOperationException("No Boss Mode run in progress. Start a run first.");

        var bossStage = progress.CurrentBossStage;
        var equivalentStage = GetEquivalentStage(bossStage);

        // Check if shot buff is active
        var hasShotBuff = character.ShotBuffBattlesRemaining > 0;
        var hasPenaltyBuff = character.PenaltyBuffActive > 0;
        var combatCharacter = hasShotBuff
            ? Character.CreateShotBuffedCopy(character)
            : character;
        if (hasPenaltyBuff)
            combatCharacter = Character.CreatePenaltyBuffedCopy(combatCharacter);

        // Get boss sprite
        var bossSprite = await GetBossSpriteAsync(bossStage);
        var bossPlacement = 0; // Terrestrial by default

        // Create boss enemy using equivalent stage difficulty
        var bossEnemy = CreateBossEnemy(bossStage, equivalentStage);
        var bossName = $"Boss #{bossStage}";
        bossEnemy.User = new ApplicationUser { UserName = bossName };

        // If the boss has remaining HP from a previous run today, apply it
        var bossFullHP = bossEnemy.TotalHP;
        if (progress.DailyBossRemainingHP.HasValue && progress.DailyBossRemainingHP.Value > 0
            && progress.DailyBossRemainingHP.Value < bossFullHP)
        {
            bossEnemy.CurrentHP = progress.DailyBossRemainingHP.Value;
        }

        var seed = Random.Shared.Next(int.MinValue, int.MaxValue);

        // Simulate combat
        var combatResult = _combatEngine.Simulate(combatCharacter, bossEnemy, seed);

        var battleData = new
        {
            Events = combatResult.Events,
            EnemyCount = 1,
            EnemySprites = new List<string> { bossSprite },
            EnemyPlacements = new List<int> { bossPlacement },
            BiomeName = "Jeans",
            EnemyStats = new[]
            {
                new
                {
                    Name = bossName,
                    HP = bossEnemy.CurrentHP ?? bossEnemy.TotalHP,
                    MaxHP = bossEnemy.TotalHP,
                    Power = bossEnemy.TotalPower,
                    Defense = bossEnemy.TotalDefense,
                    Speed = bossEnemy.TotalSpeed,
                    ActionTime = Math.Round(bossEnemy.ActionTime, 1)
                }
            }
        };

        var replayJson = JsonSerializer.Serialize(battleData, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        var stageProgress = await _stageProgressRepository.GetByUserIdAsync(character.UserId);
        var highestStage = stageProgress?.HighestStage ?? 1;

        var (xpReward, fidelisReward, finosDropped, canecasDropped, cigarrosDropped, canhaosDropped, shotsDropped, penaltiesDropped, instrumentPartsDropped, equipmentDropped) =
            CalculateBossRewards(combatResult, bossStage, character.Level, highestStage);

        // Update progress (pass hasShotBuff so we can decrement the buff per battle, matching normal gameplay)
        await UpdateProgressAfterBattle(character, progress, combatResult, bossFullHP, hasShotBuff, hasPenaltyBuff);

        return new BossModeBattleResult
        {
            BattleId = Guid.NewGuid(),
            CharacterId = characterId,
            BossStage = bossStage,
            BossName = bossName,
            Seed = seed,
            Outcome = combatResult.Outcome,
            XPReward = xpReward,
            FidelisReward = fidelisReward,
            FinosDropped = finosDropped,
            CanecasDropped = canecasDropped,
            CigarrosDropped = cigarrosDropped,
            CanhaosDropped = canhaosDropped,
            ShotsDropped = shotsDropped,
            PenaltiesDropped = penaltiesDropped,
            InstrumentPartsDropped = instrumentPartsDropped,
            EquipmentDropped = equipmentDropped,
            ReplayJson = replayJson,
            PlayerFinalHP = combatResult.AttackerFinalHP
        };
    }

    /// <inheritdoc />
    public async Task ApplyBossRunRewardsAsync(
        int characterId, int xp, decimal fidelis, int finos, int canecas, int cigarros, int canhaos, int shots, int penalties = 0,
        int? restoreHp = null,
        Dictionary<InventoryItemType, int>? instrumentParts = null,
        Dictionary<InventoryItemType, int>? equipment = null)
    {
        var character = await _characterRepository.GetByIdAsync(characterId);
        if (character == null)
        {
            _logger.LogWarning("ApplyBossRunRewardsAsync: Character {CharacterId} not found", characterId);
            return;
        }

        character.CurrentHP = restoreHp;

        // Note: Shot buff is NOT expired here — it was already consumed
        // per-battle during the run via the combat engine.
        // Forcefully expiring it here would cause CurrentHP to be scaled
        // to TotalHP(unbuffed), making the beer heal think HP is already full.

        await _characterRepository.UpdateAsync(character);

        if (xp <= 0 && fidelis <= 0 && finos <= 0 && canecas <= 0 && cigarros <= 0 && canhaos <= 0 && shots <= 0 && penalties <= 0 &&
            (instrumentParts == null || instrumentParts.Count == 0) &&
            (equipment == null || equipment.Count == 0))
            return;

        if (xp > 0)
        {
            character.AddXP(xp);
            await _characterRepository.UpdateAsync(character);
        }

        var user = await _userManager.FindByIdAsync(character.UserId);
        if (fidelis > 0 && user != null)
        {
            // Reload user to get fresh ConcurrencyStamp before updating
            await _context.Entry(user).ReloadAsync();
            user.FidelisBalance += fidelis;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed to update user Fidelis balance in boss rewards: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
                ResetStaleUserEntries();
            }
        }
        // Ensure no stale user entries poison subsequent saves
        ResetStaleUserEntries();

        // Batch all inventory drops into a single DB round-trip
        var allDrops = new Dictionary<InventoryItemType, int>();
        if (finos > 0) allDrops[InventoryItemType.Fino] = finos;
        if (canecas > 0) allDrops[InventoryItemType.Caneca] = canecas;
        if (cigarros > 0) allDrops[InventoryItemType.Cigarro] = cigarros;
        if (canhaos > 0) allDrops[InventoryItemType.Canhao] = canhaos;
        if (shots > 0) allDrops[InventoryItemType.Shot] = shots;
        if (penalties > 0) allDrops[InventoryItemType.Penalty] = penalties;

        if (instrumentParts != null)
        {
            foreach (var (partType, quantity) in instrumentParts)
                allDrops[partType] = allDrops.GetValueOrDefault(partType) + quantity;
        }

        if (equipment != null)
        {
            foreach (var (equipType, quantity) in equipment)
                allDrops[equipType] = allDrops.GetValueOrDefault(equipType) + quantity;
        }

        if (allDrops.Count > 0)
            await _inventoryRepository.AddItemsAsync(character.UserId, allDrops);

        _logger.LogInformation(
            "Applied boss run rewards for {UserName}: +{XP} XP, +{Fidelis} Fidelis, +{Finos} finos, +{Canecas} canecas, +{Cigarros} cigarros, +{Canhaos} canhaos, +{Shots} shots",
            user?.UserName ?? "unknown", xp, fidelis, finos, canecas, cigarros, canhaos, shots);
    }

    /// <inheritdoc />
    public async Task<bool> CancelBossRunAsync(int characterId, int restoreHp, int restoreShotBuffBattles = 0, int restoreCigarroShield = 0, int restoreCanhaoBoost = 0, int restorePenaltyBuff = 0)
    {
        const int maxRetries = 3;
        // Pre-fetch character for logging (available in catch blocks)
        var character = await _characterRepository.GetByIdAsync(characterId);
        var cancelUser = character != null ? await _userManager.FindByIdAsync(character.UserId) : null;
        var userName = cancelUser?.UserName ?? $"CharId:{characterId}";

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Reset stale user entries to prevent ConcurrencyStamp conflicts
                ResetStaleUserEntries();

                // Re-fetch character to get fresh state after potential retry
                character = await _characterRepository.GetByIdAsync(characterId);
                if (character == null) return false;

                var progress = await _bossModeProgressRepository.GetByUserIdAsync(character.UserId);
                if (progress == null) return false;

                // Restore character state
                character.CurrentHP = restoreHp;
                character.ShotBuffBattlesRemaining = restoreShotBuffBattles;
                character.CigarroShieldHitsRemaining = restoreCigarroShield;
                character.CanhaoDamageBoostHitsRemaining = restoreCanhaoBoost;
                character.PenaltyBuffActive = restorePenaltyBuff;
                await _characterRepository.UpdateAsync(character);

                // End the run
                progress.EndRun();
                await _bossModeProgressRepository.UpdateAsync(progress);

                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (attempt < maxRetries)
                {
                    _logger.LogWarning(ex, "CancelBossRunAsync: Concurrency conflict, retrying ({Attempt}/{Max})...", attempt + 1, maxRetries);
                    await Task.Delay(100 * (attempt + 1));
                    continue;
                }
                _logger.LogError(ex, "CancelBossRunAsync: Failed after {Max} retries for {UserName}", maxRetries, userName);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling boss run for {UserName}", userName);
                return false;
            }
        }
        return false;
    }

    /// <inheritdoc />
    public async Task<string> GetBossSpriteAsync(int bossStage)
    {
        var spritePath = _config.BossMode.EnemySpritePath;
        var fullPath = Path.Combine(_environment.WebRootPath, spritePath);

        try
        {
            if (Directory.Exists(fullPath))
            {
                var files = Directory.GetFiles(fullPath, "*.png")
                    .Concat(Directory.GetFiles(fullPath, "*.webp"))
                    .Concat(Directory.GetFiles(fullPath, "*.jpg"))
                    .ToList();

                if (files.Count > 0)
                {
                    var selectedFile = files[_random.Next(files.Count)];
                    var relativePath = Path.GetRelativePath(_environment.WebRootPath, selectedFile)
                        .Replace('\\', '/');
                    return $"/{relativePath}";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load boss sprites from {Path}", fullPath);
        }

        // Fallback
        return "/sprites/games/my-tuno/enemies/forest/boss_1_bear.png";
    }

    /// <inheritdoc />
    public string GetBackgroundPath()
    {
        return _config.BossMode.BackgroundPath;
    }

    /// <inheritdoc />
    public int GetEquivalentStage(int bossStage)
    {
        return _config.BossMode.StageOffset + bossStage - 1;
    }

    /// <summary>
    /// Creates a temporary boss character for combat, using equivalent stage difficulty.
    /// Mirrors stage mode enemy scaling but with boss-specific config.
    /// </summary>
    private Character CreateBossEnemy(int bossStage, int equivalentStage)
    {
        var bossConfig = _config.BossMode;
        var baseStats = bossConfig.BaseBossStats;

        // Use the stage mode's unified difficulty curve with the equivalent stage
        var curve = _biomeService.GetUnifiedDifficultyCurve(equivalentStage);
        var diffMult = _biomeService.GetDifficultyMultiplier(equivalentStage);
        var bossMult = bossConfig.BossStatMultiplier;

        var hp = Math.Max(1, (int)(baseStats.Hp * curve * diffMult * bossMult));
        var power = Math.Max(1, (int)(baseStats.Power * curve * diffMult * bossMult));
        var speed = Math.Max(1, (int)(baseStats.Speed * curve * diffMult * bossMult));
        var defense = Math.Max(1, (int)(baseStats.Defense * curve * diffMult * bossMult));

        var critGrowth = (equivalentStage - 1) * 0.003;
        var critChance = Math.Min(baseStats.CriticalChance + critGrowth, _config.Combat.CriticalChanceCap);

        // Boss action time gets faster as boss stages progress (much more aggressive)
        var tier = (bossStage - 1) / 5;
        var actionTime = Math.Max(0.8, 4.0 - (tier * 0.4));

        var bossName = $"Boss #{bossStage}";
        return Character.CreateStageEnemy(hp, power, speed, defense, critChance, bossName, actionTime);
    }

    /// <summary>
    /// Calculates rewards for defeating a boss in Boss Mode.
    /// </summary>
    private (int xp, decimal fidelis, int finos, int canecas, int cigarros, int canhaos, int shots, int penalties, List<InventoryItemType> instrumentParts, List<InventoryItemType> equipment) CalculateBossRewards(
        CombatResult combatResult, int bossStage, int characterLevel, int highestStage = 1)
    {
        if (combatResult.Outcome != BattleOutcome.AttackerWon)
            return (0, 0m, 0, 0, 0, 0, 0, 0, new List<InventoryItemType>(), new List<InventoryItemType>());

        var random = Random.Shared;
        var bossConfig = _config.BossMode;
        var dropRates = bossConfig.DropRates;

        // XP calculation
        var equivalentStage = GetEquivalentStage(bossStage);
        var bossLevelFactor = Math.Pow(equivalentStage, bossConfig.BossLevelXPPower);
        var levelDiff = Math.Max(0, characterLevel - equivalentStage);
        var levelDiffMult = Math.Max(bossConfig.MinXPLevelMultiplier, 1.0 - levelDiff * bossConfig.XpLevelPenaltyRate);
        var xpReward = (int)Math.Round(bossConfig.XpPerBossLevel * bossLevelFactor * levelDiffMult);

        // Fidelis calculation
        var baseFidelis = bossConfig.FidelisRewards.BossWin;
        var rewardCurve = _biomeService.GetUnifiedRewardCurve(equivalentStage);
        var biomeRewardMult = _biomeService.GetRewardMultiplierForStage(equivalentStage);
        var rewardConfig = bossConfig.RewardCurve;
        var rawLevelBonus = 1.0 + (characterLevel - 1) * rewardConfig.LevelBonusPerLevel;
        var levelBonus = Math.Min(rawLevelBonus, rewardConfig.LevelBonusCap);
        var fidelisReward = Math.Round(baseFidelis * (decimal)(rewardCurve * biomeRewardMult * levelBonus), 2);

        // Drop rolls
        var finosDropped = 0;
        var canecasDropped = 0;
        var cigarrosDropped = 0;
        var canhaosDropped = 0;
        var shotsDropped = 0;
        var penaltiesDropped = 0;
        var instrumentPartsDropped = new List<InventoryItemType>();
        var equipmentDropped = new List<InventoryItemType>();

        // Gate consumable drops behind biome progression
        // Fino=1(Forest), Shot=101(Swamp), Cigarro=301(Snowy), Caneca=501(Caverns), Canhão=701(Volcanic)
        if (highestStage >= 1 && random.NextDouble() < dropRates.FinoDropChance) finosDropped++;
        if (highestStage >= 501 && random.NextDouble() < dropRates.CanecaDropChance) canecasDropped++;
        if (highestStage >= 301 && random.NextDouble() < dropRates.CigarroDropChance) cigarrosDropped++;
        if (highestStage >= 701 && random.NextDouble() < dropRates.CanhaoDropChance) canhaosDropped++;
        if (highestStage >= 101 && random.NextDouble() < dropRates.ShotDropChance) shotsDropped++;
        if (highestStage >= 901 && random.NextDouble() < dropRates.PenaltyDropChance) penaltiesDropped++;

        var instrumentTypes = Enum.GetValues(typeof(InstrumentType))
            .Cast<InstrumentType>()
            .Where(t => t != InstrumentType.Saxofone && t != InstrumentType.Fagote)
            .ToArray();
        var equipmentSlots = Enum.GetValues(typeof(EquipmentSlot));

        if (random.NextDouble() < dropRates.InstrumentPartDropChance)
        {
            var randomInstrument = instrumentTypes[random.Next(instrumentTypes.Length)];
            instrumentPartsDropped.Add(InstrumentTypeHelper.ToInventoryPartType(randomInstrument));
        }

        if (random.NextDouble() < dropRates.EquipmentDropChance)
        {
            var randomSlot = (EquipmentSlot)equipmentSlots.GetValue(random.Next(equipmentSlots.Length))!;
            equipmentDropped.Add(EquipmentDropHelper.ToInventoryItemType(randomSlot));
        }

        return (xpReward, fidelisReward, finosDropped, canecasDropped, cigarrosDropped, canhaosDropped, shotsDropped, penaltiesDropped, instrumentPartsDropped, equipmentDropped);
    }

    /// <summary>
    /// Updates character HP and boss mode progress after a battle.
    /// Both entities are saved in a single SaveChangesAsync call to avoid
    /// change tracker conflicts from multiple sequential saves.
    /// On defeat, saves the boss's remaining HP so the next run continues where this one left off.
    /// </summary>
    private async Task UpdateProgressAfterBattle(
        Character character, BossModeProgress progress, CombatResult combatResult, int bossMaxHP, bool shotBuffUsed = false, bool penaltyBuffUsed = false)
    {
        const int maxRetries = 3;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                // Reset any stale ApplicationUser entries in the change tracker.
                // In Blazor Server, a failed UserManager.UpdateAsync can leave the user entity
                // Modified with a stale ConcurrencyStamp, which poisons ALL subsequent SaveChangesAsync calls.
                ResetStaleUserEntries();

                // Update character HP (in buffed scale if buff was active)
                if (combatResult.AttackerFinalHP > 0)
                    character.CurrentHP = combatResult.AttackerFinalHP;
                else
                    character.CurrentHP = null;

                // Write back consumable buff remaining counts from combat
                character.CigarroShieldHitsRemaining = combatResult.AttackerCigarroShieldRemaining;
                character.CanhaoDamageBoostHitsRemaining = combatResult.AttackerCanhaoBoostRemaining;

                // Decrement shot buff per battle, matching normal gameplay (BattleService).
                // ExpireShotBuff() decrements ShotBuffBattlesRemaining and, when it reaches 0,
                // scales CurrentHP proportionally from buffed max to unbuffed max.
                if (shotBuffUsed && character.ShotBuffBattlesRemaining > 0)
                {
                    character.ExpireShotBuff();
                }

                // Expire penalty buff per battle (consumed after 1 boss battle)
                if (penaltyBuffUsed && character.PenaltyBuffActive > 0)
                {
                    character.ExpirePenaltyBuff();
                }

                // Update boss progress (only on first attempt — retries already applied these)
                if (attempt == 0)
                {
                    if (combatResult.Outcome == BattleOutcome.AttackerWon)
                    {
                        progress.AdvanceBossStage();
                    }
                    else
                    {
                        // Save the boss's remaining HP so the next run picks up where this left off
                        if (combatResult.DefenderFinalHP > 0)
                        {
                            progress.SaveBossHP(combatResult.DefenderFinalHP, bossMaxHP);
                        }
                        progress.EndRun();
                    }
                }

                // Save both entities in one SaveChangesAsync call.
                // UpdateAsync marks progress as Modified and calls SaveChangesAsync.
                // EF Core change detection will also pick up the character.CurrentHP change
                // and save both in a single database round-trip.
                await _bossModeProgressRepository.UpdateAsync(progress);
                return;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (attempt < maxRetries)
                {
                    _logger.LogWarning(ex, "UpdateProgressAfterBattle: Concurrency conflict, retrying ({Attempt}/{Max})...", attempt + 1, maxRetries);
                    await Task.Delay(50 * (attempt + 1));

                    // Reset stale user entries and reload game entities
                    ResetStaleUserEntries();
                    try
                    {
                        await _characterRepository.ReloadAsync(character);
                        await _bossModeProgressRepository.ReloadAsync(progress);
                    }
                    catch (Exception reloadEx)
                    {
                        _logger.LogWarning(reloadEx, "Failed to reload entities for retry");
                    }

                    continue;
                }

                _logger.LogError(ex, "UpdateProgressAfterBattle: Failed after {Max} retries", maxRetries);
            }
        }
    }
}
