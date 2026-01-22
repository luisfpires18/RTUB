# RTUB Architecture & Improvement Plan

> **Document Version**: 1.0  
> **Created**: 2025 (post–architecture review)  
> **Purpose**: Prioritized improvement plan from full solution review—code quality, architecture, EF Core, Blazor, security, testing, performance.

**Context**: .NET 10, Blazor **Interactive Server only**, EF Core (SQLite), Identity, Options pattern, Repository + Application Services. Design: web-first, mobile-second. Rule: every PR must include at least one unit or integration test.

---

## 1. Executive Summary

- **Blazor**: Interactive Server only (`AddInteractiveServerComponents` / `AddInteractiveServerRenderMode`). No WASM. `RTUB.Shared` has `SupportedPlatform browser` but is used by Web for shared components in server-rendered context.
- **UI–data boundary violation**: `Members.razor` injects `ApplicationDbContext`, performs EF queries (`MemberStatuses`), `Remove`/`SaveChangesAsync`, and “override retired” business logic directly in the page. Same pattern: `EventEnrollments.razor` uses `DbContext.Entry(...).Detach`. `DatabaseViewer.razor` and `Albums.razor` inject `DbContext`; Albums appears to be dead injection.
- **Repository `Query()` returns tracked entities**: `Repository.Query()` returns `_dbSet.AsQueryable()` with **no `AsNoTracking()`**. Dozens of services use `Query().Where(...).ToListAsync()` for read-only flows (e.g. `AuditLogService`, `RankingService`, `EnrollmentService`). This increases memory, complicates Blazor circuits, and can cause unexpected change-tracking behavior.
- **God page**: `Members.razor` is ~3 700 lines, multiple modals, pagination helpers, and mixed UI + domain + data logic. `CODE_IMPROVEMENT_PLAN.md` already recommends `MultiModalState`, `LoadableContent`, and extracting DTOs/logic—these remain high impact.
- **Auth**: Cookie + Identity, custom `/auth/login` and `/auth/logout` minimal APIs. `[Authorize]` / `[Authorize(Roles = "...")]` and `AuthorizeView` used widely. Login/logout **disable antiforgery** (`.DisableAntiforgery()`); CSRF risk for auth endpoints is partially mitigated by POST-only and redirects but should be reviewed.
- **Security**: Password policy is intentionally weak (min length 4, no complexity) for “older users.” Lockout and expulsion checks exist. `DatabaseViewer` is Owner-only, runs raw DML (INSERT/UPDATE/DELETE) with keyword validation; SELECT uses `SqlValidationService`. R2/secrets via config; no hardcoded secrets observed.
- **EF Core**: DbContextFactory + scoped DbContext, SplitQuery, SQLite WAL interceptor. Migrations in `RTUB.Web`. `ApplicationDbContext.SaveChangesAsync` embeds large audit-logging logic (~400+ lines). `MeetingAtaRepository` uses `IDbContextFactory`; most others use scoped `ApplicationDbContext`.
- **No health checks**: No `AddHealthChecks` / `MapHealthChecks`. Add for readiness/liveness and DB connectivity.
- **Testing**: Unit tests (Core, Application, Shared, Web), integration tests (`TestWebApplicationFactory`, in-memory SQLite, seeded data), and Bunit for components. Coverage collected in CI. Gaps: many application services lack dedicated unit tests; “at least one test per PR” should be enforced via CI.
- **CI**: Build, `dotnet test` with coverage, CodeQL. No `dotnet format`, no EditorConfig/style enforcement. `TreatWarningsAsErrors` and nullable enabled in `Directory.Build.props`.
- **Logging**: ILogger used across services; mix of structured and string messages. Circuit-host `TaskCanceledException` errors suppressed in `Program.cs`. No distributed tracing observed.
- **Quick wins**: Remove dead `DbContext` injection from `Albums.razor`; add `AsNoTracking()` to `Repository.Query()`; add `.editorconfig` and run `dotnet format`; introduce health checks.
- **High-impact refactors**: Move Members’ MemberStatus override workflow into `IMemberStatusService`; replace direct DbContext usage in pages with application services; split `Members.razor` using `CODE_IMPROVEMENT_PLAN` patterns (modals, loadable content, DTOs).
- **Documentation**: `docs/CODE_IMPROVEMENT_PLAN.md` already covers duplication, modals, pagination, storage helpers, and GetByIdOrThrow. This plan complements it with architecture, EF, security, and a sequenced PR plan.

---

## 2. Architecture Snapshot

### 2.1 Current Layers and Responsibilities

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  RTUB.Web (Host)                                                             │
│  - Program.cs: DI, Options, DbContextFactory + scoped DbContext, Identity,  │
│    Blazor Server, SignalR, minimal APIs /auth/login, /auth/logout            │
│  - Pages (Razor), Components, Controllers, Hubs, Services, Migrations       │
│  - References: RTUB.Core, RTUB.Application, RTUB.Shared                      │
└─────────────────────────────────────────────────────────────────────────────┘
          │                    │                          │
          ▼                    ▼                          ▼
┌──────────────────┐  ┌────────────────────────┐  ┌──────────────────────────┐
│  RTUB.Shared     │  │  RTUB.Application      │  │  RTUB.Core               │
│  - Razor         │  │  - Data (DbContext,    │  │  - Entities, Enums,      │
│    components,   │  │    Configurations,     │  │    Constants, Helpers,   │
│    Base classes  │  │    SeedData)           │  │    Exceptions            │
│  - References:   │  │  - Repositories,       │  │  - References:           │
│    Core, App     │  │    Services,           │  │    Identity.Stores       │
│                  │  │    Interfaces, DTOs,   │  │                          │
│                  │  │    Helpers, Config     │  │                          │
│                  │  │  - References: Core    │  │                          │
└──────────────────┘  └────────────────────────┘  └──────────────────────────┘
```

### 2.2 Data Flow (Simplified)

- **Reads**: Blazor pages / components → Application services → Repositories → `ApplicationDbContext`. Some pages bypass services and use `ApplicationDbContext` directly (see anti-patterns).
- **Writes**: Same chain; audit logging runs inside `SaveChangesAsync` override.
- **Background**: Hosted services (geocoding, birthday emails, member status, reminders, notifications) use scoped `ApplicationDbContext` or factory where appropriate.

### 2.3 Key Anti-Patterns (with file references)

| Anti-pattern | Evidence |
|--------------|----------|
| **UI → DbContext** | `Members.razor`: `@inject ApplicationDbContext _context`; lines 3464–3492 use `_context.MemberStatuses`, `Remove`, `SaveChangesAsync`, and override-retired logic. |
| **UI → DbContext** | `EventEnrollments.razor`: `@inject ApplicationDbContext DbContext`; line 478 `DbContext.Entry(userData).State = EntityState.Detached`. |
| **UI → DbContext** | `DatabaseViewer.razor`: `@inject ApplicationDbContext DbContext`; runs raw SQL (SELECT via service, DML via `ExecuteSqlRawAsync`). Owner-only; acceptable for admin tool but still UI–data coupling. |
| **Dead injection** | `Albums.razor`: `@inject ApplicationDbContext DbContext`—only used in `@inject`; no other references. |
| **Tracked read-only queries** | `Repository.cs` line 112–115: `Query()` returns `_dbSet.AsQueryable()` (no `AsNoTracking`). Used by ~30+ call sites in Application services. |
| **God page** | `Members.razor`: ~3 700 lines, many modals, multiple pagination helpers, mixed UI + domain + data logic. |
| **Business logic in UI** | “Override retired” workflow in `Members.razor` (remove status → `UpdateMemberStatusAsync` → fetch again → set `IsRetired`/`OverrideRetired` → `SaveChanges`) belongs in `IMemberStatusService`. |
| **Login/logout without antiforgery** | `Program.cs`: `MapPost("/auth/login", ...).DisableAntiforgery()` and same for `/auth/logout`. |

---

## 3. Findings Table (Prioritized)

| # | Severity | Category | Evidence | Why it matters | Recommended fix | Effort | Test approach |
|---|----------|----------|----------|----------------|-----------------|--------|---------------|
| 1 | **High** | Architecture | `Members.razor` injects `ApplicationDbContext`, queries `MemberStatuses`, `Remove`/`SaveChanges`, override-retired logic in page | Violates layering; UI touches data and domain | Introduce `IMemberStatusService.ActivateMemberWithOverrideAsync(userId)` (or similar), implement in service, remove DbContext from Members and call service | M | Unit test service; integration test Members flow |
| 2 | **High** | EF Core | `Repository.Query()` returns `_dbSet.AsQueryable()` (no `AsNoTracking`) — `Repository.cs` L112–115 | Read-only flows get tracked entities; Blazor circuit and memory impact | Add `AsNoTracking()` in `Query()`. If any caller needs tracking, add `QueryTracked()` or use `GetById`/explicit attach | S | Unit tests for repositories; verify existing integration tests |
| 3 | **High** | Blazor / Architecture | `Members.razor` ~3 700 lines, many modals, pagination, mixed concerns | Hard to maintain, test, and refactor | Apply `CODE_IMPROVEMENT_PLAN`: extract DTOs, use `MultiModalState`, `LoadableContent`, split into smaller components/pages | L | Component tests; integration tests for critical flows |
| 4 | **Medium** | Architecture | `EventEnrollments.razor` uses `DbContext.Entry(...).Detach` for `UserManager` result | Couples UI to EF; workaround for Identity + shared DbContext | Prefer a small application service that returns user category/instruments without exposing DbContext; detach inside service if needed | S | Integration test EventEnrollments |
| 5 | **Medium** | Architecture | `DatabaseViewer.razor` injects `ApplicationDbContext`, runs raw SQL | Owner-only admin tool; acceptable but still UI–data coupling | Optional: introduce `IDatabaseViewerService` with `ExecuteSelectAsync` / `ExecuteModifyAsync` that wrap DbContext. Keep validation in place | M | Unit test validation; optional integration test for viewer |
| 6 | **Low** | Code quality | `Albums.razor` injects `ApplicationDbContext` but never uses it | Dead code, confusion | Remove `@inject ApplicationDbContext DbContext` | S | N/A (or trivial UI smoke test) |
| 7 | **Medium** | Security | `/auth/login` and `/auth/logout` use `DisableAntiforgery` | CSRF risk on auth endpoints | Evaluate enabling antiforgery for login/logout (e.g. token in form) or document why disabled and accept risk | S | Integration tests for login/logout |
| 8 | **Medium** | Security | Password policy: min length 4, no complexity | Weak passwords | Document as deliberate (e.g. “older users”); consider configurable policy and rate limiting / lockout (already present) | S | Unit/integration tests for auth policy |
| 9 | **Low** | Perf / Observability | No health checks | Deployment and ops cannot probe app or DB | Add `AddHealthChecks` (DB, optionally “ready”) and `MapHealthChecks` | S | Integration test hitting health endpoint |
| 10 | **Medium** | Testing | Many application services lack unit tests | “One test per PR” harder; regressions possible | Add unit tests for new and critical services; prioritize `MemberStatusService`, `EnrollmentService`, `RankingService` | M | Unit tests per service |
| 11 | **Low** | Code quality | No EditorConfig / `dotnet format` in CI | Inconsistent style | Add `.editorconfig`, run `dotnet format` in CI, fix formatting in a single PR | S | CI green after format |
| 12 | **Low** | Blazor | Most pages don’t pass `CancellationToken` into async ops | Circuit disposal can cause unobserved `OperationCanceledException` | Add `CancellationToken` to async app services and propagate from Blazor lifecycle where applicable | M | Existing tests; optional component tests |
| 13 | **Medium** | EF Core | `ApplicationDbContext.SaveChangesAsync` embeds large audit logic | Hard to test and maintain | Extract audit logic to a dedicated `IAuditLogAppender` (or similar) called from `SaveChangesAsync`; keep behavior identical | L | Unit tests for appender; integration tests for audit |
| 14 | **Low** | Blazor / UX | Some spinners use `role="status"` only, no `aria-label` | A11y | Use `LoadingSpinner` with `Label` / `ShowLabel` or ensure `aria-label` where custom spinners exist | S | Manual a11y check; optional Bunit test |
| 15 | **Low** | EF Core | `BetRepository` uses repeated `Include(b => b.Options).ThenInclude(...)` for MemberA vs MemberB | Readability only | Standard EF pattern (different ThenInclude paths from same collection). No change required; optionally add brief comment | S | N/A |

---

## 4. Target State Proposal

### 4.1 Pragmatic Architecture

- **Web**: Host only. No direct `ApplicationDbContext` in pages or components. Controllers/Hubs may use services that wrap data access.
- **Application**: Services orchestrate use cases; repositories handle data access. Domain logic lives in services or domain types, not in UI.
- **Core**: Entities, value objects, enums, domain exceptions. No EF or infrastructure.
- **Shared**: Reusable Blazor components, base classes, UI helpers. No data access.

### 4.2 Patterns to Use (Where They Add Value)

- **Options pattern**: Already used for config; keep it.
- **Result pattern**: Consider for operations that can fail (e.g. “activate member”) to avoid throwing in UI. Optional; not required everywhere.
- **CQRS/Mediator**: Not recommended unless you introduce clear write vs. read boundaries and complexity justifies it. Current repository + service layout is sufficient.
- **Validation**: Keep DataAnnotations where used; consider FluentValidation only for complex forms or shared rules. No change required for this plan.
- **Repository**: Keep generic + specific repositories. `Query()` should be read-only and no-tracking; add explicit tracked entry points only where needed.

### 4.3 DI and Logic Placement

- **DbContext**: Scoped; created via factory where needed (e.g. `MeetingAtaRepository`). No DbContext in Blazor pages.
- **Services**: Scoped; hold application and domain logic. Pages call services only.
- **Background workers**: Use scoped `IServiceScopeFactory` to resolve DbContext/services per execution.

### 4.4 Folder / Namespace Guidance

- `RTUB.Application`: `Data`, `Repositories`, `Services`, `Interfaces`, `DTOs`, `Configuration`, `Helpers`, `Extensions`.
- `RTUB.Web`: `Pages`, `Components`, `Controllers`, `Hubs`, `Services`, `Extensions`, `Migrations`. Keep Migrations in Web unless you move to a dedicated persistence project later.

---

## 5. PR Plan (Sequenced, Safe Increments)

Each PR should include **at least one** unit or integration test and a brief “Definition of Done” checklist.

| PR | Title | Scope | Steps | Rollback | Required test | Definition of Done |
|----|--------|--------|--------|----------|----------------|--------------------|
| **1** | Add `.editorconfig` and run `dotnet format` | Repo root, `Directory.Build.props` | Add `.editorconfig`; run `dotnet format`; fix newly reported issues | Revert `.editorconfig` and formatting commits | CI passes after format | Build clean; `dotnet format` reports no changes |
| **2** | Add health checks | `Program.cs` | `AddHealthChecks` (+ DB), `MapHealthChecks("/health")` | Revert Program changes | Integration test `GET /health` returns 200 | Health endpoint returns 200 |
| **3** | `Repository.Query()` use `AsNoTracking` | `Repository.cs` | Change `Query()` to return `_dbSet.AsNoTracking().AsQueryable()` | Revert | Existing repository/integration tests | No regression in tests; spot-check read-only UIs |
| **4** | Remove dead `ApplicationDbContext` injection from `Albums.razor` | `Albums.razor` | Remove `@inject ApplicationDbContext DbContext` | Revert | Smoke test / existing integration test for Albums | Albums page loads and displays data |
| **5** | Add `IMemberStatusService.ActivateMemberWithOverrideAsync` and use it from Members | `IMemberStatusService`, `MemberStatusService`, `Members.razor` | Implement `ActivateMemberWithOverrideAsync`; remove DbContext usage from Members; call service | Revert | Unit test service; integration test “activate member” flow | Members no longer inject DbContext; activation works |
| **6** | Extract Members override-retired logic into service only | Same as PR 5 | Ensure all override-retired logic lives in `MemberStatusService` | Revert | Unit test `ActivateMemberWithOverrideAsync` | No EF in Members for status |
| **7** | Replace `DbContext` usage in `EventEnrollments.razor` with service | New/updated app service, `EventEnrollments.razor` | Service returns user category/instruments; page calls service; remove DbContext and Detach | Revert | Integration test EventEnrollments | No DbContext in EventEnrollments |
| **8** | Extract DTOs from `Members.razor` (per `CODE_IMPROVEMENT_PLAN` O1) | `RTUB.Application/DTOs`, `Members.razor` | Move `ActiveMemberData`, `NicknameFormModel`, etc. to DTOs | Revert | Existing Members tests | DTOs in Application; Members compiles |
| **9** | Introduce `MultiModalState` and use in `Members.razor` (per CODE_IMPROVEMENT_PLAN P1) | `RTUB.Shared`, `Members.razor` | Add `MultiModalState<TKey>`; replace modal booleans in Members | Revert | Component test for MultiModalState; Members integration | Modals behave as before |
| **10** | Add `LoadableContent` and use in `Members.razor` (per CODE_IMPROVEMENT_PLAN P4) | `RTUB.Shared`, `Members.razor` | Add `LoadableContent`; replace loading/empty patterns in Members | Revert | Component test; Members integration | Loading/empty states unchanged |
| **11** | Add `GetByIdOrThrowAsync` extension (per `CODE_IMPROVEMENT_PLAN` S2) | `RTUB.Application/Extensions` | Implement extension; use in 1–2 services as pilot | Revert | Unit tests for extension | Services use extension where applied |
| **12** | Unit tests for `MemberStatusService` | `RTUB.Application.Tests` | Cover `GetMemberStatusAsync`, `UpdateMemberStatusAsync`, `ActivateMemberWithOverrideAsync` | Revert | New unit tests | Coverage for status logic |
| **13** | Unit tests for `EnrollmentService` and `RankingService` | `RTUB.Application.Tests` | Add tests for critical paths | Revert | New unit tests | Key flows covered |
| **14** | Evaluate antiforgery for `/auth/login` and `/auth/logout` | `Program.cs`, login UI | Enable antiforgery or document decision | Revert | Integration tests for login/logout | Auth endpoints documented and tested |
| **15** | (Optional) Comment `BetRepository` Include pattern | `BetRepository.cs` | Add brief comment that repeated `Include(Options).ThenInclude` for MemberA/MemberB is standard EF | Revert | N/A | Documented |

**Optional follow-ups** (can be separate PRs later):

- **16** | `IDatabaseViewerService` and move DbContext out of `DatabaseViewer.razor`
- **17** | Extract audit logic from `SaveChangesAsync` into `IAuditLogAppender`
- **18** | Add `CancellationToken` to async service methods and Blazor `OnInitializedAsync` where appropriate
- **19** | Add `dotnet format` step to CI (if not in PR 1)
- **20** | Broader adoption of `LoadableContent` / `MultiModalState` on other large pages (e.g. `Events.razor`)

---

## 6. Quick Wins (< 1 day)

- Add `.editorconfig` and run `dotnet format` (align with PR 1).
- Fix `Repository.Query()` to use `AsNoTracking()` (PR 3).
- Remove dead `ApplicationDbContext` injection from `Albums.razor` (PR 4).
- Add health checks (PR 2).
- Add `GetByIdOrThrowAsync` and use in a couple of services (PR 11).
- (Optional) Add comment to `BetRepository` for `Include`/`ThenInclude` pattern (PR 15).
- Ensure `LoadingSpinner` / custom spinners have `aria-label` where missing (a11y quick win).

---

## 7. Guardrails and Standards

### 7.1 Coding Conventions

- **Nullable reference types**: Keep enabled (`Nullable` in `Directory.Build.props`).
- **Warnings as errors**: Keep `TreatWarningsAsErrors`; address new warnings before merge.
- **Formatting**: Enforce via `.editorconfig` and `dotnet format`. Run in CI.
- **Analyzers**: `AnalysisLevel` latest; consider StyleCop or .NET analyzers only if the team agrees and you fix existing violations incrementally.

### 7.2 EF Core

- Use **no-tracking** for read-only queries (`AsNoTracking()` in `Query()` and ad-hoc queries).
- Prefer **projections** (e.g. `Select` to DTOs) over loading full entities when you don’t need change tracking.
- Use **SplitQuery** for root + multiple collections (already configured globally).
- Avoid **N+1**: use `Include`/`ThenInclude` or batched queries; check `UserProfileService`, `RankingService`, and similar for loops over IDs.
- **Pagination**: Use consistent helpers (`PaginationHelper`, etc.); ensure DB-level `Skip`/`Take` for large sets.

### 7.3 Blazor

- **State**: Prefer explicit state (parameters, cascading values, or dedicated state services) over global static state.
- **Parameters**: Prefer `[Parameter]` and `[EditorRequired]` where applicable; avoid overloading with cascading parameters when it hurts clarity.
- **Events**: Use `EventCallback`; avoid direct `Action`/`Func` for component events.
- **Async**: Use `async`/`await`; consider `CancellationToken` in `OnInitializedAsync` and service calls.
- **Rendering**: Use `ShouldRender` and avoid unnecessary subscriptions (e.g. `IDisposable` in components) to limit re-renders.

### 7.4 Logging

- Use **structured logging** (`Logger.LogInformation("...", arg1, arg2)`) rather than string interpolation where useful for querying.
- Avoid logging **sensitive data** (passwords, tokens, PII). Already largely respected; keep AuditContext usage focused on non-sensitive audit fields.
- Keep **circuit `TaskCanceledException`** suppression scoped to the known benign case; don’t suppress broadly.

### 7.5 Testing

- **Unit tests**: Domain helpers, extensions, services (mocking repositories), validation.
- **Integration tests**: API endpoints, auth, key pages, DB-backed flows. Use `TestWebApplicationFactory` and in-memory SQLite.
- **Component tests**: Bunit for shared components (`LoadableContent`, `MultiModalState`, etc.).
- **CI**: Require “at least one unit or integration test per PR” and enforce via branch protection or template checklists.

### 7.6 Security

- **Auth**: Use `[Authorize]` / `AuthorizeView`; enforce role checks in services for sensitive operations where appropriate.
- **Config**: Keep secrets in config/user secrets; no hardcoded credentials.
- **Input**: Validate and sanitize; use parameterized queries (EF) or validated raw SQL (e.g. `SqlValidationService`, `ValidateModifyQuery`) for DatabaseViewer.

---

## 8. References

- **Existing plan**: `docs/CODE_IMPROVEMENT_PLAN.md` — duplication, modals, pagination, storage helpers, DTOs, `GetByIdOrThrow`, `LoadableContent`, `MultiModalState`.
- **CI**: `.github/workflows/ci.yml` — build, test, coverage, CodeQL.
- **Entry point**: `src/RTUB.Web/Program.cs` — DI, Blazor Server, auth, DbContext, Options.

---

*This plan is incremental and avoids big-bang rewrites. Prioritize PRs 1–7 and 12–13 for maximum impact with minimal risk.*
