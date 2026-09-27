# RTUB After Hours

RTUB After Hours is an English-language, text-first browser crime RPG for authenticated RTUB
members. It lives inside the RTUB application but is a **separate game**: it is not MyTuno, not an
extension of MyTuno, and shares none of its gameplay systems.

Status: foundation (AH-001) plus cycles and per-cycle player state (AH-002). Nothing is playable yet. The
landing page shows the active cycle and the player's starting state, or "No active cycle".

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
