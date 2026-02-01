# Phase 4 Service Layer - Quick Reference

## 🎯 What Was Built

### Core Services
1. **StageService** - Orchestrates stage battles, progression, and rewards
2. **InventoryService.UseShotAsync()** - Full HP restoration (100%)
3. **CharacterService Equipment** - Equip/unequip instruments with stat bonuses

---

## 📚 API Reference

### IStageService

```csharp
// Get stages available for character's level
Task<List<Stage>> GetAvailableStagesAsync(int characterId, CancellationToken ct = default)

// Get character's completion progress on all stages
Task<List<CharacterStageProgress>> GetCharacterProgressAsync(int characterId, CancellationToken ct = default)

// Start a battle against a stage enemy
Task<Battle> StartStageBattleAsync(int characterId, int stageNumber, CancellationToken ct = default)

// Complete battle and distribute rewards (Fidelis, instruments, drops)
Task CompleteStageBattleAsync(int battleId, CancellationToken ct = default)

// Get instrument stat bonuses from configuration
InstrumentStatBonus? GetInstrumentStats(InventoryItemType instrumentType)
```

### IInventoryService (Extended)

```csharp
// Use Shot to restore 100% HP
Task<(bool Success, int HealedAmount, string Message)> UseShotAsync(string userId, CancellationToken ct = default)
```

### ICharacterService (Extended)

```csharp
// Equip an instrument (validates ownership)
Task<bool> EquipInstrumentAsync(int characterId, InventoryItemType instrumentType, CancellationToken ct = default)

// Remove equipped instrument
Task UnequipInstrumentAsync(int characterId, CancellationToken ct = default)

// Get stats including equipment bonuses
Task<CharacterStats> GetCharacterStatsWithEquipmentAsync(int characterId, CancellationToken ct = default)
```

---

## 🎮 Usage Examples

### Starting a Stage Battle

```csharp
// Inject IStageService
public class StageController
{
    private readonly IStageService _stageService;
    
    public StageController(IStageService stageService)
    {
        _stageService = stageService;
    }
    
    public async Task<IActionResult> StartBattle(int characterId, int stageNumber)
    {
        try
        {
            // Start the battle (simulates combat)
            var battle = await _stageService.StartStageBattleAsync(characterId, stageNumber);
            
            // Complete battle if won
            if (battle.Outcome == BattleOutcome.AttackerWon)
            {
                await _stageService.CompleteStageBattleAsync(battle.Id);
            }
            
            return Ok(battle);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message); // Portuguese error messages
        }
    }
}
```

### Using a Shot

```csharp
var (success, healAmount, message) = await _inventoryService.UseShotAsync(userId);
if (success)
{
    // "HP restaurado completamente! (+150 HP)"
    Console.WriteLine(message);
}
```

### Equipping an Instrument

```csharp
// Equip guitar
var equipped = await _characterService.EquipInstrumentAsync(
    characterId, 
    InventoryItemType.Guitar
);

if (equipped)
{
    // Get stats with bonuses
    var stats = await _characterService.GetCharacterStatsWithEquipmentAsync(characterId);
    Console.WriteLine($"Total HP: {stats.HP}, Power: {stats.Power}");
}
```

---

## 🏗️ Architecture Flow

### Stage Battle Flow
```
User Request
    ↓
[Controller/API]
    ↓
StageService.StartStageBattleAsync()
    ↓
├─ Validate level requirement
├─ Load stage config
├─ Create AI opponent
├─ Simulate combat (ICombatEngine)
├─ Save battle record
└─ Update character HP
    ↓
StageService.CompleteStageBattleAsync()
    ↓
├─ Verify victory
├─ Award Fidelis (UserManager)
├─ Grant instrument (first time)
├─ Roll beer drop (random)
└─ Roll shot drop (random)
    ↓
[Response to User]
```

---

## 🎁 Reward System

### Fidelis (Always on Victory)
- Amount from `Stage.FidelisReward`
- Awarded via `UserManager<ApplicationUser>`

### Instrument (One-Time)
- Granted on **first completion only**
- Type from `Stage.RewardInstrument`
- Tracked via `CharacterStageProgress.InstrumentClaimed`

### Beer Drop (Random)
- Probability: `Stage.BeerDropChance` (e.g., 0.2 = 20%)
- Heals 25% HP when used

### Shot Drop (Random)
- Probability: `Stage.ShotDropChance` (e.g., 0.1 = 10%)
- Heals 100% HP when used

---

## ⚙️ Configuration

### Stage Definition (scaling.config.json)
```json
{
  "myTuno": {
    "stages": {
      "stageList": [
        {
          "stageNumber": 1,
          "name": "Guitar Challenge",
          "requiredLevel": 1,
          "fidelisReward": 50,
          "rewardInstrument": "Guitar",
          "enemyStats": {
            "hp": 120,
            "power": 12,
            "speed": 10,
            "criticalChance": 0.05
          }
        }
      ],
      "instrumentStats": {
        "Guitar": {
          "hpBonus": 20,
          "powerBonus": 5,
          "speedBonus": 3,
          "criticalChanceBonus": 0.02
        }
      }
    }
  }
}
```

---

## 🔐 Security & Validation

### Automatic Checks
- ✅ Character level vs. stage requirement
- ✅ Character alive status before battle
- ✅ Item ownership before equipping
- ✅ Battle victory before rewards
- ✅ User existence for Fidelis

### Error Messages (Portuguese)
```csharp
"Nível 5 é muito baixo para stage 3 (requer nível 10)"
"Personagem derrotado. Precisa de reviver antes de lutar."
"Não tens Shot disponível"
"HP já está cheio"
```

---

## 🧪 Testing

### Mock Setup
```csharp
var mockStageRepo = new Mock<IStageRepository>();
var mockCharacterRepo = new Mock<ICharacterRepository>();
var mockInventoryRepo = new Mock<IInventoryRepository>();

var service = new StageService(
    mockStageRepo.Object,
    mockProgressRepo.Object,
    mockCharacterRepo.Object,
    mockBattleRepo.Object,
    mockInventoryRepo.Object,
    mockUserManager.Object,
    mockCombatEngine.Object,
    Options.Create(scalingConfig),
    Mock.Of<ILogger<StageService>>()
);
```

---

## 📦 Dependencies

### StageService Requires
- IStageRepository
- ICharacterStageProgressRepository
- ICharacterRepository
- IBattleRepository
- IInventoryRepository
- UserManager<ApplicationUser>
- ICombatEngine
- IOptions<MyTunoScalingConfiguration>
- ILogger<StageService>

### Registered in DI Container
```csharp
services.AddScoped<IStageService, StageService>();
```

---

## 🚦 Next Phase Checklist

For Phase 5 (Web Layer), you'll need:

- [ ] API endpoints for stage operations
- [ ] DTOs for request/response models
- [ ] Blazor components for UI
- [ ] Stage selection page
- [ ] Battle animation/visualization
- [ ] Reward display
- [ ] Integration tests

---

## 📚 Related Documentation

- `PHASE4_SERVICE_LAYER_SUMMARY.md` - Detailed implementation guide
- `PHASE4_COMPLETE.md` - Complete metrics and acceptance criteria
- `README_STAGE_MODE.md` - Overall feature documentation
- `PHASE3_IMPLEMENTATION_SUMMARY.md` - Repository layer (Phase 3)

---

**Quick Start:** Inject `IStageService` in your controller and call `StartStageBattleAsync()` → `CompleteStageBattleAsync()` 🎮
