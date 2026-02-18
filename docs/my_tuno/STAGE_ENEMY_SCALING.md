# Stage Mode — Floor & Enemy Scaling Tables

> Generated from `scaling.config.json` v5.0.0. All values are **flat per tier** (no per-floor formula).

---

## Structure Overview

- **20 Stages** (biomes), each with **1,000 floors** = **20,000 total floors**
- **Arena** starts at floor **20,001** (endless, tier 21)
- Each 10-floor block: floors 1–9 = normal enemies (1–9 mobs), floor 10 = **Miniboss** (1 mob)
- Every **100 floors** = **Boss** (1 mob, boss sprite `boss_N_X`)
- Each stage has **10 bosses** (at floors 100, 200, …, 1000 relative to stage start)

---

## Encounter Pattern (per 10-floor block)

| Floor Offset | Enemy Count | Type |
|:---:|:---:|:---|
| 1 | 1 | Normal |
| 2 | 2 | Normal |
| 3 | 3 | Normal |
| 4 | 4 | Normal |
| 5 | 5 | Normal |
| 6 | 6 | Normal |
| 7 | 7 | Normal |
| 8 | 8 | Normal |
| 9 | 9 | Normal |
| 10 | 1 | **Miniboss** |

Boss floors (every 100th) override the miniboss — 1 **Boss** enemy instead.

---

## Biome Progression (20 Stages + Arena)

| Stage | Biome | Floors | Reward Mult | Action Time |
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
| — | **Arena** | 20,001+ | ×2.50 | 1.0s |

**Action time formula:** `max(1.0, 5.0 − biomeIndex × 0.2)` where `biomeIndex = ⌊(floor − 1) / 1000⌋`.

---

## Enemy Tier Stats — Normal

| Tier | Floors | HP | Power | Defense | Speed | Crit | Fidelis | XP |
|:---:|:---|---:|---:|---:|:---:|:---:|---:|---:|
| 1 | 1–1,000 | 150 | 15 | 10 | 8 | 3% | 30 | 20 |
| 2 | 1,001–2,000 | 350 | 30 | 25 | 9 | 4% | 55 | 40 |
| 3 | 2,001–3,000 | 700 | 55 | 45 | 9 | 4% | 85 | 65 |
| 4 | 3,001–4,000 | 1,200 | 90 | 70 | 10 | 5% | 110 | 85 |
| 5 | 4,001–5,000 | 1,900 | 140 | 110 | 10 | 5% | 140 | 110 |
| 6 | 5,001–6,000 | 2,800 | 200 | 160 | 11 | 6% | 170 | 130 |
| 7 | 6,001–7,000 | 4,000 | 280 | 220 | 11 | 6% | 210 | 155 |
| 8 | 7,001–8,000 | 5,800 | 380 | 300 | 12 | 7% | 260 | 180 |
| 9 | 8,001–9,000 | 8,000 | 500 | 400 | 12 | 7% | 310 | 200 |
| 10 | 9,001–10,000 | 11,000 | 650 | 520 | 13 | 8% | 370 | 230 |
| 11 | 10,001–11,000 | 14,000 | 820 | 660 | 13 | 8% | 430 | 260 |
| 12 | 11,001–12,000 | 17,500 | 1,020 | 830 | 14 | 9% | 500 | 290 |
| 13 | 12,001–13,000 | 22,000 | 1,280 | 1,050 | 14 | 9% | 570 | 320 |
| 14 | 13,001–14,000 | 27,500 | 1,600 | 1,320 | 14 | 9% | 650 | 350 |
| 15 | 14,001–15,000 | 34,000 | 2,000 | 1,660 | 15 | 10% | 740 | 380 |
| 16 | 15,001–16,000 | 42,000 | 2,500 | 2,080 | 15 | 10% | 840 | 420 |
| 17 | 16,001–17,000 | 52,000 | 3,120 | 2,600 | 15 | 10% | 950 | 460 |
| 18 | 17,001–18,000 | 65,000 | 3,900 | 3,250 | 16 | 11% | 1,070 | 500 |
| 19 | 18,001–19,000 | 81,000 | 4,880 | 4,060 | 16 | 11% | 1,200 | 540 |
| 20 | 19,001–20,000 | 100,000 | 6,100 | 5,080 | 17 | 12% | 1,350 | 580 |
| 21 | 20,001+ (Arena) | 125,000 | 7,620 | 6,350 | 17 | 12% | 1,500 | 620 |

---

## Enemy Tier Stats — Miniboss (every 10 floors)

| Tier | HP | Power | Defense |
|:---:|---:|---:|---:|
| 1 | 400 | 35 | 25 |
| 2 | 900 | 70 | 55 |
| 3 | 1,800 | 130 | 100 |
| 4 | 3,000 | 210 | 160 |
| 5 | 4,800 | 330 | 250 |
| 6 | 7,000 | 470 | 370 |
| 7 | 10,000 | 660 | 510 |
| 8 | 14,500 | 900 | 690 |
| 9 | 20,000 | 1,180 | 920 |
| 10 | 27,500 | 1,530 | 1,200 |
| 11 | 35,000 | 1,900 | 1,500 |
| 12 | 44,000 | 2,400 | 1,900 |
| 13 | 55,000 | 3,000 | 2,400 |
| 14 | 69,000 | 3,800 | 3,000 |
| 15 | 85,000 | 4,700 | 3,800 |
| 16 | 105,000 | 5,900 | 4,800 |
| 17 | 130,000 | 7,300 | 6,000 |
| 18 | 163,000 | 9,200 | 7,500 |
| 19 | 203,000 | 11,500 | 9,300 |
| 20 | 250,000 | 14,300 | 11,700 |
| 21 | 313,000 | 17,900 | 14,600 |

Miniboss Speed/Crit/Fidelis/XP = same as the tier's normal values.

---

## Enemy Tier Stats — Boss (every 100 floors)

| Tier | HP | Power | Defense |
|:---:|---:|---:|---:|
| 1 | 1,200 | 80 | 55 |
| 2 | 2,800 | 180 | 120 |
| 3 | 5,500 | 340 | 230 |
| 4 | 9,500 | 560 | 380 |
| 5 | 15,000 | 880 | 600 |
| 6 | 22,000 | 1,250 | 870 |
| 7 | 31,000 | 1,750 | 1,200 |
| 8 | 45,000 | 2,400 | 1,650 |
| 9 | 62,000 | 3,150 | 2,200 |
| 10 | 85,000 | 4,100 | 2,850 |
| 11 | 108,000 | 5,200 | 3,600 |
| 12 | 135,000 | 6,400 | 4,600 |
| 13 | 170,000 | 8,100 | 5,800 |
| 14 | 212,000 | 10,100 | 7,300 |
| 15 | 262,000 | 12,600 | 9,100 |
| 16 | 323,000 | 15,750 | 11,400 |
| 17 | 400,000 | 19,700 | 14,300 |
| 18 | 500,000 | 24,600 | 17,900 |
| 19 | 624,000 | 30,700 | 22,300 |
| 20 | 770,000 | 38,400 | 28,000 |
| 21 | 963,000 | 48,000 | 35,000 |

Boss uses ×1.8 `bossMultiplier` on Fidelis/XP rewards.

---

## Consumable Drop Gates

Consumables unlock based on highest-floor-reached progression:

| Item | Unlocks At | Stage (Biome) |
|:---|:---:|:---|
| Fino | Floor 1 | Forest |
| Shot | Floor 1,001 | Swamp |
| Cigarro | Floor 3,001 | Snowy |
| Caneca | Floor 5,001 | Caverns |
| Canhão | Floor 7,001 | Volcanic |
| Penalty | Floor 9,001 | Sky |

Boss kills apply **×3** drop rate multiplier (`bossDropMultiplier`).

---

## Item Drop Rates (per floor clear)

| Item | Drop % | Notes |
|:---|:---:|:---|
| Fino | 0.15% | Available from floor 1 |
| Shot | 0.09% | Unlocks floor 1,001 |
| Cigarro | 0.09% | Unlocks floor 3,001 |
| Caneca | 0.04% | Unlocks floor 5,001 |
| Canhão | 0.03% | Unlocks floor 7,001 |
| Penalty | 0.02% | Unlocks floor 9,001 |
| Instrument Part | 0.03% | — |
| Equipment | 0.03% | — |

---

## Checkpoints & Progression

- **Checkpoint** every 10 floors (after each miniboss): floors 1, 11, 21, 31, …
- On defeat → return to last checkpoint
- **Floor 20,000 boss** = final biome boss → unlocks Arena (20,001+)
- Arena has no checkpoints — defeat returns to floor 20,001
