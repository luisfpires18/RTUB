# Stage Mode Phase 1 - Quick Reference

## Entities

### Stage
Location: `src/RTUB.Core/Entities/Stage.cs`

```csharp
// Create a new stage
var stage = Stage.Create(
    stageNumber: 1,
    name: "Guitar Challenge",
    requiredLevel: 5,
    enemyConfigKey: "stage_1",
    rewardInstrument: InventoryItemType.Guitarra,
    fidelisReward: 100m,
    beerDropChance: 0.2,
    shotDropChance: 0.1
);
```

### CharacterStageProgress
Location: `src/RTUB.Core/Entities/CharacterStageProgress.cs`

```csharp
// Create progress tracking
var progress = CharacterStageProgress.Create(characterId: 1, stageId: 1);

// Mark completion
progress.MarkCompleted();

// Claim reward
progress.ClaimInstrument();
```

### Character (Extended)
Location: `src/RTUB.Core/Entities/Character.cs`

```csharp
// Equip an instrument
character.EquipInstrument(InventoryItemType.Guitarra);

// Unequip
character.EquipInstrument(null);

// Check equipped
var instrument = character.EquippedInstrument; // InventoryItemType?
```

## Enums

### InventoryItemType
Location: `src/RTUB.Core/Enums/InventoryItemType.cs`

```csharp
Beer = 1
Shot = 2

// Instruments (10-21)
Guitarra = 10
Bandolim = 11
Cavaquinho = 12
Viola = 13
Violino = 14
Flauta = 15
Clarinete = 16
Saxofone = 17
Trompete = 18
Trombone = 19
Bateria = 20
Acordeao = 21
```

## Database

### Querying Stages
```csharp
// Get all active stages
var stages = await context.Stages
    .Where(s => s.IsActive)
    .OrderBy(s => s.StageNumber)
    .ToListAsync();

// Get stage by number
var stage = await context.Stages
    .FirstOrDefaultAsync(s => s.StageNumber == 1);
```

### Querying Progress
```csharp
// Get character's progress for a stage
var progress = await context.CharacterStageProgress
    .Include(csp => csp.Stage)
    .FirstOrDefaultAsync(csp => 
        csp.CharacterId == characterId && 
        csp.StageId == stageId);

// Get all progress for a character
var allProgress = await context.CharacterStageProgress
    .Include(csp => csp.Stage)
    .Where(csp => csp.CharacterId == characterId)
    .ToListAsync();
```

## Migration

### Apply Migration
```bash
cd /path/to/RTUB
dotnet ef database update --project src/RTUB.Web --startup-project src/RTUB.Web
```

### Rollback Migration
```bash
dotnet ef database update 20260201190250_AddInventoryItemsTable --project src/RTUB.Web
```

## Validation Rules

### Stage
- StageNumber > 0
- Name: Required, max 100 chars
- Description: Optional, max 500 chars
- RequiredLevel >= 0
- EnemyConfigKey: Required, max 50 chars
- FidelisReward >= 0
- BeerDropChance: 0.0 - 1.0
- ShotDropChance: 0.0 - 1.0

### CharacterStageProgress
- CharacterId > 0
- StageId > 0
- Unique constraint: (CharacterId, StageId)

## Indexes

- `IX_Stages_StageNumber` (UNIQUE)
- `IX_CharacterStageProgress_CharacterId`
- `IX_CharacterStageProgress_CharacterId_StageId` (UNIQUE)
- `IX_CharacterStageProgress_StageId`

## Foreign Keys

- `CharacterStageProgress.CharacterId` → `Characters.Id` (CASCADE)
- `CharacterStageProgress.StageId` → `Stages.Id` (CASCADE)
