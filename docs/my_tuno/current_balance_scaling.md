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
| HP | +100 | 80 + n×80 | ∞ | **300** |
| Power | +15 | 80 + n×80 | ∞ | **300** |
| Defense | +12 | 80 + n×80 | ∞ | **300** |
| Speed | +1.5 | 175 + n×175 | 41 | 20 |
| Crit | +0.5% | 140 + n×140 | 100 (cap 50%) | 50 |

**Leitão formula:** `1 + (n - startLevel) / 5` piggies (integer division). Leitão earned from Boss Mode.

> HP/Power/Defense stats are free (Fidelis only) until upgrade level 300. Above 300 they cost Leitões — a significant mid/late-game gating mechanism.

---

## Equipment (6 Armor Slots)

| Slot | Base HP | Base Power | Base Defense |
|------|--------:|-----------:|-----------:|
| Head | 75 | 13 | 20 |
| Shoulders | 60 | 7 | 25 |
| Chest | 130 | 20 | 45 |
| Gloves | 15 | 32 | 5 |
| Legs | 75 | 13 | 20 |
| Boots | 40 | 6 | 13 |
| **Total** | **395** | **91** | **128** |

**Enhancement:** `stat × quality × (1 + effectiveLevel × 0.05)` where `effectiveLevel = floor(highestStage/100) + purchasedBonusLevel`.

**Upgrade costs:** Fidelis (40 + n×40) + Drink (tiered, every 50 levels) + Leitão (from level **500**).

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

> **v6 changes:** Each enemy has a 1% global HP/Power/Defense buff (`enemyStatBuff: 1.01`). Crit chances now scale by biome (4%→50%). Special attacks are disabled — only normal attacks occur in Stage Mode.

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

## Progression Milestones — Live Server Data

The following is based on actual player data (DB snapshot). Stats include level scaling × (base + upgrades) + equipment bonuses.

| Player | Level | Stage | HP | Power | Defense | Crit | Notes |
|:-------|------:|------:|---:|------:|--------:|-----:|:------|
| calimero | 91 | 3,017 | ~35,800 | ~5,508 | ~3,503 | 25% | Leading player; Snowy biome |
| mija | 89 | 2,868 | ~18,225 | ~2,856 | ~2,366 | 25% | End of Swamp |
| atchim | 55 | 3,005* | ~15,450 | ~2,415 | ~968 | 25% | Highest 3005; currently replaying Forest |
| elchapo | 48 | 1,816 | ~13,135 | ~1,988 | ~1,283 | 5.5% | Mid Swamp |
| 22 | 28 | 457 | ~5,212 | ~629 | ~945 | 4.5% | Early Forest |

*\* atchim's current stage is 491 (Forest); highest is 3,005 (Mountains).*

**Enemy difficulty at current leading stages:**

| Stage | Enemy HP | Enemy Power | Enemy Defense | Hits to kill (calimero) | Dmg/hit by enemy |
|------:|---------:|------------:|--------------:|------------------------:|-------------------:|
| 3,011 | ~6,684 | ~501 | ~390 | ~2 | ~62 |
| 5,000 | ~16,307 | ~1,201 | ~944 | ~7–9 | ~150 |
| 7,000 | ~46,460 | ~3,253 | ~2,552 | ~35–45 | ~378 |
| 10,000 | ~177,760 | ~10,504 | ~8,403 | ~170+ | ~1,280 |

*Dmg formula: `Power × (500 / (500 + EnemyDefense))`, with 25% crit doubling ~25% of hits.*

**Conclusion:** Current top players (calimero, mija) are comfortable through tier 3 (stages 1–3,000). The next major difficulty wall is **tier 5–6 (stages 4,000–6,000)** where enemy HP and defense grow significantly faster than player stat gains via Fidelis alone. Heavy equipment investment and crit upgrades are critical from stage 3,000 onwards.

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
