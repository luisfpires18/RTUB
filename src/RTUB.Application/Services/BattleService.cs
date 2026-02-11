using System.Text.Json;
using System.Threading;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Configuration;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing battles
/// Handles battle creation, combat simulation, and reward distribution
/// Battles are NOT persisted to database - only win/loss stats are tracked on Character
/// </summary>
public class BattleService : IBattleService
{
    private readonly ICharacterRepository _characterRepository;
    private readonly ICombatEngine _combatEngine;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<BattleService> _logger;
    private readonly MyTunoScalingConfiguration _myTunoScalingConfig;
    private readonly IAuditLogService _auditLogService;
    private readonly IStageProgressRepository _stageProgressRepository;
    private readonly ApplicationDbContext _dbContext;

    public BattleService(
        ICharacterRepository characterRepository,
        ICombatEngine combatEngine,
        IInventoryRepository inventoryRepository,
        UserManager<ApplicationUser> userManager,
        ILogger<BattleService> logger,
        IOptions<MyTunoScalingConfiguration> myTunoScalingConfig,
        IAuditLogService auditLogService,
        IStageProgressRepository stageProgressRepository,
        ApplicationDbContext dbContext)
    {
        _characterRepository = characterRepository;
        _combatEngine = combatEngine;
        _inventoryRepository = inventoryRepository;
        _userManager = userManager;
        _logger = logger;
        _myTunoScalingConfig = myTunoScalingConfig.Value;
        _auditLogService = auditLogService;
        _stageProgressRepository = stageProgressRepository;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Creates and executes a battle vs a specific opponent character
    /// Rewards are NOT applied until FinalizeAndApplyRewardsAsync is called
    /// </summary>
    public async Task<BattleResult> CreateBattleVsOpponentAsync(int playerCharacterId, int opponentCharacterId, CancellationToken cancellationToken = default)
    {
        // Load player character
        var playerCharacter = await _characterRepository.GetByIdAsync(playerCharacterId);
        if (playerCharacter == null)
            throw new EntityNotFoundException(nameof(Character), playerCharacterId);

        // Check if player character is alive
        if (!playerCharacter.IsAlive())
            throw new InvalidOperationException("Personagem derrotado. Precisa de reviver antes de lutar.");

        // Load opponent character
        var opponentCharacter = await _characterRepository.GetByIdAsync(opponentCharacterId);
        if (opponentCharacter == null)
            throw new EntityNotFoundException(nameof(Character), opponentCharacterId);

        if (playerCharacterId == opponentCharacterId)
            throw new InvalidOperationException("Não podes lutar contra ti mesmo");

        var playerUser = await _userManager.FindByIdAsync(playerCharacter.UserId);
        var opponentUser = await _userManager.FindByIdAsync(opponentCharacter.UserId);
        var playerName = playerUser?.UserName ?? playerCharacter.UserId;
        var opponentName = opponentUser?.UserName ?? opponentCharacter.UserId;
        _logger.LogInformation("Battle started by {PlayerName} against {OpponentName}", playerName, opponentName);

        // Create CPU snapshot of opponent with full HP
        // This ensures the opponent always starts at full health regardless of their persisted state
        var opponentSnapshot = Character.CreateCpuSnapshot(opponentCharacter);

        // Check if shot buff is active and create buffed copy for combat
        var hasShotBuff = playerCharacter.ShotBuffBattlesRemaining > 0;
        var hasPenaltyBuff = playerCharacter.PenaltyBuffActive > 0;
        var combatCharacter = hasShotBuff 
            ? Character.CreateShotBuffedCopy(playerCharacter) 
            : playerCharacter;
        if (hasPenaltyBuff)
            combatCharacter = Character.CreatePenaltyBuffedCopy(combatCharacter);

        // Generate seed for deterministic combat
        var seed = GenerateSeed();

        // Run combat simulation using the snapshot (not the persisted character)
        var combatResult = _combatEngine.Simulate(combatCharacter, opponentSnapshot, seed);

        // Calculate rewards based on outcome
        var (xpReward, fidelisReward) = CalculateRewards(combatResult.Outcome, playerCharacter, opponentCharacter);

        // Serialize replay events to JSON
        var replayJson = JsonSerializer.Serialize(combatResult.Events, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        // Calculate shot buff state after this battle
        var shotBuffExpired = hasShotBuff && playerCharacter.ShotBuffBattlesRemaining == 1;
        var shotBuffRemaining = hasShotBuff ? playerCharacter.ShotBuffBattlesRemaining - 1 : 0;
        var penaltyBuffExpired = hasPenaltyBuff;

        // Create and return battle result (not persisted)
        return new BattleResult
        {
            BattleId = Guid.NewGuid(),
            AttackerCharacterId = playerCharacterId,
            DefenderCharacterId = opponentCharacterId,
            Seed = seed,
            Outcome = combatResult.Outcome,
            AttackerXP = xpReward,
            AttackerFidelis = fidelisReward,
            ReplayJson = replayJson,
            AttackerFinalHP = combatResult.AttackerFinalHP,
            ShotBuffUsed = hasShotBuff,
            ShotBuffExpired = shotBuffExpired,
            ShotBuffBattlesRemaining = shotBuffRemaining,
            AttackerCigarroShieldRemaining = combatResult.AttackerCigarroShieldRemaining,
            AttackerCanhaoBoostRemaining = combatResult.AttackerCanhaoBoostRemaining,
            PenaltyBuffUsed = hasPenaltyBuff,
            PenaltyBuffExpired = penaltyBuffExpired
        };
    }

    /// <summary>
    /// Finalizes a battle and applies all pending rewards and state changes
    /// Should be called after the battle animation finishes
    /// Handles concurrency exceptions by reloading and retrying once
    /// </summary>
    public async Task<bool> FinalizeAndApplyRewardsAsync(BattleResult result, CancellationToken cancellationToken = default)
    {
        const int maxRetries = 1;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await ApplyBattleRewardsAsync(result, cancellationToken);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
            {
                if (attempt < maxRetries)
                {
                    _logger.LogWarning(
                        ex,
                        "Concurrency conflict applying battle rewards for character {CharacterId}, retrying...",
                        result.AttackerCharacterId);
                    // Retry once - reload character fresh from database
                    continue;
                }

                // Final retry failed - log error and return false
                _logger.LogError(
                    ex,
                    "Failed to apply battle rewards for character {CharacterId} after {MaxRetries} retries",
                    result.AttackerCharacterId,
                    maxRetries);
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Internal method that applies battle rewards
    /// Separated to allow retry logic in FinalizeAndApplyRewardsAsync
    /// </summary>
    private async Task<bool> ApplyBattleRewardsAsync(BattleResult result, CancellationToken cancellationToken = default)
    {
        // Load the attacker character
        var playerCharacter = await _characterRepository.GetByIdAsync(result.AttackerCharacterId);
        if (playerCharacter == null)
        {
            _logger.LogError("Player character {CharacterId} not found for battle finalization", result.AttackerCharacterId);
            return false;
        }

        // Idempotency check — if this battle was already finalized, return cached success
        if (playerCharacter.LastBattleId.HasValue && playerCharacter.LastBattleId.Value == result.BattleId)
        {
            _logger.LogWarning("Duplicate finalization for BattleId {BattleId}, skipping", result.BattleId);
            return true;
        }

        // Apply battle HP result — arena battles use persistent HP
        // AttackerFinalHP is in buffed scale if buff was active; ExpireShotBuff will scale it down
        playerCharacter.CurrentHP = result.AttackerFinalHP > 0 ? result.AttackerFinalHP : 0;

        // Write back consumable buff remaining counts from combat
        playerCharacter.CigarroShieldHitsRemaining = result.AttackerCigarroShieldRemaining;
        playerCharacter.CanhaoDamageBoostHitsRemaining = result.AttackerCanhaoBoostRemaining;

        // Apply shot buff decrement if used (ExpireShotBuff scales HP down when buff expires)
        if (result.ShotBuffUsed)
        {
            playerCharacter.ExpireShotBuff();
        }

        // Expire penalty buff (consumed after 1 arena battle)
        if (result.PenaltyBuffUsed)
        {
            playerCharacter.ExpirePenaltyBuff();
        }

        // Update arena statistics
        switch (result.Outcome)
        {
            case BattleOutcome.AttackerWon:
                playerCharacter.ArenaWins++;
                break;
            case BattleOutcome.DefenderWon:
                playerCharacter.ArenaLosses++;
                break;
            case BattleOutcome.Draw:
                playerCharacter.ArenaDraws++;
                break;
        }

        // Update cooldown tracking
        playerCharacter.LastOpponentId = result.DefenderCharacterId;
        playerCharacter.LastBattleAt = DateTime.UtcNow;
        playerCharacter.LastBattleId = result.BattleId;

        // Apply rewards — modify user entity directly (no intermediate save)
        var user = await _dbContext.Users.FindAsync(new object[] { playerCharacter.UserId }, cancellationToken);
        if (user != null)
        {
            user.FidelisBalance += result.AttackerFidelis;
        }
        playerCharacter.AddXP(result.AttackerXP);

        // If player died and has no Fino/Caneca to heal, auto-heal to full HP
        // Use a single inventory query to check relevant quantities
        if (!playerCharacter.IsAlive())
        {
            var inventory = await _inventoryRepository.GetUserInventoryAsync(playerCharacter.UserId, cancellationToken);
            var finoQty = inventory?.FirstOrDefault(i => i.Type == InventoryItemType.Fino)?.Quantity ?? 0;
            var canecaQty = inventory?.FirstOrDefault(i => i.Type == InventoryItemType.Caneca)?.Quantity ?? 0;
            var hasHealingItems = finoQty > 0 || canecaQty > 0;

            if (!hasHealingItems)
            {
                playerCharacter.RestoreHP();
                _logger.LogInformation(
                    "Auto-healed character {CharacterId} after arena death (no healing items available)",
                    playerCharacter.Id);
            }
        }

        await _characterRepository.UpdateAsync(playerCharacter);

        // Roll for consumable drops and FITAB if player won
        // (drops are saved by AddItemsAsync — acceptable extra round-trip since
        // it does upsert logic; we already batched user + character into one save)
        if (result.Outcome == BattleOutcome.AttackerWon)
        {
            await TryDropConsumablesAsync(playerCharacter.UserId, user, cancellationToken);
        }

        return true;
    }

    /// <summary>
    /// Calculates XP and Fidelis rewards based on battle outcome, level difference, and attacker level.
    /// Rewards scale with attacker level (level^LevelScalePower) so high-level arena battles
    /// give rewards comparable to stage mode.
    /// </summary>
    private (int xp, decimal fidelis) CalculateRewards(BattleOutcome outcome, Character attacker, Character defender)
    {
        var rewards = _myTunoScalingConfig.BattleRewards;

        // Unified level-diff multiplier for both XP and Fidelis
        var levelDiff = defender.Level - attacker.Level;
        var levelDiffMult = Math.Max(rewards.MinRewardMultiplier,
            Math.Min(rewards.MaxRewardMultiplier, 1.0 + levelDiff * rewards.LevelDiffScale));

        // Level-based scaling: rewards grow with attacker level (like stage mode)
        var levelScale = Math.Pow(attacker.Level, rewards.LevelScalePower);

        return outcome switch
        {
            BattleOutcome.AttackerWon => (
                (int)Math.Round(rewards.BaseWinXP * levelScale * levelDiffMult),
                (decimal)Math.Round((double)rewards.WinReward * levelScale * levelDiffMult, 2)),
            BattleOutcome.DefenderWon => (0, 0m),
            BattleOutcome.Draw => (
                (int)Math.Round(rewards.BaseDrawXP * levelScale * levelDiffMult),
                (decimal)Math.Round((double)rewards.DrawReward * levelScale * levelDiffMult, 2)),
            _ => (0, 0m)
        };
    }

    /// <summary>
    /// Generates a random seed for combat simulation
    /// </summary>
    private static int GenerateSeed()
    {
        return Random.Shared.Next(int.MinValue, int.MaxValue);
    }

    /// <summary>
    /// Rolls for consumable drops (Fino, Caneca, Cigarro, Canhão) and adds to player's inventory if successful.
    /// Accepts an already-loaded user entity to batch FITAB changes without an extra round-trip.
    /// </summary>
    private async Task TryDropConsumablesAsync(string userId, ApplicationUser? user, CancellationToken cancellationToken = default)
    {
        var rewards = _myTunoScalingConfig.BattleRewards;

        // Gate consumable drops behind biome progression
        // Fino=1(Forest), Shot=101(Swamp), Cigarro=301(Snowy), Caneca=501(Caverns), Canhão=701(Volcanic)
        var stageProgress = await _stageProgressRepository.GetByUserIdAsync(userId);
        var highestStage = stageProgress?.HighestStage ?? 1;

        var drops = new Dictionary<InventoryItemType, int>();

        if (highestStage >= 1 && Random.Shared.NextDouble() < rewards.FinoDropChance)
            drops[InventoryItemType.Fino] = 1;

        if (highestStage >= 501 && Random.Shared.NextDouble() < rewards.CanecaDropChance)
            drops[InventoryItemType.Caneca] = 1;

        if (highestStage >= 301 && Random.Shared.NextDouble() < rewards.CigarroDropChance)
            drops[InventoryItemType.Cigarro] = 1;

        if (highestStage >= 701 && Random.Shared.NextDouble() < rewards.CanhaoDropChance)
            drops[InventoryItemType.Canhao] = 1;

        if (highestStage >= 901 && Random.Shared.NextDouble() < rewards.PenaltyDropChance)
            drops[InventoryItemType.Penalty] = 1;

        if (drops.Count > 0)
            await _inventoryRepository.AddItemsAsync(userId, drops, cancellationToken);

        // Roll for FITAB drop — use the already-tracked user entity to avoid extra load + save
        var fitabRoll = Random.Shared.NextDouble();
        if (fitabRoll < _myTunoScalingConfig.BossMode.FitabDropChanceBattle && user != null)
        {
            user.FitabBalance++;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
    
}
