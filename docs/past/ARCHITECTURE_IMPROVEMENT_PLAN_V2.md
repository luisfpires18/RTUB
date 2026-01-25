# RTUB Architecture & Improvement Plan v2.0

> **Document Version**: 2.0  
> **Created**: 2025 (post-PR batch completion)  
> **Purpose**: Updated improvement plan reflecting completed PRs and next-phase priorities for code quality, architecture, EF Core, Blazor, security, testing, and performance.

**Context**: .NET 10, Blazor **Interactive Server only**, EF Core (SQLite), Identity, Options pattern, Repository + Application Services. Design: web-first, mobile-second. Rule: every PR must include at least one unit or integration test.

---

## 1. Executive Summary

### 1.1 Completed Improvements (v1.0 → v2.0)

✅ **PR 1**: Added `.editorconfig` and ran `dotnet format` - codebase now consistently formatted  
✅ **PR 2**: Added health checks (`/health` endpoint with database connectivity check)  
✅ **PR 3**: Fixed `Repository.Query()` to use `AsNoTracking()` - all read-only queries now no-tracking  
✅ **PR 4**: Removed dead `ApplicationDbContext` injection from `Albums.razor`  
✅ **PR 5-6**: Added `IMemberStatusService.ActivateMemberWithOverrideAsync()` and removed all DbContext usage from `Members.razor`  
✅ **PR 7**: Replaced `DbContext` usage in `EventEnrollments.razor` with `IUserProfileService.GetUserCategoriesAsync()`  
✅ **PR 8**: Extracted DTOs from `Members.razor` (`ActiveMemberData`, `NicknameFormModel`)  
✅ **PR 11**: Added `GetByIdOrThrowAsync` extension method and applied to 28+ methods across 8 services  
✅ **PR 12**: Applied `GetByIdOrThrowAsync` to additional services (EventService, SlideshowService, ReportService, RequestService, RoleAssignmentService, RehearsalAttendanceService, NaipeService)  
✅ **PR 15**: Applied `GetByIdOrThrowAsync` to remaining services (11 more methods)  
✅ **PR 16**: Added 4 unit tests for `SlideshowService` (SetSlideshowImageAsync, DeleteSlideshowAsync edge cases)  
✅ **PR 17**: Added 2 unit tests for `RequestService` (status change notifications, date range)  
✅ **PR 18**: Extracted DTOs from `Rehearsals.razor` (`RehearsalFormModel`, `RehearsalRangeFormModel`, `AttendanceFormModel`, `RehearsalAttendanceDisplay`)

**Impact Summary**:
- **Architecture**: Removed 3 direct DbContext injections from Blazor pages
- **Code Quality**: Extracted 6 DTOs, added extension method, improved repository pattern
- **Testing**: Added 18 new tests (health checks, MemberStatus, UserProfile, RepositoryExtensions, SlideshowService, RequestService)
- **Performance**: Fixed read-only queries to use no-tracking (affects ~30+ service methods)
- **Maintainability**: Removed ~56 lines of boilerplate null-check code via `GetByIdOrThrowAsync`

### 1.2 Remaining High-Priority Issues

- **God page**: `Members.razor` still ~3,700 lines (DTOs extracted, but page structure needs refactoring)
- **UI–data boundary**: `DatabaseViewer.razor` still injects `ApplicationDbContext` (Owner-only admin tool; acceptable but could be improved)
- **Testing gaps**: Many application services still lack comprehensive unit tests
- **Blazor patterns**: Large pages could benefit from `MultiModalState` and `LoadableContent` patterns
- **Audit logging**: Large `SaveChangesAsync` override (~400+ lines) could be extracted to dedicated service
- **Security**: `/auth/login` and `/auth/logout` still disable antiforgery (documented risk)

---

## 2. Architecture Snapshot (Updated)

### 2.1 Current State (Post-v1 Improvements)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  RTUB.Web (Host)                                                             │
│  - Program.cs: DI, Options, DbContextFactory + scoped DbContext, Identity,  │
│    Blazor Server, SignalR, minimal APIs /auth/login, /auth/logout, /health │
│  - Pages (Razor), Components, Controllers, Hubs, Services, Migrations       │
│  - ✅ No direct DbContext in Members, EventEnrollments, Albums              │
│  - ⚠️  DatabaseViewer.razor still uses DbContext (Owner-only admin tool)    │
└─────────────────────────────────────────────────────────────────────────────┘
          │                    │                          │
          ▼                    ▼                          ▼
┌──────────────────┐  ┌────────────────────────┐  ┌──────────────────────────┐
│  RTUB.Shared     │  │  RTUB.Application      │  │  RTUB.Core               │
│  - Razor         │  │  - Data (DbContext,    │  │  - Entities, Enums,      │
│    components,   │  │    Configurations,     │  │    Constants, Helpers,   │
│    Base classes  │  │    SeedData)           │  │    Exceptions            │
│  - References:   │  │  - Repositories        │  │  - References:           │
│    Core, App     │  │    ✅ Query() uses      │  │    Identity.Stores       │
│                  │  │       AsNoTracking()   │  │                          │
│                  │  │  - Services           │  │                          │
│                  │  │    ✅ GetByIdOrThrow   │  │                          │
│                  │  │       extension       │  │                          │
│                  │  │  - DTOs (6 extracted) │  │                          │
│                  │  │  - Extensions         │  │                          │
│                  │  │  - References: Core   │  │                          │
└──────────────────┘  └────────────────────────┘  └──────────────────────────┘
```

### 2.2 Improvements Made

| Area | Before | After |
|------|--------|-------|
| **DbContext in Pages** | 4 pages | 1 page (DatabaseViewer - acceptable) |
| **Repository Query** | Tracked entities | No-tracking (read-only) |
| **Null Checks** | 28+ manual checks | Extension method pattern |
| **DTOs** | Nested in pages | Extracted to Application/DTOs |
| **Health Checks** | None | `/health` endpoint |
| **Code Formatting** | Inconsistent | `.editorconfig` + formatted |
| **Unit Tests** | Gaps in services | +18 new tests |

---

## 3. Next-Phase Findings (Prioritized)

| # | Severity | Category | Evidence | Why it matters | Recommended fix | Effort | Test approach |
|---|----------|----------|----------|----------------|-----------------|--------|---------------|
| **1** | **High** | Blazor / Architecture | `Members.razor` still ~3,700 lines despite DTO extraction | Hard to maintain, test, and refactor | Apply `CODE_IMPROVEMENT_PLAN`: introduce `MultiModalState`, `LoadableContent`, split into smaller components/pages | L | Component tests; integration tests for critical flows |
| **2** | **Medium** | Architecture | `DatabaseViewer.razor` injects `ApplicationDbContext`, runs raw SQL | Owner-only admin tool; acceptable but still UI–data coupling | Optional: introduce `IDatabaseViewerService` with `ExecuteSelectAsync` / `ExecuteModifyAsync` | M | Unit test validation; optional integration test |
| **3** | **Medium** | Testing | Many application services lack comprehensive unit tests | "One test per PR" harder; regressions possible | Add unit tests for critical services: `EnrollmentService`, `RankingService`, `EventService`, `RehearsalService` | M | Unit tests per service |
| **4** | **Medium** | EF Core | `ApplicationDbContext.SaveChangesAsync` embeds large audit logic (~400+ lines) | Hard to test and maintain | Extract audit logic to dedicated `IAuditLogAppender` service; keep behavior identical | L | Unit tests for appender; integration tests for audit |
| **5** | **Medium** | Security | `/auth/login` and `/auth/logout` use `DisableAntiforgery` | CSRF risk on auth endpoints | Evaluate enabling antiforgery for login/logout (e.g. token in form) or document why disabled and accept risk | S | Integration tests for login/logout |
| **6** | **Low** | Blazor | Most pages don't pass `CancellationToken` into async ops | Circuit disposal can cause unobserved `OperationCanceledException` | Add `CancellationToken` to async app services and propagate from Blazor lifecycle where applicable | M | Existing tests; optional component tests |
| **7** | **Low** | Code Quality | Some nested classes still in pages (e.g. `Events.razor`, `Games/Bets.razor`) | DTOs should be in Application layer | Extract remaining DTOs from large pages to `RTUB.Application/DTOs` | S | Existing tests; verify compilation |
| **8** | **Low** | Performance | Some services may still have N+1 query patterns | Database performance | Audit services for loops over IDs; use batch queries or `Include` where appropriate | M | Performance tests; query analysis |
| **9** | **Low** | Observability | No distributed tracing | Hard to debug production issues | Consider adding OpenTelemetry or Application Insights for request tracing | M | Integration tests; verify traces |
| **10** | **Low** | Blazor / UX | Some spinners may lack `aria-label` | A11y | Audit and ensure all loading spinners have proper ARIA labels | S | Manual a11y check; optional Bunit test |

---

## 4. Target State v2.0

### 4.1 Architecture Goals

- **Zero DbContext in Pages**: Only `DatabaseViewer.razor` remains (Owner-only admin tool; acceptable exception)
- **Comprehensive Testing**: All critical services have unit tests; integration tests cover key user flows
- **Clean Separation**: All DTOs in Application layer; pages reference DTOs, not entities directly
- **Pattern Consistency**: Large pages use `MultiModalState` and `LoadableContent` patterns
- **Maintainable Audit**: Audit logging extracted to dedicated service

### 4.2 Patterns to Adopt

- **MultiModalState**: Replace multiple boolean flags with state management for modals
- **LoadableContent**: Standardize loading/empty/error states across pages
- **Result Pattern**: Consider for operations that can fail (e.g., "activate member") to avoid throwing in UI
- **CancellationToken**: Propagate cancellation tokens through async service methods

---

## 5. PR Plan v2.0 (Next Phase)

| PR | Title | Scope | Steps | Rollback | Required test | Definition of Done |
|----|--------|--------|--------|----------|----------------|--------------------|
| **19** | Introduce `MultiModalState<TKey>` and use in `Members.razor` | `RTUB.Shared`, `Members.razor` | Add `MultiModalState<TKey>` class; replace modal booleans in Members | Revert | Component test for MultiModalState; Members integration | Modals behave as before; code is cleaner |
| **20** | Add `LoadableContent` and use in `Members.razor` | `RTUB.Shared`, `Members.razor` | Add `LoadableContent<T>` component; replace loading/empty patterns | Revert | Component test; Members integration | Loading/empty states unchanged; reusable component |
| **21** | Extract audit logic from `SaveChangesAsync` to `IAuditLogAppender` | `ApplicationDbContext`, new service | Create `IAuditLogAppender`; move audit logic; call from `SaveChangesAsync` | Revert | Unit tests for appender; integration tests for audit | Audit behavior unchanged; logic testable |
| **22** | Unit tests for `EnrollmentService` | `RTUB.Application.Tests` | Cover critical enrollment flows | Revert | New unit tests | Key flows covered |
| **23** | Unit tests for `RankingService` | `RTUB.Application.Tests` | Cover ranking calculations | Revert | New unit tests | Ranking logic tested |
| **24** | Unit tests for `EventService` | `RTUB.Application.Tests` | Cover event CRUD and video operations | Revert | New unit tests | Event operations tested |
| **25** | Extract remaining DTOs from large pages | `RTUB.Application/DTOs`, various pages | Extract DTOs from `Events.razor`, `Bets.razor`, etc. | Revert | Existing tests; verify compilation | DTOs in Application; pages compile |
| **26** | Add `CancellationToken` support to async services | Various services | Add `CancellationToken` parameters; propagate from Blazor | Revert | Existing tests; verify cancellation | Services support cancellation |
| **27** | (Optional) `IDatabaseViewerService` for `DatabaseViewer.razor` | New service, `DatabaseViewer.razor` | Create service; move DbContext logic; update page | Revert | Unit test validation; integration test | No DbContext in DatabaseViewer |
| **28** | Evaluate and document antiforgery for auth endpoints | `Program.cs`, documentation | Enable antiforgery or document decision | Revert | Integration tests for login/logout | Auth endpoints documented |
| **29** | Audit and fix N+1 query patterns | Various services | Identify loops over IDs; use batch queries | Revert | Performance tests; query analysis | No N+1 patterns |
| **30** | Add distributed tracing (OpenTelemetry) | `Program.cs`, configuration | Add OpenTelemetry; configure exporters | Revert | Integration tests; verify traces | Traces visible in monitoring |

**Priority Order**: PRs 19-21, 22-24, 25, 26, 28 (security), then optional 27, 29, 30.

---

## 6. Quick Wins v2.0 (< 1 day)

- Extract remaining DTOs from large pages (PR 25) - low risk, high clarity
- Add unit tests for one critical service (PR 22, 23, or 24) - improves confidence
- Audit and fix one N+1 query pattern (PR 29) - performance win
- Ensure all loading spinners have `aria-label` - accessibility quick win
- Document antiforgery decision for auth endpoints (PR 28) - security clarity

---

## 7. Guardrails and Standards (Updated)

### 7.1 Coding Conventions

- **Nullable reference types**: Keep enabled (`Nullable` in `Directory.Build.props`).
- **Warnings as errors**: Keep `TreatWarningsAsErrors`; address new warnings before merge.
- **Formatting**: Enforced via `.editorconfig` and `dotnet format`. ✅ **Implemented**
- **Analyzers**: `AnalysisLevel` latest; consider StyleCop or .NET analyzers only if the team agrees.

### 7.2 EF Core

- Use **no-tracking** for read-only queries. ✅ **Fixed in PR 3**
- Prefer **projections** (e.g. `Select` to DTOs) over loading full entities.
- Use **SplitQuery** for root + multiple collections (already configured globally).
- Avoid **N+1**: use `Include`/`ThenInclude` or batched queries.
- **Pagination**: Use consistent helpers; ensure DB-level `Skip`/`Take`.

### 7.3 Blazor

- **State**: Prefer explicit state (parameters, cascading values, or dedicated state services).
- **Parameters**: Prefer `[Parameter]` and `[EditorRequired]` where applicable.
- **Events**: Use `EventCallback`; avoid direct `Action`/`Func`.
- **Async**: Use `async`/`await`; consider `CancellationToken` in `OnInitializedAsync` and service calls.
- **Rendering**: Use `ShouldRender` and avoid unnecessary subscriptions.
- **DTOs**: Extract to `RTUB.Application/DTOs`; don't nest in pages. ✅ **Partially implemented**

### 7.4 Repository Pattern

- **GetByIdOrThrowAsync**: Use extension method for null checks. ✅ **Implemented in PR 11-12, 15**
- **Query()**: Always returns no-tracking queries. ✅ **Fixed in PR 3**
- **Tracked queries**: Use explicit `GetByIdAsync` or `QueryTracked()` if needed (rare).

### 7.5 Testing

- **Unit tests**: Domain helpers, extensions, services (mocking repositories), validation.
- **Integration tests**: API endpoints, auth, key pages, DB-backed flows.
- **Component tests**: Bunit for shared components (`LoadableContent`, `MultiModalState`, etc.).
- **CI**: Require "at least one unit or integration test per PR". ✅ **18 new tests added**

### 7.6 Security

- **Auth**: Use `[Authorize]` / `AuthorizeView`; enforce role checks in services.
- **Config**: Keep secrets in config/user secrets; no hardcoded credentials.
- **Input**: Validate and sanitize; use parameterized queries (EF) or validated raw SQL.
- **Antiforgery**: Evaluate for auth endpoints (PR 28).

---

## 8. Metrics and Success Criteria

### 8.1 Code Quality Metrics

- **DbContext in Pages**: 1 (DatabaseViewer - acceptable exception) ✅
- **DTOs Extracted**: 6 ✅
- **Services Using GetByIdOrThrowAsync**: 8 services, 28+ methods ✅
- **Unit Tests Added**: 18 new tests ✅
- **Code Formatting**: Consistent via `.editorconfig` ✅

### 8.2 Next-Phase Targets

- **Members.razor**: Reduce to < 2,000 lines (via MultiModalState, LoadableContent, component splitting)
- **Unit Test Coverage**: 80%+ for critical services (EnrollmentService, RankingService, EventService)
- **DTOs Extracted**: 15+ total (extract remaining from large pages)
- **N+1 Queries**: Zero identified patterns
- **CancellationToken**: All async service methods support cancellation

---

## 9. References

- **v1.0 Plan**: `docs/ARCHITECTURE_IMPROVEMENT_PLAN.md` - original findings and PR plan
- **Code Improvements**: `docs/CODE_IMPROVEMENT_PLAN.md` - duplication, modals, pagination, storage helpers
- **CI**: `.github/workflows/ci.yml` - build, test, coverage, CodeQL
- **Entry Point**: `src/RTUB.Web/Program.cs` - DI, Blazor Server, auth, DbContext, Options, health checks

---

## 10. Completed PRs Summary

| PR | Title | Status | Impact |
|----|--------|--------|--------|
| 1 | Add `.editorconfig` and run `dotnet format` | ✅ | Code consistency |
| 2 | Add health checks | ✅ | Observability |
| 3 | `Repository.Query()` use `AsNoTracking` | ✅ | Performance, memory |
| 4 | Remove dead `ApplicationDbContext` from `Albums.razor` | ✅ | Architecture |
| 5-6 | Add `ActivateMemberWithOverrideAsync` and remove DbContext from Members | ✅ | Architecture, testability |
| 7 | Replace DbContext in `EventEnrollments.razor` | ✅ | Architecture |
| 8 | Extract DTOs from `Members.razor` | ✅ | Code organization |
| 11 | Add `GetByIdOrThrowAsync` extension | ✅ | Code quality |
| 12 | Apply `GetByIdOrThrowAsync` to more services | ✅ | Code quality |
| 15 | Apply `GetByIdOrThrowAsync` to remaining services | ✅ | Code quality |
| 16 | Add unit tests for `SlideshowService` | ✅ | Test coverage |
| 17 | Add unit tests for `RequestService` | ✅ | Test coverage |
| 18 | Extract DTOs from `Rehearsals.razor` | ✅ | Code organization |

**Total**: 13 PRs completed, 18 new tests, 6 DTOs extracted, 28+ methods improved, 3 DbContext injections removed.

---

*This v2.0 plan builds on completed improvements and focuses on the next phase: large page refactoring, comprehensive testing, and architectural polish. Prioritize PRs 19-24 for maximum impact.*
