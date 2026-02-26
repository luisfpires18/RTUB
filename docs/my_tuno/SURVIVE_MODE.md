# Survive Mode — Technical Documentation

Survive mode is a **survivor.io-inspired** real-time action game built with **PixiJS 8** (TypeScript → IIFE bundle) and orchestrated by a **Blazor Server** Razor page.

---

## Architecture Overview

```
Blazor (SurviveMode.razor)
  ↕ JS Interop (surviveModeGame.start / .nextLevel / .destroy)
PixiJS Engine (SurviveScene.ts → pixi-build/pixiSurviveMode.js)
  └─ Subsystems: Input → Player → Camera → Weapons → Enemies → Projectiles → Upgrades → UI → Particles → Audio
```

### File Structure

```
pixi/
  scenes/
    SurviveScene.ts          # Orchestrator — wires all subsystems, game loop, XP orbs, win/death
  survive/
    types.ts                 # Constants, enums, interfaces, utility functions, ObjectPool, SpatialGrid
    weapons/
      WeaponDefs.ts          # 8 weapon definitions with 5-level progression each
    WeaponSystem.ts          # Manages active weapons, fires attacks per pattern, companion AI
    EnemyManager.ts          # Spawning, movement AI (4 behaviours), boss system, damage/death
    ProjectileManager.ts     # Projectile lifecycle: straight, homing, boomerang, AoE zones
    UpgradeSystem.ts         # Level-up popup (pick 1 of 3), XP thresholds, passive definitions
    InputManager.ts          # Keyboard (WASD/arrows), mouse click-to-move, touch joystick
    UIManager.ts             # HUD: timer, HP bar, kill/coin counters, weapon slots, boss HP, minimap
    ParticleSystem.ts        # Death bursts, screen flashes, floating damage numbers
    AudioManager.ts          # HTML5 Audio pools for SFX + background music
```

### Build

```bash
cd src/RTUB.Web
npm run build:pixi:survive   # single bundle
npm run build:pixi            # all bundles (arena + stage + survive)
```

Output: `wwwroot/js/pixi-build/pixiSurviveMode.js` (IIFE, ~117KB)

---

## Game Flow

### Per-Level Lifecycle

1. **Blazor** calls `surviveModeGame.start(containerId, levelData, dotNetRef)` via JS interop
2. **SurviveScene** creates PixiJS app, loads assets, builds world + player + subsystems
3. Player starts with **Tiro Certeiro** (auto-projectile weapon)
4. Enemies spawn in waves with **difficulty ramp** (speed + count increase per minute)
5. Killing enemies drops **XP orbs** → collecting orbs fills XP bar → triggers **level-up popup**
6. Level-up popup offers 3 choices: new weapon / weapon upgrade / passive buff
7. **Mid-boss** spawns at 50% timer; **final boss** spawns when timer expires
8. **Win condition**: Kill final boss (or survive timer on final level)
9. **Death condition**: Player HP reaches 0
10. JS calls `dotNetRef.invokeMethodAsync('OnLevelComplete' | 'OnPlayerDeath', kills, time, coins)`
11. Blazor persists progress via `SurviveModeService.CompleteLevelAsync()` and auto-starts next level

### Between Levels

- Blazor reloads character data, increments level counter
- Calls `surviveModeGame.nextLevel(newLevelData)` which destroys old scene and builds fresh
- Weapons/upgrades reset each level (no carry-over)

---

## Weapon System

### Weapon Slots

- **Max 6 weapons** simultaneously (`MAX_WEAPON_SLOTS`)
- Player starts with 1 weapon (Tiro Certeiro)
- New weapons acquired via level-up popup choices
- Each weapon has **5 levels** (`MAX_WEAPON_LEVEL`) of stat scaling

### Attack Patterns

| Pattern | Enum | Behaviour |
|---|---|---|
| **Projectile** | `AttackPattern.Projectile` | Fires at nearest enemy; Fire Rain variant targets random positions |
| **Orbital** | `AttackPattern.Orbital` | Blades orbit the player, damaging on contact |
| **AoE** | `AttackPattern.AoE` | Periodic explosion centered on player + screen flash |
| **Boomerang** | `AttackPattern.Boomerang` | Projectile that returns to origin, piercing multiple enemies |
| **Lightning** | `AttackPattern.Lightning` | Chain lightning that jumps between enemies (80% damage per chain) |
| **ForceField** | `AttackPattern.ForceField` | Persistent aura dealing tick damage to nearby enemies |
| **Companion** | `AttackPattern.Companion` | Orbiting pig (Leitão) that auto-fires at enemies |

### Weapon Definitions (8 weapons)

| # | ID | Name | Icon | Pattern | Level 1 → 5 Highlights |
|---|---|---|---|---|---|
| 1 | `sharpshot` | Tiro Certeiro | 🎯 | Projectile | 1→4 count, 0→2 pierce, +4 damage |
| 2 | `spinblade` | Lâmina Giratória | 🔪 | Orbital | 2→5 blades, +6 damage, +50 range |
| 3 | `shockwave` | Onda de Choque | 💥 | AoE | 3.0→1.9s CD, +11 damage, 100→180 radius |
| 4 | `boomerang` | Bumerangue | 🪃 | Boomerang | 1→3 count, 3→10 pierce, +8 damage |
| 5 | `lightning` | Raio | ⚡ | Lightning | 1→3 chains, 2→6 pierce, +12 damage |
| 6 | `forcefield` | Barreira | 🛡️ | ForceField | 70→130 radius, +5 damage, faster tick |
| 7 | `companion` | Leitão Aliado | 🐷 | Companion | 1→2 pigs at level 5, +6 damage, +70 range |
| 8 | `firerain` | Chuva de Fogo | 🔥 | Projectile | 1→4 count, 50→100 AoE, +11 damage |

### Weapon Level-Up Stats

Stats scale per level via `levels()` helper — each level adds deltas to: `damage`, `cooldown`, `range`, `count`, `pierce`, `aoeRadius`, `speedMult`.

### Weapon Evolution / Combination System

When **both ingredients** reach max level (5), the level-up popup **guarantees** the evolution as a choice. Evolving removes both base weapons and replaces them with one evolved weapon (net -1 slot, freeing space). Evolved weapons have a single powerful level and cannot be upgraded further.

| Evolution | Ingredients | Icon | Pattern | Special Behaviour |
|---|---|---|---|---|
| **Trovão Certeiro** | 🎯 Sharpshot + ⚡ Lightning | 🌩️ | Projectile | 4 electric projectiles, 3 pierce, 1.3× speed |
| **Lâmina Protetora** | 🔪 Spinblade + 🛡️ ForceField | ⚔️ | Orbital | 6 blades + periodic AoE pulse (120 radius, every 1.5s) |
| **Apocalipse** | 💥 Shockwave + 🔥 Fire Rain | ☄️ | AoE | 3 meteors targeting enemies (160 radius each), screen flash |
| **Leitão Bumerangue** | 🪃 Boomerang + 🐷 Companion | 🐗 | Companion | 3 pigs throwing boomerangs (5 pierce, 280 range) |

**Evolution flow:**
1. Player levels both ingredient weapons to 5
2. Next level-up popup guarantees the evolution card (orange border, "⟐ EVOLUÇÃO" badge)
3. Picking it fuses both weapons → evolved weapon spawns with full visuals
4. The freed slot can be filled with a new weapon on the next level-up

---

## Passive Upgrades (8 passives)

Acquired through level-up popup choices. Each has a max level (3 or 5).

| ID | Name | Icon | Effect per Level | Max |
|---|---|---|---|---|
| `moveSpeed` | Pés Rápidos | 🏃 | +12% movement speed (multiplicative) | 5 |
| `damage` | Força Bruta | ⚔️ | +15% total damage (multiplicative) | 5 |
| `hp` | Vitalidade | ❤️ | +15 max HP + heal 10 | 5 |
| `atkRange` | Olho de Águia | 🎯 | +12% range (multiplicative) | 5 |
| `atkSpeed` | Fogo Rápido | ⚡ | +12% attack speed (×0.88 cooldown) | 5 |
| `armor` | Defesa | 🛡️ | -8% damage received (multiplicative) | 5 |
| `magnet` | Íman de Moedas | 🧲 | +80 XP orb pickup radius | 3 |
| `coinRate` | Febre do Ouro | 🪙 | ×2 coins per enemy kill | 3 |

### Player Stats (mutable)

```typescript
interface PlayerStats {
  playerSpeed: number;      // base ~120, modified by Pés Rápidos
  maxHP: number;            // base 100, modified by Vitalidade
  playerHP: number;         // current HP
  damageMultiplier: number; // base 1.0, modified by Força Bruta
  cooldownMultiplier: number; // base 1.0, modified by Fogo Rápido (lower = faster)
  rangeMultiplier: number;  // base 1.0, modified by Olho de Águia
  magnetRadius: number;     // base 50, modified by Íman de Moedas
  coinDropMult: number;     // base 1.0, modified by Febre do Ouro
  armor: number;            // base 0, modified by Defesa (% reduction)
}
```

---

## Upgrade System (Level-Up Flow)

### XP Thresholds

```
Level 2:  5 orbs    Level 5: 35 orbs
Level 3: 12 orbs    Level 6: 55 orbs
Level 4: 22 orbs    Level 7: 80 orbs
Level 8+: previous + 35 orbs each
```

### Choice Selection

On level-up, 3 choices are drawn from a pool:
1. **New Weapon** — any not-yet-owned weapon (guaranteed 1 choice if <4 weapons)
2. **Weapon Level-Up** — upgrade an owned weapon (not at max)
3. **Passive** — acquire or upgrade a passive buff (not at max)

Pool is shuffled randomly. Game pauses while popup is active.

---

## Enemy System

### Enemy Types

| Type | Speed | HP | Behaviour |
|---|---|---|---|
| **Chaser** (80%) | Normal | Base | Direct pursuit with wobble |
| **Flanker** (10%) | ×1.15 | Base | Approaches at an angle, weaving |
| **Sprinter** (5%) | ×2.5 bursts | Base | Periodic dashes then pauses |
| **Tank** (5%, level 3+) | ×0.6 | Base | Slow but relentless |
| **Elite** (config%) | ×1.4 | ×3 | Yellow-outlined, larger |
| **Boss** (mid) | ×0.75 | ×80 | Clears all enemies on spawn |
| **Final Boss** | ×0.75 | ×160 | Crown icon, 2× boss HP, killing = win |

### Enemy Constants

```typescript
PLAYER_RADIUS = 24          ENEMY_HIT_RADIUS = 24
ENEMY_DAMAGE = 10           ELITE_DAMAGE = 20           BOSS_DAMAGE = 50
BASE_ENEMY_HP = 2           ELITE_HP_MULT = 3           BOSS_HP_MULT = 80
ENEMY_HP_PER_LEVEL = 1.0    INVULN_DURATION = 0.8s
```

### Difficulty Ramp

- **Spawn ramp**: +configured % more enemies per minute (default +20%)
- **Speed ramp**: enemies accelerate over time toward `maxEnemySpeed`
- **HP ramp**: +15% HP per elapsed minute
- **Wave formula**: `baseSpawn = ceil((3 + timeScale) × rampMult)` + burst bonus every 35s

### Boss Triggers

- **Mid-boss**: spawns at 50% of timer duration (clears all regular enemies)
- **Final boss**: spawns when timer expires (timer stops, must kill to win)
- **Final level**: no boss, simply survive the entire timer

---

## Companion System (Leitão Aliado 🐷)

The pig companion is a special weapon type:
- Orbits the player at a configurable radius
- Auto-fires pink projectiles at nearest enemy within range
- Level 5 grants a **second pig** (count: 1→2)
- Each pig has its own orbit angle, attack cooldown, and visual (body + snout + ears + eyes)
- Stats scale with passives (damage multiplier, cooldown multiplier, range multiplier)

---

## Input System

### Controls

| Input | Desktop | Mobile |
|---|---|---|
| Move | WASD / Arrow keys | Touch joystick (left half of screen) |
| Move (alt) | Mouse click-to-move | — |
| Upgrade choice | Click card | Tap card |

### Touch Joystick

- Spawns on pointer-down in left 55% of screen
- Knob follows finger within radius
- Deadzone: 10% of joystick radius
- Fades to 50% opacity when idle

---

## UI / HUD Elements

| Element | Position | Description |
|---|---|---|
| Timer bar | Top center | Countdown bar with time text |
| HP bar | Below timer | Red → green fill with HP text |
| Kill counter | Left side | Skull icon + kill count |
| XP counter | Left side | Star icon + orbs collected |
| Weapon slots | Bottom | Row of weapon icons with level badges |
| Boss HP bar | Top (when active) | Red bar with "BOSS" / "FINAL BOSS" label |
| Minimap | Bottom-right | 100×100px map with player dot + enemy dots |
| Level label | Top-left | "Level X — BiomeName" + "Lv.Y" (player level) |
| Messages | Center | Win/death overlay text |
| Upgrade popup | Center overlay | 3 cards with icon, title, description, type badge |

---

## Audio

| Sound | File | Trigger |
|---|---|---|
| Background music | `survival_battle.mp3` | Starts on level begin, loops |
| Win | `survive/win.mp3` | Level complete |
| Death | `survive/death.mp3` | Player death |
| Hit | `survive/hit.mp3` | Enemy hit + enemy death |
| Level-up | `survive/levelup.mp3` | XP level-up popup |
| Lightning | `survive/lightning.mp3` | Lightning chain |
| Explosion | `survive/explosion.mp3` | AoE/shockwave |

SFX uses a **pool of 4 Audio elements** per sound type for overlapping playback.

---

## Blazor Page (SurviveMode.razor)

### Route & Setup

```razor
@page "/my-tuno/survive"
@rendermode InteractiveServer
@attribute [Authorize]
```

### Top Bar (Stage-Style)

- Back button (left) → Level indicator (center, "LEVEL X") → Rewards strip (kills/coins/levels) → Controls (audio + stop, right)
- Buff indicators below: Cigarro dodge %, Canhão AOE, Penalty lifesteal

### Defeat Menu

- "SURVIVAL ENDED" banner → Level reached → Reward summary (levels, kills, coins)
- Two buttons: **RECOMEÇAR** (retry from level 1) + **IR MY TUNO** (go back)

### C# Methods

| Method | Trigger | Action |
|---|---|---|
| `AutoStartRun()` | Page load / retry | Starts from level 1 |
| `StartLevelGame()` | Each level | Builds level data, calls JS interop |
| `OnLevelComplete()` | JS callback | Persists progress, increments level, auto-starts next |
| `OnPlayerDeath()` | JS callback | Persists progress, shows defeat summary |
| `RetryRun()` | Defeat button | Destroys scene, resets state, calls `AutoStartRun()` |
| `EndRunEarly()` | Stop button | Destroys scene, shows defeat summary |

### JS Interop API

```javascript
window.surviveModeGame = {
  start(containerId, levelData, dotNetRef),  // Create scene + begin
  nextLevel(levelData),                       // Destroy old → create new
  pause(),                                    // Freeze game loop
  resume(),                                   // Unfreeze game loop
  setAudioEnabled(enabled),                   // Toggle audio
  destroy(),                                  // Full cleanup
  isActive(),                                 // Check if game running
};
```

---

## Level Data (SurviveLevelData)

Passed from Blazor to JS per level:

```typescript
interface SurviveLevelData {
  level: number;                    // Current level (1-based)
  biomeName: string;                // Biome visual theme
  timerDurationSeconds: number;     // Survival timer (seconds)
  mapWidth: number;                 // World size X
  mapHeight: number;                // World size Y
  viewportWidth: number;            // Viewport pixel width
  viewportHeight: number;           // Viewport pixel height
  playerSpeed: number;              // Base movement speed
  backgroundPath: string;           // Background image path
  enemySprites: string[];           // Enemy sprite paths
  playerSpritePath: string;         // Player sprite path
  bossSprites: string[];            // Boss sprite paths
  isFinalLevel: boolean;            // No boss — just survive timer
  baseEnemyCount: number;           // Initial enemies on screen
  maxEnemyCount: number;            // Cap on concurrent enemies
  spawnIntervalSeconds: number;     // Wave spawn interval
  enemySpeed: number;               // Base enemy speed
  maxEnemySpeed: number;            // Speed cap after ramp
  enemyScale: number;               // Visual scale multiplier
  hasEliteEnemies: boolean;         // Whether elites can spawn
  eliteSpawnChance: number;         // Chance per enemy (0-1)
  spawnRampPerMinute: number;       // Spawn count % increase per minute
  speedRampPerMinute: number;       // Speed % increase per minute
}
```

---

## Performance Optimizations

- **Object pools** for PIXI.Graphics (particles, XP orbs, projectiles, enemy containers)
- **Spatial hash grid** (128px cells) for enemy-player and enemy-projectile collision queries
- **Viewport culling** on enemy containers (hidden when off-screen)
- **Container reuse** for enemies (pool of 50)
- **Delta-time** based updates (framerate-independent)

---

## Known Gaps / Future Work

- [x] **Weapon evolution/combination** — ✅ 4 evolution recipes implemented
- [ ] **Carry-over upgrades** — persist some weapons/passives between levels
- [ ] **More enemy behaviours** — ranged enemies, summoners, shielded enemies
- [ ] **Biome-specific hazards** — environmental damage zones, obstacles
- [ ] **Equipment integration** — character equipment affecting base stats
- [ ] **Leaderboard** — persist best survival time / level reached across all players
