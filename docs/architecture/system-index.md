# RTUB System Index

Routing map only — paths plus one-line responsibilities. No architecture prose here; follow the link.

## Projects

| Path | Responsibility |
| --- | --- |
| `src/RTUB.Core/` | Domain entities, enums, constants, attributes, pure helpers. No external dependencies. |
| `src/RTUB.Application/` | Business logic: services, repositories, EF Core data layer, DTOs, factories, interfaces. |
| `src/RTUB.Shared/` | Reusable Razor components, base classes, shared static assets. |
| `src/RTUB.Web/` | Blazor Interactive Server host: pages, layout, SignalR hub, a few support controllers, DI wiring. |
| `tools/` | Standalone console utilities run by hand or by a manual workflow. Not part of the deployed app. |

## Data / SQLite

| Path | Responsibility |
| --- | --- |
| `src/RTUB.Application/Data/ApplicationDbContext.cs` | EF Core context (partial class). |
| `src/RTUB.Application/Data/ApplicationDbContext.EntityDisplay.cs` | Entity display-name partial. |
| `src/RTUB.Application/Repositories/` | Repository implementations. |
| `src/RTUB.Web/Migrations/` | EF Core migrations. |
| `src/RTUB.Web/app.db` | Local development SQLite database. |

Access pattern: `IDbContextFactory<ApplicationDbContext>` — one context per operation.

## Web host

| Path | Responsibility |
| --- | --- |
| `src/RTUB.Web/Program.cs` | Host bootstrap; DI delegated to extensions. |
| `src/RTUB.Web/Extensions/ServiceCollectionExtensions.cs` | Service registrations. |
| `src/RTUB.Web/Pages/` | Routable Blazor pages, grouped by area. |
| `src/RTUB.Web/Components/`, `src/RTUB.Web/Shared/` | Host-local components and layout. |
| `src/RTUB.Web/Hubs/MessagesHub.cs` | SignalR messaging hub. |
| `src/RTUB.Web/Controllers/` | Non-Blazor endpoints only: push subscriptions, image/media serving, CDN proxy. |
| `src/RTUB.Web/Interop/` | JS interop wrappers. |

## PWA / service worker

| Path | Responsibility |
| --- | --- |
| `src/RTUB.Web/wwwroot/service-worker.js` | Service worker: caching, offline, push handling. |
| `src/RTUB.Web/wwwroot/manifest.webmanifest` | Web app manifest. |
| `src/RTUB.Web/wwwroot/offline.html` | Offline fallback page. |
| `docs/pwa-practices.md` | Authoritative PWA guidance. |

## Push notifications

| Path | Responsibility |
| --- | --- |
| `src/RTUB.Application/Configuration/WebPushOptions.cs` | VAPID / feature-flag options. |
| `src/RTUB.Application/Services/PushNotificationService.cs` | Web Push send logic, subscription lifecycle, inbox fallback. |
| `src/RTUB.Application/Factories/PushNotificationFactory.cs` | Notification titles/bodies/URLs/tags. |
| `src/RTUB.Application/Repositories/PushSubscriptionRepository.cs`, `src/RTUB.Core/Entities/PushSubscription.cs` | Subscription persistence. |
| `src/RTUB.Web/Controllers/PushController.cs` | `/api/push/*` subscription and send endpoints. |
| `src/RTUB.Web/wwwroot/js/push-notifications.js` | Browser-side subscription manager. |
| `src/RTUB.Web/wwwroot/service-worker.js` | `push` / `notificationclick` / `pushsubscriptionchange` handlers. |
| `src/RTUB.Shared/Components/UI/PushNotificationPrompt.razor`, `PushNotificationToggle.razor` | Opt-in UI. |
| `.claude/skills/rtub-push/SKILL.md` | RTUB-specific push behavior and constraints. |

VAPID keys are configuration/secrets — never committed.
Email notifications are a separate channel (`src/RTUB.Application/Services/EmailNotificationService.cs`).

## Storage / R2 / backups

| Path | Responsibility |
| --- | --- |
| `src/RTUB.Application/Services/Cloudflare*StorageService.cs` | Per-domain Cloudflare R2 storage services (images, audio, documents, media, receipts, …). |
| `src/RTUB.Application/Services/Storage/` | Shared storage abstractions. |
| `src/RTUB.Application/Services/DatabaseBackupBackgroundService.cs` | Scheduled SQLite backup to object storage. |
| `src/RTUB.Application/Services/PreMigrationSnapshot.cs` | Startup restore point before migrating an existing database; fails closed, keeps five. |
| `src/RTUB.Application/Services/DatabaseSanitizer.cs` | Offline sanitizer: production snapshot → DEV-safe copy. Never writes to its source. |
| `src/RTUB.Application/Services/Storage/StorageObjectOrigin.cs` | Storage ownership: classifies a stored URL as this environment's, a production reference, external or unknown. |
| `src/RTUB.Application/Services/Storage/ReferenceStorageService.cs` | **Read-only** view of the production bucket for DEV/Staging. No upload, delete or copy member exists. |
| `tools/RTUB.DbSanitizer/` | Console entry point for the sanitizer. Reads the DEV password from `RTUB_DEV_PASSWORD`. |
| `docs/cloudflare-r2-and-database-backups.md` | Authoritative storage/backup design. |

## Background jobs

`src/RTUB.Application/Services/` — `IHostedService`/`BackgroundService` implementations:
`ActivityReminderBackgroundService`, `BackgroundGeocodingWorker`, `BirthdayEmailSchedulerService`, `CalotesNotificationBackgroundService`, `DatabaseBackupBackgroundService`, `MemberStatusUpdateBackgroundService`, `PendingRequestReminderService`, `QuestionNotificationBackgroundService`, `RehearsalApprovalReminderBackgroundService`, `WeeklyNotificationBackgroundService`.

## MyTuno / PixiJS

| Path | Responsibility |
| --- | --- |
| `src/RTUB.Web/pixi/` | TypeScript game source (arena, stage battle, survive mode, scenes) built with Vite. |
| `src/RTUB.Web/Pages/MyTuno/`, `src/RTUB.Web/Pages/Games/`, `src/RTUB.Web/Pages/Inventory/` | Game-facing Blazor pages. |
| `src/RTUB.Application/Services/InventoryService.*.cs` | Inventory/forge/equipment logic (partial class, 5 files). |
| `src/RTUB.Web/scaling.config.json` | Enemy/stage scaling configuration. |
| `docs/my_tuno/` | Game domain docs: equipment, weapons, scaling, survive mode, upgrade costs. |

## Tests

| Path | Responsibility |
| --- | --- |
| `tests/RTUB.Core.Tests/` | Domain/entity/helper tests. |
| `tests/RTUB.Application.Tests/` | Service and repository tests against a real EF Core context. |
| `tests/RTUB.Application.Tests/Fixtures/DatabaseFixture.cs` | Shared in-memory database fixture. |
| `tests/RTUB.Shared.Tests/` | bUnit component tests. |
| `tests/RTUB.Web.Tests/` | Host-level and page tests. |
| `tests/RTUB.Integration.Tests/` | Cross-cutting integration tests. |

## CI / deployment

| Path | Responsibility |
| --- | --- |
| `VERSION` | The release version, SemVer `MAJOR.MINOR.PATCH`. Stamped into every build by `Directory.Build.props`. |
| `.github/workflows/ci.yml` | **CI • Build & Test**: build + all suites on PRs; called by both deploy workflows. PRs into `master` also require a VERSION bump. |
| `.github/workflows/deploy-dev.yml` | **Deploy • DEV**: push to `dev` → CI → one zip → `rtub-dev` → smoke. |
| `.github/workflows/deploy-prod.yml` | **Deploy • PROD**: push to `master` → CI → one zip → private immutable archive → deploy the archived bytes to `rtub` → smoke → tag. |
| `.github/workflows/rollback-prod.yml` | **Rollback • PROD**: manual; redeploys an archived version. Never builds. |
| `.github/workflows/refresh-dev-database.yml` | **Database • Refresh DEV from PROD**: manual; sanitized production snapshot → `rtub-dev`. Never writes to `rtub-db`. |
| `.github/actions/package-release/` | The only `dotnet publish` (linux-x64, framework-dependent) → one guarded zip. |
| `.github/actions/deploy-and-verify/` | `azure/webapps-deploy` + smoke that waits for the expected version and commit. |
| `docs/release-and-rollback.md` | **Runbook**: branch model, versions, releasing, archive, rollback, hotfix, database rules and restore. |
| `docs/ci-cd-and-azure-environments.md` | DEV/PROD matrix, workflows, packaging, run-from-package, OIDC identities, setting names, smoke. |
| `.deployment`, `Directory.Build.props`, `Directory.Packages.props` | Deployment hook (inert), central build settings + version stamping, central package versions. |
| `global.json` | .NET SDK pin **and** `test.runner: Microsoft.Testing.Platform` — what makes `dotnet test` discover the xUnit v3 suites. |
| `scripts/release.sh` | version / bump-check / package / zip guards / `release.json` / schema gate. `--self-test`. |
| `scripts/release-archive.sh` | The release archive: create-only put, verified get, list. `--self-test` against a fake `az`. |
| `scripts/smoke-azure.sh` | Read-only smoke: expected version + commit, `/health`, CSP contracts. `smoke-azure-dev.sh` pins it to `rtub-dev`. |
| `scripts/resolve-dev-db-path.sh` | Validates rtub-dev's `ConnectionStrings__SqliteConnection` and emits the Kudu path of the file the app actually opens. Fails closed; `--self-test`. |
| `src/RTUB.Web/Services/BuildInfo.cs` | Version + commit behind `GET /api/version`. |

## Architecture decisions

`docs/architecture/adr/` — one file per accepted decision. See the directory README for format.

## AI / tooling configuration

| Path | Responsibility |
| --- | --- |
| `CLAUDE.md` | Routing rules for Claude sessions. |
| `STATE.md` | Living execution state — read first. |
| `.claude/settings.json`, `.claude/settings.local.json` | Permissions and enabled plugins. |
| `.claude/skills/` | Project-local skills (vendored upstream skills carry `UPSTREAM.md`). |
| `.github/copilot-instructions.md` | Repository conventions for Copilot. |
| `.github/agents/` | Role-specific agent guidance. |
