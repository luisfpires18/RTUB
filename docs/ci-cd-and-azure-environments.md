# CI/CD and Azure environments

How RTUB is built, tested and deployed, and what each Azure environment is. Setting and secret
names are documented here; values never are. They live in Azure App Service configuration and
GitHub environment variables/secrets.

Releasing, versions, rollback, the release archive, zip guards and database rules:
[release-and-rollback](release-and-rollback.md). Why: `docs/architecture/adr/0001-production-release-and-rollback.md`.

## Environments at a glance

| | DEV | PROD |
| --- | --- | --- |
| Branch | `dev` (GitHub default) | `master` |
| Deployed by | Deploy • DEV, every push to `dev` | Deploy • PROD, every push to `master`; Rollback • PROD by hand |
| App Service | `rtub-dev` (`https://rtub-dev.azurewebsites.net`) | `rtub` (`https://rtub.azurewebsites.net`) |
| Plan | `ASP-rtub-dev`, Basic B1, Linux, Italy North (own plan) | `ASP-rtubgroup-848b`, Basic B1, Linux, Italy North |
| Runtime | `DOTNETCORE\|10.0` | `DOTNETCORE\|10.0` |
| `ASPNETCORE_ENVIRONMENT` | `Staging` | `Production` |
| GitHub environment | `development` | `production` (deployment branch: `master` only) |
| Azure identity | `rtub-dev-deploy`: Website Contributor on `rtub-dev` | `rtub-prod-deploy`: Website Contributor on `rtub`, Storage Blob Data Contributor on the release archive |
| Artifact | one zip per push, workflow artifact of that run (1 day) | one zip per release, archived privately and immutably as `releases/<version>/` |
| `/api/version` | `<VERSION>-dev.<run>` + commit | `<VERSION>` + commit |
| Rollback | push again | Rollback • PROD redeploys any archived version |
| Run from package | not used (`WEBSITE_RUN_FROM_PACKAGE` intentionally absent) | `WEBSITE_RUN_FROM_PACKAGE=1` |
| Database | `Data Source=/home/site/data/<file>.db`; read the live setting, never hardcode the file name | `Data Source=/home/site/data/app.db` |
| Daily backup | off (`DatabaseBackup__Enabled=false`) | on, to the private `rtub-db` R2 bucket at 03:30 UTC |
| Pre-migration snapshot | yes | yes |
| Always On | off | off; must be on for the daily backup to run (owner action) |
| `healthCheckPath` | unset | unset |
| Basic auth publishing | SCM off, FTP off | SCM on until the publish profile is retired, FTP off |

Both App Services and both plans are in resource group `rtub_group`, subscription Azure for
Students (tenant `ipbpt.onmicrosoft.com`). DEV is on its own plan so it can never take CPU or
memory from production, whose B1 already runs at roughly 78% average memory.

## Workflows

| Workflow | File | Trigger | Does |
| --- | --- | --- | --- |
| CI • Build & Test | `ci.yml` | PRs into `dev`/`master`; called by both deploy workflows | restore, Release build, all four test suites, coverage + TRX artifacts. PRs into `master` also run *VERSION is bumped*. Deploys nothing. |
| Deploy • DEV | `deploy-dev.yml` | push to `dev` | CI, package one zip, deploy to `rtub-dev`, smoke |
| Deploy • PROD | `deploy-prod.yml` | push to `master` | VERSION check, CI, package one zip, archive, read back, schema gate, deploy to `rtub`, smoke, tag |
| Rollback • PROD | `rollback-prod.yml` | manual only | redeploy an archived version to `rtub`, smoke. Never builds. |
| Database • Refresh DEV from PROD | `refresh-dev-database.yml` | manual only | sanitized production backup into `rtub-dev` ([cloudflare-r2-and-database-backups](cloudflare-r2-and-database-backups.md)) |

Shared pieces, so nothing is implemented twice:

| Piece | Used by |
| --- | --- |
| `.github/actions/package-release`: the only `dotnet publish` | Deploy • DEV, Deploy • PROD |
| `.github/actions/deploy-and-verify`: `azure/webapps-deploy@v3` + smoke with the expected build | Deploy • DEV, Deploy • PROD, Rollback • PROD |
| `scripts/release.sh`: version, bump check, package, zip guards, `release.json`, schema gate | the above |
| `scripts/release-archive.sh`: create-only put, verified get, list | Deploy • PROD, Rollback • PROD |
| `scripts/smoke-azure.sh`: read-only smoke (`smoke-azure-dev.sh` pins it to `rtub-dev`) | all deploying workflows |

`ReleaseWorkflowTests` pins this: the five names, the triggers, one `dotnet publish`, no publish
profile, no app-settings call in a production workflow, a rollback that cannot build, and a smoke
test that always expects a version and a commit.

Concurrency: `deploy-dev` queues DEV deploys (`cancel-in-progress: false`; cancelling mid-upload
would leave half a package). Deploy • PROD's deploy job and Rollback • PROD share the group
`production` and never overlap. GitHub keeps only the newest pending run per group, so a push to
`master` while a rollback is waiting replaces the waiting rollback.

The repository is public, so workflow logs are public. `az webapp config appsettings set` and
`list` print every setting, secrets included; production workflows make no app-settings call at
all. Workflow inputs reach scripts through `env:`, never by `${{ }}` interpolation into a `run:`
body.

## Test execution

```
dotnet test --no-build --configuration Release \
  --results-directory ./coverage \
  --report-xunit-trx \
  --coverage --coverage-output-format cobertura
```

This runs all four suites: `global.json` carries `{ "test": { "runner": "Microsoft.Testing.Platform" } }`,
which makes `dotnet test` drive MTP instead of VSTest. Report filenames are left unset on
purpose: all test projects write into one results directory and MTP assigns unique names. Some
tests are skipped on Windows; the bash script self-tests run on the Linux runner.

## Build and packaging

Publish needs no Node. The React portal build, `wwwroot/portal`, is committed and published as-is;
rebuild it by hand with Node 22 (`docs/react-portal-pilot.md`). `dotnet build` and `dotnet test`
do not need Node.

The package is linux-x64, framework-dependent, built in `.github/actions/package-release` only:

```bash
dotnet publish src/RTUB.Web/RTUB.csproj -c Release -r linux-x64 --self-contained false
```

The RID is scoped to that one command (no `RuntimeIdentifier` anywhere in the build), so Windows
development and the test projects are untouched. Versus a portable publish it drops the 22-RID
`runtimes/` folder and puts `libQuestPdfSkia.so` and `libe_sqlite3.so` at the publish root.

Native libraries are startup-fatal. `QuestPDF.Settings.License` is set at the top of `Program.cs`,
before any service exists; a missing `libQuestPdfSkia.so` aborts the container with
`DllNotFoundException` and exit 134. `libe_sqlite3.so` fails the same way one step later. The zip
guards (`scripts/release.sh verify`, listed in [release-and-rollback](release-and-rollback.md))
run on the zip, not the folder; `--self-test` proves each refusal.

Never package with `Compress-Archive`. It writes entry names with backslashes; Linux unpacks
`wwwroot\manifest.webmanifest` as one flat file. With run-from-package the app still starts and
answers `/health`, but every static asset 404s ("The WebRootPath was not found"); without it Kudu's
rsync fails with `Invalid argument (22)`. Python's `zipfile` also hides the problem when reading
on Windows (it rewrites `os.sep`), so the guard reads `orig_filename`. `release.sh package` writes
with `zipfile`, whose rewrite is correct on write.

Release notes (`releases/<VERSION>.json`) are copied into the publish output by `RTUB.csproj`, so
the one zip carries its own notes (`GET /api/releases/notes`, members only). Rules:
[release-and-rollback](release-and-rollback.md) (Release notes).

## Run from package

`WEBSITE_RUN_FROM_PACKAGE=1` is supported and proven for this .NET app on App Service Linux
(Microsoft excludes only Python and Java). `rtub` runs this way; `rtub-dev` does not (setting
intentionally absent).

A zip deploy then stores the zip as-is in `/home/data/SitePackages/` and restarts the app, which
mounts the zip read-only as `/home/site/wwwroot`: only complete deployments ever run, and there is
no extract-and-rsync step to truncate. Nothing in RTUB writes into `wwwroot` at runtime, and
SQLite lives under `/home/site/data`, so read-only costs nothing.

Kudu keeps the last five zips (`SCM_MAX_ZIP_PACKAGE_COUNT`), named `yyyyMMddHHmmss.zip`, with no
`packagename.txt`, so that folder is not a way to roll back; rollback uses the release archive.
An app running a local package cannot later be switched to a remote package URL.

## GitHub Actions to Azure authentication

OIDC federation against user-assigned managed identities. No publish profile, no client secret,
nothing long-lived in the repository.

| | DEV | PROD |
| --- | --- | --- |
| Identity | `rtub-dev-deploy` | `rtub-prod-deploy` |
| Federated credential | `github-dev-env` | `github-production-env` |
| Subject | `repo:luisfpires18/RTUB:environment:development` | `repo:luisfpires18/RTUB:environment:production` |
| Issuer / audience | `https://token.actions.githubusercontent.com` / `api://AzureADTokenExchange` | same |
| Roles | Website Contributor on the `rtub-dev` site | Website Contributor on the `rtub` site; Storage Blob Data Contributor on the archive container |

The subject is the environment form because the deploying jobs declare `environment:`; a
branch-form credential would not match. A job needs `permissions: { id-token: write }` to mint the
token; in Deploy • PROD only the deploy job has it. The build job runs npm and NuGet code and gets
read access to the repository only.

Environment variables (identifiers, not secrets): `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`,
`AZURE_SUBSCRIPTION_ID` on both; `RELEASE_STORAGE_ACCOUNT` on `production`. The `development`
environment also holds the four refresh secrets listed in the backups document.

## Azure DEV notes

- Never set `healthCheckPath` on an app that has no working code yet. Azure probes it every minute
  and restarts the instance on failure. On the Free F1 tier (15 worker stop requests per day) a
  DEV app disabled itself within the hour (`403 Site Disabled` until 00:00 UTC). DEV is on B1,
  where it is merely pointless with one instance. Return DEV to F1 only after accepting the
  Free-tier limits.
- `Staging` behaves like production (`!IsDevelopment()`): HSTS, exception page, response
  compression, 30-day static caching. It is not `Test`: migrations and seeding run at startup.
  There is no `appsettings.Staging.json`.
- DEV holds real personal data after a refresh: treat access to it as production-equivalent, and
  never give it production R2 or backup credentials.

## Database

`/home` is the Azure Files share mounted into the container: it survives restarts, redeploys and
scaling. Never point `ConnectionStrings__SqliteConnection` outside `/home`; anything else is on the
container's ephemeral layer.

`Program.cs` creates the database directory if needed, takes a pre-migration snapshot when an
existing database has pending migrations, applies them, then seeds. Rules and restore procedure:
[release-and-rollback](release-and-rollback.md) (Database).

### Seeding

`SeedData.InitializeAsync` seeds only when the database has no users at all.
`SeedData:SeedFullDataset` decides what:

| Value | Result | Also requires |
| --- | --- | --- |
| unset / `false` | Owner account only (the production path) | `AdminUser:Password` |
| `true` | Owner plus the full development member dataset (DEV) | `AdminUser:Password` and `SeedData:MemberPassword` |

Only `true`/`false` (any case) are accepted; `1`, `yes` or an empty string throw and the app
refuses to start, so a portal typo cannot silently pick a seed.

## App Service settings

No deploy applies App Service settings. Every setting with its environment, secrecy, source and
rationale (R2 required, run-from-package, VAPID, email, release-notes flag, dead `IDrive__*`), plus
the redacted audit scripts: [configuration-runbook](configuration-runbook.md).

## Deployment smoke test

`scripts/smoke-azure.sh` (read-only), run by `.github/actions/deploy-and-verify` after every
deploy and rollback with the build it expects:

1. Waits until `/api/version` reports the expected version and commit (up to 10 minutes). Until
   the restart happens, the old instance keeps answering `/health` with 200. A release built
   before `/api/version` existed is recognised by its 404 instead.
2. `/health`, `/login`, `/manifest.webmanifest`, `/service-worker.js` all 200.
3. HTML carries `Content-Security-Policy`; `/service-worker.js` carries none (a CSP served with a
   worker script governs the worker's own fetches and breaks its cross-origin caching).
4. Still up five seconds later, still the same build.

By hand: `SKIP_AZ=1 APP=rtub EXPECT_VERSION=2.0.0 EXPECT_COMMIT=<sha> ./scripts/smoke-azure.sh`.
Without `SKIP_AZ` it also reads site state and `healthCheckPath` with `az` (reads only); if the
site is stopped it prints the start command and exits.

## `/api/version` and `/health`

- `GET /api/version` returns `{"version":"2.0.0","commit":"<40-hex>"}`. Anonymous,
  `Cache-Control: no-store`, JSON (so no CSP), under `/api/` so the service worker never caches
  it. Version and commit only; the repository is public, so the commit reveals nothing new.
- `GET /health` is `MapHealthChecks` with one `AddDbContextCheck`. Anonymous, read-only,
  network-only for the service worker (`offline.js` uses it as its reachability probe). Do not
  grow it into a health subsystem or put it behind authorization.
