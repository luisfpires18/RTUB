# Stage Mode Documentation

## Overview

Stage Mode is the main "push forward" mode in My Tuno where players auto-fight through numbered stages and collect resources as they clear them. Unlike Arena mode (PvP against other players' characters), Stage Mode features AI-controlled animal and mob enemies.

## User Interface

### Stage Selection Modal
When clicking "Stages" from MyTunoHome, a modal appears showing:
- Current stage progress
- Highest stage reached
- Last checkpoint
- Two start options:
  - **Start from Stage 1** - Begin fresh
  - **Start from Checkpoint** - Resume from last saved point

### Battle Screen
The Stage page is focused and minimal:
- Auto-starts battle when loaded
- Shows Phaser3 battle animation
- Speed controls (1x, 1.5x, 2x) and audio toggle
- After battle: loot display with rewards earned
- Continue/Return buttons

## Core Features

### Stage Progression
- Stages are numbered sequentially (1, 2, 3...)
- Players advance by defeating the enemy in each stage
- Progress is tracked per user through the `StageProgress` entity

### Checkpoints
- **Before Stage 100**: Checkpoint every 10 stages (1, 10, 20, 30...)
- **After Stage 100**: Checkpoint every 20 stages (100, 120, 140...)
- **After Stage 10,000**: No new checkpoints (Infinite Land)
- When defeated, players return to their last checkpoint

### Enemies & Rewards
Enemies drop rewards based on type and stage:
- **Fidelis**: Currency that scales with stage number
- **Beer**: Healing item (10% base drop rate)
- **Shot**: Special ability item (5% base drop rate)

### Enemy Types

| Type | Appearance | Stats | Drop Rate Multiplier |
|------|------------|-------|---------------------|
| Normal | Every stage (except mini-boss/boss) | Base stats | 1x |
| Mini-Boss | Every 10 stages | 2x-3x stats | 2x |
| Boss | Every 100 stages | 5x-10x stats | 3x |

### Biomes/Regions

Stages are grouped into regions, each spanning 100 stages:

| Region | Stage Range | Type |
|--------|-------------|------|
| Forest | 1-100 | Starting zone |
| Desert | 101-200 | Sandy terrain |
| Mountains | 201-300 | Rocky peaks |
| Swamp | 301-400 | Murky waters |
| Tundra | 401-500 | Frozen lands |
| Volcano | 501-600 | Fiery depths |
| Ocean | 601-700 | Underwater |
| Sky | 701-800 | Floating islands |
| Underground | 801-900 | Dark caverns |
| Cursed Lands | 901-1000 | Dark magic |

Regions cycle after stage 1000 until reaching stage 10,000.

### Infinite Land (Endless Mode)
- Unlocked after defeating the Stage 10,000 boss
- Stages continue past 10,000
- No checkpoints
- No mini-bosses (only normal enemies and bosses every 100 stages)

## Configuration

### StageModeConfig (appsettings.json)
All Stage Mode settings are configurable:

```json
{
  "myTuno": {
    "stageMode": {
      "enemyCountStageInterval": 20,
      "maxEnemiesPerStage": 5,
      "baseStageXP": 30,
      "miniBossXPMultiplier": 3,
      "bossXPMultiplier": 10,
      "dropRates": {
        "beerDropChance": 0.10,
        "shotDropChance": 0.05,
        "bossDropMultiplier": 3.0,
        "miniBossDropMultiplier": 2.0
      },
      "defaultEnemySprite": "/sprites/games/my-tuno/default_enemy.png",
      "defaultBackground": "/sprites/games/my-tuno/backgrounds/forest.png",
      "playerSprite": "/sprites/games/my-tuno/default_tuno.png",
      "regions": [
        {
          "regionId": "Forest",
          "displayName": "Forest",
          "backgroundSprite": "/sprites/games/my-tuno/backgrounds/forest.png",
          "enemySprites": ["/sprites/games/my-tuno/enemies/wolf.png"],
          "bossSprite": "/sprites/games/my-tuno/enemies/boss_bear.png",
          "miniBossSprite": "/sprites/games/my-tuno/enemies/miniboss_wolf.png"
        }
      ]
    }
  }
}
```

## Database Entities

### StageProgress
Tracks player progression through Stage Mode:
- `CurrentStage`: Current stage number
- `HighestStage`: Highest stage ever reached
- `LastCheckpoint`: Last checkpoint stage
- `CurrentRegion`: Current biome/region
- `EndlessModeUnlocked`: Whether player beat Stage 10,000
- `TotalStagesCleared`: Total stages completed
- `TotalMiniBossesDefeated`: Mini-boss defeat count
- `TotalBossesDefeated`: Boss defeat count

### StageEnemy
Template for enemy types:
- `Name`: Enemy display name
- `Type`: Normal, MiniBoss, or Boss
- `Region`: Which region the enemy appears in
- `BaseHP`, `BasePower`, `BaseSpeed`: Base combat stats
- `BaseCriticalChance`: Critical hit chance
- `BaseFidelisDrop`: Base currency drop
- `BeerDropChance`, `ShotDropChance`: Item drop rates
- `SpritePath`: Path to enemy sprite image

### StageBattle
Records stage battle history:
- `CharacterId`: Player's character
- `StageNumber`: Stage where battle occurred
- `StageEnemyId`: Enemy template used (nullable)
- `EnemyType`, `Region`, `EnemyName`: Battle context
- `Seed`: RNG seed for replay
- `Outcome`: AttackerWon, DefenderWon, or Draw
- `XPReward`, `FidelisReward`: Rewards earned
- `BeersDropped`, `ShotsDropped`: Items dropped
- `ReplayJson`: Battle replay data

## Service Layer

### IStageService Interface
```csharp
public interface IStageService
{
    Task<StageProgress> GetOrCreateStageProgressAsync(string userId);
    Task<StageProgress?> GetStageProgressAsync(string userId);
    Task<StageEnemy?> GetCurrentStageEnemyAsync(StageProgress stageProgress);
    Task<StageBattle> ExecuteStageBattleAsync(int characterId);
    Task<List<StageBattle>> GetRecentBattlesAsync(int characterId, int count);
    Task<StageProgress> ReturnToCheckpointAsync(string userId);
}
```

## Stat Scaling

Enemy stats scale with stage number:
```
ScaleFactor = 1.0 + (StageNumber - 1) * 0.05
ScaledHP = BaseHP * ScaleFactor
ScaledPower = BasePower * ScaleFactor
ScaledSpeed = BaseSpeed * (1.0 + (StageNumber - 1) * 0.03)  // Slower scaling
ScaledFidelis = BaseFidelisDrop * (1.0 + (StageNumber - 1) * 0.02)
```

## XP Rewards

| Battle Outcome | Normal Enemy | Mini-Boss | Boss |
|---------------|--------------|-----------|------|
| Victory | 30 XP | 90 XP | 300 XP |
| Defeat | 10 XP | 10 XP | 10 XP |

## Migration

The Stage Mode entities are created by the migration:
`20260202004233_AddStageModeEntities`

This creates:
- `StageProgresses` table
- `StageEnemies` table
- `StageBattles` table
- Required indexes for performance

## Battle Scene (phaserStageBattle.js)

A new Phaser3 scene specifically for Stage Mode with a different layout from Arena:

### Layout
- **Player**: Bottom center of screen
- **Enemies**: Top area (supports multiple enemies in a row)
- **Background**: Forest background (region-specific backgrounds supported)
- **HP Bars**: Player HP at bottom, Enemy HP at top

### Sprites Used
| Sprite | Path |
|--------|------|
| Player | `/sprites/games/my-tuno/default_tuno.png` |
| Background | `/sprites/games/my-tuno/backgrounds/forest.png` |
| Wolf (Normal/Mini-Boss) | `/sprites/games/my-tuno/enemies/wolf.png` |
| Bear (Boss) | `/sprites/games/my-tuno/enemies/boss_bear.png` |

### Features
- Auto-scales enemy sprite based on type (Boss > Mini-Boss > Normal)
- Stage number display with enemy type badge
- Battle log with attack damage and critical hits
- Victory/Defeat animations with sound effects
- Speed controls (1x, 1.5x, 2x)
- Audio toggle

### JavaScript API
```javascript
// Start battle
stageBattleGame.start('containerId', {
    EventsJson: '...', // Battle replay JSON
    DotNetRef: blazorRef, // For callback
    StageNumber: 1,
    EnemyType: 'normal', // 'normal', 'miniBoss', 'boss'
    EnemyCount: 1,
    PlayerName: 'Player',
    EnemyName: 'Wolf',
    BackgroundPath: '/sprites/games/my-tuno/backgrounds/forest.png',
    PlayerSpritePath: '/sprites/games/my-tuno/default_tuno.png',
    EnemySpritePath: '/sprites/games/my-tuno/enemies/wolf.png'
});

// Destroy battle
stageBattleGame.destroy();

// Control speed
stageBattleGame.setSpeed(1.5);

// Toggle audio
stageBattleGame.setAudioEnabled(false);
```

## Future Enhancements

Planned future features:
1. ~~New Battle Layout - Player at bottom, enemies at top~~ ✅ Implemented
2. ~~Region-specific enemy sprites~~ ✅ Implemented (Forest with wolf/bear)
3. Multiple enemies per stage (scaling with level) - Infrastructure ready
4. Weekly stage challenges
5. Stage Mode leaderboard
6. Special boss encounters at milestone stages
7. Additional regions with unique backgrounds and enemies
