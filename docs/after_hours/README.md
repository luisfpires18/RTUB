# RTUB After Hours

RTUB After Hours is an English-language, text-first browser crime RPG for authenticated RTUB
members. It lives inside the RTUB application but is a **separate game**: it is not MyTuno, not an
extension of MyTuno, and shares none of its gameplay systems.

Status: foundation (AH-001), cycles and per-cycle player state (AH-002), and the core solo loop (AH-003):
crimes, jail, cover jobs, bank, XP and levels. `/after-hours` is the dashboard (stats, bank);
`/after-hours/crimes` is the crime list and cover job.

**Temporary DEV limitation:** crimes pay cash and XP only. Cargo rewards arrive with AH-004; nothing
cargo-related is modelled or awarded yet.

## Numbering

After Hours has its own unit sequence, independent of RTUB's global `NNN` sequence:

| Item | Convention | Example |
| --- | --- | --- |
| Unit | `AH-NNN` | `AH-001` |
| Branch | `feature/after-hours/NNN-<slug>` | `feature/after-hours/001-foundation` |
| Commit | `AH-NNN: <summary>` | `AH-001: add after hours foundation` |

## Isolation

After Hours **may reuse** RTUB infrastructure:

- identity: `ApplicationUser` (its `Id` is the player key), roles, cookie authentication, authorization
- the app shell (`MainLayout`) and generic shared UI (`RTUB.Shared/Components/Common/*`, `LoadingSpinner`, `Alert`)
- `FiscalYear` as a year identity
- `ApplicationDbContext` via `IDbContextFactory`, SQLite, the migrations project
- options, DI, logging and test conventions

After Hours **must not use**:

- `ApplicationUser.FidelisBalance`, `FidelisRewardsConfiguration`, or any real RTUB finance record
  (`Transaction`, `MbwayTransfer`, `MemberDebt`, `Report`) - game money is After Hours state only
- MyTuno gameplay: `Character`, `InventoryItem`, `ForgedWeapon`, `BossModeProgress`, XP/levels,
  equipment, combat, balances, progression services, `MyTunoScalingConfiguration`, `XpSettings`,
  `GameRewardSettings`, `LevelDefinition`, `pixi/`
- betting state (`Bet`) or the mini-game catalogue scores (`Game`, `GameScore`) as After Hours scoring

MyTuno may be looked at for layout ideas only. Do not copy its CSS: it hard-codes the MyTuno palette.
Do not use a MyTuno page as a template - they inject a dozen MyTuno services.

Game state goes in After Hours tables keyed by `UserId` (and cycle), never in new `ApplicationUser` columns.

## Feature gate

| | |
| --- | --- |
| Configuration key | `AfterHours:Enabled` (App Service: `AfterHours__Enabled`) |
| Options | `RTUB.Application.Configuration.AfterHoursOptions` |
| Default | `false`. A missing section, a missing key or `false` all mean **disabled**. |
| DEV (`rtub-dev`) | `AfterHours__Enabled=true`, set by the owner as an App Service setting |
| PROD (`rtub`) | **unset**. Only set during the final After Hours release. Never in `appsettings.Production.json`. |
| Local | `dotnet user-secrets set --project src/RTUB.Web/RTUB.csproj "AfterHours:Enabled" "true"` |

After Hours code can reach `master` long before the game is released, so hiding the menu is not
enough. The gate is a server-side authorization policy:

- `RTUB.Security.AfterHoursAuthorization.Policy` (`"AfterHours"`) requires an authenticated user
  **and** `AfterHoursEnabledRequirement`, satisfied by `AfterHoursEnabledHandler` reading
  `IOptionsMonitor<AfterHoursOptions>` on every evaluation.
- `src/RTUB.Web/Pages/AfterHours/_Imports.razor` applies `[Authorize(Policy = ...)]` to every page
  in that folder and its subfolders. Never put an After Hours page anywhere else, and never override
  the attribute on a page.
- The `Jogos` menu entry in `MainLayout.razor` uses `<AuthorizeView Policy="...">` with the same
  policy, so the menu and the route cannot disagree. No component checks `AfterHoursOptions` itself.

Behaviour: an anonymous visitor is redirected to `/login?ReturnUrl=...`. A signed-in user, of any
role, is refused while the game is disabled: endpoint authorization forbids the request and the
cookie handler redirects to `AccessDeniedPath` (`/login`), the same as any other refused page in RTUB.
Blazor navigation goes over HTTP (the router is not interactive), so the gate runs on every navigation.

### Rules for later units

- **HTTP endpoints and hubs.** Any After Hours controller, minimal API or SignalR hub/method must
  carry `[Authorize(Policy = AfterHoursAuthorization.Policy)]` (or `.RequireAuthorization(...)`).
  Page policy does not cover them.
- **Background jobs.** Hosted services (rollover, settlement, schedules) are not behind any policy.
  They must check `IOptionsMonitor<AfterHoursOptions>.CurrentValue.Enabled` and do nothing while disabled.
- **Services** are only reachable from gated pages today. If one becomes reachable another way,
  gate that entry point, not every caller.

## Folder ownership

| Layer | Path | Namespace | From |
| --- | --- | --- | --- |
| Options | `src/RTUB.Application/Configuration/AfterHoursOptions.cs` | `RTUB.Application.Configuration` | AH-001 |
| Gate | `src/RTUB.Web/Security/AfterHoursAuthorization.cs` | `RTUB.Security` | AH-001 |
| Pages | `src/RTUB.Web/Pages/AfterHours/` | `RTUB.Pages.AfterHours` | AH-001 |
| Page components | `src/RTUB.Web/Pages/AfterHours/Components/` | `RTUB.Pages.AfterHours.Components` | later |
| Entities, enums | `src/RTUB.Core/Entities/AfterHours/`, `src/RTUB.Core/Enums/AfterHours/` | `RTUB.Core.Entities.AfterHours`, `RTUB.Core.Enums.AfterHours` | AH-002 |
| Services, interfaces | `src/RTUB.Application/Services/AfterHours/`, `Interfaces/AfterHours/` | `...Services.AfterHours` | AH-002 |
| DbContext partial | `src/RTUB.Application/Data/ApplicationDbContext.AfterHours.cs` | `RTUB.Application.Data` | AH-002 |
| EF mapping | `src/RTUB.Application/Data/Configurations/AfterHours/` | `RTUB.Application.Data.Configurations.AfterHours` | AH-002 |
| Tests | `tests/RTUB.Integration.Tests/Pages/AfterHoursGateTests.cs`, `tests/RTUB.Web.Tests/Security/AfterHoursAuthorizationTests.cs` | | AH-001 |
| Tests | `tests/RTUB.Integration.Tests/Application/AfterHoursCycleStateTests.cs`, `tests/RTUB.Core.Tests/Entities/AfterHours/` | | AH-002 |
| Pure rules | `src/RTUB.Core/Helpers/AfterHours/` (levels, crime catalogue and odds, jail policy, actions) | `RTUB.Core.Helpers.AfterHours` | AH-003 |
| Tests | `tests/RTUB.Integration.Tests/Application/AfterHoursActionTests.cs`, `tests/RTUB.Core.Tests/Entities/AfterHours/AfterHoursRulesTests.cs` | | AH-003 |

Pages must stay in `RTUB.Web`: the router only scans that assembly.

## Visual identity

The final After Hours palette and branding are **not decided yet**. The landing page is kept structural
on purpose: its scoped CSS derives colours from the inherited text colour, so branding can be applied
later without restructuring the page.

## Cycles

`GameCycle` (table `AfterHoursGameCycles`) is one playable period.

- **Year identity** comes from `FiscalYearId`. A fiscal year may hold any number of cycles, for example
  a Pilot and a Live one. Cycles are finished, never deleted: they are history.
- **Kind**: `Pilot` or `Live` (`GameCycleKind`).
- **Boundaries**: `StartUtc` (inclusive) and `EndUtc` (exclusive), exact UTC instants stored on the cycle.
  The academic year runs September to August in Europe/Lisbon, but that is presentation only. Authoritative
  boundaries are never derived from `FiscalYear`, `FiscalYearHelper` or `DateTime.Today`.
  `GameCycle.Create` rejects non-UTC values and an end that is not after the start. The database rejects
  the latter too (`CK_AfterHoursGameCycles_EndAfterStart`).
- **Status**: `Scheduled` → `Active` → `Finished` (`GameCycleStatus`). Transitions are explicit calls on
  `IGameCycleService`; there is no automatic start, rollover or background job.
- **At most one Active cycle**: filtered unique index `IX_AfterHoursGameCycles_SingleActive`.
  The *playable* cycle is the Active one while `StartUtc <= now < EndUtc`; an Active cycle outside its
  boundaries is not playable. `IGameCycleService.GetActiveCycleAsync` returns null when nothing is playable.
- Application-enforced only: the order of status transitions. Overlap between cycles' boundaries is not
  constrained; only the single-Active rule decides which cycle is authoritative.

## Player cycle state

`PlayerCycleState` (table `AfterHoursPlayerCycleStates`) is a player's annual gameplay power for one cycle.
Identity (`ApplicationUser`) and history stay outside it; a new cycle starts a new row.

- **One row per cycle and user**: unique index `IX_AfterHoursPlayerCycleStates_Cycle_User`.
- **Starting values** (constants on the entity, never taken from a client): Level 1, XP 0, wallet 400,
  bank 0, energy 240 of max 240, heat 0, Toughness/Stealth/Smarts/Charisma rank 4.
- **Cash** is After Hours money only, stored as whole units (`long`). Check constraints keep wallet,
  bank, energy and heat non-negative.
- **Energy and heat** are stored as "value as of timestamp" (`Energy` + `EnergyUpdatedAtUtc`,
  `Heat` + `HeatUpdatedAtUtc`, UTC). Regeneration and decay are computed from elapsed time and written back
  when the value changes; nothing ticks them in the background.
- **Creation**: `IPlayerCycleStateService.GetOrCreateForActiveCycleAsync(userId)` is idempotent. When two
  tabs create at once, the unique index rejects the second insert and the service returns the row that won.
  With no playable cycle it returns null and creates nothing, neither a state nor a cycle.
- Excluded from the audit log, like other gameplay state. Cycle records are audited.
- A cycle with player states cannot be deleted (`Restrict`); deleting a user deletes their states (`Cascade`).

## Time: energy and heat

All time comes from `TimeProvider` (registered as `TimeProvider.System`); business code never reads
`DateTime.UtcNow`. State is reconciled on every load and inside every action, by the same code
(`PlayerCycleState.Reconcile`); reads return a reconciled copy and write nothing.

- **Energy**: +1 per whole 6 minutes, up to `MaxEnergy` (240, so empty to full is 24 h). The
  timestamp advances only by the whole intervals used, so a partial interval carries over (13 min →
  +2, 1 min kept). At max energy the timestamp is pinned to now: a full bar banks no hidden reserve.
- **Heat**: −1 per whole 10 minutes, never below 0, same carry-over rule; at 0 the timestamp is pinned
  to now. Crimes are refused at heat ≥ 80 (after reconciliation).

## XP and levels

Cumulative XP to reach level L is `12·(L−1)³ + 88·(L−1)` (L2 100, L5 1,120, L10 9,540, L20 83,980),
capped at level 20; XP keeps accumulating at the cap. `AfterHoursLevels` is the only implementation.
The manual's table disagrees with the formula from L8 up; the owner chose the formula (AH-003).

## Crimes

Catalogue C01–C12 from Game Manual v2 lives in `CrimeCatalogue` (server-owned). A request carries only
a crime id and an approach; cost, chance, rewards, heat and skill are always read server-side.

- Chance = base + 2·(skill − 4) − 2·⌊heat / 10⌋ + approach, clamped 15–95. Roll 1–100; success when
  roll ≤ chance.
- Approaches: Careful +8 pts, cash ×0.8, heat ½ rounded up; Standard as listed; Bold −8 pts, cash
  ×1.25, XP ×1.1, heat +4.
- Rounding (`CrimeRules`): integer arithmetic, half up (35 × 1.25 → 44, 26 × 1.1 → 29, 26 × 0.25 → 7).
- Success: −energy, +cash, +XP, +heat. Failure: −energy, +25% of the adjusted XP, no cash, +heat,
  then a jail roll.
- Refused when: level too low, in jail, heat ≥ 80, not enough energy.

### Jail — AH-003 implementation choice (not in Game Manual v2)

The manual gives no numbers. `JailPolicy` holds the temporary formulas, using the heat the crime was
attempted at:

- chance on a failed crime: `10 + RequiredLevel + ⌊heat / 5⌋` %, clamped 10–60;
- duration: `2 + ⌊RequiredLevel / 5⌋ + ⌊heat / 20⌋` minutes, at most 8.

Stored as `PlayerCycleState.JailUntilUtc`; the player is free once it passes, with no job. Jail blocks
crimes and cover jobs (the latter is also an AH-003 choice); the bank stays usable.

## Cover jobs

Manual: 10 energy, −15 heat (never below 0). **AH-003 choices:** +20 cash, +12 XP; "high heat" is
heat ≥ 50. At high heat a cover job can be repeated; below 50 it is limited to one per UTC calendar day,
tracked in `PlayerCycleState.CoverJobDailyUsedOn`. High-heat jobs do not use up the daily one.

## Bank

Wallet cash is exposed (to future PvP); bank cash is protected. Deposit: wallet −amount, fee
⌈2% of amount⌉, bank +(amount − fee); refused if the bank would get nothing. Withdrawal: bank −amount,
wallet +amount, **no fee (AH-003 choice)**. Only After Hours columns are touched: never Fidelis, finance
`Transaction`, `MemberDebt` or MB Way.

## Actions: receipts, idempotency, concurrency

`IAfterHoursActionService` (crime, cover job, deposit, withdraw) is the only way state changes.

- **Idempotency key.** Every request carries a client key (≤ 64 chars). The pages create one per
  attempt and keep it until the attempt completes, so a double submit replays rather than repeats.
- **Receipt.** An accepted action writes a `PlayerActionReceipt` (`AfterHoursPlayerActionReceipts`)
  with the request, dice roll and chance, jail result and every delta, in the **same transaction** as
  the state change. A retry with the same key returns that receipt (`Replayed`), without running the
  rules or rolling dice again. The same key with a different request is refused. Unique index
  `(PlayerCycleStateId, IdempotencyKey)` is the backstop. Refusals write nothing and have no receipt.
- **Concurrency.** Each action is one SQLite write transaction (`BEGIN IMMEDIATE`, the default for
  Microsoft.Data.Sqlite): receipt lookup, state load, reconciliation, rules, dice, update and receipt
  insert all run under the database write lock, so tabs and devices are serialized and each sees the
  previous result — no double spend, no lost update, no negative balance (the check constraints stay as
  a last line). Lock contention (`SQLITE_BUSY`/`LOCKED`) or a lost unique race rolls back and retries the
  whole action; nothing is committed before that.
- **Dice** (`IAfterHoursDice`) are server-side only, never seeded or influenced by a client.
- Receipts are excluded from the audit log.
