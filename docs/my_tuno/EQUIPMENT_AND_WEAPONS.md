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
| Head | 75 | 13 | 20 |
| Shoulders | 60 | 7 | 25 |
| Chest | 130 | 20 | 45 |
| Gloves | 15 | 32 | 5 |
| Legs | 75 | 13 | 20 |
| Boots | 40 | 6 | 13 |
| **Total** | **395** | **91** | **128** |

### Stat Formula

Each slot's contribution to a stat (HP, Power, or Defense) is:

$$\text{SlotBonus} = (\text{BaseStat} + \text{bonusLevel} \times \text{flatPerLevel}) \times Q \times L$$

Where:
- **flatPerLevel** = HP: 100, Power: 15, Defense: 12
- **Q** (Quality) = 1.0 for new characters (legacy characters may have randomized values 0.7–1.3)
- **L** (Level Scale) = `1.0 + characterLevel × equipmentLevelScale` (`equipmentLevelScale = 0.005`)
  - At level 170: L = **1.85×**
  - At level 500: L = **3.50×**
  - At level 1000: L = **6.00×**

### Upgrading Equipment

Each slot can be upgraded independently using **Fidelis + Drinks + Leitões** (from level 1001).

**Fidelis Cost Formula:** `Cost(n) = 240 + n × 240` Fidelis (where `n` = current slot bonus level)

**Drink Requirement:** Uses tiered drinks. Every **100 levels** advances to the next drink tier:
- Levels 0–99: No drinks required
- Levels 100–199: Cerveja (qty 1→50)
- Levels 200–299: Vinho (qty 1→50)
- Levels 300–399: Licor (qty 1→50)
- Levels 400–499: Rum (qty 1→50)
- Levels 500–599: Tequilla (qty 1→50)
- Levels 600–699: Vodka (qty 1→50)
- Levels 700–799: Gin (qty 1→50)
- Levels 800–899: Whisky (qty 1→50)
- Levels 900–999: Absinto (qty 1→50)
- Levels 1000+: Aguardente (qty cycles 1→50)

**Leitão Cost:** 0 for levels 0–999. From level 1000+: crescendo starting at 1 piggy, +1 every 250 levels.

| Level Range | Leitão Cost |
|-------------|------------:|
| 0–999 | 0 |
| 1000–1249 | 1 |
| 1250–1499 | 2 |
| 1500–1749 | 3 |
| 1750–1999 | 4 |
| 2000–2249 | 5 |
| … | +1 per 250 |

**Max per-slot enhancement level: 9999** (`maxEquipmentEnhancement: 9999`).

### Equipment Upgrade Cost Table (sample levels)

| Level | Fidelis Cost | Drink | Qty | Leitão |
|------:|-----------:|-------|----:|-------:|
| 1 | 480 | — | — | 0 |
| 50 | 12,240 | — | — | 0 |
| 100 | 24,240 | Cerveja | 1 | 0 |
| 200 | 48,240 | Vinho | 1 | 0 |
| 500 | 120,240 | Tequilla | 1 | 0 |
| 1000 | 240,240 | Aguardente | 1 | 1 |
| 1250 | 300,240 | Aguardente | 26 | 2 |
| 1500 | 360,240 | Aguardente | 1 | 3 |

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

Base stats come from the `instrument` config entry (`hp: 10, power: 16, defense: 7`):

$$\text{Stat} = \text{InstrumentBase} \times D_t \times Q_i \times H$$

Where:
- **$D_t$** (Drink Tier Multiplier) = `1.0 + (energyCost − 1) × drinkStatBonusPerTier`
  - With `drinkStatBonusPerTier = 0.25`: Cerveja = 1.0×, Vinho = 1.25×, Licor = 1.50×, … Aguardente = 3.25×
- **$Q_i$** (Instrument Quality) = random roll in `[0.85, 1.15]`
- **H** (Handed Multiplier) = 2.0 for two-handed, 1.0 for one-handed

**Drink Tier Multiplier Table:**

| Drink | Energy Cost | Tier Mult | 1H Mult | 2H Mult |
|-------|----------:|----------:|--------:|--------:|
| Cerveja | 1 | 1.00× | 1.00× | 2.00× |
| Vinho | 2 | 1.25× | 1.25× | 2.50× |
| Licor | 3 | 1.50× | 1.50× | 3.00× |
| Rum | 4 | 1.75× | 1.75× | 3.50× |
| Tequilla | 5 | 2.00× | 2.00× | 4.00× |
| Vodka | 6 | 2.25× | 2.25× | 4.50× |
| Gin | 7 | 2.50× | 2.50× | 5.00× |
| Whisky | 8 | 2.75× | 2.75× | 5.50× |
| Absinto | 9 | 3.00× | 3.00× | 6.00× |
| Aguardente | 10 | 3.25× | 3.25× | 6.50× |

**Bonus rolls** (high-tier drinks only, drinkEnergyCost ≥ 7):
- **Critical Chance**: 30% chance to roll +1%–10% crit (2H gets two independent rolls)
- **Speed**: 25% chance to roll +1–5 speed (2H gets two independent rolls)

### Weapon Level Scaling

Equipped weapons scale with character level:

$$\text{EffectiveStat} = \text{WeaponStat} \times (1.0 + \text{characterLevel} \times \text{weaponCharacterLevelScale})$$

`weaponCharacterLevelScale = 0.005`. Speed and Crit bonuses from weapons are flat (not scaled).

| Char Level | Weapon Multiplier |
|-----------:|------------------:|
| 1 | 1.005× |
| 100 | 1.50× |
| 170 | 1.85× |
| 500 | 3.50× |
| 1000 | 6.00× |

### Weapon Upgrading

Weapons can be upgraded using **Fidelis + Drinks + Leitões** (from level **12**).

**Fidelis Cost Formula:** `Cost(n) = 50 + n × 50` Fidelis (where `n` = current weapon level)

**Drink Requirement:** Same tier system as equipment (100 levels per tier, see above).

**Leitão Cost:** Same crescendo as equipment — 0 for levels 0–999, then +1 every 250 levels from 1000.

**Stat bonus per level:** +5% of base stats per level (`weaponUpgradeStatBonus = 0.05`)

**Max Weapon Level:** 9999

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
| `maxEquipmentEnhancement` | 9999 | Cap per slot |
| `equipmentLevelScale` | 0.005 | Character level → equipment multiplier |
| `weaponCharacterLevelScale` | 0.005 | Character level → weapon multiplier |
| `forging.weaponUpgradeBaseCost` | 50 | Base Fidelis for weapon upgrade |
| `forging.weaponUpgradeCostPerLevel` | 50 | Per-level Fidelis increment |
| `forging.equipmentUpgradeBaseCost` | 240 | Base Fidelis for armor upgrade |
| `forging.equipmentUpgradeCostPerLevel` | 240 | Per-level Fidelis increment |
| `forging.twoHandedMultiplier` | 2.0 | 2H weapon stat multiplier |
| `forging.weaponUpgradeStatBonus` | 0.05 | +5% per weapon level |
| `forging.maxWeaponLevel` | 9999 | Weapon level cap |
| `forging.upgradeLevelsPerDrinkTier` | 100 | Levels before next drink tier |
| `forging.drinkStatBonusPerTier` | 0.25 | +25% stats per drink tier above Cerveja |

### Piggies (Leitão) Cost Config

| Key | Value | Description |
|-----|------:|-------------|
| Weapon/Equipment Leitão | Starts at 1000 | Crescendo: 0 below 1000, then +1 every 250 levels |

Leitões are earned exclusively from **Boss Mode** victories.
