# 🎮 Stage Mode Feature - Implementation Package

**Status:** Ready for Implementation  
**Created:** 2025-02-01  
**Target:** My-Tuno Progressive Stage System

---

## 📦 What's Included

This package contains a complete architectural analysis and implementation guide for adding a **Stage Mode** feature to the My-Tuno game system.

### Documents

1. **`STAGE_MODE_IMPLEMENTATION_GUIDE.md`** (Main Document)
   - Comprehensive exploration of current system
   - Detailed code snippets and file paths
   - Two architecture options (Progressive Stages vs Arena Extension)
   - Complete implementation checklist
   - Database schema design
   - Service layer architecture

2. **`STAGE_MODE_ARCHITECTURE_DIAGRAMS.md`** (Visual Reference)
   - ASCII architecture diagrams
   - Current system flow
   - Proposed system additions
   - Battle flow comparison
   - File structure overview
   - Quick command reference

3. **`README_STAGE_MODE.md`** (This File)
   - Package overview
   - Quick start guide

---

## 🎯 Feature Overview

### What is Stage Mode?

A progressive challenge system where players battle through 12 stages, each rewarding a unique instrument upon first completion.

**Key Features:**
- 12 stages, each themed around an instrument
- Progressive difficulty curve
- Level-gated unlocking
- Guaranteed instrument reward on first boss victory
- Re-playable for farming resources (beer, fidelis)
- Persistent completion tracking per character

### Why Stage Mode?

**Current System (Arena):**
- ✅ PvP-style battles against random opponents
- ✅ Random rewards (XP, Fidelis, 20% beer drop)
- ❌ No structured progression
- ❌ No collectibles/achievements

**Stage Mode Adds:**
- ✅ Structured progression system
- ✅ Collectible instruments (12 unique types)
- ✅ Predictable difficulty scaling
- ✅ Achievement tracking
- ✅ Replay value for farming

---

## 🚀 Quick Start

### 1. Review the Architecture

**Option A: Progressive Stage System (Recommended)**
- Full-featured stage progression
- New entities: `Stage`, `CharacterStageProgress`
- Separate page: `/my-tuno/stages`
- Best for long-term game depth

**Option B: Arena Extension (Simpler)**
- Extends existing Arena with milestone rewards
- No new entities needed
- Instruments drop at specific levels (5, 10, 15...)
- Quick to implement

👉 **See `STAGE_MODE_IMPLEMENTATION_GUIDE.md` Section 7** for detailed comparison

### 2. Choose Your Approach

**For MVP (Minimum Viable Product):**
- Start with **Option B** (Arena Extension)
- Takes ~2-4 hours to implement
- Gets instruments into the game quickly
- Can upgrade to Option A later

**For Full Feature:**
- Go with **Option A** (Progressive Stages)
- Takes ~1-2 days to implement
- Better player experience
- More engaging progression

### 3. Implementation Phases

**Phase 1: Database Schema** (30 min - 2 hours)
```bash
cd src/RTUB.Application
dotnet ef migrations add AddStageMode --startup-project ../RTUB.Web
dotnet ef database update --startup-project ../RTUB.Web
```

**Phase 2: Repository Layer** (1-3 hours)
- Create repositories
- Add DbSets to ApplicationDbContext
- Test with unit tests

**Phase 3: Service Layer** (2-4 hours)
- Implement business logic
- Integrate with existing BattleService
- Add reward distribution logic

**Phase 4: UI Layer** (3-6 hours)
- Create Stages.razor page
- Build stage selection grid
- Add instrument display
- Integrate Phaser battle animation

**Phase 5: Testing & Polish** (2-4 hours)
- Unit tests
- Integration tests
- UI/UX refinements

---

## 📋 Implementation Checklist

### Option A: Progressive Stages

#### Phase 1: Database Schema
- [ ] Create `Stage.cs` entity in `/src/RTUB.Core/Entities/`
- [ ] Create `CharacterStageProgress.cs` entity
- [ ] Extend `InventoryItemType` enum (add 12 instruments)
- [ ] Update `MyTunoScaling.cs` with stage scaling formulas
- [ ] Create migration: `dotnet ef migrations add AddStageMode`
- [ ] Seed initial 12 stages in migration
- [ ] Apply migration: `dotnet ef database update`

#### Phase 2: Repository Layer
- [ ] Create `IStageRepository.cs` interface
- [ ] Implement `StageRepository.cs`
- [ ] Create `ICharacterStageProgressRepository.cs` interface
- [ ] Implement `CharacterStageProgressRepository.cs`
- [ ] Add `DbSet<Stage>` to `ApplicationDbContext`
- [ ] Add `DbSet<CharacterStageProgress>` to `ApplicationDbContext`
- [ ] Register repositories in DI container

#### Phase 3: Service Layer
- [ ] Create `IStageService.cs` interface
- [ ] Implement `StageService.cs`
  - [ ] `GetAvailableStagesAsync()`
  - [ ] `GetStageProgressAsync()`
  - [ ] `CreateStageBattleAsync()`
  - [ ] `CompleteStageAsync()`
- [ ] Extend `IBattleService` with stage battle support
- [ ] Update `BattleService.cs`
  - [ ] `CreateStageBattleAsync()` method
  - [ ] Stage boss stat loading
  - [ ] Instrument drop logic
- [ ] Extend `IInventoryService` for instruments
- [ ] Update `InventoryService.cs`
  - [ ] `AddInstrumentAsync()`
  - [ ] `GetInstrumentsAsync()`
  - [ ] `HasInstrumentAsync()`
- [ ] Register services in DI container

#### Phase 4: UI Layer
- [ ] Create `Stages.razor` page (`/my-tuno/stages`)
- [ ] Build stage selection grid component
  - [ ] Stage card design
  - [ ] Locked/unlocked states
  - [ ] Boss stats preview
  - [ ] Reward preview
- [ ] Implement stage battle view
  - [ ] Reuse Phaser battle animation
  - [ ] Boss encounter screen
  - [ ] Victory/defeat modals
- [ ] Create instrument reward modal
- [ ] Update `MyTunoHome.razor`
  - [ ] Add "Stages" navigation button
  - [ ] Display collected instruments
  - [ ] Instrument collection progress bar
- [ ] Add CSS styling for new components

#### Phase 5: Testing
- [ ] Unit tests for `StageService`
- [ ] Unit tests for repositories
- [ ] Integration test: Complete stage flow
- [ ] Integration test: Instrument drop logic
- [ ] E2E test: Full stage progression
- [ ] Manual testing: UI/UX flow

#### Phase 6: Polish
- [ ] Add animations for instrument drops
- [ ] Add sound effects (optional)
- [ ] Add tooltips for locked stages
- [ ] Performance optimization
- [ ] Code review and refactoring
- [ ] Documentation updates

---

## 🎸 Instrument Types (12 Total)

All instruments from `InstrumentType` enum ready for use:

1. **Guitarra** (Guitar) - Stage 1
2. **Bandolim** (Mandolin) - Stage 2
3. **Cavaquinho** (Small Guitar) - Stage 3
4. **Acordeao** (Accordion) - Stage 4
5. **Fagote** (Bassoon) - Stage 5
6. **Flauta** (Flute) - Stage 6
7. **Baixo** (Bass) - Stage 7
8. **Contrabaixo** (Double Bass) - Stage 8
9. **Percussao** (Percussion) - Stage 9
10. **Pandeireta** (Tambourine) - Stage 10
11. **Estandarte** (Banner/Standard) - Stage 11
12. **Violino** (Violin) - Stage 12

---

## 🗂️ File References

### Must Read
- `STAGE_MODE_IMPLEMENTATION_GUIDE.md` - Complete details
- `STAGE_MODE_ARCHITECTURE_DIAGRAMS.md` - Visual reference

### Current System Files

**Core Domain:**
- `/src/RTUB.Core/Entities/Character.cs`
- `/src/RTUB.Core/Entities/Battle.cs`
- `/src/RTUB.Core/Entities/InventoryItem.cs`
- `/src/RTUB.Core/Enums/InstrumentType.cs`
- `/src/RTUB.Core/Enums/InventoryItemType.cs`

**Application Layer:**
- `/src/RTUB.Application/Services/BattleService.cs`
- `/src/RTUB.Application/Services/CharacterService.cs`
- `/src/RTUB.Application/Services/InventoryService.cs`
- `/src/RTUB.Application/Services/DeterministicCombatEngine.cs`

**Presentation Layer:**
- `/src/RTUB.Web/Pages/MyTuno/MyTunoHome.razor`
- `/src/RTUB.Web/Pages/MyTuno/Arena.razor`

---

## 💡 Design Principles

This implementation follows **Clean Architecture** principles:

### Dependency Flow
```
Web (UI) → Application (Services) → Core (Domain)
         ↘ Data (Repositories) ↗
```

### Key Patterns Used
- **Repository Pattern**: Data access abstraction
- **Factory Pattern**: Entity creation (`Stage.Create()`, `Battle.Create()`)
- **Service Layer Pattern**: Business logic isolation
- **Dependency Injection**: Loose coupling
- **SOLID Principles**: Throughout the codebase

### Best Practices
- ✅ Async/await for all I/O operations
- ✅ Interface-based abstractions
- ✅ Constructor injection
- ✅ PascalCase for public, _camelCase for private
- ✅ Single Responsibility Principle
- ✅ Validation in entity classes
- ✅ Audit fields on all entities

---

## 🔧 Development Commands

```bash
# Navigate to project root
cd /home/runner/work/RTUB/RTUB

# Create migration
cd src/RTUB.Application
dotnet ef migrations add AddStageMode --startup-project ../RTUB.Web

# Apply migration
dotnet ef database update --startup-project ../RTUB.Web

# Build
cd ../..
dotnet build

# Format code
dotnet format

# Run project
cd src/RTUB.Web
dotnet run

# Run tests
cd ../RTUB.Tests
dotnet test
```

---

## 🎮 Example Stage Configuration

```csharp
// Stage.cs - Factory method example
public static Stage Create(int stageNumber, string name, InstrumentType rewardInstrument, int requiredLevel)
{
    // Auto-calculate boss stats based on stage number
    var bossHP = 100 + (stageNumber * 50);
    var bossPower = 10 + (stageNumber * 3);
    var bossSpeed = 10 + (stageNumber * 2);
    var bossCriticalChance = 0.01 + (stageNumber * 0.01);
    
    return new Stage
    {
        StageNumber = stageNumber,
        Name = name,
        RequiredLevel = requiredLevel,
        RewardInstrumentType = (InventoryItemType)rewardInstrument,
        BossHP = bossHP,
        BossPower = bossPower,
        BossSpeed = bossSpeed,
        BossCriticalChance = Math.Min(0.5, bossCriticalChance) // Cap at 50%
    };
}
```

**Usage in Seed:**
```csharp
Stage.Create(1, "Guitarra Trial", InstrumentType.Guitarra, requiredLevel: 1)
```

---

## 🧪 Testing Strategy

### Unit Tests
- `StageService` business logic
- `StageRepository` CRUD operations
- `CharacterStageProgressRepository` queries
- Instrument drop logic

### Integration Tests
- Full stage battle flow
- Database migrations
- Service layer integration

### E2E Tests
- User completes a stage
- Instrument reward is granted
- Stage progression unlocks next stage

### Manual Testing Checklist
- [ ] Navigate to `/my-tuno/stages`
- [ ] Verify locked stages show lock icon
- [ ] Select unlocked stage
- [ ] Battle boss (win)
- [ ] Verify instrument reward appears
- [ ] Check inventory shows new instrument
- [ ] Re-battle same stage
- [ ] Verify no duplicate instrument (beer drop instead)
- [ ] Level up character
- [ ] Verify next stage unlocks

---

## 📚 Additional Resources

### EF Core Migrations
- [EF Core Migrations Documentation](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)

### Blazor Server
- [Blazor Components](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/)
- [Blazor Routing](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/routing)

### Clean Architecture
- [Clean Architecture by Uncle Bob](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [SOLID Principles](https://en.wikipedia.org/wiki/SOLID)

---

## 🤝 Contributing

When implementing this feature:

1. **Create a feature branch:**
   ```bash
   git checkout -b feature/stage-mode
   ```

2. **Follow existing patterns:**
   - Look at `BattleService.cs` for service structure
   - Look at `CharacterRepository.cs` for repository structure
   - Look at `Arena.razor` for UI patterns

3. **Test thoroughly:**
   - Add unit tests for new services
   - Add integration tests for new flows
   - Manual test all UI interactions

4. **Code review:**
   - Request review before merging
   - Address all feedback
   - Ensure CI/CD passes

5. **Merge to main:**
   ```bash
   git checkout main
   git merge feature/stage-mode
   ```

---

## 📞 Support

If you encounter issues during implementation:

1. Review `STAGE_MODE_IMPLEMENTATION_GUIDE.md` for detailed explanations
2. Check existing code patterns in current files
3. Consult EF Core documentation for migration issues
4. Review Blazor docs for UI component issues

---

## ✅ Success Criteria

Stage Mode is complete when:

- [ ] All 12 stages are in the database
- [ ] Stages unlock based on character level
- [ ] Battles can be fought against stage bosses
- [ ] Instruments drop on first completion (100%)
- [ ] Beer drops on subsequent completions (20%)
- [ ] UI displays stage selection grid
- [ ] UI shows collected instruments
- [ ] Battle animation works correctly
- [ ] All tests pass
- [ ] Code is reviewed and approved
- [ ] Documentation is updated

---

**Ready to build!** 🚀

---

**Document Version:** 1.0  
**Last Updated:** 2025-02-01  
**Author:** AI Architecture Assistant
