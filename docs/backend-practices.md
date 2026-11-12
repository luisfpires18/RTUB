# Backend Practices

C# / .NET 10 / EF Core 10 / SQLite rules specific to this repo. Layer overview: `.github/copilot-instructions.md`.

## Layers

```
RTUB.Core         entities, enums, helpers (no dependencies)
RTUB.Application  services, repositories, DTOs, EF Core, background services (framework-neutral)
RTUB.Web          host, minimal-API endpoints, a few controllers, React portal
```

- Endpoints and controllers stay thin: check authorization, validate, delegate to an `RTUB.Application` service.
- `RTUB.Application` references no web UI types (`IBrowserFile`, `AuthenticationState`, `IJSRuntime`, `NavigationManager`).
- Constructor injection only; services are Scoped and registered in `src/RTUB.Web/Extensions/ServiceCollectionExtensions.cs`.
- Entities inherit `BaseEntity`, are created through static factories (`Entity.Create(...)`) and mutated through named
  methods; keep the public parameterless constructor for EF.

## Data access

- One context per operation via `IDbContextFactory<ApplicationDbContext>`. `Repository<T>` (`Application/Repositories/`)
  saves on every `AddAsync` / `UpdateAsync` / `DeleteAsync`, reads with `AsNoTracking()`, and updates by fetch then `SetValues()`.
  Use `GetByIdOrThrowAsync` (throws `EntityNotFoundException`).
- `ApplicationDbContext` is a partial class split across files; edit the right partial.
- Read-only queries: `AsNoTracking()`, projections instead of full entities, batch-load related rows (no queries in loops).
- Never block on async (`.Result`, `.Wait()`).
- Messaging tables (`Conversations`, `Messages`, `MessageReactions`, `ConversationUserSettings`) are never dropped.

### Migrations

- Live in `src/RTUB.Web/Migrations`; the context is wired with `MigrationsAssembly("RTUB")` (the web assembly name), so
  `dotnet ef migrations add <Name> --project src/RTUB.Web`.
- Commit the `.Designer.cs` with every migration.
- Keep each migration compatible with the previous production release (expand/contract): an app rollback never touches
  the schema. See `docs/release-and-rollback.md` → *Migrations: the N-1 rule*.

## Errors and logging

- Missing entity: `EntityNotFoundException`. Guard clauses throw `ArgumentException` / `InvalidOperationException`.
- Log with `ILogger` and structured parameters, never `Console.WriteLine`.
- User-facing messages are Portuguese (pt-PT).

## Portugal time

PROD runs on UTC. Anything that means a Portuguese day or clock uses `RTUB.Core.Helpers.PortugalTime` with the injected
`TimeProvider`:

- `Today(time)` for "today", past / future, ages and the fiscal year (`FiscalYearHelper.GetCurrentFiscalYearStartYear`).
- `Now(time)` for times shown to people ("Gerado em").
- `NextUtc(time, localTime, day?)` for a hosted job's next run at a configured Portugal clock time. DST-safe: a time the
  spring change skips runs an hour later, a time the autumn change repeats runs once.

Stored technical timestamps (audit, `CreatedAt`, tokens, expirations) stay `DateTime.UtcNow`. Never `DateTime.Today` /
`DateTime.Now` in new code. Services take `TimeProvider? time = null` (DI passes `TimeProvider.System`; tests pass a fixed clock).

## Caching

`IMemoryCache` for hot read data (e.g. `DocumentTreeCache`, music, naipes). Set an expiration and invalidate on writes.
Never cache per-user permission results across users.

## Testing

xUnit v3, Moq, FluentAssertions; `MethodName_Scenario_ExpectedResult`; Arrange / Act / Assert.

- Do not mock repositories or `ApplicationDbContext`. Use the real context from `tests/RTUB.Application.Tests/Fixtures/DatabaseFixture.cs`
  (shared in-memory SQLite, `CleanDatabase()` does not clean `Users`). Mock only external dependencies (HTTP, storage,
  email/push, `IHttpContextAccessor`).
- The in-memory provider enforces `[Required]`: seed `ApplicationUser` with `FirstName`, `LastName`, `Nickname`.
- Tests sharing `IClassFixture<DatabaseFixture>` share one database: clean and seed in the constructor.
- No wall-clock timing in tests; inject a fixed `TimeProvider`.
- API behaviour: `tests/RTUB.Integration.Tests/Api/` with `TestWebApplicationFactory`.
- Run `dotnet test RTUB.sln` with 0 failures before calling work done.
