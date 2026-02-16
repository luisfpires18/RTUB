-- ============================================================================
-- RTUB Balance Fix Script
-- ============================================================================
-- Run against: src/RTUB.Web/app.db (SQLite)
-- Command: sqlite3 "src/RTUB.Web/app.db" < scripts/balance-fix.sql
--
-- This script fixes multiple balance issues caused by stale cached values
-- from previous scaling configurations, exploit-like weapon equip bugs,
-- and missing upgrade caps.
--
-- MAKE A BACKUP FIRST:
--   copy src\RTUB.Web\app.db src\RTUB.Web\app.db.backup
-- ============================================================================

-- ── Step 1: Fix base Defense for ALL characters ──────────────────────────────
-- All characters have Defense=0 from the original migration. The code uses
-- EffectiveDefense fallback to BaseDefense=5, but the DB value should match.
UPDATE Characters SET Defense = 5 WHERE Defense = 0;

-- ── Step 2: Fix weapon equip exploits ────────────────────────────────────────

-- PREPUCIO (Id=7): 2H weapon (Id=40) equipped in BOTH slots — double bonus.
-- A 2H weapon should only occupy slot 1.
UPDATE Characters SET EquippedWeapon2 = NULL WHERE Id = 7 AND EquippedWeapon2 = EquippedWeapon1;

-- MIJA (Id=43): EquippedWeapon2=10 references a weapon that doesn't exist
-- in the ForgedWeapons table (deleted or never created for this user).
UPDATE Characters SET EquippedWeapon2 = NULL
WHERE Id = 43 AND EquippedWeapon2 IS NOT NULL
  AND EquippedWeapon2 NOT IN (SELECT Id FROM ForgedWeapons WHERE UserId = (SELECT UserId FROM Characters WHERE Id = 43));

-- ── Step 3: Cap stat upgrades ────────────────────────────────────────────────
-- HP at 8% per upgrade compounds too aggressively without a cap:
--   40 upgrades → 1.08^40 = 21.7× (strong but fair)
--   50 upgrades → 1.08^50 = 46.9× (game-breaking)
--   63 upgrades → 1.08^63 = 132× (absurd — jeans)
-- Power/Defense at 4% are linear-ish but still need bounds:
--   50 upgrades → 1.04^50 = 7.1× (solid ceiling)
--   72 upgrades → 1.04^72 = 17× (too much — jeans)

-- HP: cap at 40 (affects: mija 50→40, jeans 63→40, prepucio 46→40, ambrosio 41→40)
UPDATE Characters SET HpUpgrades = 40 WHERE HpUpgrades > 40;

-- Power: cap at 50 (affects: mija 50→50 ok, jeans 72→50)
UPDATE Characters SET PowerUpgrades = 50 WHERE PowerUpgrades > 50;

-- Defense: cap at 40 (affects: mija 50→40, jeans 47→40, ambrosio 44→40)
UPDATE Characters SET DefenseUpgrades = 40 WHERE DefenseUpgrades > 40;

-- ── Step 4: Cap equipment slot bonus levels at 15 ────────────────────────────
-- MIJA has slot bonus levels of 22-26 per slot, giving enhancement multipliers
-- up to 8.8×. Capping at 15 limits the purchased bonus while keeping the
-- stage-derived bonus (HighestStage/100) uncapped as progression reward.
-- At stage 1381 with cap 15: m = 1 + (13+15)×0.20 = 6.6× — still very strong.
UPDATE Characters SET EquippedHeadBonusLevel = 15 WHERE EquippedHeadBonusLevel > 15;
UPDATE Characters SET EquippedShouldersBonusLevel = 15 WHERE EquippedShouldersBonusLevel > 15;
UPDATE Characters SET EquippedChestBonusLevel = 15 WHERE EquippedChestBonusLevel > 15;
UPDATE Characters SET EquippedGlovesBonusLevel = 15 WHERE EquippedGlovesBonusLevel > 15;
UPDATE Characters SET EquippedLegsBonusLevel = 15 WHERE EquippedLegsBonusLevel > 15;
UPDATE Characters SET EquippedBootsBonusLevel = 15 WHERE EquippedBootsBonusLevel > 15;

-- ── Step 5: Recalculate ALL weapon stats (Level >= 1) ────────────────────────
-- Weapon stats were cached from a previous config with ~2× higher instrument
-- base power. The upgrade formula is deterministic:
--   bonusStat = round(baseStat × energyCost × (1 + level×0.10) × handedMult)
-- Current config: instrument = { hp: 8, power: 12, defense: 5 }
-- Energy cost mapping: SourceDrink enum - 2 = energyCost
--   (Cerveja=3→1, Vinho=4→2, ..., Aguardente=12→10)
-- 2H weapons: handedMult = 2.0, 1H: 1.0
-- Speed and CriticalChance are forge-time only and NOT recalculated.
UPDATE ForgedWeapons SET
  BonusHP = CAST(ROUND(8 * (SourceDrink - 2) * (1.0 + Level * 0.10) * CASE WHEN IsTwoHanded = 1 THEN 2.0 ELSE 1.0 END) AS INTEGER),
  BonusPower = CAST(ROUND(12 * (SourceDrink - 2) * (1.0 + Level * 0.10) * CASE WHEN IsTwoHanded = 1 THEN 2.0 ELSE 1.0 END) AS INTEGER),
  BonusDefense = CAST(ROUND(5 * (SourceDrink - 2) * (1.0 + Level * 0.10) * CASE WHEN IsTwoHanded = 1 THEN 2.0 ELSE 1.0 END) AS INTEGER)
WHERE Level >= 1;

-- ── Step 6: Recalculate ALL equipment bonuses ────────────────────────────────
-- Equipment bonuses were cached from an old config and are 15-57× inflated for
-- most active players. Examples of stored vs correct (current config):
--   MIJA:  HP 713,430 → ~10,000 (57× inflated!)
--   PREPUCIO: HP 95,996 → ~4,500 (21× inflated!)
--   JEANS: HP 6,975 → ~6,000 (≈correct, recently re-equipped)
--
-- Formula per armor slot:
--   bonus = round(baseStat × quality × levelScale × enhMult)
--   quality = stored value if > 0, else 1.0 (average)
--   levelScale = 1 + characterLevel × 0.05
--   enhMult = 1 + (stageDerived + slotBonusLevel) × 0.20
--   stageDerived = HighestStage / 100 (integer division)
--
-- Weapon contribution:
--   bonus = round(weaponStat × (1 + characterLevel × 0.02))
--
-- Equipment base stats from current config:
--   Head:      hp=60,  power=10, defense=15
--   Shoulders: hp=45,  power=5,  defense=20
--   Chest:     hp=100, power=15, defense=35
--   Gloves:    hp=10,  power=25, defense=3
--   Legs:      hp=60,  power=10, defense=15
--   Boots:     hp=30,  power=5,  defense=10

-- Step 6a: Reset all equipment bonuses to zero
UPDATE Characters SET
  EquipmentHPBonus = 0,
  EquipmentPowerBonus = 0,
  EquipmentDefenseBonus = 0,
  EquipmentSpeedBonus = 0,
  EquipmentCriticalBonus = 0.0;

-- Step 6b: Add Head slot bonuses
UPDATE Characters SET
  EquipmentHPBonus = EquipmentHPBonus + CAST(ROUND(
    60 * CASE WHEN EquippedHeadQuality > 0 THEN EquippedHeadQuality ELSE 1.0 END
    * (1.0 + Level * 0.05)
    * (1.0 + (COALESCE((SELECT sp.HighestStage / 100 FROM StageProgresses sp WHERE sp.UserId = Characters.UserId), 0) + EquippedHeadBonusLevel) * 0.20)
  ) AS INTEGER),
  EquipmentPowerBonus = EquipmentPowerBonus + CAST(ROUND(
    10 * CASE WHEN EquippedHeadQuality > 0 THEN EquippedHeadQuality ELSE 1.0 END
    * (1.0 + Level * 0.05)
    * (1.0 + (COALESCE((SELECT sp.HighestStage / 100 FROM StageProgresses sp WHERE sp.UserId = Characters.UserId), 0) + EquippedHeadBonusLevel) * 0.20)
  ) AS INTEGER),
  EquipmentDefenseBonus = EquipmentDefenseBonus + CAST(ROUND(
    15 * CASE WHEN EquippedHeadQuality > 0 THEN EquippedHeadQuality ELSE 1.0 END
    * (1.0 + Level * 0.05)
    * (1.0 + (COALESCE((SELECT sp.HighestStage / 100 FROM StageProgresses sp WHERE sp.UserId = Characters.UserId), 0) + EquippedHeadBonusLevel) * 0.20)
  ) AS INTEGER)
WHERE EquippedHead IS NOT NULL;

-- Step 6c: Add Shoulders slot bonuses
UPDATE Characters SET
  EquipmentHPBonus = EquipmentHPBonus + CAST(ROUND(
    45 * CASE WHEN EquippedShouldersQuality > 0 THEN EquippedShouldersQuality ELSE 1.0 END
    * (1.0 + Level * 0.05)
    * (1.0 + (COALESCE((SELECT sp.HighestStage / 100 FROM StageProgresses sp WHERE sp.UserId = Characters.UserId), 0) + EquippedShouldersBonusLevel) * 0.20)
  ) AS INTEGER),
  EquipmentPowerBonus = EquipmentPowerBonus + CAST(ROUND(
    5 * CASE WHEN EquippedShouldersQuality > 0 THEN EquippedShouldersQuality ELSE 1.0 END
    * (1.0 + Level * 0.05)
    * (1.0 + (COALESCE((SELECT sp.HighestStage / 100 FROM StageProgresses sp WHERE sp.UserId = Characters.UserId), 0) + EquippedShouldersBonusLevel) * 0.20)
  ) AS INTEGER),
  EquipmentDefenseBonus = EquipmentDefenseBonus + CAST(ROUND(
    20 * CASE WHEN EquippedShouldersQuality > 0 THEN EquippedShouldersQuality ELSE 1.0 END
    * (1.0 + Level * 0.05)
    * (1.0 + (COALESCE((SELECT sp.HighestStage / 100 FROM StageProgresses sp WHERE sp.UserId = Characters.UserId), 0) + EquippedShouldersBonusLevel) * 0.20)
  ) AS INTEGER)
WHERE EquippedShoulders IS NOT NULL;

-- Step 6d: Add Chest slot bonuses
UPDATE Characters SET
  EquipmentHPBonus = EquipmentHPBonus + CAST(ROUND(
    100 * CASE WHEN EquippedChestQuality > 0 THEN EquippedChestQuality ELSE 1.0 END
    * (1.0 + Level * 0.05)
    * (1.0 + (COALESCE((SELECT sp.HighestStage / 100 FROM StageProgresses sp WHERE sp.UserId = Characters.UserId), 0) + EquippedChestBonusLevel) * 0.20)
  ) AS INTEGER),
  EquipmentPowerBonus = EquipmentPowerBonus + CAST(ROUND(
    15 * CASE WHEN EquippedChestQuality > 0 THEN EquippedChestQuality ELSE 1.0 END
    * (1.0 + Level * 0.05)
    * (1.0 + (COALESCE((SELECT sp.HighestStage / 100 FROM StageProgresses sp WHERE sp.UserId = Characters.UserId), 0) + EquippedChestBonusLevel) * 0.20)
  ) AS INTEGER),
  EquipmentDefenseBonus = EquipmentDefenseBonus + CAST(ROUND(
    35 * CASE WHEN EquippedChestQuality > 0 THEN EquippedChestQuality ELSE 1.0 END
    * (1.0 + Level * 0.05)
    * (1.0 + (COALESCE((SELECT sp.HighestStage / 100 FROM StageProgresses sp WHERE sp.UserId = Characters.UserId), 0) + EquippedChestBonusLevel) * 0.20)
  ) AS INTEGER)
WHERE EquippedChest IS NOT NULL;

-- Step 6e: Add Gloves slot bonuses
UPDATE Characters SET
  EquipmentHPBonus = EquipmentHPBonus + CAST(ROUND(
    10 * CASE WHEN EquippedGlovesQuality > 0 THEN EquippedGlovesQuality ELSE 1.0 END
    * (1.0 + Level * 0.05)
    * (1.0 + (COALESCE((SELECT sp.HighestStage / 100 FROM StageProgresses sp WHERE sp.UserId = Characters.UserId), 0) + EquippedGlovesBonusLevel) * 0.20)
  ) AS INTEGER),
  EquipmentPowerBonus = EquipmentPowerBonus + CAST(ROUND(
    25 * CASE WHEN EquippedGlovesQuality > 0 THEN EquippedGlovesQuality ELSE 1.0 END
    * (1.0 + Level * 0.05)
    * (1.0 + (COALESCE((SELECT sp.HighestStage / 100 FROM StageProgresses sp WHERE sp.UserId = Characters.UserId), 0) + EquippedGlovesBonusLevel) * 0.20)
  ) AS INTEGER),
  EquipmentDefenseBonus = EquipmentDefenseBonus + CAST(ROUND(
    3 * CASE WHEN EquippedGlovesQuality > 0 THEN EquippedGlovesQuality ELSE 1.0 END
    * (1.0 + Level * 0.05)
    * (1.0 + (COALESCE((SELECT sp.HighestStage / 100 FROM StageProgresses sp WHERE sp.UserId = Characters.UserId), 0) + EquippedGlovesBonusLevel) * 0.20)
  ) AS INTEGER)
WHERE EquippedGloves IS NOT NULL;

-- Step 6f: Add Legs slot bonuses
UPDATE Characters SET
  EquipmentHPBonus = EquipmentHPBonus + CAST(ROUND(
    60 * CASE WHEN EquippedLegsQuality > 0 THEN EquippedLegsQuality ELSE 1.0 END
    * (1.0 + Level * 0.05)
    * (1.0 + (COALESCE((SELECT sp.HighestStage / 100 FROM StageProgresses sp WHERE sp.UserId = Characters.UserId), 0) + EquippedLegsBonusLevel) * 0.20)
  ) AS INTEGER),
  EquipmentPowerBonus = EquipmentPowerBonus + CAST(ROUND(
    10 * CASE WHEN EquippedLegsQuality > 0 THEN EquippedLegsQuality ELSE 1.0 END
    * (1.0 + Level * 0.05)
    * (1.0 + (COALESCE((SELECT sp.HighestStage / 100 FROM StageProgresses sp WHERE sp.UserId = Characters.UserId), 0) + EquippedLegsBonusLevel) * 0.20)
  ) AS INTEGER),
  EquipmentDefenseBonus = EquipmentDefenseBonus + CAST(ROUND(
    15 * CASE WHEN EquippedLegsQuality > 0 THEN EquippedLegsQuality ELSE 1.0 END
    * (1.0 + Level * 0.05)
    * (1.0 + (COALESCE((SELECT sp.HighestStage / 100 FROM StageProgresses sp WHERE sp.UserId = Characters.UserId), 0) + EquippedLegsBonusLevel) * 0.20)
  ) AS INTEGER)
WHERE EquippedLegs IS NOT NULL;

-- Step 6g: Add Boots slot bonuses
UPDATE Characters SET
  EquipmentHPBonus = EquipmentHPBonus + CAST(ROUND(
    30 * CASE WHEN EquippedBootsQuality > 0 THEN EquippedBootsQuality ELSE 1.0 END
    * (1.0 + Level * 0.05)
    * (1.0 + (COALESCE((SELECT sp.HighestStage / 100 FROM StageProgresses sp WHERE sp.UserId = Characters.UserId), 0) + EquippedBootsBonusLevel) * 0.20)
  ) AS INTEGER),
  EquipmentPowerBonus = EquipmentPowerBonus + CAST(ROUND(
    5 * CASE WHEN EquippedBootsQuality > 0 THEN EquippedBootsQuality ELSE 1.0 END
    * (1.0 + Level * 0.05)
    * (1.0 + (COALESCE((SELECT sp.HighestStage / 100 FROM StageProgresses sp WHERE sp.UserId = Characters.UserId), 0) + EquippedBootsBonusLevel) * 0.20)
  ) AS INTEGER),
  EquipmentDefenseBonus = EquipmentDefenseBonus + CAST(ROUND(
    10 * CASE WHEN EquippedBootsQuality > 0 THEN EquippedBootsQuality ELSE 1.0 END
    * (1.0 + Level * 0.05)
    * (1.0 + (COALESCE((SELECT sp.HighestStage / 100 FROM StageProgresses sp WHERE sp.UserId = Characters.UserId), 0) + EquippedBootsBonusLevel) * 0.20)
  ) AS INTEGER)
WHERE EquippedBoots IS NOT NULL;

-- Step 6h: Add Weapon 1 bonuses
UPDATE Characters SET
  EquipmentHPBonus = EquipmentHPBonus + COALESCE((
    SELECT CAST(ROUND(fw.BonusHP * (1.0 + Characters.Level * 0.02)) AS INTEGER)
    FROM ForgedWeapons fw WHERE fw.Id = Characters.EquippedWeapon1
  ), 0),
  EquipmentPowerBonus = EquipmentPowerBonus + COALESCE((
    SELECT CAST(ROUND(fw.BonusPower * (1.0 + Characters.Level * 0.02)) AS INTEGER)
    FROM ForgedWeapons fw WHERE fw.Id = Characters.EquippedWeapon1
  ), 0),
  EquipmentDefenseBonus = EquipmentDefenseBonus + COALESCE((
    SELECT CAST(ROUND(fw.BonusDefense * (1.0 + Characters.Level * 0.02)) AS INTEGER)
    FROM ForgedWeapons fw WHERE fw.Id = Characters.EquippedWeapon1
  ), 0),
  EquipmentSpeedBonus = EquipmentSpeedBonus + COALESCE((
    SELECT fw.BonusSpeed FROM ForgedWeapons fw WHERE fw.Id = Characters.EquippedWeapon1
  ), 0),
  EquipmentCriticalBonus = EquipmentCriticalBonus + COALESCE((
    SELECT fw.BonusCriticalChance FROM ForgedWeapons fw WHERE fw.Id = Characters.EquippedWeapon1
  ), 0)
WHERE EquippedWeapon1 IS NOT NULL;

-- Step 6i: Add Weapon 2 bonuses (only if different from Weapon 1 and exists)
UPDATE Characters SET
  EquipmentHPBonus = EquipmentHPBonus + COALESCE((
    SELECT CAST(ROUND(fw.BonusHP * (1.0 + Characters.Level * 0.02)) AS INTEGER)
    FROM ForgedWeapons fw WHERE fw.Id = Characters.EquippedWeapon2
  ), 0),
  EquipmentPowerBonus = EquipmentPowerBonus + COALESCE((
    SELECT CAST(ROUND(fw.BonusPower * (1.0 + Characters.Level * 0.02)) AS INTEGER)
    FROM ForgedWeapons fw WHERE fw.Id = Characters.EquippedWeapon2
  ), 0),
  EquipmentDefenseBonus = EquipmentDefenseBonus + COALESCE((
    SELECT CAST(ROUND(fw.BonusDefense * (1.0 + Characters.Level * 0.02)) AS INTEGER)
    FROM ForgedWeapons fw WHERE fw.Id = Characters.EquippedWeapon2
  ), 0),
  EquipmentSpeedBonus = EquipmentSpeedBonus + COALESCE((
    SELECT fw.BonusSpeed FROM ForgedWeapons fw WHERE fw.Id = Characters.EquippedWeapon2
  ), 0),
  EquipmentCriticalBonus = EquipmentCriticalBonus + COALESCE((
    SELECT fw.BonusCriticalChance FROM ForgedWeapons fw WHERE fw.Id = Characters.EquippedWeapon2
  ), 0)
WHERE EquippedWeapon2 IS NOT NULL
  AND EquippedWeapon2 != COALESCE(EquippedWeapon1, -1);

-- ── Verification Query ───────────────────────────────────────────────────────
-- Run this after the script to verify results:
SELECT u.UserName, c.Level, c.HpUpgrades, c.PowerUpgrades, c.DefenseUpgrades,
       c.EquipmentHPBonus, c.EquipmentPowerBonus, c.EquipmentDefenseBonus
FROM Characters c JOIN AspNetUsers u ON c.UserId = u.Id
WHERE c.Level >= 10
ORDER BY c.Level DESC;
