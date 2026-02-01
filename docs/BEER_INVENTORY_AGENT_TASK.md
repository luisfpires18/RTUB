# Agent Task: Implement Beer Inventory Item System

## Objective
Implement a beer inventory item system for the My Tuno game that allows players to collect beer as battle rewards and use it to restore HP.

## Requirements

### 1. Configuration
- Add `BeerDropChance` property to `MyTunoScaling` configuration
- Default drop chance should be configurable (suggested: 20% or 0.2)
- Add configuration to `appsettings.json` under `MyTuno` section

### 2. Database Schema
Create a new `InventoryItem` entity with the following properties:
- `Id` (int) - Primary key
- `UserId` (string) - Foreign key to ApplicationUser
- `Type` (InventoryItemType enum) - Type of item
- `Quantity` (int) - Number of items owned
- `CreatedAt` (DateTime) - When first obtained
- `UpdatedAt` (DateTime) - Last modified

Create `InventoryItemType` enum:
```csharp
public enum InventoryItemType
{
    Beer = 1
}
```

### 3. Repository Pattern
Implement `IInventoryRepository` interface with methods:
- `Task<InventoryItem?> GetItemAsync(string userId, InventoryItemType type)`
- `Task AddItemAsync(string userId, InventoryItemType type, int quantity)`
- `Task<bool> ConsumeItemAsync(string userId, InventoryItemType type, int quantity)`
- `Task<List<InventoryItem>> GetUserInventoryAsync(string userId)`

### 4. Battle Reward System
Update `BattleService.CreateBattleVsAIAsync` method:
- After battle completion and XP/Fidelis distribution
- Check if player won the battle
- Roll for beer drop using configured `BeerDropChance`
- If successful, add 1 beer to player's inventory
- Log the beer drop for debugging

Example logic:
```csharp
if (outcome == BattleOutcome.AttackerWins)
{
    var random = new Random();
    if (random.NextDouble() < MyTunoScaling.BeerDropChance)
    {
        await _inventoryRepository.AddItemAsync(playerCharacter.UserId, InventoryItemType.Beer, 1);
        _logger.LogInformation("Beer dropped for player {UserId}", playerCharacter.UserId);
    }
}
```

### 5. Beer Usage System
Create `InventoryService` with `UseBeerAsync` method:
- Check if player has at least 1 beer
- Check if player's CurrentHP < TotalHP (can't use if already at full HP)
- Calculate heal amount: 25% of TotalHP (rounded)
- Restore HP: `CurrentHP = Math.Min(TotalHP, CurrentHP + healAmount)`
- Decrease beer quantity by 1
- Save changes to database
- Return success/failure status

### 6. UI Updates (MyTunoHome.razor)
Update the Inventory section:
- Display beer icon (🍺 or `bi-cup-straw`)
- Show current beer quantity
- Add "Usar" (Use) button that:
  - Calls the UseBeerAsync method
  - Shows success message ("Recuperaste X HP!")
  - Updates the character's HP display
  - Disables when quantity is 0 or HP is full
  - Shows loading state during use

Example UI:
```html
<div class="p-2 border border-warning rounded d-flex justify-content-between align-items-center" 
     style="background-color: #1e1e1e;">
    <span class="d-flex align-items-center">
        <i class="bi bi-cup-straw text-warning me-2"></i>
        <span>Cerveja</span>
    </span>
    <div class="d-flex align-items-center gap-2">
        <strong class="text-warning">@beerQuantity</strong>
        <button class="btn btn-sm btn-warning" 
                @onclick="UseBeer"
                disabled="@(usingSeer || beerQuantity == 0 || character.CurrentHP >= character.TotalHP)">
            @if (usingBeer)
            {
                <span class="spinner-border spinner-border-sm"></span>
            }
            else
            {
                <text>Usar</text>
            }
        </button>
    </div>
</div>
```

### 7. Visual Feedback
When beer is used:
- Show a toast/alert message: "Recuperaste X HP!"
- Update HP bar immediately
- Decrease beer count visually
- Add a visual effect (optional: green glow on HP bar)

## Technical Considerations

### Database Migration
Create migration file: `AddInventoryItemsTable`
- Create InventoryItems table
- Add indexes on UserId and Type for performance
- Add unique constraint on (UserId, Type) to prevent duplicates

### Error Handling
- Handle case where user has no beer
- Handle case where HP is already full
- Handle database errors gracefully
- Show user-friendly error messages

### Testing
Consider adding tests for:
- Beer drop probability (verify ~20% drop rate over many battles)
- Beer usage (verify HP restoration works correctly)
- Edge cases (0 beer, full HP, exact 25% calculation)

### Performance
- Use indexed queries for inventory lookups
- Consider caching beer quantity in character object for faster display
- Batch inventory updates if multiple items added

## Success Criteria
- [ ] Beer drops after winning battles based on configured chance
- [ ] Beer appears in inventory with correct quantity
- [ ] Using beer restores exactly 25% HP (rounded)
- [ ] Beer quantity decreases after use
- [ ] Cannot use beer when HP is full
- [ ] Cannot use beer when quantity is 0
- [ ] UI updates immediately after use
- [ ] Configuration can be changed in appsettings.json
- [ ] All changes are properly tested
- [ ] Database migration applies cleanly

## Files to Create/Modify

### New Files:
1. `src/RTUB.Core/Entities/InventoryItem.cs`
2. `src/RTUB.Core/Enums/InventoryItemType.cs`
3. `src/RTUB.Application/Interfaces/IInventoryRepository.cs`
4. `src/RTUB.Application/Repositories/InventoryRepository.cs`
5. `src/RTUB.Application/Services/InventoryService.cs`
6. `src/RTUB.Web/Migrations/YYYYMMDDHHMMSS_AddInventoryItemsTable.cs`

### Modified Files:
1. `src/RTUB.Core/Configuration/MyTunoScaling.cs` - Add BeerDropChance
2. `src/RTUB.Application/Configuration/MyTunoScalingConfiguration.cs` - Configure from appsettings
3. `src/RTUB.Web/appsettings.json` - Add BeerDropChance value
4. `src/RTUB.Application/Services/BattleService.cs` - Add beer drop logic
5. `src/RTUB.Web/Pages/MyTuno/MyTunoHome.razor` - Add beer UI and usage
6. `src/RTUB.Web/Data/ApplicationDbContext.cs` - Add InventoryItems DbSet
7. `src/RTUB.Web/Program.cs` - Register InventoryService and Repository

## Estimated Effort
- Database setup: 30 minutes
- Repository implementation: 45 minutes
- Service logic: 45 minutes
- Battle integration: 30 minutes
- UI implementation: 1 hour
- Testing and polish: 1 hour
**Total: ~4-5 hours**

## Priority
Medium-High - Adds gameplay depth and strategy, improves user engagement

## Dependencies
- Requires CurrentHP system to be working (already implemented)
- Requires battle system to be functional (already implemented)
- Requires MyTunoScaling configuration system (already implemented)
