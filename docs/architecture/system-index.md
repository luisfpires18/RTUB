# RTUB System Index

Routing map only — paths plus one-line responsibilities. No architecture prose here; follow the link.

## Projects

| Path | Responsibility |
| --- | --- |
| `src/RTUB.Core/` | Domain entities, enums, constants, attributes, pure helpers. No external dependencies. |
| `src/RTUB.Application/` | Business logic: services, repositories, EF Core data layer, DTOs, factories, interfaces. |
| `src/RTUB.Shared/` | Reusable Razor components, base classes, shared static assets. |
| `src/RTUB.Web/` | Blazor Interactive Server host: pages, layout, a few support controllers, DI wiring. |
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
| `src/RTUB.Web/Controllers/` | Non-Blazor endpoints only: push subscriptions, image/media serving, React session state. |
| `src/RTUB.Web/Interop/` | JS interop wrappers. |

## PWA / service worker

| Path | Responsibility |
| --- | --- |
| `src/RTUB.Web/wwwroot/service-worker.js` | Service worker: caching, offline, push handling. |
| `src/RTUB.Web/wwwroot/manifest.webmanifest` | Web app manifest. |
| `src/RTUB.Web/wwwroot/offline.html` | Offline fallback page. |
| `docs/pwa-practices.md` | Authoritative PWA guidance. |

## React public-portal pilot

| Path | Responsibility |
| --- | --- |
| `src/RTUB.Web/portal/` | React 19 + Vite source of every React page (route list: `Program.cs`, `portal/src/main.tsx`). Not published. |
| `src/RTUB.Web/portal/src/App.tsx`, `MemberShell.tsx` | The shell: visitors' header (public sections, "Pedir atuação", Login); signed-in members' header and member menu (rail ≥ 1000px, drawer below; task 030). |
| `src/RTUB.Application/Helpers/MemberMenuAccess.cs` | Which member-menu groups a member sees (the Blazor navbar's rules), sent as `menu` by `/api/account/me`. Hides links only. |
| `src/RTUB.Web/wwwroot/portal/` | Committed build output served by the host. Rebuild with `npm run build:portal`. |
| `src/RTUB.Web/Program.cs` (React shell mapping) | Route ownership: the only paths React owns, plus the `/portal...` redirects. |
| `src/RTUB.Web/Controllers/AccountController.cs` | `GET /api/account/me`: the caller's own session summary for React (+ `menu` flags since 030). |
| `src/RTUB.Web/Endpoints/PublicRequestEndpoints.cs` | `POST /api/public/requests` (+ antiforgery token): public request submission for React. |
| `src/RTUB.Application/Services/PublicRequestService.cs` | The one public request submission path, called only by the API. |
| `docs/react-portal-pilot.md` | Route ownership, constraints, representative content, CI gap. |
| `src/RTUB.Web/Endpoints/MusicEndpoints.cs`, `src/RTUB.Application/Services/MusicService.cs` | React Music API and its rules (`MusicAuthorization`). `docs/react-music.md`. |
| `src/RTUB.Web/Endpoints/EventEndpoints.cs`, `src/RTUB.Application/Services/EventAgendaService.cs` | React Events API: agenda, one event, a member's enrollment, Admin/Owner create/edit/delete (`EventsAuthorization`). Admin/Owner management (`IEventAdminService`, `IEventRepertoireAdminService`, `IEventParticipantsAdminService`) and members' statistics (`/api/events/stats`). `/member/events` redirects to `/events` (012F); discussion (`IEventDiscussionBoardService`, members) and contact tracking (`IEventContactsAdminService`, Mod and above) are React since 013; no Blazor event page is left. `docs/react-events.md`. |
| `src/RTUB.Web/Endpoints/RehearsalEndpoints.cs`, `src/RTUB.Application/Services/RehearsalAgendaService.cs`, `RehearsalAdminService.cs` | React Rehearsals API (014): list, one rehearsal, own presença, statistics (members); create/range/edit/cancel/delete, notices, confirm/add/remove presenças (Admin/Owner, `RehearsalsAuthorization`). `docs/react-rehearsals.md`. |
| `src/RTUB.Web/Endpoints/GalleryEndpoints.cs`, `src/RTUB.Application/Services/GalleryTimelineService.cs`, `GalleryManagementService.cs` | React Gallery API: viewer-aware timeline (009) and members' upload / edit / delete / tags (015, `GalleryAuthorization`). `/member/gallery` redirects to `/gallery`. `docs/react-gallery.md`. |

## Push notifications

| Path | Responsibility |
| --- | --- |
| `src/RTUB.Application/Configuration/WebPushOptions.cs` | VAPID / feature-flag options. |
| `src/RTUB.Application/Services/PushNotificationService.cs` | Web Push send logic, subscription lifecycle (no inbox copy since 027). |
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

## Games / Bets / MyTuno (removed)

Removed from the app by task 026. Only their EF entities, configurations and DbSets remain, until the contract
task drops the tables: `docs/games-bets-mytuno-removal.md`.

## Images (slideshows) and Labels admin (removed)

The Blazor `/images` and `/labels` admin pages were removed by task 029A (both plain 404s; `/images/<file>` is still
served by `ImagesController`). The Leaderboard story is fixed in code (`LeaderboardService.Story`). Still in place until
029B: the `Label`/`Slideshow` entities, configurations, DbSets, tables and seed data, `ILabelService`/`LabelService`
(last caller: the dead `Components/Portal/*`), `ISlideshowService`/`SlideshowService` (no caller) and their repositories.

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
