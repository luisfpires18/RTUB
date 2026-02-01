using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Interfaces;
using RTUB.Application.DTOs;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;
using System.Text.Json;

namespace RTUB.Application.Services;

/// <summary>
/// Service implementation for Stage Mode operations
/// Handles stage battles, progression, and reward distribution
/// </summary>
public class StageService : IStageService
{
    private readonly IStageRepository _stageRepository;
    private readonly ICharacterStageProgressRepository _progressRepository;
    private readonly ICharacterRepository _characterRepository;
    private readonly IBattleRepository _battleRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICombatEngine _combatEngine;
    private readonly IOptions<MyTunoScalingConfiguration> _scalingConfig;
    private readonly ILogger<StageService> _logger;

    // Thread-safe Random instance for drop calculations and seed generation
    private static readonly Random _random = new Random();
    private static readonly object _randomLock = new object();

    public StageService(
        IStageRepository stageRepository,
        ICharacterStageProgressRepository progressRepository,
        ICharacterRepository characterRepository,
        IBattleRepository battleRepository,
        IInventoryRepository inventoryRepository,
        UserManager<ApplicationUser> userManager,
        ICombatEngine combatEngine,
        IOptions<MyTunoScalingConfiguration> scalingConfig,
        ILogger<StageService> logger)
    {
        _stageRepository = stageRepository;
        _progressRepository = progressRepository;
        _characterRepository = characterRepository;
        _battleRepository = battleRepository;
        _inventoryRepository = inventoryRepository;
        _userManager = userManager;
        _combatEngine = combatEngine;
        _scalingConfig = scalingConfig;
        _logger = logger;
    }

    /// <summary>
    /// Get all available stages for a character based on their level
    /// </summary>
    public async Task<List<Stage>> GetAvailableStagesAsync(int characterId, CancellationToken cancellationToken = default)
    {
        var character = await _characterRepository.GetByIdAsync(characterId);
        if (character == null)
            throw new EntityNotFoundException(nameof(Character), characterId);
            
        return await _stageRepository.GetStagesForLevelAsync(character.Level, cancellationToken);
    }

    /// <summary>
    /// Get a character's progress on all stages
    /// </summary>
    public async Task<List<CharacterStageProgress>> GetCharacterProgressAsync(int characterId, CancellationToken cancellationToken = default)
    {
        return await _progressRepository.GetCharacterProgressAsync(characterId, cancellationToken);
    }

    /// <summary>
    /// Start a battle against a stage enemy
    /// Creates an AI opponent based on stage configuration and simulates combat
    /// </summary>
    public async Task<Battle> StartStageBattleAsync(int characterId, int stageNumber, CancellationToken cancellationToken = default)
    {
        // Load player character
        var character = await _characterRepository.GetByIdAsync(characterId);
        if (character == null)
            throw new EntityNotFoundException(nameof(Character), characterId);
        
        // Check if character is alive
        if (!character.IsAlive())
            throw new InvalidOperationException("Personagem derrotado. Precisa de reviver antes de lutar.");
        
        // Load stage
        var stage = await _stageRepository.GetByStageNumberAsync(stageNumber, cancellationToken);
        if (stage == null)
            throw new InvalidOperationException($"Stage {stageNumber} não encontrado");
        
        // Verify character meets level requirement
        if (character.Level < stage.RequiredLevel)
        {
            throw new InvalidOperationException(
                $"Nível {character.Level} é muito baixo para stage {stageNumber} (requer nível {stage.RequiredLevel})");
        }
        
        // Get stage configuration from scaling config
        var stageConfig = _scalingConfig.Value.Stages.StageList
            .FirstOrDefault(s => s.StageNumber == stageNumber);
            
        if (stageConfig == null)
        {
            throw new InvalidOperationException($"Stage {stageNumber} configuration não encontrada");
        }
        
        // Create AI opponent based on stage stats
        // Note: We don't save the AI opponent to database - it's temporary for the battle simulation
        var aiOpponent = Character.Create(
            userId: $"STAGE_AI_{stageNumber}",
            nickname: stage.Name,
            level: character.Level, // Match player level
            hp: stageConfig.EnemyStats.Hp,
            power: stageConfig.EnemyStats.Power,
            speed: stageConfig.EnemyStats.Speed,
            criticalChance: stageConfig.EnemyStats.CriticalChance
        );
        
        // We need to save the AI opponent temporarily so it has an ID for the battle record
        await _characterRepository.AddAsync(aiOpponent);
        
        // Generate seed for deterministic combat
        var seed = GenerateSeed();
        
        // Run combat simulation
        var combatResult = _combatEngine.Simulate(character, aiOpponent, seed);
        
        // Create battle record
        var battle = Battle.Create(characterId, aiOpponent.Id, seed, combatResult.Outcome);
        
        // For stage battles, we don't give standard battle rewards (handled separately in CompleteStageBattleAsync)
        battle.SetRewards(0, 0m);
        
        // Serialize replay events to JSON
        var replayJson = JsonSerializer.Serialize(combatResult.Events, new JsonSerializerOptions
        {
            WriteIndented = false
        });
        battle.SetReplay(replayJson);
        
        // Persist battle
        await _battleRepository.AddAsync(battle);
        
        // Update character HP based on battle outcome
        await ApplyHPChangesAsync(character, combatResult);
        
        _logger.LogInformation(
            "Stage battle created: Character {CharacterId} vs Stage {StageNumber}, Outcome: {Outcome}",
            characterId, stageNumber, combatResult.Outcome);
        
        return battle;
    }

    /// <summary>
    /// Complete a stage battle and distribute rewards
    /// Awards: Fidelis, instrument (first time), beer/shot drops
    /// </summary>
    public async Task CompleteStageBattleAsync(int battleId, CancellationToken cancellationToken = default)
    {
        var battle = await _battleRepository.GetByIdAsync(battleId);
        if (battle == null)
            throw new EntityNotFoundException(nameof(Battle), battleId);
        
        // Only winners get rewards
        if (battle.Outcome != BattleOutcome.AttackerWon)
        {
            _logger.LogInformation("Battle {BattleId} not won, no stage rewards", battleId);
            return;
        }
        
        var attacker = await _characterRepository.GetByIdAsync(battle.AttackerCharacterId);
        if (attacker == null)
            throw new EntityNotFoundException(nameof(Character), battle.AttackerCharacterId);
            
        var defender = await _characterRepository.GetByIdAsync(battle.DefenderCharacterId);
        if (defender == null)
        {
            _logger.LogWarning("Defender character {DefenderId} not found for battle {BattleId}", 
                battle.DefenderCharacterId, battleId);
            return;
        }
        
        // Identify stage from defender (AI opponent)
        // The defender's UserId should be in format "STAGE_AI_{stageNumber}"
        if (!defender.UserId.StartsWith("STAGE_AI_"))
        {
            _logger.LogWarning("Battle {BattleId} defender is not a stage AI opponent", battleId);
            return;
        }
        
        // Extract stage number from AI user ID
        var stageNumberStr = defender.UserId.Replace("STAGE_AI_", "");
        if (!int.TryParse(stageNumberStr, out var stageNumber))
        {
            _logger.LogWarning("Could not parse stage number from AI user ID {UserId}", defender.UserId);
            return;
        }
        
        // Load stage
        var stage = await _stageRepository.GetByStageNumberAsync(stageNumber, cancellationToken);
        if (stage == null)
        {
            _logger.LogWarning("Stage {StageNumber} not found for battle {BattleId}", stageNumber, battleId);
            return;
        }
        
        // Get or create progress record
        var progress = await _progressRepository.GetOrCreateProgressAsync(attacker.Id, stage.Id, cancellationToken);
        
        var isFirstCompletion = progress.CompletionCount == 0;
        
        // Mark stage as completed
        progress.MarkCompleted();
        await _progressRepository.UpdateAsync(progress);
        
        // Award Fidelis
        var user = await _userManager.FindByIdAsync(attacker.UserId);
        if (user != null)
        {
            user.FidelisBalance += stage.FidelisReward;
            await _userManager.UpdateAsync(user);
            _logger.LogInformation("Awarded {Fidelis} Fidelis to user {UserId} for completing stage {StageNumber}",
                stage.FidelisReward, attacker.UserId, stage.StageNumber);
        }
        
        // Award instrument on first completion
        if (isFirstCompletion && !progress.InstrumentClaimed)
        {
            await _inventoryRepository.AddItemAsync(attacker.UserId, stage.RewardInstrument, 1, cancellationToken);
            progress.ClaimInstrument();
            await _progressRepository.UpdateAsync(progress);
            _logger.LogInformation("Awarded instrument {Instrument} to user {UserId} for first completion of stage {StageNumber}",
                stage.RewardInstrument, attacker.UserId, stage.StageNumber);
        }
        
        // Random beer drop (using thread-safe Random instance)
        double beerRoll;
        double shotRoll;
        lock (_randomLock)
        {
            beerRoll = _random.NextDouble();
            shotRoll = _random.NextDouble();
        }
        
        if (beerRoll < stage.BeerDropChance)
        {
            await _inventoryRepository.AddItemAsync(attacker.UserId, InventoryItemType.Beer, 1, cancellationToken);
            _logger.LogInformation("Beer dropped for user {UserId} after stage {StageNumber}", 
                attacker.UserId, stage.StageNumber);
        }
        
        // Random shot drop
        if (shotRoll < stage.ShotDropChance)
        {
            await _inventoryRepository.AddItemAsync(attacker.UserId, InventoryItemType.Shot, 1, cancellationToken);
            _logger.LogInformation("Shot dropped for user {UserId} after stage {StageNumber}", 
                attacker.UserId, stage.StageNumber);
        }
        
        _logger.LogInformation("Completed stage battle rewards for user {UserId}, stage {StageNumber}, battle {BattleId}",
            attacker.UserId, stage.StageNumber, battleId);
    }

    /// <summary>
    /// Get instrument stats bonus from configuration
    /// </summary>
    public InstrumentStatBonus? GetInstrumentStats(InventoryItemType instrumentType)
    {
        var instrumentName = instrumentType.ToString();
        
        if (_scalingConfig.Value.Stages.InstrumentStats.TryGetValue(instrumentName, out var stats))
        {
            return stats;
        }
        
        return null;
    }

    /// <summary>
    /// Updates character HP based on battle outcome
    /// </summary>
    private async Task ApplyHPChangesAsync(Character character, CombatResult combatResult)
    {
        character.CurrentHP = combatResult.AttackerFinalHP;
        await _characterRepository.UpdateAsync(character);
        
        _logger.LogInformation(
            "HP updated after stage battle: Character {CharacterId} HP = {HP}, Outcome = {Outcome}",
            character.Id, character.CurrentHP, combatResult.Outcome);
    }

    /// <summary>
    /// Generates a random seed for combat simulation using thread-safe Random instance
    /// </summary>
    private static int GenerateSeed()
    {
        lock (_randomLock)
        {
            return _random.Next(int.MinValue, int.MaxValue);
        }
    }
}
