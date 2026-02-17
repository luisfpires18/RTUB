# MyTuno — Scaling & Progression Reference

> Auto-generated from `scaling.config.json` formulas and test assertions.
> All values assume **default config** (production). Quality rolls use average (1.0).

---

## 1. Core Formulas

### Level Scale Factor

```
LevelScaleFactor = 1.0 + 0.03 × (Level − 1)^1.55
```

Defense uses `√(LevelScaleFactor)` — it grows much slower than offensive stats.

### Stat Formulas

| Stat | Formula |
|---|---|
| **TotalHP** | `⌊BaseHP × LevelScaleFactor × (1.08)^HpUpgrades⌋ + EquipmentHPBonus` |
| **TotalPower** | `⌊BasePower × LevelScaleFactor × (1.04)^PowerUpgrades⌋ + EquipmentPowerBonus` |
| **TotalSpeed** | `⌊BaseSpeed × LevelScaleFactor⌋ + ⌊SpeedUpgrades × 1.5⌋` |
| **TotalDefense** | `⌊BaseDef × √LevelScaleFactor × (1.04)^DefenseUpgrades⌋ + EquipmentDefenseBonus` |
| **CriticalChance** | `min(0.50, CritUpgrades × 0.005)` |
| **ActionTime** | `max(1.0s, 5.0s − SpeedUpgrades × 0.0976s)` |

### XP Per Level

```
XP needed for Level N→N+1 = round(50 × N^2.2)
```

---

## 2. Level Progression — Base Stats (No Upgrades, No Equipment)

| Level | Scale | HP | Power | Defense | XP to Next | Cumulative XP |
|------:|------:|---:|------:|--------:|-----------:|--------------:|
| 1 | 1.00 | 100 | 10 | 5 | 50 | 0 |
| 5 | 1.26 | 126 | 13 | 6 | 1,725 | 1,897 |
| 10 | 1.90 | 190 | 19 | 7 | 7,924 | 20,949 |
| 15 | 2.79 | 279 | 28 | 8 | 19,336 | 81,208 |
| 20 | 3.88 | 388 | 39 | 10 | 36,411 | 209,700 |
| 25 | 5.13 | 513 | 51 | 11 | 59,489 | 435,452 |
| 30 | 6.54 | 654 | 65 | 13 | 88,846 | 789,049 |
| 40 | 9.78 | 978 | 98 | 16 | 167,302 | 2,008,393 |
| 50 | 13.50 | 1,350 | 135 | 18 | 273,341 | 4,135,276 |
| 60 | 17.67 | 1,767 | 177 | 21 | 408,228 | 7,451,408 |
| 70 | 22.25 | 2,225 | 222 | 24 | 573,041 | 12,250,253 |
| 80 | 27.21 | 2,721 | 272 | 26 | 768,720 | 18,835,394 |
| 90 | 32.53 | 3,253 | 325 | 29 | 996,101 | 27,519,329 |
| 100 | 38.18 | 3,818 | 382 | 31 | MAX | 38,622,557 |

---

## 3. With Moderate Upgrades (HP 10 / Power 10 / Speed 10 / Defense 5 / Crit 20)

> ActionTime ≈ 4.02s | CritChance = 10%

| Level | HP | Power | Defense | Notes |
|------:|---:|------:|--------:|-------|
| 1 | 216 | 15 | 6 | Starting with some investment |
| 10 | 411 | 28 | 8 | Early game |
| 20 | 837 | 57 | 12 | Forest biome cleared |
| 30 | 1,413 | 97 | 16 | Mountains |
| 40 | 2,110 | 145 | 19 | Snowy / Tropical |
| 50 | 2,915 | 200 | 22 | Caverns |
| 60 | 3,815 | 262 | 26 | Desert |
| 70 | 4,803 | 329 | 29 | Volcanic |
| 80 | 5,874 | 403 | 32 | Ruins |
| 90 | 7,022 | 481 | 35 | Sky |
| 100 | 8,244 | 565 | 38 | End-game baseline |

---

## 4. With Maxed Upgrades (HP 30 / Power 30 / Speed 41 / Defense 20 / Crit 100)

> ActionTime = 1.00s | CritChance = 50% (cap)

| Level | HP | Power | Defense | Notes |
|------:|---:|------:|--------:|-------|
| 1 | 1,006 | 32 | 11 | Whale at level 1 |
| 10 | 1,916 | 62 | 15 | |
| 20 | 3,903 | 126 | 22 | |
| 30 | 6,585 | 212 | 28 | |
| 40 | 9,837 | 317 | 34 | |
| 50 | 13,585 | 438 | 40 | |
| 60 | 17,781 | 573 | 46 | |
| 70 | 22,388 | 722 | 52 | |
| 80 | 27,379 | 882 | 57 | |
| 90 | 32,730 | 1,055 | 62 | |
| 100 | 38,423 | 1,238 | 68 | Fully maxed |

---

## 5. Equipment Bonuses (Full Set, Average Quality, No Enhancement)

### Armor Base Stats Per Slot

| Slot | HP | Power | Defense |
|------|---:|------:|--------:|
| Head | 60 | 10 | 15 |
| Shoulders | 45 | 5 | 20 |
| Chest | 100 | 15 | 35 |
| Gloves | 10 | 25 | 3 |
| Legs | 60 | 10 | 15 |
| Boots | 30 | 5 | 10 |
| Instrument | 8 | 12 | 5 |
| **Full Set** | **313** | **82** | **103** |

### Equipment Scaling by Level

Equipment scales with level: `bonus = baseStat × (1 + level × 0.05) × (1 + enhancement × 0.20)`

Weapons scale separately: `bonus = weaponStat × (1 + level × 0.02)`

| Level | Armor HP | Armor Pwr | Armor Def | Weapon HP | Weapon Pwr | Weapon Def | **Total HP** | **Total Pwr** | **Total Def** |
|------:|---------:|----------:|----------:|----------:|-----------:|-----------:|-------------:|--------------:|--------------:|
| 1 | 329 | 86 | 108 | 16 | 24 | 10 | **345** | **110** | **118** |
| 10 | 470 | 123 | 154 | 19 | 29 | 12 | **489** | **152** | **166** |
| 20 | 626 | 164 | 206 | 22 | 34 | 14 | **648** | **198** | **220** |
| 30 | 782 | 205 | 258 | 26 | 38 | 16 | **808** | **243** | **274** |
| 50 | 1,096 | 287 | 360 | 32 | 48 | 20 | **1,128** | **335** | **380** |
| 70 | 1,408 | 369 | 464 | 38 | 58 | 24 | **1,446** | **427** | **488** |
| 100 | 1,878 | 492 | 618 | 48 | 72 | 30 | **1,926** | **564** | **648** |

### Enhancement Multiplier (from Stages Cleared)

Enhancement level = `⌊highestStage / 100⌋ + slotBonusLevel`

Each enhancement level: **+20%** equipment stat bonus.

| Enhancement | Stage Milestone | Multiplier |
|------------:|----------------:|-----------:|
| 0 | 0 | ×1.0 |
| 1 | 100 | ×1.2 |
| 5 | 500 | ×2.0 |
| 10 | 1,000 | ×3.0 |
| 15 | 1,500 | ×4.0 |
| 20 | 2,000 | ×5.0 |

---

## 6. Combined Stats — "Where should I be?"

> Full set equipment (avg quality) + moderate upgrades (HP 10, Pwr 10, Spd 10, Def 5, Crit 20).
> No enhancement. ActionTime ≈ 4.02s, Crit = 10%.

| Level | HP (stat+equip) | Power (stat+equip) | Defense (stat+equip) | Suggested Stage |
|------:|----------------:|-------------------:|---------------------:|----------------:|
| 1 | 216 + 345 = **561** | 15 + 110 = **125** | 6 + 118 = **124** | 1–20 |
| 10 | 411 + 489 = **900** | 28 + 152 = **180** | 8 + 166 = **174** | 20–60 |
| 20 | 837 + 648 = **1,485** | 57 + 198 = **255** | 12 + 220 = **232** | 60–120 |
| 30 | 1,413 + 808 = **2,221** | 97 + 243 = **340** | 16 + 274 = **290** | 120–200 |
| 50 | 2,915 + 1,128 = **4,043** | 200 + 335 = **535** | 22 + 380 = **402** | 300–500 |
| 70 | 4,803 + 1,446 = **6,249** | 329 + 427 = **756** | 29 + 488 = **517** | 500–750 |
| 100 | 8,244 + 1,926 = **10,170** | 565 + 564 = **1,129** | 38 + 648 = **686** | 750–1,200 |

---

## 7. Stage Enemy Difficulty

### Difficulty Curve

```
difficultyCurve = 1 + 0.15 × (stage − 1)^1.05
```

Boss stages (every 10th): enemy stats × **1.8** boss multiplier.

### Enemy Stats By Stage

| Stage | Normal HP | Normal Pwr | Normal Def | Boss HP | Boss Pwr |
|------:|----------:|-----------:|-----------:|--------:|---------:|
| 1 | 35 | 8 | 2 | 216 | 36 |
| 10 | 88 | 20 | 5 | 541 | 90 |
| 50 | 348 | 79 | 20 | 2,145 | 357 |
| 100 | 689 | 157 | 39 | 4,252 | 709 |
| 200 | 1,396 | 319 | 80 | 8,617 | 1,436 |
| 300 | 2,122 | 485 | 121 | 13,098 | 2,183 |
| 500 | 3,609 | 825 | 206 | 22,273 | 3,712 |
| 700 | 5,127 | 1,172 | 293 | 31,639 | 5,273 |
| 1,000 | 7,443 | 1,701 | 425 | 45,934 | 7,656 |
| 1,500 | 11,379 | 2,601 | 650 | 70,222 | 11,704 |
| 2,000 | 15,382 | 3,516 | 879 | 94,927 | 15,821 |

### Encounter Pattern (every 10-stage block)

| Stage Offset | Enemies |
|-------------:|--------:|
| 1–2 | 1 |
| 3–4 | 2 |
| 5–6 | 3 |
| 7–8 | 4 |
| 9 | 5 |
| 10 (boss) | 1 (boss) |

### Biome Progression

| Biome | Stages | Reward Mult |
|-------|-------:|------------:|
| Forest | 1–100 | ×1.00 |
| Swamp | 101–200 | ×1.05 |
| Mountains | 201–300 | ×1.10 |
| Snowy | 301–400 | ×1.15 |
| Tropical | 401–500 | ×1.20 |
| Caverns | 501–600 | ×1.25 |
| Desert | 601–700 | ×1.30 |
| Volcanic | 701–800 | ×1.35 |
| Ruins | 801–900 | ×1.40 |
| Sky | 901–1,000 | ×1.45 |
| Underwater | 1,001–1,100 | ×1.50 |
| Underground | 1,101–1,200 | ×1.55 |
| Mechanical | 1,201–1,300 | ×1.60 |
| Frostfire | 1,301–1,400 | ×1.65 |
| Corruption | 1,401–1,500 | ×1.70 |
| Dark | 1,501–1,600 | ×1.75 |
| Alien | 1,601–1,700 | ×1.80 |
| Void | 1,701–1,800 | ×1.85 |
| Timerift | 1,801–1,900 | ×1.90 |
| Light | 1,901–2,000 | ×1.95 |
| Arena | 2,001+ | ×2.50 |

---

## 8. Upgrade Costs

### Formula

```
Cost = BaseCost × (1 + UpgradeCount)²
```

### Per-Stat Base Costs

| Stat | Base Cost |
|------|----------:|
| HP / Power / Defense | 30 |
| Speed | 35 |
| Critical Chance | 60 |

### Cost at Upgrade Count

| Upgrades | HP / Pwr / Def | Speed | Crit |
|---------:|---------------:|------:|-----:|
| 0 → 1 | 30 | 35 | 60 |
| 5 → 6 | 1,080 | 1,260 | 2,160 |
| 10 → 11 | 3,630 | 4,235 | 7,260 |
| 15 → 16 | 7,680 | 8,960 | 15,360 |
| 20 → 21 | 13,230 | 15,435 | 26,460 |
| 25 → 26 | 20,280 | 23,660 | 40,560 |
| 30 → 31 | 28,830 | 33,635 | 57,660 |
| 40 → 41 | 50,430 | 58,835 | 100,860 |
| 50 → 51 | 78,030 | — | 156,060 |

> Speed max = **41** upgrades (1.0s action time). Crit max = **100** upgrades (50% cap).

---

## 9. Combat

### Damage Formula

```
rawDamage = Power × rand(0.8–1.2) × [×2 if crit]
finalDamage = max(1, ⌊rawDamage × 1000 / (1000 + Defense)⌋)
```

### Defense Mitigation Table

| Defense | Damage Reduction |
|--------:|-----------------:|
| 100 | 9.1% |
| 250 | 20.0% |
| 500 | 33.3% |
| 1,000 | 50.0% |
| 2,000 | 66.7% |
| 5,000 | 83.3% |

### Consumable Effects

| Item | Effect | Details |
|------|--------|---------|
| **Fino** | Heal 25% max HP | 150s cooldown |
| **Caneca** | Heal 50% max HP | 300s cooldown |
| **Cigarro** | Shield | Absorbs 3 hits |
| **Canhão** | +30% damage | Next 3 hits |
| **Shot** | +20% Power | 5 battles |
| **Penalty** | −0.5s action time, +50% crit | Until end of run |

---

## 10. Weapon Forging

### Drink Tiers

| Drink | Energy Cost | Forge Cost | Unlock Stage | Weapon Levels |
|-------|------------:|-----------:|-------------:|--------------:|
| Cerveja | 1 | 1 | 1 | 1–5 |
| Vinho | 2 | 1 | 101 | 6–10 |
| Licor | 3 | 2 | 201 | 11–15 |
| Rum | 4 | 2 | 301 | 16–20 |
| Tequilla | 5 | 3 | 401 | 21–25 |
| Vodka | 6 | 3 | 501 | 26–30 |
| Gin | 7 | 4 | 601 | 31–35 |
| Whisky | 8 | 5 | 701 | 36–40 |
| Absinto | 9 | 6 | 801 | 41–45 |
| Aguardente | 10 | 8 | 901 | 46–50 |

> Gin+ drinks (energy ≥ 7) unlock crit/speed rolls on weapons.

### Weapon Stat Scaling

```
totalMult = drinkEnergyCost × (1 + weaponLevel × 0.10) × handedMult
BonusHP    = round(8  × totalMult)
BonusPower = round(12 × totalMult)
BonusDef   = round(5  × totalMult)
```

Two-handed multiplier: **×2.0** (matches dual-wield 1H).

### Weapon Upgrade Cost

```
upgradeCost = 50 × 1.18^currentLevel
```

Each level: **+10%** stat bonus.

---

## 11. Arena Rewards

```
rewardMultiplier = clamp(1.0 + (defenderLevel − attackerLevel) × 0.015, 0.05, 2.5)
levelScaling     = level^0.8
```

| Outcome | Base Fidelis | Base XP |
|---------|------------:|---------:|
| Win | 150 | 150 |
| Draw | 40 | 75 |
| Revive cost | 30 | — |
| Restore HP | 20 | — |

---

## 12. Boss Mode

```
bossDifficulty = difficultyCurve(1000 + (bossStage − 1) × 10) × 2.0
```

Base boss stats: HP=150, Power=25, Speed=6, Defense=10, Crit=15%.

Fidelis per win: **430**. XP per boss: `120 × bossLevel^0.6`.

---

## 13. Improvements (Game-wide Upgrades)

| Improvement | Effect per Upgrade | Base | Max | Cost Formula |
|---|---|---|---|---|
| Energy Amount | +2 max energy | 10 | ∞ | 100 × (1+n)² |
| Energy Regen | −2s regen interval | 60s | min 5s | 2,000 × (1+n)^2.8 |
| Shot Buff Bonus | +0.5% shot multiplier | 1.20 | 40 upgrades | 4,000 × (1+n)³ |
| Fidelis Earned | +2% Fidelis earnings | ×1.0 | ∞ | 400 × (1+n)² |

---

## 14. Powers (Combat Enhancements)

| Power | Bonus per Upgrade | Base Multiplier | Cost Formula |
|---|---|---|---|
| Heavy Attack | +0.05 | 2.0× | 80 × (1+n)² |
| Special Attack | +0.05 | spell-specific | 200 × (1+n)^2.5 |

---

## 15. Daily Rewards

```
dailyFidelis = 900 + level × 12 + balance × 0.05
```

---

## 16. Drop Rates (per enemy killed)

### Stage Mode

| Item | Normal | Boss (×3) |
|------|-------:|----------:|
| Fino | 0.15% | 0.45% |
| Shot | 0.09% | 0.27% |
| Cigarro | 0.09% | 0.27% |
| Caneca | 0.04% | 0.12% |
| Canhão | 0.03% | 0.09% |
| Penalty | 0.02% | 0.06% |
| Equipment | 0.03% | 0.09% |
| Instrument Part | 0.03% | 0.09% |

### Boss Mode (doubled rates)

Fino=0.30%, Shot=0.18%, Cigarro=0.18%, Caneca=0.08%, Canhão=0.06%, Penalty=0.04%, Equipment=0.06%, FITAB=0.50%
