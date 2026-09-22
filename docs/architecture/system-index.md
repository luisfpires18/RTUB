# RTUB System Index

Routing map only — paths plus one-line responsibilities. No architecture prose here; follow the link.

## Projects

| Path | Responsibility |
| --- | --- |
| `src/RTUB.Core/` | Domain entities, enums, constants, attributes, pure helpers. No external dependencies. |
| `src/RTUB.Application/` | Business logic: services, repositories, EF Core data layer, DTOs, factories, interfaces. |
| `src/RTUB.Shared/` | Reusable Razor components, base classes, shared static assets. |
| `src/RTUB.Web/` | Blazor Interactive Server host: pages, layout, SignalR hub, a few support controllers, DI wiring. |

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
| `.github/workflows/ci.yml` | Build, test, production deploy (`master` push) and Azure DEV deploy (`dev` push). |
| `docs/ci-cd-and-azure-environments.md` | Branch/deploy model, test command, Node version, Azure DEV + production topology, OIDC, setting names. |
| `.deployment`, `Directory.Build.props`, `Directory.Packages.props` | Deployment hook and central build/package versioning. |
| `global.json` | .NET SDK pin **and** `test.runner: Microsoft.Testing.Platform` — what makes `dotnet test` discover the xUnit v3 suites. |
| `scripts/` | Operational helper scripts, run by hand. |
| `scripts/smoke-azure-dev.sh` | Read-only Azure DEV smoke test: `/health`, CSP on HTML, no CSP on the service worker. Writes nothing. |
| `scripts/package-azure-dev.sh` | Packages a publish tree for Azure Linux and rejects backslash separators — never use `Compress-Archive`. |

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
