# Random Enemy Sprite Selection - Technical Flow

## High-Level Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                        USER INITIATES BATTLE                        │
└────────────────────────────────┬────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    StageService.ExecuteStageBattleAsync()            │
│                                                                      │
│  1. Determine enemy type for current stage (Normal/MiniBoss/Boss)   │
│  2. Determine current region (Forest/Desert/Mountain/etc.)          │
└────────────────────────────────┬────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│           StageEnemyRepository.GetRandomEnemyAsync()                │
│                                                                      │
│  Query: SELECT * FROM StageEnemies                                  │
│         WHERE Type = @enemyType AND Region = @region               │
│         ORDER BY RANDOM()                                           │
│         LIMIT 1                                                     │
│                                                                      │
│  Returns: StageEnemy entity with properties:                        │
│    • Id, Name (e.g., "Forest Wolf")                                │
│    • SpritePath (e.g., "/sprites/.../wolf_variant_2.png")          │
│    • BaseHP, BasePower, BaseSpeed, BaseCriticalChance              │
│    • BaseFidelisDrop, BeerDropChance, ShotDropChance               │
└────────────────────────────────┬────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    Create Temporary Enemy Character                 │
│                                                                      │
│  Character.CreateStageEnemy(                                        │
│    hp: enemyTemplate.GetScaledHP(stageNumber),                     │
│    power: enemyTemplate.GetScaledPower(stageNumber),               │
│    speed: enemyTemplate.GetScaledSpeed(stageNumber),               │
│    criticalChance: enemyTemplate.BaseCriticalChance,               │
│    enemyName: enemyTemplate.Name                                    │
│  )                                                                  │
└────────────────────────────────┬────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                        Run Combat Simulation                         │
│                                                                      │
│  CombatEngine.Simulate(playerCharacter, enemyCharacter, seed)       │
│  Returns: CombatResult with events and outcome                      │
└────────────────────────────────┬────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      Create StageBattle Record                       │
│                                                                      │
│  StageBattle.Create(                                                │
│    characterId,                                                     │
│    stageNumber,                                                     │
│    stageEnemyId: enemyTemplate.Id,  ← Links to random enemy        │
│    enemyType,                                                       │
│    region,                                                          │
│    enemyName: enemyTemplate.Name,                                   │
│    seed,                                                            │
│    outcome                                                          │
│  )                                                                  │
│                                                                      │
│  ⚡ NEW: Load navigation property                                   │
│  stageBattle.StageEnemy = enemyTemplate  ← Makes SpritePath available│
└────────────────────────────────┬────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    Stage.razor Receives Battle                       │
│                                                                      │
│  var enemySpritePath = currentBattle.StageEnemy?.SpritePath         │
│      ?? GetDefaultEnemySpriteForType(currentBattle.EnemyType);      │
│                                                                      │
│  Examples:                                                          │
│    • If DB has "/sprites/.../wolf_variant_2.png" → use that        │
│    • If DB SpritePath is null → use "/sprites/.../wolf.png"        │
└────────────────────────────────┬────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                  Pass Data to Phaser Game Engine                     │
│                                                                      │
│  var battleData = new {                                             │
│    EventsJson = currentBattle.ReplayJson,                           │
│    EnemySpritePath = enemySpritePath,  ← Random sprite from DB      │
│    EnemyName = currentBattle.EnemyName,                             │
│    EnemyType = "normal" | "miniBoss" | "boss",                      │
│    ...                                                              │
│  };                                                                 │
│                                                                      │
│  JSRuntime.InvokeVoidAsync("stageBattleGame.start", battleData);    │
└────────────────────────────────┬────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│              Phaser Game Renders Battle with Sprite                  │
│                                                                      │
│  load.image('enemy', battleData.EnemySpritePath);                   │
│  this.add.sprite(x, y, 'enemy');                                    │
│                                                                      │
│  🎮 Player sees the random enemy sprite in battle animation!        │
└─────────────────────────────────────────────────────────────────────┘
```

## Database Example

### StageEnemy Table
```sql
+----+------------------+----------+--------+--------+------------------------------------------+
| Id | Name             | Type     | Region | BaseHP | SpritePath                               |
+----+------------------+----------+--------+--------+------------------------------------------+
| 1  | Forest Wolf      | Normal   | Forest | 50     | /sprites/.../wolf.png                    |
| 2  | Gray Wolf        | Normal   | Forest | 50     | /sprites/.../wolf_gray.png               |
| 3  | Black Wolf       | Normal   | Forest | 52     | /sprites/.../wolf_black.png              |
| 4  | Alpha Wolf       | MiniBoss | Forest | 150    | /sprites/.../wolf_alpha.png              |
| 5  | Forest Bear      | Boss     | Forest | 500    | /sprites/.../boss_bear.png               |
| 6  | Ancient Bear     | Boss     | Forest | 520    | /sprites/.../boss_bear_ancient.png       |
+----+------------------+----------+--------+--------+------------------------------------------+
```

### Scenario: Player at Stage 1 (Normal Enemy, Forest Region)

1. **Query executes:**
   ```sql
   SELECT * FROM StageEnemies 
   WHERE Type = 'Normal' AND Region = 'Forest'
   ORDER BY RANDOM() LIMIT 1
   ```

2. **Possible Results:**
   - Battle 1: Gets "Forest Wolf" → Shows `/sprites/.../wolf.png`
   - Battle 2: Gets "Gray Wolf" → Shows `/sprites/.../wolf_gray.png`
   - Battle 3: Gets "Black Wolf" → Shows `/sprites/.../wolf_black.png`
   - Battle 4: Gets "Forest Wolf" → Shows `/sprites/.../wolf.png` again
   - etc...

3. **Visual Result:** 🎮
   Each battle has a random chance of showing different wolf sprites!

## Code Changes Summary

### Before (Hardcoded)
```csharp
// Stage.razor - OLD CODE
var (enemyTypeName, enemySpritePath) = GetEnemyTypeConfig(stageNumber);

private static (string TypeName, string SpritePath) GetEnemyTypeConfig(int stageNumber)
{
    var enemyType = StageProgress.GetEnemyTypeForStage(stageNumber);
    return enemyType switch
    {
        EnemyType.Boss => ("boss", "/sprites/.../boss_bear.png"),     // ❌ Always same
        EnemyType.MiniBoss => ("miniBoss", "/sprites/.../wolf.png"),  // ❌ Always same
        _ => ("normal", "/sprites/.../wolf.png")                      // ❌ Always same
    };
}
```

### After (Database-Driven)
```csharp
// StageService.cs - NEW CODE
if (enemyTemplate != null)
{
    stageBattle.StageEnemy = enemyTemplate;  // ✅ Load navigation property
}

// Stage.razor - NEW CODE
var enemyTypeName = GetEnemyTypeName(currentBattle.EnemyType);
var enemySpritePath = currentBattle.StageEnemy?.SpritePath   // ✅ From database!
    ?? GetDefaultEnemySpriteForType(currentBattle.EnemyType); // Fallback
```

## Benefits

✅ **Data-Driven**: Sprites controlled by database, not code
✅ **Random Variety**: Each battle can show a different sprite
✅ **Type-Safe**: Still respects enemy types (Normal/MiniBoss/Boss)
✅ **Region-Aware**: Different enemies per region (Forest/Desert/Mountain)
✅ **Backward Compatible**: Falls back if database has no sprite path
✅ **Extensible**: Add new sprites via database inserts, no code changes

---
**Key Insight**: The randomization happens in `GetRandomEnemyAsync()`, not in the UI layer. The UI simply displays what the service layer decided!
