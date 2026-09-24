# CI/CD and Azure environments

How RTUB is built, tested and deployed, and what each Azure environment is. Setting and secret
**names** are documented here; **values never are** - they live in Azure App Service configuration
and GitHub environment variables/secrets.

Releasing, versions, rollback and the database rules: **`docs/release-and-rollback.md`**.
Why it is built this way: `docs/architecture/adr/0001-production-release-and-rollback.md`.

## Environments at a glance

| | DEV | PROD |
| --- | --- | --- |
| Branch | `dev` (GitHub default branch) | `master` |
| Deployed by | **Deploy • DEV**, every push to `dev` | **Deploy • PROD**, every push to `master`; **Rollback • PROD** by hand |
| App Service | `rtub-dev` → `https://rtub-dev.azurewebsites.net` | `rtub` → `https://rtub.azurewebsites.net` |
| Plan | `ASP-rtub-dev`, **Free F1**, Linux, Italy North (own plan) | `ASP-rtubgroup-848b`, **Basic B1**, Linux, Italy North |
| Runtime | `DOTNETCORE\|10.0` | `DOTNETCORE\|10.0` |
| `ASPNETCORE_ENVIRONMENT` | `Staging` | `Production` |
| GitHub environment | `development` | `production` (deployment branch: `master` only) |
| Azure identity | `rtub-dev-deploy` - Website Contributor on `rtub-dev` | `rtub-prod-deploy` - Website Contributor on `rtub`, Storage Blob Data Contributor on the release archive |
| Artifact | one zip per push, workflow artifact of that run (1 day) | one zip per release, archived privately and immutably as `releases/<version>/` |
| `/api/version` | `<VERSION>-dev.<run>` + commit | `<VERSION>` + commit |
| Rollback | push again | **Rollback • PROD** redeploys any archived version |
| Run from package | unset | `WEBSITE_RUN_FROM_PACKAGE=1` |
| Database | `Data Source=/home/site/data/<file>.db` - **read the live setting; never hardcode it** (`rtub-dev-v3.db` as of 2026-09-22) | `Data Source=/home/site/data/app.db` |
| Daily backup | off (`DatabaseBackup__Enabled=false`) | on, to the private `rtub-db` R2 bucket at 03:30 UTC |
| Pre-migration snapshot | yes | yes |
| Always On | off | **on** - the daily backup needs it |
| `healthCheckPath` | unset | unset |
| Basic auth publishing | SCM off, FTP off | SCM off, FTP off |

Both App Services and both plans are in resource group `rtub_group`, subscription *Azure for
Students* (tenant `ipbpt.onmicrosoft.com`). DEV is on its own plan so it can never take CPU or
memory from production, whose B1 already runs at roughly 78% average memory.

## Workflows

The Actions sidebar is meant to be read without opening anything:

| Workflow | File | Trigger | Does |
| --- | --- | --- | --- |
| **CI • Build & Test** | `ci.yml` | PRs into `dev`/`master`; called by both deploy workflows | restore, Release build, all five test suites, coverage + TRX artifacts. PRs into `master` also run *VERSION is bumped*. Deploys nothing. |
| **Deploy • DEV** | `deploy-dev.yml` | push to `dev` | CI → package one zip → deploy to `rtub-dev` → smoke. |
| **Deploy • PROD** | `deploy-prod.yml` | push to `master` | VERSION check → CI → package one zip → archive → read back → schema gate → deploy to `rtub` → smoke → tag. |
| **Rollback • PROD** | `rollback-prod.yml` | manual only | redeploy an archived version to `rtub` → smoke. Never builds. |
| **Database • Refresh DEV from PROD** | `refresh-dev-database.yml` | manual only | sanitized production backup → `rtub-dev` (unit 029, `docs/cloudflare-r2-and-database-backups.md`). |

Shared pieces, so nothing is implemented twice:

| Piece | Used by |
| --- | --- |
| `.github/actions/package-release` - the only `dotnet publish` | Deploy • DEV, Deploy • PROD |
| `.github/actions/deploy-and-verify` - `azure/webapps-deploy@v3` + smoke with the expected build | Deploy • DEV, Deploy • PROD, Rollback • PROD |
| `scripts/release.sh` - version, bump check, package, zip guards, `release.json`, schema gate | the above |
| `scripts/release-archive.sh` - the archive: create-only put, verified get, list | Deploy • PROD, Rollback • PROD |
| `scripts/smoke-azure.sh` - read-only smoke (`smoke-azure-dev.sh` pins it to `rtub-dev`) | all four deploying workflows |

`ReleaseWorkflowTests` pins this: the five names, the triggers, one `dotnet publish`, no publish
profile, no app-settings call in a production workflow, a rollback that cannot build, and a smoke
test that always expects a version and a commit.

**Concurrency.** `deploy-dev` queues DEV deploys (`cancel-in-progress: false` - cancelling
mid-upload would leave half a package). Deploy • PROD's deploy job and Rollback • PROD share the
group `production`: they never overlap. GitHub keeps only the newest *pending* run per group, so a
push to `master` while a rollback is waiting replaces the waiting rollback.

**The repository is public, so workflow logs are public.** `az webapp config appsettings set` and
`list` print every setting, secrets included; production workflows make no app-settings call at all.
Workflow inputs reach scripts through `env:`, never by `${{ }}` interpolation into a `run:` body.

## Test execution

```
dotnet test --no-build --configuration Release \
  --results-directory ./coverage \
  --report-xunit-trx \
  --coverage --coverage-output-format cobertura
```

This discovers and runs all five suites: `global.json` carries
`{ "test": { "runner": "Microsoft.Testing.Platform" } }`, which makes `dotnet test` drive MTP rather
than VSTest (unit 011). Report filenames are left unset on purpose: all five projects write into one
results directory and MTP assigns unique names.

Baseline (unit 030): **4855 total, 0 failed**. On Windows 66 are skipped; on the Linux runner six of
those (the bash script self-tests) run, leaving 60.

## Build and packaging

**Node is required for publish.** `RTUB.csproj`'s `BuildPixiTS` target runs `npm ci
--ignore-scripts` + `npm run build:pixi` before publish. CI uses **Node 22** (Node 20 left
maintenance in April 2026; 22 satisfies `vite ^6`, `cross-env ^10`, `typescript ^5.7` as pinned).
`dotnet build` and `dotnet test` do not need Node.

**linux-x64, framework-dependent**, in `.github/actions/package-release` only:

```bash
dotnet publish src/RTUB.Web/RTUB.csproj -c Release -r linux-x64 --self-contained false
```

| | portable (`-c Release`) | `-r linux-x64 --self-contained false` |
| --- | --- | --- |
| Size | 330.5 MB / 1291 files | **266.6 MB** / 1263 files |
| `runtimes/` | 22 RIDs, 71.8 MB | absent |
| `libQuestPdfSkia.so`, `libe_sqlite3.so` | under `runtimes/linux-x64/native/` | **publish root** |

The RID is scoped to that one command - no `RuntimeIdentifier` anywhere in the build - so Windows
development and the test projects are untouched. The zip is ~230 MB (mostly `wwwroot/sprites`).

**Native libraries are startup-fatal.** `QuestPDF.Settings.License` is set at the top of
`Program.cs`, before any service exists; a missing `libQuestPdfSkia.so` aborts the container with
`DllNotFoundException` and exit 134. `libe_sqlite3.so` fails the same way one step later.

**The guards run on the zip** (`scripts/release.sh verify`), not on the folder: `RTUB.dll`, deps and
runtimeconfig present; both native libraries present, non-empty and ELF64 x86-64; manifest,
service worker and offline page present; at least 500 files under `wwwroot/`; no backslash or
path-traversal entry names. Its `--self-test` proves each refusal.

**Never package with `Compress-Archive`.** It writes entry names with backslashes; Linux unpacks
`wwwroot\manifest.webmanifest` as one flat file. With run-from-package the app still starts and
answers `/health`, but every static asset 404s ("The WebRootPath was not found"); without it Kudu's
rsync fails with `Invalid argument (22)`. Python's `zipfile` also hides the problem when *reading*
on Windows (it rewrites `os.sep`), so the guard reads `orig_filename`. `release.sh package` writes
with `zipfile`, whose rewrite is correct on write.

## Run from package

`WEBSITE_RUN_FROM_PACKAGE=1` is **supported and proven** for this .NET app on App Service Linux.
Microsoft's current documentation excludes only Python and Java; an older "Windows only" quote this
document used to carry is obsolete. PROD (`rtub`) runs this way; on DEV (`rtub-dev`) the setting is unset.

With it, a zip deploy stores the zip as-is in `/home/data/SitePackages/` and restarts the app, which
mounts the zip read-only as `/home/site/wwwroot`: only complete deployments ever run, and there is no
extract-and-rsync step to truncate. Nothing in RTUB writes into `wwwroot` at runtime, and SQLite
lives under `/home/site/data`, so read-only costs nothing.

Historical observation from `rtub-dev` on 2026-09-22, when it still ran from package (it no longer
does): exactly the last five zips are kept, named
`yyyyMMddHHmmss.zip`, and there is **no `packagename.txt`** - so that folder is not a way to roll
back. Rollback uses the release archive instead (ADR 0001). Kudu keeps five by default
(`SCM_MAX_ZIP_PACKAGE_COUNT`).

An app running a *local* package cannot later be switched to a remote package URL (Microsoft); not
needed here.

## GitHub Actions → Azure authentication

OIDC federation against user-assigned managed identities. No publish profile, no client secret,
nothing long-lived in the repository.

| | DEV | PROD |
| --- | --- | --- |
| Identity | `rtub-dev-deploy` | `rtub-prod-deploy` |
| Federated credential | `github-dev-env` | `github-production-env` |
| Subject | `repo:luisfpires18/RTUB:environment:development` | `repo:luisfpires18/RTUB:environment:production` |
| Issuer / audience | `https://token.actions.githubusercontent.com` / `api://AzureADTokenExchange` | same |
| Roles | Website Contributor on the `rtub-dev` site | Website Contributor on the `rtub` site; Storage Blob Data Contributor on the archive container |

The subject is the *environment* form because the deploying jobs declare `environment:`; a
branch-form credential would not match. A job needs `permissions: { id-token: write }` to mint the
token; in Deploy • PROD only the deploy job has it - the build job runs npm and NuGet code and gets
read access to the repository only.

Environment **variables** (identifiers, not secrets): `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`,
`AZURE_SUBSCRIPTION_ID` on both; `RELEASE_STORAGE_ACCOUNT` on `production`. The `development`
environment also holds the four refresh secrets listed in the backups document.

## Azure DEV notes

- DEV was created on Free F1, moved to B1 by the owner after the unit 027 incident below, and moved
  back to **F1** by the owner once the Free-tier limits were judged acceptable.
- **Never set `healthCheckPath` on an app that has no working code yet.** Azure probes it every
  minute and restarts the instance on failure. On F1, with 15 worker stop requests per day, unit
  027's DEV disabled itself within the hour (`WPStopRequests` 36/15, `403 Site Disabled` until
  00:00 UTC) while CPU and egress were still at zero. On B1 it is merely pointless with one
  instance.
- `Staging` behaves like production (`!IsDevelopment()`): HSTS, exception page, response
  compression, 30-day static caching. It is not `Test`: migrations and seeding run at startup.
  There is no `appsettings.Staging.json`.
- **DEV holds real personal data** after a refresh (unit 029): treat access to it as
  production-equivalent, and never give it production R2 or backup credentials.

## Database

`/home` is the Azure Files share mounted into the container: it survives restarts, redeploys and
scaling. Never point `ConnectionStrings__SqliteConnection` outside `/home` - anything else is on the
container's ephemeral layer.

`Program.cs` creates the database directory if needed, takes a pre-migration snapshot when an
existing database has pending migrations, applies them, then seeds. Rules and restore procedure:
`docs/release-and-rollback.md` → *Database*.

### Seeding

`SeedData.InitializeAsync` seeds only when the database has **no users at all**.
`SeedData:SeedFullDataset` decides what:

| Value | Result | Also requires |
| --- | --- | --- |
| unset / `false` | Owner account only - the production path | `AdminUser:Password` |
| `true` | Owner **plus** the full development member dataset - DEV | `AdminUser:Password` **and** `SeedData:MemberPassword` |

Only `true`/`false` (any case) are accepted; `1`, `yes` or an empty string throw and the app refuses
to start, so a portal typo cannot silently pick a seed.

## App Service setting names

Names only; values live in Azure. None of these are in the repository or applied by a deploy.

| Name | DEV | PROD | Note |
| --- | --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Staging` | `Production` | |
| `ASPNETCORE_URLS`, `WEBSITES_PORT` | set | set | must agree |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` | `true` | real client address and scheme behind the Azure front end |
| `ConnectionStrings__SqliteConnection` | set | set | under `/home/site/data` |
| `WEBSITE_RUN_FROM_PACKAGE` | unset | `1` | see above |
| `AdminUser__Password` | set | not needed while users exist | startup-fatal on an empty database |
| `SeedData__SeedFullDataset`, `SeedData__MemberPassword` | set | absent | |
| `DatabaseBackup__Enabled` | `false` | `true` | plus `DatabaseBackup__Bucket`/`AccessKeyId`/`SecretAccessKey`/`AccountId`/`ScheduledTime` in PROD |
| `Cloudflare__R2__AccountId`, `…AccessKeyId`, `…SecretAccessKey`, `…Bucket`, `…PublicUrl` | DEV bucket, bucket-scoped token | production | **required**: the home page resolves storage services |
| `Cloudflare__R2__ReferencePublicUrl`, `Cloudflare__R2__Reference__*` | set | never | DEV's read-only view of production media (unit 029) |
| `WebPush__Vapid*`, `EmailSettings__*` | optional | set | absence disables the feature, not startup |

**R2 is required, not optional.** `Index.razor` (`@page "/"`) resolves `ISlideshowService` →
`CloudflareImageStorageService` → `IAmazonS3`; without credentials the app starts and `/health`
answers 200, but `/` returns 500. `IAmazonS3` reaches 13 domain services through 11 storage
services. Making it optional was rejected: it would turn real storage failures into silence.

`RemoteNavigationManager already initialized` in a log is fallout from an earlier unhandled
exception (`UseExceptionHandler` re-rendering `/Error` on the same `HttpContext`), not a bug of its
own.

## Deployment smoke test

`scripts/smoke-azure.sh` (read-only), run by `.github/actions/deploy-and-verify` after every deploy
and rollback with the build it expects:

1. **Waits until `/api/version` reports the expected version AND commit** (up to 10 minutes). Until
   the restart happens, the old instance keeps answering `/health` with 200 - the smoke test used
   to be able to pass against it. A release built before `/api/version` existed is recognised by
   its 404 instead.
2. `/health`, `/login`, `/manifest.webmanifest`, `/service-worker.js` all 200.
3. HTML carries `Content-Security-Policy`; `/service-worker.js` carries **none** (a CSP served with a
   worker script governs the worker's own fetches and breaks its cross-origin caching).
4. Still up five seconds later, still the same build.

By hand: `SKIP_AZ=1 APP=rtub EXPECT_VERSION=2.0.0 EXPECT_COMMIT=<sha> ./scripts/smoke-azure.sh`.
Without `SKIP_AZ` it also reads site state and `healthCheckPath` with `az` - reads only; if the
site is stopped it prints the start command and exits.

## `/api/version` and `/health`

- `GET /api/version` → `{"version":"2.0.0","commit":"<40-hex>"}`. Anonymous, `Cache-Control:
  no-store`, JSON (so no CSP), under `/api/` so the service worker never caches it. Version and
  commit only - the repository is public, so the commit reveals nothing new.
- `GET /health` → `MapHealthChecks` with one `AddDbContextCheck`. Anonymous, read-only,
  network-only for the service worker (`offline.js` uses it as its reachability probe). Do not grow
  it into a health subsystem or put it behind authorization.
