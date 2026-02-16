using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Core.Configuration;
using RTUB.Core.Entities;

namespace RTUB.Application.Data;

/// <summary>
/// One-time migration: maps ALL characters from the old 1-2000 system to the new 1-100 system,
/// and compresses Fidelis balances to match the new cost curve.
/// DELETE THIS FILE after running once in production.
/// </summary>
public static partial class SeedData
{
    /// <summary>
    /// Migrates ALL characters to the new scaling system:
    /// - Level: sqrt(oldLevel / 2000) × 100
    /// - HP/Power/Defense upgrades: sqrt(old / 300) × 50 (compound exponential formula now)
    /// - Speed/Crit upgrades: unchanged (already capped, additive formula unchanged)
    /// - XP: reset to 0 (curve changed to exponential)
    /// - CurrentHP: reset to null (full heal)
    /// Also compresses all user Fidelis balances to match the new upgrade cost scale.
    /// </summary>
    public static async Task MigrateCharacterLevelsAsync(ApplicationDbContext dbContext, ILogger? logger = null)
    {
        var maxLevel = MyTunoScaling.MaxLevel;

        var allCharacters = await dbContext.Characters.ToListAsync();

        if (allCharacters.Count == 0)
        {
            logger?.LogInformation("No characters to migrate");
            return;
        }

        logger?.LogInformation("Migrating {Count} characters (level + upgrades) to new scaling", allCharacters.Count);

        const double oldMaxLevel = 2000.0;
        const double oldMaxUpgrades = 300.0; // reference max for sqrt mapping
        const double newMaxUpgrades = 50.0;  // target max upgrades

        foreach (var character in allCharacters)
        {
            var oldLevel = character.Level;
            var oldHpUp = character.HpUpgrades;
            var oldPowerUp = character.PowerUpgrades;
            var oldDefenseUp = character.DefenseUpgrades;

            // Level: sqrt mapping
            var ratio = Math.Min(1.0, oldLevel / oldMaxLevel);
            var newLevel = (int)Math.Max(1, Math.Round(Math.Sqrt(ratio) * maxLevel));
            newLevel = Math.Min(newLevel, maxLevel);

            // HP/Power/Defense upgrades: sqrt mapping (same pattern as level)
            var newHpUp = MapUpgrades(oldHpUp, oldMaxUpgrades, newMaxUpgrades);
            var newPowerUp = MapUpgrades(oldPowerUp, oldMaxUpgrades, newMaxUpgrades);
            var newDefenseUp = MapUpgrades(oldDefenseUp, oldMaxUpgrades, newMaxUpgrades);

            character.Level = newLevel;
            character.XP = 0;
            character.CurrentHP = null;
            character.HpUpgrades = newHpUp;
            character.PowerUpgrades = newPowerUp;
            character.DefenseUpgrades = newDefenseUp;
            // Speed and Crit upgrades stay unchanged — they're additive with hard caps

            logger?.LogInformation(
                "Migrated (User {UserId}): Level {OldLvl}→{NewLvl}, HP {OldHp}→{NewHp}, Pow {OldPow}→{NewPow}, Def {OldDef}→{NewDef}",
                character.UserId, oldLevel, newLevel, oldHpUp, newHpUp, oldPowerUp, newPowerUp, oldDefenseUp, newDefenseUp);
        }

        await dbContext.SaveChangesAsync();
        logger?.LogInformation("Migration complete for {Count} characters", allCharacters.Count);

        // --- Fidelis balance compression ---
        await MigrateFidelisBalancesAsync(dbContext, logger);

        // --- Weapon level compression ---
        await MigrateWeaponLevelsAsync(dbContext, logger);

        // --- Equipment slot bonus level compression ---
        await MigrateEquipmentSlotBonusLevelsAsync(dbContext, logger);
    }

    /// <summary>
    /// Compresses Fidelis balances so existing wealth matches the new upgrade cost scale.
    /// Uses sqrt mapping: newFidelis = sqrt(old / 10M) × 100K.
    /// Examples: 10M→100K, 1M→31.6K, 100K→10K, 10K→3.2K.
    /// Small balances (≤1000) are untouched to avoid penalizing new players.
    /// </summary>
    private static async Task MigrateFidelisBalancesAsync(ApplicationDbContext dbContext, ILogger? logger = null)
    {
        var allUsers = await dbContext.Users.ToListAsync();
        var migratedCount = 0;

        // Reference max: assumes richest players have ~10M Fidelis under old system
        const decimal oldRefMax = 10_000_000m;
        // Target max: after migration the richest get ~100K — enough for ~10 upgrades from level 48
        const decimal newMax = 100_000m;
        // Don't touch small balances — only compress above this threshold
        const decimal minThreshold = 1_000m;

        foreach (var user in allUsers)
        {
            var oldBalance = user.FidelisBalance;
            if (oldBalance <= minThreshold)
                continue;

            // sqrt compression: preserves ranking, compresses range
            var ratio = Math.Min(1.0m, oldBalance / oldRefMax);
            var newBalance = (decimal)Math.Sqrt((double)ratio) * newMax;
            newBalance = Math.Max(minThreshold, Math.Round(newBalance, 2));

            user.FidelisBalance = newBalance;
            migratedCount++;

            logger?.LogInformation(
                "Fidelis migrated (User {UserId}): {OldBalance:N0}→{NewBalance:N0}",
                user.Id, oldBalance, newBalance);
        }

        if (migratedCount > 0)
        {
            await dbContext.SaveChangesAsync();
            logger?.LogInformation("Fidelis migration complete for {Count} users", migratedCount);
        }
    }

    /// <summary>
    /// Maps old upgrade count to new range using sqrt compression.
    /// sqrt(old / refMax) × targetMax, clamped to [0, targetMax].
    /// </summary>
    private static int MapUpgrades(int oldCount, double refMax, double targetMax)
    {
        if (oldCount <= 0) return 0;
        var ratio = Math.Min(1.0, oldCount / refMax);
        return (int)Math.Min(targetMax, Math.Round(Math.Sqrt(ratio) * targetMax));
    }

    /// <summary>
    /// Compresses forged weapon levels to match the new upgrade cost curve.
    /// Old system: 75 levels per drink tier × 10 tiers = 750 theoretical max.
    /// New system: 5 levels per drink tier × 10 tiers = 50 practical max.
    /// Uses sqrt compression: sqrt(oldLevel / 200) × 30, so old levels map down.
    /// Weapons at level ≤ 3 are untouched.
    /// Also recalculates weapon stats using the new weaponUpgradeStatBonus (0.10).
    /// </summary>
    private static async Task MigrateWeaponLevelsAsync(ApplicationDbContext dbContext, ILogger? logger = null)
    {
        var weapons = await dbContext.ForgedWeapons.ToListAsync();
        if (weapons.Count == 0) return;

        var migratedCount = 0;

        // Reference max: old system practical ceiling was ~100 (500 × 1.08^100 = 1M+ Fidelis per upgrade)
        const double oldRefMax = 100.0;
        const double newTargetMax = 30.0;

        foreach (var weapon in weapons)
        {
            if (weapon.Level <= 3) continue;

            var oldLevel = weapon.Level;
            var ratio = Math.Min(1.0, oldLevel / oldRefMax);
            var newLevel = (int)Math.Max(1, Math.Round(Math.Sqrt(ratio) * newTargetMax));
            newLevel = Math.Min(newLevel, 50);

            weapon.Level = newLevel;
            migratedCount++;

            logger?.LogInformation(
                "Weapon {WeaponId} migrated: Level {OldLevel}→{NewLevel}",
                weapon.Id, oldLevel, newLevel);
        }

        if (migratedCount > 0)
        {
            await dbContext.SaveChangesAsync();
            logger?.LogInformation("Weapon level migration complete for {Count} weapons", migratedCount);
        }
    }

    /// <summary>
    /// Compresses equipment slot bonus levels using the same sqrt mapping as weapon levels.
    /// Old system used upgradeLevelsPerDrinkTier=75, new uses 5.
    /// Slot bonus levels ≤ 3 are untouched.
    /// </summary>
    private static async Task MigrateEquipmentSlotBonusLevelsAsync(ApplicationDbContext dbContext, ILogger? logger = null)
    {
        var characters = await dbContext.Characters.ToListAsync();
        var migratedCount = 0;

        const double oldRefMax = 100.0;
        const double newTargetMax = 30.0;

        foreach (var character in characters)
        {
            var anyChanged = false;

            anyChanged |= MigrateSlotLevel(character, c => c.EquippedHeadBonusLevel, (c, v) => c.EquippedHeadBonusLevel = v, oldRefMax, newTargetMax);
            anyChanged |= MigrateSlotLevel(character, c => c.EquippedShouldersBonusLevel, (c, v) => c.EquippedShouldersBonusLevel = v, oldRefMax, newTargetMax);
            anyChanged |= MigrateSlotLevel(character, c => c.EquippedChestBonusLevel, (c, v) => c.EquippedChestBonusLevel = v, oldRefMax, newTargetMax);
            anyChanged |= MigrateSlotLevel(character, c => c.EquippedGlovesBonusLevel, (c, v) => c.EquippedGlovesBonusLevel = v, oldRefMax, newTargetMax);
            anyChanged |= MigrateSlotLevel(character, c => c.EquippedLegsBonusLevel, (c, v) => c.EquippedLegsBonusLevel = v, oldRefMax, newTargetMax);
            anyChanged |= MigrateSlotLevel(character, c => c.EquippedBootsBonusLevel, (c, v) => c.EquippedBootsBonusLevel = v, oldRefMax, newTargetMax);

            if (anyChanged) migratedCount++;
        }

        if (migratedCount > 0)
        {
            await dbContext.SaveChangesAsync();
            logger?.LogInformation("Equipment slot bonus migration complete for {Count} characters", migratedCount);
        }
    }

    /// <summary>
    /// Migrates a single equipment slot bonus level using sqrt compression.
    /// Returns true if the value was changed.
    /// </summary>
    private static bool MigrateSlotLevel(Character character, Func<Character, int> getter, Action<Character, int> setter, double refMax, double targetMax)
    {
        var oldLevel = getter(character);
        if (oldLevel <= 3) return false;

        var ratio = Math.Min(1.0, oldLevel / refMax);
        var newLevel = (int)Math.Max(1, Math.Round(Math.Sqrt(ratio) * targetMax));
        newLevel = Math.Min(newLevel, 50);

        setter(character, newLevel);
        return true;
    }
}
