---
name: rtub-sqlite
description: RTUB-specific SQLite + EF Core facts — the IDbContextFactory/repository contract, WAL and busy-timeout wiring, the migrations project quirk (MigrationsAssembly "RTUB"), the two different test database setups, and how backup/restore interacts with WAL and Cloudflare R2. Load this whenever the task touches EF Core repositories or ApplicationDbContext, adds or edits a migration, changes SQLite connection strings or PRAGMAs, debugs "database is locked"/SQLITE_BUSY, writes or fixes database-backed tests, or changes backup/restore or a hosted service that reads or writes the database. Do not load it for backend work that never touches the database.
---

# RTUB SQLite / EF Core

Facts that are non-obvious from the code and easy to get wrong. Everything here was verified
against the current implementation. Read the linked file when you need the detail.

## Data access contract

`IDbContextFactory<ApplicationDbContext>` is the access pattern — **one context per operation**,
created and disposed inside the method. Blazor Server circuits are long-lived; a context held
across renders leaks and cross-talks.

`Repository<T>` (`src/RTUB.Application/Repositories/Repository.cs`) already implements this, and
each mutating method **saves internally**. There is no `SaveChangesAsync` on `IRepository<T>`;
do not add one and do not look for a unit-of-work — `AddAsync`/`UpdateAsync`/`DeleteAsync` are
each a complete transaction.

Consequences worth knowing before you write repository code:

- `UpdateAsync` is fetch-then-`SetValues`: it reloads the entity by PK into a fresh context and
  copies **scalar** values. Navigation-property and collection changes on a detached entity are
  silently dropped. Modify children through their own repository.
- `UpdateAsync` throws `InvalidOperationException` when the row is gone. Deleted-in-between is
  an exception, not a no-op.
- Read methods use `AsNoTracking()`. Use `QueryAsync(...)` for custom queries — it hands you an
  `IQueryable` inside a scoped context and disposes it. Never return an unmaterialized
  `IQueryable` out of a repository; that leaks the context.
- `InventoryService` and `ApplicationDbContext` are partial classes split across files. Edit the
  right partial.

## SQLite configuration

Registered in `AddDatabaseServices` (`src/RTUB.Web/Extensions/ServiceCollectionExtensions.cs`)
with `QuerySplittingBehavior.SplitQuery` as the default and `SqliteConnectionInterceptor`
attached.

`src/RTUB.Application/Data/SqliteConnectionInterceptor.cs` runs on **every** connection open:
`journal_mode=WAL`, `busy_timeout=30000`, `synchronous=NORMAL`, `temp_store=MEMORY`,
`mmap_size=256MB`, `cache_size=-64000`. Failures are swallowed on purpose (read-only databases
must still work), so a silently missing PRAGMA is possible — check there first when concurrency
behaves oddly.

`Program.cs` sets `DefaultTimeout=30` and **deliberately does not set `Cache=Shared`**.
Shared-cache adds table-level locks that deadlock against WAL under Blazor Server. If you hit
`SQLITE_BUSY`, do not "fix" it by adding shared cache — look for a long-lived context or a write
held open across an `await`.

Connection string key is `ConnectionStrings:SqliteConnection`, environment-supplied only; it is
absent from both `appsettings.json` files and falls back to `Data Source=app.db`.

## Migrations

The migrations assembly is `"RTUB"` — the Web host project is `src/RTUB.Web/RTUB.csproj`
(assembly name `RTUB`, not `RTUB.Web`), and migrations live in `src/RTUB.Web/Migrations/` even
though `ApplicationDbContext` is in `RTUB.Application`. Run EF CLI against the Web project:

```bash
dotnet ef migrations add <Name> --project src/RTUB.Web
```

- Commit the `.Designer.cs` alongside the migration and the updated
  `ApplicationDbContextModelSnapshot.cs`. Existing migrations are paired; an unpaired one breaks
  the next `migrations add`.
- `PendingModelChangesWarning` is suppressed in `AddDatabaseServices`, so a drifted model will
  **not** fail the build. Verify the generated migration by reading it.
- SQLite cannot do most `ALTER TABLE` operations. Several existing migrations drop to raw SQL to
  avoid EF emitting `PRAGMA` statements inside a transaction — follow that precedent when
  altering or dropping columns.
- Migrations and seeding run at startup in `Program.cs`, guarded by
  `app.Environment.EnvironmentName != "Test"` and only when `GetPendingMigrationsAsync()` is
  non-empty.
- Before migrating a database that already has migration history, `Program.cs` calls
  `PreMigrationSnapshot.Take` (`src/RTUB.Application/Services/PreMigrationSnapshot.cs`): online
  backup → `journal_mode=DELETE` → validated → `<db dir>/backups/pre-migration/`, newest 5 kept.
  It **throws** when it cannot, which aborts startup before anything is migrated. Do not catch it.
- Every migration must work with the previous production release (N-1, expand/contract): an app
  rollback never changes the schema. Rule and restore procedure: `docs/release-and-rollback.md`.
- Only classes carrying `[Migration("…")]` are migrations. `UpdateCardStatusEnumValues` and
  `NerbaOrderEventRequired` have no attribute and have never run anywhere; the release manifest
  lists attribute IDs, not file names.

## Tests — two different databases

The two test setups are not interchangeable; pick by what you are testing.

**Unit tests** (`tests/RTUB.Application.Tests/Fixtures/DatabaseFixture.cs`) use the EF Core
**InMemory** provider with a mocked `IDbContextFactory`. It is not relational: raw SQL,
transactions, and split-query behavior do not apply. It *does* enforce `[Required]` data
annotations that SQLite would not, so seeding `ApplicationUser` requires `FirstName`, `LastName`
and `Nickname` (`src/RTUB.Core/Entities/ApplicationUser.cs`).

`CleanDatabase` deliberately **does not clear `Users`**, and it clears an explicit hand-written
list of `DbSet`s — add yours when you add an entity, or tests leak rows into each other. With
`IClassFixture<DatabaseFixture>` the database is shared across methods in the class, so clean and
seed in the constructor.

**Integration tests** (`tests/RTUB.Integration.Tests/TestWebApplicationFactory.cs`) use real
SQLite over a single shared `DataSource=:memory:` connection that is held open for the fixture's
lifetime — closing it destroys the schema. Schema comes from `EnsureCreated()`, **not**
migrations, so integration tests never exercise the migration path.

## Backups, restore, and background services

Full detail lives in `docs/cloudflare-r2-and-database-backups.md` — read it before changing any
of this. The load-bearing points:

- WAL means a file copy of `app.db` + `-wal` + `-shm` is **not** a valid backup. Snapshots use
  `SqliteConnection.BackupDatabase()` (online backup API), validated with `PRAGMA quick_check`,
  a schema-object count, and a size ratio against the live file, then promoted to `current.db`
  in a private R2 bucket only after upload succeeds. Two generations, current and previous.
  See `src/RTUB.Application/Services/DatabaseBackupBackgroundService.cs` and
  `src/RTUB.Application/Configuration/DatabaseBackupOptions.cs`.
- Backups are `Enabled = false` by default so non-production never writes to the bucket.
- Restore: stop the App Service, replace `app.db`, and delete stale `app.db-wal` / `app.db-shm`
  sidecars before restarting — leftover sidecars will resurrect old pages over the restored file.
- Every scheduler guards only with an in-process `_lastRunDate`, so a scaled-out App Service
  fires each instance independently. Jitter plus a same-day check on `current.db` mitigate this
  for backups; there is no lease table. Assume single-instance when adding a DB-writing hosted
  service, and say so if you rely on it.
- Hosted services are singletons — resolve scoped DB dependencies through `IServiceScopeFactory`,
  never inject a repository or context directly.

## Related

`docs/backend-practices.md` for general C#/EF Core conventions,
`docs/architecture/system-index.md` for where data-layer files live.
