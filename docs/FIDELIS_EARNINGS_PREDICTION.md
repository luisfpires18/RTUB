# Fidelis Earnings Prediction — My Tuno

> Auto-generated analysis based on current `scaling.config.json` values (v1.3.0 — enemy-level XP system).  
> **Updated**: XP now uses enemy-level formula with level-diff scaling. Fidelis unchanged.

---

## Table of Contents

1. [How Fidelis is Earned (Summary)](#1-how-fidelis-is-earned)
2. [Arena Mode — Battle Earnings](#2-arena-mode--battle-earnings)
3. [Stage Mode — Battle Earnings](#3-stage-mode--battle-earnings)
4. [Stage Mode — Per-Stage Detailed Table](#4-stage-mode--per-stage-detailed-table)
5. [Stage Mode — Block Summaries (every 10 stages)](#5-stage-mode--block-summaries)
6. [Stage Mode — Cumulative Earnings](#6-stage-mode--cumulative-earnings)
7. [Other Fidelis Sources](#7-other-fidelis-sources)
8. [Fidelis Sinks (Costs)](#8-fidelis-sinks-costs)

---

## 1. How Fidelis is Earned

Fidelis is earned through **5 main sources**:

| Source | Type | Notes |
|--------|------|-------|
| **Arena Battles** | Win / Draw | Scales with attacker level AND defender level |
| **Stage Battles** | Win only | Scales with stage number and character level (capped) |
| **Discard Items** | Equipment / Instrument Parts | Fixed values (25 / 30 per item) |
| **Daily Reward** | Login bonus | 45 base + 3 per character level |
| **Destilaria** | Resource gathering | Indirect (gather → sell/use) |

---

## 2. Arena Mode — Battle Earnings

### Formulas

```
Win Fidelis  = WinReward  × AttackerScale × DefenderScale
Draw Fidelis = DrawReward × AttackerScale × DefenderScale
Loss Fidelis = 0

AttackerScale = 1 + attackerLevel × AttackerLevelFidelisScale
DefenderScale = 1 + (defenderLevel - 1) × FidelisLevelMultiplier
```

### Current Config Values

| Parameter | Value |
|-----------|-------|
| WinReward (base) | **15** |
| DrawReward (base) | **7.5** |
| AttackerLevelFidelisScale | **0.06** (+6% per attacker level) |
| FidelisLevelMultiplier | **0.1** (+10% per defender level above 1) |

### Arena Win Earnings Table (Fidelis per Win)

> Rows = Attacker Level, Columns = Defender Level

| Attacker Lvl | Def Lvl 1 | Def Lvl 5 | Def Lvl 10 | Def Lvl 15 | Def Lvl 20 | Def Lvl 30 | Def Lvl 50 |
|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **1** | 15.90 | 21.83 | 29.34 | 36.84 | 44.35 | 59.36 | 89.39 |
| **3** | 17.70 | 24.30 | 32.66 | 41.01 | 49.37 | 66.08 | 99.51 |
| **5** | 19.50 | 26.78 | 35.99 | 45.20 | 54.41 | 72.82 | 109.65 |
| **10** | 24.00 | 32.95 | 44.28 | 55.61 | 66.94 | 89.59 | 134.90 |
| **15** | 28.50 | 39.13 | 52.58 | 66.02 | 79.47 | 106.37 | 160.16 |
| **20** | 33.00 | 45.30 | 60.87 | 76.44 | 92.01 | 123.14 | 185.41 |
| **30** | 42.00 | 57.66 | 77.47 | 97.28 | 117.09 | 156.70 | 235.93 |
| **50** | 60.00 | 82.35 | 110.68 | 138.96 | 167.29 | 223.82 | 336.96 |
| **75** | 82.50 | 113.27 | 152.18 | 191.07 | 230.02 | 307.81 | 463.31 |
| **100** | 105.00 | 144.17 | 193.76 | 243.26 | 292.85 | 391.95 | 590.10 |

### Arena Draw Earnings Table (Fidelis per Draw)

| Attacker Lvl | Def Lvl 1 | Def Lvl 5 | Def Lvl 10 | Def Lvl 20 | Def Lvl 50 |
|:---:|:---:|:---:|:---:|:---:|:---:|
| **1** | 7.95 | 10.91 | 14.67 | 22.18 | 44.70 |
| **5** | 9.75 | 13.39 | 18.00 | 27.20 | 54.83 |
| **10** | 12.00 | 16.47 | 22.14 | 33.47 | 67.45 |
| **20** | 16.50 | 22.65 | 30.44 | 46.01 | 92.71 |
| **50** | 30.00 | 41.18 | 55.34 | 83.64 | 168.48 |
| **100** | 52.50 | 72.09 | 96.88 | 146.43 | 295.05 |

---

## 3. Stage Mode — Battle Earnings

### Formulas

```
stageScaling    = 1.0 + (MaxMult - 1.0) × (1 - e^(-stageNumber × ScalingFactor))
                = 1.0 + 5.0 × (1 - e^(-stage × 0.003))

levelMultiplier = min(1 + (level - 1) × 0.04, 1.8)

Normal Fidelis  = NormalWin × enemyCount × stageScaling × levelMultiplier
Boss Fidelis    = BossWin   × 1          × stageScaling × levelMultiplier

XP (Normal)     = XpPerEnemyLevel × √(stageNumber) × enemyCount × levelDiffMult
XP (Boss)       = XpPerEnemyLevel × √(stageNumber) × 1          × BossXPMultiplier × levelDiffMult

levelDiffMult   = max(MinXPLevelMultiplier, 1.0 − max(0, playerLevel − stageNumber) × XpLevelPenaltyRate)
```

> **Enemy Level = Stage Number.** XP scales with √(stageNumber) so higher stages give more XP, but
> the growth is sublinear. The `levelDiffMult` reduces XP when the player far outlevels the enemies
> (1.5% reduction per level above, floored at 5%), making low-stage farming ineffective for XP.
> Fidelis is **not** affected by levelDiffMult.

### Current Config Values

| Parameter | Value |
|-----------|-------|
| NormalWin (base fidelis) | **12** |
| BossWin (base fidelis) | **40** |
| XpPerEnemyLevel | **12** |
| EnemyLevelXPPower | **0.5** (√) |
| BossXPMultiplier | **8** |
| XpLevelPenaltyRate | **0.015** |
| MinXPLevelMultiplier | **0.05** |
| StageRewardScalingFactor | **0.003** |
| MaxStageRewardMultiplier | **6.0** |
| FidelisLevelMultiplier | **0.04** |
| FidelisLevelMultiplierCap | **1.8** |

### Level Multiplier Table

The level multiplier caps at **1.8** (reached at level **21**).

| Character Level | Level Multiplier |
|:---:|:---:|
| 1 | 1.00 |
| 3 | 1.08 |
| 6 | 1.20 |
| 11 | 1.40 |
| 16 | 1.60 |
| 21+ | **1.80** (capped) |

### Stage Encounter Pattern (repeats every 10 stages)

| Stage Offset | Type | Enemy Count |
|:---:|:---:|:---:|
| 1 | Normal | 1 |
| 2 | Normal | 1 |
| 3 | Normal | 2 |
| 4 | Normal | 2 |
| 5 | Normal | 3 |
| 6 | Normal | 3 |
| 7 | Normal | 4 |
| 8 | Normal | 4 |
| 9 | Normal | 5 |
| 10 | **BOSS** | 1 |

**Per block**: 25 normal enemies + 1 boss across 10 stages.

### Stage Scaling Curve

| Stage | stageScaling | % of Max |
|:---:|:---:|:---:|
| 1 | 1.015 | 0.3% |
| 10 | 1.148 | 3.0% |
| 25 | 1.361 | 7.2% |
| 50 | 1.696 | 13.9% |
| 100 | 2.296 | 25.9% |
| 150 | 2.812 | 36.2% |
| 200 | 3.256 | 45.1% |
| 300 | 3.967 | 59.3% |
| 400 | 4.494 | 69.9% |
| 500 | 4.884 | 77.7% |
| 600 | 5.174 | 83.5% |
| 700 | 5.388 | 87.8% |
| 800 | 5.546 | 90.9% |
| 900 | 5.664 | 93.3% |
| 1000 | 5.751 | 95.0% |

---

## 4. Stage Mode — Per-Stage Detailed Table

### Stages 1–10 (Block 1 — Forest)

**At Level 1 (multiplier = 1.0):**

| Stage | Type | Enemies | stageScaling | Fidelis | XP |
|:---:|:---:|:---:|:---:|:---:|:---:|
| 1 | Normal | 1 | 1.015 | **12.18** | 12 |
| 2 | Normal | 1 | 1.030 | **12.36** | 17 |
| 3 | Normal | 2 | 1.045 | **25.08** | 42 |
| 4 | Normal | 2 | 1.060 | **25.43** | 48 |
| 5 | Normal | 3 | 1.074 | **38.68** | 80 |
| 6 | Normal | 3 | 1.089 | **39.21** | 88 |
| 7 | Normal | 4 | 1.104 | **52.99** | 127 |
| 8 | Normal | 4 | 1.119 | **53.69** | 136 |
| 9 | Normal | 5 | 1.133 | **67.99** | 180 |
| 10 | **Boss** | 1 | 1.148 | **45.91** | 304 |
| | | | **Block Total** | **374** | **1,034** |

**At Level 21+ (multiplier = 1.80):**

| Stage | Type | Enemies | Fidelis | XP |
|:---:|:---:|:---:|:---:|:---:|
| 1 | Normal | 1 | **21.92** | 8 |
| 2 | Normal | 1 | **22.25** | 12 |
| 3 | Normal | 2 | **45.14** | 30 |
| 4 | Normal | 2 | **45.78** | 36 |
| 5 | Normal | 3 | **69.62** | 61 |
| 6 | Normal | 3 | **70.58** | 68 |
| 7 | Normal | 4 | **95.38** | 100 |
| 8 | Normal | 4 | **96.64** | 109 |
| 9 | Normal | 5 | **122.38** | 148 |
| 10 | **Boss** | 1 | **82.64** | 253 |
| | | | **Block Total** | **672** | **825** |

> ⚠️ XP is reduced at level 21 because the player outlevels stages 1–10 (levelDiffMult ≈ 0.70–0.84).

### Stages 91–100 (Block 10 — End of Forest)

**At Level 21+ (multiplier = 1.80):**

| Stage | Type | Enemies | stageScaling | Fidelis | XP |
|:---:|:---:|:---:|:---:|:---:|:---:|
| 91 | Normal | 1 | 2.195 | **47.40** | 114 |
| 92 | Normal | 1 | 2.206 | **47.65** | 115 |
| 93 | Normal | 2 | 2.217 | **95.79** | 231 |
| 94 | Normal | 2 | 2.229 | **96.28** | 233 |
| 95 | Normal | 3 | 2.240 | **145.15** | 351 |
| 96 | Normal | 3 | 2.251 | **145.88** | 353 |
| 97 | Normal | 4 | 2.262 | **195.47** | 473 |
| 98 | Normal | 4 | 2.274 | **196.44** | 475 |
| 99 | Normal | 5 | 2.285 | **246.76** | 597 |
| 100 | **Boss** | 1 | 2.296 | **165.31** | 960 |
| | | | **Block Total** | **1,382** | **3,902** |

### Stages 491–500 (Block 50 — End of Tropical)

**At Level 21+ (multiplier = 1.80):**

| Stage | Type | Enemies | stageScaling | Fidelis | XP |
|:---:|:---:|:---:|:---:|:---:|:---:|
| 491 | Normal | 1 | 4.854 | **104.84** | 266 |
| 492 | Normal | 1 | 4.857 | **104.92** | 266 |
| 493 | Normal | 2 | 4.861 | **209.98** | 533 |
| 494 | Normal | 2 | 4.864 | **210.13** | 533 |
| 495 | Normal | 3 | 4.867 | **315.41** | 801 |
| 496 | Normal | 3 | 4.871 | **315.63** | 802 |
| 497 | Normal | 4 | 4.874 | **421.14** | 1,070 |
| 498 | Normal | 4 | 4.878 | **421.43** | 1,071 |
| 499 | Normal | 5 | 4.881 | **527.15** | 1,340 |
| 500 | **Boss** | 1 | 4.884 | **351.67** | 2,147 |
| | | | **Block Total** | **2,982** | **8,829** |

### Stages 991–1000 (Block 100 — End of Dark)

**At Level 21+ (multiplier = 1.80):**

| Stage | Type | Enemies | stageScaling | Fidelis | XP |
|:---:|:---:|:---:|:---:|:---:|:---:|
| 991 | Normal | 1 | 5.744 | **124.08** | 378 |
| 992 | Normal | 1 | 5.745 | **124.09** | 378 |
| 993 | Normal | 2 | 5.746 | **248.22** | 756 |
| 994 | Normal | 2 | 5.747 | **248.25** | 757 |
| 995 | Normal | 3 | 5.747 | **372.43** | 1,136 |
| 996 | Normal | 3 | 5.748 | **372.47** | 1,136 |
| 997 | Normal | 4 | 5.749 | **496.70** | 1,516 |
| 998 | Normal | 4 | 5.750 | **496.76** | 1,516 |
| 999 | Normal | 5 | 5.750 | **621.03** | 1,896 |
| 1000 | **Boss** | 1 | 5.751 | **414.08** | 3,036 |
| | | | **Block Total** | **3,518** | **12,505** |

---

## 5. Stage Mode — Block Summaries

Total Fidelis earned per 10-stage block (assumes **all wins**, level 21+ cap = 1.80).
XP shown at dynamic progression level (player levels up as they clear stages).

| Block | Stages | Biome | Avg stageScaling | Block Fidelis | Block XP |
|:---:|:---:|:---:|:---:|:---:|:---:|
| 1 | 1–10 | Forest | 1.082 | **672** | 1,034 |
| 2 | 11–20 | Forest | 1.227 | **761** | 1,635 |
| 3 | 21–30 | Forest | 1.368 | **847** | 2,060 |
| 4 | 31–40 | Forest | 1.505 | **931** | 2,412 |
| 5 | 41–50 | Forest | 1.638 | **1,012** | 2,717 |
| 6 | 51–60 | Forest | 1.767 | **1,090** | 2,993 |
| 7 | 61–70 | Forest | 1.892 | **1,166** | 3,242 |
| 8 | 71–80 | Forest | 2.013 | **1,241** | 3,477 |
| 9 | 81–90 | Forest | 2.131 | **1,312** | 3,697 |
| 10 | 91–100 | Forest | 2.245 | **1,382** | 3,902 |
| 15 | 141–150 | Swamp | 2.768 | **1,701** | 4,802 |
| 20 | 191–200 | Swamp | 3.219 | **1,976** | 5,559 |
| 25 | 241–250 | Mountains | 3.606 | **2,212** | 6,225 |
| 30 | 291–300 | Mountains | 3.939 | **2,415** | 6,826 |
| 40 | 391–400 | Snowy | 4.474 | **2,741** | 7,890 |
| 50 | 491–500 | Tropical | 4.869 | **2,982** | 8,829 |
| 60 | 591–600 | Caverns | 5.162 | **3,161** | 9,677 |
| 70 | 691–700 | Desert | 5.379 | **3,293** | 10,455 |
| 80 | 791–800 | Volcanic | 5.540 | **3,392** | 11,180 |
| 90 | 891–900 | Ruins | 5.659 | **3,464** | 11,861 |
| 100 | 991–1000 | Dark | 5.748 | **3,518** | 12,505 |

---

## 6. Stage Mode — Cumulative Earnings

Estimated **total Fidelis earned** from stage 1 up to a given stage (level 21+, all wins).
XP uses dynamic player progression (player levels up as they clear stages).

| Reached Stage | Est. Cumulative Fidelis | Est. Cumulative XP | Player Level |
|:---:|:---:|:---:|:---:|
| 10 | ~672 | ~1,034 | **6** |
| 20 | ~1,433 | ~2,669 | **10** |
| 50 | ~4,222 | ~9,858 | **20** |
| 100 | ~10,414 | ~27,169 | **33** |
| 150 | ~18,301 | ~49,457 | **44** |
| 200 | ~27,646 | ~75,785 | **55** |
| 300 | ~49,929 | ~138,558 | **74** |
| 400 | ~75,954 | ~212,802 | **92** |
| 500 | ~104,751 | ~296,953 | **109** |
| 600 | ~135,601 | ~389,970 | **125** |
| 700 | ~167,973 | ~491,069 | **140** |
| 800 | ~201,471 | ~599,651 | **155** |
| 900 | ~235,805 | ~715,232 | **169** |
| 1000 | ~270,757 | ~837,407 | **183** |

> These are estimates assuming the player wins every fight. Actual values depend on run length (rewards are only applied when a run ends, not on cancel/back).

---

## 7. Other Fidelis Sources

### Daily Login Reward

```
Daily Fidelis = 45 + (characterLevel × 3)
```

| Character Level | Daily Fidelis |
|:---:|:---:|
| 1 | 48 |
| 5 | 60 |
| 10 | 75 |
| 20 | 105 |
| 50 | 195 |
| 100 | 345 |

### Item Discard Values

| Item | Fidelis per Discard |
|------|:---:|
| Equipment piece | **25** |
| Instrument part | **30** |

### Arena Beer Drop

- **5% chance** to drop a Beer on arena win (configurable via `beerDropChance: 0.05`).

### Stage Item Drop Rates (per enemy killed)

| Item | Normal Enemy | Boss (×3.0) |
|------|:---:|:---:|
| Beer | 10% | 30% |
| Shot | 5% | 15% |
| Instrument Part | 0.5% | 1.5% |
| Equipment Piece | 0.8% | 2.4% |

---

## 8. Fidelis Sinks (Costs)

| Action | Cost |
|--------|:---:|
| Revive character | **30** |
| Restore HP | **20** |
| Stat upgrades | Varies (base 5–20, cost exponent 1.5–1.8) |
| Weapon upgrade (level 0→1) | **50** (×1.5 per subsequent level) |

---

## Key Observations

1. **Stage mode is the primary Fidelis earner** — a full 10-stage block at high stages yields ~3,200–3,500 Fidelis, while a single arena win gives 15–590 depending on levels.

2. **Stage scaling has a slow curve** — rewards reach 78% of max at stage 500, 95% at stage 1000. Early stages (1–100) give proportionally less, so farming low stages is less profitable.

3. **Character level matters up to level 21** — the level multiplier caps at **1.80× at level 21**, giving sustained progression meaning without making high-level farming overpowered. The slow ramp (4% per level) means low-level farming abuse is minimal.

4. **Clear progression curve** — Stage 100 block earns ~1,382 vs stage 1 block at ~672. Stage 500 block earns ~2,982. Each biome feels more rewarding than the last.

5. **Boss stages now feel rewarding** — a boss gives 40 base Fidelis. While still below stage 9's 5×12=60 from normals, the gap is much smaller and bosses grant massive XP (8× multiplier).

6. **Arena Fidelis still scales better with level** — at level 100 vs a level 50 opponent, a win gives ~590 Fidelis. Arena remains the dominant Fidelis source for high-level PvP.

7. **Estimated daily earnings** (active play, level 21+):
   - ~10 arena wins: **~250–500 Fidelis** (depending on opponent levels)
   - ~100 stages cleared: **~10,400 Fidelis**
   - Daily login: **~50–350 Fidelis**
   - Item discards: variable

8. **XP anti-farming** — a level 112 player farming stages 1–110 earns only ~12,914 XP (2 levels). Farming at-level content (e.g. stages 101–110) gives ~3,800 XP per run — rewarding but not exploitable.

---

## Balance Changes Applied

| Parameter | Original | After Rebalance | Rationale |
|---|:---:|:---:|---|
| `normalWin` | 10 | **12** | Small base bump; most increase comes from better scaling |
| `bossWin` | 25 | **40** | Bosses feel more rewarding without eclipsing multi-enemy stages |
| `maxStageRewardMultiplier` | 3.0 | **6.0** | More headroom for high-stage rewards — deeper runs pay off more |
| `stageRewardScalingFactor` | 0.005 | **0.003** | Slower curve — easy stages give less, hard stages give proportionally more |
| `fidelisLevelMultiplier` | 0.1 | **0.04** | Slower per-level ramp — less farming abuse at low levels |
| `fidelisLevelMultiplierCap` | 1.3 | **1.8** | Level matters until level 21. High-level players earn 20% more Fidelis |
| `statGrowthExponent` | 0.15 | **0.20** | Stats accelerate more at high levels — level 112 gets +23% more stats |
| `baseStageXP` | 30 | **60** | Legacy — replaced by enemy-level XP system |
| `xpPerEnemyLevel` | — | **12** | XP per enemy based on √(stageNumber), not flat baseStageXP |
| `enemyLevelXPPower` | — | **0.5** | Sublinear XP growth: stage 100 gives 10× stage 1, not 100× |
| `xpLevelPenaltyRate` | — | **0.015** | 1.5% XP reduction per level above enemy — discourages low-stage farming |
| `minXPLevelMultiplier` | — | **0.05** | Floor at 5% XP — never zero, but negligible for overleveled content |

### Impact Summary

| Stage Block | Original (lvl 4+, 1.3×) | Rebalanced (lvl 6+, 1.5×) | Change |
|:---:|:---:|:---:|:---:|
| 1–10 | 380 | 560 | +47% |
| 91–100 | 633 | 1,150 | +82% |
| 491–500 | 1,012 | 2,491 | +146% |
| 991–1000 | 1,068 | 2,920 | +173% |
| **Total 1–1000** | **~72,425** | **~172,183** | **+138%** |
