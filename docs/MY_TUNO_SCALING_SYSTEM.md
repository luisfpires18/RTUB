# My-Tuno Game Scaling System Documentation

## Overview

The My-Tuno game features two main combat modes with distinct scaling mechanics:
1. **Arena Mode**: Player vs Player matchmaking-based battles
2. **Stage Mode**: Single-player progressive difficulty — the goal is to push as far as possible before being defeated. There is no "winning" stage mode; the player always eventually loses as enemies scale infinitely.

The scaling system is configured via `scaling.config.json` and defined in `MyTunoScalingConfiguration.cs`.

---

## 1. ARENA MODE SCALING

### 1.1 Player Statistics Scaling

#### Base Stats (Player Creation)
```json
"baseStats": {
  "level": 1,
  "xp": 0,
  "hp": 100,
  "power": 10,
  "speed": 10,
  "defense": 5,
  "criticalChance": 0.01
}
```

**Formula**: `StatValue = BaseStat + (Level - 1) * BaseStat * StatMultiplierPerLevel`
- **StatMultiplierPerLevel**: 0.1 (10% per level)
- Example: Level 10 HP = 100 + (10-1) × 100 × 0.1 = 190 HP

#### Level Progression
- **XP Formula**: `XP_Required = Level × 100`
  - Level 1→2: 100 XP
  - Level 10→11: 1000 XP
  - Level 36→37: 3600 XP

### 1.2 Upgrade System (Fidelis-based)

Each stat has independent upgrade scaling with a configurable max level cap:

| Stat | Bonus/Upgrade | Base Cost | Max Upgrades | Notes |
|------|---------------|-----------|--------------|-------|
| **HP** | +10 | 5 Fidelis | 50 | Highest bonus, lowest cost |
| **Power** | +2 | 10 Fidelis | 50 | Linear scaling with damage |
| **Speed** | +1 | 15 Fidelis | 40 | Affects action time (capped at 1.0s) |
| **Defense** | +2 | 12 Fidelis | 50 | Mitigation formula: `mult = 50/(50+defense)` |
| **Critical** | +0.005 | 20 Fidelis | 50 | Chance modifier (capped at 50%) |

**Cost Progression Formula**: `Cost = BaseCost × (1 + UpgradeCount) ^ 1.5`

**Max Level Enforcement**: Both the backend (`UpgradeService`) and frontend UI enforce the `maxUpgrades` limit per stat. When a stat reaches its max, the purchase button shows "Máximo Alcançado" and the backend rejects further purchases.

**Config Structure**:
```json
"upgrades": {
  "hp": {
    "initialBought": 0,
    "bonusPerUpgrade": 10,
    "baseCost": 5,
    "maxUpgrades": 50
  },
  "power": {
    "initialBought": 0,
    "bonusPerUpgrade": 2,
    "baseCost": 10,
    "maxUpgrades": 50
  },
  "speed": {
    "initialBought": 0,
    "bonusPerUpgrade": 1,
    "baseCost": 15,
    "maxUpgrades": 40
  },
  "criticalChance": {
    "initialBought": 0,
    "bonusPerUpgrade": 0.005,
    "baseCost": 20,
    "maxUpgrades": 50
  },
  "defense": {
    "initialBought": 0,
    "bonusPerUpgrade": 2,
    "baseCost": 12,
    "maxUpgrades": 50
  }
}
```

### 1.3 Arena Battle Rewards

#### Fidelis & XP Rewards
```json
"battleRewards": {
  "winReward": 15,
  "drawReward": 7.5,
  "baseWinXP": 50,
  "baseDrawXP": 30,
  "xpScalingFactor": 0.08,
  "minXpMultiplier": 0.2,
  "maxXpMultiplier": 3.0,
  "reviveCost": 30,
  "restoreHPCost": 10,
  "fidelisLevelMultiplier": 0.1
}
```

#### XP Calculation with Level Scaling
**Formula**: `XP = BaseXP × clamp(1.0 + (OpponentLevel - PlayerLevel) × XpScalingFactor, MinXpMultiplier, MaxXpMultiplier)`

- **BaseWinXP**: 50, **BaseDrawXP**: 30
- **XpScalingFactor**: 0.08 (8% per level difference)
- **Min Multiplier**: 0.2 (minimum 20% of base XP)
- **Max Multiplier**: 3.0 (maximum 300% of base XP)

#### Fidelis Level Bonus
**Formula**: `FidelisReward = BaseReward × (1 + PlayerLevel × FidelisLevelMultiplier)`
- **FidelisLevelMultiplier**: 0.1 (10% bonus per level)

### 1.4 Combat Mechanics

#### Defense Mitigation
**Formula**: `Multiplier = DefenseK / (DefenseK + Defense)`

- **DefenseK**: 50 (constant)
- **MinDamage**: 1 (guaranteed minimum damage)

| Defense | Multiplier | Mitigation |
|---------|-----------|------------|
| 5 | 0.909 | 9% |
| 25 | 0.667 | 33% |
| 50 | 0.500 | 50% |
| 100 | 0.333 | 67% |

#### Critical Chance
- **CriticalChanceCap**: 0.5 (50% max)
- Player base: 1%, upgradeable by +0.5% per upgrade

#### Items
- **Shot Buff Multiplier**: 1.20 (+20% to all stats, arena and stage mode)

### 1.5 Matchmaking

```json
"matchmaking": {
  "cooldownMinutes": 60,
  "initialPowerRangeMin": 0.7,
  "initialPowerRangeMax": 1.3,
  "expandedPowerRangeMin": 0.5,
  "expandedPowerRangeMax": 1.5,
  "powerRatingWeights": {
    "hp": 0.5,
    "power": 2.0,
    "speed": 1.5
  }
}
```

**Power Rating System**: Opponents are matched based on a weighted power rating:
- HP weight: 0.5, Power weight: 2.0, Speed weight: 1.5
- Initial range: 70%–130% of player's power rating
- Expanded range (if no match): 50%–150%

**Leaderboard Display**:
- 4 opponents at/below player level (sorted by proximity)
- 4 opponents above player level (sorted by proximity)
- Arena tab sorted by wins, Level tab by level, Stage tab by highest stage

---

## 2. STAGE MODE SCALING

### 2.1 Enemy Composition

#### Encounter Rules
```json
"encounterRules": {
  "bossEveryNStages": 10,
  "enemyCountByStageOffset": [
    { "from": 1, "to": 2, "count": 1 },
    { "from": 3, "to": 4, "count": 2 },
    { "from": 5, "to": 6, "count": 3 },
    { "from": 7, "to": 8, "count": 4 },
    { "from": 9, "to": 9, "count": 5 }
  ]
}
```

- **Boss every 10th stage**: Stage 10, 20, 30, etc.
- **Enemy count within each 10-stage block** (based on offset within block):

| Stage Offset | Enemy Count |
|-------------|-------------|
| 1–2 | 1 |
| 3–4 | 2 |
| 5–6 | 3 |
| 7–8 | 4 |
| 9 | 5 |
| 10 (boss) | 1 boss |

### 2.2 Enemy Base Stats

```json
"baseEnemyStats": {
  "normal": { "hp": 50, "power": 8, "speed": 5, "defense": 3, "criticalChance": 0.05 },
  "boss": { "hp": 500, "power": 20, "speed": 8, "defense": 15, "criticalChance": 0.15 }
}
```

### 2.3 Enemy Stat Scaling Per Stage

**Formula**: `ScaledStat = BaseStat × (1.0 + (Stage - 1) × ScalingRate)`

```json
"enemyScaling": {
  "hpPerStage": 0.08,
  "powerPerStage": 0.05,
  "speedPerStage": 0.03,
  "defensePerStage": 0.04,
  "criticalChancePerStage": 0.002
}
```

| Stat | Per Stage Rate | Notes |
|------|----------------|-------|
| **HP** | 8% | Fastest growth |
| **Power** | 5% | Linear damage increase |
| **Speed** | 3% | Slower action bar |
| **Defense** | 4% | Increasing damage reduction |
| **Critical** | +0.2% | Additive, not multiplicative |

**Examples** (Normal Enemy):
| Stage | HP | Power | Speed | Defense |
|-------|-----|-------|-------|---------|
| 1 | 50 | 8 | 5 | 3 |
| 10 | 86 | 11.6 | 6.35 | 4.44 |
| 50 | 246 | 27.6 | 12.35 | 8.88 |
| 100 | 446 | 47.6 | 19.85 | 14.88 |

#### Additional Scaling Layer
```json
"scaling": {
  "hpGrowthPerStage": 0.06,
  "damageGrowthPerStage": 0.05,
  "armorGrowthPerStage": 0.03,
  "bossMultiplier": 2.5
}
```

### 2.4 Stage Mode XP Rewards

```json
"stageMode": {
  "baseStageXP": 30,
  "bossXPMultiplier": 10,
  "stageRewardScalingFactor": 0.05
}
```

**Formula**: `XP = BaseStageXP × EnemyCount × EnemyTypeMultiplier × (1.0 + Stage × StageRewardScalingFactor)`

- **EnemyTypeMultiplier**: Normal: 1.0, Boss: 10.0
- **StageRewardScalingFactor**: 0.05 (5% per stage)

**Examples**:
| Stage | Enemies | Type | XP |
|-------|---------|------|-----|
| 1 | 1 normal | Normal | 30 × 1 × 1.0 × 1.05 ≈ 31.5 |
| 10 | 1 boss | Boss | 30 × 1 × 10 × 1.5 = 450 |
| 50 | 5 normal | Normal | 30 × 5 × 1.0 × 3.5 = 525 |

### 2.5 Fidelis Drop Rates

```json
"fidelisRewards": {
  "normalWin": 10,
  "bossWin": 50
}
```

Fixed rewards per enemy type, no scaling applied.

### 2.6 Item Drop System

```json
"dropRates": {
  "beerDropChance": 0.10,
  "shotDropChance": 0.05,
  "bossDropMultiplier": 3.0
}
```

- Normal enemy: 10% beer, 5% shot
- Boss: 30% beer, 15% shot (3× multiplier)

### 2.7 Biome System

```json
"biomes": [
  {
    "name": "Forest",
    "stageMin": 1,
    "stageMax": 100,
    "enemySpritePath": "sprites/games/my-tuno/enemies/forest",
    "bossSpritePrefix": "boss_"
  },
  {
    "name": "Desert",
    "stageMin": 101,
    "stageMax": 200,
    "enemySpritePath": "sprites/games/my-tuno/enemies/desert",
    "bossSpritePrefix": "boss_"
  }
]
```

---

## 3. CONFIGURATION REFERENCE

### 3.1 Complete Config File Structure

All values live in `scaling.config.json` under the `"myTuno"` key and map to `MyTunoScalingConfiguration.cs`.

| Config Key | Type | Description |
|------------|------|-------------|
| `version` | string | Game version identifier |
| `description` | string | Game description text |
| `nextFeatures` | string | Upcoming features text |
| `baseStats` | object | Starting player stats |
| `levelScaling` | object | Per-level stat multiplier and XP base |
| `upgrades` | object | Per-stat upgrade config (bonus, cost, max) |
| `defenseK` | number | Defense formula constant (50) |
| `minDamage` | number | Guaranteed minimum damage (1) |
| `combat.criticalChanceCap` | number | Max critical chance (0.5) |
| `items.shotBuffMultiplier` | number | Shot item stat multiplier (1.20) |
| `beerDropChance` | number | Arena beer drop chance (0.5) |
| `battleRewards` | object | Arena Fidelis/XP reward config |
| `matchmaking` | object | Arena matchmaking parameters |
| `stageMode` | object | Stage mode full configuration |

### 3.2 Key C# Classes

| Class | File | Purpose |
|-------|------|---------|
| `MyTunoScalingConfiguration` | Configuration/MyTunoScalingConfiguration.cs | Root config model |
| `MyTunoUpgradeStat` | Configuration/MyTunoScalingConfiguration.cs | Per-stat upgrade config (bonusPerUpgrade, baseCost, maxUpgrades) |
| `MyTunoUpgrades` | Configuration/MyTunoScalingConfiguration.cs | Container for all 5 stat upgrades |
| `StageModeConfig` | Configuration/MyTunoScalingConfiguration.cs | Stage mode settings |
| `EnemyScalingConfig` | Configuration/MyTunoScalingConfiguration.cs | Per-stage enemy stat growth rates |
| `EncounterRulesConfig` | Configuration/MyTunoScalingConfiguration.cs | Boss frequency & enemy count rules |
| `BiomeConfig` | Configuration/MyTunoScalingConfiguration.cs | Biome stage ranges & sprite paths |
| `UpgradeService` | Services/UpgradeService.cs | Handles upgrade purchases with max level enforcement |

---

## 4. SUMMARY TABLE

### Arena Mode
| Component | Scaling Mechanism | Config Key | Configurable |
|-----------|------------------|-----------|--------------|
| Player Base Stats | Level × 0.1 | `baseStats`, `levelScaling` | ✓ |
| Stat Upgrades | BaseCost × (1+count)^1.5 | `upgrades` | ✓ |
| Max Upgrade Levels | Per-stat cap | `upgrades.*.maxUpgrades` | ✓ |
| Level Progression | Level × 100 XP | `levelScaling.xpPerLevelBase` | ✓ |
| Arena XP Rewards | Level diff × 0.08 | `battleRewards.xpScalingFactor` | ✓ |
| Fidelis Rewards | Base × (1 + level × 0.1) | `battleRewards.fidelisLevelMultiplier` | ✓ |
| Defense Mitigation | K/(K+DEF) formula | `defenseK`, `minDamage` | ✓ |
| Critical Chance Cap | 50% max | `combat.criticalChanceCap` | ✓ |
| Matchmaking | Power-rating balanced | `matchmaking` | ✓ |

### Stage Mode
| Component | Scaling Mechanism | Config Key | Configurable |
|-----------|------------------|-----------|--------------|
| Enemy Count | Offset-based rules per 10-stage block | `stageMode.encounterRules` | ✓ |
| Boss Frequency | Every Nth stage | `stageMode.encounterRules.bossEveryNStages` | ✓ |
| Enemy HP | Base × (1 + stage × 0.08) | `stageMode.enemyScaling.hpPerStage` | ✓ |
| Enemy Power | Base × (1 + stage × 0.05) | `stageMode.enemyScaling.powerPerStage` | ✓ |
| Enemy Speed | Base × (1 + stage × 0.03) | `stageMode.enemyScaling.speedPerStage` | ✓ |
| Enemy Defense | Base × (1 + stage × 0.04) | `stageMode.enemyScaling.defensePerStage` | ✓ |
| Stage XP Reward | 30 × count × type × (1 + stage×0.05) | `stageMode.baseStageXP` | ✓ |
| Item Drops | Base chance × boss multiplier | `stageMode.dropRates` | ✓ |
| Fidelis Rewards | Fixed per enemy type | `stageMode.fidelisRewards` | ✓ |
| Biomes | Stage range → sprite path | `stageMode.biomes` | ✓ |
