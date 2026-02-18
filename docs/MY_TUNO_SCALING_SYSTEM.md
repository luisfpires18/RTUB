# My-Tuno Game Scaling System Documentation

## Overview

My-Tuno features four combat modes, each with different scaling and DB-write strategies:

| Mode | Description | DB Writes | Rewards |
|---|---|:---:|---|
| **Stage** | 20 biomes × 1,000 floors (20,000 total) | 0 per battle | Batched at run end (`ApplyRunRewardsAsync`) |
| **Boss** | Boss rush with scaling difficulty | 1 per battle | Progress + HP saved, rewards at run end |
| **Battle (Arena)** | PvP matchmaking | 2–3 after each fight | `FinalizeAndApplyRewardsAsync` |
| **Survive** | Endless survival | 1 per level | `CompleteLevelAsync`, rewards at run end |

Scaling is configured via `scaling.config.json` and loaded into `MyTunoScaling` via `MyTunoScalingConfiguration`.

---

## 1. Arena Mode

### Player Stats

**Base stats** (character creation):

| Stat | Base Value |
|---|---:|
| HP | 200 |
| Power | 25 |
| Speed | 10 |
| Defense | 20 |
| Critical | 2% |

**Level scaling:** `StatValue = BaseStat × (1 + 0.008 × (Level − 1))` — max level 100.

### Upgrade System (Fidelis)

Each stat upgrades independently with linear cost: `Cost(N) = BaseCost + N × CostPerLevel`.

| Stat | Bonus/Upgrade | Base Cost | Per-Level | Max | Notes |
|---|:---:|---:|---:|:---:|---|
| HP | +100 | 50 | 50 | ∞ | Highest flat bonus |
| Power | +15 | 50 | 50 | ∞ | Linear damage scaling |
| Speed | +1.5 | 150 | 150 | 41 | Reduces action time (min 1.0s) |
| Defense | +12 | 50 | 50 | ∞ | Mitigation: `500/(500+Def)` |
| Critical | +0.5% | 120 | 120 | 80 | Capped at 40% |

### Combat

```
DamageMult = 500 / (500 + TargetDefense)
Damage     = max(1, ⌊Power × DamageMult⌋)
CritDamage = Damage × 2.0
```

### Arena Rewards

| Outcome | Fidelis | XP |
|---|---:|---:|
| Win | 120 | 100 |
| Draw | 40 | 50 |

Level difference scales rewards ±50% (1% per level diff).

---

## 2. Stage Mode

### Structure

- **20 biomes** (stages) × **1,000 floors** = **20,000 floors**
- **Arena** unlocks at floor **20,001** after clearing floor 20,000 boss
- Arena is endless (tier 21 flat stats)

### Encounter Pattern

Within each 10-floor block:

| Floor Offset | Enemies | Type |
|:---:|:---:|:---|
| 1–9 | 1–9 | Normal (ramp) |
| 10 | 1 | **Miniboss** |

Every **100th floor** = **Boss** (overrides miniboss, 1 enemy, unique boss sprite).

### Enemy Scaling — Tier System

Enemies use **flat stats per tier** — all floors within a tier face the same base stats. No per-floor formula.

21 tiers: T1–T20 (one per biome of 1,000 floors) + T21 (arena 20,001+).

**Key tiers:**

| Tier | Floors | Normal HP | Miniboss HP | Boss HP | Fidelis | XP |
|:---:|:---|---:|---:|---:|---:|---:|
| 1 | 1–1,000 | 150 | 400 | 1,200 | 30 | 20 |
| 5 | 4,001–5,000 | 1,900 | 4,800 | 15,000 | 140 | 110 |
| 10 | 9,001–10,000 | 11,000 | 27,500 | 85,000 | 370 | 230 |
| 15 | 14,001–15,000 | 34,000 | 85,000 | 262,000 | 740 | 380 |
| 20 | 19,001–20,000 | 100,000 | 250,000 | 770,000 | 1,350 | 580 |
| 21 | 20,001+ | 125,000 | 313,000 | 963,000 | 1,500 | 620 |

Full 21-tier tables: see `docs/my_tuno/STAGE_ENEMY_SCALING.md`.

### Biomes

| # | Biome | Floors | Reward × | Action Time |
|:---:|:---|:---|:---:|:---:|
| 1 | Forest | 1–1,000 | ×1.00 | 5.0s |
| 2 | Swamp | 1,001–2,000 | ×1.05 | 4.8s |
| 3 | Mountains | 2,001–3,000 | ×1.10 | 4.6s |
| 4 | Snowy | 3,001–4,000 | ×1.15 | 4.4s |
| 5 | Tropical | 4,001–5,000 | ×1.20 | 4.2s |
| 6 | Caverns | 5,001–6,000 | ×1.25 | 4.0s |
| 7 | Desert | 6,001–7,000 | ×1.30 | 3.8s |
| 8 | Volcanic | 7,001–8,000 | ×1.35 | 3.6s |
| 9 | Ruins | 8,001–9,000 | ×1.40 | 3.4s |
| 10 | Sky | 9,001–10,000 | ×1.45 | 3.2s |
| 11 | Underwater | 10,001–11,000 | ×1.50 | 3.0s |
| 12 | Underground | 11,001–12,000 | ×1.55 | 2.8s |
| 13 | Mechanical | 12,001–13,000 | ×1.60 | 2.6s |
| 14 | Frostfire | 13,001–14,000 | ×1.65 | 2.4s |
| 15 | Corruption | 14,001–15,000 | ×1.70 | 2.2s |
| 16 | Dark | 15,001–16,000 | ×1.75 | 2.0s |
| 17 | Alien | 16,001–17,000 | ×1.80 | 1.8s |
| 18 | Void | 17,001–18,000 | ×1.85 | 1.6s |
| 19 | Timerift | 18,001–19,000 | ×1.90 | 1.4s |
| 20 | Light | 19,001–20,000 | ×1.95 | 1.2s |
| — | Arena | 20,001+ | ×2.50 | 1.0s |

**Action time formula:** `max(1.0, 5.0 − ⌊(floor−1)/1000⌋ × 0.2)`

### Checkpoints

- Checkpoint every 10 floors (after miniboss)
- Death → return to last checkpoint
- Arena: no checkpoints (death → floor 20,001)

### Drop Rates (per floor)

| Item | Chance | Boss × | Gate Floor |
|:---|:---:|:---:|:---:|
| Fino | 0.15% | ×3 | 1 |
| Shot | 0.09% | ×3 | 1,001 |
| Cigarro | 0.09% | ×3 | 3,001 |
| Caneca | 0.04% | ×3 | 5,001 |
| Canhão | 0.03% | ×3 | 7,001 |
| Penalty | 0.02% | ×3 | 9,001 |
| Instrument Part | 0.03% | ×3 | — |
| Equipment | 0.03% | ×3 | — |

### Consumable Effects

| Item | Effect |
|:---|:---|
| Fino | Heal 25% HP (150s cooldown) |
| Caneca | Heal 50% HP (300s cooldown) |
| Cigarro | 3 shield charges |
| Canhão | 3 boost charges |
| Shot | ×1.20 power for 5 battles |
| Penalty | −50% speed, +50% crit, min 0.5s action |

---

## 3. Boss Mode

Boss mode uses a separate scaling formula tied to boss level.

### Boss Base Stats

| Stat | Value |
|---|---:|
| HP | 150 |
| Power | 25 |
| Speed | 6 |
| Defense | 10 |
| Critical | 15% |

### Scaling Parameters

| Parameter | Value |
|---|:---:|
| Stage offset | 1,000 |
| Boss stage scaling | 10 |
| Boss stat multiplier | ×2.0 |
| XP per boss level | 120 |
| Boss level XP power | 0.6 |
| XP level penalty rate | 1% |
| Min XP level multiplier | 0.10 |
| Level bonus per level | +1% (cap ×2.0) |
| Boss win Fidelis | 430 |

### Drop Rates (Boss Mode — 2× stage rates)

| Item | Chance |
|:---|:---:|
| Fino | 0.30% |
| Shot | 0.18% |
| Cigarro | 0.18% |
| Caneca | 0.08% |
| Canhão | 0.06% |
| Penalty | 0.04% |
| Instrument Part | 0.06% |
| Equipment | 0.06% |
| FITAB | 0.50% |

---

## 4. Equipment & Forging

### Equipment Slot Stats

| Slot | HP | Power | Defense |
|:---|---:|---:|---:|
| Head | 60 | 10 | 15 |
| Shoulders | 45 | 5 | 20 |
| Chest | 100 | 15 | 35 |
| Gloves | 10 | 25 | 3 |
| Legs | 60 | 10 | 15 |
| Boots | 30 | 5 | 10 |
| Instrument | 8 | 12 | 5 |

Quality range: **0.70–1.30**. Enhancement: **+5% per level**, max **15** (+75%).

### Drink Resources for Forging

| Drink | Energy | Forge Cost | Unlock Floor |
|:---|:---:|:---:|:---:|
| Cerveja | 1 | 1 | 1 |
| Vinho | 2 | 1 | 1,001 |
| Licor | 3 | 2 | 2,001 |
| Rum | 4 | 2 | 3,001 |
| Tequilla | 5 | 3 | 4,001 |
| Vodka | 6 | 3 | 5,001 |
| Gin | 7 | 4 | 6,001 |
| Whisky | 8 | 5 | 7,001 |
| Absinto | 9 | 6 | 8,001 |
| Aguardente | 10 | 8 | 9,001 |

Energy regeneration: 1 energy per **60 seconds**. Cast time: **3 seconds**.

### Weapon Forging

| Parameter | Value |
|---|:---:|
| Weapon upgrade base cost | 50 |
| Weapon upgrade per level | 50 |
| Max weapon level | 20 |
| Stat bonus per level | +5% |
| Two-handed multiplier | ×2.0 |
| Instrument quality range | 0.85–1.15 |
| Upgrade levels per drink tier | 5 |

### Crit & Speed Rolls

| Roll | Min Drink Cost | Chance | Range |
|:---|:---:|:---:|:---|
| Critical | 7 | 30% | +1%–10% |
| Speed | 7 | 25% | +1–5 |

---

## 5. FITAB Drop Rates

| Source | Chance |
|:---|:---:|
| Stage Mode | 0.20% |
| Battle (Arena) | 0.10% |
| Boss Mode | 0.50% |

---

## 6. Daily Reward

| Parameter | Value |
|---|:---:|
| Base Fidelis | 500 |
| Per-level bonus | +5 |
| Balance percent | 0% |

---

## Key Files

| File | Purpose |
|---|---|
| `scaling.config.json` | Master config for all scaling values |
| `MyTunoScalingConfiguration.cs` | Config POCO (deserialization target) |
| `MyTunoScaling.cs` | Static singleton — `MyTunoScaling.Configure()` |
| `StageService.cs` | Floor progression, enemy creation, action time |
| `StageBiomeService.cs` | Biome lookup, boss/miniboss detection, encounter rules |
| `DeterministicCombatEngine.cs` | Pre-computed combat outcomes (seeded RNG) |
| `CombatActionService.cs` | Interactive combat actions (auto-attacks, spells) |
| `docs/my_tuno/STAGE_ENEMY_SCALING.md` | Full 21-tier enemy stat tables |
