# RTUB — AI Coding Instructions

## Architecture

Blazor Interactive Server app (.NET 10, C# 14, SQLite) following Clean Architecture:

```
Core (entities, enums, domain logic) → Application (services, repos, DTOs) → Shared (Razor components) → Web (pages, JS interop)
```

Dependencies flow inward — `Web` references all, `Core` references nothing. All projects target `net10.0`. Central package versioning via `Directory.Packages.props` (no versions in `.csproj`). `TreatWarningsAsErrors` is enabled globally.

## Commands

| Action | Command |
|---|---|
| Build | `dotnet build RTUB.sln` |
| Run | `dotnet run --project src/RTUB.Web/RTUB.csproj` |
| Test | `dotnet test RTUB.sln` |
| Migrations | `dotnet ef migrations add <Name> --project src/RTUB.Web` |

## Entity Conventions

- Inherit from `BaseEntity` (provides `Id`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`)
- Use **static factory methods** (`Entity.Create(...)`) — not `new Entity()`
- Keep a `public Entity() { }` constructor for EF Core
- Business mutations via named methods (`UpdateDetails(...)`, `SetImage(...)`)
- Data annotations use **Portuguese error messages** for user-facing validation
- Navigation properties: `virtual ICollection<T>` initialized to `new List<T>()`

## Service Conventions

- One `I*Service` interface per service, registered as **Scoped** in `ServiceCollectionExtensions.cs`
- Constructor-inject repositories (`IRepository<T>`), `UserManager<ApplicationUser>`, `ILogger<T>`, `IOptions<T>`
- All async methods use `Async` suffix and accept `CancellationToken cancellationToken = default`
- Guard clauses at method entry; throw `EntityNotFoundException`, `ArgumentException`, `InvalidOperationException`
- XML `<summary>` doc comments on all public members

## Repository Pattern

`Repository<T>` (in `Application/Repositories/`) wraps `ApplicationDbContext`. **Critical behavior:**
- `AddAsync()`, `UpdateAsync()`, `DeleteAsync()` each call `SaveChangesAsync()` immediately — every mutation is a DB write
- Reads use `AsNoTracking()` by default
- `UpdateAsync()` handles Blazor's long-lived DbContext via tracked-entity detection with `SetValues()` copy
- Extend `Repository<T>` for domain-specific queries (e.g., `EventRepository`)

## Razor Page Structure

Follow this directive order:
```razor
@page "/route"
@rendermode InteractiveServer
@using ...
@attribute [Authorize]
@inject IMyService MyService
@inject IJSRuntime JSRuntime
@inject ILogger<MyPage> Logger
@implements IAsyncDisposable
```

- All user-facing text in **Portuguese**
- Use `RTUB.Shared/Components/UI/` (`<LoadingSpinner />`, `<EmptyState />`) before creating new shared components
- CRUD pages inherit `ManagedModalPageBase<TEntity>` or `CrudTablePageBase<TEntity>` from `Shared/Base/`
- CSS isolation via `.razor.css` scoped stylesheets
- Implement `IAsyncDisposable` when using JS interop or `DotNetObjectReference`

## JS Interop

- Wrap calls in `try-catch` with `ILogger.LogWarning` — never let JS errors crash the circuit
- Namespace JS functions under objects (e.g., `rtubAudioPlayer.playAudio(...)`)

## Testing

xUnit + Moq + FluentAssertions. Naming: `MethodName_Scenario_ExpectedResult`. Tests use **real repositories** backed by EF InMemory provider (not mocked repos). `DatabaseFixture` provides shared context with `CleanDatabase()`. bUnit for Razor component tests. Integration tests use `TestWebApplicationFactory`.

**Mandatory green gate:** After any code change, always run `dotnet test RTUB.sln` and ensure **100 % of tests pass** (0 failures) before considering the work complete. If a change breaks existing tests, fix them in the same commit — never leave the suite red.

## Key Files

- DI registration: `src/RTUB.Web/Extensions/ServiceCollectionExtensions.cs`
- DB context: `src/RTUB.Application/Data/ApplicationDbContext.cs` (~55 DbSets)
- Practice docs: `docs/backend-practices.md`, `docs/frontend-practices.md`
