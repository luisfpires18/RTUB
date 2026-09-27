# RTUB After Hours

RTUB After Hours is an English-language, text-first browser crime RPG for authenticated RTUB
members. It lives inside the RTUB application but is a **separate game**: it is not MyTuno, not an
extension of MyTuno, and shares none of its gameplay systems.

Status: foundation only (AH-001). Nothing is playable. The route shows a static landing page.

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
- `FiscalYear` as a year identity (from AH-002)
- `ApplicationDbContext` via `IDbContextFactory`, SQLite, the migrations project (from AH-002)
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
| Entities | `src/RTUB.Core/Entities/AfterHours/` | `RTUB.Core.Entities.AfterHours` | AH-002 |
| Services, interfaces | `src/RTUB.Application/Services/AfterHours/`, `Interfaces/AfterHours/` | `...Services.AfterHours` | AH-002 |
| DbContext partial | `src/RTUB.Application/Data/ApplicationDbContext.AfterHours.cs` | `RTUB.Application.Data` | AH-002 |
| Tests | `tests/RTUB.Integration.Tests/Pages/AfterHoursGateTests.cs`, `tests/RTUB.Web.Tests/Security/AfterHoursAuthorizationTests.cs` | | AH-001 |

Pages must stay in `RTUB.Web`: the router only scans that assembly.

## Visual identity

The final After Hours palette and branding are **not decided yet**. The landing page is kept structural
on purpose: its scoped CSS derives colours from the inherited text colour, so branding can be applied
later without restructuring the page.

## Open item for AH-002: cycle boundaries

`FiscalYear` holds only `StartYear`/`EndYear`, with no dates, and `FiscalYearHelper` works out the current
year from the server's local `DateTime.Today`. Neither is precise enough to be an authoritative
boundary. AH-002 must add an After Hours-specific cycle model linked to `FiscalYear`, with **explicit
UTC start and end instants**, allowing both a `Pilot` and a `Live` cycle for the same `FiscalYear`.
Do not derive authoritative cycle boundaries from `FiscalYearHelper`.
