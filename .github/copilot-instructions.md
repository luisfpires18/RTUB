# RTUB — AI Coding Instructions

## Architecture

ASP.NET Core host (.NET 10, SQLite) with a **React 19 + Vite** frontend, following Clean Architecture:

```
Core (entities, enums, domain logic) → Application (services, repos, DTOs, EF Core) → Web (host, minimal-API endpoints, controllers, React portal)
```

Dependencies flow inward: `Web` references all, `Core` references nothing. All projects target `net10.0`. Central package
versioning via `Directory.Packages.props` (no versions in `.csproj`). `TreatWarningsAsErrors` is on globally.

There is no Blazor: no `.razor` components, no `/_blazor`, no `RTUB.Shared`, no bUnit. Do not reintroduce them or the
removed routes (`/games`, `/bets`, `/my-tuno`, `/images`, `/labels`). Every page is the React shell; `/Error` is the only
server-owned static page.

## Commands

| Action | Command |
|---|---|
| Build | `dotnet build RTUB.sln` |
| Run | `dotnet run --project src/RTUB.Web/RTUB.csproj` |
| Test | `dotnet test RTUB.sln` |
| Migrations | `dotnet ef migrations add <Name> --project src/RTUB.Web` |
| Portal type-check / build (from `src/RTUB.Web`) | `npm run check:portal` / `npm run build:portal` |

## Entities

- Inherit `BaseEntity` (`Id`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`); static factories (`Entity.Create(...)`);
  keep a public parameterless constructor for EF; mutate through named methods.
- Data annotations carry Portuguese error messages.
- Navigation collections: `virtual ICollection<T>` initialized to `new List<T>()`.
- Messaging entities (`Conversation`, `Message`, `MessageReaction`, `ConversationUserSettings`) and tables are preserved for Messaging v2: never drop them.

## Services and repositories

- One `I*Service` per service, Scoped, registered in `ServiceCollectionExtensions.cs`. Constructor-inject repositories,
  `UserManager<ApplicationUser>`, `ILogger<T>`, `IOptions<T>`. `Async` suffix. Guard clauses throw `EntityNotFoundException`,
  `ArgumentException`, `InvalidOperationException`. `RTUB.Application` stays framework-neutral (no Razor/Blazor types).
- `Repository<T>` uses `IDbContextFactory<ApplicationDbContext>`, one context per operation. Add/Update/Delete save
  immediately; reads are `AsNoTracking()`; update is fetch-then-`SetValues()`. Extend it for domain queries.
- More: `docs/backend-practices.md`.

## API + React

- React pages call minimal-API groups in `src/RTUB.Web/Endpoints/` (`/api/*`); the endpoint checks authorization and
  delegates to a service. Every rule is enforced server-side; the UI only hides.
- Mutating calls carry the antiforgery token (`X-CSRF-TOKEN`); sensitive responses are `no-store`.
- All user-facing text is Portuguese (pt-PT).
- Route ownership is code: `src/RTUB.Web/Program.cs`. Portal rules: `docs/react-portal-pilot.md`; per-module docs: `docs/react-*.md`.

## Testing

xUnit v3 + Moq + FluentAssertions; `MethodName_Scenario_ExpectedResult`. Real repositories on EF InMemory / SQLite (not
mocked); `DatabaseFixture` gives a shared context with `CleanDatabase()`; integration tests use `TestWebApplicationFactory`.
React behaviour is pinned by source-contract tests in `tests/RTUB.Web.Tests/ReactPortal/`.

After any code change run `dotnet test RTUB.sln` and require 0 failures before calling work complete.

## Secrets

Never commit secrets (VAPID private keys, R2 keys, SMTP passwords, credentialed connection strings). Settings per
environment: `docs/configuration-runbook.md`.

## Key files

- DI: `src/RTUB.Web/Extensions/ServiceCollectionExtensions.cs`
- DB context: `src/RTUB.Application/Data/ApplicationDbContext.cs` (partial)
- Practice docs: `docs/backend-practices.md`, `docs/frontend-practices.md`, `docs/react-portal-pilot.md`, `docs/pwa-practices.md`
