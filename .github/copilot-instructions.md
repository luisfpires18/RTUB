# RTUB — AI Coding Instructions

## Architecture

ASP.NET Core host (.NET 10, SQLite) with a **React 19 + Vite** frontend, following Clean Architecture:

```
Core (entities, enums, domain logic) → Application (services, repos, DTOs, EF Core) → Web (host, minimal-API endpoints, controllers, React portal)
```

Dependencies flow inward — `Web` references all, `Core` references nothing. All projects target `net10.0`. Central package versioning via `Directory.Packages.props` (no versions in `.csproj`). `TreatWarningsAsErrors` is enabled globally.

**There is no Blazor.** The host was retired in task 037: no `.razor` components, no `App.razor` / `MainLayout`, no `/_blazor` or `blazor.web.js`, no `RTUB.Shared`, no bUnit. Do not reintroduce them, and do not reintroduce the removed routes (`/games`, `/bets`, `/my-tuno`, `/messages`, `/images`, `/labels`, `/share`). Every page is the React shell; `/Error` is the only server-owned static page.

## Commands

| Action | Command |
|---|---|
| Build | `dotnet build RTUB.sln` |
| Run | `dotnet run --project src/RTUB.Web/RTUB.csproj` |
| Test | `dotnet test RTUB.sln` |
| Migrations | `dotnet ef migrations add <Name> --project src/RTUB.Web` |
| Portal type-check / build (from `src/RTUB.Web`) | `npm run check:portal` / `npm run build:portal` |

## Entity Conventions

- Inherit from `BaseEntity` (provides `Id`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`)
- Use **static factory methods** (`Entity.Create(...)`) — not `new Entity()`
- Keep a `public Entity() { }` constructor for EF Core
- Business mutations via named methods (`UpdateDetails(...)`, `SetImage(...)`)
- Data annotations use **Portuguese error messages** for user-facing validation
- Navigation properties: `virtual ICollection<T>` initialized to `new List<T>()`
- The messaging entities (`Conversation`, `Message`, `MessageReaction`, `ConversationUserSettings`) and their tables are **preserved for Messaging v2** — never drop or delete them.

## Service Conventions

- One `I*Service` interface per service, registered as **Scoped** in `ServiceCollectionExtensions.cs`
- Constructor-inject repositories (`IRepository<T>`), `UserManager<ApplicationUser>`, `ILogger<T>`, `IOptions<T>`
- All async methods use the `Async` suffix
- Guard clauses at method entry; throw `EntityNotFoundException`, `ArgumentException`, `InvalidOperationException`
- `RTUB.Application` stays framework-neutral: no Blazor / Razor Components types (`IBrowserFile`, `AuthenticationState`, `IJSRuntime`, `NavigationManager`)
- XML `<summary>` doc comments on public members

## Repository Pattern

`Repository<T>` (in `Application/Repositories/`) uses `IDbContextFactory<ApplicationDbContext>` — one context per operation. **Critical behavior:**
- `AddAsync()`, `UpdateAsync()`, `DeleteAsync()` each save immediately — every mutation is a DB write
- Reads use `AsNoTracking()` by default
- `UpdateAsync()` uses fetch-then-`SetValues()`
- Extend `Repository<T>` for domain-specific queries (e.g., `EventRepository`)

## API + React

- React pages call minimal-API endpoint groups in `src/RTUB.Web/Endpoints/` (`/api/*`); the endpoint checks authorization and delegates to an Application service. Every rule is enforced server-side; the UI only hides.
- Mutating calls carry the antiforgery token (`X-CSRF-TOKEN`); sensitive responses are `no-store`.
- All user-facing text in **Portuguese** (pt-PT).
- Route ownership is code: `src/RTUB.Web/Program.cs`. Portal rules: `docs/react-portal-pilot.md`; per-module docs `docs/react-*.md`.

## Testing

xUnit v3 + Moq + FluentAssertions. Naming: `MethodName_Scenario_ExpectedResult`. Tests use **real repositories** backed by EF InMemory / SQLite (not mocked repos). `DatabaseFixture` provides a shared context with `CleanDatabase()`. Integration tests use `TestWebApplicationFactory`. React behaviour is pinned by source-contract tests in `tests/RTUB.Web.Tests/ReactPortal/`.

**Mandatory green gate:** After any code change, run `dotnet test RTUB.sln` and ensure **100 % of tests pass** (0 failures) before considering the work complete.

## Configuration and secrets

Never commit secrets (VAPID private keys, R2 keys, SMTP passwords, connection strings with credentials). Settings per environment and the safe audit scripts: `docs/configuration-runbook.md`.

## Key Files

- DI registration: `src/RTUB.Web/Extensions/ServiceCollectionExtensions.cs`
- DB context: `src/RTUB.Application/Data/ApplicationDbContext.cs` (partial)
- Practice docs: `docs/backend-practices.md`, `docs/react-portal-pilot.md`, `docs/pwa-practices.md`
