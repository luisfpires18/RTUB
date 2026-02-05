# My-Tuno Game Scaling System Documentation

## Overview

The My-Tuno game features two main combat modes with distinct scaling mechanics:
1. **Arena Mode**: Player vs Player matchmaking-based battles
2. **Stage Mode**: Single-player progressive difficulty with multiple enemies

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

- **Level Costs**: Predefined cost table in config (20 levels defined: 10, 25, 50, 100, 175, 275, 400, 550, 725, 925, 1150, 1400, 1675, 1975, 2300, 2650, 3025, 3425, 3850)

### 1.2 Upgrade System (Fidelis-based)

Each stat has independent upgrade scaling:

| Stat | Bonus/Upgrade | Base Cost | Notes |
|------|---------------|-----------|-------|
| **HP** | +10 | 5 Fidelis | Highest bonus, lowest cost |
| **Power** | +2 | 10 Fidelis | Linear scaling with damage |
| **Speed** | +1 | 15 Fidelis | Affects action bar fill rate |
| **Defense** | +2 | 12 Fidelis | Mitigation formula: `mult = 50/(50+defense)` |
| **Critical** | +0.005 | 20 Fidelis | Chance modifier (5% base for player) |

**Cost Progression**: Linear (cost increases with number of upgrades)

### 1.3 Arena Battle XP Rewards

#### Reward Types
```json
"battleRewards": {
  "winReward": 15,        // Fidelis on win
  "drawReward": 10,       // Fidelis on draw
  "reviveCost": 100,      // Fidelis to revive
  "restoreHPCost": 50     // Fidelis to restore HP
}
```

#### XP Calculation with Level Scaling
**Formula**: `XP = BaseXP × (1.0 + (OpponentLevel - PlayerLevel) × XpScalingFactor)`

- **BaseXP**: 50 (for wins), 35 (for draws) - hardcoded
- **XpScalingFactor**: 0.05 (5% per level difference)
- **Min Multiplier**: 0.2 (minimum 20% of base XP)
- **Max Multiplier**: 3.0 (maximum 300% of base XP)

**Examples**:
- Beat level 1 opponent at level 2: XP = 50 × (1.0 + (-1) × 0.05) = 47.5 → clamped to 10 (min)
- Beat level 5 opponent at level 2: XP = 50 × (1.0 + (5-2) × 0.05) = 57.5 XP
- Beat level 10 opponent at level 2: XP = 50 × (1.0 + (10-2) × 0.05) = 70 XP → clamped to 150 (max)

### 1.4 Defense Mechanic

**Damage Mitigation Formula**: `Multiplier = DefenseK / (DefenseK + Defense)`

- **DefenseK**: 50 (constant)
- **MinDamage**: 1 (guaranteed minimum damage)

**Examples**:
- Defense 5: mult = 50/(50+5) = 0.909 (9% mitigation)
- Defense 25: mult = 50/(50+25) = 0.667 (33% mitigation)
- Defense 50: mult = 50/(50+50) = 0.5 (50% mitigation)

### 1.5 Matchmaking

**Balanced Leaderboard System**:
- 4 opponents at/below player level (sorted by proximity - closest first)
- 4 opponents above player level (sorted by proximity - closest first)
- Sorted by wins descending in Arena tab, level in Level tab

---

## 2. STAGE MODE SCALING

### 2.1 Enemy Composition

#### Enemy Count Calculation
**Formula**: `Count = min(1 + (Stage / EnemyCountStageInterval), MaxEnemiesPerStage)`

- **EnemyCountStageInterval**: 20
- **MaxEnemiesPerStage**: 5

| Stage | Count |
|-------|-------|
| 1-19 | 1 |
| 20-39 | 2 |
| 40-59 | 3 |
| 60-79 | 4 |
| 80+ | 5 |

#### Enemy Type Distribution
- **Every 10th stage**: Boss encounter (stage 10, 20, 30, etc.)
- **Other stages**: Normal enemies

**Stage Offset Rules** (planned, not yet in config):
- Stages 1-9: 1 normal enemy
- Stages 10: 1 boss
- Stages 11-19: 2 normal enemies
- Stages 20: 1 boss
- *Pattern repeats*

### 2.2 Enemy Base Stats

```json
"baseEnemyStats": {
  "normal": { "hp": 50, "power": 8, "speed": 5, "defense": 3, "criticalChance": 0.05 },
  "miniBoss": { "hp": 200, "power": 15, "speed": 7, "defense": 8, "criticalChance": 0.10 },
  "boss": { "hp": 500, "power": 20, "speed": 8, "defense": 15, "criticalChance": 0.15 }
}
```

### 2.3 Enemy Stat Scaling Per Stage

**Formula**: `ScaledStat = BaseStat × (1.0 + (Stage - 1) × ScalingRate)`

| Stat | Per Stage Rate | Notes |
|------|----------------|-------|
| **HP** | 0.08 (8%) | Fastest growth |
| **Power** | 0.05 (5%) | Linear damage increase |
| **Speed** | 0.03 (3%) | Slower action bar |
| **Defense** | 0.04 (4%) | Increasing damage reduction |
| **Critical** | +0.002 | Additive, not multiplicative |

**Examples** (Normal Enemy):
- **Stage 1**: HP = 50, Power = 8, Speed = 5, Defense = 3
- **Stage 10**: HP = 50×(1+9×0.08) = 86, Power = 8×(1+9×0.05) = 11.6, Speed = 5×(1+9×0.03) = 6.35
- **Stage 50**: HP = 50×(1+49×0.08) = 246, Power = 8×(1+49×0.05) = 27.6, Speed = 5×(1+49×0.03) = 12.35

### 2.4 Stage Mode XP Rewards

#### Base Rewards
```json
"stageMode": {
  "baseStageXP": 30,
  "miniBossXPMultiplier": 3,
  "bossXPMultiplier": 10
}
```

**Formula**: `XP = BaseStageXP × EnemyCount × EnemyTypeMultiplier × StageScaling`

- **BaseStageXP**: 30
- **EnemyCount**: Number of enemies defeated (1-5)
- **EnemyTypeMultiplier**: 
  - Normal: 1.0
  - Mini-Boss: 3.0
  - Boss: 10.0
- **StageScaling**: `1.0 + (Stage × 0.05)` — 5% per stage
  - Stage 1: 1.05x
  - Stage 10: 1.5x
  - Stage 50: 3.5x

**Examples**:
- Stage 1, 1 normal enemy: 30 × 1 × 1.0 × 1.05 = ~31.5 XP
- Stage 10, 1 boss: 30 × 1 × 10 × 1.5 = 450 XP
- Stage 50, 5 normal enemies: 30 × 5 × 1.0 × 3.5 = 525 XP

### 2.5 Fidelis Drop Rates

```json
"fidelisRewards": {
  "normalWin": 10,
  "miniBossWin": 25,
  "bossWin": 50
}
```

Fixed rewards per enemy type, no scaling applied.

### 2.6 Item Drop System

```json
"dropRates": {
  "beerDropChance": 0.10,        // 10% per normal enemy
  "shotDropChance": 0.05,        // 5% per normal enemy
  "bossDropMultiplier": 3.0,     // 3x multiplier for boss
  "miniBossDropMultiplier": 2.0  // 2x multiplier for mini-boss
}
```

---

## 3. MISSING CONFIGURATION ITEMS

### 3.1 Stage Offset Rules (MISSING)
**Location**: `EncounterRulesConfig` is defined but not populated in `scaling.config.json`

**What's Missing**:
```csharp
public class EncounterRulesConfig
{
    public int BossEveryNStages { get; set; } = 10;
    public List<EnemyCountRule> EnemyCountByStageOffset { get; set; } = new();
}
```

**Should Be Added to Config**:
```json
"encounterRules": {
  "bossEveryNStages": 10,
  "enemyCountByStageOffset": [
    { "from": 1, "to": 9, "count": 1 },
    { "from": 10, "to": 10, "count": 1 },
    { "from": 11, "to": 19, "count": 2 },
    { "from": 20, "to": 20, "count": 1 },
    { "from": 21, "to": 29, "count": 3 },
    { "from": 30, "to": 30, "count": 1 }
  ]
}
```

### 3.2 Biome Configuration (MISSING)
**Location**: `BiomeConfig` is defined but not populated

**What's Missing**:
```csharp
public class BiomeConfig
{
    public string Name { get; set; }
    public int StageMin { get; set; }
    public int StageMax { get; set; }
    public string EnemySpritePath { get; set; }
    public string BossSpritePrefix { get; set; }
}
```

**Should Be Added**:
```json
"biomes": [
  {
    "name": "Forest",
    "stageMin": 1,
    "stageMax": 25,
    "enemySpritePath": "sprites/games/my-tuno/enemies/forest",
    "bossSpritePrefix": "boss_"
  },
  {
    "name": "Cave",
    "stageMin": 26,
    "stageMax": 50,
    "enemySpritePath": "sprites/games/my-tuno/enemies/cave",
    "bossSpritePrefix": "boss_"
  }
]
```

### 3.3 Stat Scaling Configuration (MISSING)
**Location**: `StageScalingConfig` is defined but not in JSON config

**What's Missing**:
```csharp
public class StageScalingConfig
{
    public double HpGrowthPerStage { get; set; } = 0.06;
    public double DamageGrowthPerStage { get; set; } = 0.05;
    public double ArmorGrowthPerStage { get; set; } = 0.03;
    public double BossMultiplier { get; set; } = 2.5;
}
```

**Should Be Added**:
```json
"scaling": {
  "hpGrowthPerStage": 0.06,
  "damageGrowthPerStage": 0.05,
  "armorGrowthPerStage": 0.03,
  "bossMultiplier": 2.5
}
```

### 3.4 Mini-Boss Introduction (MISSING)
**What's Missing**: Configuration does not define when mini-bosses should appear in stage progression

**Suggested Addition**:
```json
"enemyIntroductionConfig": {
  "miniBosses": {
    "firstAppearanceStage": 30,
    "appearanceFrequency": 5,
    "notes": "Mini-boss appears at stages: 30, 35, 40, 45, etc."
  }
}
```

### 3.5 Player Speed Stat Integration (MISSING)
**Issue**: Speed stat is configured but its impact on game mechanics is incomplete

**Current State**:
- Speed stat can be upgraded
- Speed is used in battle calculation (affects action bar)
- **Missing**: Configuration values for:
  - Base action bar fill time (currently hardcoded to ~3.5 seconds)
  - Speed-to-action-time conversion formula
  - Minimum/maximum action times

**Should Be Added**:
```json
"speedMechanics": {
  "baseActionTime": 3.5,
  "speedToActionTimeFormula": "baseActionTime / (1 + speed * 0.05)",
  "minActionTime": 0.5,
  "maxActionTime": 10.0
}
```

### 3.6 Aerial Enemy Placement (PARTIALLY MISSING)
**Current**: 
- `PlacementType` enum exists (Terrestrial=0, Aerial=1)
- JavaScript handles positioning
- **Missing**: Configuration for which enemies can be aerial, scaling rules for aerial enemies

**Suggested Addition**:
```json
"aerialEnemyConfig": {
  "enableAerialEnemies": true,
  "aerialEnemyChance": 0.30,
  "firstAerialStage": 20,
  "aerialEnemyStats": {
    "speedMultiplier": 1.2,
    "defenseMultiplier": 0.9,
    "evasionBonus": 0.05
  }
}
```

### 3.7 Boss Variant Configuration (MISSING)
**Current**: Single boss type per biome

**Missing**:
- Boss variants by stage range
- Boss stat scaling multipliers
- Boss special abilities
- Boss transition points (stage 10 vs stage 50 boss difficulty)

**Suggested Addition**:
```json
"bossVariants": [
  {
    "stageRange": [10, 30],
    "name": "Forest King",
    "statMultiplier": 2.0,
    "sprite": "/sprites/games/my-tuno/enemies/boss_forest_king.png"
  },
  {
    "stageRange": [31, 60],
    "name": "Ancient Guardian",
    "statMultiplier": 3.5,
    "sprite": "/sprites/games/my-tuno/enemies/boss_ancient.png"
  }
]
```

---

## 4. CURRENT HARDCODED VALUES

Items that should be moved to configuration:

| Item | Current Value | Location | Recommendation |
|------|---------------|----------|-----------------|
| Base Arena Win XP | 50 | BattleService.cs | Add to `battleRewards` |
| Base Arena Draw XP | 35 | BattleService.cs | Add to `battleRewards` |
| Action Bar Time | 3.5 seconds | StageService.cs | Add to `speedMechanics` |
| Stage Scaling Rate | 5% per stage | StageService.cs | Add to `stageMode.scaling` |
| Beer Drop Chance | 50% | MyTunoScalingConfiguration.cs | ✓ Configured |
| Defense Constant K | 50 | MyTunoScalingConfiguration.cs | ✓ Configured |
| Min Damage | 1 | MyTunoScalingConfiguration.cs | ✓ Configured |

---

## 5. SUMMARY TABLE

### Arena Mode
| Component | Scaling Mechanism | Config Key | Configurable |
|-----------|------------------|-----------|--------------|
| Player Base Stats | Level × 0.1 | `baseStats`, `levelScaling` | ✓ |
| Stat Upgrades | Linear cost increase | `upgrades` | ✓ |
| Level Progression | Level × 100 XP | `levelScaling` | ✓ |
| Arena XP Rewards | Level difference × 0.05 | `battleRewards.xpScalingFactor` | ✓ |
| Defense Mitigation | K/(K+DEF) formula | `defenseK`, `minDamage` | ✓ |
| Matchmaking | Level-based balance | (hardcoded) | ✗ |

### Stage Mode
| Component | Scaling Mechanism | Config Key | Configurable |
|-----------|------------------|-----------|--------------|
| Enemy Count | 1 + stage/20 | `stageMode.enemyCountStageInterval` | ✓ |
| Enemy HP | Base × (1 + stage × 0.08) | `stageMode.enemyScaling.hpPerStage` | ✓ |
| Enemy Power | Base × (1 + stage × 0.05) | `stageMode.enemyScaling.powerPerStage` | ✓ |
| Enemy Speed | Base × (1 + stage × 0.03) | `stageMode.enemyScaling.speedPerStage` | ✓ |
| Enemy Defense | Base × (1 + stage × 0.04) | `stageMode.enemyScaling.defensePerStage` | ✓ |
| Stage XP Reward | 30 × count × type × (1 + stage×0.05) | `stageMode.baseStageXP` | ⚠ (hardcoded rate) |
| Boss Multiplier | ✗ Not used | (missing) | ✗ |
| Stage Offset Rules | (hardcoded: stage % 10) | (missing) | ✗ |

---

## 6. RECOMMENDATIONS FOR COMPLETION

**Priority 1 (High Impact)**:
1. Add `EncounterRulesConfig` to JSON for dynamic boss/enemy patterns
2. Add `StageScalingConfig` to JSON for alternative scaling rules
3. Implement boss variant system with proper stat multipliers

**Priority 2 (Medium Impact)**:
1. Add speed mechanics configuration (action time formula)
2. Add biome progression system
3. Move hardcoded XP values to config

**Priority 3 (Enhancement)**:
1. Add aerial enemy configuration
2. Add boss special ability system
3. Add difficulty tiers or prestige system

