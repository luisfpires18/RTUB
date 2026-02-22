# Weapons, Equipment & Discard Values

> Reference documentation for weapon forging, upgrades, equipment enhancements, and discard (sell) value formulas.

---

## 1. Weapon Types

| Type | Enum | Handed | Two-Handed Multiplier |
|------|-----:|--------|----------------------:|
| Sword (1H) | 1 | 1H | 1.0× |
| Axe (1H) | 2 | 1H | 1.0× |
| Mace | 3 | 1H | 1.0× |
| Shield | 5 | 1H (off-hand) | 1.0× |
| Dagger | 11 | 1H | 1.0× |
| Staff | 4 | 2H | 2.0× |
| Bow | 6 | 2H | 2.0× |
| Sword (2H) | 7 | 2H | 2.0× |
| Spear | 8 | 2H | 2.0× |
| Axe (2H) | 9 | 2H | 2.0× |
| Hammer | 10 | 2H | 2.0× |

Players have **2 weapon slots**. One-handed weapons go in either slot (dual-wield is possible). Two-handed weapons occupy both slots.

---

## 2. Weapon Stats

Each forged weapon (`ForgedWeapon`) stores:

| Stat | Source | Modified by Upgrades? |
|------|--------|-----------------------|
| **BonusHP** | Base formula | Yes (recalculated) |
| **BonusPower** | Base formula | Yes (recalculated) |
| **BonusDefense** | Base formula | Yes (recalculated) |
| **BonusSpeed** | Bonus roll (≥ Gin tier) | No |
| **BonusCriticalChance** | Bonus roll (≥ Gin tier) | No |
| **Level** | Starts at 0, max 20 | — |

---

## 3. Forging a Weapon

### Inputs

- **1× Instrument Part** (any instrument type)
- **N× Drink** (quantity = drink's `forgeCost`)

### Drink Tiers

| Drink | Energy Cost | Forge Cost | Unlocked at Stage |
|-------|----------:|----------:|------------------:|
| Cerveja | 1 | 1 | 1 |
| Vinho | 2 | 1 | 2,001 |
| Licor | 3 | 2 | 4,001 |
| Rum | 4 | 2 | 6,001 |
| Tequilla | 5 | 3 | 8,001 |
| Vodka | 6 | 3 | 10,001 |
| Gin | 7 | 4 | 12,001 |
| Whisky | 8 | 5 | 14,001 |
| Absinto | 9 | 6 | 16,001 |
| Aguardente | 10 | 8 | 18,001 |

### Base Stat Formula

```
Stat = InstrumentBase × drinkTierMult × qualityRoll × twoHandedMultiplier
```

Where:
```
drinkTierMult = 1.0 + (energyCost − 1) × drinkStatBonusPerTier
```

| Instrument Base | Value |
|----------------|------:|
| HP | 10 |
| Power | 16 |
| Defense | 7 |

- **drinkStatBonusPerTier** = 0.25 (configurable)
- **qualityRoll** — random in `[0.85, 1.15]`
- **twoHandedMultiplier** — 2.0 for 2H weapons, 1.0 for 1H

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

### Bonus Rolls (Gin tier or higher, i.e. drinkEnergyCost ≥ 7)

| Bonus | Roll Chance | Range |
|-------|----------:|-------|
| Critical Chance | 30% | +1% – 10% |
| Speed | 25% | +1 – 5 |

Two-handed weapons get **two independent rolls** for each bonus (can stack).

### Example — Aguardente + 1H Weapon

```
drinkTierMult = 1.0 + (10 − 1) × 0.25 = 3.25
HP    = 10 × 3.25 × Q  →  28 –  37 (avg 33)
Power = 16 × 3.25 × Q  →  44 –  60 (avg 52)
Def   = 7  × 3.25 × Q  →  19 –  26 (avg 23)
```

### Example — Cerveja + 1H Weapon

```
drinkTierMult = 1.0 + (1 − 1) × 0.25 = 1.00
HP    = 10 × 1.00 × Q  →   9 –  12 (avg 10)
Power = 16 × 1.00 × Q  →  14 –  18 (avg 16)
Def   = 7  × 1.00 × Q  →   6 –   8 (avg 7)
```

---

## 4. Weapon Upgrades

### Cost Formula

```
Fidelis = 50 + currentLevel × 50
Drink   = tier drink, quantity = (currentLevel % 5) + 1
Leitão  = 0 if currentLevel < 12, else 1 + (currentLevel - 12) / 5
```

Drink tier advances every 5 levels (`upgradeLevelsPerDrinkTier = 5`). Leitão cost starts at level **12**.

| Level | Fidelis | Drink | Qty | Leitão |
|------:|--------:|-------|----:|-------:|
| 0 → 1 | 50 | Cerveja | 1 | 0 |
| 1 → 2 | 100 | Cerveja | 2 | 0 |
| 2 → 3 | 150 | Cerveja | 3 | 0 |
| 3 → 4 | 200 | Cerveja | 4 | 0 |
| 4 → 5 | 250 | Cerveja | 5 | 0 |
| 5 → 6 | 300 | Vinho | 1 | 0 |
| 9 → 10 | 500 | Vinho | 5 | 0 |
| 10 → 11 | 550 | Licor | 1 | 0 |
| 12 → 13 | 650 | Licor | 3 | 1 |
| 14 → 15 | 750 | Licor | 5 | 1 |
| 15 → 16 | 800 | Rum | 1 | 1 |
| 19 → 20 | 1,000 | Rum | 5 | 2 |

**Max level: 20**

### Stat Recalculation on Upgrade

```
Stat = (InstrumentBase + weaponLevel × statPerLevel) × drinkTierMult × twoHandedMultiplier
```

Where:
```
drinkTierMult = 1.0 + (energyCost − 1) × drinkStatBonusPerTier
```

Upgrades **recalculate** HP, Power, and Defense from base values. The original quality roll is replaced by the level-based formula. Speed and Critical Chance bonuses are **never modified** by upgrades.

**Stat per level:** HP = +100, Power = +15, Defense = +12 (from `equipmentHpPerLevel`, `equipmentPowerPerLevel`, `equipmentDefensePerLevel`).

---

## 5. Equipment Upgrades

### Cost Formula

```
Fidelis = 40 + currentSlotLevel × 40
Drink   = tier drink, quantity = (currentSlotLevel % 5) + 1
Leitão  = 0 if currentSlotLevel < 500, else 1 + (currentSlotLevel - 500) / 5
```

Same drink tier progression as weapons (every 5 levels). Leitão cost starts at level **500** — effectively never required since max enhancement is 15.

| Slot Level | Fidelis | Leitão |
|-----------:|--------:|-------:|
| 0 → 1 | 40 | 0 |
| 1 → 2 | 80 | 0 |
| 2 → 3 | 120 | 0 |
| 4 → 5 | 200 | 0 |
| 9 → 10 | 400 | 0 |
| 14 → 15 | 600 | 0 |

**Max enhancement: 15 per slot** (Leitão never required; equipment upgrades are Fidelis + Drinks only)

### Effect

Each slot bonus level adds **+5%** to that slot's stat multiplier:

```
SlotBonus = BaseStat × quality × (1.0 + slotBonusLevel × 0.05)
```

---

## 6. Discard (Sell) Values

### Equipment Discard

```
Value = 20 × (1 + characterLevel × 0.05) × enhancementMultiplier
```

Where `enhancementMultiplier = 1.0 + slotBonusLevel × 0.05`.

### Instrument Part Discard

```
Value = 25 × (1 + characterLevel × 0.05)
```

### Weapon Discard

```
Value = 50 × (1 + weaponLevel × 0.5) × drinkEnergyCost × (1 + characterLevel × 0.05)
```

| Factor | Effect |
|--------|--------|
| Base value | 50 Fidelis |
| Weapon level | +50% per level |
| Drink rarity | Scales linearly with energy cost (1–10) |
| Character level | +5% per level |

### Weapon Discard Examples

| Weapon Level | Drink | Char Level | Discard Value |
|-------------:|-------|----------:|--------------:|
| 0 | Cerveja (1) | 1 | 52.5 |
| 0 | Aguardente (10) | 1 | 525 |
| 5 | Vinho (2) | 20 | 700 |
| 10 | Aguardente (10) | 50 | 10,500 |
| 20 | Aguardente (10) | 100 | 33,000 |

---

## 7. Configuration Reference

All values live in `scaling.config.json` with defaults in `MyTunoScalingConfiguration.cs`.

### Forging

| Key | Value | Description |
|-----|------:|-------------|
| `castTimeSeconds` | 5 | Forge animation duration |
| `weaponUpgradeBaseCost` | 50 | Base Fidelis for weapon upgrade |
| `weaponUpgradeCostPerLevel` | 50 | Fidelis increment per level |
| `maxWeaponLevel` | 20 | Weapon level cap |
| `weaponUpgradeStatBonus` | 0.05 | +5% stats per weapon level |
| `equipmentUpgradeBaseCost` | 40 | Base Fidelis for equipment upgrade |
| `equipmentUpgradeCostPerLevel` | 40 | Fidelis increment per level |
| `maxEquipmentEnhancement` | 15 | Equipment slot level cap |
| `equipmentEnhancementBonus` | 0.05 | +5% per slot enhancement level |
| `twoHandedMultiplier` | 2.0 | 2H weapon stat multiplier |
| `instrumentQualityMin` | 0.85 | Min quality roll |
| `instrumentQualityMax` | 1.15 | Max quality roll |
| `upgradeLevelsPerDrinkTier` | 5 | Levels per drink tier |
| `drinkStatBonusPerTier` | 0.25 | +25% stats per drink tier above Cerveja |

### Bonus Rolls

| Key | Value | Description |
|-----|------:|-------------|
| `critMinDrinkCost` | 7 | Min drink tier for crit rolls |
| `critRollChance` | 0.30 | 30% chance per roll |
| `critMin` | 0.01 | Min crit bonus |
| `critMax` | 0.10 | Max crit bonus |
| `speedMinDrinkCost` | 7 | Min drink tier for speed rolls |
| `speedRollChance` | 0.25 | 25% chance per roll |
| `speedMin` | 1 | Min speed bonus |
| `speedMax` | 5 | Max speed bonus |

### Discard Values

| Key | Value | Description |
|-----|------:|-------------|
| `discardValues.equipment` | 20 | Base equipment discard |
| `discardValues.instrumentPart` | 25 | Base instrument part discard |
| `discardValues.weapon` | 50 | Base weapon discard |
| `discardLevelScale` | 0.05 | Per-level discard scaling |

### Piggies (Leitão) Costs

All upgrades require Leitões (🐷) once above a threshold level. Leitões are earned exclusively from **Boss Mode** victories.

| Upgrade Type | Start Level | Base Cost | +1 Every N Levels |
|-------------|------------:|----------:|------------------:|
| HP / Power / Defense | 300 | 1 | 5 |
| Speed | 20 | 1 | 5 |
| Crit | 50 | 1 | 5 |
| Improvement (Energy) | 20 | 1 | 5 |
| Power (Heavy/Special) | 20 | 1 | 5 |
| Weapon | 12 | 1 | 5 |
| Equipment slot | 500 | 1 | 5 |

**Formula:** `PiggiesCostConfig.CalculateCost(currentLevel, startLevel, baseCost, costEveryNLevels)`
- Returns `0` below start level
- Returns `baseCost + (currentLevel - startLevel) / costEveryNLevels` at or above start level
