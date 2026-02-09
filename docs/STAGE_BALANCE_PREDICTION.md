# Stage Mode Full Rebalance — Prediction & Analysis

> Comprehensive balance analysis for My Tuno Stage Mode.  
> Covers: Enemy stats, XP/Leveling, Upgrades, Equipment, Weapons, Fidelis economy.

---

## Table of Contents

1. [Core Problems Identified](#1-core-problems-identified)
2. [All Changes Summary](#2-all-changes-summary)
3. [Player Progression Curve](#3-player-progression-curve)
4. [Enemy Stat Curves](#4-enemy-stat-curves)
5. [Battle Simulations at Milestones](#5-battle-simulations-at-milestones)
6. [Upgrade Economy](#6-upgrade-economy)
7. [XP & Leveling Flow](#7-xp--leveling-flow)
8. [Equipment & Weapon Impact](#8-equipment--weapon-impact)
9. [Fidelis Economy Flow](#9-fidelis-economy-flow)
10. [Progression Experience Summary](#10-progression-experience-summary)

---

## 1. Core Problems Identified

### The Shared Scaling Problem

Enemies and player use the **same polynomial formula**:

$$\text{ScaleFactor}(N) = 1 + 0.15 \times (N-1)^{1.20}$$

| Stage/Level | Scale Factor |
|:---:|:---:|
| 1 | 1.0 |
| 10 | 3.09 |
| 50 | 17.01 |
| 100 | 38.23 |
| 200 | 87.04 |
| 500 | 260.31 |

The problem: **enemy stage number acts as "level", but the player's actual level lags far behind.**  
At stage 100, enemies have ~30x scaling, but the player (from XP earned) is only level ~18 with ~5.6x scaling.

### Before (Critical Issues)

| Issue | Details |
|---|---|
| **Enemy bases too high** | Normal: 150 HP / 15 Power vs Player: 100 HP / 10 Power. Enemies are 50% stronger at ANY stage |
| **XP too slow** | `xpPerLevelBase=120` → player reaches level ~18 after 100 stages, enemies scale as "level 100" |
| **Upgrades too weak** | 2% per upgrade = 50 upgrades for +100%. With expensive costs, barely matters |
| **Boss multiplier compounds** | 2.5× on both HP AND Power → bosses are unkillable walls |
| **Biome multipliers too steep** | Forest 1.1× → Dark 5.0× stacked ON TOP of stage scaling |
| **Speed upgrades undervalued** | At +1 per upgrade with 1.8 cost exponent, too expensive for the benefit |
| **Equipment too weak** | Full set: +70 HP, +13 Power, +21 Def — trivial at stage 50+ |

### Root Math: Why Stage 100 was Impossible (Before)

Player level ~18 at stage 100 (old XP rate):

| | Player (Lvl 18) | Normal Enemy (Stage 100) | Ratio |
|---|:---:|:---:|:---:|
| Scale Factor | 5.59 | 30.58 | 5.5× gap |
| HP | 559 | 5,046 | 9× |
| Power | 56 | 505 | 9× |
| Defense | 28 | 217 | 8× |

**Result**: Player deals ~10 damage/hit, needs 500+ hits to kill 1 enemy. Dies in ~2 hits.

---

## 2. All Changes Summary

### A. Enemy Base Stats (lowered to be BELOW player base)

| Stat | Normal (Before → After) | Boss (Before → After) |
|---|:---:|:---:|
| HP | 150 → **40** | 350 → **80** |
| Power | 15 → **5** | 20 → **8** |
| Speed | 5 → **5** | 4 → **3** |
| Defense | 8 → **2** | 10 → **4** |
| Critical | 0.06 → **0.04** | 0.15 → **0.10** |

**Why**: With shared scaling formula, enemy bases must be lower than player to compensate for the level vs stage gap. Player base (100/10/10/5) will naturally outscale enemy base (40/5/5/2) when level approximates stage.

### B. Biome Difficulty Multipliers (flattened)

| Biome | Before → After |
|---|:---:|
| Forest (1–100) | 1.1 → **1.0** |
| Swamp (101–200) | 1.4 → **1.1** |
| Mountains (201–300) | 1.7 → **1.2** |
| Snowy (301–400) | 2.0 → **1.3** |
| Tropical (401–500) | 2.3 → **1.4** |
| Caverns (501–600) | 2.6 → **1.5** |
| Desert (601–700) | 3.0 → **1.7** |
| Volcanic (701–800) | 3.5 → **1.9** |
| Ruins (801–900) | 4.0 → **2.1** |
| Dark (901–1000) | 5.0 → **2.5** |

### C. Boss Multiplier

| | Before → After |
|---|:---:|
| `bossMultiplier` | 2.5 → **1.2** |

Applies to both HP and Power. Lowered so bosses are tough but beatable.

### D. XP & Leveling

| Parameter | Before → After | Effect |
|---|:---:|---|
| `xpPerLevelBase` | 120 → **50** | Levels come ~2.4× faster |
| `xpPerEnemyLevel` | — → **12** | XP based on enemy level (√ stage), not flat baseStageXP |
| `enemyLevelXPPower` | — → **0.5** | Sublinear √ scaling: stage 100 = 10× stage 1 XP, not 100× |
| `xpLevelPenaltyRate` | — → **0.015** | 1.5% XP reduction per level above enemy |
| `minXPLevelMultiplier` | — → **0.05** | Floor at 5% XP for massively overleveled content |
| `baseStageXP` | 30 → **60** | Legacy, replaced by enemy-level XP system |
| `bossXPMultiplier` | 10 → **8** | Bosses still give great XP, slightly less spiky |
| `stageRewardScalingFactor` | 0.005 → **0.003** | Slower curve — early stages worth less, hard stages worth more |
| `maxStageRewardMultiplier` | 3.0 → **6.0** | Deeper runs are proportionally more rewarding (Fidelis) |
| `fidelisLevelMultiplierCap` | 1.3 → **1.8** | Level matters until 21 (not too exploitable for farming) |
| `fidelisLevelMultiplier` | 0.1 → **0.04** | Slower per-level ramp — less exploit at low levels |
| `statGrowthExponent` | 0.15 → **0.20** | Stats grow faster at high levels — each level is more valuable |

> **Key change**: XP is now based on **enemy level** (= stage number) with √ scaling and a
> level-difference multiplier. High-level players get negligible XP from low stages.
> Formula: `XP = 12 × √(stageNumber) × enemyCount × bossXPMult × levelDiffMult`

### E. Upgrades (more impactful + cheaper)

| Upgrade | Mult (Before→After) | BaseCost (Before→After) | Exponent (Before→After) |
|---|:---:|:---:|:---:|
| HP | 0.02 → **0.03** | 5 → 5 | 1.5 → 1.5 |
| Power | 0.02 → **0.03** | 10 → **8** | 1.5 → 1.5 |
| Defense | 0.02 → **0.03** | 12 → **10** | 1.5 → 1.5 |
| Speed | 1 → **1.5** | 15 → **10** | 1.8 → **1.5** |
| Critical | 0.005 → **0.007** | 20 → **15** | 1.6 → 1.6 |

### F. Equipment Stats (boosted)

| Slot | HP (Before→After) | Power (Before→After) | Defense (Before→After) |
|---|:---:|:---:|:---:|
| Head | 15 → **20** | 0 | 3 → **5** |
| Shoulders | 10 → **15** | 0 | 5 → **7** |
| Chest | 25 → **35** | 0 | 8 → **12** |
| Gloves | 0 | 5 → **8** | 0 |
| Legs | 15 → **20** | 0 | 3 → **5** |
| Boots | 5 → **10** | 0 | 2 → **4** |
| Instrument | 0 | 8 → **12** | 0 |
| **Full Set** | **70 → 100** | **13 → 20** | **21 → 33** |

### G. Weapon Forging

| | Before → After |
|---|:---:|
| `weaponUpgradeBaseCost` | 50 → **40** |
| `weaponUpgradeStatBonus` | 0.10 → **0.15** |

### H. Enemy Scaling Rates

| Parameter | Before → After |
|---|:---:|
| `enemySpeedScalingRate` | 0.073 → **0.05** |
| `enemyScaling.speedPerStage` | 0.011 → **0.008** |
| `enemyScaling.criticalChancePerStage` | 0.007 → **0.005** |
| `enemyScaling.hpPerStage` | 0.66 → **0.15** |
| `enemyScaling.powerPerStage` | 0.66 → **0.15** |
| `enemyScaling.defensePerStage` | 0.33 → **0.12** |
| `scaling.hpGrowthPerStage` | 0.15 → **0.10** |
| `scaling.damageGrowthPerStage` | 0.15 → **0.10** |
| `scaling.armorGrowthPerStage` | 0.6 → **0.40** |

---

## 3. Player Progression Curve

### Level vs Stage (New XP Rate)

XP per level = `Level × 50`. Cumulative to level L = $25 \times L \times (L-1)$.

XP formula: `XP = 12 × √(stageNumber) × enemyCount × bossXPMult × levelDiffMult`.
As the player levels up, `levelDiffMult` kicks in for stages below the player’s level,
slightly reducing XP from older content.

| Reached Stage | Approx Cumulative XP | Approx Player Level |
|:---:|:---:|:---:|
| 10 | ~1,034 | **~6** |
| 50 | ~9,858 | **~20** |
| 100 | ~27,169 | **~33** |
| 200 | ~75,785 | **~55** |
| 300 | ~138,558 | **~74** |
| 500 | ~296,953 | **~109** |
| 700 | ~491,069 | **~140** |
| 1000 | ~837,407 | **~183** |

### Player Stats at Key Levels (no upgrades, no equipment)

$$\text{ScaleFactor}(L) = 1 + 0.15 \times (L-1)^{1.20}$$

| Level | Scale | HP | Power | Defense | Speed | Action Time |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| 1 | 1.00 | 100 | 10 | 5 | 10 | 4.80s |
| 5 | 1.79 | 179 | 18 | 9 | 18 | 4.64s |
| 10 | 3.09 | 309 | 31 | 15 | 31 | 4.38s |
| 20 | 6.14 | 614 | 61 | 31 | 61 | 3.77s |
| 33 | 10.60 | 1,060 | 106 | 53 | 106 | 2.88s |
| 50 | 17.01 | 1,701 | 170 | 85 | 170 | 1.60s |
| 75 | 27.25 | 2,725 | 273 | 136 | 273 | 1.00s |
| 100 | 38.23 | 3,823 | 382 | 191 | 382 | 1.00s |
| 150 | 61.80 | 6,180 | 618 | 309 | 618 | 1.00s |

> Action Time = max(1.0, 5.0 − TotalSpeed × 0.02). Caps at 1.0s around level 65+.

---

## 4. Enemy Stat Curves

### Normal Enemy Stats (NEW base: 40/5/5/2)

> HP & Power scale with same polynomial as player. Defense at 80% rate. Speed at reduced rate.

| Stage | Biome (Diff) | HP | Power | Defense | Speed | AT |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| 1 | Forest (1.0) | 40 | 5 | 2 | 5 | 4.90s |
| 5 | Forest (1.0) | 71 | 8 | 3 | 6 | 4.88s |
| 10 | Forest (1.0) | 123 | 15 | 6 | 7 | 4.86s |
| 25 | Forest (1.0) | 311 | 38 | 15 | 11 | 4.78s |
| 50 | Forest (1.0) | 680 | 85 | 34 | 17 | 4.66s |
| 100 | Forest (1.0) | 1,529 | 191 | 76 | 29 | 4.42s |
| 150 | Swamp (1.1) | 2,719 | 339 | 135 | 42 | 4.16s |
| 200 | Swamp (1.1) | 3,829 | 478 | 191 | 54 | 3.92s |
| 300 | Mountains (1.2) | 6,779 | 847 | 338 | 79 | 3.42s |
| 500 | Tropical (1.4) | 14,577 | 1,822 | 728 | 129 | 2.42s |
| 700 | Desert (1.7) | 26,490 | 3,311 | 1,324 | 179 | 1.42s |
| 1000 | Dark (2.5) | 59,744 | 7,468 | 2,987 | 254 | 1.00s |

### Boss Enemy Stats (NEW base: 80/8/3/4, bossMultiplier 1.2)

| Stage | Biome | HP | Power | Defense | Speed | AT |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| 10 | Forest (1.0) | 297 | 29 | 11 | 5 | 4.90s |
| 50 | Forest (1.0) | 1,632 | 163 | 61 | 13 | 4.74s |
| 100 | Forest (1.0) | 3,669 | 366 | 137 | 23 | 4.54s |
| 200 | Swamp (1.1) | 9,191 | 919 | 344 | 43 | 4.14s |
| 300 | Mountains (1.2) | 16,271 | 1,627 | 610 | 63 | 3.74s |
| 500 | Tropical (1.4) | 34,985 | 3,498 | 1,311 | 103 | 2.94s |
| 1000 | Dark (2.5) | 143,386 | 14,338 | 5,376 | 203 | 1.00s |

---

## 5. Battle Simulations at Milestones

### How Combat Works

- **Damage** = Power × variance(0.8–1.2) × crit(2x) → then mitigated by defense
- **Defense mitigation** = $\frac{K}{K + \text{Defense}}$ where K = 50
- **Action Time** = max(1.0, 5.0 − Speed × 0.02 − SpeedUpgrades × 0.075)
- **Timeout**: 300 seconds max. Higher HP wins on timeout.

### Stage 10 Boss — The First Real Test

**Player**: Level ~6, ~5 speed upgrades, shot buff (+20%), Cerveja weapon +1

| | Player | Boss |
|---|:---:|:---:|
| HP | ~250 | 297 |
| Power | ~25 | 30 |
| Defense | ~12 | 11 |
| Action Time | ~4.1s | 4.90s |
| Damage/hit | ~21 | ~19 |
| Hits to kill | ~14 (58s) | ~13 (64s) |

**Result**: Player wins with shot buff but tighter than before. Without shot, could go either way — creates meaningful item strategy. **First boss feels achievable and rewarding.**

### Stage 50 Normal (5-enemy stage = stage 49)

**Player**: Level ~20, 15 speed upgrades, 10 HP/Power/Def upgrades, full equipment

| | Player | Each Enemy (×5) |
|---|:---:|:---:|
| HP | ~900 | 680 |
| Power | ~100 | 85 |
| Defense | ~55 | 34 |
| Action Time | ~2.7s | 4.66s |
| Damage/hit | ~74 | ~42 |
| Hits to kill | ~9 (23s) | ~27 (126s) |

**5 enemies combined DPS**: 5 × 42/4.66 = 45.1 → Player survives ~25s  
**Time to kill all 5**: 5 × 23 = 115s  
**Result**: Player dies. Needs more upgrades or to skip this stage offset. **Creates natural progression wall at multi-enemy stages.**

### Stage 100 Boss — Forest Completion

**Player**: Level ~33, 20 speed upgrades, 15 HP + 10 Power + 5 Def upgrades, equipment + Cerveja weapon +3, shot buff

| | Player (buffed) | Boss |
|---|:---:|:---:|
| HP | ~1,600 | 3,669 |
| Power | ~190 | 367 |
| Defense | ~95 | 137 |
| Action Time | ~1.3s | 4.54s |
| Damage/hit | ~50 | ~115 |
| Hits to kill | ~74 (96s) | ~14 (64s) |

**Result**: Close fight — player needs good upgrades and a shot buff. **Feels like a true milestone boss.** Without shot or extra upgrades, player loses — creates meaningful progression.

### Stage 200 Boss — Swamp Completion

**Player**: Level ~55, 40 speed upgrades, 30 HP/Power/Defense upgrades, Vinho weapon +5

| | Player (buffed) | Boss |
|---|:---:|:---:|
| HP | ~3,500 | 9,191 |
| Power | ~370 | 919 |
| Defense | ~180 | 344 |
| Action Time | 1.0s | 4.14s |
| Damage/hit | ~54 | ~180 |
| Hits to kill | ~170 (170s) | ~23 (95s) |

**Result**: Tight loss without extra investment. Needs ~5 more Power upgrades or a weapon upgrade to tip the balance. **Creates a "gear check" before entering Mountains biome.**

### Stage 500 — Mid-Endgame

**Player**: Level ~109, maxed speed (AT=1.0s), 100 HP/Power/Def upgrades, Tequilla weapon +8

| | Player | Normal Enemy |
|---|:---:|:---:|
| HP | ~16,500 | 14,577 |
| Power | ~1,650 | 1,822 |
| Defense | ~820 | 729 |
| Action Time | 1.0s | 2.42s |
| Damage/hit | ~117 | ~55 |
| Hits to kill | ~125 (125s) | ~336 (813s) |

**Result**: Player handles 1v1 and 2-enemy stages comfortably. 3+ enemies becomes the wall. **Correct challenge scaling for endgame.**

---

## 6. Upgrade Economy

### Upgrade Costs (Fidelis)

Cost for upgrade N = `baseCost × N^exponent`

#### Total Cost for N Upgrades of Each Type

| # Upgrades | HP (cost 5, exp 1.5) | Power (cost 8, exp 1.5) | Defense (cost 10, exp 1.5) | Speed (cost 10, exp 1.5) | Critical (cost 15, exp 1.6) |
|:---:|:---:|:---:|:---:|:---:|:---:|
| 5 | 41 | 65 | 82 | 82 | 155 |
| 10 | 168 | 269 | 336 | 336 | 684 |
| 20 | 747 | 1,196 | 1,495 | 1,495 | 3,299 |
| 30 | 1,772 | 2,835 | 3,543 | 3,543 | 8,223 |
| 50 | 5,427 | 8,683 | 10,854 | 10,854 | 27,035 |
| 100 | 24,291 | 38,866 | 48,582 | 48,582 | 133,244 |

### Upgrade Impact

| Upgrades | HP Mult | Power Mult | Defense Mult | Speed AT Reduction | Crit Bonus |
|:---:|:---:|:---:|:---:|:---:|:---:|
| 10 | ×1.30 | ×1.30 | ×1.30 | −0.75s | +0.07 |
| 20 | ×1.60 | ×1.60 | ×1.60 | −1.50s | +0.14 |
| 30 | ×1.90 | ×1.90 | ×1.90 | −2.25s | +0.21 |
| 50 | ×2.50 | ×2.50 | ×2.50 | −3.75s | +0.35 |
| 100 | ×4.00 | ×4.00 | ×4.00 | −7.50s (capped) | +0.49 (near cap) |

> Speed upgrades also add +1.5 to TotalSpeed per upgrade (flat), improving action time through the speed formula too.

### Recommended Upgrade Priority

1. **Speed** (by far #1) — reduces action time dramatically, translates to more DPS
2. **Power** — more damage per hit
3. **HP** — survive longer
4. **Defense** — diminishing returns but still valuable
5. **Critical** — luxury investment, great for max-level players

---

## 7. XP & Leveling Flow

### XP Per 10-Stage Block

Formula: `XP = 12 × √(stageNumber) × enemyCount × bossXPMult × levelDiffMult`

With per-block enemy counts: 1+1+2+2+3+3+4+4+5 normals + 1 boss (8× XP).
Values below use dynamic player progression (leveling reduces XP from earlier stages slightly).

| Block | Stages | Player Level | Block XP |
|:---:|:---:|:---:|:---:|
| 1 | 1–10 | 1→6 | ~1,034 |
| 5 | 41–50 | 17→20 | ~2,717 |
| 10 | 91–100 | 31→33 | ~3,902 |
| 20 | 191–200 | 52→55 | ~5,559 |
| 30 | 291–300 | 72→74 | ~6,826 |
| 50 | 491–500 | 107→109 | ~8,829 |
| 100 | 991–1000 | 182→183 | ~12,505 |

### Cumulative XP & Level

| Stage Reached | Cumulative XP | Player Level | XP to Next Level |
|:---:|:---:|:---:|:---:|
| 10 | ~1,034 | **6** | 300 |
| 50 | ~9,858 | **20** | 1,000 |
| 100 | ~27,169 | **33** | 1,650 |
| 200 | ~75,785 | **55** | 2,750 |
| 300 | ~138,558 | **74** | 3,700 |
| 500 | ~296,953 | **109** | 5,450 |
| 700 | ~491,069 | **140** | 7,000 |
| 1000 | ~837,407 | **183** | 9,150 |

### Arena XP (unchanged formulas)

| Player Level | XP per Arena Win | Wins to Next Level |
|:---:|:---:|:---:|
| 5 | 70 | 4 |
| 10 | 90 | 6 |
| 20 | 130 | 8 |
| 50 | 250 | 10 |
| 100 | 450 | 11 |

> Arena has 60-min cooldown → max ~24 wins/day → ~2-3 level-ups per day at mid-game. Good supplementary source.

---

## 8. Equipment & Weapon Impact

### Full Equipment Set (NEW values)

| Stat | Total Bonus |
|---|:---:|
| HP | +100 |
| Power | +20 (Gloves 8 + Instrument 12) |
| Defense | +33 |

At level 36 (stage ~100), base HP = 1,169. Equipment adds +100 → +8.5% effective HP.  
At level 10 (stage ~10), base HP = 309. Equipment adds +100 → +32% effective HP.  
**Equipment matters MORE in early/mid game** — good design, encourages drops.

### Weapon Scaling (NEW: +15% per level)

| Weapon Level | Stat Multiplier | Power with Cerveja (cost 1) | Power with Vodka (cost 6) | Power with Aguardente (cost 10) |
|:---:|:---:|:---:|:---:|:---:|
| +0 | 1.0× | 12 | 72 | 120 |
| +1 | 1.15× | 14 | 83 | 138 |
| +3 | 1.45× | 17 | 104 | 174 |
| +5 | 1.75× | 21 | 126 | 210 |
| +8 | 2.20× | 26 | 158 | 264 |
| +10 | 2.50× | 30 | 180 | 300 |

### Weapon Upgrade Costs (NEW: baseCost 40, mult 1.5)

| From → To | Cost (Fidelis) | Cumulative |
|:---:|:---:|:---:|
| +0 → +1 | 40 | 40 |
| +1 → +2 | 60 | 100 |
| +2 → +3 | 90 | 190 |
| +3 → +4 | 135 | 325 |
| +4 → +5 | 203 | 528 |
| +5 → +6 | 304 | 832 |
| +7 → +8 | 684 | 2,198 |
| +9 → +10 | 1,538 | 5,275 |

**Weapon progression path**: Early game = Cerveja weapon → Mid game = Vinho/Licor → Late game = higher-tier drinks with upgrades.

---

## 9. Fidelis Economy Flow

### Fidelis Income (per 10-stage block, level 21+, current Fidelis config)

Using previously calculated values (normalWin=12, bossWin=40, maxMult=6.0, levelCap=1.80):

| Block | Stages | Block Fidelis |
|:---:|:---:|:---:|
| 1 | 1–10 | ~672 |
| 10 | 91–100 | ~1,382 |
| 20 | 191–200 | ~1,976 |
| 50 | 491–500 | ~2,982 |
| 100 | 991–1000 | ~3,518 |

### Fidelis Sinks vs Income

At stage 100 completion (~10,400 cumulative Fidelis earned):

| Spend On | Example Cost | Remaining |
|---|:---:|:---:|
| 20 Speed upgrades | ~1,495 | 8,905 |
| 15 HP upgrades | ~510 | 8,395 |
| 15 Power upgrades | ~817 | 7,578 |
| 10 Defense upgrades | ~336 | 7,242 |
| Weapon +3 | ~190 | 7,052 |
| Revives (est. 10×) | ~300 | 6,752 |
| HP Restores (est. 20×) | ~400 | 6,352 |
| **Remaining for savings** | | **~6,350** |

Player has healthy surplus to save for harder content. Not too tight, not too generous.

### Daily Fidelis Income (all sources, level 36)

| Source | Estimate |
|---|:---:|
| Daily Login | 153 (45 + 36×3) |
| 10 Arena Wins | ~500 |
| 50 Stages cleared | ~5,200 |
| Item Discards | ~200 |
| **Total** | **~6,053/day** |

---

## 10. Progression Experience Summary

### Expected Player Journey

| Stage Range | Biome | Player Level | Experience |
|---|---|:---:|---|
| **1–10** | Forest | 1–6 | Tutorial — learn mechanics, easy enemies, first boss needs shot buff or a few upgrades |
| **11–30** | Forest | 6–14 | Build foundation — start buying speed upgrades, collect first equipment pieces |
| **31–60** | Forest | 14–23 | Hitting walls at 4-5 enemy stages — need to farm and upgrade to push through |
| **61–100** | Forest | 23–33 | Forest mastery — bosses require shot buffs + good upgrades, 5-enemy stages are max difficulty |
| **101–200** | Swamp | 33–55 | New biome +10% difficulty — feels noticeably harder, need weapon upgrade + continued investment |
| **201–300** | Mountains | 55–74 | Mid-game — player should have good weapon, 50+ upgrades in key stats |
| **301–500** | Snowy/Tropical | 74–109 | Late-game — heavy investment required, higher drinks unlock better weapons |
| **501–700** | Caverns/Desert | 109–140 | Endgame — diminishing returns on rewards, pure challenge for dedicated players |
| **701–1000** | Volcanic/Ruins/Dark | 140–183 | Ultra-endgame — bragging rights, leaderboard pushing |

### Key Design Principles Achieved

1. **Stage 1-10 is accessible** — new player clears first block on first attempt
2. **First boss (stage 10) is a milestone** — requires either upgrades or shot buff
3. **Multi-enemy stages create natural walls** — not every stage is clearable on first attempt
4. **Upgrades feel impactful** — 3% per upgrade (up from 2%) means tangible power difference
5. **Speed is king** — smart players learn to prioritize speed upgrades for DPS
6. **Equipment drops matter early** — +100 HP when you have 300 base = massive boost
7. **Weapon tier tracks progression** — unlock stronger drinks = stronger weapons at each biome
8. **Bosses are trophy fights** — close, exciting battles that reward preparation
9. **Biomes feel different** — gradual difficulty escalation, not sudden spikes
10. **No infinite farming** — XP scales with enemy level (√stage) and penalizes overleveled players (1.5%/level above, 5% floor). A level 112 player farming stages 1–110 gains only ~2 levels instead of ~6. Fidelis rewards still scale via stageScaling curve, but XP requires at-level content.
