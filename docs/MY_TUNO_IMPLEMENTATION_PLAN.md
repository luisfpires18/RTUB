# My Tuno - Implementation Plan

## High-Level Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                         RTUB.Web (UI Layer)                      │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  /my-tuno (Separate Route - NOT in Games section)        │   │
│  │  - MyTunoHome.razor (Dashboard)                          │   │
│  │  - CharacterProfile.razor (Stats + Fidelis)              │   │
│  │  - UpgradeShop.razor (Buy HP/Power/Speed)                │   │
│  │  - Arena.razor (Battle vs AI + History)                   │   │
│  │  - ReplayViewer.razor (Battle replay)                    │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                  RTUB.Application (Service Layer)               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐         │
│  │ UpgradeService│  │BattleService │  │Matchmaking   │         │
│  │              │  │              │  │Service (AI)  │         │
│  └──────────────┘  └──────────────┘  └──────────────┘         │
│  ┌──────────────┐                                              │
│  │CombatEngine   │                                              │
│  │(Deterministic)│                                              │
│  └──────────────┘                                              │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    RTUB.Core (Domain Layer)                      │
│  ┌──────────────┐  ┌──────────────┐                            │
│  │  Character   │  │    Battle    │                            │
│  │  Entity      │  │    Entity    │                            │
│  └──────────────┘  └──────────────┘                            │
│  ┌──────────────┐  ┌──────────────┐                            │
│  │ CombatEngine│  │Deterministic  │                            │
│  │  Interface   │  │   RNG Wrapper│                            │
│  └──────────────┘  └──────────────┘                            │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│              RTUB.Application.Data (Infrastructure)             │
│  ┌──────────────┐  ┌──────────────┐                            │
│  │CharacterRepo │  │ BattleRepo   │                            │
│  └──────────────┘  └──────────────┘                            │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │         ApplicationDbContext (EF Core 10)                 │  │
│  │  - Characters, Battles                                     │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

## Entity/Table Design

### 1. Character Entity
```csharp
// RTUB.Core/Entities/Character.cs
public class Character : BaseEntity
{
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    // Progression
    public int Level { get; set; } = 1;
    public int XP { get; set; } = 0;
    
    // Base Stats (ONLY these three)
    public int HP { get; set; } = 100;        // Base HP
    public int Power { get; set; } = 10;      // Base Power
    public int Speed { get; set; } = 10;      // Base Speed
    
    // Upgrade Counts (for cost calculation)
    public int HpUpgrades { get; set; } = 0;
    public int PowerUpgrades { get; set; } = 0;
    public int SpeedUpgrades { get; set; } = 0;
    
    // Navigation
    public virtual ApplicationUser User { get; set; } = null!;
    
    // Computed properties
    public int TotalHP => HP + (HpUpgrades * 10);      // +10 HP per upgrade
    public int TotalPower => Power + (PowerUpgrades * 2);  // +2 Power per upgrade
    public int TotalSpeed => Speed + (SpeedUpgrades * 1);  // +1 Speed per upgrade
    
    // Factory method
    public static Character Create(string userId)
    {
        return new Character
        {
            UserId = userId,
            Level = 1,
            XP = 0,
            HP = 100,
            Power = 10,
            Speed = 10
        };
    }
    
    public void AddXP(int amount)
    {
        XP += amount;
        // Level up logic: 100 XP per level (configurable)
        while (XP >= Level * 100)
        {
            XP -= Level * 100;
            Level++;
        }
    }
}
```

**Database Schema:**
- Table: `Characters`
- Indexes: `IX_Characters_UserId` (unique), `IX_Characters_Level`
- FK: `UserId` → `AspNetUsers.Id` (CASCADE DELETE)

### 2. Battle Entity
```csharp
// RTUB.Core/Entities/Battle.cs
public class Battle : BaseEntity
{
    [Required]
    public int AttackerCharacterId { get; set; }
    
    [Required]
    public int DefenderCharacterId { get; set; }
    
    // Note: All battles are vs AI (other players' characters used as CPU opponents)
    // Defender is always an AI opponent (another player's character, but treated as CPU)
    
    [Required]
    public int Seed { get; set; }  // RNG seed for determinism
    
    [Required]
    public BattleOutcome Outcome { get; set; }  // AttackerWon, DefenderWon, Draw
    
    // Rewards (stored as deltas)
    // Note: Defender rewards are always 0 (AI opponents don't receive rewards)
    public int AttackerXP { get; set; } = 0;
    public int DefenderXP { get; set; } = 0;  // Always 0 for AI
    public decimal AttackerFidelis { get; set; } = 0m;
    public decimal DefenderFidelis { get; set; } = 0m;  // Always 0 for AI
    
    // Replay data (JSON)
    public string ReplayJson { get; set; } = string.Empty;
    
    // Navigation
    public virtual Character Attacker { get; set; } = null!;
    public virtual Character Defender { get; set; } = null!;
}
```

**Enums:**
```csharp
// RTUB.Core/Enums/BattleOutcome.cs
public enum BattleOutcome
{
    AttackerWon = 0,
    DefenderWon = 1,
    Draw = 2
}
```

**Database Schema:**
- Table: `Battles`
- Indexes: `IX_Battles_AttackerCharacterId`, `IX_Battles_DefenderCharacterId`, `IX_Battles_CreatedAt`
- FKs: `AttackerCharacterId` → `Characters.Id`, `DefenderCharacterId` → `Characters.Id`
- **Note:** Defender is always an AI opponent (another player's character used as CPU)

## Services & Use Cases

### 1. CharacterService
**Interface:** `ICharacterService`
**Responsibilities:**
- Get character by user ID
- Create character for new user (on first access)
- Update character stats after upgrades
- Add XP and handle level-ups

**Methods:**
```csharp
Task<Character> GetOrCreateCharacterAsync(string userId);
Task<Character> GetCharacterAsync(string userId);
Task UpdateCharacterAsync(Character character);
```

### 2. UpgradeService
**Interface:** `IUpgradeService`
**Responsibilities:**
- Calculate upgrade cost based on current upgrade count
- Purchase stat upgrade (HP/Power/Speed)
- Atomic transaction: deduct Fidelis + increment upgrade count
- Concurrency-safe using database transaction with rowversion check

**Upgrade Cost Formula:**
```
Cost = BaseCost * (1 + UpgradeCount) ^ 1.5
- HP: BaseCost = 50 Fidelis
- Power: BaseCost = 75 Fidelis
- Speed: BaseCost = 100 Fidelis
```

**Methods:**
```csharp
Task<decimal> GetUpgradeCostAsync(string userId, StatType statType);
Task<UpgradeResult> PurchaseUpgradeAsync(string userId, StatType statType);
```

**DTO:**
```csharp
public enum StatType { HP, Power, Speed }
public class UpgradeResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public decimal NewFidelisBalance { get; set; }
    public int NewUpgradeCount { get; set; }
}
```

**Concurrency Strategy:**
- Use `ApplicationUser.ConcurrencyStamp` (Identity's built-in rowversion)
- Load user with tracking
- Check balance in transaction
- Deduct Fidelis + update Character in same transaction
- Catch `DbUpdateConcurrencyException` and retry (max 3 attempts)

### 3. MatchmakingService (AI Opponent Selection)
**Interface:** `IMatchmakingService`
**Responsibilities:**
- Find suitable AI opponent (another player's character used as CPU)
- Calculate power rating from HP/Power/Speed
- Avoid recent opponents (cooldown: 5 battles)
- Match within ±20% power rating
- Exclude the current player's own character

**Power Rating Formula:**
```
PowerRating = (TotalHP * 0.4) + (TotalPower * 0.4) + (TotalSpeed * 0.2)
```

**AI Opponent Selection:**
- Selects from existing player characters in the database
- Opponent does NOT need to be online (fully offline/AI)
- Uses opponent's actual stats but treats as CPU-controlled

**Methods:**
```csharp
Task<Character?> FindAIOpponentAsync(int characterId);
```

### 4. BattleService
**Interface:** `IBattleService`
**Responsibilities:**
- Create battle record (player vs AI)
- Run deterministic combat simulation
- Apply rewards (XP + Fidelis) to player only (AI opponent doesn't get rewards)
- Persist replay JSON

**Methods:**
```csharp
Task<Battle> CreateBattleAsync(int playerCharacterId);  // Finds AI opponent automatically
Task<Battle> GetBattleAsync(int battleId);
Task<List<Battle>> GetBattleHistoryAsync(int characterId, int page = 1, int pageSize = 20);
```

**Reward Rules (Player only - AI opponent doesn't receive rewards):**
- Player Wins: 50 XP + 10 Fidelis
- Player Loses: 20 XP + 5 Fidelis
- Draw: 30 XP + 7 Fidelis

### 5. CombatEngine (Domain Service)
**Interface:** `ICombatEngine`
**Implementation:** `DeterministicCombatEngine`
**Responsibilities:**
- Run deterministic combat simulation
- Generate replay events
- Use seeded RNG for all randomness

**Combat Rules:**
1. **Turn-based system** (Speed determines turn order)
2. **Initiative:** Higher Speed attacks first. If equal, attacker goes first.
3. **Damage Formula:**
   ```
   BaseDamage = Power
   Variance = Random(0.8, 1.2) using seeded RNG
   FinalDamage = BaseDamage * Variance (rounded)
   ```
4. **Turn Order:** Each character's turn frequency = Speed / 10 (minimum 1 turn per 10 ticks)
5. **Max Rounds:** 50 rounds (100 total actions) to prevent infinite fights
6. **Victory Condition:** First to reach 0 HP loses. If both reach 0 in same round, Draw.

**Replay Event Schema:**
```json
{
  "version": "1.0",
  "events": [
    { "type": "RoundStart", "round": 1, "timestamp": 0 },
    { "type": "Attack", "attacker": "Character1", "defender": "Character2", "damage": 12, "timestamp": 1 },
    { "type": "HPUpdate", "character": "Character2", "hp": 88, "timestamp": 1 },
    { "type": "Attack", "attacker": "Character2", "defender": "Character1", "damage": 10, "timestamp": 2 },
    { "type": "HPUpdate", "character": "Character1", "hp": 90, "timestamp": 2 },
    { "type": "RoundEnd", "round": 1, "timestamp": 2 },
    ...
    { "type": "KO", "character": "Character2", "timestamp": 45 },
    { "type": "Victory", "winner": "Character1", "timestamp": 45 }
  ],
  "finalState": {
    "character1": { "hp": 15 },
    "character2": { "hp": 0 }
  }
}
```

**Methods:**
```csharp
CombatResult Simulate(Character attacker, Character defender, int seed);
```

**DTO:**
```csharp
public class CombatResult
{
    public BattleOutcome Outcome { get; set; }
    public List<CombatEvent> Events { get; set; } = new();
    public int AttackerFinalHP { get; set; }
    public int DefenderFinalHP { get; set; }
}

public class CombatEvent
{
    public string Type { get; set; } = string.Empty;  // RoundStart, Attack, Damage, HPUpdate, KO, Victory, RoundEnd
    public int? Round { get; set; }
    public string? Attacker { get; set; }
    public string? Defender { get; set; }
    public int? Damage { get; set; }
    public string? Character { get; set; }
    public int? HP { get; set; }
    public string? Winner { get; set; }
    public int Timestamp { get; set; }
}
```

**Note:** SignalR, PresenceService, and LiveChallengeService are **NOT needed** for AI-only battles. All battles are offline/simulated.

## Deterministic Combat Engine - Detailed Rules

### Algorithm Pseudocode:
```
1. Initialize:
   - AttackerHP = attacker.TotalHP
   - DefenderHP = defender.TotalHP
   - RNG = new SeededRandom(seed)
   - TurnQueue = PriorityQueue (Speed-based)
   - Round = 1
   - MaxRounds = 50

2. While AttackerHP > 0 AND DefenderHP > 0 AND Round <= MaxRounds:
   a. Process all actions in current round:
      - Calculate turn order based on Speed
      - For each action:
        * Attacker = next in queue
        * Defender = opponent
        * Damage = Attacker.TotalPower * RNG.Next(0.8, 1.2)
        * DefenderHP -= Damage
        * Emit Attack event
        * Emit HPUpdate event
        * If DefenderHP <= 0:
          - Emit KO event
          - Emit Victory event (Attacker wins)
          - Break
   
   b. Round++
   c. Emit RoundEnd event

3. If Round > MaxRounds:
   - Determine winner by remaining HP
   - If equal HP: Draw
   - Emit Victory/Draw event

4. Return CombatResult
```

### Seeded RNG Wrapper:
```csharp
// RTUB.Core/Utilities/SeededRandom.cs
public class SeededRandom
{
    private readonly Random _random;
    
    public SeededRandom(int seed)
    {
        _random = new Random(seed);
    }
    
    public double NextDouble() => _random.NextDouble();
    public int Next(int min, int max) => _random.Next(min, max);
    public double Next(double min, double max) => min + (max - min) * _random.NextDouble();
}
```

## UI Pages/Components

### 1. My Tuno Home/Dashboard
**Route:** `/my-tuno`
**File:** `RTUB.Web/Pages/MyTuno/MyTunoHome.razor`

**Features:**
- Character summary card (Level, XP, Stats)
- Quick actions: Upgrade Shop, Arena
- Recent battle history (last 5)
- Fidelis balance display

### 2. Character Profile
**Route:** `/my-tuno/profile`
**File:** `RTUB.Web/Pages/MyTuno/CharacterProfile.razor`

**Features:**
- Full stat display (HP, Power, Speed)
- Upgrade counts
- Level and XP progress bar
- Battle statistics (wins/losses/draws)

### 3. Upgrade Shop
**Route:** `/my-tuno/upgrade`
**File:** `RTUB.Web/Pages/MyTuno/UpgradeShop.razor`

**Features:**
- Three upgrade cards (HP, Power, Speed)
- Current stat value + next upgrade cost
- Purchase button (disabled if insufficient Fidelis)
- Confirmation modal before purchase

### 4. Arena (Battle vs AI)
**Route:** `/my-tuno/arena`
**File:** `RTUB.Web/Pages/MyTuno/Arena.razor`

**Features:**
- "Fight" button (triggers AI matchmaking + battle)
- Loading state during simulation
- Opponent info display (shows which player's character is the AI opponent)
- Replay viewer (canvas/JS animation)
- Battle history table (paginated)
- Filter by outcome

### 5. Replay Viewer
**Route:** `/my-tuno/replay/{battleId}`
**File:** `RTUB.Web/Pages/MyTuno/ReplayViewer.razor`

**Features:**
- Canvas-based animation (JS interop)
- Play/Pause/Step controls
- Event log sidebar
- Speed control (0.5x, 1x, 2x)

## Routing & Navigation

### Routes:
- `/my-tuno` - Home/Dashboard
- `/my-tuno/profile` - Character Profile
- `/my-tuno/upgrade` - Upgrade Shop
- `/my-tuno/arena` - Arena (Battle vs AI)
- `/my-tuno/replay/{battleId}` - Replay Viewer

### Navigation Entry:
Add to `MainLayout.razor` navigation (separate from Games dropdown):
```razor
<li class="nav-item">
    <NavLink class="nav-link" href="/my-tuno">
        <i class="bi bi-sword"></i> My Tuno
    </NavLink>
</li>
```

**Placement:** After "Jogos" dropdown, before "Galeria"

## Step-by-Step Implementation Plan

### Milestone 1: Core Domain & Entities
**Goal:** Set up Character entity and basic data model

**Tasks:**
1. Create `Character` entity in `RTUB.Core/Entities/`
2. Create `Battle` entity in `RTUB.Core/Entities/`
3. Create enums: `BattleOutcome`, `StatType`
4. Create EF Core configurations for all entities
5. Add DbSets to `ApplicationDbContext`
6. Create migration: `AddMyTunoEntities`
7. Update `ApplicationDbContext.GetEntityDisplayName()` for new entities

**Test:**
- `CharacterTests.cs`: Verify Character creation, XP/level progression, stat calculations
- `BattleTests.cs`: Verify Battle entity creation and relationships

---

### Milestone 2: Character Service & Repository
**Goal:** Basic character CRUD operations

**Tasks:**
1. Create `ICharacterRepository` interface
2. Create `CharacterRepository` implementation
3. Create `ICharacterService` interface
4. Create `CharacterService` implementation
5. Register services in `Program.cs`

**Test:**
- `CharacterServiceTests.cs`: GetOrCreateCharacterAsync, GetCharacterAsync, UpdateCharacterAsync

---

### Milestone 3: Upgrade System
**Goal:** Purchase stat upgrades with Fidelis

**Tasks:**
1. Create `IUpgradeService` interface
2. Create `UpgradeService` with cost calculation
3. Implement atomic upgrade purchase (transaction + concurrency handling)
4. Create UpgradeShop.razor page
5. Add upgrade endpoints/API if needed

**Test:**
- `UpgradeServiceTests.cs`: 
  - Cost calculation scales correctly
  - Purchase succeeds with sufficient Fidelis
  - Purchase fails with insufficient Fidelis
  - Concurrency: Two simultaneous purchases don't double-spend
  - Fidelis never goes negative

---

### Milestone 4: Deterministic Combat Engine
**Goal:** Server-authoritative battle simulation

**Tasks:**
1. Create `SeededRandom` utility class
2. Create `ICombatEngine` interface
3. Create `DeterministicCombatEngine` implementation
4. Implement combat rules (turn-based, damage formula, max rounds)
5. Generate replay event JSON

**Test:**
- `CombatEngineTests.cs`:
  - **Determinism test**: Same inputs + seed => identical outcome/replay
  - Different seeds => different outcomes
  - Max rounds enforced (no infinite fights)
  - Victory conditions correct (HP <= 0)
  - Draw when both reach 0 HP simultaneously

---

### Milestone 5: Battle Service & AI Matchmaking
**Goal:** Player vs AI battle flow

**Tasks:**
1. Create `IMatchmakingService` interface
2. Create `MatchmakingService` (power rating, cooldown logic, AI opponent selection)
3. Create `IBattleService` interface
4. Create `BattleService` (create battle vs AI, run combat, apply rewards to player only)
5. Create `IBattleRepository` interface and implementation
6. Register services

**Test:**
- `MatchmakingServiceTests.cs`: Finds AI opponent within power range, respects cooldown, excludes player's own character
- `BattleServiceTests.cs`: Creates battle, applies rewards to player only (not AI), persists replay

---

### Milestone 6: Arena UI
**Goal:** Battle vs AI user interface

**Tasks:**
1. Create `Arena.razor` page
2. Implement "Fight" button → AI matchmaking → battle → replay
3. Display AI opponent info (which player's character is being used as CPU)
4. Create basic replay viewer component (event log, no animation yet)
5. Add battle history table

**Test:**
- Manual testing: Complete battle vs AI flow works end-to-end

---

### Milestone 7: Replay Viewer (Canvas Animation)
**Goal:** Animated battle replay

**Tasks:**
1. Create `ReplayViewer.razor` page
2. Implement JS interop for canvas animation
3. Parse replay JSON and render events
4. Add play/pause/step controls
5. Add speed control

**Test:**
- Manual testing: Replay animation matches combat events correctly

---

### Milestone 8: UI Polish & Navigation
**Goal:** Complete My Tuno area integration

**Tasks:**
1. Create `MyTunoHome.razor` dashboard
2. Create `CharacterProfile.razor` page
3. Add navigation entry to `MainLayout.razor`
4. Create shared layout for My Tuno pages (optional)
5. Mobile-responsive styling
6. Add loading states and error handling

**Test:**
- Manual testing: All pages accessible, navigation works, mobile-friendly

---

### Milestone 9: Final Testing & Documentation
**Goal:** Comprehensive testing and cleanup

**Tasks:**
1. Integration tests for complete flows
2. Performance testing (combat determinism under load)
3. Security review (authorization checks)
4. Update README with My Tuno section
5. Code cleanup and optimization

**Test:**
- `MyTunoIntegrationTests.cs`: End-to-end flows (upgrade → battle → rewards)
- Load test: 100 concurrent battles verify determinism

---

## Security Considerations

1. **Authorization:**
   - All My Tuno pages require `[Authorize]`
   - Users can only access their own Character
   - Validate `userId` in all service methods

2. **Input Validation:**
   - Validate stat upgrade requests
   - Sanitize replay JSON

3. **Concurrency:**
   - Use `ApplicationUser.ConcurrencyStamp` for Fidelis updates
   - Retry logic for upgrade purchases
   - Database transactions for atomic operations

4. **Rate Limiting:**
   - Consider rate limiting on battle creation (e.g., max 10 battles/hour per user)

## Performance Considerations

1. **Combat Engine:**
   - Keep combat simulation synchronous (fast enough)
   - Cache power ratings for matchmaking
   - Index battle queries by CharacterId and CreatedAt

2. **Replay Storage:**
   - Store replay JSON compressed if large
   - Consider archiving old battles after 90 days

## Future Enhancements (Out of Scope)

- Character customization (name, avatar)
- Equipment/items system
- Guilds/clans
- Tournaments
- Leaderboards (separate from existing Games leaderboard)
- Achievements
