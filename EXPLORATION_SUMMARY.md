# MyTuno Game - Beer Inventory System Implementation Summary

## Task Overview
Implement a beer inventory item system for the My Tuno game that allows players to collect beer as battle rewards and use it to restore HP (25% of TotalHP).

**Location of Task:** `/home/runner/work/RTUB/RTUB/docs/BEER_INVENTORY_AGENT_TASK.md`

---

## Current Project Structure

### 1. Core Architecture (Clean Architecture Pattern)

```
src/RTUB.Core/          # Domain layer (Pure C#, no external dependencies)
  ├── Configuration/
  │   └── MyTunoScaling.cs
  ├── Entities/
  │   ├── Character.cs
  │   ├── Battle.cs
  │   └── Product.cs (Existing shop system)
  └── Enums/
      └── BattleOutcome.cs

src/RTUB.Application/   # Business Logic layer
  ├── Data/
  │   ├── ApplicationDbContext.cs
  │   └── Configurations/
  ├── Interfaces/
  │   ├── IRepository.cs
  │   ├── ICharacterRepository.cs
  │   └── IBattleRepository.cs
  ├── Repositories/
  │   ├── Repository.cs
  │   ├── CharacterRepository.cs
  │   └── BattleRepository.cs
  └── Services/
      ├── BattleService.cs
      └── CharacterService.cs

src/RTUB.Web/           # Presentation layer
  ├── Pages/MyTuno/
  │   ├── MyTunoHome.razor
  │   └── Arena.razor
  ├── appsettings.json
  └── Program.cs
```

---

## 2. Existing MyTuno Game Components

### **A. MyTunoScaling Configuration** 
**Path:** `src/RTUB.Core/Configuration/MyTunoScaling.cs`

**Current Properties:**
```csharp
public static class MyTunoScaling
{
    public static int BaseLevel { get; private set; } = 1;
    public static int BaseXp { get; private set; } = 0;
    public static int BaseHp { get; private set; } = 100;
    public static int BasePower { get; private set; } = 10;
    public static int BaseSpeed { get; private set; } = 10;
    public static double BaseCriticalChance { get; private set; } = 0.01;
    
    public static double StatMultiplierPerLevel { get; private set; } = 0.1;
    public static int XpPerLevelBase { get; private set; } = 100;
    
    // Upgrade tracking
    public static int InitialHpUpgrades { get; private set; } = 0;
    public static int InitialPowerUpgrades { get; private set; } = 0;
    public static int InitialSpeedUpgrades { get; private set; } = 0;
    public static int InitialCriticalUpgrades { get; private set; } = 0;
    
    // Upgrade bonuses
    public static double HpUpgradeBonus { get; private set; } = 10;
    public static double PowerUpgradeBonus { get; private set; } = 2;
    public static double SpeedUpgradeBonus { get; private set; } = 1;
    public static double CriticalChanceUpgradeBonus { get; private set; } = 0.005;
    
    public static void Configure(...) { /* Configuration method */ }
}
```

**✅ What Needs to Be Added:**
- `public static double BeerDropChance { get; private set; } = 0.2;` (20% default)
- Update `Configure()` method signature to include `beerDropChance` parameter

---

### **B. Character Entity**
**Path:** `src/RTUB.Core/Entities/Character.cs`

**Key Properties:**
```csharp
public class Character : BaseEntity
{
    public string UserId { get; set; }
    
    // Progression
    public int Level { get; set; }
    public int XP { get; set; }
    
    // Base Stats
    public int HP { get; set; }
    public int Power { get; set; }
    public int Speed { get; set; }
    public double CriticalChance { get; set; }
    
    // Current HP (null = full HP)
    public int? CurrentHP { get; set; } = null;
    
    // Upgrade counts
    public int HpUpgrades { get; set; }
    public int PowerUpgrades { get; set; }
    public int SpeedUpgrades { get; set; }
    public int CriticalUpgrades { get; set; }
    
    // Computed Properties
    public int TotalHP => (int)(HP * (1 + (Level - 1) * 0.1)) + (int)(HpUpgrades * 10);
    public int TotalPower => ...;
    public int TotalSpeed => ...;
    public double TotalCriticalChance => ...;
    
    // Key Methods
    public void AddXP(int amount) { /* Handles level-ups */ }
    public void TakeDamage(int damage) { /* Updates CurrentHP */ }
    public void Heal(int amount) { /* Restores HP */ }
    public void RestoreHP() { /* Sets CurrentHP to TotalHP */ }
    public bool IsAlive() { /* Returns CurrentHP > 0 */ }
}
```

**✅ Important:** Character already has HP restoration methods (`Heal()`, `RestoreHP()`) that can be used by InventoryService.

---

### **C. Battle Entity**
**Path:** `src/RTUB.Core/Entities/Battle.cs`

**Key Properties:**
```csharp
public class Battle : BaseEntity
{
    public int AttackerCharacterId { get; set; }
    public int DefenderCharacterId { get; set; }
    public int Seed { get; set; }
    public BattleOutcome Outcome { get; set; }  // AttackerWon, DefenderWon, Draw
    
    // Rewards
    public int AttackerXP { get; set; }
    public decimal AttackerFidelis { get; set; }
    public int DefenderXP { get; set; }  // Always 0 for AI
    public decimal DefenderFidelis { get; set; }  // Always 0
    
    public string ReplayJson { get; set; }
    
    public virtual Character Attacker { get; set; }
    public virtual Character Defender { get; set; }
}
```

**Enum:** `BattleOutcome` has values: `AttackerWon = 0`, `DefenderWon = 1`, `Draw = 2`

---

### **D. BattleService**
**Path:** `src/RTUB.Application/Services/BattleService.cs`

**Key Method to Modify:** `CreateBattleVsAIAsync(int playerCharacterId)`

**Current Flow:**
```csharp
public async Task<Battle> CreateBattleVsAIAsync(int playerCharacterId)
{
    // 1. Load player character
    var playerCharacter = await _characterRepository.GetByIdAsync(playerCharacterId);
    
    // 2. Find AI opponent
    var aiOpponent = await _matchmakingService.FindAIOpponentAsync(playerCharacter);
    
    // 3. Generate seed & run combat simulation
    var combatResult = _combatEngine.Simulate(playerCharacter, aiOpponent, seed);
    
    // 4. Calculate rewards based on outcome
    var (xpReward, fidelisReward) = CalculateRewards(combatResult.Outcome);
    
    // 5. Create battle record
    var battle = Battle.Create(...);
    battle.SetRewards(xpReward, fidelisReward);
    battle.SetReplay(replayJson);
    
    // 6. Save battle
    await _battleRepository.AddAsync(battle);
    
    // 7. Apply rewards
    await ApplyRewardsAsync(playerCharacter, xpReward, fidelisReward);
    
    return battle;
}
```

**Reward Constants:**
- Win: `BaseWinXP = 50`, `BaseWinFidelis = 10m`
- Loss: `BaseLossXP = 20`, `BaseLossFidelis = 5m`
- Draw: `BaseDrawXP = 30`, `BaseDrawFidelis = 7.5m`

**✅ Where to Add Beer Drop Logic:**
After line 96 (after applying rewards), add:
```csharp
// Add beer drop logic
if (combatResult.Outcome == BattleOutcome.AttackerWon)
{
    var random = new Random();
    if (random.NextDouble() < MyTunoScaling.BeerDropChance)
    {
        await _inventoryRepository.AddItemAsync(playerCharacter.UserId, InventoryItemType.Beer, 1);
        _logger.LogInformation("Beer dropped for player {UserId}", playerCharacter.UserId);
    }
}
```

---

### **E. Repository Pattern Implementation**

**Base Repository:** `src/RTUB.Application/Repositories/Repository.cs`

**Features:**
- Generic CRUD operations: `GetByIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`
- Queries: `FindAsync`, `FirstOrDefaultAsync`, `CountAsync`, `AnyAsync`
- Advanced: `Query()` returns `IQueryable` for custom queries (with no-tracking)
- **Smart duplicate tracking:** Automatically detaches duplicate entities before update
- **Auto-save:** Calls `SaveChangesAsync()` after mutations

**Example Specialized Repository:** `CharacterRepository`
```csharp
public class CharacterRepository : Repository<Character>, ICharacterRepository
{
    public async Task<Character?> GetByUserIdAsync(string userId)
    {
        return await _context.Characters
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == userId);
    }
    // ... other specialized methods
}
```

**✅ Pattern to Follow:** Create `InventoryRepository : Repository<InventoryItem>, IInventoryRepository`

---

### **F. MyTunoHome.razor**
**Path:** `src/RTUB.Web/Pages/MyTuno/MyTunoHome.razor`

**Current Structure (970 lines):**

**1. Character Dashboard (3-column layout):**
```razor
<div class="row mb-4">
    <!-- Column 1: Inventory (Fidelis) -->
    <div class="col-md-4">
        <div class="card bg-dark text-light">
            <h3>Inventário</h3>
            <div class="p-2 border border-success rounded">
                <i class="bi bi-coin text-success"></i>
                <span>Fidelis</span>
                <strong>@user?.FidelisBalance.ToString("F0")</strong>
            </div>
            <!-- ✅ Beer should be added here -->
        </div>
    </div>
    
    <!-- Column 2: Character Stats -->
    <div class="col-md-4">
        <div class="card bg-dark text-light">
            <h3>Personagem</h3>
            <p>HP: @character.CurrentHP / @character.TotalHP</p>
            <p>Power: @character.TotalPower</p>
            <p>Speed: @character.TotalSpeed</p>
            <p>Critical: @(character.TotalCriticalChance * 100)%</p>
        </div>
    </div>
    
    <!-- Column 3: Leaderboard -->
    <div class="col-md-4">
        <!-- Leaderboard table -->
    </div>
</div>

<!-- Upgrade Shop Section -->
<div class="row mb-4">
    <!-- 4 upgrade cards for HP, Power, Speed, Critical -->
</div>
```

**2. Key Dependencies:**
```csharp
@inject ICharacterService CharacterService
@inject IUpgradeService UpgradeService
@inject IBattleRepository BattleRepository
@inject UserManager<ApplicationUser> UserManager
@inject AuthenticationStateProvider AuthenticationStateProvider
@inject IJSRuntime JSRuntime
```

**✅ Need to Add:**
- `@inject IInventoryService InventoryService`
- Beer inventory display in Column 1 (Inventário)
- "Usar" button with click handler
- Success message toast/alert

**3. Existing Code Patterns:**

**Loading State Pattern:**
```csharp
private bool isLoading = true;
private bool isProcessing = false;

protected override async Task OnInitializedAsync()
{
    await LoadDataAsync();
    isLoading = false;
}
```

**Error Handling Pattern:**
```csharp
try
{
    // Operation
    await JSRuntime.InvokeVoidAsync("alert", "Success message!");
}
catch (Exception ex)
{
    await JSRuntime.InvokeVoidAsync("alert", $"Error: {ex.Message}");
    _logger.LogError(ex, "Operation failed");
}
```

---

### **G. ApplicationDbContext**
**Path:** `src/RTUB.Application/Data/ApplicationDbContext.cs`

**Current DbSets (60+ entities):**
```csharp
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    // Game entities
    public DbSet<Character> Characters { get; set; }
    public DbSet<Battle> Battles { get; set; }
    
    // Shop entities
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductReservation> ProductReservations { get; set; }
    
    // ... 50+ other DbSets
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
```

**Configuration Files:** Located in `src/RTUB.Application/Data/Configurations/`
- Uses Fluent API for entity configuration
- 47+ configuration files

**✅ Need to Add:**
- `public DbSet<InventoryItem> InventoryItems { get; set; }`
- Create `Configurations/InventoryItemConfiguration.cs` for Fluent API setup

---

### **H. Existing Product System (Reference)**
**Path:** `src/RTUB.Core/Entities/Product.cs`

**Note:** This is for the **shop system** (albums, pins, t-shirts), not game inventory.

**Key Differences:**
- `Product` is for **purchasable shop items** with stock management
- `InventoryItem` (to be created) is for **player-owned game items** (beer, potions, etc.)

---

## 3. Configuration System

### **appsettings.json Structure**
**Path:** `src/RTUB.Web/appsettings.json`

**Current Sections:**
```json
{
  "UseSqlServer": false,
  "AllowedHosts": "*",
  "WebPush": { "Enabled": true },
  "LoginPopup": { "Enabled": false },
  
  "ScheduledTasks": {
    "BirthdayEmailScheduler": { "Enabled": true, "Time": "11:00" },
    "MemberStatusUpdate": { "Enabled": true, "Time": "00:00" },
    // ... other tasks
  },
  
  "Cloudflare": {
    "R2": {
      "Bucket": "rtub"
    }
  },
  
  "RankingSystem": {
    "XPRewards": {
      "Rehearsal": 15,
      "Festival": 100,
      "Sarau": 50,
      // ...
    },
    "Levels": [
      { "Name": "Caloiro", "RequiredXP": 0 },
      { "Name": "Tuno", "RequiredXP": 100 },
      // ...
    ]
  },
  
  "Games": {
    "FidelisRewards": { /* ... */ },
    "AvoidQuestions": { /* ... */ },
    "BmrBebeMaisRui": { /* ... */ }
  }
}
```

**✅ Need to Add:**
```json
{
  "MyTuno": {
    "BeerDropChance": 0.2,
    // Future: Other game configuration
  }
}
```

**Configuration Loading:** Likely in `Program.cs` or a startup configuration class.

---

## 4. Database & Migrations

### **Current Database:**
- **SQLite** (based on `UseSqlServer: false`)
- **Location:** `src/RTUB.Web/Migrations/`
- **Latest Migration:** `20260123233357_AddActivityLockAndMeetingAtaConfirmation`

### **Migration Naming Pattern:**
- Format: `YYYYMMDDHHMMSS_DescriptiveName`
- Example: `20260123233357_AddActivityLockAndMeetingAtaConfirmation`

### **Audit System:**
- Automatic tracking: `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`
- Implemented in `ApplicationDbContext` via `OnSaveChanges`

---

## 5. What Needs to Be Implemented

### **A. New Entities**

#### 1. **InventoryItem** (`src/RTUB.Core/Entities/InventoryItem.cs`)
```csharp
public class InventoryItem : BaseEntity
{
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    public InventoryItemType Type { get; set; }
    
    [Required]
    [Range(0, int.MaxValue)]
    public int Quantity { get; set; }
    
    // Navigation
    public virtual ApplicationUser User { get; set; } = null!;
    
    // Factory method pattern (following existing conventions)
    public static InventoryItem Create(string userId, InventoryItemType type, int quantity = 0);
    
    // Business logic
    public void AddQuantity(int amount);
    public bool ConsumeQuantity(int amount);
}
```

#### 2. **InventoryItemType Enum** (`src/RTUB.Core/Enums/InventoryItemType.cs`)
```csharp
namespace RTUB.Core.Enums;

public enum InventoryItemType
{
    Beer = 1
    // Future: Potion = 2, Elixir = 3, etc.
}
```

---

### **B. Repository Layer**

#### 3. **IInventoryRepository** (`src/RTUB.Application/Interfaces/IInventoryRepository.cs`)
```csharp
public interface IInventoryRepository : IRepository<InventoryItem>
{
    Task<InventoryItem?> GetItemAsync(string userId, InventoryItemType type);
    Task AddItemAsync(string userId, InventoryItemType type, int quantity);
    Task<bool> ConsumeItemAsync(string userId, InventoryItemType type, int quantity);
    Task<List<InventoryItem>> GetUserInventoryAsync(string userId);
}
```

#### 4. **InventoryRepository** (`src/RTUB.Application/Repositories/InventoryRepository.cs`)
```csharp
public class InventoryRepository : Repository<InventoryItem>, IInventoryRepository
{
    public async Task<InventoryItem?> GetItemAsync(string userId, InventoryItemType type)
    {
        return await _context.InventoryItems
            .FirstOrDefaultAsync(i => i.UserId == userId && i.Type == type);
    }
    
    public async Task AddItemAsync(string userId, InventoryItemType type, int quantity)
    {
        var existing = await GetItemAsync(userId, type);
        if (existing != null)
        {
            existing.AddQuantity(quantity);
            await UpdateAsync(existing);
        }
        else
        {
            var item = InventoryItem.Create(userId, type, quantity);
            await AddAsync(item);
        }
    }
    
    public async Task<bool> ConsumeItemAsync(string userId, InventoryItemType type, int quantity)
    {
        var item = await GetItemAsync(userId, type);
        if (item == null || item.Quantity < quantity)
            return false;
        
        if (item.ConsumeQuantity(quantity))
        {
            await UpdateAsync(item);
            return true;
        }
        return false;
    }
    
    public async Task<List<InventoryItem>> GetUserInventoryAsync(string userId)
    {
        return await _context.InventoryItems
            .Where(i => i.UserId == userId)
            .ToListAsync();
    }
}
```

---

### **C. Service Layer**

#### 5. **IInventoryService** (`src/RTUB.Application/Interfaces/IInventoryService.cs`)
```csharp
public interface IInventoryService
{
    Task<bool> UseBeerAsync(string userId);
    Task<int> GetBeerQuantityAsync(string userId);
}
```

#### 6. **InventoryService** (`src/RTUB.Application/Services/InventoryService.cs`)
```csharp
public class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly ICharacterRepository _characterRepository;
    private readonly ILogger<InventoryService> _logger;
    
    public async Task<bool> UseBeerAsync(string userId)
    {
        // 1. Get character
        var character = await _characterRepository.GetByUserIdAsync(userId);
        if (character == null)
            return false;
        
        // 2. Check if HP is full
        var currentHp = character.CurrentHP ?? character.TotalHP;
        if (currentHp >= character.TotalHP)
            return false;
        
        // 3. Consume 1 beer
        if (!await _inventoryRepository.ConsumeItemAsync(userId, InventoryItemType.Beer, 1))
            return false;
        
        // 4. Calculate heal (25% of TotalHP)
        var healAmount = (int)Math.Round(character.TotalHP * 0.25);
        
        // 5. Restore HP
        character.Heal(healAmount);
        await _characterRepository.UpdateAsync(character);
        
        _logger.LogInformation("Player {UserId} used beer, restored {HealAmount} HP", userId, healAmount);
        return true;
    }
    
    public async Task<int> GetBeerQuantityAsync(string userId)
    {
        var item = await _inventoryRepository.GetItemAsync(userId, InventoryItemType.Beer);
        return item?.Quantity ?? 0;
    }
}
```

---

### **D. Database Configuration**

#### 7. **InventoryItemConfiguration** (`src/RTUB.Application/Data/Configurations/InventoryItemConfiguration.cs`)
```csharp
public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("InventoryItems");
        
        builder.HasKey(i => i.Id);
        
        builder.Property(i => i.UserId)
            .IsRequired()
            .HasMaxLength(450);
        
        builder.Property(i => i.Type)
            .IsRequired();
        
        builder.Property(i => i.Quantity)
            .IsRequired();
        
        // Indexes for performance
        builder.HasIndex(i => i.UserId);
        builder.HasIndex(i => i.Type);
        
        // Unique constraint (one row per user per item type)
        builder.HasIndex(i => new { i.UserId, i.Type })
            .IsUnique();
        
        // Foreign key relationship
        builder.HasOne(i => i.User)
            .WithMany()
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

#### 8. **Migration** (`src/RTUB.Web/Migrations/YYYYMMDDHHMMSS_AddInventoryItemsTable.cs`)
```bash
# Command to create migration:
dotnet ef migrations add AddInventoryItemsTable --project src/RTUB.Web --startup-project src/RTUB.Web
```

---

### **E. Modifications to Existing Files**

#### 9. **MyTunoScaling.cs**
**Add:**
```csharp
public static double BeerDropChance { get; private set; } = 0.2;
```

**Update `Configure()` method:**
```csharp
public static void Configure(
    // ... existing parameters ...,
    double beerDropChance)
{
    // ... existing assignments ...
    BeerDropChance = beerDropChance;
}
```

#### 10. **BattleService.cs**
**After `ApplyRewardsAsync` (line ~96), add:**
```csharp
// Drop beer on victory
if (combatResult.Outcome == BattleOutcome.AttackerWon)
{
    var random = new Random();
    if (random.NextDouble() < MyTunoScaling.BeerDropChance)
    {
        await _inventoryRepository.AddItemAsync(playerCharacter.UserId, InventoryItemType.Beer, 1);
        _logger.LogInformation("Beer dropped for player {UserId}", playerCharacter.UserId);
    }
}
```

**Constructor:** Add `IInventoryRepository _inventoryRepository` dependency.

#### 11. **ApplicationDbContext.cs**
**Add DbSet:**
```csharp
public DbSet<InventoryItem> InventoryItems { get; set; }
```

#### 12. **appsettings.json**
**Add section:**
```json
{
  "MyTuno": {
    "BeerDropChance": 0.2
  }
}
```

#### 13. **Program.cs**
**Register services:**
```csharp
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
```

---

### **F. UI Implementation**

#### 14. **MyTunoHome.razor**

**Add to @code section:**
```csharp
@inject IInventoryService InventoryService

private int beerQuantity = 0;
private bool usingBeer = false;
private string? successMessage = null;

protected override async Task OnInitializedAsync()
{
    // ... existing code ...
    await LoadBeerQuantity();
}

private async Task LoadBeerQuantity()
{
    if (user != null)
    {
        beerQuantity = await InventoryService.GetBeerQuantityAsync(user.Id);
    }
}

private async Task UseBeer()
{
    if (usingBeer) return;
    
    usingBeer = true;
    successMessage = null;
    
    try
    {
        var success = await InventoryService.UseBeerAsync(user!.Id);
        
        if (success)
        {
            // Reload character and beer quantity
            character = await CharacterService.GetOrCreateCharacterAsync(user.Id);
            await LoadBeerQuantity();
            
            var healAmount = (int)Math.Round(character.TotalHP * 0.25);
            successMessage = $"Recuperaste {healAmount} HP!";
            
            // Auto-hide message after 3 seconds
            StateHasChanged();
            await Task.Delay(3000);
            successMessage = null;
        }
        else
        {
            await JSRuntime.InvokeVoidAsync("alert", "Não tens cerveja ou HP está cheio!");
        }
    }
    catch (Exception ex)
    {
        await JSRuntime.InvokeVoidAsync("alert", $"Erro: {ex.Message}");
        _logger.LogError(ex, "Failed to use beer");
    }
    finally
    {
        usingBeer = false;
        StateHasChanged();
    }
}
```

**Add to Inventory section (Column 1):**
```razor
<div class="col-md-4">
    <div class="card bg-dark text-light">
        <div class="card-body">
            <h3 class="card-title mb-3">Inventário</h3>
            
            <!-- Fidelis -->
            <div class="p-2 border border-success rounded d-flex justify-content-between align-items-center mb-2" 
                 style="background-color: #1e1e1e;">
                <span class="d-flex align-items-center">
                    <i class="bi bi-coin text-success me-2"></i>
                    <span>Fidelis</span>
                </span>
                <strong class="text-success">@user?.FidelisBalance.ToString("F0")</strong>
            </div>
            
            <!-- Beer -->
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
                            disabled="@(usingBeer || beerQuantity == 0 || character.CurrentHP >= character.TotalHP)">
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
            
            <!-- Success Message -->
            @if (!string.IsNullOrEmpty(successMessage))
            {
                <div class="alert alert-success mt-2 mb-0" role="alert">
                    @successMessage
                </div>
            }
        </div>
    </div>
</div>
```

---

## 6. Testing Checklist

### **Unit Tests (Optional but Recommended)**
- [ ] `InventoryService.UseBeerAsync` - Returns false when no beer
- [ ] `InventoryService.UseBeerAsync` - Returns false when HP is full
- [ ] `InventoryService.UseBeerAsync` - Restores exactly 25% HP (rounded)
- [ ] `InventoryService.UseBeerAsync` - Decreases beer quantity by 1
- [ ] `InventoryRepository.AddItemAsync` - Creates new item if not exists
- [ ] `InventoryRepository.AddItemAsync` - Updates quantity if exists
- [ ] `InventoryRepository.ConsumeItemAsync` - Returns false when insufficient quantity

### **Integration Tests**
- [ ] Beer drops after winning battles (~20% over 100+ battles)
- [ ] Beer does NOT drop after losses or draws
- [ ] Database migration applies cleanly
- [ ] Unique constraint prevents duplicate (UserId, Type) rows

### **Manual Testing**
- [ ] Win a battle → Check if beer drops (may take multiple wins)
- [ ] Navigate to MyTunoHome → Verify beer count displays
- [ ] Click "Usar" button → Verify HP increases by 25%
- [ ] Click "Usar" when HP is full → Button should be disabled
- [ ] Click "Usar" when beer quantity is 0 → Button should be disabled
- [ ] Verify success message appears and disappears after 3 seconds
- [ ] Verify beer quantity decreases after use

---

## 7. Implementation Order

**Recommended sequence:**

1. **Database Layer** (30 minutes)
   - Create `InventoryItemType` enum
   - Create `InventoryItem` entity
   - Create `InventoryItemConfiguration` class
   - Add `DbSet<InventoryItem>` to `ApplicationDbContext`
   - Create migration and apply

2. **Repository Layer** (30 minutes)
   - Create `IInventoryRepository` interface
   - Implement `InventoryRepository` class
   - Register in `Program.cs`

3. **Service Layer** (45 minutes)
   - Create `IInventoryService` interface
   - Implement `InventoryService` class
   - Register in `Program.cs`

4. **Configuration** (15 minutes)
   - Update `MyTunoScaling` with `BeerDropChance`
   - Add to `appsettings.json`
   - Update configuration loading (if needed)

5. **Battle Integration** (30 minutes)
   - Modify `BattleService.CreateBattleVsAIAsync`
   - Add beer drop logic after rewards
   - Test battles to verify drops

6. **UI Implementation** (1 hour)
   - Update `MyTunoHome.razor` with beer display
   - Add "Usar" button and handler
   - Style matching existing UI
   - Add success message toast

7. **Testing & Polish** (1 hour)
   - Manual testing of full flow
   - Fix bugs and edge cases
   - Verify all success criteria met
   - Code cleanup and formatting

**Total Estimated Time: 4-5 hours**

---

## 8. Key Architecture Patterns to Follow

### **A. Entity Factory Pattern**
```csharp
// Private constructor for EF Core
private InventoryItem() { }

// Public factory method for business logic
public static InventoryItem Create(string userId, InventoryItemType type, int quantity)
{
    // Validation logic
    return new InventoryItem { ... };
}
```

### **B. Repository Pattern**
- Inherit from `Repository<T>` for base CRUD
- Add specialized methods in interface
- Use `async/await` throughout
- Return `Task<T?>` for nullable results

### **C. Service Pattern**
- Constructor injection of dependencies
- Use interfaces (`IInventoryService`, not concrete class)
- Return `Task<bool>` for success/failure operations
- Log important actions with `ILogger`

### **D. SOLID Principles**
- **Single Responsibility:** Each class has one clear purpose
- **Dependency Inversion:** Depend on `IInventoryRepository`, not `InventoryRepository`
- **Interface Segregation:** Keep interfaces focused and minimal

---

## 9. Potential Issues & Solutions

### **Issue 1: Beer Drops Too Frequently/Infrequently**
**Solution:** Adjust `BeerDropChance` in `appsettings.json` (no code changes needed)

### **Issue 2: HP Restoration Calculation**
**Current Formula:** `healAmount = (int)Math.Round(character.TotalHP * 0.25)`
- Example: TotalHP = 110 → Heal = 28 HP (27.5 rounded)
- Example: TotalHP = 100 → Heal = 25 HP

**Edge Case:** What if CurrentHP + healAmount > TotalHP?
**Solution:** Already handled by `Character.Heal()` method (caps at TotalHP)

### **Issue 3: Concurrent Beer Usage**
**Scenario:** User clicks "Usar" multiple times rapidly
**Solution:** `usingBeer` flag prevents concurrent operations

### **Issue 4: Migration Conflicts**
**If migration already exists:** Delete migration file and run `dotnet ef migrations add` again
**If database already updated:** Use `dotnet ef database update` to sync

---

## 10. Success Criteria Verification

- [x] **Configuration exists:** MyTunoScaling has BeerDropChance property
- [x] **Character HP system works:** `Heal()`, `RestoreHP()`, `CurrentHP` already implemented
- [x] **Repository pattern is established:** Base `Repository<T>` exists
- [x] **BattleService is extensible:** Can add beer drop logic after rewards
- [x] **UI has inventory section:** Fidelis display exists, can add beer below it
- [x] **Configuration system works:** appsettings.json is loaded and used

**All prerequisites are met! Implementation can proceed immediately.**

---

## 11. Quick Reference Commands

```bash
# Navigate to project root
cd /home/runner/work/RTUB/RTUB

# Build project
dotnet build src/RTUB.Web

# Format code (ALWAYS run before committing)
dotnet format src/RTUB.Web

# Create migration
dotnet ef migrations add AddInventoryItemsTable --project src/RTUB.Web --startup-project src/RTUB.Web

# Apply migration
dotnet ef database update --project src/RTUB.Web --startup-project src/RTUB.Web

# Run tests (if exists)
dotnet test

# Run application
dotnet run --project src/RTUB.Web
```

---

## 12. File Checklist

### **Files to Create (6 new files):**
- [ ] `src/RTUB.Core/Entities/InventoryItem.cs`
- [ ] `src/RTUB.Core/Enums/InventoryItemType.cs`
- [ ] `src/RTUB.Application/Interfaces/IInventoryRepository.cs`
- [ ] `src/RTUB.Application/Interfaces/IInventoryService.cs`
- [ ] `src/RTUB.Application/Repositories/InventoryRepository.cs`
- [ ] `src/RTUB.Application/Services/InventoryService.cs`
- [ ] `src/RTUB.Application/Data/Configurations/InventoryItemConfiguration.cs` (7th file)

### **Files to Modify (7 files):**
- [ ] `src/RTUB.Core/Configuration/MyTunoScaling.cs`
- [ ] `src/RTUB.Application/Services/BattleService.cs`
- [ ] `src/RTUB.Application/Data/ApplicationDbContext.cs`
- [ ] `src/RTUB.Web/appsettings.json`
- [ ] `src/RTUB.Web/Pages/MyTuno/MyTunoHome.razor`
- [ ] `src/RTUB.Web/Program.cs`
- [ ] Migration file (auto-generated)

---

## 13. Code Style Guidelines

### **Naming Conventions:**
- **Classes/Methods:** PascalCase (`InventoryService`, `UseBeerAsync`)
- **Private fields:** _camelCase (`_inventoryRepository`)
- **Local variables:** camelCase (`beerQuantity`, `healAmount`)
- **Constants:** PascalCase (`BaseWinXP`, `BeerDropChance`)

### **Async/Await:**
- Always use `async Task` (never `.Result` or `.Wait()`)
- Suffix async methods with `Async` (`UseBeerAsync`)
- Use `ConfigureAwait(false)` in library code (Repository/Service layers)

### **Error Handling:**
- Validate inputs in entity constructors/factory methods
- Return `false` or `null` for expected failures (no beer, HP full)
- Throw exceptions for unexpected errors (null dependencies)
- Log errors with `ILogger`

### **XML Documentation:**
Use XML comments for public APIs:
```csharp
/// <summary>
/// Uses a beer to restore 25% of the character's total HP
/// </summary>
/// <param name="userId">The user ID</param>
/// <returns>True if beer was used successfully, false if not enough beer or HP is full</returns>
public async Task<bool> UseBeerAsync(string userId)
```

---

**End of Exploration Summary**

This document provides complete context for implementing the Beer Inventory System. All existing code patterns, architecture decisions, and implementation details are documented above.
