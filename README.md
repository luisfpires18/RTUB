# RTUB - Real Tuna Universitária de Bragança

The web platform and installable PWA of the Real Tuna Universitária de Bragança, a traditional Portuguese university
music group ("tuna"). Public site plus the members' area: events, rehearsals, music, gallery, governance, meetings,
treasury, logistics, inventory, shop, documentation and the Owner's operations tools.

## Stack

- **ASP.NET Core on .NET 10**: minimal-API endpoints (`/api/*`) and a few controllers. No Blazor: the host was retired
  in task 037 (no `App.razor`, `MainLayout`, `/_blazor` or `blazor.web.js`; `RTUB.Shared` is gone).
- **React 19 + Vite** (`src/RTUB.Web/portal/`) owns every page. The committed build (`wwwroot/portal`) is published
  as-is. The only other HTML page is the static server page `/Error` (`wwwroot/error.html`).
- **EF Core 10 + SQLite** (WAL), migrated on startup after a pre-migration snapshot.
- **ASP.NET Core Identity** (cookie auth; roles Owner, Admin, Mod, Member). Password recovery and email confirmation
  are React pages over `/api/account/*`.
- **PWA**: one service worker (`wwwroot/service-worker.js`, registered only by `wwwroot/js/sw-register.js`), offline
  page, **Web Push** (VAPID). Members turn push on and off per device in React `/profile`; the Owner sends custom
  pushes from `/notifications`.
- **Cloudflare R2** for every stored file (images, audio, video, documents, receipts) and for the daily production
  database backup. (IDrive is historical only: nothing reads `IDrive:*`.)
- **Maps**: Leaflet with CARTO tiles, geocoding by Nominatim (OpenStreetMap). No API key.
- **Email**: SMTP. **PDF**: QuestPDF.
- **Tests**: xUnit v3 (Microsoft Testing Platform) + Moq + FluentAssertions, four test projects.

## Layout

```
src/
  RTUB.Core/          entities, enums, constants, pure helpers (no dependencies)
  RTUB.Application/   services, repositories, EF Core (ApplicationDbContext, partial), DTOs, background jobs
  RTUB.Web/           host: Program.cs, Endpoints/, Controllers/, Extensions/ (DI), Migrations/,
                      portal/ (React source), wwwroot/ (portal build, service worker, manifest, error/offline pages)
tests/
  RTUB.Core.Tests, RTUB.Application.Tests, RTUB.Web.Tests, RTUB.Integration.Tests
tools/
  RTUB.DbSanitizer/   production snapshot -> DEV-safe copy (manual workflow)
scripts/              release, archive, smoke and configuration-audit scripts
docs/                 runbooks and per-module docs (start at docs/architecture/system-index.md)
```

Removed modules stay removed: games, bets, MyTuno, messages, images, labels and `/share` are plain 404s. Their
database tables are kept; the messaging tables in particular are **preserved for Messaging v2** and must never be
dropped (`docs/messages-removal.md`).

## Getting started

Prerequisites: .NET 10 SDK, Git. Node 22 only to rebuild the React portal.

```bash
git clone https://github.com/luisfpires18/RTUB.git
cd RTUB
dotnet restore
dotnet user-secrets set --project src/RTUB.Web/RTUB.csproj "AdminUser:Password" "<owner-password>"
dotnet run --project src/RTUB.Web/RTUB.csproj
```

`AdminUser:Password` is required the first time the app starts against an empty database (the Owner account is
created from it; there is no default). For the full development member dataset also set
`SeedData:SeedFullDataset` = `true` and `SeedData:MemberPassword`.

**Every setting, where it lives (committed appsettings, user-secrets, Azure App Settings) and how to audit it safely:
[`docs/configuration-runbook.md`](docs/configuration-runbook.md).** Secrets go in user-secrets locally and in Azure App
Settings for DEV/PROD - never in a tracked file. `appsettings.Development.json` is **not** git-ignored.

A local run without `Cloudflare:R2:*` starts, but storage-backed routes answer 500. Point
`ConnectionStrings:SqliteConnection` at a scratch copy before trying things out, or the run writes to
`src/RTUB.Web/app.db`.

### Resetting a local development database (destructive, opt-in)

`SeedData.ResetDevDataAsync` clears every push subscription, resets **every** user's password to one configured value
and rewrites every email to `{UserName}@rtub.pt`. It runs only when the environment is `Development` or `Staging`
(Azure DEV) **and** `DevelopmentDataReset:Enabled` is `true`; `Production`, `Test` and anything else never run it.

```bash
dotnet user-secrets set --project src/RTUB.Web/RTUB.csproj "DevelopmentDataReset:Enabled" "true"
```

```bash
dotnet user-secrets set --project src/RTUB.Web/RTUB.csproj "DevelopmentDataReset:Password" "<development-reset-password>"
```

The password is validated before anything is written and never logged. **Remove `Enabled` once the reset has run**,
or every startup resets again:

```bash
dotnet user-secrets remove --project src/RTUB.Web/RTUB.csproj "DevelopmentDataReset:Enabled"
```

## Build and test

```bash
dotnet build -c Release
```

```bash
dotnet test --configuration Release
```

`global.json` selects the Microsoft Testing Platform runner, so `dotnet test` runs all four suites. CI's exact command
is in `docs/ci-cd-and-azure-environments.md` → *Test execution*.

React portal (from `src/RTUB.Web`, Node 22):

```bash
npm run check:portal
```

```bash
npm run build:portal
```

Commit the rebuilt `wwwroot/portal` with the source change (`docs/react-portal-pilot.md`).

## Deployment

Only GitHub Actions deploys - never a manual or Visual Studio publish:

- merge into `dev` → **Deploy • DEV** (`rtub-dev`)
- bump the root `VERSION` (SemVer), merge `dev` → `master` with a merge commit → **Deploy • PROD** (`rtub`)
- **Rollback • PROD** redeploys any archived version without rebuilding
- `GET /api/version` reports the running version and commit

Releasing, rollback, hotfix and database restore: [`docs/release-and-rollback.md`](docs/release-and-rollback.md).
Environments, workflows and App Service settings: [`docs/ci-cd-and-azure-environments.md`](docs/ci-cd-and-azure-environments.md).
Storage and backups: [`docs/cloudflare-r2-and-database-backups.md`](docs/cloudflare-r2-and-database-backups.md).

## Documentation

- [`docs/architecture/system-index.md`](docs/architecture/system-index.md) - where things live
- [`docs/react-portal-pilot.md`](docs/react-portal-pilot.md) - React route ownership and portal rules; per-module docs `docs/react-*.md`
- [`docs/backend-practices.md`](docs/backend-practices.md), [`docs/pwa-practices.md`](docs/pwa-practices.md)
- [`docs/configuration-runbook.md`](docs/configuration-runbook.md) - settings, user-secrets, Azure App Settings, VAPID keys
- [`docs/architecture/adr/`](docs/architecture/adr/) - architecture decisions

## License and contact

Developed for the Real Tuna Universitária de Bragança. Maintainer: [@luisfpires18](https://github.com/luisfpires18).
Live: [https://rtub.azurewebsites.net/](https://rtub.azurewebsites.net/)
