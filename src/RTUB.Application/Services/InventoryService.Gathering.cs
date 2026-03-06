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

// Gathering — energy management and resource gathering
public partial class InventoryService
{
    public async Task<(int CurrentEnergy, int MaxEnergy, int SecondsUntilNextRegen)> GetCurrentEnergyForCharacterAsync(Character character, CancellationToken cancellationToken = default)
    {
        if (character == null)
            return (0, 10, 0);

        ApplyEnergyRegen(character);

        var secondsUntilNext = 0;
        if (character.Energy < character.MaxEnergy)
        {
            var regenInterval = (int)character.EffectiveRegenInterval;
            if (regenInterval <= 0) regenInterval = 60;
            var lastRegen = character.LastEnergyRegenAt ?? DateTime.UtcNow;
            var elapsed = (DateTime.UtcNow - lastRegen).TotalSeconds;
            secondsUntilNext = Math.Max(1, regenInterval - (int)elapsed);
        }

        return (character.Energy, character.MaxEnergy, secondsUntilNext);
    }

    /// <summary>
    /// Gets the current energy for a character, applying passive regen since last check
    /// </summary>
    public async Task<(int CurrentEnergy, int MaxEnergy, int SecondsUntilNextRegen)> GetCurrentEnergyAsync(string userId, CancellationToken cancellationToken = default)
    {
        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null)
        {
            return (0, 10, 0);
        }

        ApplyEnergyRegen(character);

        // Calculate seconds until next regen tick
        var secondsUntilNext = 0;
        if (character.Energy < character.MaxEnergy)
        {
            var regenInterval = (int)character.EffectiveRegenInterval;
            if (regenInterval <= 0) regenInterval = 60;
            var lastRegen = character.LastEnergyRegenAt ?? DateTime.UtcNow;
            var elapsed = (DateTime.UtcNow - lastRegen).TotalSeconds;
            secondsUntilNext = Math.Max(1, regenInterval - (int)elapsed);
        }

        return (character.Energy, character.MaxEnergy, secondsUntilNext);
    }

    /// <summary>
    /// Gathers a resource by spending energy.
    /// Enforces a server-side cooldown to prevent browser-console abuse.
    /// </summary>
    public async Task<(bool Success, int Gathered, int RemainingEnergy, string Message)> GatherResourceAsync(string userId, InventoryItemType resourceType, CancellationToken cancellationToken = default)
    {
        // Validate resource type is active in config
        var typeName = resourceType.ToString();
        var resourceConfig = _gatheringConfig.Resources.FirstOrDefault(r => r.Type == typeName && r.IsActive);
        if (resourceConfig == null)
        {
            return (false, 0, 0, "Tipo de recurso inválido para destilação");
        }

        var energyCost = resourceConfig.EnergyCost;

        // Acquire per-user lock to prevent multi-tab race conditions.
        // Without this, two tabs can both read Energy=10, both deduct, and both save Energy=9
        // instead of the correct 10→9→8 sequence.
        var userLock = GetUserEnergyLock(userId);
        await userLock.WaitAsync(cancellationToken);
        try
        {
            // Force a fresh read from DB inside the lock to pick up changes from other circuits
            var character = await _characterRepository.GetByUserIdAsync(userId);
            if (character == null)
            {
                _logger.LogWarning("User {UserId} attempted to gather but has no character", userId);
                return (false, 0, 0, "Personagem não encontrado");
            }

            // ── Server-side cooldown: enforce the character's effective cast time ──
            // This prevents browser-console loops from bypassing the UI cast bar.
            // Uses EffectiveCastTime which accounts for Destilaria cast speed upgrades.
            var now = DateTime.UtcNow;
            var cooldown = Math.Max(character.EffectiveCastTime, 0.1);
            if (_cache.TryGetValue(GatherTimeCacheKeyPrefix + userId, out DateTime lastGather))
            {
                var elapsed = (now - lastGather).TotalSeconds;
                if (elapsed < cooldown)
                {
                    return (false, 0, 0, "Aguarda antes de destilar novamente");
                }
            }

            // Apply passive energy regen first
            ApplyEnergyRegen(character);

            // Check if enough energy
            if (character.Energy < energyCost)
            {
                return (false, 0, character.Energy, $"Energia insuficiente! Precisas de {energyCost} energia");
            }

            // Spend energy (don't reset LastEnergyRegenAt — preserve partial regen progress)
            character.Energy -= energyCost;
            await _characterRepository.UpdateAsync(character);

            // Check for double gathering (Destilaria improvement)
            var doubleChance = character.DoubleGatheringChance;
            var isDouble = doubleChance > 0 && Random.Shared.NextDouble() < doubleChance;
            var gatherAmount = isDouble ? 2 : 1;

            // Add resource to inventory
            await _inventoryRepository.AddItemAsync(userId, resourceType, gatherAmount, cancellationToken);

            // Record successful gather time for cooldown enforcement (bounded by sliding expiration)
            _cache.Set(GatherTimeCacheKeyPrefix + userId, DateTime.UtcNow, _gatherCacheOptions);

            var resourceName = resourceType switch
            {
                InventoryItemType.Vodka => "Vodka",
                InventoryItemType.Gin => "Gin",
                InventoryItemType.Whisky => "Whisky",
                InventoryItemType.Absinto => "Absinto",
                _ => resourceType.ToString()
            };

            var message = isDouble
                ? $"🔥 DUPLO! +{gatherAmount} {resourceName}!"
                : $"+1 {resourceName}!";

            return (true, gatherAmount, character.Energy, message);
        }
        finally
        {
            userLock.Release();
        }
    }

    /// <summary>
    /// Applies passive energy regeneration based on elapsed time since last regen
    /// 1 energy per regen interval (default 60 seconds), capped at MaxEnergy
    /// </summary>
    private void ApplyEnergyRegen(Character character)
    {
        if (character.Energy >= character.MaxEnergy)
        {
            character.LastEnergyRegenAt = DateTime.UtcNow;
            return;
        }

        var regenInterval = (int)character.EffectiveRegenInterval;
        if (regenInterval <= 0) regenInterval = 60;

        var lastRegen = character.LastEnergyRegenAt ?? DateTime.UtcNow;
        var elapsed = DateTime.UtcNow - lastRegen;
        var regenAmount = (int)(elapsed.TotalSeconds / regenInterval);

        if (regenAmount > 0)
        {
            character.Energy = Math.Min(character.MaxEnergy, character.Energy + regenAmount);
            // Keep remainder time by advancing lastRegen by the consumed ticks only
            character.LastEnergyRegenAt = lastRegen.AddSeconds(regenAmount * regenInterval);
        }
    }

    /// <summary>
    /// Gets all instrument part quantities for a user (items with type 100-112)
    /// </summary>
}
