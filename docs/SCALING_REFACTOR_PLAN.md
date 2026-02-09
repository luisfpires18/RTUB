# Scaling Refactor Plan — "Unity-Style" Simplification

> **Status**: Phase 1 + 2 IMPLEMENTED (feature-flagged via `useUnifiedScaling`).  
> **Goal**: Replace the current 6+ overlapping multiplier layers with a single, predictable,  
> fun-to-tune scaling system like you'd build in Unity with AnimationCurves + a single difficulty coefficient.

---

## Table of Contents

1. [What's Wrong Today](#1-whats-wrong-today)
2. [Design Principles (Unity-Style)](#2-design-principles-unity-style)
3. [The New Model — "Single Curve + Biome Flavor"](#3-the-new-model--single-curve--biome-flavor)
4. [Config Schema (Before → After)](#4-config-schema-before--after)
5. [Enemy Stat Formula (Single Source of Truth)](#5-enemy-stat-formula-single-source-of-truth)
6. [Reward Formula (Unified)](#6-reward-formula-unified)
7. [Biome Config (Simplified)](#7-biome-config-simplified)
8. [Migration Steps](#8-migration-steps)
9. [Files to Change](#9-files-to-change)
10. [Test Plan](#10-test-plan)
11. [Risk Assessment](#11-risk-assessment)

---

## 1. What's Wrong Today

### The Multiplier Spaghetti

Currently, to determine a Stage 500 Tropical enemy's HP you need to compute:

```
baseHP × polynomialScaling(stage, hpPerStage, exponent)
       × hpGrowthPerStage(stage)      ← SEPARATE redundant layer
       × difficultyMultiplier(biome=5) ← flat integer multiplier
       × bossMultiplier(if boss=1.2)
```

That's **4 multiplicative layers**. They compound exponentially and are nearly impossible to
hand-tune. Changing one breaks the curve of another.

| Problem | Details |
|---|---|
| **Two competing enemy scaling configs** | `enemyScaling.*PerStage` AND `scaling.hpGrowthPerStage` both scale enemy stats — redundant |
| **Integer biome multipliers (1–15)** | `difficultyMultiplier` linearly ramps 1→15. At stage 950 (Dark, ×10), enemies are 10× stronger from biome ALONE, stacked on polynomial scaling |
| **Docs vs reality mismatch** | `STAGE_BALANCE_PREDICTION.md` proposed 1.0→2.5 biome range, actual config has 1→15 |
| **Fidelis has its own scaling stack** | `stageRewardScalingFactor` × `fidelisLevelMultiplier` × cap — separate system with separate tuning |
| **No single place to see the curve** | You can't graph "enemy power at stage X" without running 3 formulas |
| **Per-stat exponents differ** | HP, Power, Speed, Defense each have different growth rates and exponents — hard to reason about |

### Why This Wouldn't Happen in Unity

In Unity, you'd create:
- **One AnimationCurve** for enemy difficulty (x=stage, y=multiplier)
- **One AnimationCurve** for rewards
- Each biome gets a **small flavor modifier** (1.0–1.3) at most
- You'd preview the curve in the inspector and know exactly what stage 500 looks like

---

## 2. Design Principles (Unity-Style)

1. **Single Difficulty Curve** — ONE formula determines enemy scaling. Not 2, not 4. ONE.
2. **Biome = Flavor, Not Power** — Biomes change aesthetics + add modest stat shifts (max 1.5×, not 15×). Use biomes to shift stat DISTRIBUTIONS (e.g., "Swamp enemies are slow but tanky") rather than flat multipliers.
3. **Rewards Follow Difficulty** — ONE reward curve tied to the same stage number. No separate stacking. If difficulty went up, rewards go up proportionally.
4. **Config is Graphable** — A designer should be able to plug the formula into a spreadsheet and instantly see the full curve from stage 1 to 1000.
5. **Stat Identity** — Each stat grows at the SAME rate (from the unified curve), but enemies have different BASE stats. No per-stat exponent tuning.
6. **Boss = Simple Multiplier** — Boss is just `curve × bossMultiplier`. One layer, not compounding.

---

## 3. The New Model — "Single Curve + Biome Flavor"

### Enemy Stats

```
EnemyStat(stage) = BaseStat × DifficultyCurve(stage) × BiomeFlavor × BossMult
```

Where:
- **`BaseStat`** = per-enemy-type base (normal: 40 HP, 5 Power, etc.)
- **`DifficultyCurve(stage)`** = `1 + scalingRate × (stage - 1) ^ growthExponent`
  - Single `scalingRate` (e.g., 0.12)
  - Single `growthExponent` (e.g., 1.15)
  - ALL stats use the SAME curve
- **`BiomeFlavor`** = small per-biome modifier, max 1.5. Optional per-stat overrides for "themed" biomes
- **`BossMult`** = 1.2 (unchanged, only applies to boss encounters)

### Rewards (Fidelis + XP)

```
FidelisReward(stage) = BaseReward × RewardCurve(stage) × LevelBonus
XPReward(stage)      = XPBase × √stage × enemyCount × levelDiffPenalty
```

Where:
- **`RewardCurve(stage)`** = `1 + rewardRate × (stage - 1) ^ rewardExponent` — mirrors difficulty but can have its own tuning
- **`LevelBonus`** = `min(1 + (level - 1) × 0.04, 1.8)` — unchanged, simple cap
- No separate `stageRewardScalingFactor` + `maxStageRewardMultiplier` + `fidelisLevelMultiplier` stack. Just ONE curve.

---

## 4. Config Schema (Before → After)

### BEFORE (current — 25+ config values for enemy scaling alone)

```jsonc
"stageMode": {
  // Scaling layer 1: per-stat rates
  "enemyScaling": {
    "hpPerStage": 0.15,
    "powerPerStage": 0.15,
    "speedPerStage": 0.008,
    "defensePerStage": 0.12,
    "criticalChancePerStage": 0.005
  },
  // Scaling layer 2: REDUNDANT growth rates
  "scaling": {
    "hpGrowthPerStage": 0.10,
    "damageGrowthPerStage": 0.10,
    "armorGrowthPerStage": 0.40,
    "bossMultiplier": 1.2
  },
  // Scaling layer 3: per-biome integer multipliers (1-15!)
  "biomes": [
    { "name": "Forest", "difficultyMultiplier": 1 },
    { "name": "Swamp", "difficultyMultiplier": 2 },
    // ... up to 15
  ],
  // Reward layer 1
  "stageRewardScalingFactor": 0.003,
  "maxStageRewardMultiplier": 6.0,
  // Reward layer 2
  "fidelisLevelMultiplier": 0.04,
  "fidelisLevelMultiplierCap": 1.8,
  // + separate XP formula with its own params
}
```

### AFTER (proposed — ~10 config values)

```jsonc
"stageMode": {
  // === ONE difficulty curve for ALL enemy stats ===
  "difficultyCurve": {
    "scalingRate": 0.12,
    "growthExponent": 1.15
  },

  // === Base enemy stats (unchanged) ===
  "baseEnemyStats": {
    "normal": { "hp": 40, "power": 5, "speed": 5, "defense": 2, "criticalChance": 0.04 },
    "boss":   { "hp": 80, "power": 8, "speed": 3, "defense": 4, "criticalChance": 0.10 }
  },
  "bossMultiplier": 1.2,

  // === ONE reward curve ===
  "rewardCurve": {
    "scalingRate": 0.08,
    "growthExponent": 1.10,
    "levelBonusPerLevel": 0.04,
    "levelBonusCap": 1.8
  },

  // === Fidelis base rewards (unchanged) ===
  "fidelisRewards": {
    "normalWin": 12,
    "bossWin": 40
  },

  // === XP (simplified, keep current enemy-level system) ===
  "xp": {
    "xpPerEnemyLevel": 12,
    "enemyLevelXPPower": 0.5,
    "bossXPMultiplier": 8,
    "xpLevelPenaltyRate": 0.015,
    "minXPLevelMultiplier": 0.05
  },

  // === Biomes — flavor only, small multipliers ===
  "biomes": [
    {
      "name": "Forest",
      "stageMin": 1, "stageMax": 100,
      "difficultyMultiplier": 1.0,
      "statFlavor": { "hp": 1.0, "power": 1.0, "speed": 1.0, "defense": 1.0 },
      "rewardMultiplier": 1.0,
      "enemySpritePath": "sprites/games/my-tuno/enemies/forest",
      "bossSpritePrefix": "boss_"
    },
    {
      "name": "Swamp",
      "stageMin": 101, "stageMax": 200,
      "difficultyMultiplier": 1.05,
      "statFlavor": { "hp": 1.2, "power": 0.9, "speed": 0.8, "defense": 1.1 },
      "rewardMultiplier": 1.1,
      "enemySpritePath": "sprites/games/my-tuno/enemies/swamp",
      "bossSpritePrefix": "boss_"
    },
    {
      "name": "Mountains",
      "stageMin": 201, "stageMax": 300,
      "difficultyMultiplier": 1.10,
      "statFlavor": { "hp": 1.0, "power": 1.1, "speed": 0.9, "defense": 1.2 },
      "rewardMultiplier": 1.2,
      "enemySpritePath": "sprites/games/my-tuno/enemies/mountains",
      "bossSpritePrefix": "boss_"
    },
    {
      "name": "Snowy",
      "stageMin": 301, "stageMax": 400,
      "difficultyMultiplier": 1.15,
      "statFlavor": { "hp": 1.0, "power": 1.0, "speed": 0.7, "defense": 1.3 },
      "rewardMultiplier": 1.3,
      "enemySpritePath": "sprites/games/my-tuno/enemies/snowy",
      "bossSpritePrefix": "boss_"
    },
    {
      "name": "Tropical",
      "stageMin": 401, "stageMax": 500,
      "difficultyMultiplier": 1.20,
      "statFlavor": { "hp": 0.9, "power": 1.2, "speed": 1.2, "defense": 0.9 },
      "rewardMultiplier": 1.4,
      "enemySpritePath": "sprites/games/my-tuno/enemies/tropical",
      "bossSpritePrefix": "boss_"
    },
    {
      "name": "Caverns",
      "stageMin": 501, "stageMax": 600,
      "difficultyMultiplier": 1.25,
      "statFlavor": { "hp": 1.3, "power": 1.0, "speed": 0.8, "defense": 1.2 },
      "rewardMultiplier": 1.5,
      "enemySpritePath": "sprites/games/my-tuno/enemies/caverns",
      "bossSpritePrefix": "boss_"
    },
    {
      "name": "Desert",
      "stageMin": 601, "stageMax": 700,
      "difficultyMultiplier": 1.30,
      "statFlavor": { "hp": 0.9, "power": 1.3, "speed": 1.1, "defense": 0.8 },
      "rewardMultiplier": 1.6,
      "enemySpritePath": "sprites/games/my-tuno/enemies/forest",
      "bossSpritePrefix": "boss_"
    },
    {
      "name": "Volcanic",
      "stageMin": 701, "stageMax": 800,
      "difficultyMultiplier": 1.35,
      "statFlavor": { "hp": 1.1, "power": 1.4, "speed": 0.9, "defense": 1.0 },
      "rewardMultiplier": 1.7,
      "enemySpritePath": "sprites/games/my-tuno/enemies/forest",
      "bossSpritePrefix": "boss_"
    },
    {
      "name": "Ruins",
      "stageMin": 801, "stageMax": 900,
      "difficultyMultiplier": 1.40,
      "statFlavor": { "hp": 1.2, "power": 1.2, "speed": 1.0, "defense": 1.1 },
      "rewardMultiplier": 1.8,
      "enemySpritePath": "sprites/games/my-tuno/enemies/ruins",
      "bossSpritePrefix": "boss_"
    },
    {
      "name": "Dark",
      "stageMin": 901, "stageMax": 1000,
      "difficultyMultiplier": 1.45,
      "statFlavor": { "hp": 1.1, "power": 1.3, "speed": 1.1, "defense": 1.2 },
      "rewardMultiplier": 2.0,
      "enemySpritePath": "sprites/games/my-tuno/enemies/forest",
      "bossSpritePrefix": "boss_"
    },
    {
      "name": "Void",
      "stageMin": 1001, "stageMax": 999999999,
      "difficultyMultiplier": 1.50,
      "statFlavor": { "hp": 1.2, "power": 1.3, "speed": 1.2, "defense": 1.3 },
      "rewardMultiplier": 2.5,
      "enemySpritePath": "sprites/games/my-tuno/enemies/void",
      "bossSpritePrefix": "boss_"
    }
  ]
}
```

### What Changed

| Removed | Replaced By |
|---|---|
| `enemyScaling.hpPerStage` (0.15) | `difficultyCurve.scalingRate` (0.12) — shared |
| `enemyScaling.powerPerStage` (0.15) | Same curve |
| `enemyScaling.speedPerStage` (0.008) | Same curve (speed base is already low) |
| `enemyScaling.defensePerStage` (0.12) | Same curve |
| `enemyScaling.criticalChancePerStage` | Linear from base, tiny growth — keep simple |
| `scaling.hpGrowthPerStage` (0.10) | **Removed** — was redundant with `enemyScaling` |
| `scaling.damageGrowthPerStage` (0.10) | **Removed** — was redundant |
| `scaling.armorGrowthPerStage` (0.40) | **Removed** — was redundant |
| `stageRewardScalingFactor` + `maxStageRewardMultiplier` | `rewardCurve` — single polynomial |
| `fidelisLevelMultiplier` + `fidelisLevelMultiplierCap` | `rewardCurve.levelBonusPerLevel` + `levelBonusCap` |
| Biome `difficultyMultiplier` 1→15 | Biome `difficultyMultiplier` 1.0→1.5 |
| (nothing) | **New**: `statFlavor` per biome — gives biomes identity |
| (nothing) | **New**: `rewardMultiplier` per biome — replaces need for `fidelisMultiplier` |

**Net result**: ~25 scaling config values → ~10 + per-biome flavor. ONE formula to understand.

---

## 5. Enemy Stat Formula (Single Source of Truth)

### The One Formula

```csharp
/// <summary>
/// Single difficulty curve used for ALL enemy stats.
/// Equivalent to a Unity AnimationCurve evaluated at a stage number.
/// </summary>
public static double DifficultyCurve(int stage, double scalingRate, double growthExponent)
{
    if (stage <= 1) return 1.0;
    return 1.0 + scalingRate * Math.Pow(stage - 1, growthExponent);
}

/// <summary>
/// Calculate any enemy stat at a given stage.
/// </summary>
public static int CalculateEnemyStat(
    double baseStat,
    int stage,
    double scalingRate,
    double growthExponent,
    double biomeDifficultyMultiplier,  // 1.0 – 1.5
    double biomeStatFlavor,            // 0.7 – 1.4 (per-stat, per-biome)
    double bossMult)                   // 1.0 for normal, 1.2 for boss
{
    var curve = DifficultyCurve(stage, scalingRate, growthExponent);
    return Math.Max(1, (int)(baseStat * curve * biomeDifficultyMultiplier * biomeStatFlavor * bossMult));
}
```

### Predicted Enemy HP Curve (scalingRate=0.12, exponent=1.15)

| Stage | Curve Value | Normal HP (base 40) | With Biome 1.0 | With Biome 1.3 | With Biome 1.5 |
|:---:|:---:|:---:|:---:|:---:|:---:|
| 1 | 1.0 | 40 | 40 | 52 | 60 |
| 10 | 2.44 | 98 | 98 | 127 | 146 |
| 50 | 15.5 | 620 | 620 | 806 | 930 |
| 100 | 35.5 | 1,420 | 1,420 | 1,846 | 2,130 |
| 200 | 82.0 | 3,280 | 3,280 | 4,264 | 4,920 |
| 500 | 247 | 9,880 | 9,880 | 12,844 | 14,820 |
| 1000 | 568 | 22,720 | 22,720 | 29,536 | 34,080 |

Compare to current system at stage 500 Tropical (×5): 40 × polynomial × **5** = massively inflated.
New system at stage 500 Tropical (×1.2): 40 × 247 × **1.2** = 11,856. Smooth and predictable.

### Player vs Enemy Curve Comparison

The player also uses a polynomial: `1 + 0.20 × (level - 1)^1.20` (with `statGrowthExponent=0.20`).

At stage 500, player is ~level 109:
- Player curve: `1 + 0.20 × 108^1.20` ≈ 62.3 → Player HP = 100 × 62.3 = 6,230
- Enemy curve: 247 → Normal Enemy HP = 40 × 247 × 1.2 = 11,856

Ratio: Enemy has ~1.9× HP but 1/2 base → effective ~0.95×. Player is roughly matched at equal
investment, which is exactly the Unity feel: enemies are beatable but challenging. Multi-enemy
stages are the real walls.

---

## 6. Reward Formula (Unified)

### Fidelis

```csharp
public static double RewardCurve(int stage, double scalingRate, double growthExponent)
{
    if (stage <= 1) return 1.0;
    return 1.0 + scalingRate * Math.Pow(stage - 1, growthExponent);
}

public static double CalculateFidelisReward(
    double baseReward,       // normalWin=12 or bossWin=40
    int stage,
    int enemyCount,
    double rewardScalingRate, // 0.08
    double rewardExponent,    // 1.10
    int playerLevel,
    double levelBonusPerLevel, // 0.04
    double levelBonusCap,      // 1.8
    double biomeRewardMultiplier) // 1.0 – 2.5
{
    var curve = RewardCurve(stage, rewardScalingRate, rewardExponent);
    var levelBonus = Math.Min(1.0 + (playerLevel - 1) * levelBonusPerLevel, levelBonusCap);
    return baseReward * enemyCount * curve * levelBonus * biomeRewardMultiplier;
}
```

**This replaces the need for a separate `fidelisMultiplier`** — `biomeRewardMultiplier` in each
biome config handles it naturally. Harder biomes give 1.0× to 2.5× Fidelis. Simple.

### XP (Keep Current — Already Clean)

The enemy-level XP system (`XP = 12 × √stage × count × bossMult × levelDiffPenalty`) is already
well-designed. No changes needed — it's the one part that was Unity-style from the start.

---

## 7. Biome Config (Simplified)

### Biome Identity via `statFlavor`

Instead of biomes being pure difficulty multipliers ("Forest = 1, Void = 15"),
biomes now give enemies **character**:

| Biome | Identity | HP | Power | Speed | Defense |
|---|---|:---:|:---:|:---:|:---:|
| **Forest** | Balanced | 1.0 | 1.0 | 1.0 | 1.0 |
| **Swamp** | Tanky & slow | 1.2 | 0.9 | 0.8 | 1.1 |
| **Mountains** | Armored | 1.0 | 1.1 | 0.9 | 1.2 |
| **Snowy** | Frozen, slow, armored | 1.0 | 1.0 | 0.7 | 1.3 |
| **Tropical** | Glass cannon fast | 0.9 | 1.2 | 1.2 | 0.9 |
| **Caverns** | Cave troll — tanky | 1.3 | 1.0 | 0.8 | 1.2 |
| **Desert** | Scorching — high power | 0.9 | 1.3 | 1.1 | 0.8 |
| **Volcanic** | Burn damage | 1.1 | 1.4 | 0.9 | 1.0 |
| **Ruins** | Undead all-rounder | 1.2 | 1.2 | 1.0 | 1.1 |
| **Dark** | Shadow assassin | 1.1 | 1.3 | 1.1 | 1.2 |
| **Void** | Eldritch horror | 1.2 | 1.3 | 1.2 | 1.3 |

This creates **gameplay variety** without exponential difficulty spikes. Players adapt their
strategy per biome rather than just needing "more numbers".

### `BiomeConfig` C# Model (New)

```csharp
public class BiomeConfig
{
    public string Name { get; set; } = "Forest";
    public int StageMin { get; set; } = 1;
    public int StageMax { get; set; } = 100;
    public string EnemySpritePath { get; set; } = "sprites/games/my-tuno/enemies/forest";
    public string BossSpritePrefix { get; set; } = "boss_";

    /// <summary>
    /// Overall difficulty modifier for this biome. Range: 1.0 – 1.5.
    /// Applied uniformly to all enemy stats on top of the difficulty curve.
    /// </summary>
    public double DifficultyMultiplier { get; set; } = 1.0;

    /// <summary>
    /// Per-stat flavor modifiers that give each biome a unique combat identity.
    /// Values typically range 0.7 – 1.4. Multiplied with DifficultyMultiplier.
    /// </summary>
    public BiomeStatFlavor StatFlavor { get; set; } = new();

    /// <summary>
    /// Fidelis/reward multiplier for this biome. Range: 1.0 – 2.5.
    /// Harder/deeper biomes reward more Fidelis.
    /// </summary>
    public double RewardMultiplier { get; set; } = 1.0;
}

public class BiomeStatFlavor
{
    public double Hp { get; set; } = 1.0;
    public double Power { get; set; } = 1.0;
    public double Speed { get; set; } = 1.0;
    public double Defense { get; set; } = 1.0;
}
```

---

## 8. Migration Steps

### Phase 1: Config & Model (Non-Breaking)

| # | Task | Files | Risk |
|:---:|---|---|---|
| 1 | Add `DifficultyCurveConfig` class to `MyTunoScalingConfiguration.cs` | Configuration/ | None — additive |
| 2 | Add `RewardCurveConfig` class to `MyTunoScalingConfiguration.cs` | Configuration/ | None — additive |
| 3 | Add `BiomeStatFlavor` class + `RewardMultiplier` to `BiomeConfig` | Configuration/ | None — additive, default 1.0 |
| 4 | Add `difficultyCurve` and `rewardCurve` sections to `scaling.config.json` | scaling.config.json | None — new keys, old ones still read |
| 5 | Add `statFlavor` and `rewardMultiplier` to each biome in JSON | scaling.config.json | None — defaults to 1.0 |

### Phase 2: New Scaling Logic (Behind Feature Flag)

| # | Task | Files | Risk |
|:---:|---|---|---|
| 6 | Create `UnifiedScalingService` with `DifficultyCurve()` + `CalculateEnemyStat()` | Services/ | None — new service |
| 7 | Add `useUnifiedScaling` bool to config | scaling.config.json | None — defaults false |
| 8 | In `StageService.CreateTemporaryEnemyCharacter()`, branch on flag | StageService.cs | Low — flag off = no change |
| 9 | In Fidelis reward calculation, branch on flag for `RewardCurve` | StageService.cs or reward service | Low — flag off = no change |

### Phase 3: Cutover & Cleanup

| # | Task | Files | Risk |
|:---:|---|---|---|
| 10 | Enable `useUnifiedScaling` flag, playtest | scaling.config.json | Medium — balance testing needed |
| 11 | Remove old `enemyScaling.*PerStage` config reads | Configuration/, Services/ | Low after flag validation |
| 12 | Remove old `scaling.hpGrowthPerStage/damageGrowthPerStage/armorGrowthPerStage` | Configuration/, Services/ | Low after flag validation |
| 13 | Remove `stageRewardScalingFactor` + `maxStageRewardMultiplier` | Configuration/, Services/ | Low after flag validation |
| 14 | Flatten biome `difficultyMultiplier` values to 1.0–1.5 range | scaling.config.json | Part of cutover |
| 15 | Update all docs (`MY_TUNO_SCALING_SYSTEM.md`, `STAGE_BALANCE_PREDICTION.md`, `FIDELIS_EARNINGS_PREDICTION.md`) | docs/ | None |
| 16 | Update tests for new formula | tests/ | None |

### Phase 4: Optional Enhancements

| # | Task | Notes |
|:---:|---|---|
| 17 | Add a "Scaling Preview" debug tool (admin page?) | Graph difficulty curve + rewards in browser for quick tuning |
| 18 | Consider piecewise curves for fine-grained control | E.g., different exponent after stage 500 for endgame feel |
| 19 | Unity AnimationCurve export/import if you ever port | Store curve keyframes in JSON |

---

## 9. Files to Change

### Core Changes

| File | What Changes |
|---|---|
| `src/RTUB.Application/Configuration/MyTunoScalingConfiguration.cs` | Add `DifficultyCurveConfig`, `RewardCurveConfig`, `BiomeStatFlavor`; modify `BiomeConfig` |
| `src/RTUB.Application/Services/StageService.cs` | Replace multi-layer scaling with single `DifficultyCurve()` call |
| `src/RTUB.Application/Services/StageBiomeService.cs` | Update `GetDifficultyMultiplier()` to also return `StatFlavor` and `RewardMultiplier` |
| `src/RTUB.Application/Interfaces/IStageBiomeService.cs` | Add `GetBiomeScaling()` method returning all biome modifiers |
| `src/RTUB.Web/scaling.config.json` | New schema with `difficultyCurve`, `rewardCurve`, per-biome `statFlavor`/`rewardMultiplier` |

### Test Changes

| File | What Changes |
|---|---|
| `tests/RTUB.Application.Tests/Services/StageBiomeServiceTests.cs` | Test new `GetBiomeScaling()`, verify small multipliers |
| `tests/RTUB.Application.Tests/Services/StageServiceTests.cs` | Verify enemy stats match single-curve formula |
| New: `tests/RTUB.Application.Tests/Services/UnifiedScalingServiceTests.cs` | Unit tests for `DifficultyCurve()` and `CalculateEnemyStat()` |

### Doc Changes

| File | What Changes |
|---|---|
| `docs/MY_TUNO_SCALING_SYSTEM.md` | Rewrite Stage Mode section with single formula |
| `docs/STAGE_BALANCE_PREDICTION.md` | Update all tables with new values |
| `docs/FIDELIS_EARNINGS_PREDICTION.md` | Update reward tables |

---

## 10. Test Plan

### Unit Tests

1. `DifficultyCurve(1) == 1.0`
2. `DifficultyCurve(100, 0.12, 1.15)` matches hand-calculated value
3. `CalculateEnemyStat()` with biome flavor 1.0 == without flavor
4. `CalculateEnemyStat()` with boss mult 1.2 == baseStat × curve × 1.2
5. `RewardCurve()` monotonically increasing
6. Biome `RewardMultiplier` correctly scales Fidelis rewards
7. All biome `DifficultyMultiplier` values between 1.0 and 1.5
8. All biome `StatFlavor` values between 0.5 and 2.0

### Integration Tests

1. Stage 10 boss beatable at player level 6 with shot buff
2. Stage 100 boss requires ~level 33 + upgrades
3. Stage 500 normal enemies matchable 1v1 at ~level 109
4. Fidelis economy: stage 100 completion yields enough for basic upgrades
5. XP curve: player reaches level ~33 by stage 100

### Balance Validation

1. Graph new difficulty curve vs player curve — gap should be <2× through stage 200
2. Verify no biome transition creates a sudden spike >20%
3. Verify endgame (stage 700+) is still losing inevitably but slowly
4. Compare new Fidelis income vs upgrade costs — player should never feel "stuck" before stage 50

---

## 11. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|---|:---:|:---:|---|
| Balance feels too easy after removing biome ×15 | Medium | Medium | Feature flag — can revert. Tune `scalingRate`/`growthExponent` |
| Existing player progression disrupted | Low | High | Migrations preserve player data. Only enemy scaling/rewards change |
| Formula too simple for endgame variety | Low | Low | `statFlavor` per biome adds variety. Can add piecewise curves later |
| Test coverage gaps | Medium | Medium | Write comprehensive unit tests during Phase 2 |
| Config migration breaks existing JSON readers | Low | Medium | All new fields have defaults. Old fields kept until Phase 3 |

---

## Summary

**Before**: 6 layered multipliers, 25+ config values, unpredictable curves, docs don't match reality.

**After**: 1 difficulty curve, 1 reward curve, small biome flavors, ~10 core config values, graphable in a spreadsheet.

**The Unity way**: If you can't preview the curve and immediately understand what stage 500 looks like, you have too many layers. One curve to rule them all.
