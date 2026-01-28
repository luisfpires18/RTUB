# My Tuno (Blazor Server) Implementation Plan

## High-level architecture diagram (text)

```
UI (Blazor Server - /my-tuno)
  ├─ Pages/Components: Dashboard, Character, Shop, Arena, Live Challenge, Replay
  ├─ SignalR Client (Presence + Live Match)
  └─ Calls Application Services (via DI)
        │
Application Layer
  ├─ UpgradeService
  ├─ MatchmakingService
  ├─ BattleService (Async/Live)
  ├─ PresenceService
  └─ LiveChallengeService
        │
Domain Layer
  ├─ Entities: Character, Battle, ChallengeRequest
  ├─ Combat Engine (deterministic)
  └─ RNG abstraction (seeded)
        │
Infrastructure Layer
  ├─ EF Core 10 (DbContext + repositories)
  ├─ Identity (ApplicationUser with Fidelis + concurrency token)
  └─ SignalR Hubs (PresenceHub, LiveMatchHub)
```

## Entities / tables

### Character
- **Id** (PK)
- **UserId** (FK → ApplicationUser)
- **Level** (int)
- **Xp** (int)
- **Hp** (int)
- **Power** (int)
- **Speed** (int)
- **HpUpgrades** (int)
- **PowerUpgrades** (int)
- **SpeedUpgrades** (int)
- **CreatedAt**, **UpdatedAt** (optional timestamps)
- Indexes:
  - Unique index on **UserId** (1 Character per user)
  - Optional index on **(Power, Speed, Hp)** for matchmaking scans

### Battle
- **Id** (PK)
- **Mode** (enum: Async, Live)
- **Seed** (long)
- **Outcome** (enum: AttackerWin, DefenderWin, Draw)
- **ReplayJson** (string/JSON)
- **AttackerCharacterId** (FK → Character)
- **DefenderCharacterId** (FK → Character)
- **AttackerXpDelta** (int)
- **DefenderXpDelta** (int)
- **AttackerFidelisDelta** (int)
- **DefenderFidelisDelta** (int)
- **StartedAt**, **CompletedAt** (timestamps)
- Indexes:
  - Index on **(AttackerCharacterId, StartedAt)**
  - Index on **(DefenderCharacterId, StartedAt)**

### ChallengeRequest
- **Id** (PK)
- **RequesterUserId** (FK → ApplicationUser)
- **TargetUserId** (FK → ApplicationUser)
- **Status** (Pending/Accepted/Declined/Expired)
- **CreatedAt**, **ExpiresAt**, **ResolvedAt**
- Indexes:
  - Index on **(TargetUserId, Status)**

## Services / use-cases and responsibilities

### UpgradeService
- Calculates per-stat upgrade cost using upgrade counts.
- Atomically deducts **ApplicationUser.Fidelis** and increments stat + upgrade count.
- Enforces ownership (user can only upgrade their own Character).
- Uses optimistic concurrency token on ApplicationUser (or transaction + row lock).

### MatchmakingService
- Picks opponent Character for async battles.
- Rating example: `Rating = Hp * 1.0 + Power * 2.0 + Speed * 1.5`.
- Filters out:
  - Same user
  - Recent opponents (cooldown window)
  - Very large rating gaps (±20% by default)

### BattleService
- Creates battle records (async and live).
- Runs deterministic combat simulation (server-authoritative).
- Applies XP/Fidelis rewards to appropriate users.
- Persists ReplayJson for client playback.

### PresenceService
- Tracks online presence for My Tuno via SignalR connections.
- Exposes availability list for Live Challenges.

### LiveChallengeService
- Creates and manages ChallengeRequest.
- Enforces accept/decline timeout.
- Starts live battle on accept, handles disconnect rules.

## SignalR message contracts

### Client → Server
- `Presence/JoinMyTuno()` – mark user online for My Tuno.
- `Presence/LeaveMyTuno()` – mark user offline.
- `Challenge/Send(targetUserId)`
- `Challenge/Accept(requestId)`
- `Challenge/Decline(requestId)`
- `Match/Join(matchId)`
- `Match/Leave(matchId)`

### Server → Client
- `Presence/OnlineList(users)`
- `Challenge/ChallengeSent(requestId)`
- `Challenge/ChallengeReceived(request)`
- `Challenge/ChallengeAccepted(requestId, matchId)`
- `Challenge/ChallengeDeclined(requestId)`
- `Challenge/Timeout(requestId)`
- `Match/MatchStarted(matchId, seed, attacker, defender)`
- `Match/MatchEvent(matchId, event)`
- `Match/MatchFinished(matchId, outcome, rewards)`

## Deterministic combat engine

### Rules (HP/Power/Speed only)
1. **Initiative**:
   - Calculate `initiative = Speed + rng(0..2)` for each fighter.
   - Higher initiative attacks first.
2. **Rounds**:
   - Max rounds = 30 to avoid infinite battles.
   - Each round: both fighters act once (ordered by initiative).
3. **Damage**:
   - Base damage = Power.
   - Variance = rng(-2..+2).
   - Final damage = max(1, Power + variance).
4. **KO**:
   - If HP <= 0, battle ends immediately.
5. **Rewards**:
   - Winner: XP + Fidelis.
   - Loser: small XP.
   - Reward values are constants in config to keep tuning easy.

### Event / replay schema (versioned)
```json
{
  "version": 1,
  "seed": 123456,
  "events": [
    { "type": "RoundStart", "round": 1 },
    { "type": "Attack", "round": 1, "attackerId": "A", "defenderId": "B", "damage": 7 },
    { "type": "HpChange", "round": 1, "targetId": "B", "hpAfter": 18 },
    { "type": "KO", "round": 3, "winnerId": "A", "loserId": "B" }
  ]
}
```

### Seed usage rules
- Seed generated on server for every battle.
- RNG used only through deterministic wrapper.
- Replay can be seed + inputs or explicit event list; start with event list for easy playback.

## UI pages / components

### My Tuno Home (Dashboard) – `/my-tuno`
- Quick summary: Character stats, Level/XP, Fidelis balance.
- Entry points to Shop, Arena, Live Challenges, Replay history.

### Character Profile – `/my-tuno/character`
- Full stats + Level/XP.
- Shows upgrade counts and next cost per stat.

### Upgrade Shop – `/my-tuno/shop`
- Buttons for HP/Power/Speed upgrades.
- Shows Fidelis balance and cost per upgrade.
- Validates server-side and returns error feedback.

### Arena – `/my-tuno/arena`
- Start async auto battle.
- Recent match history list (async only).

### Live Challenge – `/my-tuno/live`
- Online users list (presence).
- Inbox for incoming challenges (accept/decline).
- Live match status and events.

### Replay Viewer – `/my-tuno/replay/{battleId}`
- Loads ReplayJson and animates sequence.
- Uses minimal re-rendering to be mobile-friendly.

## Step-by-step implementation plan (milestones + tests)

### Milestone 1: Data model + migrations
- Add Character, Battle, ChallengeRequest entities.
- Add concurrency token to ApplicationUser (rowversion).
- Create EF Core mappings + indexes.
- **Test**: `BattleEngine_Determinism_SameSeedSameOutcome`.

### Milestone 2: Combat engine + rewards
- Implement deterministic combat engine with RNG wrapper.
- Implement replay event schema.
- **Test**: `CombatEngine_Determinism_ReplayStable`.

### Milestone 3: Upgrade flow
- Implement UpgradeService with transaction/concurrency-safe Fidelis deductions.
- Add upgrade cost calculator based on upgrade counts.
- **Test**: `UpgradeService_PreventsNegativeFidelis`.

### Milestone 4: Async auto battle
- Implement MatchmakingService and async battle flow.
- Persist Battle records + replay.
- **Test**: `AutoBattle_PersistsBattleAndRewards`.

### Milestone 5: Live challenge + presence (SignalR)
- Presence tracking hub + service.
- Challenge request lifecycle (send/accept/decline/timeout).
- Live match flow and event streaming.
- **Test**: `LiveChallenge_AcceptCreatesBattle`.

### Milestone 6: UI pages + navigation
- Add top-level `/my-tuno` nav entry (not under Games).
- Build pages: dashboard, character, shop, arena, live, replay.
- **Test**: `BlazorRoutes_MyTunoAccessible`.
