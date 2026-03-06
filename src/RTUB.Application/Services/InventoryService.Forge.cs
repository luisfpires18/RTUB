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

// Forge — weapon forging, upgrades, equipment slot upgrades
public partial class InventoryService
{
    public async Task<(bool Success, ForgedWeapon? Weapon, string Message)> ForgeWeaponAsync(
        string userId, InventoryItemType instrumentPart, InventoryItemType drink,
        WeaponType weaponType, string weaponName, CancellationToken cancellationToken = default)
    {
        if (!InstrumentTypeHelper.IsInstrumentPart(instrumentPart))
            return (false, null, "Item de instrumento inválido");

        if (!DrinkTypes.Contains(drink))
            return (false, null, "Bebida inválida");

        if (string.IsNullOrWhiteSpace(weaponName) || weaponName.Length > 100)
            return (false, null, "Nome da arma inválido (máx 100 caracteres)");

        var instrItem = await _inventoryRepository.GetItemAsync(userId, instrumentPart, cancellationToken);
        if (instrItem == null || instrItem.Quantity <= 0)
            return (false, null, "Não tens este instrumento no inventário");

        // Determine how many drinks this tier requires
        var drinkResource = _scalingConfig.Gathering.Resources
            .FirstOrDefault(r => r.Type == drink.ToString());
        var forgeCost = drinkResource?.ForgeCost ?? 1;

        var drinkItem = await _inventoryRepository.GetItemAsync(userId, drink, cancellationToken);
        if (drinkItem == null || drinkItem.Quantity < forgeCost)
            return (false, null, $"Precisas de {forgeCost}x {drinkResource?.Name ?? drink.ToString()} (tens {drinkItem?.Quantity ?? 0})");

        // Calculate weapon stats from config, scaled by drink tier
        var weaponStats = _scalingConfig.StageMode.EquipmentStats.Instrument;
        var forging = _scalingConfig.StageMode.Forging;
        var drinkEnergyCost = drinkResource?.EnergyCost ?? 1;

        // Drink tier multiplier: higher-tier drinks produce stronger weapons
        // Formula: 1.0 + (energyCost - 1) * bonusPerTier → Cerveja=1.0×, Vinho=1.25×, … Aguardente=3.25×
        var drinkTierMult = 1.0 + (drinkEnergyCost - 1) * forging.DrinkStatBonusPerTier;

        // Roll random instrument quality within configured range
        var instrumentQuality = forging.InstrumentQualityMin +
            Random.Shared.NextDouble() * (forging.InstrumentQualityMax - forging.InstrumentQualityMin);

        // 2H weapons get a multiplier to match dual-wielding 1H
        var isTwoHanded = WeaponTypeHelper.IsTwoHanded(weaponType);
        var handedMult = isTwoHanded ? forging.TwoHandedMultiplier : 1.0;

        var totalMult = drinkTierMult * instrumentQuality * handedMult;

        // Roll crit and speed for high-tier drinks (unique, forge-time only)
        // 2H weapons get two independent rolls to match dual-wielding two 1H weapons
        var rollCount = isTwoHanded ? 2 : 1;

        var bonusCrit = 0.0;
        for (int i = 0; i < rollCount; i++)
        {
            if (drinkEnergyCost >= forging.CritMinDrinkCost && Random.Shared.NextDouble() < forging.CritRollChance)
            {
                bonusCrit += forging.CritMin + Random.Shared.NextDouble() * (forging.CritMax - forging.CritMin);
            }
        }
        bonusCrit = Math.Round(bonusCrit, 3);

        var bonusSpeed = 0;
        for (int i = 0; i < rollCount; i++)
        {
            if (drinkEnergyCost >= forging.SpeedMinDrinkCost && Random.Shared.NextDouble() < forging.SpeedRollChance)
            {
                bonusSpeed += Random.Shared.Next(forging.SpeedMin, forging.SpeedMax + 1);
            }
        }

        var weapon = ForgedWeapon.Create(
            userId, weaponName, weaponType,
            instrumentPart, drink,
            bonusHP: (int)Math.Round(weaponStats.HP * totalMult),
            bonusPower: (int)Math.Round(weaponStats.Power * totalMult),
            bonusSpeed: bonusSpeed,
            bonusDefense: (int)Math.Round(weaponStats.Defense * totalMult),
            bonusCriticalChance: bonusCrit);

        // Atomic transaction: consume both materials + create weapon in a single commit.
        // Prevents material loss if drink consume or weapon insert fails after instrument is consumed.
        var ctx = _contextFactory.CreateDbContext();
        var transaction = await ctx.Database.BeginTransactionIfSupportedAsync(cancellationToken);
        var instrRows = await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE InventoryItems SET Quantity = Quantity - 1 WHERE UserId = {userId} AND Type = {(int)instrumentPart} AND Quantity >= 1",
            cancellationToken);
        if (instrRows == 0)
            return (false, null, "Erro ao consumir instrumento");

        // Atomically consume drink
        var drinkRows = await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE InventoryItems SET Quantity = Quantity - {forgeCost} WHERE UserId = {userId} AND Type = {(int)drink} AND Quantity >= {forgeCost}",
            cancellationToken);
        if (drinkRows == 0)
            return (false, null, "Erro ao consumir bebida");

        ctx.ForgedWeapons.Add(weapon);
        await ctx.SaveChangesAsync(cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);

        return (true, weapon, $"Arma forjada: {weaponName}!");
    }

    public async Task<List<ForgedWeapon>> GetForgedWeaponsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var ctx = _contextFactory.CreateDbContext();
        return await ctx.ForgedWeapons
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<(bool Success, string Message)> EquipWeaponAsync(string userId, int weaponId, int slot, CancellationToken cancellationToken = default)
    {
        if (slot != 1 && slot != 2)
            return (false, "Slot inválido");

        var ctx = _contextFactory.CreateDbContext();
        var weapon = await ctx.ForgedWeapons.FirstOrDefaultAsync(w => w.Id == weaponId && w.UserId == userId, cancellationToken);
        if (weapon == null)
            return (false, "Arma não encontrada");

        var character = await ctx.Characters.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (character == null)
            return (false, "Personagem não encontrado");

        // Unequip any weapons currently in the target slot(s)
        if (weapon.IsTwoHanded)
        {
            // Two-handed fills both slots: unequip whatever is in slot 1 and 2
            await UnequipWeaponInternal(character, 1, ctx, cancellationToken);
            await UnequipWeaponInternal(character, 2, ctx, cancellationToken);
            character.EquippedWeapon1 = weaponId;
            character.EquippedWeapon2 = weaponId; // same weapon in both slots
        }
        else
        {
            // One-handed: if the other slot has a two-handed weapon, unequip it from both
            var otherSlot = slot == 1 ? 2 : 1;
            var otherWeaponId = slot == 1 ? character.EquippedWeapon2 : character.EquippedWeapon1;
            if (otherWeaponId.HasValue)
            {
                var otherWeapon = await ctx.ForgedWeapons.FirstOrDefaultAsync(w => w.Id == otherWeaponId.Value, cancellationToken);
                if (otherWeapon?.IsTwoHanded == true)
                {
                    await UnequipWeaponInternal(character, 1, ctx, cancellationToken);
                    await UnequipWeaponInternal(character, 2, ctx, cancellationToken);
                }
            }

            await UnequipWeaponInternal(character, slot, ctx, cancellationToken);
            if (slot == 1) character.EquippedWeapon1 = weaponId;
            else character.EquippedWeapon2 = weaponId;
        }

        weapon.IsEquipped = true;
        await RecalculateEquipmentBonusesAsync(character, cancellationToken, ctx);
        await ctx.SaveChangesAsync(cancellationToken);

        return (true, $"{weapon.Name} equipado!");
    }

    public async Task<(bool Success, string Message)> UnequipWeaponAsync(string userId, int slot, CancellationToken cancellationToken = default)
    {
        if (slot != 1 && slot != 2)
            return (false, "Slot inválido");

        var ctx = _contextFactory.CreateDbContext();
        var character = await ctx.Characters.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (character == null)
            return (false, "Personagem não encontrado");

        var weaponId = slot == 1 ? character.EquippedWeapon1 : character.EquippedWeapon2;
        if (!weaponId.HasValue)
            return (false, "Nenhuma arma equipada neste slot");

        var weapon = await ctx.ForgedWeapons.FirstOrDefaultAsync(w => w.Id == weaponId.Value, cancellationToken);

        // If two-handed, clear both slots
        if (weapon?.IsTwoHanded == true)
        {
            character.EquippedWeapon1 = null;
            character.EquippedWeapon2 = null;
        }
        else
        {
            if (slot == 1) character.EquippedWeapon1 = null;
            else character.EquippedWeapon2 = null;
        }

        if (weapon != null) weapon.IsEquipped = false;

        await RecalculateEquipmentBonusesAsync(character, cancellationToken, ctx);
        await ctx.SaveChangesAsync(cancellationToken);

        return (true, weapon != null ? $"{weapon.Name} desequipado!" : "Arma desequipada!");
    }

    private async Task UnequipWeaponInternal(Character character, int slot, ApplicationDbContext ctx, CancellationToken cancellationToken)
    {
        var weaponId = slot == 1 ? character.EquippedWeapon1 : character.EquippedWeapon2;
        if (!weaponId.HasValue) return;

        var weapon = await ctx.ForgedWeapons.FirstOrDefaultAsync(w => w.Id == weaponId.Value, cancellationToken);
        if (weapon != null) weapon.IsEquipped = false;

        if (slot == 1) character.EquippedWeapon1 = null;
        else character.EquippedWeapon2 = null;
    }

    /// <summary>
    /// Public entry point to recalculate all equipment bonuses for a user's character.
    /// Call this on page load to ensure bonuses reflect current formula/config.
    /// </summary>
    public async Task RecalculateEquipmentBonusesForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var ctx = _contextFactory.CreateDbContext();
        var character = await ctx.Characters
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (character == null) return;

        await RecalculateEquipmentBonusesAsync(character, cancellationToken, ctx);
        await ctx.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Recalculates all equipment stat bonuses based on currently equipped items.
    /// Each character gets unique equipment quality per slot via deterministic seeding
    /// (characterId × 7919 + slotIndex × 31), giving variety across players without DB changes.
    /// </summary>
    private async Task RecalculateEquipmentBonusesAsync(Character character, CancellationToken cancellationToken = default, ApplicationDbContext? ctx = null)
    {
        var stats = _scalingConfig.StageMode.EquipmentStats;
        var levelScale = 1.0 + character.Level * _scalingConfig.StageMode.EquipmentLevelScale;

        // Flat per-level bonuses (match stat upgrade flat bonuses so equipment levels feel equivalent)
        var hpPerLvl = _scalingConfig.StageMode.EquipmentHpPerLevel;
        var powPerLvl = _scalingConfig.StageMode.EquipmentPowerPerLevel;
        var defPerLvl = _scalingConfig.StageMode.EquipmentDefensePerLevel;

        int hp = 0, power = 0, defense = 0;

        // All 6 armor pieces are permanently equipped — always compute bonuses.
        // Quality defaults to 1.0 for characters created after the refactor.
        // Legacy characters with quality 0 fall back to 1.0.
        double GetQ(double stored) => stored > 0 ? stored : 1.0;

        // New formula: (baseStat + bonusLevel × flatPerLevel) × quality × levelScale
        void AddSlot(EquipmentPieceStats baseStats, double quality, int bonusLevel)
        {
            var q = GetQ(quality);
            hp += (int)Math.Round((baseStats.HP + bonusLevel * hpPerLvl) * q * levelScale);
            power += (int)Math.Round((baseStats.Power + bonusLevel * powPerLvl) * q * levelScale);
            defense += (int)Math.Round((baseStats.Defense + bonusLevel * defPerLvl) * q * levelScale);
        }

        AddSlot(stats.Head, character.EquippedHeadQuality, character.GetSlotBonusLevel(EquipmentSlot.Head));
        AddSlot(stats.Shoulders, character.EquippedShouldersQuality, character.GetSlotBonusLevel(EquipmentSlot.Shoulders));
        AddSlot(stats.Chest, character.EquippedChestQuality, character.GetSlotBonusLevel(EquipmentSlot.Chest));
        AddSlot(stats.Gloves, character.EquippedGlovesQuality, character.GetSlotBonusLevel(EquipmentSlot.Gloves));
        AddSlot(stats.Legs, character.EquippedLegsQuality, character.GetSlotBonusLevel(EquipmentSlot.Legs));
        AddSlot(stats.Boots, character.EquippedBootsQuality, character.GetSlotBonusLevel(EquipmentSlot.Boots));

        // Add weapon bonuses from forged weapons (with character level scaling)
        var weaponLevelScale = 1.0 + character.Level * _scalingConfig.StageMode.WeaponCharacterLevelScale;

        var equippedWeaponIds = new HashSet<int>();
        if (character.EquippedWeapon1.HasValue) equippedWeaponIds.Add(character.EquippedWeapon1.Value);
        if (character.EquippedWeapon2.HasValue) equippedWeaponIds.Add(character.EquippedWeapon2.Value);

        int speed = 0;
        double critChance = 0;

        if (equippedWeaponIds.Count > 0)
        {
            var weaponCtx = ctx ?? _contextFactory.CreateDbContext();
            // When caller provides a context, use tracking to see in-memory changes (e.g. after upgrade);
            // otherwise use AsNoTracking for standalone reads.
            var weaponQuery = weaponCtx.ForgedWeapons.Where(w => equippedWeaponIds.Contains(w.Id));
            var weapons = ctx != null
                ? await weaponQuery.ToListAsync(cancellationToken)
                : await weaponQuery.AsNoTracking().ToListAsync(cancellationToken);

            foreach (var w in weapons)
            {
                hp += (int)Math.Round(w.BonusHP * weaponLevelScale);
                power += (int)Math.Round(w.BonusPower * weaponLevelScale);
                defense += (int)Math.Round(w.BonusDefense * weaponLevelScale);
                speed += w.BonusSpeed;
                critChance += w.BonusCriticalChance;
            }
        }

        character.EquipmentHPBonus = hp;
        character.EquipmentPowerBonus = power;
        character.EquipmentDefenseBonus = defense;
        character.EquipmentSpeedBonus = speed;
        character.EquipmentCriticalBonus = critChance;
    }

    public decimal GetWeaponUpgradeCost(int currentLevel)
    {
        var forging = _scalingConfig.StageMode.Forging;
        if (forging.WeaponUpgradeCostScale > 0)
            return Math.Round(forging.WeaponUpgradeBaseCost + (decimal)((long)currentLevel * currentLevel) * forging.WeaponUpgradeCostScale, 2);
        return forging.WeaponUpgradeBaseCost + currentLevel * forging.WeaponUpgradeCostPerLevel;
    }

    /// <summary>
    /// Ordered list of drink types from lowest to highest tier, matching gathering resource order.
    /// </summary>
    private static readonly InventoryItemType[] DrinkTierOrder = new[]
    {
        InventoryItemType.Cerveja, InventoryItemType.Vinho, InventoryItemType.Licor,
        InventoryItemType.Rum, InventoryItemType.Tequilla, InventoryItemType.Vodka,
        InventoryItemType.Gin, InventoryItemType.Whisky, InventoryItemType.Absinto,
        InventoryItemType.Aguardente
    };

    /// <summary>
    /// Number of free levels before any drink is required for weapon/equipment upgrades.
    /// </summary>
    private const int DrinkFreeLevels = 50;

    /// <summary>
    /// Number of upgrade levels per drink tier (e.g. 10 levels = one drink type).
    /// </summary>
    private const int DrinkLevelsPerTier = 50;

    /// <summary>
    /// Maximum drink quantity within a tier (scales from 1 to this value).
    /// </summary>
    private const int DrinkMaxQtyPerTier = 25;

    /// <summary>
    /// Core algorithm for the new drink requirement system.
    /// Each tier requires only ONE drink type (not cumulative). Piggies (Leitão) start
    /// from a configurable threshold, crescendo starting at 1 and increasing every few levels.
    /// <para>
    /// Level 1-5: no drinks. 6-15: Cerveja 1→5. 16-25: Vinho 1→5.
    /// 26-35: Licor 1→5. … 96+: Aguardente cycling + piggies.
    /// </para>
    /// </summary>
    private static (List<(InventoryItemType DrinkType, int Quantity)> Drinks, int LeitaoCost) CalculateDrinkTierRequirements(int currentLevel)
    {
        const int PiggyStartLevel = 500;
        const int PiggyStepSize = 125;

        var drinks = new List<(InventoryItemType DrinkType, int Quantity)>();

        if (currentLevel < DrinkFreeLevels)
            return (drinks, 0);

        var effectiveLevel = currentLevel - DrinkFreeLevels;
        var tierIndex = effectiveLevel / DrinkLevelsPerTier;

        // Drink type: cap at Aguardente (last tier), then Aguardente continues cycling
        var drinkIndex = Math.Min(tierIndex, DrinkTierOrder.Length - 1);

        // Drink quantity scales 1→50 across the 100 levels of this tier
        var levelInTier = effectiveLevel % DrinkLevelsPerTier;
        var drinkQty = Math.Min(DrinkMaxQtyPerTier, levelInTier / 2 + 1);
        drinks.Add((DrinkTierOrder[drinkIndex], drinkQty));

        // Piggies: 0 for levels 1-1000, then crescendo +1 every 250 levels starting at 1001.
        // 1001-1250→1, 1251-1500→2, 1501-1750→3, 1751-2000→4, …
        var leitaoCost = currentLevel < PiggyStartLevel
            ? 0
            : (currentLevel - PiggyStartLevel) / PiggyStepSize + 1;

        return (drinks, leitaoCost);
    }

    /// <summary>
    /// Calculates drink requirements for a WEAPON upgrade at a given level.
    /// Returns only the single drink type for the current tier (not cumulative).
    /// </summary>
    public List<(InventoryItemType DrinkType, int Quantity)> GetUpgradeDrinkRequirement(int currentLevel)
    {
        return CalculateDrinkTierRequirements(currentLevel).Drinks;
    }

    /// <summary>
    /// Calculates the Leitão cost for a WEAPON upgrade at a given level.
    /// Leitão is required starting from the Licor tier (level 301+).
    /// </summary>
    public int GetUpgradeLeitaoCost(int currentLevel)
    {
        return CalculateDrinkTierRequirements(currentLevel).LeitaoCost;
    }

    /// <inheritdoc />
    public InventoryItemType? GetNextDrinkTier(InventoryItemType currentDrink)
    {
        var index = Array.IndexOf(DrinkTierOrder, currentDrink);
        if (index < 0 || index >= DrinkTierOrder.Length - 1)
            return null;
        return DrinkTierOrder[index + 1];
    }

    /// <inheritdoc />
    public decimal GetDrinkUpgradeCost(int currentWeaponLevel)
    {
        if (currentWeaponLevel <= 0) return 0m;

        // Half the total Fidelis cost from level 0 → current level.
        var forging = _scalingConfig.StageMode.Forging;
        var L = (decimal)currentWeaponLevel;

        decimal totalCost;
        if (forging.WeaponUpgradeCostScale > 0)
        {
            // Quadratic: Σ (baseCost + i² × costScale) for i=0..L-1
            // = L × baseCost + costScale × L×(L-1)×(2L-1)/6
            var n = (long)currentWeaponLevel;
            var sumOfSquares = (decimal)(n * (n - 1) * (2 * n - 1)) / 6m;
            totalCost = L * forging.WeaponUpgradeBaseCost + forging.WeaponUpgradeCostScale * sumOfSquares;
        }
        else
        {
            // Linear: Σ (baseCost + i × costPerLevel) for i=0..L-1
            totalCost = L * forging.WeaponUpgradeBaseCost + forging.WeaponUpgradeCostPerLevel * L * (L - 1) / 2m;
        }

        return Math.Round(totalCost / 2m, 2);
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> UpgradeWeaponDrinkAsync(
        string userId, int weaponId, CancellationToken cancellationToken = default)
    {
        var ctx = _contextFactory.CreateDbContext();
        var weapon = await ctx.ForgedWeapons.FirstOrDefaultAsync(
            w => w.Id == weaponId && w.UserId == userId, cancellationToken);
        if (weapon == null)
            return (false, "Arma não encontrada");

        var nextDrink = GetNextDrinkTier(weapon.SourceDrink);
        if (nextDrink == null)
            return (false, "A arma já está no tier máximo de bebida.");

        // Validate player has unlocked the next drink tier (stage requirement)
        var nextDrinkResource = _scalingConfig.Gathering.Resources
            .FirstOrDefault(r => r.Type == nextDrink.Value.ToString());
        if (nextDrinkResource == null)
            return (false, "Bebida seguinte não encontrada na configuração.");

        var stageProgress = await ctx.StageProgresses
            .AsNoTracking()
            .FirstOrDefaultAsync(sp => sp.UserId == userId, cancellationToken);
        var highestStage = stageProgress?.HighestStage ?? 0;
        if (highestStage < nextDrinkResource.UnlockStage)
            return (false, $"Precisas de atingir o stage {nextDrinkResource.UnlockStage} para desbloquear {nextDrinkResource.Name}.");

        var cost = GetDrinkUpgradeCost(weapon.Level);

        var user = await ctx.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null || user.FidelisBalance < cost)
            return (false, $"Fidelis insuficiente (necessário: {cost:N0})");

        var character = await ctx.Characters.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (character == null)
            return (false, "Personagem não encontrado");

        var transaction = await ctx.Database.BeginTransactionIfSupportedAsync(cancellationToken);

        user.FidelisBalance -= cost;
        weapon.SourceDrink = nextDrink.Value;

        // Recalculate stats with the new drink tier multiplier
        var hpPerLvl = _scalingConfig.StageMode.EquipmentHpPerLevel;
        var powPerLvl = _scalingConfig.StageMode.EquipmentPowerPerLevel;
        var defPerLvl = _scalingConfig.StageMode.EquipmentDefensePerLevel;
        var forging = _scalingConfig.StageMode.Forging;
        var baseStats = _scalingConfig.StageMode.EquipmentStats.Instrument;

        var drinkEnergyCost = nextDrinkResource.EnergyCost;
        var drinkTierMult = 1.0 + (drinkEnergyCost - 1) * forging.DrinkStatBonusPerTier;
        var handedMult = weapon.IsTwoHanded ? forging.TwoHandedMultiplier : 1.0;
        var scaleMult = drinkTierMult * handedMult;

        weapon.BonusHP = (int)Math.Round((baseStats.HP + weapon.Level * hpPerLvl) * scaleMult);
        weapon.BonusPower = (int)Math.Round((baseStats.Power + weapon.Level * powPerLvl) * scaleMult);
        weapon.BonusDefense = (int)Math.Round((baseStats.Defense + weapon.Level * defPerLvl) * scaleMult);

        // Recalculate equipment bonuses if weapon is equipped
        if (weapon.IsEquipped)
        {
            await RecalculateEquipmentBonusesAsync(character, cancellationToken, ctx);
        }

        await ctx.SaveChangesAsync(cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);

        var currentDrinkName = _scalingConfig.Gathering.Resources
            .FirstOrDefault(r => r.Type == weapon.SourceDrink.ToString())?.Name ?? weapon.SourceDrink.ToString();
        return (true, $"Bebida da arma melhorada para {currentDrinkName}!");
    }

    /// <summary>
    /// Calculates drink requirements for an EQUIPMENT upgrade at a given level.
    /// Returns only the single drink type for the current tier (not cumulative).
    /// </summary>
    public List<(InventoryItemType DrinkType, int Quantity)> GetEquipmentUpgradeDrinkRequirements(int currentLevel)
    {
        return CalculateDrinkTierRequirements(currentLevel).Drinks;
    }

    /// <summary>
    /// Calculates the Leitão cost for an EQUIPMENT upgrade at a given level.
    /// Leitão is required starting from the Licor tier (level 301+).
    /// </summary>
    public int GetEquipmentUpgradeLeitaoCost(int currentLevel)
    {
        return CalculateDrinkTierRequirements(currentLevel).LeitaoCost;
    }

    public async Task<(bool Success, string Message)> UpgradeWeaponAsync(string userId, int weaponId, CancellationToken cancellationToken = default)
    {
        var ctx = _contextFactory.CreateDbContext();
        var weapon = await ctx.ForgedWeapons.FirstOrDefaultAsync(w => w.Id == weaponId && w.UserId == userId, cancellationToken);
        if (weapon == null)
            return (false, "Arma não encontrada");

        // Check max weapon level
        var maxLevel = _scalingConfig.StageMode.Forging.MaxWeaponLevel;
        if (maxLevel > 0 && weapon.Level >= maxLevel)
            return (false, $"Nível máximo da arma alcançado ({maxLevel}).");

        var cost = GetWeaponUpgradeCost(weapon.Level);

        var character = await ctx.Characters.FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (character == null)
            return (false, "Personagem não encontrado");

        var user = await ctx.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null || user.FidelisBalance < cost)
            return (false, $"Fidelis insuficiente (necessário: {cost:F2})");

        // Require the single drink type for the current tier
        var drinkRequirements = GetUpgradeDrinkRequirement(weapon.Level);

        // Validate all drinks are available before consuming any (fail-fast)
        foreach (var (drinkType, drinkQty) in drinkRequirements)
        {
            var drinkItem = await _inventoryRepository.GetItemAsync(userId, drinkType, cancellationToken);
            var drinkRes = _scalingConfig.Gathering.Resources.FirstOrDefault(r => r.Type == drinkType.ToString());
            var drinkName = drinkRes?.Name ?? drinkType.ToString();
            if (drinkItem == null || drinkItem.Quantity < drinkQty)
                return (false, $"Precisas de {drinkQty}x {drinkName} (tens {drinkItem?.Quantity ?? 0})");
        }

        // Check Leitão cost (integrated into drink tier system)
        var leitaoCost = GetUpgradeLeitaoCost(weapon.Level);

        if (leitaoCost > 0)
        {
            var leitaoItem = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Leitao, cancellationToken);
            if (leitaoItem == null || leitaoItem.Quantity < leitaoCost)
                return (false, $"Leitões insuficientes. Necessário: {leitaoCost}, Disponível: {leitaoItem?.Quantity ?? 0}");
        }

        // Atomic transaction: consume all drinks + optional Leitão + deduct Fidelis + upgrade weapon
        // in a single commit. Prevents material loss if any step fails mid-way.
        var transaction = await ctx.Database.BeginTransactionIfSupportedAsync(cancellationToken);

        // Consume all drinks via raw SQL on the same context (atomic with WHERE guard)
        foreach (var (drinkType, drinkQty) in drinkRequirements)
        {
            var rows = await ctx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE InventoryItems SET Quantity = Quantity - {drinkQty} WHERE UserId = {userId} AND Type = {(int)drinkType} AND Quantity >= {drinkQty}",
                cancellationToken);
            if (rows == 0)
                return (false, "Erro ao consumir bebida");
        }

        // Consume Leitão if required
        if (leitaoCost > 0)
        {
            var leitaoRows = await ctx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE InventoryItems SET Quantity = Quantity - {leitaoCost} WHERE UserId = {userId} AND Type = {(int)InventoryItemType.Leitao} AND Quantity >= {leitaoCost}",
                cancellationToken);
            if (leitaoRows == 0)
                return (false, "Erro ao consumir Leitões");
        }

        user.FidelisBalance -= cost;
        weapon.Level += 1;

        // Recalculate stats: flat bonus per level (matches equipment/stat upgrade model)
        var hpPerLvl = _scalingConfig.StageMode.EquipmentHpPerLevel;
        var powPerLvl = _scalingConfig.StageMode.EquipmentPowerPerLevel;
        var defPerLvl = _scalingConfig.StageMode.EquipmentDefensePerLevel;
        var forging = _scalingConfig.StageMode.Forging;
        var baseStats = _scalingConfig.StageMode.EquipmentStats.Instrument;

        // Find drink tier multiplier from the weapon's source drink
        var drinkResource = _scalingConfig.Gathering.Resources
            .FirstOrDefault(r => r.Type == weapon.SourceDrink.ToString());
        var drinkEnergyCost = drinkResource?.EnergyCost ?? 1;
        var drinkTierMult = 1.0 + (drinkEnergyCost - 1) * forging.DrinkStatBonusPerTier;

        // 2H weapons get the two-handed multiplier to match dual-wielding 1H
        var handedMult = weapon.IsTwoHanded ? forging.TwoHandedMultiplier : 1.0;

        var scaleMult = drinkTierMult * handedMult;

        weapon.BonusHP = (int)Math.Round((baseStats.HP + weapon.Level * hpPerLvl) * scaleMult);
        weapon.BonusPower = (int)Math.Round((baseStats.Power + weapon.Level * powPerLvl) * scaleMult);
        weapon.BonusDefense = (int)Math.Round((baseStats.Defense + weapon.Level * defPerLvl) * scaleMult);

        // Recalculate equipment bonuses if weapon is equipped
        if (weapon.IsEquipped)
        {
            await RecalculateEquipmentBonusesAsync(character, cancellationToken, ctx);
        }

        await ctx.SaveChangesAsync(cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);

        return (true, $"Arma melhorada para +{weapon.Level}!");
    }

    /// <summary>
    /// Gets the enhancement level for a specific equipment slot (manual upgrades only).
    /// </summary>
    public async Task<int> GetSlotEnhancementLevelAsync(string userId, EquipmentSlot slot, CancellationToken cancellationToken = default)
    {
        var ctx = _contextFactory.CreateDbContext();
        var character = await ctx.Characters
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        return character?.GetSlotBonusLevel(slot) ?? 0;
    }

    /// <summary>
    /// Gets the Fidelis cost to upgrade equipment enhancement to the next level.
    /// Uses quadratic formula when CostScale > 0: baseCost + n² × costScale.
    /// Otherwise linear: baseCost + n × costPerLevel.
    /// </summary>
    public decimal GetEquipmentUpgradeCost(int currentBonusLevel)
    {
        var forging = _scalingConfig.StageMode.Forging;
        if (forging.EquipmentUpgradeCostScale > 0)
            return Math.Round(forging.EquipmentUpgradeBaseCost + (decimal)((long)currentBonusLevel * currentBonusLevel) * forging.EquipmentUpgradeCostScale, 2);
        return Math.Round(forging.EquipmentUpgradeBaseCost + currentBonusLevel * forging.EquipmentUpgradeCostPerLevel, 2);
    }

    /// <summary>
    /// Upgrades a specific equipment slot's enhancement level by 1. Costs Fidelis.
    /// The slot bonus level is stored on the Character entity.
    /// </summary>
    public async Task<(bool Success, string Message)> UpgradeEquipmentSlotAsync(string userId, EquipmentSlot slot, CancellationToken cancellationToken = default)
    {
        var ctx = _contextFactory.CreateDbContext();
        var character = await ctx.Characters
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (character == null)
            return (false, "Personagem não encontrado");

        var currentSlotLevel = character.GetSlotBonusLevel(slot);

        // Check max equipment enhancement level
        var maxEnhancement = _scalingConfig.StageMode.MaxEquipmentEnhancement;
        if (maxEnhancement > 0 && currentSlotLevel >= maxEnhancement)
            return (false, $"Nível máximo de equipamento alcançado ({maxEnhancement}).");

        var cost = GetEquipmentUpgradeCost(currentSlotLevel);

        var user = await ctx.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null || user.FidelisBalance < cost)
            return (false, $"Fidelis insuficiente (necessário: {cost:F2})");

        // Require the single drink type for the current tier
        var drinkRequirements = GetEquipmentUpgradeDrinkRequirements(currentSlotLevel);

        // Validate all drinks are available before consuming any (fail-fast)
        foreach (var (drinkType, drinkQty) in drinkRequirements)
        {
            var drinkItem = await _inventoryRepository.GetItemAsync(userId, drinkType, cancellationToken);
            var drinkRes = _scalingConfig.Gathering.Resources.FirstOrDefault(r => r.Type == drinkType.ToString());
            var drinkName = drinkRes?.Name ?? drinkType.ToString();
            if (drinkItem == null || drinkItem.Quantity < drinkQty)
                return (false, $"Precisas de {drinkQty}x {drinkName} (tens {drinkItem?.Quantity ?? 0})");
        }

        // Check Leitão cost (integrated into drink tier system)
        var leitaoCost = GetEquipmentUpgradeLeitaoCost(currentSlotLevel);

        if (leitaoCost > 0)
        {
            var leitaoItem = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Leitao, cancellationToken);
            if (leitaoItem == null || leitaoItem.Quantity < leitaoCost)
                return (false, $"Leitões insuficientes. Necessário: {leitaoCost}, Disponível: {leitaoItem?.Quantity ?? 0}");
        }

        // Atomic transaction: consume all drinks + optional Leitão + deduct Fidelis + upgrade slot
        // in a single commit. Prevents material loss if any step fails mid-way.
        var transaction = await ctx.Database.BeginTransactionIfSupportedAsync(cancellationToken);

        // Consume all drinks via raw SQL on the same context (atomic with WHERE guard)
        foreach (var (drinkType, drinkQty) in drinkRequirements)
        {
            var rows = await ctx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE InventoryItems SET Quantity = Quantity - {drinkQty} WHERE UserId = {userId} AND Type = {(int)drinkType} AND Quantity >= {drinkQty}",
                cancellationToken);
            if (rows == 0)
                return (false, "Erro ao consumir bebida");
        }

        // Consume Leitão if required
        if (leitaoCost > 0)
        {
            var leitaoRows = await ctx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE InventoryItems SET Quantity = Quantity - {leitaoCost} WHERE UserId = {userId} AND Type = {(int)InventoryItemType.Leitao} AND Quantity >= {leitaoCost}",
                cancellationToken);
            if (leitaoRows == 0)
                return (false, "Erro ao consumir Leitões");
        }

        user.FidelisBalance -= cost;
        character.SetSlotBonusLevel(slot, currentSlotLevel + 1);

        // Recalculate equipment bonuses with the new per-slot enhancement level
        await RecalculateEquipmentBonusesAsync(character, cancellationToken, ctx);

        await ctx.SaveChangesAsync(cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);

        var newLevel = currentSlotLevel + 1;
        var slotName = slot.ToString().ToUpperInvariant();
        return (true, $"{slotName} melhorado para +{newLevel}!");
    }

    /// <summary>
    /// Calculates the Fidelis value for discarding a forged weapon.
    /// Formula: weaponDiscardBase × (1 + weaponLevel × 0.5) × drinkCostMultiplier × (1 + charLevel × discardLevelScale)
    /// </summary>
    public decimal GetWeaponDiscardValue(ForgedWeapon weapon, int characterLevel)
    {
        var discardBase = _scalingConfig.StageMode.DiscardValues.Weapon;
        var discardLevelScale = _scalingConfig.StageMode.DiscardLevelScale;

        // Scale with weapon enhancement level
        var weaponLevelMult = 1.0 + weapon.Level * 0.5;

        // Scale with drink rarity (higher tier drinks = more valuable weapons)
        var drinkResource = _scalingConfig.Gathering.Resources
            .FirstOrDefault(r => r.Type == weapon.SourceDrink.ToString());
        var drinkCostMultiplier = drinkResource?.EnergyCost ?? 1;

        // Scale with character level
        var charLevelMult = 1.0 + characterLevel * (double)discardLevelScale;

        return Math.Round(discardBase * (decimal)(weaponLevelMult * drinkCostMultiplier * charLevelMult), 2);
    }

    /// <summary>
    /// Discards a forged weapon in exchange for Fidelis currency.
    /// Unequips the weapon first if it is currently equipped.
    /// </summary>
    public async Task<(bool Success, decimal FidelisGained, string Message)> DiscardWeaponAsync(string userId, int weaponId, CancellationToken cancellationToken = default)
    {
        var ctx = _contextFactory.CreateDbContext();
        var weapon = await ctx.ForgedWeapons
            .FirstOrDefaultAsync(w => w.Id == weaponId && w.UserId == userId, cancellationToken);

        if (weapon == null)
            return (false, 0, "Arma não encontrada");

        var character = await ctx.Characters
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        // Unequip weapon if currently equipped
        if (weapon.IsEquipped && character != null)
        {
            if (character.EquippedWeapon1 == weaponId)
                character.EquippedWeapon1 = null;
            if (character.EquippedWeapon2 == weaponId)
                character.EquippedWeapon2 = null;
        }

        // Calculate discard value
        var charLevel = character?.Level ?? 1;
        var fidelisValue = GetWeaponDiscardValue(weapon, charLevel);

        var weaponName = weapon.Name;

        // Remove weapon from database
        ctx.ForgedWeapons.Remove(weapon);

        // Credit Fidelis to user
        var user = await ctx.Users.FindAsync(new object[] { userId }, cancellationToken);
        if (user != null)
        {
            user.FidelisBalance += fidelisValue;
        }

        // Recalculate equipment bonuses if weapon was equipped
        if (weapon.IsEquipped && character != null)
        {
            await RecalculateEquipmentBonusesAsync(character, cancellationToken, ctx);
        }

        await ctx.SaveChangesAsync(cancellationToken);

        return (true, fidelisValue, $"{weaponName} descartada por {fidelisValue:F2} Fidelis!");
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> RenameWeaponAsync(string userId, int weaponId, string newName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newName) || newName.Length > 100)
            return (false, "Nome da arma inválido (máx 100 caracteres)");

        var ctx = _contextFactory.CreateDbContext();
        var weapon = await ctx.ForgedWeapons
            .FirstOrDefaultAsync(w => w.Id == weaponId && w.UserId == userId, cancellationToken);

        if (weapon == null)
            return (false, "Arma não encontrada");

        weapon.Rename(newName);
        await ctx.SaveChangesAsync(cancellationToken);

        return (true, "Arma renomeada com sucesso!");
    }

    /// <inheritdoc />
    public async Task<(bool Success, string Message)> ApplyRareSetUpgradeAsync(string userId, InventoryItemType rareItemType, CancellationToken cancellationToken = default)
    {
        // Validate the item is a rare set piece
        var slot = EquipmentDropHelper.FromRareInventoryItemType(rareItemType);
        if (slot == null)
            return (false, "Item inválido — não é uma peça de conjunto raro.");

        var ctx = _contextFactory.CreateDbContext();

        // Check player has the item in inventory
        var inventoryItem = await ctx.InventoryItems
            .FirstOrDefaultAsync(i => i.UserId == userId && i.Type == rareItemType, cancellationToken);
        if (inventoryItem == null || inventoryItem.Quantity < 1)
            return (false, "Não tens esta peça rara no inventário.");

        // Check the character doesn't already have this slot applied
        var character = await ctx.Characters
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (character == null)
            return (false, "Personagem não encontrado.");

        if (character.IsRareSetSlotApplied(slot.Value))
            return (false, "Esta peça rara já foi aplicada.");

        // Consume the item from inventory
        inventoryItem.ConsumeQuantity(1);

        // Apply the rare set upgrade
        character.ApplyRareSetSlot(slot.Value);

        await ctx.SaveChangesAsync(cancellationToken);

        var slotName = EquipmentDropHelper.GetDisplayName(slot.Value);
        return (true, $"Peça rara {slotName} aplicada com sucesso!");
    }
}
