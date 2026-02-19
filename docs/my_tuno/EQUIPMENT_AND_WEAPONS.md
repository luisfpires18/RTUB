# My Tuno — Equipment & Weapons

## Overview

Players have two gear systems:
1. **Equipment (Armor)** — 6 permanent armor pieces that give HP, Power, and Defense bonuses
2. **Weapons** — Forged from instrument parts + drinks, equippable in 1-2 weapon slots

---

## Equipment (Armor)

### Slots

All 6 armor pieces are **permanently equipped** from character creation. They cannot be unequipped or swapped.

| Slot | Base HP | Base Power | Base Defense |
|------|--------:|-----------:|-----------:|
| Head | 60 | 10 | 15 |
| Shoulders | 45 | 5 | 20 |
| Chest | 100 | 15 | 35 |
| Gloves | 10 | 25 | 3 |
| Legs | 60 | 10 | 15 |
| Boots | 30 | 5 | 10 |
| **Total** | **305** | **70** | **98** |

### Stat Formula

Each slot's contribution to a stat (HP, Power, or Defense) is:

$$\text{SlotBonus} = \text{BaseStat} \times Q \times L \times E$$

Where:
- **Q** (Quality) = 1.0 for new characters (legacy characters may have randomized values 0.7–1.3)
- **L** (Level Scale) = `1.0 + characterLevel × equipmentLevelScale` (currently `equipmentLevelScale = 0.0`, so L = 1.0)
- **E** (Enhancement Multiplier) = `1.0 + (stageDerived + slotBonusLevel) × 0.05`
  - `stageDerived` = `highestStage / 100` (integer division)
  - `slotBonusLevel` = purchased upgrade level for that slot

### Upgrading Equipment

Each slot can be upgraded independently using **Fidelis + Drinks + Leitões** (from level 5+).

**Fidelis Cost Formula:** `Cost(n) = 40 + n × 40` Fidelis (where `n` = current slot bonus level)

**Leitão Cost Formula:** `Cost(n) = 1 + (n - 12) / 5` (starts at level 12, +1 every 5 levels)

**Drink Requirement:** Uses tiered drinks. Every **50 levels** advances to the next drink tier:
- Levels 0–49: Cerveja (1× to 50×)
- Levels 50–99: Vinho (1× to 50×)
- Levels 100–149: Licor (1× to 50×)
- Levels 150–199: Rum (1× to 50×)
- ... (continues through all 10 drink tiers)
- Levels 450+: Aguardente (50× max)

**No hard cap on per-slot bonus level.** Natural cost scaling (Fidelis + drinks + Leitão) acts as the soft ceiling.

### Equipment Upgrade Cost Table (sample levels)

| Level | Fidelis Cost | Cumulative | Drink | Qty | Leitão |
|------:|-----------:|-----------:|-------|----:|-------:|
| 1 | 40 | 40 | Cerveja | 1 | 0 |
| 5 | 200 | 600 | Cerveja | 5 | 0 |
| 10 | 400 | 2,200 | Cerveja | 10 | 0 |
| 12 | 480 | 3,120 | Cerveja | 12 | 1 |
| 20 | 800 | 8,400 | Cerveja | 20 | 2 |
| 30 | 1,200 | 18,600 | Cerveja | 30 | 4 |
| 40 | 1,600 | 32,800 | Cerveja | 40 | 6 |
| 50 | 2,000 | 51,000 | Cerveja | 50 | 8 |
| 51 | 2,040 | 53,040 | Vinho | 1 | 8 |
| 100 | 4,000 | 202,000 | Vinho | 50 | 18 |
| 15 | 600 | 4,800 | Licor | 5 | 3 |

---

## Weapons (Forged)

### Forging

Weapons are created by combining **1 instrument part** + **N drinks** at the forge.

**Drink cost per forge** is determined by the `forgeCost` field in the drink's config:

| Drink | Energy Cost | Forge Cost |
|-------|----------:|----------:|
| Cerveja | 1 | 1 |
| Vinho | 2 | 1 |
| Licor | 3 | 2 |
| Rum | 4 | 2 |
| Tequilla | 5 | 3 |
| Vodka | 6 | 3 |
| Gin | 7 | 4 |
| Whisky | 8 | 5 |
| Absinto | 9 | 6 |
| Aguardente | 10 | 8 |

### Weapon Types

| Type | Handed | Notes |
|------|--------|-------|
| Sword (1H) | One | Can dual-wield |
| Axe (1H) | One | Can dual-wield |
| Mace | One | Can dual-wield |
| Dagger | One | Can dual-wield |
| Shield | One | Off-hand only |
| Staff | Two | Gets 2× stat multiplier |
| Bow | Two | Gets 2× stat multiplier |
| Sword (2H) | Two | Gets 2× stat multiplier |
| Spear | Two | Gets 2× stat multiplier |
| Axe (2H) | Two | Gets 2× stat multiplier |
| Hammer | Two | Gets 2× stat multiplier |

Two-handed weapons occupy both weapon slots but get the `twoHandedMultiplier` (2.0×) applied to base stats.

### Weapon Stat Formula

Base stats come from the `instrument` config entry (`hp: 8, power: 12, defense: 5`):

$$\text{Stat} = \text{InstrumentBase} \times \text{drinkEnergyCost} \times Q_i \times H$$

Where:
- **drinkEnergyCost** = the `energyCost` of the drink used (1–10)
- **$Q_i$** (Instrument Quality) = random roll in `[0.85, 1.15]`
- **H** (Handed Multiplier) = 2.0 for two-handed, 1.0 for one-handed

**Bonus rolls** (high-tier drinks only, drinkEnergyCost ≥ 7):
- **Critical Chance**: 30% chance to roll +1%–10% crit (2H gets two independent rolls)
- **Speed**: 25% chance to roll +1–5 speed (2H gets two independent rolls)

### Weapon Level Scaling

Equipped weapons scale with character level:

$$\text{EffectiveStat} = \text{WeaponStat} \times (1.0 + \text{characterLevel} \times \text{weaponCharacterLevelScale})$$

Currently `weaponCharacterLevelScale = 0.0` so there's no level scaling. Speed and Crit bonuses from weapons are flat (not scaled).

### Weapon Upgrading

Weapons can be upgraded using **Fidelis + Drinks + Leitões** (from level 5+).

**Fidelis Cost Formula:** `Cost(n) = 50 + n × 50` Fidelis (where `n` = current weapon level)

**Leitão Cost Formula:** `Cost(n) = 1 + (n - 5) / 5` (starts at level 5, +1 every 5 levels)

**Stat bonus per level:** +5% of base stats per level (`weaponUpgradeStatBonus = 0.05`)

**Max Weapon Level:** 20

### Weapon Equipping

- Players have 2 weapon slots
- One-handed weapons can be placed in either slot (dual-wield possible)
- Two-handed weapons occupy both slots
- Weapons can be freely equipped/unequipped

---

## Drink Unlock Stages

Drinks are unlocked by reaching specific stage milestones. There is a gap biome between each drink unlock:

| Drink | Unlock Stage | Biome |
|-------|------------:|-------|
| Cerveja | 1 | Forest |
| Vinho | 2,001 | Mountains |
| Licor | 4,001 | Tropical |
| Rum | 6,001 | Desert |
| Tequilla | 8,001 | Ruins |
| Vodka | 10,001 | Underwater |
| Gin | 12,001 | Mechanical |
| Whisky | 14,001 | Corruption |
| Absinto | 16,001 | Alien |
| Aguardente | 18,001 | Timerift |

---

## Config Reference

All values are defined in `scaling.config.json` under `stageMode`:

| Key | Value | Description |
|-----|-------|-------------|
| `equipmentStats.*` | per-slot HP/Power/Def | Base armor stats |
| `equipmentEnhancementBonus` | 0.05 | +5% per enhancement level |
| `maxEquipmentEnhancement` | 15 | Cap per slot |
| `equipmentLevelScale` | 0.0 | Character level → equipment multiplier |
| `weaponCharacterLevelScale` | 0.0 | Character level → weapon multiplier |
| `forging.weaponUpgradeBaseCost` | 50 | Base Fidelis for weapon upgrade |
| `forging.weaponUpgradeCostPerLevel` | 50 | Per-level Fidelis increment |
| `forging.equipmentUpgradeBaseCost` | 40 | Base Fidelis for armor upgrade |
| `forging.equipmentUpgradeCostPerLevel` | 40 | Per-level Fidelis increment |
| `forging.twoHandedMultiplier` | 2.0 | 2H weapon stat multiplier |
| `forging.weaponUpgradeStatBonus` | 0.05 | +5% per weapon level |
| `forging.maxWeaponLevel` | 20 | Weapon level cap |
| `forging.upgradeLevelsPerDrinkTier` | 5 | Levels before next drink tier |

### Piggies (Leitão) Cost Config

| Key | Value | Description |
|-----|------:|-------------|
| `piggies.equipmentUpgradeStartLevel` | 12 | Level at which Leitão cost begins for equipment |
| `piggies.equipmentUpgradeBaseCost` | 1 | Base Leitão cost |
| `piggies.equipmentUpgradeCostEveryNLevels` | 5 | +1 Leitão every N levels |
| `piggies.weaponUpgradeStartLevel` | 12 | Level at which Leitão cost begins for weapons |
| `piggies.weaponUpgradeBaseCost` | 1 | Base Leitão cost |
| `piggies.weaponUpgradeCostEveryNLevels` | 5 | +1 Leitão every N levels |

Leitões are earned exclusively from **Boss Mode** victories.
