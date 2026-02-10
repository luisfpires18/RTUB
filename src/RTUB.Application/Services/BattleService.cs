using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
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
    private readonly IMatchmakingService _matchmakingService;
    private readonly ICombatEngine _combatEngine;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<BattleService> _logger;
    private readonly MyTunoScalingConfiguration _myTunoScalingConfig;
    private readonly IAuditLogService _auditLogService;
    private readonly IStageProgressRepository _stageProgressRepository;

    public BattleService(
        ICharacterRepository characterRepository,
        IMatchmakingService matchmakingService,
        ICombatEngine combatEngine,
        IInventoryRepository inventoryRepository,
        UserManager<ApplicationUser> userManager,
        ILogger<BattleService> logger,
        IOptions<MyTunoScalingConfiguration> myTunoScalingConfig,
        IAuditLogService auditLogService,
        IStageProgressRepository stageProgressRepository)
    {
        _characterRepository = characterRepository;
        _matchmakingService = matchmakingService;
        _combatEngine = combatEngine;
        _inventoryRepository = inventoryRepository;
        _userManager = userManager;
        _logger = logger;
        _myTunoScalingConfig = myTunoScalingConfig.Value;
        _auditLogService = auditLogService;
        _stageProgressRepository = stageProgressRepository;
    }

    /// <summary>
    /// Creates and executes a battle vs a specific opponent character
    /// Rewards are NOT applied until FinalizeAndApplyRewardsAsync is called
    /// </summary>
    public async Task<BattleResult> CreateBattleVsOpponentAsync(int playerCharacterId, int opponentCharacterId)
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
        await _auditLogService.AddAsync(new AuditLog
        {
            EntityType = "ArenaBattle",
            Action = "Started",
            UserName = playerName,
            UserId = playerCharacter.UserId,
            Timestamp = DateTime.UtcNow,
            EntityDisplayName = $"{playerName} vs {opponentName}",
            Changes = System.Text.Json.JsonSerializer.Serialize(new { Opponent = opponentName })
        });

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
    public async Task<bool> FinalizeAndApplyRewardsAsync(BattleResult result)
    {
        const int maxRetries = 1;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await ApplyBattleRewardsAsync(result);
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
    private async Task<bool> ApplyBattleRewardsAsync(BattleResult result)
    {
        // Load the attacker character
        var playerCharacter = await _characterRepository.GetByIdAsync(result.AttackerCharacterId);
        if (playerCharacter == null)
        {
            _logger.LogError("Player character {CharacterId} not found for battle finalization", result.AttackerCharacterId);
            return false;
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

        // Apply rewards
        await ApplyRewardsAsync(playerCharacter, result.AttackerXP, result.AttackerFidelis);

        // Arena battles do NOT restore HP — if you die, you stay dead until revived

        await _characterRepository.UpdateAsync(playerCharacter);

        // Roll for consumable drops if player won
        if (result.Outcome == BattleOutcome.AttackerWon)
        {
            await TryDropConsumablesAsync(playerCharacter.UserId);
            await TryDropFitabAsync(playerCharacter.UserId);
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
    /// Applies XP and Fidelis rewards to the player
    /// Note: AI opponents do not receive rewards
    /// </summary>
    private async Task ApplyRewardsAsync(Character character, int xp, decimal fidelis)
    {
        // Add XP to character (handles level-ups)
        character.AddXP(xp);

        // Update character
        await _characterRepository.UpdateAsync(character);

        // Add Fidelis to user
        var user = await _userManager.FindByIdAsync(character.UserId);
        if (user != null)
        {
            user.FidelisBalance += fidelis;
            await _userManager.UpdateAsync(user);
        }
    }

    /// <summary>
    /// Generates a random seed for combat simulation
    /// </summary>
    private static int GenerateSeed()
    {
        return new Random().Next(int.MinValue, int.MaxValue);
    }

    /// <summary>
    /// Rolls for consumable drops (Fino, Caneca, Cigarro, Canhão) and adds to player's inventory if successful
    /// </summary>
    private async Task TryDropConsumablesAsync(string userId)
    {
        var random = new Random();
        var rewards = _myTunoScalingConfig.BattleRewards;

        // Gate consumable drops behind biome progression
        // Fino=1(Forest), Shot=101(Swamp), Cigarro=301(Snowy), Caneca=501(Caverns), Canhão=701(Volcanic)
        var stageProgress = await _stageProgressRepository.GetByUserIdAsync(userId);
        var highestStage = stageProgress?.HighestStage ?? 1;

        if (highestStage >= 1 && random.NextDouble() < rewards.FinoDropChance)
            await _inventoryRepository.AddItemAsync(userId, InventoryItemType.Fino, 1);

        if (highestStage >= 501 && random.NextDouble() < rewards.CanecaDropChance)
            await _inventoryRepository.AddItemAsync(userId, InventoryItemType.Caneca, 1);

        if (highestStage >= 301 && random.NextDouble() < rewards.CigarroDropChance)
            await _inventoryRepository.AddItemAsync(userId, InventoryItemType.Cigarro, 1);

        if (highestStage >= 701 && random.NextDouble() < rewards.CanhaoDropChance)
            await _inventoryRepository.AddItemAsync(userId, InventoryItemType.Canhao, 1);

        if (highestStage >= 901 && random.NextDouble() < rewards.PenaltyDropChance)
            await _inventoryRepository.AddItemAsync(userId, InventoryItemType.Penalty, 1);
    }

    /// <summary>
    /// Rolls for FITAB drop (Boss Mode entry currency) after arena victory
    /// </summary>
    private async Task TryDropFitabAsync(string userId)
    {
        var roll = Random.Shared.NextDouble();

        if (roll < _myTunoScalingConfig.BossMode.FitabDropChanceBattle)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.FitabBalance++;
                await _userManager.UpdateAsync(user);
            }
        }
    }
}
