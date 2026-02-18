# MyTuno — Scaling & Progression Reference

> Source of truth: `scaling.config.json` v5.0.0.
> For full enemy tier tables see `docs/my_tuno/STAGE_ENEMY_SCALING.md`.

---

## 1. Core Formulas

### Level Scale Factor

```
LevelScaleFactor = 1.0 + bonusPerLevel × (Level − 1)
                 = 1.0 + 0.008 × (Level − 1)
```

### Stat Formulas

| Stat | Formula |
|---|---|
| **TotalHP** | `BaseHP + (UpgradeLevel × 100)` |
| **TotalPower** | `BasePower + (UpgradeLevel × 15)` |
| **TotalSpeed** | `BaseSpeed + (UpgradeLevel × 1.5)` |
| **TotalDefense** | `BaseDef + (UpgradeLevel × 12)` |
| **CriticalChance** | `min(0.40, BaseCrit + UpgradeLevel × 0.005)` |

### XP Per Level

```
XP(Level → Level+1) = XPBase × Level^XPExponent
                     = 100 × Level^1.5
```

---

## 2. Base Stats (Character Creation)

| Stat | Value |
|---|---:|
| HP | 200 |
| Power | 25 |
| Speed | 10 |
| Defense | 20 |
| Critical | 2% |
| Max Level | 100 |

---

## 3. Upgrade Costs (Fidelis)

All upgrades use **linear cost**: `Cost(N) = BaseCost + N × CostPerLevel`.

| Stat | Flat Bonus | Base Cost | Per-Level | Max Upgrades | Cost @ Max |
|---|:---:|---:|---:|:---:|---:|
| HP | +100 | 50 | 50 | ∞ | — |
| Power | +15 | 50 | 50 | ∞ | — |
| Speed | +1.5 | 150 | 150 | 41 | 6,300 |
| Defense | +12 | 50 | 50 | ∞ | — |
| Critical | +0.5% | 120 | 120 | 80 | 9,720 |

### Upgrade Cost Milestones

| Upgrade # | HP/Power/Def | Speed | Critical |
|---:|---:|---:|---:|
| 1 | 100 | 300 | 240 |
| 5 | 300 | 900 | 720 |
| 10 | 550 | 1,650 | 1,320 |
| 20 | 1,050 | 3,150 | 2,520 |
| 30 | 1,550 | 4,650 | 3,720 |
| 41 | — | 6,300 | 5,040 |
| 50 | 2,550 | — | 6,120 |
| 80 | 4,050 | — | 9,720 |

---

## 4. Improvement Costs (Fidelis)

| Improvement | Flat Bonus | Base Cost | Per-Level |
|---|:---:|---:|---:|
| Energy Amount | +2 | 150 | 150 |
| Energy Regen | +2 | 300 | 300 |
| Shot Buff Bonus | +0.5% | 500 | 500 (max 40) |
| Fidelis Earned | +2% | 200 | 200 |

---

## 5. Power Costs (Fidelis)

| Power | Flat Bonus | Base Cost | Per-Level |
|---|:---:|---:|---:|
| Heavy Attack | +5% dmg | 100 | 100 |
| Special Attack | +5% dmg | 250 | 250 |

---

## 6. Combat Mechanics

| Parameter | Value |
|---|:---:|
| Defense K constant | 500 |
| Min damage | 1 |
| Crit chance cap | 40% |
| Crit multiplier | ×2.0 |
| Shot buff multiplier | ×1.20 |

**Damage formula:**
```
DamageMult = DefenseK / (DefenseK + TargetDefense)
           = 500 / (500 + Defense)
Damage     = max(MinDamage, ⌊Power × DamageMult⌋)
```

---

## 7. Stage / Floor Progression

### Structure

- **20 biomes** × **1,000 floors** each = **20,000 floors**
- **Arena** at floor **20,001+** (endless, tier 21)
- Boss every **100** floors, Miniboss every **10** floors
- Normal floors: **1–9 enemies** (ramp within each 10-floor block)

### Tier-based Enemy Stats

Enemies use flat stats per tier — no per-floor scaling. All 21 tiers are listed in `docs/my_tuno/STAGE_ENEMY_SCALING.md`.

**Selected tiers for reference:**

| Tier | Floors | Normal HP | Boss HP | Fidelis | XP |
|:---:|:---|---:|---:|---:|---:|
| 1 | 1–1,000 | 150 | 1,200 | 30 | 20 |
| 5 | 4,001–5,000 | 1,900 | 15,000 | 140 | 110 |
| 10 | 9,001–10,000 | 11,000 | 85,000 | 370 | 230 |
| 15 | 14,001–15,000 | 34,000 | 262,000 | 740 | 380 |
| 20 | 19,001–20,000 | 100,000 | 770,000 | 1,350 | 580 |
| 21 | 20,001+ (Arena) | 125,000 | 963,000 | 1,500 | 620 |

### Action Time per Biome

```
ActionTime = max(1.0, 5.0 − biomeIndex × 0.2)
biomeIndex = ⌊(floor − 1) / 1000⌋
```

Forest (5.0s) → Swamp (4.8s) → … → Light (1.2s) → Arena (1.0s)

### Checkpoints

- Checkpoint every 10 floors (after miniboss)
- Death → return to last checkpoint
- Floor 20,000 boss → unlocks Arena

---

## 8. Biome List

| # | Biome | Floors | Reward × |
|:---:|:---|:---|:---:|
| 1 | Forest | 1–1,000 | 1.00 |
| 2 | Swamp | 1,001–2,000 | 1.05 |
| 3 | Mountains | 2,001–3,000 | 1.10 |
| 4 | Snowy | 3,001–4,000 | 1.15 |
| 5 | Tropical | 4,001–5,000 | 1.20 |
| 6 | Caverns | 5,001–6,000 | 1.25 |
| 7 | Desert | 6,001–7,000 | 1.30 |
| 8 | Volcanic | 7,001–8,000 | 1.35 |
| 9 | Ruins | 8,001–9,000 | 1.40 |
| 10 | Sky | 9,001–10,000 | 1.45 |
| 11 | Underwater | 10,001–11,000 | 1.50 |
| 12 | Underground | 11,001–12,000 | 1.55 |
| 13 | Mechanical | 12,001–13,000 | 1.60 |
| 14 | Frostfire | 13,001–14,000 | 1.65 |
| 15 | Corruption | 14,001–15,000 | 1.70 |
| 16 | Dark | 15,001–16,000 | 1.75 |
| 17 | Alien | 16,001–17,000 | 1.80 |
| 18 | Void | 17,001–18,000 | 1.85 |
| 19 | Timerift | 18,001–19,000 | 1.90 |
| 20 | Light | 19,001–20,000 | 1.95 |
| — | Arena | 20,001+ | 2.50 |

---

## 9. Drop Rates (Stage Mode)

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

---

## 10. Equipment

### Slot Base Stats

| Slot | HP | Power | Defense |
|:---|---:|---:|---:|
| Head | 60 | 10 | 15 |
| Shoulders | 45 | 5 | 20 |
| Chest | 100 | 15 | 35 |
| Gloves | 10 | 25 | 3 |
| Legs | 60 | 10 | 15 |
| Boots | 30 | 5 | 10 |
| Instrument | 8 | 12 | 5 |

### Quality & Enhancement

| Parameter | Value |
|---|:---:|
| Quality range | 0.70–1.30 |
| Enhancement bonus | +5% per level |
| Max enhancement | 15 |
| Enhancement at max | +75% |

### Discard Values (Fidelis)

| Type | Base Value |
|---|---:|
| Equipment | 20 |
| Instrument Part | 25 |
| Weapon | 30 |

Level scaling: `+1% per discard level` (`discardLevelScale = 0.01`).

---

## 11. Weapon Forging

### Drink Resources

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

### Forging Parameters

| Parameter | Value |
|---|:---:|
| Cast time | 5s |
| Weapon upgrade base cost | 50 |
| Weapon upgrade per level | 50 |
| Max weapon level | 20 |
| Weapon stat bonus / level | +5% |
| Equipment upgrade base cost | 40 |
| Equipment upgrade per level | 40 |
| Two-handed multiplier | ×2.0 |
| Instrument quality range | 0.85–1.15 |
| Upgrade levels per drink tier | 5 |

### Crit & Speed Rolls

| Roll | Min Drink Cost | Roll Chance | Range |
|:---|:---:|:---:|:---|
| Critical | 7 | 30% | +1%–10% |
| Speed | 7 | 25% | +1–5 |

---

## 12. Boss Mode

| Parameter | Value |
|---|:---:|
| Stage offset | 1,000 |
| Boss stage scaling | 10 |
| XP per boss level | 120 |
| Boss level XP power | 0.6 |
| XP level penalty rate | 1% |
| Min XP level multiplier | 0.10 |
| Level bonus per level | +1% |
| Level bonus cap | ×2.0 |
| Boss stat multiplier | ×2.0 |
| Boss win Fidelis | 430 |

### Boss Base Stats

| Stat | Value |
|---|---:|
| HP | 150 |
| Power | 25 |
| Speed | 6 |
| Defense | 10 |
| Critical | 15% |

### Boss Mode Drop Rates

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

## 13. Battle (Arena) Rewards

| Outcome | Fidelis | Base XP |
|:---|---:|---:|
| Win | 120 | 100 |
| Draw | 40 | 50 |

| Parameter | Value |
|---|:---:|
| Level diff scale | 1% |
| Level diff cap | ±50% |
| Revive cost | 30 |
| Restore HP cost | 20 |

### Arena Drop Rates

| Item | Chance |
|:---|:---:|
| Fino | 0.15% |
| Cigarro | 0.09% |
| Caneca | 0.04% |
| Canhão | 0.03% |
| Penalty | 0.02% |

---

## 14. FITAB Drop Rates

| Source | Chance |
|:---|:---:|
| Stage Mode | 0.20% |
| Battle (Arena) | 0.10% |
| Boss Mode | 0.50% |

---

## 15. Consumables

| Item | Effect | Cooldown |
|:---|:---|:---:|
| Fino | Heal 25% HP | 150s |
| Caneca | Heal 50% HP | 300s |
| Cigarro | 3 shield charges | — |
| Canhão | 3 boost charges | — |
| Shot | ×1.20 power for 5 battles | — |
| Penalty | −50% speed, +50% crit, min 0.5s action | — |

---

## 16. Daily Reward

| Parameter | Value |
|---|:---:|
| Base Fidelis | 500 |
| Per-level bonus | +5 |
| Balance percent | 0% |
