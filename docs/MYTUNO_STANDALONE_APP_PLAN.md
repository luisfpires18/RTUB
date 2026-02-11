# MyTuno Standalone App — Agent Build Plan

> **Goal**: Extract the "My Tuno" RPG game from the RTUB monolith into a standalone **Blazor Server .NET 10** app with **EF Core + SQLite**.
> This document is a step-by-step blueprint an AI agent can follow to create the app **from scratch** while preserving all existing game logic.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Solution Structure](#2-solution-structure)
3. [Phase 1 — Project Scaffolding](#3-phase-1--project-scaffolding)
4. [Phase 2 — Domain Layer (Core)](#4-phase-2--domain-layer-core)
5. [Phase 3 — Application Layer (Services)](#5-phase-3--application-layer-services)
6. [Phase 4 — Infrastructure Layer (EF Core + SQLite)](#6-phase-4--infrastructure-layer-ef-core--sqlite)
7. [Phase 5 — Presentation Layer (Blazor Pages & Components)](#7-phase-5--presentation-layer-blazor-pages--components)
8. [Phase 6 — Static Assets (Sprites, JS, CSS, Audio)](#8-phase-6--static-assets-sprites-js-css-audio)
9. [Phase 7 — Configuration & Scaling System](#9-phase-7--configuration--scaling-system)
10. [Phase 8 — Authentication & User System](#10-phase-8--authentication--user-system)
11. [Phase 9 — Tests](#11-phase-9--tests)
12. [Phase 10 — Final Wiring & Startup](#12-phase-10--final-wiring--startup)
13. [Complete File Inventory (Source → Target)](#13-complete-file-inventory-source--target)
14. [Database Schema](#14-database-schema)
15. [Key Differences from RTUB](#15-key-differences-from-rtub)
16. [Appendix A — Scaling Config JSON](#appendix-a--scaling-config-json)
17. [Appendix B — Game Mechanics Reference](#appendix-b--game-mechanics-reference)

---

## 1. Architecture Overview

```
MyTuno (Standalone)
├── MyTuno.Core          → Domain entities, enums, helpers, scaling constants
├── MyTuno.Application   → Services, interfaces, DTOs, repositories (abstractions)
├── MyTuno.Infrastructure→ EF Core DbContext, SQLite, repository implementations
├── MyTuno.Web           → Blazor Server app, pages, components, wwwroot assets
└── MyTuno.Tests         → Unit + integration tests
```

### Technology Stack

| Component | Technology |
|-----------|-----------|
| Framework | .NET 10 |
| UI | Blazor Server (Interactive SSR) |
| Database | SQLite via EF Core |
| Auth | ASP.NET Core Identity |
| Battle Animations | PixiJS (JavaScript interop) |
| CSS | Bootstrap 5 + custom CSS |
| Config | `scaling.config.json` bound to options pattern |

### Game Modes (all must be preserved)

| Mode | Description |
|------|-------------|
| **Arena** | PvP matchmaking battles with replay animations |
| **Stage Mode** | Progressive PvE stages with biomes, bosses, loot drops |
| **Boss Mode** | Special boss encounters |
| **Survive Mode** | Endless survival with escalating difficulty |
| **Destilaria (Gathering)** | Resource gathering with energy system |
| **Forge** | Craft weapons from instrument parts + drinks |
| **Upgrades** | Spend Fidelis to upgrade stats (HP, Power, Speed, Defense, Crit) |
| **Leaderboard** | Rankings by Arena wins, Level, Stage progress |
| **Inventory** | Consumables (Fino, Shot, Caneca, Cigarro, Canhão, Penalty) + materials |
| **Daily Reward** | Login bonus based on character level |
| **Equipment** | Equip head/shoulders/chest/gloves/legs/boots gear |

---

## 2. Solution Structure

```
MyTuno/
├── MyTuno.sln
├── Directory.Build.props
├── Directory.Packages.props
├── scaling.config.json
│
├── src/
│   ├── MyTuno.Core/
│   │   ├── MyTuno.Core.csproj
│   │   ├── Configuration/
│   │   │   └── MyTunoScaling.cs
│   │   ├── Entities/
│   │   │   ├── BaseEntity.cs
│   │   │   ├── ApplicationUser.cs
│   │   │   ├── Character.cs
│   │   │   ├── InventoryItem.cs
│   │   │   ├── StageProgress.cs
│   │   │   ├── SurviveModeProgress.cs
│   │   │   ├── BossModeProgress.cs
│   │   │   ├── StageEnemy.cs
│   │   │   ├── ForgedWeapon.cs
│   │   │   ├── ForgeComboConfig.cs
│   │   │   ├── ItemTypeConfig.cs
│   │   │   ├── LeaderboardComment.cs
│   │   │   ├── LeaderboardCommentLike.cs
│   │   │   ├── Transaction.cs
│   │   │   └── Trophy.cs
│   │   ├── Enums/
│   │   │   ├── BattleOutcome.cs
│   │   │   ├── EnemyType.cs
│   │   │   ├── EquipmentSlot.cs
│   │   │   ├── InventoryItemType.cs
│   │   │   ├── StatType.cs
│   │   │   └── WeaponType.cs
│   │   └── Helpers/
│   │       ├── WeaponTypeHelper.cs
│   │       └── EquipmentDropHelper.cs
│   │
│   ├── MyTuno.Application/
│   │   ├── MyTuno.Application.csproj
│   │   ├── Configuration/
│   │   │   └── MyTunoScalingConfiguration.cs   (full config model ~700 lines)
│   │   ├── DTOs/
│   │   │   ├── BattleResult.cs
│   │   │   ├── StageBattleResult.cs
│   │   │   ├── BossModeBattleResult.cs
│   │   │   ├── SurviveModeLevelResult.cs
│   │   │   ├── CombatResult.cs
│   │   │   ├── MyTunoLeaderboardEntry.cs
│   │   │   ├── UpgradeResult.cs
│   │   │   ├── ItemTypeConfigDto.cs
│   │   │   ├── LeaderboardCommentDto.cs
│   │   │   └── TransactionHistoryEntryDto.cs
│   │   ├── Interfaces/
│   │   │   ├── IRepository.cs
│   │   │   ├── ICharacterRepository.cs
│   │   │   ├── ICharacterService.cs
│   │   │   ├── IBattleService.cs
│   │   │   ├── ICombatEngine.cs
│   │   │   ├── IStageService.cs
│   │   │   ├── IStageBiomeService.cs
│   │   │   ├── IStageProgressRepository.cs
│   │   │   ├── IStageEnemyRepository.cs
│   │   │   ├── IStageEnemyManagementService.cs
│   │   │   ├── IBossModeService.cs
│   │   │   ├── IBossModeProgressRepository.cs
│   │   │   ├── ISurviveModeService.cs
│   │   │   ├── ISurviveModeProgressRepository.cs
│   │   │   ├── IInventoryService.cs
│   │   │   ├── IInventoryRepository.cs
│   │   │   ├── IUpgradeService.cs
│   │   │   ├── IRankingService.cs
│   │   │   ├── ITransactionService.cs
│   │   │   ├── ITransactionRepository.cs
│   │   │   ├── ILeaderboardCommentService.cs
│   │   │   ├── ILeaderboardCommentRepository.cs
│   │   │   ├── IForgeComboConfigRepository.cs
│   │   │   ├── IItemTypeConfigRepository.cs
│   │   │   ├── IItemTypeConfigService.cs
│   │   │   ├── ITrophyRepository.cs
│   │   │   ├── ITrophyService.cs
│   │   │   └── IUserProfileRepository.cs
│   │   └── Services/
│   │       ├── BattleService.cs
│   │       ├── DeterministicCombatEngine.cs
│   │       ├── CharacterService.cs
│   │       ├── StageService.cs
│   │       ├── StageBiomeService.cs
│   │       ├── StageEnemyManagementService.cs
│   │       ├── BossModeService.cs
│   │       ├── SurviveModeService.cs
│   │       ├── InventoryService.cs
│   │       ├── UpgradeService.cs
│   │       ├── RankingService.cs
│   │       ├── TransactionService.cs
│   │       ├── LeaderboardCommentService.cs
│   │       ├── ItemTypeConfigService.cs
│   │       ├── ItemTypeConfigInitializer.cs
│   │       ├── TrophyService.cs
│   │       └── TransactionFilterService.cs
│   │
│   ├── MyTuno.Infrastructure/
│   │   ├── MyTuno.Infrastructure.csproj
│   │   ├── Data/
│   │   │   └── MyTunoDbContext.cs
│   │   ├── Repositories/
│   │   │   ├── Repository.cs
│   │   │   ├── CharacterRepository.cs
│   │   │   ├── InventoryRepository.cs
│   │   │   ├── StageProgressRepository.cs
│   │   │   ├── StageEnemyRepository.cs
│   │   │   ├── BossModeProgressRepository.cs
│   │   │   ├── SurviveModeProgressRepository.cs
│   │   │   ├── TransactionRepository.cs
│   │   │   ├── LeaderboardCommentRepository.cs
│   │   │   ├── ForgeComboConfigRepository.cs
│   │   │   ├── ItemTypeConfigRepository.cs
│   │   │   ├── TrophyRepository.cs
│   │   │   └── UserProfileRepository.cs
│   │   └── Migrations/
│   │       └── (auto-generated)
│   │
│   └── MyTuno.Web/
│       ├── MyTuno.Web.csproj
│       ├── Program.cs
│       ├── App.razor
│       ├── _Imports.razor
│       ├── appsettings.json
│       ├── scaling.config.json
│       ├── Pages/
│       │   ├── MyTunoHome.razor / .razor.css
│       │   ├── Arena.razor / .razor.css
│       │   ├── Stage.razor / .razor.css
│       │   ├── SurviveMode.razor / .razor.css
│       │   ├── BossMode.razor / .razor.css
│       │   ├── AllCharacters.razor
│       │   ├── WeaponDrinkConfig.razor
│       │   ├── StageEnemies.razor / .razor.css
│       │   ├── Auth/
│       │   │   ├── Login.razor
│       │   │   └── Register.razor
│       │   └── Profile.razor
│       ├── Shared/
│       │   ├── MainLayout.razor
│       │   └── NavMenu.razor
│       ├── Components/
│       │   ├── Cards/
│       │   │   ├── CharacterCard.razor / .razor.css
│       │   │   ├── CharacterOverviewCard.razor / .razor.css
│       │   │   ├── EnemyCard.razor / .razor.css
│       │   │   └── StageEnemyCard.razor / .razor.css
│       │   └── Shared/
│       │       └── CacheBuster.cs
│       └── wwwroot/
│           ├── css/
│           │   ├── site.css
│           │   └── my-tuno.css
│           ├── js/
│           │   ├── pixiBattle.js         (arena PvP battle animations)
│           │   ├── pixiStageBattle.js    (stage mode battle animations)
│           │   └── pixiSurviveMode.js    (survive mode animations)
│           ├── sprites/games/my-tuno/    (all game sprites — 236+ files)
│           │   ├── default_tuno.png
│           │   ├── tuno_attacking_left.png
│           │   ├── tuno_attacking_right.png
│           │   ├── backgrounds/          (arena, forest, swamp, mountains, etc.)
│           │   └── enemies/              (per-biome enemy + boss sprites)
│           └── audio/games/my-tuno/      (battle sound effects)
│
└── tests/
    ├── MyTuno.Core.Tests/
    │   ├── Entities/
    │   │   ├── CharacterTests.cs
    │   │   ├── StageProgressTests.cs
    │   │   ├── BossModeProgressTests.cs
    │   │   ├── ForgedWeaponTests.cs
    │   │   ├── InventoryItemTests.cs
    │   │   ├── LeaderboardCommentTests.cs
    │   │   └── LeaderboardCommentLikeTests.cs
    │   └── Helpers/
    │       └── WeaponTypeHelperTests.cs
    │
    └── MyTuno.Application.Tests/
        └── Services/
            ├── BattleServiceTests.cs
            ├── CombatEngineTests.cs
            ├── InventoryServiceTests.cs
            ├── CharacterServiceTests.cs
            ├── UpgradeServiceTests.cs
            ├── StageServiceTests.cs
            └── LeaderboardCommentServiceTests.cs
```

---

## 3. Phase 1 — Project Scaffolding

### Step 1.1 — Create Solution

```bash
mkdir MyTuno && cd MyTuno
dotnet new sln -n MyTuno

# Create projects
dotnet new classlib -n MyTuno.Core -o src/MyTuno.Core -f net10.0
dotnet new classlib -n MyTuno.Application -o src/MyTuno.Application -f net10.0
dotnet new classlib -n MyTuno.Infrastructure -o src/MyTuno.Infrastructure -f net10.0
dotnet new blazorserver -n MyTuno.Web -o src/MyTuno.Web -f net10.0
dotnet new xunit -n MyTuno.Core.Tests -o tests/MyTuno.Core.Tests -f net10.0
dotnet new xunit -n MyTuno.Application.Tests -o tests/MyTuno.Application.Tests -f net10.0

# Add projects to solution
dotnet sln add src/MyTuno.Core
dotnet sln add src/MyTuno.Application
dotnet sln add src/MyTuno.Infrastructure
dotnet sln add src/MyTuno.Web
dotnet sln add tests/MyTuno.Core.Tests
dotnet sln add tests/MyTuno.Application.Tests

# Add project references
dotnet add src/MyTuno.Application reference src/MyTuno.Core
dotnet add src/MyTuno.Infrastructure reference src/MyTuno.Core
dotnet add src/MyTuno.Infrastructure reference src/MyTuno.Application
dotnet add src/MyTuno.Web reference src/MyTuno.Core
dotnet add src/MyTuno.Web reference src/MyTuno.Application
dotnet add src/MyTuno.Web reference src/MyTuno.Infrastructure
dotnet add tests/MyTuno.Core.Tests reference src/MyTuno.Core
dotnet add tests/MyTuno.Application.Tests reference src/MyTuno.Application
dotnet add tests/MyTuno.Application.Tests reference src/MyTuno.Core
```

### Step 1.2 — NuGet Packages

**MyTuno.Core** — no external packages needed (pure domain).

**MyTuno.Application**:
```xml
<PackageReference Include="Microsoft.Extensions.Options" Version="10.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
```

**MyTuno.Infrastructure**:
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.0" />
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.0" />
```

**MyTuno.Web**:
```xml
<PackageReference Include="Microsoft.AspNetCore.Identity.UI" Version="10.0.0" />
```

**Tests**:
```xml
<PackageReference Include="xunit" Version="2.*" />
<PackageReference Include="Moq" Version="4.*" />
<PackageReference Include="FluentAssertions" Version="7.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.0" />
```

### Step 1.3 — Directory.Build.props

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

---

## 4. Phase 2 — Domain Layer (Core)

Port the following files from RTUB. **Rename namespace** from `RTUB.Core` → `MyTuno.Core`.

### 4.1 — Entities to Port (1:1 copy with namespace change)

| Source File (RTUB) | Target File (MyTuno) | Notes |
|---|---|---|
| `RTUB.Core/Entities/BaseEntity.cs` | `MyTuno.Core/Entities/BaseEntity.cs` | Base class with `Id`, `CreatedAt`, `UpdatedAt` |
| `RTUB.Core/Entities/Character.cs` | `MyTuno.Core/Entities/Character.cs` | **Main game entity** (~700 lines). Contains all stats, upgrades, equipment slots, combat logic, buff tracking, XP leveling. Uses `MyTunoScaling` static class. |
| `RTUB.Core/Entities/InventoryItem.cs` | `MyTuno.Core/Entities/InventoryItem.cs` | Item with `UserId`, `Type` (InventoryItemType), `Quantity` |
| `RTUB.Core/Entities/StageProgress.cs` | `MyTuno.Core/Entities/StageProgress.cs` | Tracks current stage, highest stage, enemies defeated. Contains `GetEnemyTypeForStage()` |
| `RTUB.Core/Entities/SurviveModeProgress.cs` | `MyTuno.Core/Entities/SurviveModeProgress.cs` | Tracks survive mode current/highest level |
| `RTUB.Core/Entities/BossModeProgress.cs` | `MyTuno.Core/Entities/BossModeProgress.cs` | Tracks boss mode progress |
| `RTUB.Core/Entities/StageEnemy.cs` | `MyTuno.Core/Entities/StageEnemy.cs` | Custom enemy configuration per stage |
| `RTUB.Core/Entities/ForgedWeapon.cs` | `MyTuno.Core/Entities/ForgedWeapon.cs` | Weapons created from instruments + drinks. Has `WeaponType`, stat bonuses, enhancement level |
| `RTUB.Core/Entities/ForgeComboConfig.cs` | `MyTuno.Core/Entities/ForgeComboConfig.cs` | Forge recipe definitions |
| `RTUB.Core/Entities/ItemTypeConfig.cs` | `MyTuno.Core/Entities/ItemTypeConfig.cs` | Item type display config (names, sprites, categories) |
| `RTUB.Core/Entities/LeaderboardComment.cs` | `MyTuno.Core/Entities/LeaderboardComment.cs` | Comments on player leaderboard profiles |
| `RTUB.Core/Entities/LeaderboardCommentLike.cs` | `MyTuno.Core/Entities/LeaderboardCommentLike.cs` | Likes on leaderboard comments |
| `RTUB.Core/Entities/Transaction.cs` | `MyTuno.Core/Entities/Transaction.cs` | Fidelis currency transaction history |
| `RTUB.Core/Entities/Trophy.cs` | `MyTuno.Core/Entities/Trophy.cs` | Achievement/trophy system |

### 4.2 — ApplicationUser (Simplified)

Create a **new simplified** `ApplicationUser` that extends `IdentityUser` with only game-relevant fields:

```csharp
using Microsoft.AspNetCore.Identity;

namespace MyTuno.Core.Entities;

public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Nickname { get; set; }
    public string? ProfilePictureSrc { get; set; }

    // Game currency
    public decimal FidelisBalance { get; set; } = 100m;

    // Daily reward tracking
    public DateTime? LastDailyRewardClaim { get; set; }

    // Navigation properties
    public virtual Character? Character { get; set; }
    public virtual ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();
    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
```

> **Key difference from RTUB**: No `YearTuno`, `MonthTuno`, `Categories`, `Positions` fields — those are RTUB-org-specific. The standalone app starts users with a default Fidelis balance.

### 4.3 — Enums to Port (1:1 copy)

| Source File | Description |
|---|---|
| `RTUB.Core/Enums/BattleOutcome.cs` | `AttackerWon`, `DefenderWon`, `Draw` |
| `RTUB.Core/Enums/EnemyType.cs` | `Normal`, `Boss` |
| `RTUB.Core/Enums/EquipmentSlot.cs` | `Head`, `Shoulders`, `Chest`, `Gloves`, `Legs`, `Boots` |
| `RTUB.Core/Enums/InventoryItemType.cs` | 30+ item types: consumables, drinks, instrument parts, equipment pieces |
| `RTUB.Core/Enums/StatType.cs` | `HP`, `Power`, `Speed`, `CriticalChance`, `Defense` |
| `RTUB.Core/Enums/WeaponType.cs` | 11 weapon types (swords, axes, bows, etc.) |

### 4.4 — Configuration (Static Scaling Constants)

Port `RTUB.Core/Configuration/MyTunoScaling.cs` → `MyTuno.Core/Configuration/MyTunoScaling.cs`

This is a **static class** with all game balance constants (base stats, level scaling, upgrade multipliers, combat formula constants). It has a `Configure()` method called at startup to load values from config.

### 4.5 — Helpers to Port

| Source File | Description |
|---|---|
| `RTUB.Core/Helpers/WeaponTypeHelper.cs` | `IsTwoHanded()`, display name mapping for weapon types |
| `RTUB.Core/Helpers/EquipmentDropHelper.cs` | Slot display names, `EquipmentSlot` ↔ `InventoryItemType` conversion |

---

## 5. Phase 3 — Application Layer (Services)

Port from `RTUB.Application` → `MyTuno.Application`. Rename namespaces.

### 5.1 — Configuration Model

Port `RTUB.Application/Configuration/MyTunoScalingConfiguration.cs` → `MyTuno.Application/Configuration/MyTunoScalingConfiguration.cs`

This is a **large file (~700+ lines)** containing the full configuration model that maps to `scaling.config.json`:

- `MyTunoScalingConfiguration` (root) — version, description, base stats, level scaling, upgrades, combat, matchmaking, daily reward, gathering, stage mode, boss mode, survive mode, etc.
- Nested classes: `MyTunoBaseStats`, `MyTunoLevelScaling`, `MyTunoUpgrades`, `MyTunoUpgradeStat`, `StageModeConfig`, `EnemyScalingConfig`, `BiomeConfig`, `EncounterRulesConfig`, `GatheringConfig`, `BossModeConfig`, `SurviveModeConfig`, `ForgingConfig`, `EquipmentStatsConfig`, `DiscardValuesConfig`, plus many more.

### 5.2 — Interfaces to Port

| Interface | Methods (key ones) |
|---|---|
| `ICharacterService` | `GetByUserIdAsync`, `CreateCharacterAsync`, `GetOrCreateAsync`, all stat/buff management |
| `ICharacterRepository` | CRUD + `GetByUserIdAsync`, `GetAllWithUsersAsync` |
| `IBattleService` | `CreateBattleVsOpponentAsync`, `FinalizeAndApplyRewardsAsync` |
| `ICombatEngine` | `SimulateCombat` — deterministic turn-based combat simulation |
| `IStageService` | `StartStageAsync`, `CompleteStageAsync`, `GetProgressAsync` |
| `IStageProgressRepository` | CRUD for stage progress |
| `IStageBiomeService` | `GetBiomeForStage`, sprite path resolution |
| `IStageEnemyRepository` | Custom enemy CRUD |
| `IStageEnemyManagementService` | Admin enemy management |
| `IBossModeService` | `StartBossBattleAsync`, `CompleteBossAsync` |
| `IBossModeProgressRepository` | Boss mode progress CRUD |
| `ISurviveModeService` | `StartSurviveAsync`, `CompleteWaveAsync` |
| `ISurviveModeProgressRepository` | Survive mode progress CRUD |
| `IInventoryService` | `UseFinoAsync`, `UseShotAsync`, `GetInventoryAsync`, `AddItemAsync`, `RemoveItemAsync`, `UseCanecaAsync`, `UseCigarroAsync`, `UseCanhaoAsync`, `UsePenaltyAsync` |
| `IInventoryRepository` | Item CRUD by user + type |
| `IUpgradeService` | `PurchaseUpgradeAsync` (stat + cost calculation with max enforcement) |
| `IRankingService` | Leaderboard queries (by arena wins, level, stage) |
| `ITransactionService` | `AddTransactionAsync`, `GetHistoryAsync`, Fidelis balance management |
| `ITransactionRepository` | Transaction CRUD |
| `ILeaderboardCommentService` | `AddCommentAsync`, `GetCommentsAsync`, `ToggleLikeAsync` |
| `ILeaderboardCommentRepository` | Comment/like CRUD |
| `IForgeComboConfigRepository` | Forge recipe CRUD |
| `IItemTypeConfigRepository` | Item config CRUD |
| `IItemTypeConfigService` | `GetAllAsync`, `GetByKeyAsync` |
| `ITrophyRepository` | Trophy CRUD |
| `ITrophyService` | Trophy management |
| `IUserProfileRepository` | User profile queries for leaderboard display |
| `IRepository<T>` | Generic base repository pattern |

### 5.3 — Services to Port

| Service | Lines (approx) | Description |
|---|---|---|
| `BattleService.cs` | ~300 | Arena PvP battle orchestration: matchmaking, reward calculation, cooldown enforcement |
| `DeterministicCombatEngine.cs` | ~400 | Turn-based combat simulation. Produces replay JSON for animation. Handles speed-based turns, crits, defense mitigation, buffs |
| `CharacterService.cs` | ~250 | Character CRUD, daily reward claim, character reset |
| `StageService.cs` | ~400 | Stage progression, enemy stat generation, loot drops, XP/Fidelis rewards |
| `StageBiomeService.cs` | ~100 | Biome lookup by stage number, sprite path resolution |
| `StageEnemyManagementService.cs` | ~150 | Admin stage enemy configuration |
| `BossModeService.cs` | ~300 | Boss mode battle flow, reward calculation |
| `SurviveModeService.cs` | ~300 | Survive mode wave progression, scaling |
| `InventoryService.cs` | ~400 | Item usage (healing, buffs), quantity management, gather/craft operations |
| `UpgradeService.cs` | ~200 | Stat upgrade purchase with escalating Fidelis cost, max level enforcement |
| `RankingService.cs` | ~150 | Leaderboard queries and sorting |
| `TransactionService.cs` | ~100 | Fidelis transaction logging |
| `TransactionFilterService.cs` | ~80 | Transaction history filtering |
| `LeaderboardCommentService.cs` | ~150 | Leaderboard comment CRUD with likes |
| `ItemTypeConfigService.cs` | ~100 | Item configuration management |
| `ItemTypeConfigInitializer.cs` | ~200 | Seeds default item type configurations on startup |
| `TrophyService.cs` | ~100 | Trophy/achievement management |

### 5.4 — DTOs to Port

| DTO | Description |
|---|---|
| `BattleResult.cs` | Arena battle result with replay data, rewards, buff states |
| `StageBattleResult.cs` | Stage battle result with loot drops, stage progression |
| `BossModeBattleResult.cs` | Boss mode battle result |
| `SurviveModeLevelResult.cs` | Survive mode wave result |
| `CombatResult.cs` | Raw combat engine output |
| `MyTunoLeaderboardEntry.cs` | Leaderboard display data (character, wins, stage, level) |
| `UpgradeResult.cs` | Upgrade purchase result |
| `ItemTypeConfigDto.cs` | Item config display data |
| `LeaderboardCommentDto.cs` | Comment display data |
| `TransactionHistoryEntryDto.cs` | Transaction history display data |

---

## 6. Phase 4 — Infrastructure Layer (EF Core + SQLite)

### 6.1 — DbContext

```csharp
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MyTuno.Core.Entities;

namespace MyTuno.Infrastructure.Data;

public class MyTunoDbContext : IdentityDbContext<ApplicationUser>
{
    public MyTunoDbContext(DbContextOptions<MyTunoDbContext> options) : base(options) { }

    public DbSet<Character> Characters => Set<Character>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<StageProgress> StageProgresses => Set<StageProgress>();
    public DbSet<SurviveModeProgress> SurviveModeProgresses => Set<SurviveModeProgress>();
    public DbSet<BossModeProgress> BossModeProgresses => Set<BossModeProgress>();
    public DbSet<StageEnemy> StageEnemies => Set<StageEnemy>();
    public DbSet<ForgedWeapon> ForgedWeapons => Set<ForgedWeapon>();
    public DbSet<ForgeComboConfig> ForgeComboConfigs => Set<ForgeComboConfig>();
    public DbSet<ItemTypeConfig> ItemTypeConfigs => Set<ItemTypeConfig>();
    public DbSet<LeaderboardComment> LeaderboardComments => Set<LeaderboardComment>();
    public DbSet<LeaderboardCommentLike> LeaderboardCommentLikes => Set<LeaderboardCommentLike>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Trophy> Trophies => Set<Trophy>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Character - one per user
        builder.Entity<Character>(e =>
        {
            e.HasIndex(c => c.UserId).IsUnique();
            e.Property(c => c.Fidelis).HasColumnType("decimal(18,2)");
        });

        // InventoryItem - unique per user+type
        builder.Entity<InventoryItem>(e =>
        {
            e.HasIndex(i => new { i.UserId, i.Type }).IsUnique();
        });

        // StageProgress - one per user
        builder.Entity<StageProgress>(e =>
        {
            e.HasIndex(s => s.UserId).IsUnique();
        });

        // SurviveModeProgress - one per user
        builder.Entity<SurviveModeProgress>(e =>
        {
            e.HasIndex(s => s.UserId).IsUnique();
        });

        // BossModeProgress - one per user
        builder.Entity<BossModeProgress>(e =>
        {
            e.HasIndex(s => s.UserId).IsUnique();
        });

        // ForgedWeapon
        builder.Entity<ForgedWeapon>(e =>
        {
            e.HasIndex(w => w.CharacterId);
        });

        // LeaderboardComment
        builder.Entity<LeaderboardComment>(e =>
        {
            e.HasIndex(c => c.TargetUserId);
            e.HasMany(c => c.Likes).WithOne(l => l.Comment).HasForeignKey(l => l.CommentId);
        });

        // LeaderboardCommentLike - unique per comment+user
        builder.Entity<LeaderboardCommentLike>(e =>
        {
            e.HasIndex(l => new { l.CommentId, l.UserId }).IsUnique();
        });

        // Transaction
        builder.Entity<Transaction>(e =>
        {
            e.HasIndex(t => t.UserId);
            e.Property(t => t.Amount).HasColumnType("decimal(18,2)");
        });

        // ItemTypeConfig
        builder.Entity<ItemTypeConfig>(e =>
        {
            e.HasIndex(i => i.Key).IsUnique();
        });

        // ForgeComboConfig
        builder.Entity<ForgeComboConfig>(e =>
        {
            e.HasIndex(f => f.ComboKey).IsUnique();
        });
    }
}
```

### 6.2 — Repository Implementations

Port all repositories from `RTUB.Application/Repositories/` → `MyTuno.Infrastructure/Repositories/`. Change injection from `ApplicationDbContext` → `MyTunoDbContext`.

Key repositories to implement:

| Repository | Key Methods |
|---|---|
| `Repository<T>` | `GetByIdAsync`, `GetAllAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`, `SaveChangesAsync` |
| `CharacterRepository` | `GetByUserIdAsync`, `GetAllWithUsersAsync`, `GetByIdWithUserAsync` |
| `InventoryRepository` | `GetByUserAndTypeAsync`, `GetAllByUserAsync`, `GetOrCreateAsync` |
| `StageProgressRepository` | `GetByUserIdAsync`, `GetOrCreateAsync` |
| `SurviveModeProgressRepository` | `GetByUserIdAsync`, `GetOrCreateAsync` |
| `BossModeProgressRepository` | `GetByUserIdAsync`, `GetOrCreateAsync` |
| `StageEnemyRepository` | `GetByStageAsync`, `GetAllAsync` |
| `TransactionRepository` | `GetByUserIdAsync` with pagination + filtering |
| `LeaderboardCommentRepository` | `GetByTargetUserAsync` with includes for likes |
| `ForgeComboConfigRepository` | `GetByComboKeyAsync` |
| `ItemTypeConfigRepository` | `GetByKeyAsync`, `GetAllAsync` |
| `TrophyRepository` | `GetByUserIdAsync` |
| `UserProfileRepository` | User queries for leaderboard (minimal user info) |

### 6.3 — SQLite Connection String

In `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=mytuno.db"
  }
}
```

### 6.4 — Initial Migration

```bash
cd src/MyTuno.Infrastructure
dotnet ef migrations add InitialCreate --startup-project ../MyTuno.Web
```

---

## 7. Phase 5 — Presentation Layer (Blazor Pages & Components)

### 7.1 — Pages to Port

These are **large Razor files** from `RTUB.Web/Pages/MyTuno/`. They must be ported with namespace/injection changes.

| Source Page | Route | Lines (approx) | Description |
|---|---|---|---|
| `MyTunoHome.razor` + `.razor.css` | `/my-tuno` → `/` (home) | ~3400 | Main dashboard: character stats, modes, leaderboard, inventory, upgrades, equipment, daily reward, destilaria, forge |
| `Arena.razor` + `.razor.css` | `/my-tuno/arena` → `/arena` | ~650 | PvP arena with matchmaking, battle animation (PixiJS), rewards |
| `Stage.razor` + `.razor.css` | `/my-tuno/stages` → `/stages` | ~1100 | Stage mode progression with biome display, enemy scaling, battle animation |
| `SurviveMode.razor` + `.razor.css` | `/my-tuno/survive` → `/survive` | ~650 | Endless survival mode |
| `BossMode.razor` + `.razor.css` | `/my-tuno/boss-mode` → `/boss-mode` | ~800 | Boss encounters |
| `AllCharacters.razor` | `/my-tuno/characters` → `/characters` | ~100 | View all player characters |
| `WeaponDrinkConfig.razor` | `/my-tuno/config` → `/config` | ~80 | Weapon/drink configuration page |
| `StageEnemies.razor` + `.razor.css` | `/my-tuno/enemies` → `/enemies` | ~200 | Admin stage enemy management |

### 7.2 — Route Changes

All routes change from `/my-tuno/...` to root-level since this is now a dedicated app:

| Old Route | New Route |
|---|---|
| `/my-tuno` | `/` |
| `/my-tuno/arena` | `/arena` |
| `/my-tuno/stages` | `/stages` |
| `/my-tuno/survive` | `/survive` |
| `/my-tuno/boss-mode` | `/boss-mode` |

### 7.3 — Shared Components to Port

From `RTUB.Shared/Components/Cards/`:

| Component | Description |
|---|---|
| `CharacterCard.razor` + `.razor.css` | Displays character stats for arena matchmaking |
| `CharacterOverviewCard.razor` + `.razor.css` | Character overview with detailed stats |
| `EnemyCard.razor` + `.razor.css` | Enemy card with HP/XP bars for battle display |
| `StageEnemyCard.razor` + `.razor.css` | Stage enemy management card |

### 7.4 — Layout Changes

Create a new `MainLayout.razor` with navigation specific to MyTuno:

```razor
@inherits LayoutComponentBase

<div class="page">
    <nav class="navbar navbar-expand-lg navbar-dark bg-dark">
        <div class="container-fluid">
            <a class="navbar-brand" href="/">
                <img src="/sprites/games/my-tuno/default_tuno.png" height="30" alt="MyTuno" />
                My Tuno
            </a>
            <NavMenu />
        </div>
    </nav>
    <main class="container-fluid mt-3">
        @Body
    </main>
</div>
```

Nav items:
- Home (Dashboard)
- Arena
- Stages
- Boss Mode
- Survive
- Leaderboard
- Profile

### 7.5 — Injection Changes in Pages

All pages inject services. Change injections from RTUB namespaces:

```razor
@* BEFORE (RTUB) *@
@inject ICharacterService CharacterService
@inject IBattleService BattleService
@inject IOptions<MyTunoScalingConfiguration> ScalingConfig
@inject AuthenticationStateProvider AuthState

@* AFTER (MyTuno) — same pattern, just ensure namespaces resolve *@
@inject ICharacterService CharacterService
@inject IBattleService BattleService
@inject IOptions<MyTunoScalingConfiguration> ScalingConfig
@inject AuthenticationStateProvider AuthState
```

The actual service interfaces are identical; only the namespace and the user resolution logic change.

### 7.6 — User Resolution

In RTUB, pages get the current user via `AuthenticationStateProvider` + `UserManager`. The same pattern applies with Identity in the standalone app. Create a helper:

```csharp
public static class UserHelper
{
    public static async Task<string?> GetUserIdAsync(AuthenticationStateProvider authState)
    {
        var state = await authState.GetAuthenticationStateAsync();
        return state.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }
}
```

---

## 8. Phase 6 — Static Assets (Sprites, JS, CSS, Audio)

### 8.1 — Sprites Directory

Copy the entire `RTUB.Web/wwwroot/sprites/games/my-tuno/` directory (236+ files) to `MyTuno.Web/wwwroot/sprites/games/my-tuno/`.

Structure:
```
sprites/games/my-tuno/
├── default_tuno.png              (player sprite)
├── tuno_attacking_left.png       (battle animation)
├── tuno_attacking_right.png      (battle animation)
├── backgrounds/
│   ├── arena.png
│   ├── forest.png
│   ├── swamp.png
│   ├── mountains.png
│   ├── snowy.png
│   ├── tropical.png
│   ├── caverns.png
│   ├── desert.png
│   ├── volcanic.png
│   ├── ruins.png
│   ├── dark.png
│   ├── void.png
│   └── jeans.png
└── enemies/
    ├── forest/       (wolf, bosses 1-10)
    ├── swamp/        (frog, bosses)
    ├── mountains/    (goat, bosses)
    ├── snowy/        (penguin, bosses)
    ├── tropical/     (parrot, bosses)
    ├── caverns/      (bat, bosses)
    ├── desert/       (beetle, bosses)
    ├── volcanic/     (beetle, bosses)
    ├── ruins/        (skeleton, bosses)
    ├── dark/         (undead, bosses)
    ├── void/         (wraith, bosses)
    └── jeans/        (boss mode enemies)
```

### 8.2 — JavaScript Files

Copy and adapt these PixiJS-based battle animation files:

| File | Lines (approx) | Description |
|---|---|---|
| `pixiBattle.js` | ~1230 | Arena PvP battle animation engine. Exposes `window.myTunoGame` global |
| `pixiStageBattle.js` | ~800 | Stage mode battle animation |
| `pixiSurviveMode.js` | ~2070 | Survive mode game engine |

These files use **PixiJS** for 2D rendering. Include PixiJS via CDN in the layout:
```html
<script src="https://cdnjs.cloudflare.com/ajax/libs/pixi.js/7.3.3/pixi.min.js"></script>
```

### 8.3 — CSS Files

Port:
- `RTUB.Web/wwwroot/css/4-pages/my-tuno.css` → `MyTuno.Web/wwwroot/css/my-tuno.css`
- All `.razor.css` scoped styles come with their respective pages

### 8.4 — Audio Files

Audio files referenced in `pixiSurviveMode.js`:
```
/audio/games/my-tuno/survive/win.mp3
/audio/games/my-tuno/survive/death.mp3
/audio/games/my-tuno/survive/hit.mp3
```

Ensure these are created or sourced at `MyTuno.Web/wwwroot/audio/games/my-tuno/survive/`.

---

## 9. Phase 7 — Configuration & Scaling System

### 9.1 — scaling.config.json

Copy `RTUB.Web/scaling.config.json` → `MyTuno.Web/scaling.config.json`. The entire file is scoped under the `"myTuno"` key and maps to `MyTunoScalingConfiguration`.

Key sections (see [Appendix A](#appendix-a--scaling-config-json) for full reference):

```json
{
  "myTuno": {
    "version": "2.0.0",
    "description": "...",
    "baseStats": { "level": 1, "xp": 0, "hp": 100, "power": 10, "speed": 10, "defense": 5, "criticalChance": 0.0 },
    "levelScaling": { "maxLevel": 1000, "statMultiplierPerLevel": 0.15, "statGrowthExponent": 0.20, "xpPerLevelBase": 50 },
    "upgrades": { ... },
    "combat": { "defenseK": 50, "minDamage": 1, "criticalChanceCap": 0.5, "shotBuffMultiplier": 1.20 },
    "matchmaking": { ... },
    "battleRewards": { ... },
    "dailyReward": { ... },
    "gathering": { ... },
    "stageMode": { "biomes": [...], "encounterRules": {...}, "enemyScaling": {...}, "dropRates": {...}, ... },
    "bossMode": { ... },
    "surviveMode": { ... }
  }
}
```

### 9.2 — Configuration Registration in Program.cs

```csharp
// Load scaling config
builder.Configuration.AddJsonFile("scaling.config.json", optional: false, reloadOnChange: true);

// Bind to options
builder.Services.Configure<MyTunoScalingConfiguration>(
    builder.Configuration.GetSection(MyTunoScalingConfiguration.SectionName));

// Initialize static scaling constants
var scalingConfig = builder.Configuration
    .GetSection(MyTunoScalingConfiguration.SectionName)
    .Get<MyTunoScalingConfiguration>();

if (scalingConfig != null)
{
    MyTunoScaling.Configure(
        scalingConfig.BaseStats.Level,
        scalingConfig.BaseStats.XP,
        scalingConfig.BaseStats.HP,
        scalingConfig.BaseStats.Power,
        scalingConfig.BaseStats.Speed,
        scalingConfig.BaseStats.Defense,
        scalingConfig.BaseStats.CriticalChance,
        scalingConfig.LevelScaling.MaxLevel,
        scalingConfig.LevelScaling.StatMultiplierPerLevel,
        scalingConfig.LevelScaling.StatGrowthExponent,
        scalingConfig.LevelScaling.XpPerLevelBase,
        scalingConfig.Upgrades.HP.MultiplierPerUpgrade,
        scalingConfig.Upgrades.Power.MultiplierPerUpgrade,
        scalingConfig.Upgrades.Speed.MultiplierPerUpgrade,
        scalingConfig.Upgrades.CriticalChance.MultiplierPerUpgrade,
        scalingConfig.Upgrades.Defense.MultiplierPerUpgrade,
        scalingConfig.Combat.DefenseK,
        scalingConfig.Combat.MinDamage,
        scalingConfig.Combat.ShotBuffMultiplier);
}
```

---

## 10. Phase 8 — Authentication & User System

### 10.1 — Identity Setup

```csharp
// In Program.cs
builder.Services.AddDbContext<MyTunoDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<MyTunoDbContext>();
```

### 10.2 — Auth Pages

Create minimal login/register pages:
- `/Account/Login` — Email + Password login
- `/Account/Register` — Registration with Nickname field
- `/Account/Logout`

Or use scaffolded Identity UI: `dotnet aspnet-codegenerator identity --dbContext MyTunoDbContext`.

### 10.3 — Auto-Create Character on Login

When a user logs in for the first time, create their character automatically:

```csharp
// In a middleware or on the home page OnInitializedAsync:
var character = await CharacterService.GetByUserIdAsync(userId);
if (character == null)
{
    character = await CharacterService.CreateCharacterAsync(userId);
}
```

---

## 11. Phase 9 — Tests

### 11.1 — Core Entity Tests to Port

From `RTUB.Core.Tests/Entities/`:

| Test File | Tests |
|---|---|
| `CharacterTests.cs` | Character creation, stat calculation, level up, buff management, equipment bonuses |
| `StageProgressTests.cs` | Stage progression logic, enemy type determination |
| `BossModeProgressTests.cs` | Boss mode create/advance |
| `ForgedWeaponTests.cs` | Weapon creation, validation |
| `InventoryItemTests.cs` | Add/remove quantity, validation |
| `LeaderboardCommentTests.cs` | Comment CRUD, validation |
| `LeaderboardCommentLikeTests.cs` | Like toggle |

### 11.2 — Application Service Tests to Port

From `RTUB.Application.Tests/Services/`:

| Test File | Tests |
|---|---|
| `BattleServiceTests.cs` | Battle creation, reward calculation, cooldown |
| `CombatEngineTests.cs` | Deterministic combat simulation, damage calculation, defense mitigation |
| `InventoryServiceTests.cs` | Item usage (Fino healing, Shot buffs), quantity management |
| `CharacterServiceTests.cs` | Character CRUD, daily rewards |
| `UpgradeServiceTests.cs` | Upgrade purchase, cost escalation, max level |
| `LeaderboardCommentServiceTests.cs` | Comment service operations |

### 11.3 — Test Infrastructure

Tests use:
- **InMemory EF Core** for repository tests
- **Moq** for service layer mocking
- **FluentAssertions** for readable assertions
- Custom test fixtures for database setup

---

## 12. Phase 10 — Final Wiring & Startup

### 12.1 — Complete Program.cs

```csharp
using Microsoft.EntityFrameworkCore;
using MyTuno.Core.Configuration;
using MyTuno.Application.Configuration;
using MyTuno.Application.Interfaces;
using MyTuno.Application.Services;
using MyTuno.Infrastructure.Data;
using MyTuno.Infrastructure.Repositories;
using MyTuno.Core.Entities;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ──
builder.Configuration.AddJsonFile("scaling.config.json", optional: false, reloadOnChange: true);

// ── Database ──
builder.Services.AddDbContext<MyTunoDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Identity ──
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<MyTunoDbContext>();

// ── Scaling Configuration ──
builder.Services.Configure<MyTunoScalingConfiguration>(
    builder.Configuration.GetSection(MyTunoScalingConfiguration.SectionName));

var scalingConfig = builder.Configuration
    .GetSection(MyTunoScalingConfiguration.SectionName)
    .Get<MyTunoScalingConfiguration>();

if (scalingConfig != null)
{
    MyTunoScaling.Configure(/* ... all 19 parameters from scaling config ... */);
}

// ── Repositories ──
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<ICharacterRepository, CharacterRepository>();
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<IStageProgressRepository, StageProgressRepository>();
builder.Services.AddScoped<ISurviveModeProgressRepository, SurviveModeProgressRepository>();
builder.Services.AddScoped<IBossModeProgressRepository, BossModeProgressRepository>();
builder.Services.AddScoped<IStageEnemyRepository, StageEnemyRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<ILeaderboardCommentRepository, LeaderboardCommentRepository>();
builder.Services.AddScoped<IForgeComboConfigRepository, ForgeComboConfigRepository>();
builder.Services.AddScoped<IItemTypeConfigRepository, ItemTypeConfigRepository>();
builder.Services.AddScoped<ITrophyRepository, TrophyRepository>();
builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();

// ── Services ──
builder.Services.AddScoped<ICharacterService, CharacterService>();
builder.Services.AddScoped<IBattleService, BattleService>();
builder.Services.AddScoped<ICombatEngine, DeterministicCombatEngine>();
builder.Services.AddScoped<IStageService, StageService>();
builder.Services.AddScoped<IStageBiomeService, StageBiomeService>();
builder.Services.AddScoped<IStageEnemyManagementService, StageEnemyManagementService>();
builder.Services.AddScoped<IBossModeService, BossModeService>();
builder.Services.AddScoped<ISurviveModeService, SurviveModeService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IUpgradeService, UpgradeService>();
builder.Services.AddScoped<IRankingService, RankingService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<ILeaderboardCommentService, LeaderboardCommentService>();
builder.Services.AddScoped<IItemTypeConfigService, ItemTypeConfigService>();
builder.Services.AddScoped<ITrophyService, TrophyService>();

// ── Blazor ──
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// ── Middleware ──
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// ── Auto-migrate + seed ──
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MyTunoDbContext>();
    await db.Database.MigrateAsync();

    // Seed item type configs
    var itemConfigService = scope.ServiceProvider.GetRequiredService<IItemTypeConfigService>();
    // Call initialization if needed
}

app.Run();
```

### 12.2 — appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=mytuno.db"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### 12.3 — CacheBuster Utility

The pages use a `CacheBuster.Bust()` utility for cache-busting static file URLs. Create a simple implementation:

```csharp
namespace MyTuno.Web.Components;

public static class CacheBuster
{
    private static readonly string _version = DateTime.UtcNow.Ticks.ToString();

    public static string Bust(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        var separator = path.Contains('?') ? '&' : '?';
        return $"{path}{separator}v={_version}";
    }
}
```

---

## 13. Complete File Inventory (Source → Target)

### Entities (RTUB.Core → MyTuno.Core)

| # | Source Path | Target Path |
|---|---|---|
| 1 | `src/RTUB.Core/Entities/BaseEntity.cs` | `src/MyTuno.Core/Entities/BaseEntity.cs` |
| 2 | `src/RTUB.Core/Entities/Character.cs` | `src/MyTuno.Core/Entities/Character.cs` |
| 3 | `src/RTUB.Core/Entities/InventoryItem.cs` | `src/MyTuno.Core/Entities/InventoryItem.cs` |
| 4 | `src/RTUB.Core/Entities/StageProgress.cs` | `src/MyTuno.Core/Entities/StageProgress.cs` |
| 5 | `src/RTUB.Core/Entities/SurviveModeProgress.cs` | `src/MyTuno.Core/Entities/SurviveModeProgress.cs` |
| 6 | `src/RTUB.Core/Entities/BossModeProgress.cs` | `src/MyTuno.Core/Entities/BossModeProgress.cs` |
| 7 | `src/RTUB.Core/Entities/StageEnemy.cs` | `src/MyTuno.Core/Entities/StageEnemy.cs` |
| 8 | `src/RTUB.Core/Entities/ForgedWeapon.cs` | `src/MyTuno.Core/Entities/ForgedWeapon.cs` |
| 9 | `src/RTUB.Core/Entities/ForgeComboConfig.cs` | `src/MyTuno.Core/Entities/ForgeComboConfig.cs` |
| 10 | `src/RTUB.Core/Entities/ItemTypeConfig.cs` | `src/MyTuno.Core/Entities/ItemTypeConfig.cs` |
| 11 | `src/RTUB.Core/Entities/LeaderboardComment.cs` | `src/MyTuno.Core/Entities/LeaderboardComment.cs` |
| 12 | `src/RTUB.Core/Entities/LeaderboardCommentLike.cs` | `src/MyTuno.Core/Entities/LeaderboardCommentLike.cs` |
| 13 | `src/RTUB.Core/Entities/Transaction.cs` | `src/MyTuno.Core/Entities/Transaction.cs` |
| 14 | `src/RTUB.Core/Entities/Trophy.cs` | `src/MyTuno.Core/Entities/Trophy.cs` |
| 15 | *New* | `src/MyTuno.Core/Entities/ApplicationUser.cs` |

### Enums (RTUB.Core → MyTuno.Core)

| # | Source Path | Target Path |
|---|---|---|
| 16 | `src/RTUB.Core/Enums/BattleOutcome.cs` | `src/MyTuno.Core/Enums/BattleOutcome.cs` |
| 17 | `src/RTUB.Core/Enums/EnemyType.cs` | `src/MyTuno.Core/Enums/EnemyType.cs` |
| 18 | `src/RTUB.Core/Enums/EquipmentSlot.cs` | `src/MyTuno.Core/Enums/EquipmentSlot.cs` |
| 19 | `src/RTUB.Core/Enums/InventoryItemType.cs` | `src/MyTuno.Core/Enums/InventoryItemType.cs` |
| 20 | `src/RTUB.Core/Enums/StatType.cs` | `src/MyTuno.Core/Enums/StatType.cs` |
| 21 | `src/RTUB.Core/Enums/WeaponType.cs` | `src/MyTuno.Core/Enums/WeaponType.cs` |

### Configuration & Helpers (RTUB.Core → MyTuno.Core)

| # | Source Path | Target Path |
|---|---|---|
| 22 | `src/RTUB.Core/Configuration/MyTunoScaling.cs` | `src/MyTuno.Core/Configuration/MyTunoScaling.cs` |
| 23 | `src/RTUB.Core/Helpers/WeaponTypeHelper.cs` | `src/MyTuno.Core/Helpers/WeaponTypeHelper.cs` |
| 24 | `src/RTUB.Core/Helpers/EquipmentDropHelper.cs` | `src/MyTuno.Core/Helpers/EquipmentDropHelper.cs` |

### Application Configuration (RTUB.Application → MyTuno.Application)

| # | Source Path | Target Path |
|---|---|---|
| 25 | `src/RTUB.Application/Configuration/MyTunoScalingConfiguration.cs` | `src/MyTuno.Application/Configuration/MyTunoScalingConfiguration.cs` |

### DTOs (RTUB.Application → MyTuno.Application)

| # | Source Path | Target Path |
|---|---|---|
| 26 | `src/RTUB.Application/DTOs/BattleResult.cs` | `src/MyTuno.Application/DTOs/BattleResult.cs` |
| 27 | `src/RTUB.Application/DTOs/StageBattleResult.cs` | `src/MyTuno.Application/DTOs/StageBattleResult.cs` |
| 28 | `src/RTUB.Application/DTOs/BossModeBattleResult.cs` | `src/MyTuno.Application/DTOs/BossModeBattleResult.cs` |
| 29 | `src/RTUB.Application/DTOs/SurviveModeLevelResult.cs` | `src/MyTuno.Application/DTOs/SurviveModeLevelResult.cs` |
| 30 | `src/RTUB.Application/DTOs/CombatResult.cs` | `src/MyTuno.Application/DTOs/CombatResult.cs` |
| 31 | `src/RTUB.Application/DTOs/MyTunoLeaderboardEntry.cs` | `src/MyTuno.Application/DTOs/MyTunoLeaderboardEntry.cs` |
| 32 | `src/RTUB.Application/DTOs/UpgradeResult.cs` | `src/MyTuno.Application/DTOs/UpgradeResult.cs` |
| 33 | `src/RTUB.Application/DTOs/ItemTypeConfigDto.cs` | `src/MyTuno.Application/DTOs/ItemTypeConfigDto.cs` |
| 34 | `src/RTUB.Application/DTOs/LeaderboardCommentDto.cs` | `src/MyTuno.Application/DTOs/LeaderboardCommentDto.cs` |
| 35 | `src/RTUB.Application/DTOs/TransactionHistoryEntryDto.cs` | `src/MyTuno.Application/DTOs/TransactionHistoryEntryDto.cs` |

### Interfaces (RTUB.Application → MyTuno.Application)

| # | Source Path | Target Path |
|---|---|---|
| 36 | `src/RTUB.Application/Interfaces/IRepository.cs` | `src/MyTuno.Application/Interfaces/IRepository.cs` |
| 37 | `src/RTUB.Application/Interfaces/ICharacterRepository.cs` | `src/MyTuno.Application/Interfaces/ICharacterRepository.cs` |
| 38 | `src/RTUB.Application/Interfaces/ICharacterService.cs` | `src/MyTuno.Application/Interfaces/ICharacterService.cs` |
| 39 | `src/RTUB.Application/Interfaces/IBattleService.cs` | `src/MyTuno.Application/Interfaces/IBattleService.cs` |
| 40 | `src/RTUB.Application/Interfaces/ICombatEngine.cs` | `src/MyTuno.Application/Interfaces/ICombatEngine.cs` |
| 41 | `src/RTUB.Application/Interfaces/IStageService.cs` | `src/MyTuno.Application/Interfaces/IStageService.cs` |
| 42 | `src/RTUB.Application/Interfaces/IStageBiomeService.cs` | `src/MyTuno.Application/Interfaces/IStageBiomeService.cs` |
| 43 | `src/RTUB.Application/Interfaces/IStageProgressRepository.cs` | `src/MyTuno.Application/Interfaces/IStageProgressRepository.cs` |
| 44 | `src/RTUB.Application/Interfaces/IStageEnemyRepository.cs` | `src/MyTuno.Application/Interfaces/IStageEnemyRepository.cs` |
| 45 | `src/RTUB.Application/Interfaces/IStageEnemyManagementService.cs` | `src/MyTuno.Application/Interfaces/IStageEnemyManagementService.cs` |
| 46 | `src/RTUB.Application/Interfaces/IBossModeService.cs` | `src/MyTuno.Application/Interfaces/IBossModeService.cs` |
| 47 | `src/RTUB.Application/Interfaces/IBossModeProgressRepository.cs` | `src/MyTuno.Application/Interfaces/IBossModeProgressRepository.cs` |
| 48 | `src/RTUB.Application/Interfaces/ISurviveModeService.cs` | `src/MyTuno.Application/Interfaces/ISurviveModeService.cs` |
| 49 | `src/RTUB.Application/Interfaces/ISurviveModeProgressRepository.cs` | `src/MyTuno.Application/Interfaces/ISurviveModeProgressRepository.cs` |
| 50 | `src/RTUB.Application/Interfaces/IInventoryService.cs` | `src/MyTuno.Application/Interfaces/IInventoryService.cs` |
| 51 | `src/RTUB.Application/Interfaces/IInventoryRepository.cs` | `src/MyTuno.Application/Interfaces/IInventoryRepository.cs` |
| 52 | `src/RTUB.Application/Interfaces/IUpgradeService.cs` | `src/MyTuno.Application/Interfaces/IUpgradeService.cs` |
| 53 | `src/RTUB.Application/Interfaces/IRankingService.cs` | `src/MyTuno.Application/Interfaces/IRankingService.cs` |
| 54 | `src/RTUB.Application/Interfaces/ITransactionService.cs` | `src/MyTuno.Application/Interfaces/ITransactionService.cs` |
| 55 | `src/RTUB.Application/Interfaces/ITransactionRepository.cs` | `src/MyTuno.Application/Interfaces/ITransactionRepository.cs` |
| 56 | `src/RTUB.Application/Interfaces/ILeaderboardCommentService.cs` | `src/MyTuno.Application/Interfaces/ILeaderboardCommentService.cs` |
| 57 | `src/RTUB.Application/Interfaces/ILeaderboardCommentRepository.cs` | `src/MyTuno.Application/Interfaces/ILeaderboardCommentRepository.cs` |
| 58 | `src/RTUB.Application/Interfaces/IForgeComboConfigRepository.cs` | `src/MyTuno.Application/Interfaces/IForgeComboConfigRepository.cs` |
| 59 | `src/RTUB.Application/Interfaces/IItemTypeConfigRepository.cs` | `src/MyTuno.Application/Interfaces/IItemTypeConfigRepository.cs` |
| 60 | `src/RTUB.Application/Interfaces/IItemTypeConfigService.cs` | `src/MyTuno.Application/Interfaces/IItemTypeConfigService.cs` |
| 61 | `src/RTUB.Application/Interfaces/ITrophyRepository.cs` | `src/MyTuno.Application/Interfaces/ITrophyRepository.cs` |
| 62 | `src/RTUB.Application/Interfaces/ITrophyService.cs` | `src/MyTuno.Application/Interfaces/ITrophyService.cs` |
| 63 | `src/RTUB.Application/Interfaces/IUserProfileRepository.cs` | `src/MyTuno.Application/Interfaces/IUserProfileRepository.cs` |

### Services (RTUB.Application → MyTuno.Application)

| # | Source Path | Target Path |
|---|---|---|
| 64 | `src/RTUB.Application/Services/BattleService.cs` | `src/MyTuno.Application/Services/BattleService.cs` |
| 65 | `src/RTUB.Application/Services/DeterministicCombatEngine.cs` | `src/MyTuno.Application/Services/DeterministicCombatEngine.cs` |
| 66 | `src/RTUB.Application/Services/CharacterService.cs` | `src/MyTuno.Application/Services/CharacterService.cs` |
| 67 | `src/RTUB.Application/Services/StageService.cs` | `src/MyTuno.Application/Services/StageService.cs` |
| 68 | `src/RTUB.Application/Services/StageBiomeService.cs` | `src/MyTuno.Application/Services/StageBiomeService.cs` |
| 69 | `src/RTUB.Application/Services/StageEnemyManagementService.cs` | `src/MyTuno.Application/Services/StageEnemyManagementService.cs` |
| 70 | `src/RTUB.Application/Services/BossModeService.cs` | `src/MyTuno.Application/Services/BossModeService.cs` |
| 71 | `src/RTUB.Application/Services/SurviveModeService.cs` | `src/MyTuno.Application/Services/SurviveModeService.cs` |
| 72 | `src/RTUB.Application/Services/InventoryService.cs` | `src/MyTuno.Application/Services/InventoryService.cs` |
| 73 | `src/RTUB.Application/Services/UpgradeService.cs` | `src/MyTuno.Application/Services/UpgradeService.cs` |
| 74 | `src/RTUB.Application/Services/RankingService.cs` | `src/MyTuno.Application/Services/RankingService.cs` |
| 75 | `src/RTUB.Application/Services/TransactionService.cs` | `src/MyTuno.Application/Services/TransactionService.cs` |
| 76 | `src/RTUB.Application/Services/TransactionFilterService.cs` | `src/MyTuno.Application/Services/TransactionFilterService.cs` |
| 77 | `src/RTUB.Application/Services/LeaderboardCommentService.cs` | `src/MyTuno.Application/Services/LeaderboardCommentService.cs` |
| 78 | `src/RTUB.Application/Services/ItemTypeConfigService.cs` | `src/MyTuno.Application/Services/ItemTypeConfigService.cs` |
| 79 | `src/RTUB.Application/Services/ItemTypeConfigInitializer.cs` | `src/MyTuno.Application/Services/ItemTypeConfigInitializer.cs` |
| 80 | `src/RTUB.Application/Services/TrophyService.cs` | `src/MyTuno.Application/Services/TrophyService.cs` |

### Repositories (RTUB.Application → MyTuno.Infrastructure)

| # | Source Path | Target Path |
|---|---|---|
| 81 | `src/RTUB.Application/Repositories/Repository.cs` | `src/MyTuno.Infrastructure/Repositories/Repository.cs` |
| 82 | `src/RTUB.Application/Repositories/CharacterRepository.cs` | `src/MyTuno.Infrastructure/Repositories/CharacterRepository.cs` |
| 83 | `src/RTUB.Application/Repositories/InventoryRepository.cs` | `src/MyTuno.Infrastructure/Repositories/InventoryRepository.cs` |
| 84 | `src/RTUB.Application/Repositories/StageProgressRepository.cs` | `src/MyTuno.Infrastructure/Repositories/StageProgressRepository.cs` |
| 85 | `src/RTUB.Application/Repositories/SurviveModeProgressRepository.cs` | `src/MyTuno.Infrastructure/Repositories/SurviveModeProgressRepository.cs` |
| 86 | `src/RTUB.Application/Repositories/BossModeProgressRepository.cs` | `src/MyTuno.Infrastructure/Repositories/BossModeProgressRepository.cs` |
| 87 | `src/RTUB.Application/Repositories/StageEnemyRepository.cs` | `src/MyTuno.Infrastructure/Repositories/StageEnemyRepository.cs` |
| 88 | `src/RTUB.Application/Repositories/TransactionRepository.cs` | `src/MyTuno.Infrastructure/Repositories/TransactionRepository.cs` |
| 89 | `src/RTUB.Application/Repositories/LeaderboardCommentRepository.cs` | `src/MyTuno.Infrastructure/Repositories/LeaderboardCommentRepository.cs` |
| 90 | `src/RTUB.Application/Repositories/ForgeComboConfigRepository.cs` | `src/MyTuno.Infrastructure/Repositories/ForgeComboConfigRepository.cs` |
| 91 | `src/RTUB.Application/Repositories/ItemTypeConfigRepository.cs` | `src/MyTuno.Infrastructure/Repositories/ItemTypeConfigRepository.cs` |
| 92 | `src/RTUB.Application/Repositories/TrophyRepository.cs` | `src/MyTuno.Infrastructure/Repositories/TrophyRepository.cs` |
| 93 | `src/RTUB.Application/Repositories/UserProfileRepository.cs` | `src/MyTuno.Infrastructure/Repositories/UserProfileRepository.cs` |

### Pages (RTUB.Web → MyTuno.Web)

| # | Source Path | Target Path |
|---|---|---|
| 94 | `src/RTUB.Web/Pages/MyTuno/MyTunoHome.razor` | `src/MyTuno.Web/Pages/MyTunoHome.razor` |
| 95 | `src/RTUB.Web/Pages/MyTuno/MyTunoHome.razor.css` | `src/MyTuno.Web/Pages/MyTunoHome.razor.css` |
| 96 | `src/RTUB.Web/Pages/MyTuno/Arena.razor` | `src/MyTuno.Web/Pages/Arena.razor` |
| 97 | `src/RTUB.Web/Pages/MyTuno/Arena.razor.css` | `src/MyTuno.Web/Pages/Arena.razor.css` |
| 98 | `src/RTUB.Web/Pages/MyTuno/Stage.razor` | `src/MyTuno.Web/Pages/Stage.razor` |
| 99 | `src/RTUB.Web/Pages/MyTuno/Stage.razor.css` | `src/MyTuno.Web/Pages/Stage.razor.css` |
| 100 | `src/RTUB.Web/Pages/MyTuno/SurviveMode.razor` | `src/MyTuno.Web/Pages/SurviveMode.razor` |
| 101 | `src/RTUB.Web/Pages/MyTuno/SurviveMode.razor.css` | `src/MyTuno.Web/Pages/SurviveMode.razor.css` |
| 102 | `src/RTUB.Web/Pages/MyTuno/BossMode.razor` | `src/MyTuno.Web/Pages/BossMode.razor` |
| 103 | `src/RTUB.Web/Pages/MyTuno/BossMode.razor.css` | `src/MyTuno.Web/Pages/BossMode.razor.css` |
| 104 | `src/RTUB.Web/Pages/MyTuno/AllCharacters.razor` | `src/MyTuno.Web/Pages/AllCharacters.razor` |
| 105 | `src/RTUB.Web/Pages/MyTuno/WeaponDrinkConfig.razor` | `src/MyTuno.Web/Pages/WeaponDrinkConfig.razor` |
| 106 | `src/RTUB.Web/Pages/MyTuno/StageEnemies.razor` | `src/MyTuno.Web/Pages/StageEnemies.razor` |
| 107 | `src/RTUB.Web/Pages/MyTuno/StageEnemies.razor.css` | `src/MyTuno.Web/Pages/StageEnemies.razor.css` |

### Shared Components (RTUB.Shared → MyTuno.Web)

| # | Source Path | Target Path |
|---|---|---|
| 108 | `src/RTUB.Shared/Components/Cards/CharacterCard.razor` | `src/MyTuno.Web/Components/Cards/CharacterCard.razor` |
| 109 | `src/RTUB.Shared/Components/Cards/CharacterCard.razor.css` | `src/MyTuno.Web/Components/Cards/CharacterCard.razor.css` |
| 110 | `src/RTUB.Shared/Components/Cards/CharacterOverviewCard.razor` | `src/MyTuno.Web/Components/Cards/CharacterOverviewCard.razor` |
| 111 | `src/RTUB.Shared/Components/Cards/CharacterOverviewCard.razor.css` | `src/MyTuno.Web/Components/Cards/CharacterOverviewCard.razor.css` |
| 112 | `src/RTUB.Shared/Components/Cards/EnemyCard.razor` | `src/MyTuno.Web/Components/Cards/EnemyCard.razor` |
| 113 | `src/RTUB.Shared/Components/Cards/EnemyCard.razor.css` | `src/MyTuno.Web/Components/Cards/EnemyCard.razor.css` |
| 114 | `src/RTUB.Shared/Components/Cards/StageEnemyCard.razor` | `src/MyTuno.Web/Components/Cards/StageEnemyCard.razor` |
| 115 | `src/RTUB.Shared/Components/Cards/StageEnemyCard.razor.css` | `src/MyTuno.Web/Components/Cards/StageEnemyCard.razor.css` |

### JavaScript (RTUB.Web → MyTuno.Web)

| # | Source Path | Target Path |
|---|---|---|
| 116 | `src/RTUB.Web/wwwroot/js/pixiBattle.js` | `src/MyTuno.Web/wwwroot/js/pixiBattle.js` |
| 117 | `src/RTUB.Web/wwwroot/js/pixiStageBattle.js` | `src/MyTuno.Web/wwwroot/js/pixiStageBattle.js` |
| 118 | `src/RTUB.Web/wwwroot/js/pixiSurviveMode.js` | `src/MyTuno.Web/wwwroot/js/pixiSurviveMode.js` |

### CSS (RTUB.Web → MyTuno.Web)

| # | Source Path | Target Path |
|---|---|---|
| 119 | `src/RTUB.Web/wwwroot/css/4-pages/my-tuno.css` | `src/MyTuno.Web/wwwroot/css/my-tuno.css` |

### Config

| # | Source Path | Target Path |
|---|---|---|
| 120 | `src/RTUB.Web/scaling.config.json` | `src/MyTuno.Web/scaling.config.json` |

### Sprites (bulk copy)

| # | Source Path | Target Path |
|---|---|---|
| 121 | `src/RTUB.Web/wwwroot/sprites/games/my-tuno/` (236+ files) | `src/MyTuno.Web/wwwroot/sprites/games/my-tuno/` |

### Tests

| # | Source Path | Target Path |
|---|---|---|
| 122-128 | `tests/RTUB.Core.Tests/Entities/Character*.cs`, etc. | `tests/MyTuno.Core.Tests/Entities/` |
| 129-135 | `tests/RTUB.Application.Tests/Services/Battle*.cs`, etc. | `tests/MyTuno.Application.Tests/Services/` |

**Total: ~135+ files to port** (excluding 236 sprite files which are bulk-copied).

---

## 14. Database Schema

### Entity Relationship Diagram

```
ApplicationUser (Identity)
├── 1:1 Character
│   ├── Stats: Level, XP, HP, Power, Speed, Defense, CriticalChance
│   ├── Upgrades: HpUpgrades, PowerUpgrades, SpeedUpgrades, DefenseUpgrades, CriticalUpgrades
│   ├── Equipment: EquippedHead, EquippedShoulders, EquippedChest, EquippedGloves, EquippedLegs, EquippedBoots
│   ├── Buffs: ShotBuffBattlesRemaining, CigarroShieldHitsRemaining, CanhaoDamageBoostHitsRemaining, PenaltyBuffActive
│   ├── Arena: ArenaWins, ArenaLosses, ArenaDraws, LastOpponentId, LastBattleAt
│   ├── Currency: Fidelis (decimal)
│   └── 1:N ForgedWeapon
├── 1:N InventoryItem (UserId + Type unique)
├── 1:1 StageProgress
│   ├── CurrentStage, HighestStage, EnemiesDefeatedInCurrentStage
│   └── StartStage (checkpoint)
├── 1:1 SurviveModeProgress
│   ├── CurrentLevel, HighestLevel
│   └── StartLevel
├── 1:1 BossModeProgress
│   └── HighestBossDefeated, CurrentBoss
├── 1:N Transaction (Fidelis history)
├── 1:N Trophy
└── target:N LeaderboardComment
    └── 1:N LeaderboardCommentLike

StageEnemy (standalone, admin-configured)
ForgeComboConfig (standalone, recipe definitions)
ItemTypeConfig (standalone, item display config)
```

### Key Tables

| Table | Primary Key | Foreign Keys | Unique Constraints |
|---|---|---|---|
| `AspNetUsers` | `Id` (string) | — | `Email`, `UserName` |
| `Characters` | `Id` (int) | `UserId → AspNetUsers.Id` | `UserId` |
| `InventoryItems` | `Id` (int) | `UserId → AspNetUsers.Id` | `(UserId, Type)` |
| `StageProgresses` | `Id` (int) | `UserId → AspNetUsers.Id` | `UserId` |
| `SurviveModeProgresses` | `Id` (int) | `UserId → AspNetUsers.Id` | `UserId` |
| `BossModeProgresses` | `Id` (int) | `UserId → AspNetUsers.Id` | `UserId` |
| `ForgedWeapons` | `Id` (int) | `CharacterId → Characters.Id` | — |
| `Transactions` | `Id` (int) | `UserId → AspNetUsers.Id` | — |
| `LeaderboardComments` | `Id` (int) | `TargetUserId`, `AuthorId → AspNetUsers.Id` | — |
| `LeaderboardCommentLikes` | `Id` (int) | `CommentId`, `UserId` | `(CommentId, UserId)` |
| `StageEnemies` | `Id` (int) | — | — |
| `ForgeComboConfigs` | `Id` (int) | — | `ComboKey` |
| `ItemTypeConfigs` | `Id` (int) | — | `Key` |
| `Trophies` | `Id` (int) | `UserId → AspNetUsers.Id` | — |

---

## 15. Key Differences from RTUB

| Aspect | RTUB (Original) | MyTuno (Standalone) |
|---|---|---|
| **Framework** | Blazor Server .NET 8 | Blazor Server .NET 10 |
| **Database** | SQL Server | SQLite |
| **Auth** | ASP.NET Identity with roles/claims for org | ASP.NET Identity (simplified, no org roles) |
| **User Model** | `ApplicationUser` with 40+ org fields | Simplified `ApplicationUser` with ~6 game fields |
| **Fidelis Source** | Earned via rehearsals, events, betting, AND game | Earned via game only (battles, stages, daily reward) |
| **Default Balance** | 10 Fidelis (org context) | 100 Fidelis (standalone, more generous start) |
| **Routes** | `/my-tuno/*` (nested under org app) | `/*` (root-level, dedicated app) |
| **Navigation** | Dropdown item in org navbar | Dedicated game navbar |
| **Repositories** | In Application layer, coupled to main DbContext | In Infrastructure layer, clean separation |
| **Sprites Path** | Under org wwwroot | Same path preserved for compatibility |
| **SignalR** | Shared MessagesHub | Not needed (no org messaging) |
| **Push Notifications** | Shared with org | Not included (can be added later) |

---

## Appendix A — Scaling Config JSON

The full `scaling.config.json` should be copied verbatim from `RTUB.Web/scaling.config.json`. It contains ~315 lines under the `"myTuno"` key with:

- `version`, `description`, `nextFeatures`
- `baseStats` (7 fields)
- `levelScaling` (4 fields)
- `upgrades` (5 stats × ~4 fields each)
- `combat` (defenseK, minDamage, criticalChanceCap, shotBuffMultiplier)
- `matchmaking` (cooldown, power ranges, weights)
- `battleRewards` (win/draw rewards, XP scaling, revive/restore costs)
- `dailyReward` (baseFidelis, perLevelFidelis)
- `gathering` (energy, resources, costs)
- `stageMode` (biomes[11], encounterRules, baseEnemyStats, enemyScaling, dropRates, fidelisRewards, forging, equipmentStats, discardValues)
- `bossMode` (enemySpritePath, backgroundPath, scaling)
- `surviveMode` (wave scaling, rewards)

---

## Appendix B — Game Mechanics Reference

### Combat Formula
```
Damage = AttackerPower × (DefenseK / (DefenseK + DefenderDefense))
       × CritMultiplier (2.0 if crit, 1.0 otherwise)
       × BuffMultiplier (1.2 if shot active)
       = max(MinDamage, result)
```

### Turn Order
Speed determines action bar fill rate. Higher speed = more frequent attacks. Turn-based with speed-weighted timing:
```
ActionTime = BaseActionTime / (1 + SpeedBonus × 0.01)
```
Minimum action time capped at 0.5s (with Penalty buff) or 1.0s normally.

### Level Up
```
XP_Required = Level × XpPerLevelBase (50)
StatScale = 1 + StatMultiplierPerLevel × (Level - 1) ^ (1 + StatGrowthExponent)
```

### Upgrade Cost
```
Cost = BaseCost × (1 + UpgradeCount) ^ CostExponent
```

### Daily Reward
```
Fidelis = BaseFidelis + (CharacterLevel × PerLevelFidelis)
Default: 45 + (Level × 3)
```

### Stage Enemy Stats
```
EnemyStat = BaseStat × (1 + (Stage - 1) × ScalingRate) × BiomeDifficultyMultiplier × BossMultiplier
```

### Fidelis Economy
- **Arena Win**: 15 × (1 + attackerLevel × 0.1) × (1 + (defenderLevel - 1) × 0.1)
- **Stage Normal**: 10 per enemy
- **Stage Boss**: 50 per boss
- **Daily Reward**: 45 + 3 × level
- **Upgrade Cost**: Escalating (20-50 base, ^2.0 exponent)
- **Revive**: 30 Fidelis
- **HP Restore**: 10 Fidelis

---

## Agent Execution Checklist

When building this app, follow these phases in order:

- [ ] **Phase 1**: Create solution, projects, references, NuGet packages
- [ ] **Phase 2**: Port all Core entities, enums, helpers, configuration (24 files)
- [ ] **Phase 3**: Port all Application interfaces, services, DTOs, configuration (56 files)
- [ ] **Phase 4**: Create Infrastructure with DbContext, repositories (14 files)
- [ ] **Phase 5**: Port all Blazor pages and components (22 files with .css)
- [ ] **Phase 6**: Copy all static assets (sprites, JS, CSS, audio — 240+ files)
- [ ] **Phase 7**: Copy scaling.config.json and wire configuration
- [ ] **Phase 8**: Set up Identity auth with simplified user model
- [ ] **Phase 9**: Port all tests (13+ test files)
- [ ] **Phase 10**: Wire Program.cs, run migrations, verify build
- [ ] **Verify**: `dotnet build` succeeds, `dotnet test` passes, app runs at `https://localhost:5001`
