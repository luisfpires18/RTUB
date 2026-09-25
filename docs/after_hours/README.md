# RTUB After Hours

RTUB After Hours is an English-language, text-first browser crime RPG for authenticated RTUB
members. It lives inside the RTUB application but is a **separate game**: it is not MyTuno, not an
extension of MyTuno, and shares none of its gameplay systems.

Status: foundation (AH-001), cycles and per-cycle player state (AH-002), the core solo loop (AH-003):
crimes, jail, cover jobs, bank, XP and levels, cargo with buyer contracts (AH-004), and skill training
with equipment (AH-005), PvP (AH-006), families (AH-007), objectives with championships (AH-008), and the
yearbook with cycle rollover (AH-009). `/after-hours` is the dashboard (stats, bank); `/after-hours/crimes` is the crime
list and cover job; `/after-hours/cargo` is the cargo inventory, the fence and the buyer contracts;
`/after-hours/training` and `/after-hours/equipment` are training and gear; `/after-hours/pvp` is PvP
(status, defence, targets, attack setup, recent battles) and `/after-hours/pvp/report/{id}` a stored battle;
`/after-hours/family` is the family page; `/after-hours/objectives` and `/after-hours/leaderboards` are
objectives and championship standings; `/after-hours/yearbook` is the archive of finished cycles.

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
| Tests | `tests/RTUB.Integration.Tests/Application/AfterHoursCargoTests.cs`, `tests/RTUB.Core.Tests/Entities/AfterHours/AfterHoursCargoRulesTests.cs` | | AH-004 |
| Tests | `tests/RTUB.Integration.Tests/Application/AfterHoursTrainingGearTests.cs`, `tests/RTUB.Core.Tests/Entities/AfterHours/AfterHoursTrainingGearRulesTests.cs` | | AH-005 |
| Tests | `tests/RTUB.Integration.Tests/Application/AfterHoursPvpTests.cs`, `tests/RTUB.Core.Tests/Entities/AfterHours/AfterHoursPvpRulesTests.cs`, `AfterHoursPvpPagesTests` in `Pages/AfterHoursGateTests.cs` | | AH-006 |
| Tests | `tests/RTUB.Integration.Tests/Application/AfterHoursFamilyTests.cs`, `tests/RTUB.Core.Tests/Entities/AfterHours/AfterHoursFamilyRulesTests.cs`, `AfterHoursFamilyPagesTests` in `Pages/AfterHoursGateTests.cs` | | AH-007 |
| Tests | `tests/RTUB.Integration.Tests/Application/AfterHoursObjectiveTests.cs`, `tests/RTUB.Core.Tests/Entities/AfterHours/AfterHoursObjectiveRulesTests.cs`, `AfterHoursObjectivePagesTests` in `Pages/AfterHoursGateTests.cs` | | AH-008 |
| Tests | `tests/RTUB.Integration.Tests/Application/AfterHoursRolloverTests.cs`, `tests/RTUB.Core.Tests/Entities/AfterHours/AfterHoursRolloverRulesTests.cs`, `AfterHoursYearbookPagesTests` in `Pages/AfterHoursGateTests.cs` | | AH-009 |

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
  `IGameCycleService`, or a rollover (`IAfterHoursRolloverService`, AH-009) that archives a cycle as it finishes
  it; there is no automatic start, rollover or background job. `IGameCycleService.FinishAsync` is the raw
  primitive and archives nothing: finish real cycles through the rollover service.
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
- Success: −energy, +cash, +XP, +heat, +the crime's cargo. Failure: −energy, +25% of the adjusted XP,
  no cash, no cargo, +heat, then a jail roll.
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

`IAfterHoursActionService` (crime, cover job, deposit, withdraw, fence sale, contract delivery, training,
gear purchase, equip, unequip, save defence, PvP attack, and the family actions) is the only way player
state changes. All After Hours writes, including contract rotation, go through
`AfterHoursWriteTransaction`.

- **Idempotency key.** Every request carries a client key (≤ 64 chars). The pages create one per
  attempt and keep it until the attempt completes, so a double submit replays rather than repeats.
- **Receipt.** An accepted action writes a `PlayerActionReceipt` (`AfterHoursPlayerActionReceipts`)
  with the request, dice roll and chance, jail result and every delta (including the one cargo type it
  moved, `CargoType` + `CargoDelta`; the skill trained and its new rank, `Skill` + `SkillRankAfter`; the
  gear item bought or (un)equipped, `GearKey`), in the **same transaction** as
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

## Cargo

Game Manual v2. Stable ids: `CargoType` (stored as int). Prices are server-owned (`CargoCatalogue`).

| Cargo | Base fence price |
| --- | ---: |
| Phone | 15 |
| Electronics | 30 |
| Ticket bundle | 20 |
| Spirits | 25 |
| Art piece | 80 |

Crime cargo (successes only): C01 1 phone · C02 none · C03 1 electronics · C04 1 ticket bundle ·
C05 none · C06 2 spirits · C07 2 electronics · C08 3 electronics · C09 3 spirits · C10 3 ticket bundles ·
C11 4 electronics · C12 2 art pieces. The award is part of the crime's transaction and receipt, so a
replayed key never adds cargo twice.

**Persistence:** `PlayerCargo` (`AfterHoursPlayerCargo`), loaded as `PlayerCycleState.Cargo`. One row
per state and type (unique index `IX_AfterHoursPlayerCargo_State_Type`), quantity never negative (check
constraint). Cargo belongs to the cycle's state, so a new cycle starts empty. No trading.

**Fence** (always available): the player picks a type and quantity; the server pays quantity × base
price. Refused for zero, negative or more than owned. Receipt kind `FenceSale`.

## Buyer contracts

NPC buyers want a quantity of one cargo for cash and XP. A `BuyerContract`
(`AfterHoursBuyerContracts`) belongs to a cycle and a rotation window, and is offered to every player of
that cycle. Its name, cargo, quantity and rewards are copied from the template when created. Completion is
**per player**: `BuyerContractCompletion` (`AfterHoursBuyerContractCompletions`), unique per contract and
player state. One player delivering never uses the contract up for anyone else.

**Delivery** (receipt kind `ContractDelivery`) checks, in one transaction: the contract is in the
player's cycle, now is inside `[AvailableFromUtc, ExpiresAtUtc)`, the player has not delivered it,
and holds enough cargo. It then removes the cargo, pays cash and XP (level from the AH-003 curve),
records the completion and the receipt.

### Rotation — AH-004 implementation defaults (not Game Manual v2)

The manual only says contracts rotate. `BuyerContractRules` holds the defaults:

- windows of **12 hours**, aligned to 00:00 and 12:00 UTC;
- **3 contracts** per window, each expiring at the end of its window;
- slot k of window w uses template `(3·w + k) mod 10`: deterministic, all ten come round in turn.

Rotation is lazy and persisted: the first request in a window creates its three rows in a write
transaction; every later request reads them. The unique index
`IX_AfterHoursBuyerContracts_Cycle_Window_Slot` stops concurrent first visits creating duplicates. No
background job.

### Templates — AH-004 implementation defaults

The Midnight Collector is the manual's example; the others were chosen for AH-004 with conservative
premiums (about 27–33% over the fence value).

| Key | Buyer | Wants | Cash | XP | Fence value |
| --- | --- | --- | ---: | ---: | ---: |
| T01 | The Midnight Collector | 4 art pieces | 460 | 80 | 320 |
| T02 | Pawnshop Pedro | 3 phones | 60 | 15 | 45 |
| T03 | The Repair Stall | 5 phones | 100 | 25 | 75 |
| T04 | Night Market Vendor | 4 electronics | 155 | 30 | 120 |
| T05 | The Crypto Kid | 8 electronics | 310 | 55 | 240 |
| T06 | The Ticket Tout | 3 ticket bundles | 80 | 20 | 60 |
| T07 | Festival Promoter | 6 ticket bundles | 155 | 35 | 120 |
| T08 | The Bar Owner | 4 spirits | 130 | 25 | 100 |
| T09 | Wedding Caterer | 8 spirits | 255 | 45 | 200 |
| T10 | The Gallery Fixer | 2 art pieces | 210 | 40 | 160 |

## Training

**Game Manual v2 rules:** one training point per day, at most 3 stored. Raising a skill by one rank
costs 1 point, 20 energy and `120 + 40·(rank − 4)` **wallet** cash (never bank). Skills cap at
`min(12, 4 + ⌊level / 2⌋)`: level 1 → 4, level 4 → 6, level 12 → 10, level 16+ → 12. Training is
refused with no point, too little energy, too little wallet cash, or the skill at its cap
(`TrainingRules`, `AfterHoursActions.TrainSkill`).

**Reconciliation (AH-005 choice):** `PlayerCycleState.TrainingPoints` is the stored count as of
`TrainingPointsDay`, a calendar day in **Europe/Lisbon** (`LisbonCalendar`, IANA zone, daylight saving
included). Like energy and heat it is reconciled lazily on every read and inside every action: each
Lisbon day that has started since `TrainingPointsDay` adds one point, up to 3, and the day moves to
today, so a day can never grant twice. A new cycle state starts with **1 point, anchored to its creation
day** (today's point, no backlog). States created before AH-005 have no day yet; their first
reconciliation grants 1 point and anchors to that day. The count is kept within 0–3 by the rules;
there is no database check (adding one would rebuild the state table).

## Equipment

Three slots: Weapon, Outfit, VehicleTool (`GearSlot`; named "gear" to stay apart from MyTuno's
`EquipmentSlot`). Names, tiers and unlock levels follow the manual; **prices are AH-005 defaults** (the
manual gives ranges), tunable in `GearCatalogue`. The catalogue is server-owned and never persisted.

| Tier | Unlock level | Weapon | Outfit | Vehicle / tool |
| --- | ---: | --- | --- | --- |
| 1 | 3 | Brass Knuckles — 400 | Hooded Jacket — 500 | Lockpick Kit — 600 |
| 2 | 10 | Switchblade — 2,000 | Armored Jacket — 2,750 | Modified Scooter — 3,500 |
| 3 | 16 | Compact Pistol — 9,000 | Tailored Protection — 12,000 | Getaway Car — 15,000 |
| 4 | 20 | Collector Weapon — 40,000 | Reinforced Suit — 50,000 | Specialist Rig — 60,000 |

- **Owning:** `PlayerGear` (`AfterHoursPlayerGear`), one row per state and item (unique index
  `IX_AfterHoursPlayerGear_State_Item`), with slot and tier copied from the catalogue. Bought with wallet
  cash only, once per cycle; it stays owned for the cycle whether equipped or not.
- **Equipping:** `PlayerCycleState.EquippedWeaponKey` / `EquippedOutfitKey` / `EquippedVehicleToolKey`,
  one column per slot, so at most one item per slot by construction. Equip replaces the slot's item;
  unequip empties it; only owned items can be equipped. A purchase auto-equips when its slot is empty
  (AH-005 choice).
- **For PvP (AH-006):** strength reads `GearCatalogue.BestOwnedTiers` — the best tier **owned** per slot,
  ignoring what is equipped. No PvP power is computed yet.

## PvP

`/after-hours/pvp`. Rules in `PvpRules`, resolution in `AfterHoursActions.Attack`, reads in `IPvpService`.

### Game Manual v2 rules

- Open PvP: targets do not consent and can be offline; attacking yourself is refused.
- Only **capped wallet cash** and **capped cargo** can be stolen. Bank, XP, equipment, account data and
  history are never touched.
- **New-player protection:** 72 h from the state's creation (`CreatedAt`, set from the server clock), or
  until the player's **first accepted** attack, whichever comes first. A refused attack or opening the page
  keeps it. `PvpInitiatedAtUtc` is the only thing stored for it. States created before AH-006 use their
  real `CreatedAt`.
- A **defender who loses** gets 6 h of protection. The same attacker can hit the same target once per
  24 h (accepted attacks only; the stored battles are the history). Jail blocks starting attacks.
  Losing puts a player in **Recovery**, which blocks their own attacks but not crimes.
- **Effective power** = Toughness + Stealth + Smarts + Charisma + 2 × weapon tier + outfit tier +
  vehicle/tool tier, over the **best owned** tier per slot (`GearCatalogue.BestOwnedTiers`), never the
  equipped items, so unequipping cannot make anyone look weak. Used to compare strength and to scale loot.
- **Loot** (attacker wins only), exactly as the manual writes it, in decimal:
  ratio = defender power / attacker power; multiplier = min(1, ratio × ratio), with no lower bound and
  no rounding (the battle stores the full decimal); wallet = ⌊min(wallet × 0.10, 500) × multiplier⌋;
  cargo budget = ⌊min(cargo base value × 0.20, 250) × multiplier⌋. **Only the final wallet amount and
  cargo budget are floored.** The budget is spent on whole items, highest base price first (ties by
  `CargoType` order), while each full price fits. Cargo moves as cargo, never as cash; tiny holdings can
  yield nothing.
- **Battle:** three rounds with a chosen tactic and risk stance; the defender uses a saved defence.
  Ambush > Negotiation > Setup > Counterattack > Ambush; other pairs are neutral.
- Every attack is **atomic and idempotent** (below).

### AH-006 implementation defaults (not in the manual; tune in `PvpRules`)

- Attack costs **20 energy**. After every accepted attack the attacker waits **5 minutes** (cooldown).
- A beaten defender recovers for **15 minutes**; a beaten attacker for **10 / 15 / 30 minutes**
  (Cautious / Standard / Reckless).
- **Tactic specialisation:** Ambush (Stealth − 4) + (Toughness − 4); Negotiation (Charisma − 4) +
  (Smarts − 4); Counterattack (Toughness − 4) + selected gear tiers; Setup (Smarts − 4) + selected gear tiers.
- A favourable matchup is **+4** to that side's round score (nothing is subtracted from the other).
- **Risk stance** adds −2 / 0 / +2 to every attacker round score. It does not change loot.
- **Round:** score = loadout power (the effective-power formula over the gear actually taken) +
  specialisation + matchup (+ risk for the attacker) + a random factor **−2..+2**. The higher score deals
  10 + the difference, capped at 30; a tie deals 1 each. Higher total damage wins; equal totals go to one
  50/50 roll. The dice are the existing 1–100 `IAfterHoursDice`: factor = `(roll − 1) mod 5 − 2`
  (uniform), tie-break = attacker on roll ≤ 50.
- **No counter-loot:** a winning defender takes nothing from the attacker.
- **No PvP XP or annual score yet:** those belong to AH-008 (objectives and championships). Reports show
  outcome, rounds, loot, protection and recovery only.
- The defender has no risk stance. A player who never saved a defence fights with **Counterattack** and
  whatever is **equipped at that moment**; a saved defence (`DefenceTactic` + three keys on the state) is
  used as saved, even after equipment changes. Saving costs nothing and is an action with a receipt.

### Persistence

`PvpBattle` (`AfterHoursPvpBattles`) is written once, in the attack's transaction, and never changed: both
players and states, time, tactics, stance, both loadouts (keys and tiers, whether the defence was saved),
effective and loadout powers, specialisation, matchup and risk figures, total damage, tie-break roll,
winner, loot multiplier, cash taken, and every recovery, protection and cooldown it applied. Its three
`PvpBattleRound` rows keep each round's random factors, scores and damage; `PvpBattleCargo` rows keep the
cargo moved. A report is rendered from these rows alone: nothing is recalculated or rerolled.
`PvpBattle.ReceiptId` (unique) links it to the attacker's receipt; the link lives on the battle so the
receipts table was not altered.

**Reports** (`/after-hours/pvp/report/{id}`) are visible to the attacker and the defender only; anyone else
gets "Report not found", the same as for a missing battle. The attacker is always the signed-in user; the
target's state id is the only player identity a request carries. Target lists show name, level and
effective power, never cash or cargo.

### Atomicity, idempotency, concurrency

An attack is one `IAfterHoursActionService` action: inside one `AfterHoursWriteTransaction` it loads both
players, reconciles the attacker, checks every restriction (before any energy is spent or die rolled),
validates the chosen loadout (owned, right slot, tiers from the catalogue), resolves the defender's setup,
spends energy, ends the attacker's new-player protection if this is their first attack, rolls, moves
wallet and cargo, applies recovery, protection and cooldown, and inserts the battle and the receipt.
Nothing is visible until that commits; any failure rolls all of it back.

The receipt's request fingerprint holds the target, tactic, stance and loadout. The same key returns the
same battle and changes nothing (no energy, dice, loot, or new timers); the same key with any change is
refused. Because attacks serialize on the write lock, the second of two racing attacks sees the first:
cooldown, a defender's new protection, the 24-hour history and spent energy all hold. Saving a defence
writes its four fields in one update, so a mix of two saves cannot happen.

## Families

Game crews only: unrelated to real RTUB mentors/padrinhos, RTUB roles, MyTuno or any real membership.
Reads in `IFamilyService`; every change is an `IAfterHoursActionService` action (one write transaction
with a receipt; the receipt records `FamilyId`). Rules in `FamilyRules`.

### Game Manual v2 rules

- At most **4 active members**.
- Creating a family needs **level 5** and **1,500 cash**.
- Members donate to the family **treasury**.
- After **leaving**, a player waits **72 hours** before joining or creating a family (from exactly
  `LeftAtUtc + 72 h`). Declining an invitation starts no cooldown.
- Roles **Boss, Enforcer, Fixer, Member**. The Boss edits name and motto, sends and cancels invitations,
  sets roles and transfers leadership. Enforcer and Fixer carry no permissions yet: they are reserved for
  heists and family objectives in later units.
- **Family identity and membership persist** across academic years; **treasury is per cycle** and resets.

### Model

- **Persistent** (no cycle foreign key): `Family` (`AfterHoursFamilies`; unique `NormalizedName` =
  trimmed, upper-invariant name), `FamilyMembership` (`AfterHoursFamilyMemberships`; history rows with
  `JoinedAtUtc` / `LeftAtUtc`, active = no `LeftAtUtc`), `FamilyInvitation` (`AfterHoursFamilyInvitations`;
  status and resolution time kept as history).
- **Annual:** `FamilyCycleState` (`AfterHoursFamilyCycleStates`), one row per family and cycle, created
  lazily with a 0 treasury (check constraint ≥ 0). This is the rollover boundary: later upgrades and
  family score belong here too. A family entering a new cycle gets a new row; its identity and
  memberships are untouched. Nothing about families is stored on `PlayerCycleState`.
- **Database guarantees:** one active membership per user (filtered unique index); at most one active
  Boss per family (filtered unique index; the transfer demotes, saves, then promotes inside one
  transaction); one pending invitation per family and player (filtered unique index); unique name.
  The 4-member cap is checked under the write lock at acceptance.
- The leave cooldown is derived from the latest `LeftAtUtc` in the membership history, so it survives
  cycle changes.
- **For AH-008:** a family's id never changes and memberships keep their dates, so points can be attributed
  to the family a player belonged to when they earned them. No score columns or tables exist yet.

### Flows

- **Create:** level 5, not in a family, no cooldown, valid unique name, 1,500 in the wallet → wallet −1,500,
  family, Boss membership, this cycle's treasury row, receipt.
- **Invite** (Boss only): a player with a state in the current cycle, not yourself, not in any family, no
  pending invitation from your family. Several families may invite the same player. Invitations do not
  reserve a place.
- **Accept:** checked at that moment: still pending, family not disbanded, not in a family, no cooldown,
  fewer than 4 active members. Joins as Member, opens this cycle's treasury row if needed, marks the
  invitation accepted and **cancels the player's other pending invitations**. Decline and Boss cancel
  only close that invitation.
- **Roles:** the Boss sets another active member to Member, Enforcer or Fixer; never to Boss (that is
  Transfer Boss) and never their own role. **Transfer Boss:** the chosen member becomes Boss and the old
  Boss becomes Member, so there is always exactly one Boss.
- **Leave:** members leave freely (history kept, cooldown starts). A Boss with other members must
  transfer first.
- **Donate:** any member, amount ≥ 1, from the wallet, into this cycle's treasury.

### AH-007 implementation defaults (not in the manual)

- The 1,500 creation cost and all donations come from the **wallet**; the bank is never used. The manual
  only says "cash".
- Names 3–24 characters, mottos optional and at most 120, both trimmed; no profanity filter.
- Invitations **do not expire**.
- A Boss who is the **last member** may leave: that **disbands** the family (`DisbandedAtUtc`), cancels its
  pending invitations, keeps all history and cycle rows, and starts the Boss's cooldown. Families are never
  deleted, a disbanded family takes no members, and its **name stays reserved**.
- No donation fee, no withdrawals or refunds.

### Source gap: family upgrades

The manual says the treasury "buys family upgrades" but gives no upgrade list, costs, levels or effects.
AH-007 therefore stores the treasury correctly on the annual `FamilyCycleState` and **does not invent any
upgrade or bonus**. Upgrades will attach to that row once their values are decided. Heists, family
objectives, scoring, leaderboards and PvP family bonuses are later units.

## Objectives and championships

`/after-hours/objectives` and `/after-hours/leaderboards`. Catalogue and scoring in `ObjectiveCatalogue`,
action-to-progress rules in `ObjectiveRules`, persistence in `ObjectiveTracker`, reads in `IObjectiveService`.

### Game Manual v2 rules

- Three layers: **daily** objectives (small rewards, no championship points), **weekly individual**
  objectives and **weekly family** objectives. Objectives complete and pay out automatically: there is no
  claim button.
- Two championships, **Individual** and **Family**. Neither scores raw XP, cash or level: each scores
  weekly objective points, and the annual score is the **sum of the best 12 weekly scores** (all of them
  when there are fewer than 12). The running week counts provisionally.
- Individual weekly caps: **40** solo crime, **20** cargo/contracts, **20** PvP, **20** family
  participation, **100** in total.
- PvP scoring guards against farming weak targets, and **same-family** PvP scores nothing.
- **Family points stay with the family where they were earned.** Leaving or switching family moves nothing.

### How progress is recorded

Progress only ever comes from accepted actions. The action service calls `ObjectiveTracker` for every
accepted action, inside that action's write transaction, after the rules have accepted it and before its
single save. The tracker reads what the action did from its receipt (and battle): crimes (success, cash,
energy), cover jobs (energy; heat actually reduced), training energy, fence sales, contract deliveries,
PvP battles and family donations. Bank moves, gear, and the family creation cost count for nothing.
Because it runs in the same transaction, progress, completion, XP and points commit or roll back with
the action; a replayed key returns before the tracker runs, and a refused action never reaches it. The
family is the one the player belongs to **at that moment, under the write lock**, and its id is stored on
the family's rows.

**Persistence** (target, XP and points are copied when a row is created and never recalculated, so
tuning the catalogue never changes history):

- `PlayerObjectiveProgress` (`AfterHoursPlayerObjectiveProgress`): state, daily or weekly, the Lisbon
  day number or the week index, objective key; unique per that instance. Progress stays within 0..target.
- `FamilyObjectiveProgress` (`AfterHoursFamilyObjectiveProgress`): family, cycle, week, objective key;
  unique.
- `PvpObjectiveCredit` (`AfterHoursPvpObjectiveCredits`): one row per qualifying attacker win. Filtered unique
  indexes allow one individual credit per attacker and target per week, and one family credit per family
  and target per week.

Daily completion awards its XP to the player at once (normal XP and level rules; no catch-up). Weekly
completion stores its points. Scores and standings are computed from stored awarded points when read;
there is no separate annual total column.

**Rollout:** tracking starts with AH-008. Actions accepted before it, including earlier DEV testing,
are not backfilled.

### AH-008 implementation defaults (not in the manual)

- **Game weeks:** week 1 starts at `GameCycle.StartUtc`; each week is the next 7 days as a UTC [start, end)
  interval; the last one is cut short by the cycle end. No calendar or ISO weeks.
- **Daily objectives** reset at Lisbon midnight. Each day shows 3 of the 5, the same for every player:
  templates `DayNumber mod 5` and the next two. Rewards are XP only (no cash, deliberately):

  | Key | Objective | Target | XP |
  | --- | --- | ---: | ---: |
  | D01 | Working Night: successful crimes | 3 | 15 |
  | D02 | Cool It: a cover job that reduces heat | 1 | 10 |
  | D03 | Burn the Clock: energy spent on crimes, cover jobs, training, PvP | 60 | 15 |
  | D04 | Move the Goods: cargo units fenced or delivered | 1 | 10 |
  | D05 | Settle a Score: a PvP win as attacker or defender | 1 | 15 |

- **Weekly individual sets** alternate: odd weeks A, even weeks B.

  | Category | Set A | Set B | Points |
  | --- | --- | --- | ---: |
  | Solo crime | 12 successful crimes | 15 successful crimes | 20 |
  | Solo crime | 1,500 cash from successful crimes | 240 energy spent on crimes | 20 |
  | Cargo/contracts | fence 10 cargo units | complete 2 buyer contracts | 20 |
  | PvP | qualified PvP points (below) | same | up to 20 |
  | Family participation | donate 1,000 to your family | 10 successful crimes while in a family | 20 |

- **Individual PvP points:** only attacker wins; the target must be outside the attacker's family at battle
  time and not new-player protected; a target counts once per attacker per week; each counts
  **⌊10 × the battle's loot multiplier⌋** (so weaker targets are worth less), and the category stops at 20.
  PvP loot itself is unchanged.
- **Family weekly sets** alternate with the same parity; each objective is worth 25, the family cap is **100**:

  | Set A | Set B |
  | --- | --- |
  | members complete 30 successful crimes | members complete 40 successful crimes |
  | donate 3,000 to the treasury | donate 2,000 to the treasury |
  | fence 20 cargo units | complete 4 buyer contracts |
  | 4 qualified PvP wins on distinct outside targets | same |

  A family PvP win needs an attacker win, the attacker in the family and the target outside it at battle
  time, and a target not new-player protected. A target counts once per family per week. Power
  difference does not change the family count.
- The family score counts **family objectives only**, never the sum of members' individual scores.
- **Ties:** equal annual scores share a rank (1, 1, 3); names only order the display. Nobody is declared
  champion on the live board: champions are decided when a cycle is archived (AH-009, below).

### Not in AH-008

Catch-up XP (manual section 20) is carried to AH-010. Heist objectives wait for heists. Yearbook and
rollover are AH-009; award administration is AH-010.

## Yearbook and rollover

`/after-hours/yearbook` (read-only, `IYearbookService`); transitions in `IAfterHoursRolloverService`; rules in
`RolloverRules`.

### Canonical rules

- **Power resets, identity persists.** A cycle's gameplay state is its own rows: `PlayerCycleState` (level,
  XP, skills, wallet, bank, energy, heat, jail, PvP timers, training, defence), its gear and cargo, receipts,
  objective progress, PvP credits, buyer contracts and completions, and `FamilyCycleState` (treasury and,
  later, upgrades). The account, `Family`, `FamilyMembership`, the yearbook and PvP battles persist.
- **Nothing is reset in place.** Rollover never updates, copies or deletes a gameplay row: the old cycle is
  history. The new cycle creates its own rows lazily through the normal paths: a player's first visit or
  action calls `PlayerCycleState.CreateInitial` (level 1, XP 0, wallet 400, bank 0, energy 240, heat 0, skills
  4, 1 training point, no gear, cargo, jail or PvP state); a family's first donation or join opens a 0
  treasury; contracts and objectives rotate for the new cycle. States are not cloned eagerly.
- **Family identity and membership persist** across the rollover (same ids, same membership rows); the
  treasury does not carry over.
- **Live archives are official; Pilot archives never are.** Pilot results are kept for history and testing
  and never become champions or trophies.
- **September rollover:** a Live cycle runs from 1 September 00:00 Europe/Lisbon to the next 1 September,
  as exact UTC instants (`RolloverRules.SeptemberStartUtc`, via `LisbonCalendar`; summer time, so 23:00 UTC
  on 31 August).

### Transitions

Both run as **one** `AfterHoursWriteTransaction`: re-check, snapshot, write the archive, finish the source,
save (freeing the single-Active slot), insert the Active target, link it from the archive, commit. Anything
that fails before commit leaves the source Active and nothing else written.

- **Live → Live** (`RolloverLiveAsync(sourceCycleId)`): the source must be an Active Live cycle and
  `now >= EndUtc` (one tick before is refused). The target fiscal year is the existing RTUB `FiscalYear` whose
  `StartYear` equals the source fiscal year's `EndYear`; none or several → refused, nothing written. Fiscal
  years are never created or guessed here (the owner creates them in Finance). Target: Live, from 1 September
  of the fiscal year's `StartYear` to 1 September of its `EndYear`.
- **Pilot → Live** (`TransitionPilotToLiveAsync(pilotId, fiscalYearId, startUtc, endUtc)`): the source must be
  an Active Pilot; it **may finish before its `EndUtc`** (pilot length is administrative). The target's fiscal
  year and UTC boundaries are explicit (the first Live cycle may cover only part of an academic year): UTC,
  end after start, an existing fiscal year, and an end in the future.
- Refused: the wrong kind (a Live cycle through the Pilot path or the reverse), a Scheduled source, a source
  Finished without an archive (for example by `FinishAsync`), and a new cycle that would already be over.
  There is no "force" override; AH-010 decides whether admins need one.
- **Idempotent.** A cycle has at most one archive (unique `GameCycleId`) and a target is started by at most
  one archive (unique `NextGameCycleId`). Calls serialize on the write lock; a later call finds the archive
  and returns the same `RolloverResult` with `Replayed = true`, changing nothing. A Pilot transition repeated
  with a different target is refused. Entry rows are unique per archive and user / family, and per family
  entry and user.
- Lazy creators never write into a cycle a rollover has just finished: `PlayerCycleStateService` creates a
  state inside a write transaction that re-checks that the cycle is still Active, and contract rotation does
  the same. Actions already re-read the active cycle under the lock.

### Archive model

`CycleArchive` (`AfterHoursCycleArchives`): source cycle, kind, fiscal year id and label snapshot, source
start and end, `ArchivedAtUtc`, `Official`, `NextGameCycleId`. Children:

- `YearbookPlayerEntry` (`AfterHoursYearbookPlayers`), one per `PlayerCycleState` of the cycle: user id,
  display-name snapshot (nickname, else user name), final level, XP, the four skills, annual score, rank,
  scoring weeks, family id and name at the cycle's end, champion flag. No wallet, bank, cargo or battle data.
- `YearbookFamilyEntry` (`AfterHoursYearbookFamilies`), every family with a treasury row or family objective
  progress in the cycle (disbanded ones too): name snapshot, annual score, rank, scoring weeks, champion flag.
- `YearbookFamilyMember` (`AfterHoursYearbookFamilyMembers`): the family's roster **at the cycle's end**, from
  membership history (`JoinedAtUtc <= end < LeftAtUtc`), with name snapshot and role. The end is the cycle's
  `EndUtc`, or the transition time for a Pilot finished early.

Scores and ranks come from the same code as the live leaderboard (`ObjectiveService.StandingsAsync`: best 12
weekly scores from stored awarded points). Once written nothing recalculates: renames, family moves and
catalogue changes leave the archive as it was, and reads never write. PvP battles are **not** copied: they
already belong to the old cycle and stay there. The archive row is audited; its entry rows are excluded from
the audit log like other gameplay rows. Entry and roster rows keep `UserId` as a plain historical value, not a
foreign key: deleting an account removes its live game data (states, memberships) but never its yearbook
rows, which render from their snapshots alone.

**Winner records** are the entries themselves: the champions of fiscal year X are the `IsChampion` rows of the
archive with that `FiscalYearId` and `Official = true`; several such rows are co-champions; `Kind` says Pilot
or Live.

### AH-009 choices (not in the manual)

- **Ties:** competition ranking (100, 100, 80 → 1, 1, 3); no tie-break is invented.
- **Champions:** in an official archive, every player (and every family) tied for the highest **positive**
  score is champion. If everyone scored 0, the rankings are archived with no champion. A Pilot never has one.
- **No material rewards:** winning grants no cash, XP, gear, title or bonus. The manual says awards survive but
  defines no catalogue; award administration is AH-010.
- The yearbook is **read-only** for players (behind the After Hours gate, like every page in the folder) and
  lists archives newest first; the running cycle stays on `/after-hours/leaderboards`. Dates show as Lisbon
  days.
- Rollover has **no player-facing page or endpoint**. AH-010 binds it to owner/admin tooling and decides any
  scheduling; a hosted job would have to check `AfterHours:Enabled` itself.
- The migration only adds the four yearbook tables. Existing (DEV) cycles are not archived retroactively.
