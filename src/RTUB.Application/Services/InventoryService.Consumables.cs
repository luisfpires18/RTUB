using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

// Consumables — healing items, buffs, quantity getters
public partial class InventoryService
{

    /// <summary>
    /// Uses a Fino to heal the user's character (25% HP)
    /// </summary>
    public async Task<(bool Success, int HealedAmount, string Message)> UseFinoAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await UseHealingItemAsync(userId, InventoryItemType.Fino, FinoHealPercentage, "Fino", cancellationToken);
    }

    /// <summary>
    /// Uses a Caneca to heal the user's character (50% HP)
    /// </summary>
    public async Task<(bool Success, int HealedAmount, string Message)> UseCanecaAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await UseHealingItemAsync(userId, InventoryItemType.Caneca, CanecaHealPercentage, "Caneca", cancellationToken);
    }

    /// <summary>
    /// Shared healing logic for Fino/Caneca
    /// </summary>
    private async Task<(bool Success, int HealedAmount, string Message)> UseHealingItemAsync(
        string userId, InventoryItemType itemType, double healPercentage, string itemName,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate character (read-only checks)
        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            var userName = user?.UserName ?? userId;
            _logger.LogWarning("User {UserName} attempted to use {Item} but has no character", userName, itemName);
            return (false, 0, "Personagem não encontrado");
        }

        var maxHp = character.ShotBuffBattlesRemaining > 0
            ? character.GetDisplayMaxHP()
            : character.TotalHP;

        var currentHp = character.CurrentHP ?? maxHp;

        if (currentHp >= maxHp)
        {
            return (false, 0, "O personagem já está com HP máximo");
        }

        // 2. Atomically consume item (prevents TOCTOU race — single SQL with WHERE Quantity >= 1)
        var consumed = await _inventoryRepository.ConsumeItemAsync(userId, itemType, 1, cancellationToken);
        if (!consumed)
        {
            return (false, 0, $"Não tens {itemName} no inventário");
        }

        // 3. Apply heal effect (only after successful consume)
        var healAmount = (int)Math.Round(maxHp * healPercentage);

        character.Heal(healAmount);
        await _characterRepository.UpdateAsync(character);

        return (true, healAmount, $"Personagem curado! +{healAmount} HP");
    }

    /// <summary>
    /// Uses a Cigarro — shields the next 3 incoming hits (no damage taken)
    /// </summary>
    public async Task<(bool Success, string Message)> UseCigarroAsync(string userId, CancellationToken cancellationToken = default)
    {
        // 1. Validate character (read-only checks)
        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            var userName = user?.UserName ?? userId;
            _logger.LogWarning("User {UserName} attempted to use cigarro but has no character", userName);
            return (false, "Personagem não encontrado");
        }

        var currentHp = character.CurrentHP ?? character.TotalHP;
        if (currentHp <= 0)
        {
            return (false, "Não podes usar cigarro num personagem morto");
        }

        if (character.CigarroShieldHitsRemaining > 0)
        {
            return (false, "Já tens um cigarro ativo");
        }

        // 2. Atomically consume item (prevents TOCTOU race)
        var consumed = await _inventoryRepository.ConsumeItemAsync(userId, InventoryItemType.Cigarro, 1, cancellationToken);
        if (!consumed)
        {
            return (false, "Não tens cigarros no inventário");
        }

        // 3. Apply effect (only after successful consume)
        character.CigarroShieldHitsRemaining = CigarroBuffRuns;
        await _characterRepository.UpdateAsync(character);

        return (true, $"Cigarro ativado! +{(int)(character.EffectiveCigarroDodgeChance * 100)}% dodge por {CigarroBuffRuns} runs");
    }

    /// <summary>
    /// Uses a Canhão — next 3 outgoing hits deal 30% more damage
    /// </summary>
    public async Task<(bool Success, string Message)> UseCanhaoAsync(string userId, CancellationToken cancellationToken = default)
    {
        // 1. Validate character (read-only checks)
        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            var userName = user?.UserName ?? userId;
            _logger.LogWarning("User {UserName} attempted to use canhão but has no character", userName);
            return (false, "Personagem não encontrado");
        }

        var currentHp = character.CurrentHP ?? character.TotalHP;
        if (currentHp <= 0)
        {
            return (false, "Não podes usar canhão num personagem morto");
        }

        if (character.HasCanhaoBuff)
        {
            return (false, "Já tens um canhão ativo");
        }

        // 2. Atomically consume item (prevents TOCTOU race)
        var consumed = await _inventoryRepository.ConsumeItemAsync(userId, InventoryItemType.Canhao, 1, cancellationToken);
        if (!consumed)
        {
            return (false, "Não tens canhões no inventário");
        }

        // 3. Apply effect (only after successful consume)
        // Store as paused remaining ms — the timer only ticks during active stage runs.
        // ResumeCanhaoBuff() is called when a stage run begins.
        var canhaoMinutes = character.EffectiveCanhaoMinutes;
        character.CanhaoBuffExpiresAt = null;
        character.CanhaoBuffRemainingMs = (long)TimeSpan.FromMinutes(canhaoMinutes).TotalMilliseconds;
        await _characterRepository.UpdateAsync(character);

        return (true, $"Canhão ativado! AOE por {canhaoMinutes} minutos");
    }

    /// <summary>
    /// Uses a Penalty — 0.5s attack speed + 100% crit for 1 run/battle
    /// </summary>
    public async Task<(bool Success, string Message)> UsePenaltyAsync(string userId, CancellationToken cancellationToken = default)
    {
        // 1. Validate character (read-only checks)
        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            var userName = user?.UserName ?? userId;
            _logger.LogWarning("User {UserName} attempted to use penalty but has no character", userName);
            return (false, "Personagem não encontrado");
        }

        var currentHp = character.CurrentHP ?? character.TotalHP;
        if (currentHp <= 0)
        {
            return (false, "Não podes usar penalty num personagem morto");
        }

        if (character.HasPenaltyBuff)
        {
            return (false, "Já tens um penalty ativo");
        }

        // 2. Atomically consume item (prevents TOCTOU race)
        var consumed = await _inventoryRepository.ConsumeItemAsync(userId, InventoryItemType.Penalty, 1, cancellationToken);
        if (!consumed)
        {
            return (false, "Não tens penalties no inventário");
        }

        // 3. Apply effect (only after successful consume)
        // Store as paused state (RemainingMs) so the timer only ticks inside active runs
        var penaltyMinutes = character.EffectivePenaltyMinutes;
        var totalMs = (long)TimeSpan.FromMinutes(penaltyMinutes).TotalMilliseconds;
        character.PenaltyBuffRemainingMs = totalMs;
        character.PenaltyBuffExpiresAt = null;
        await _characterRepository.UpdateAsync(character);

        return (true, $"Penalty ativado! {(character.EffectivePenaltyLifesteal * 100):F1}% lifesteal por {penaltyMinutes} minutos");
    }

    /// <summary>
    /// Gets the quantity of Fino in the user's inventory
    /// </summary>
    public async Task<int> GetFinoQuantityAsync(string userId, CancellationToken cancellationToken = default)
    {
        var item = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Fino, cancellationToken);
        return item?.Quantity ?? 0;
    }

    /// <summary>
    /// Gets the quantity of Caneca in the user's inventory
    /// </summary>
    public async Task<int> GetCanecaQuantityAsync(string userId, CancellationToken cancellationToken = default)
    {
        var item = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Caneca, cancellationToken);
        return item?.Quantity ?? 0;
    }

    /// <summary>
    /// Gets the quantity of Cigarro in the user's inventory
    /// </summary>
    public async Task<int> GetCigarroQuantityAsync(string userId, CancellationToken cancellationToken = default)
    {
        var item = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Cigarro, cancellationToken);
        return item?.Quantity ?? 0;
    }

    /// <summary>
    /// Gets the quantity of Canhão in the user's inventory
    /// </summary>
    public async Task<int> GetCanhaoQuantityAsync(string userId, CancellationToken cancellationToken = default)
    {
        var item = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Canhao, cancellationToken);
        return item?.Quantity ?? 0;
    }

    /// <summary>
    /// Gets the quantity of Penalty in the user's inventory
    /// </summary>
    public async Task<int> GetPenaltyQuantityAsync(string userId, CancellationToken cancellationToken = default)
    {
        var item = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Penalty, cancellationToken);
        return item?.Quantity ?? 0;
    }

    /// <summary>
    /// Gets the quantity of shots in the user's inventory
    /// </summary>
    public async Task<int> GetShotQuantityAsync(string userId, CancellationToken cancellationToken = default)
    {
        var shotItem = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Shot, cancellationToken);
        return shotItem?.Quantity ?? 0;
    }

    // Shot empowers the next 5 arena battles or stage runs
    private const int ShotBuffBattles = 5;
    private const double ShotBuffMultiplier = 1.05; // 5% boost

    /// <summary>
    /// Uses a shot to empower the character's next 5 arena battles
    /// Increases all stats by 20% for the duration
    /// Also scales up CurrentHP proportionally to the new buffed max HP
    /// </summary>
    public async Task<(bool Success, int BattlesEmpowered, string Message)> UseShotAsync(string userId, CancellationToken cancellationToken = default)
    {
        // 1. Validate character (read-only checks)
        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            var userName = user?.UserName ?? userId;
            _logger.LogWarning("User {UserName} attempted to use shot but has no character", userName);
            return (false, 0, "Personagem não encontrado");
        }

        var currentHp = character.CurrentHP ?? character.TotalHP;
        if (currentHp <= 0)
        {
            return (false, 0, "Não podes usar shot num personagem morto");
        }

        if (character.ShotBuffBattlesRemaining > 0)
        {
            return (false, 0, "Já tens um buff ativo");
        }

        // 2. Atomically consume item (prevents TOCTOU race)
        var consumed = await _inventoryRepository.ConsumeItemAsync(userId, InventoryItemType.Shot, 1, cancellationToken);
        if (!consumed)
        {
            return (false, 0, "Não tens shots no inventário");
        }

        // 3. Apply buff effect (only after successful consume)
        character.ShotBuffBattlesRemaining = ShotBuffBattles;
        
        // Scale up CurrentHP proportionally to the new buffed max HP
        // Use the clean percentage formula for the buffed max HP
        var currentHpValue = character.CurrentHP ?? character.TotalHP;
        var unbuffedMaxHp = character.TotalHP;
        var buffedMaxHp = character.GetDisplayMaxHP();
        
        // Calculate the new CurrentHP proportionally
        var hpRatio = (double)currentHpValue / unbuffedMaxHp;
        character.CurrentHP = (int)Math.Round(hpRatio * buffedMaxHp);
        
        await _characterRepository.UpdateAsync(character);

        return (true, ShotBuffBattles, $"Shot ativado! +{(int)((character.EffectiveShotBuffMultiplier - 1) * 100)}% stats por {ShotBuffBattles} runs");
    }

    /// <summary>
    /// Gets the energy cost for a resource type from config
    /// </summary>
    private int GetEnergyCost(InventoryItemType type)
    {
        var typeName = type.ToString();
        var resource = _gatheringConfig.Resources.FirstOrDefault(r => r.Type == typeName);
        return resource?.EnergyCost ?? int.MaxValue;
    }

    /// <summary>
    /// Gets the quantity of a resource in the user's inventory
    /// </summary>
    public async Task<int> GetResourceQuantityAsync(string userId, InventoryItemType type, CancellationToken cancellationToken = default)
    {
        var item = await _inventoryRepository.GetItemAsync(userId, type, cancellationToken);
        return item?.Quantity ?? 0;
    }

    /// <summary>
    /// Gets all inventory item quantities for a user in a single query.
    /// Returns a dictionary of item type to quantity, replacing multiple individual Get*QuantityAsync calls.
    /// </summary>
    public async Task<Dictionary<InventoryItemType, int>> GetUserInventorySummaryAsync(string userId, CancellationToken cancellationToken = default)
    {
        var allItems = await _inventoryRepository.GetUserInventoryAsync(userId, cancellationToken);
        return allItems
            .Where(i => i.Quantity > 0)
            .ToDictionary(i => i.Type, i => i.Quantity);
    }

    /// <summary>
    /// Gets the current energy for an already-loaded character, applying passive regen since last check.
    /// Avoids a redundant character load when the caller already has the character.
    /// </summary>
}
