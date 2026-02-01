# Repository Usage Guide - Stage Mode

## Quick Reference

This guide shows how to use the new Stage and CharacterStageProgress repositories in your services.

## Dependency Injection

```csharp
public class StageService
{
    private readonly IStageRepository _stageRepository;
    private readonly ICharacterStageProgressRepository _progressRepository;
    
    public StageService(
        IStageRepository stageRepository,
        ICharacterStageProgressRepository progressRepository)
    {
        _stageRepository = stageRepository;
        _progressRepository = progressRepository;
    }
}
```

## IStageRepository Methods

### 1. Get All Active Stages

```csharp
// Get all active stages ordered by stage number
var stages = await _stageRepository.GetAllActiveStagesAsync(cancellationToken);

// Result: List<Stage> ordered by StageNumber
// Use case: Display all available stages in UI
```

### 2. Get Stage by Number

```csharp
// Get a specific stage by its stage number (1-12)
var stage = await _stageRepository.GetByStageNumberAsync(3, cancellationToken);

// Result: Stage? (null if not found or not active)
// Use case: Load specific stage for battle initiation
```

### 3. Get Stages for Character Level

```csharp
// Get all stages available for a character's level
var availableStages = await _stageRepository.GetStagesForLevelAsync(
    characterLevel: 5, 
    cancellationToken);

// Result: List<Stage> where RequiredLevel <= 5
// Use case: Filter stages by character's level
```

## ICharacterStageProgressRepository Methods

### 1. Get Character Progress

```csharp
// Get all progress records for a character
var progressRecords = await _progressRepository.GetCharacterProgressAsync(
    characterId: 123, 
    cancellationToken);

// Result: List<CharacterStageProgress> with Stage navigation property loaded
// Ordered by Stage.StageNumber
// Use case: Display character's progress across all stages
```

### 2. Get Specific Progress

```csharp
// Get progress for a specific character and stage
var progress = await _progressRepository.GetProgressAsync(
    characterId: 123,
    stageId: 456,
    cancellationToken);

// Result: CharacterStageProgress? with Stage navigation property
// Use case: Check completion count, last completion date, etc.
```

### 3. Check Stage Completion

```csharp
// Check if a character has completed a stage
var hasCompleted = await _progressRepository.HasCompletedStageAsync(
    characterId: 123,
    stageId: 456,
    cancellationToken);

// Result: bool (true if CompletionCount > 0)
// Use case: Determine if first-time rewards should be given
```

### 4. Get or Create Progress

```csharp
// Get or create a progress record (idempotent operation)
var progress = await _progressRepository.GetOrCreateProgressAsync(
    characterId: 123,
    stageId: 456,
    cancellationToken);

// Result: CharacterStageProgress (always returns a record)
// Creates new record if doesn't exist, returns existing otherwise
// Use case: Ensure progress tracking exists before battle
```

## Complete Example: Stage Service

```csharp
public class StageService : IStageService
{
    private readonly IStageRepository _stageRepository;
    private readonly ICharacterStageProgressRepository _progressRepository;
    private readonly ICharacterRepository _characterRepository;

    public StageService(
        IStageRepository stageRepository,
        ICharacterStageProgressRepository progressRepository,
        ICharacterRepository characterRepository)
    {
        _stageRepository = stageRepository;
        _progressRepository = progressRepository;
        _characterRepository = characterRepository;
    }

    public async Task<StageListDto> GetAvailableStagesAsync(
        int characterId, 
        CancellationToken cancellationToken = default)
    {
        // Get character to check level
        var character = await _characterRepository.GetByIdAsync(characterId);
        if (character == null)
            throw new NotFoundException("Character not found");

        // Get stages available for character's level
        var stages = await _stageRepository.GetStagesForLevelAsync(
            character.Level, 
            cancellationToken);

        // Get character's progress for all stages
        var progressRecords = await _progressRepository.GetCharacterProgressAsync(
            characterId, 
            cancellationToken);

        // Map to DTOs with completion status
        var stageDtos = stages.Select(stage =>
        {
            var progress = progressRecords.FirstOrDefault(p => p.StageId == stage.Id);
            return new StageDto
            {
                Id = stage.Id,
                StageNumber = stage.StageNumber,
                Name = stage.Name,
                Description = stage.Description,
                RequiredLevel = stage.RequiredLevel,
                FidelisReward = stage.FidelisReward,
                CompletionCount = progress?.CompletionCount ?? 0,
                IsCompleted = progress?.CompletionCount > 0,
                InstrumentClaimed = progress?.InstrumentClaimed ?? false
            };
        }).ToList();

        return new StageListDto { Stages = stageDtos };
    }

    public async Task<BattleResultDto> CompleteStageAsync(
        int characterId,
        int stageId,
        bool victory,
        CancellationToken cancellationToken = default)
    {
        // Get or create progress record
        var progress = await _progressRepository.GetOrCreateProgressAsync(
            characterId, 
            stageId, 
            cancellationToken);

        if (!victory)
        {
            return new BattleResultDto { Victory = false };
        }

        // Check if this is first completion
        var isFirstCompletion = progress.CompletionCount == 0;

        // Mark as completed (updates CompletionCount and timestamps)
        progress.MarkCompleted();
        await _progressRepository.UpdateAsync(progress);

        // Get stage for rewards
        var stage = await _stageRepository.GetByIdAsync(stageId);
        if (stage == null)
            throw new NotFoundException("Stage not found");

        return new BattleResultDto
        {
            Victory = true,
            FidelisReward = stage.FidelisReward,
            IsFirstCompletion = isFirstCompletion,
            InstrumentReward = isFirstCompletion ? stage.RewardInstrument : null
        };
    }
}
```

## Best Practices

### ✅ DO:
- Always pass `CancellationToken` to repository methods
- Use `GetOrCreateProgressAsync` when you need to ensure a progress record exists
- Use `HasCompletedStageAsync` for simple existence checks (more efficient than GetProgressAsync)
- Handle null returns from `GetProgressAsync` and `GetByStageNumberAsync`

### ❌ DON'T:
- Don't use `GetProgressAsync` just to check if a stage is completed (use `HasCompletedStageAsync` instead)
- Don't manually create CharacterStageProgress entities (use factory method or GetOrCreateProgressAsync)
- Don't forget to handle the case where a stage might not exist or be inactive
- Don't call SaveChangesAsync() - repositories handle this automatically

## Performance Notes

- All read-only queries use `AsNoTracking()` for better performance
- `HasCompletedStageAsync` uses `Any()` query which is more efficient than loading the entity
- Navigation properties are eagerly loaded only when needed to avoid N+1 queries
- Results are ordered at the database level for efficiency

## Testing

All repositories can be easily mocked using interfaces:

```csharp
var mockStageRepo = new Mock<IStageRepository>();
mockStageRepo
    .Setup(r => r.GetAllActiveStagesAsync(It.IsAny<CancellationToken>()))
    .ReturnsAsync(new List<Stage> { /* test stages */ });

var service = new StageService(mockStageRepo.Object, ...);
```
