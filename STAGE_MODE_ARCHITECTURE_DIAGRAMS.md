# 🏗️ Stage Mode Architecture - Quick Reference

## Current System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        PRESENTATION LAYER                        │
│                     /src/RTUB.Web/Pages/MyTuno/                 │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  MyTunoHome.razor (/my-tuno)                                    │
│  ├─── Character Stats Display                                   │
│  ├─── Inventory (Fidelis, Beer)                                 │
│  ├─── Upgrade Buttons                                           │
│  └─── Navigation to Arena                                       │
│                                                                  │
│  Arena.razor (/my-tuno/arena)                                   │
│  ├─── Opponent Selection (4 random)                             │
│  ├─── Battle Animation (Phaser)                                 │
│  ├─── Battle Results                                            │
│  └─── Battle History Table                                      │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      APPLICATION LAYER                           │
│                  /src/RTUB.Application/Services/                │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ICharacterService ──▶ CharacterService                         │
│  └── GetOrCreateCharacterAsync()                                │
│  └── UpdateCharacterAsync()                                     │
│                                                                  │
│  IBattleService ──▶ BattleService                               │
│  └── CreateBattleVsOpponentAsync()                              │
│      ├── DeterministicCombatEngine.Simulate()                   │
│      ├── ApplyRewards (XP, Fidelis)                             │
│      ├── TryDropBeer()                                          │
│      └── SaveBattle()                                           │
│                                                                  │
│  IInventoryService ──▶ InventoryService                         │
│  └── UseBeerAsync() (heal 25% HP)                               │
│  └── GetBeerQuantityAsync()                                     │
│                                                                  │
│  IUpgradeService ──▶ UpgradeService                             │
│  └── UpgradeStatAsync(StatType, cost)                           │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    REPOSITORY LAYER                              │
│                /src/RTUB.Application/Repositories/              │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ICharacterRepository ──▶ CharacterRepository                   │
│  └── GetByUserIdAsync()                                         │
│  └── GetRandomOpponentsAsync()                                  │
│  └── GetMemberCharactersAsync()                                 │
│                                                                  │
│  IBattleRepository ──▶ BattleRepository                         │
│  └── GetByCharacterIdAsync()                                    │
│  └── AddAsync()                                                 │
│                                                                  │
│  IInventoryRepository ──▶ InventoryRepository                   │
│  └── GetItemAsync(userId, type)                                 │
│  └── AddItemAsync(userId, type, quantity)                       │
│  └── ConsumeItemAsync(userId, type, quantity)                   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                         DOMAIN LAYER                             │
│                    /src/RTUB.Core/Entities/                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Character                                                       │
│  ├── UserId (FK to ApplicationUser)                             │
│  ├── Level, XP                                                  │
│  ├── HP, Power, Speed, CriticalChance                           │
│  ├── HpUpgrades, PowerUpgrades, SpeedUpgrades, CriticalUpgrades │
│  ├── CurrentHP (nullable)                                       │
│  └── Computed: TotalHP, TotalPower, TotalSpeed, TotalCritChance │
│                                                                  │
│  Battle                                                          │
│  ├── AttackerCharacterId (FK)                                   │
│  ├── DefenderCharacterId (FK)                                   │
│  ├── Seed (RNG)                                                 │
│  ├── Outcome (AttackerWon, DefenderWon, Draw)                   │
│  ├── AttackerXP, AttackerFidelis                                │
│  └── ReplayJson (battle events)                                 │
│                                                                  │
│  InventoryItem                                                   │
│  ├── UserId (FK)                                                │
│  ├── Type (Beer = 1)                                            │
│  └── Quantity                                                   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                       DATABASE LAYER                             │
│                SQLite (ApplicationDbContext)                     │
├─────────────────────────────────────────────────────────────────┤
│  Characters Table                                                │
│  Battles Table                                                   │
│  InventoryItems Table                                            │
└─────────────────────────────────────────────────────────────────┘
```

---

## Proposed Stage Mode Architecture (Option A)

```
┌─────────────────────────────────────────────────────────────────┐
│                    NEW PRESENTATION LAYER                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Stages.razor (/my-tuno/stages)  ◀── NEW                        │
│  ├─── Available Stages Grid                                     │
│  ├─── Stage Info Cards                                          │
│  │    ├── Stage Number & Name                                   │
│  │    ├── Boss Stats Preview                                    │
│  │    ├── Instrument Reward Icon                                │
│  │    └── Locked/Unlocked State                                 │
│  ├─── Stage Battle View                                         │
│  │    ├── Boss Encounter                                        │
│  │    ├── Battle Animation (reuse Phaser)                       │
│  │    └── Victory Screen                                        │
│  └─── Instrument Collection Display                             │
│                                                                  │
│  MyTunoHome.razor (MODIFIED)                                    │
│  └─── Add "Stages" navigation button                            │
│  └─── Display collected instruments                             │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    NEW APPLICATION LAYER                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  IStageService ──▶ StageService  ◀── NEW                        │
│  └── GetAvailableStagesAsync(characterLevel)                    │
│  └── GetStageProgressAsync(characterId, stageNumber)            │
│  └── CreateStageBattleAsync(characterId, stageNumber)           │
│  └── CompleteStageAsync(characterId, stageNumber)               │
│                                                                  │
│  IBattleService (EXTENDED)                                      │
│  └── CreateStageBattleAsync()  ◀── NEW METHOD                   │
│      ├── Load Stage Boss Stats                                  │
│      ├── DeterministicCombatEngine.Simulate()                   │
│      ├── ApplyRewards (XP, Fidelis)                             │
│      ├── DropInstrument (if first completion)                   │
│      └── SaveBattle()                                           │
│                                                                  │
│  IInventoryService (EXTENDED)                                   │
│  └── AddInstrumentAsync(userId, instrumentType)  ◀-- NEW        │
│  └── GetInstrumentsAsync(userId)  ◀-- NEW                       │
│  └── HasInstrumentAsync(userId, instrumentType)  ◀-- NEW        │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    NEW REPOSITORY LAYER                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  IStageRepository ──▶ StageRepository  ◀── NEW                  │
│  └── GetByStageNumberAsync(stageNumber)                         │
│  └── GetStagesUnlockedForLevelAsync(level)                      │
│  └── GetAllStagesAsync()                                        │
│                                                                  │
│  ICharacterStageProgressRepository  ◀── NEW                     │
│  └── GetProgressAsync(characterId, stageNumber)                 │
│  └── CreateOrUpdateProgressAsync()                              │
│  └── GetCompletedStagesAsync(characterId)                       │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      NEW DOMAIN ENTITIES                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Stage  ◀── NEW ENTITY                                          │
│  ├── Id (PK)                                                    │
│  ├── StageNumber (1, 2, 3...)                                   │
│  ├── Name ("Guitarra Trial", "Bandolim Challenge", etc.)        │
│  ├── RequiredLevel (unlock threshold)                           │
│  ├── RewardInstrumentType (InventoryItemType enum)              │
│  ├── BossHP                                                     │
│  ├── BossPower                                                  │
│  ├── BossSpeed                                                  │
│  └── BossCriticalChance                                         │
│                                                                  │
│  CharacterStageProgress  ◀── NEW ENTITY                         │
│  ├── Id (PK)                                                    │
│  ├── CharacterId (FK)                                           │
│  ├── StageNumber                                                │
│  ├── IsCompleted (bool)                                         │
│  ├── CompletedAt (DateTime?)                                    │
│  ├── TimesCompleted (int, for farming tracking)                 │
│  └── BestCompletionTime (TimeSpan?, optional feature)           │
│                                                                  │
│  InventoryItemType (EXTENDED ENUM)                              │
│  ├── Beer = 1                                                   │
│  ├── Guitarra = 2  ◀── NEW                                      │
│  ├── Bandolim = 3  ◀── NEW                                      │
│  ├── Cavaquinho = 4  ◀── NEW                                    │
│  ├── Acordeao = 5  ◀── NEW                                      │
│  ├── Fagote = 6  ◀── NEW                                        │
│  ├── Flauta = 7  ◀── NEW                                        │
│  ├── Baixo = 8  ◀── NEW                                         │
│  ├── Contrabaixo = 9  ◀── NEW                                   │
│  ├── Percussao = 10  ◀── NEW                                    │
│  ├── Pandeireta = 11  ◀── NEW                                   │
│  ├── Estandarte = 12  ◀── NEW                                   │
│  └── Violino = 13  ◀── NEW                                      │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    NEW DATABASE TABLES                           │
├─────────────────────────────────────────────────────────────────┤
│  Stages Table  ◀── NEW                                          │
│  ├── PK: Id                                                     │
│  ├── StageNumber (unique index)                                 │
│  └── Boss stats + reward instrument                             │
│                                                                  │
│  CharacterStageProgress Table  ◀── NEW                          │
│  ├── PK: Id                                                     │
│  ├── FK: CharacterId                                            │
│  ├── StageNumber                                                │
│  ├── Unique Index: (CharacterId, StageNumber)                   │
│  └── Completion tracking                                        │
│                                                                  │
│  InventoryItems Table (NO CHANGE)                               │
│  └── Type column now supports new instrument values             │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Battle Flow Comparison

### Current Arena Flow
```
User Selects Opponent
      ↓
IBattleService.CreateBattleVsOpponentAsync()
      ↓
Load Attacker & Defender Characters
      ↓
DeterministicCombatEngine.Simulate()
      ↓
Calculate Rewards (Fixed: 50 XP, 10F)
      ↓
Update Character (AddXP, AddFidelis, UpdateHP)
      ↓
TryDropBeer (20% chance)
      ↓
Save Battle Record
      ↓
Return Battle with ReplayJson
      ↓
Phaser Animates Battle
      ↓
Display Results
```

### Proposed Stage Flow
```
User Selects Unlocked Stage
      ↓
IStageService.CreateStageBattleAsync()
      ↓
Load Character & Stage Boss Stats
      ↓
Check Stage Progress (first completion?)
      ↓
DeterministicCombatEngine.Simulate()
      ↓
Calculate Rewards (Scaled: XP varies by stage)
      ↓
Update Character (AddXP, AddFidelis, UpdateHP)
      ↓
IF First Completion:
│ DropInstrument (100% guaranteed)
│ MarkStageCompleted()
ELSE:
│ TryDropBeer (20% chance)
      ↓
Save Battle Record
      ↓
Return Battle with ReplayJson
      ↓
Phaser Animates Battle
      ↓
Display Results + Instrument Reward (if applicable)
```

---

## Stage Scaling Example

```
Stage 1: Guitarra Trial
├── RequiredLevel: 1
├── BossHP: 150 (1.5x player base)
├── BossPower: 12
├── BossSpeed: 10
├── BossCriticalChance: 0.01
├── XP Reward: 75 XP
├── Fidelis Reward: 15F
└── Instrument: Guitarra

Stage 2: Bandolim Challenge
├── RequiredLevel: 3
├── BossHP: 250
├── BossPower: 18
├── BossSpeed: 15
├── BossCriticalChance: 0.05
├── XP Reward: 150 XP
├── Fidelis Reward: 25F
└── Instrument: Bandolim

Stage 3: Cavaquinho Quest
├── RequiredLevel: 5
├── BossHP: 400
├── BossPower: 25
├── BossSpeed: 20
├── BossCriticalChance: 0.08
├── XP Reward: 250 XP
├── Fidelis Reward: 40F
└── Instrument: Cavaquinho

... (continues for all 12 instruments)
```

**Scaling Formula:**
```csharp
BossHP = BaseHP * (1.5 + (stageNumber * 0.3))
BossPower = BasePower * (1.2 + (stageNumber * 0.2))
BossSpeed = BaseSpeed * (1.0 + (stageNumber * 0.15))
BossCriticalChance = 0.01 + (stageNumber * 0.01)

XpReward = 50 + (stageNumber * 25)
FidelisReward = 10 + (stageNumber * 5)
```

---

## Dependency Injection Setup

```csharp
// ServiceCollectionExtensions.cs
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMyTunoServices(this IServiceCollection services)
    {
        // Existing services
        services.AddScoped<ICharacterService, CharacterService>();
        services.AddScoped<IBattleService, BattleService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IUpgradeService, UpgradeService>();
        
        // NEW: Stage Mode services
        services.AddScoped<IStageService, StageService>();
        
        // Repositories
        services.AddScoped<ICharacterRepository, CharacterRepository>();
        services.AddScoped<IBattleRepository, BattleRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        
        // NEW: Stage repositories
        services.AddScoped<IStageRepository, StageRepository>();
        services.AddScoped<ICharacterStageProgressRepository, CharacterStageProgressRepository>();
        
        return services;
    }
}
```

---

## Migration Script Template

```bash
# From /src/RTUB.Application directory
dotnet ef migrations add AddStageMode --startup-project ../RTUB.Web

# Review migration in Migrations/ folder
# Then apply:
dotnet ef database update --startup-project ../RTUB.Web
```

**Expected Migration Content:**
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Add Stages table
    migrationBuilder.CreateTable(
        name: "Stages",
        columns: table => new
        {
            Id = table.Column<int>(nullable: false)
                .Annotation("Sqlite:Autoincrement", true),
            StageNumber = table.Column<int>(nullable: false),
            Name = table.Column<string>(maxLength: 100, nullable: false),
            RequiredLevel = table.Column<int>(nullable: false),
            RewardInstrumentType = table.Column<int>(nullable: false),
            BossHP = table.Column<int>(nullable: false),
            BossPower = table.Column<int>(nullable: false),
            BossSpeed = table.Column<int>(nullable: false),
            BossCriticalChance = table.Column<double>(nullable: false),
            // Audit fields
            CreatedAt = table.Column<string>(nullable: false),
            UpdatedAt = table.Column<string>(nullable: true),
            CreatedBy = table.Column<string>(nullable: true),
            UpdatedBy = table.Column<string>(nullable: true)
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_Stages", x => x.Id);
        });
    
    // Add CharacterStageProgress table
    migrationBuilder.CreateTable(
        name: "CharacterStageProgress",
        columns: table => new
        {
            Id = table.Column<int>(nullable: false)
                .Annotation("Sqlite:Autoincrement", true),
            CharacterId = table.Column<int>(nullable: false),
            StageNumber = table.Column<int>(nullable: false),
            IsCompleted = table.Column<bool>(nullable: false, defaultValue: false),
            CompletedAt = table.Column<string>(nullable: true),
            TimesCompleted = table.Column<int>(nullable: false, defaultValue: 0),
            // Audit fields
            CreatedAt = table.Column<string>(nullable: false),
            UpdatedAt = table.Column<string>(nullable: true)
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_CharacterStageProgress", x => x.Id);
            table.ForeignKey(
                name: "FK_CharacterStageProgress_Characters_CharacterId",
                column: x => x.CharacterId,
                principalTable: "Characters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        });
    
    // Indexes
    migrationBuilder.CreateIndex(
        name: "IX_Stages_StageNumber",
        table: "Stages",
        column: "StageNumber",
        unique: true);
    
    migrationBuilder.CreateIndex(
        name: "IX_CharacterStageProgress_CharacterId_StageNumber",
        table: "CharacterStageProgress",
        columns: new[] { "CharacterId", "StageNumber" },
        unique: true);
    
    // Seed initial stages
    migrationBuilder.InsertData(
        table: "Stages",
        columns: new[] { "StageNumber", "Name", "RequiredLevel", "RewardInstrumentType", 
                        "BossHP", "BossPower", "BossSpeed", "BossCriticalChance", "CreatedAt" },
        values: new object[,]
        {
            { 1, "Guitarra Trial", 1, 2, 150, 12, 10, 0.01, DateTime.UtcNow.ToString("o") },
            { 2, "Bandolim Challenge", 3, 3, 250, 18, 15, 0.05, DateTime.UtcNow.ToString("o") },
            // ... etc
        });
}
```

---

## File Structure Summary

```
RTUB/
├── src/
│   ├── RTUB.Core/
│   │   ├── Entities/
│   │   │   ├── Character.cs (existing)
│   │   │   ├── Battle.cs (existing)
│   │   │   ├── InventoryItem.cs (existing)
│   │   │   ├── Stage.cs  ◀── NEW
│   │   │   └── CharacterStageProgress.cs  ◀── NEW
│   │   ├── Enums/
│   │   │   ├── InstrumentType.cs (existing)
│   │   │   ├── InventoryItemType.cs (EXTEND)
│   │   │   └── BattleOutcome.cs (existing)
│   │   └── Configuration/
│   │       └── MyTunoScaling.cs (EXTEND - add stage scaling)
│   │
│   ├── RTUB.Application/
│   │   ├── Interfaces/
│   │   │   ├── ICharacterService.cs (existing)
│   │   │   ├── IBattleService.cs (EXTEND)
│   │   │   ├── IInventoryService.cs (EXTEND)
│   │   │   ├── IStageService.cs  ◀── NEW
│   │   │   ├── IStageRepository.cs  ◀── NEW
│   │   │   └── ICharacterStageProgressRepository.cs  ◀── NEW
│   │   ├── Services/
│   │   │   ├── BattleService.cs (EXTEND)
│   │   │   ├── InventoryService.cs (EXTEND)
│   │   │   └── StageService.cs  ◀── NEW
│   │   ├── Repositories/
│   │   │   ├── StageRepository.cs  ◀── NEW
│   │   │   └── CharacterStageProgressRepository.cs  ◀── NEW
│   │   ├── Data/
│   │   │   └── ApplicationDbContext.cs (EXTEND DbSets)
│   │   └── Migrations/
│   │       └── YYYYMMDDHHMMSS_AddStageMode.cs  ◀── NEW
│   │
│   └── RTUB.Web/
│       └── Pages/
│           └── MyTuno/
│               ├── MyTunoHome.razor (MODIFY)
│               ├── Arena.razor (existing)
│               └── Stages.razor  ◀── NEW
│
└── STAGE_MODE_IMPLEMENTATION_GUIDE.md  ◀── THIS DOCUMENT
```

---

## Quick Command Reference

```bash
# Navigate to project
cd /home/runner/work/RTUB/RTUB

# Create migration
cd src/RTUB.Application
dotnet ef migrations add AddStageMode --startup-project ../RTUB.Web

# Apply migration
dotnet ef database update --startup-project ../RTUB.Web

# Build project
cd ../..
dotnet build

# Format code
dotnet format

# Run project
cd src/RTUB.Web
dotnet run
```

---

**Ready to implement!** 🚀
