# My Tuno — Current Balance & Scaling Overview

## Base Character (Level 1)

| Stat | Value |
|------|------:|
| HP | 200 |
| Power | 25 |
| Defense | 20 |
| Speed | 10 |
| Critical Chance | 0% |

**Level scaling:** `1.0 + (level - 1) × 0.008` — at level 100: **1.792× multiplier** on total stats.

---

## Stat Upgrades (Fidelis)

| Stat | Bonus/Lvl | Fidelis Formula | Max Lvl | Leitão Starts |
|------|----------:|-----------------|--------:|--------------:|
| HP | +100 | 80 + n×80 | ∞ | 100 |
| Power | +15 | 80 + n×80 | ∞ | 100 |
| Defense | +12 | 80 + n×80 | ∞ | 100 |
| Speed | +1.5 | 175 + n×175 | 41 | 20 |
| Crit | +0.5% | 140 + n×140 | 100 (cap 50%) | 50 |

**Leitão formula:** `1 + (n - startLevel) / 5` piggies (integer division). Leitão earned from Boss Mode.

---

## Equipment (6 Armor Slots)

| Slot | Base HP | Base Power | Base Defense |
|------|--------:|-----------:|-----------:|
| Head | 60 | 10 | 15 |
| Shoulders | 45 | 5 | 20 |
| Chest | 100 | 15 | 35 |
| Gloves | 10 | 25 | 3 |
| Legs | 60 | 10 | 15 |
| Boots | 30 | 5 | 10 |
| **Total** | **305** | **70** | **98** |

**Enhancement:** `stat × quality × (1 + effectiveLevel × 0.05)` where `effectiveLevel = floor(highestStage/100) + purchasedBonusLevel`.

**Upgrade costs:** Fidelis (40 + n×40) + Drink (tiered, every 50 levels) + Leitão (from level 12).

**Drink tier schedule (50 levels each):**
Cerveja → Vinho → Licor → Rum → Tequilla → Vodka → Gin → Whisky → Absinto → Aguardente

---

## Weapons (Forged)

- Max weapon level: **20** (upgrade: 50 + n×50 Fidelis + Leitão from level 12)
- Stat bonus: **+5% per level** (`weaponUpgradeStatBonus: 0.05`)
- Two-handed: **2× base stats**, costs 2× drinks to forge
- Quality: 0.85–1.15 for instruments

---

## Stage Mode (Biomes & Enemies)

21 biomes, each spanning 1000 floors. Bosses every 100 floors, minibosses every 10.

| Biome | Floors | Enemy HP | Boss HP | Fidelis/Kill | Drink Unlock |
|-------|-------:|---------:|--------:|-------------:|:-------------|
| Forest | 1–1000 | 150 | 1,200 | 30 | Cerveja (1) |
| Swamp | 1001–2000 | 350 | 2,800 | 55 | Vinho (2001) |
| Mountains | 2001–3000 | 700 | 5,500 | 85 | — |
| Snowy | 3001–4000 | 1,200 | 9,500 | 110 | Licor (4001) |
| Tropical | 4001–5000 | 1,900 | 15,000 | 140 | — |
| Caverns | 5001–6000 | 2,800 | 22,000 | 170 | Rum (6001) |
| Desert | 6001–7000 | 4,000 | 31,000 | 210 | — |
| Volcanic | 7001–8000 | 5,800 | 45,000 | 260 | Tequilla (8001) |
| Ruins | 8001–9000 | 8,000 | 62,000 | 310 | — |
| Sky | 9001–10000 | 11,000 | 85,000 | 370 | Vodka (10001) |
| Underwater | 10001–11000 | 14,000 | 108,000 | 430 | — |
| Underground | 11001–12000 | 17,500 | 135,000 | 500 | Gin (12001) |
| Mechanical | 12001–13000 | 22,000 | 170,000 | 570 | — |
| Frostfire | 13001–14000 | 27,500 | 212,000 | 650 | Whisky (14001) |
| Corruption | 14001–15000 | 34,000 | 262,000 | 740 | — |
| Dark | 15001–16000 | 42,000 | 323,000 | 840 | Absinto (16001) |
| Alien | 16001–17000 | 52,000 | 400,000 | 950 | — |
| Void | 17001–18000 | 65,000 | 500,000 | 1,070 | Aguardente (18001) |
| Timerift | 18001–19000 | 81,000 | 624,000 | 1,200 | — |
| Light | 19001–20000 | 100,000 | 770,000 | 1,350 | — |
| Arena | 20001+ | 125,000 | 963,000 | 1,500 | — |

**Boss sprites:** Deterministic — boss_1 at floor 100, boss_2 at 200, …, boss_10 at 1000 (10 per biome). Arena uses modulus selection.

---

## Boss Mode

- Separate from stages. Own progression (boss stage 1, 2, 3, …)
- Base boss stats: HP 150, Power 25, Speed 6, Defense 10, Crit 15%
- Scaling: `bossStat × bossStatMultiplier(2.5) × (1 + bossStage × bossStageScaling(10))`
- Fidelis reward: **430 per win**
- Leitão drop: 15% per boss kill + guaranteed every 5 bosses
- Uses "jeans" biome sprites (49 generic enemies, no boss-prefixed files)

---

## Consumables

| Item | Effect | Cooldown | Drop Rate |
|------|--------|----------|-----------|
| Fino | Heal 25% HP | 150s | 0.15% |
| Caneca | Heal 50% HP | 300s | 0.04% |
| Shot | +5% all stats ×5 battles | — | 0.09% |
| Cigarro | +10% dodge ×5 battles | — | 0.09% |
| Canhão | AOE ×5 battles | — | 0.03% |
| Penalty | 0.5% lifesteal ×5 battles | — | 0.02% |

---

## Progression Milestones (Approximate)

| Level | Stage | Biome | Total HP (est.) | Total Power (est.) | Notes |
|------:|------:|:------|----------------:|-------------------:|:------|
| 1 | 1–10 | Forest | 505 | 95 | Fresh character + base equipment |
| 10 | ~200 | Forest | 700 | 130 | A few stat upgrades, equipment +2 |
| 20 | ~500 | Forest | 1,200 | 200 | Approaching first boss cycle |
| 30 | 1000–1500 | Swamp | 2,500 | 350 | Vinho unlocked, equipment +10 |
| 50 | 3000–4000 | Snowy | 6,000 | 700 | Licor unlocked, maxing speed |
| 70 | 6000–8000 | Desert/Volcanic | 15,000 | 1,500 | Tequilla, forged weapons |
| 100 | 10000+ | Sky/Underwater | 40,000+ | 4,000+ | Level cap, stat upgrades continue |

*Estimates assume moderate upgrade investment and average equipment quality.*

---

## Combat Formula

$$\text{Damage} = \text{Power} × \text{LevelScale} × \frac{\text{defenseK}(500)}{\text{defenseK} + \text{EnemyDefense}}$$

- Crit: 2× damage, chance capped at 50%
- Min damage: 1
- Action time determines attack speed (lower = faster)

---

## Gathering (Energy System)

Drinks cost energy to gather. Energy regenerates every 60 seconds.

| Drink | Energy Cost | Unlock Stage |
|-------|----------:|-------------:|
| Cerveja | 1 | 1 |
| Vinho | 2 | 2001 |
| Licor | 3 | 4001 |
| Rum | 4 | 6001 |
| Tequilla | 5 | 8001 |
| Vodka | 6 | 10001 |
| Gin | 7 | 12001 |
| Whisky | 8 | 14001 |
| Absinto | 9 | 16001 |
| Aguardente | 10 | 18001 |

---

## Rating / Rank System

Arena PvP determines rating. Ranks:
- Iron III/II/I (0–299)
- Bronze III/II/I (300–599)
- Silver III/II/I (600–899)
- Gold III/II/I (900–1199)
- Platinum III/II/I (1200–1499)
- Diamond III/II/I (1500–1799)
- Master III/II/I (1800–2099)
- Grandmaster III/II/I (2100–2399)
- Legend (2400–2699)
- Challenger (2700+)
