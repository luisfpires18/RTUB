# CI/CD and Azure environments

Authoritative description of how RTUB is built, tested and deployed. Setting **names** are
documented here; **values are never** — they live in Azure App Service configuration and GitHub
environment variables/secrets.

## Branch and deployment model

```
work branch  --PR-->  dev  --CI-->  Azure DEV (rtub-dev)  -->  manual testing
                       |
                       +--manual merge-->  master  --CI-->  Azure PROD (rtub)
```

`dev` deploys automatically once CI is green. `master` is production and is only ever reached by a
deliberate merge. There is no approval gate on DEV: it is meant to be cheap to redeploy.

Feature and chore branches are never deployed. They only get build + test, via the pull request.

## Workflow

Everything lives in a single file, `.github/workflows/ci.yml`, with two jobs.

| Job | Triggers | Does |
| --- | --- | --- |
| `build-and-test` | PR to `dev`/`master`/`main`, push to `dev`/`master`/`main` | restore, Release build, all five test suites, coverage + TRX artifacts. On a **push to master only**, also publishes and deploys to production. |
| `deploy-dev` | push to `dev`, `needs: build-and-test` | publish, Azure OIDC login, deploy to `rtub-dev`, smoke test. |

`deploy-dev` re-runs `dotnet publish` instead of consuming an artifact from `build-and-test`. That
is deliberate: the production publish/deploy steps inside `build-and-test` are guarded by
`github.ref == 'refs/heads/master'` and were left byte-identical, so nothing in this pipeline can
regress the production path. The cost is roughly two extra minutes per dev push.

`deploy-dev` carries `concurrency: { group: deploy-dev, cancel-in-progress: true }` so two quick
pushes to `dev` cannot race each other into the same App Service.

## Test execution

```
dotnet test --no-build --configuration Release \
  --results-directory ./coverage \
  --report-xunit-trx \
  --coverage --coverage-output-format cobertura
```

This **does** discover and run all five suites. The repo migrated to xUnit v3 and the Microsoft
Testing Platform in unit 011, and `global.json` carries the switch that makes `dotnet test` drive
MTP rather than VSTest:

```json
{ "test": { "runner": "Microsoft.Testing.Platform" } }
```

Without that entry `dotnet test` falls back to the VSTest host, finds no VSTest adapter in an
xUnit v3 project and reports zero tests. With it, the command above is correct as written — no
`dotnet run --project`, no invoking the built test executables by hand.

Report filenames are deliberately left unset. All five projects write into one results directory
and MTP assigns unique names; a fixed name would have them overwrite each other.

Current baseline: **4713 total, 0 failed, 4653 succeeded, 60 skipped.**

## Node

Node is **required** for publish, not optional. `src/RTUB.Web/RTUB.csproj` has:

```xml
<Target Name="BuildPixiTS" BeforeTargets="BeforePublish" Condition="Exists('package.json')">
  <Exec Command="npm ci --ignore-scripts" ... />
  <Exec Command="npm run build:pixi" ... />
</Target>
```

so `dotnet publish` fails outright on a runner without Node. `dotnet build` and `dotnet test` do
not need it.

**Node 22** is the CI version. Node 20 reached end of life in April 2026. 22 is the repo's local
toolchain version and satisfies `vite ^6`, `cross-env ^10` and `typescript ^5.7` as they are
pinned today — no frontend dependency was upgraded to accommodate it. Node 24 is the next step
when 22 leaves maintenance (April 2027).

## Azure DEV environment

| Thing | Value |
| --- | --- |
| Subscription | `Azure for Students` (tenant `ipbpt.onmicrosoft.com`) |
| Resource group | `rtub_group` (same as production) |
| App Service | `rtub-dev` → `https://rtub-dev.azurewebsites.net` |
| Plan | `ASP-rtub-dev` — **Free F1, Linux, Italy North** (production is on its own Basic B1 plan, `ASP-rtubgroup-848b`) |
| Runtime | `DOTNETCORE|10.0` |
| Environment | `ASPNETCORE_ENVIRONMENT=Staging` |
| HTTPS only | on |
| Run from package | `WEBSITE_RUN_FROM_PACKAGE=1` |
| Health check path | **unset — deliberately, see below** |

The DEV app runs on its **own** App Service Plan so DEV load cannot starve production of CPU or
RAM. That matters more than it sounds: production's B1 plan already sits at roughly **78% average
memory**, so a second RTUB sharing it would be a production risk, not a saving.

### Free F1 limits, and the trap one of them sets

Per plan, per day: 60 CPU-minutes, ~165 MB egress, 1 GB memory, and — the one that bites —
**15 worker stop requests** (`WPStopRequests`). Also no Always On, so the app unloads after ~20
minutes idle and the next request pays a cold start including the EF Core migration check.

Every restart counts against that allowance of 15, and **every App Service configuration write
restarts the app**. A handful of `az webapp config` calls while setting the environment up will
spend a third of a day's budget on their own.

**Do not set `healthCheckPath` on an app that has no working code deployed yet.** Azure then
probes the path every minute, gets a failure, and restarts the instance — which on Free tier
burns the stop-request allowance within the hour and leaves the site `403 Site Disabled` until
00:00 UTC. That is exactly how unit 027's first attempt at bringing `rtub-dev` up died: the health
check was armed seven minutes before the first deployment, the deployment then failed, and the
restart loop ran for ninety minutes. `WPStopRequests` reached **36 against a limit of 15** while
`CpuTime` and `BytesSent` were both still at zero.

So the health check path is **left unset on Free tier**. Azure's health-check feature needs Basic
or higher to do anything useful anyway — with one Free instance there is nothing to fail over to.
`/health` is still the right endpoint for the CI smoke test, which polls it directly. Set
`healthCheckPath` only if DEV is later moved to a Basic plan, and only after a deploy has
succeeded.

### Native assets: why DEV publishes RID-specific

RTUB has two native dependencies that must be present as **linux-x64** binaries, and one of them
is loaded before anything else in the app:

| Library | Package | Loaded at |
| --- | --- | --- |
| `libQuestPdfSkia.so` | QuestPDF | `Program.cs:48`, `QuestPDF.Settings.License = …` |
| `libe_sqlite3.so` | SQLitePCLRaw | first `DbContext` use |

QuestPDF's is set before a single service is registered, so if it is missing the container does
not start degraded — it aborts with `DllNotFoundException: Unable to load shared library
'QuestPdfSkia'` and exits **134**. That is not a QuestPDF bug and not a missing-runtime-asset bug
in the package: a plain `dotnet publish -c Release` produces the file correctly.

The DEV publish is RID-specific anyway:

```bash
dotnet publish src/RTUB.Web/RTUB.csproj -c Release -r linux-x64 --self-contained false
```

| | `-c Release` (portable) | `-r linux-x64 --self-contained false` |
| --- | --- | --- |
| Size | 330.5 MB / 1291 files | **266.6 MB** / 1263 files |
| `runtimes/` | 22 RIDs, 71.8 MB | **absent** |
| `libQuestPdfSkia.so` | `runtimes/linux-x64/native/` | **publish root** |
| `libe_sqlite3.so` | `runtimes/linux-x64/native/` | **publish root** |
| deps.json target | `.NETCoreApp,Version=v10.0` | `.NETCoreApp,Version=v10.0/linux-x64` |
| deps.json section | `runtimeTargets`, keyed by RID | `native` |

Both produce the byte-identical binary — same ELF x86-64 BuildID. Only the location differs.

What the RID-specific publish buys is 64 MB less payload and native libraries sitting in the
publish root, the directory the host always probes, instead of a deep `runtimes/` subtree that a
partial deployment can truncate. `-r linux-x64` is scoped to that one workflow step. There is no
`RuntimeIdentifier` property in `Directory.Build.props` or any `.csproj`, so Windows development
and all five test projects are unaffected.

QuestPDF 2024.10.3 ships no `qpdf`/`libqpdf` native library — `QuestPdfSkia` is the only one.

### Deploy guards

`deploy-dev` will not call `azure/webapps-deploy` until it has verified the publish output. Two
steps, both relative to the workspace, nothing absolute.

**Native libraries.** For each of `libQuestPdfSkia.so` and `libe_sqlite3.so`: exists, non-empty,
and `file` reports `ELF 64-bit … x86-64`. Accepts either layout — publish root or
`runtimes/linux-x64/native/` — so it stays correct if the publish command is ever changed back to
RID-less. Verified against four fixtures: RID-specific tree (pass), portable tree (pass), missing
library (fail), zero-byte library (fail).

**Static assets.** `publish/wwwroot` exists, `manifest.webmanifest`, `service-worker.js` and
`offline.html` are present and non-empty, and the tree holds at least 500 files. The floor is
there because a *partial* copy is the failure mode that actually happened, and three surviving
files would otherwise pass. Verified against four fixtures: full tree (pass, 1204 files), no
`wwwroot` (fail), three-file tree (fail on the count), missing `service-worker.js` (fail).

Neither guard can catch a deployment that is correct on the runner and wrong on the App Service —
that is what the post-deploy smoke test is for.

### Packaging: never use `Compress-Archive`

**Use `./scripts/package-azure-dev.sh`.** It packages and then refuses to emit an archive Azure
Linux cannot unpack.

PowerShell's `Compress-Archive` writes entry names with **backslash** separators on Windows. The
ZIP specification (APPNOTE 4.4.17.1) requires forward slashes. Linux unpacks such an archive into
files whose names literally contain `\`, so `wwwroot/manifest.webmanifest` arrives as one flat
file called `wwwroot\manifest.webmanifest`. Two failures follow, and they look unrelated:

- **Ordinary deployment fails outright.** Backslash is not legal on the SMB-backed `/home` share,
  so Kudu's rsync rejects every such entry and the deploy returns HTTP 400:

  ```
  rsync: [generator] recv_generator: failed to stat
  "/home/site/wwwroot/LatoFont\Lato-Black.ttf": Invalid argument (22)
  ```

- **With run-from-package it fails silently instead.** The ~39 root-level entries have no
  separator, so the app starts, migrates, seeds and answers `/health` with 200 — but
  `/home/site/wwwroot/wwwroot` never exists, the log says `The WebRootPath was not found`, and
  **every static asset 404s**.

The trap is that this is nearly invisible to verification. Python's `zipfile` does
`filename.replace(os.sep, "/")` when **reading**, so on Windows any `zipfile`-based check converts
the bad names on the way in and reports "conformant" every time. Verify by parsing the central
directory bytes — which is what the script does.

CI is unaffected: `azure/webapps-deploy` packages `./publish` on `ubuntu-latest`, where the
separator is already `/`.

### Do not use run-from-package on Linux either

Separately from the above, **`WEBSITE_RUN_FROM_PACKAGE=1` must not be set on this app.**
Microsoft's documentation is explicit: *"The run from package feature is currently Windows only
and is not yet supported in App Service for Linux."* `rtub-dev` and `rtub` are both Linux
(`DOTNETCORE|10.0`); on Linux the setting only has meaning as a blob **URL**.

Unit 027 set it to `1` to dodge a Kudu rsync failure. That was treating the symptom — the rsync
was failing on the backslash names above — and it converted a loud failure into a quiet one. The
original rsync trouble also no longer applies: the RID-specific publish is ~267 MB with no
`runtimes/` subtree, versus the ~334 MB portable payload.

**Recovering an app that has been in this state** also means clearing the stale share underneath
the old mount: `/home/site/wwwroot` there still held an old *Windows* publish (`RTUB.exe`,
`web.config`, `hostingstart.html`, no subdirectories at all).

### Environment semantics

`Staging` is not `Development`. `Program.cs` branches on `IsDevelopment()`, so under `Staging` the
app behaves like production: HSTS on, exception handler page, response compression on, 30-day
static-file caching, no HTTPS redirection middleware (TLS terminates at the Azure front end).
`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` is what makes `Request.Scheme` read `https` behind
that front end — the app never calls `UseForwardedHeaders()` itself.

`Staging` is also **not** `Test`: migrations and seeding *do* run on startup. There is no
`appsettings.Staging.json`, so `appsettings.json` defaults apply — which is why
`DatabaseBackup:Enabled` defaults to `false` and DEV never touches the production backup bucket.

## Database

DEV has its own SQLite file on durable storage, on its own App Service, on its own plan. Nothing
is shared with production.

| | Production | DEV |
| --- | --- | --- |
| `ConnectionStrings__SqliteConnection` | `Data Source=/home/site/data/app.db` | `Data Source=/home/site/data/rtub-dev.db` |
| `DatabaseBackup__Enabled` | `true` | `false` |

`/home` is the Azure Files share mounted into the container. It survives restarts, redeploys and
scale operations. Never point the connection string at a path outside `/home` — anything else is
on the container's ephemeral layer and is lost on the next restart.

`Program.cs` creates the directory if it does not exist, then applies any pending EF Core
migrations before seeding.

### Seeding

`SeedData.InitializeAsync` only seeds when the database has **no users at all**. Which seed runs
is decided by `SeedData:SeedFullDataset`:

| `SeedData:SeedFullDataset` | Result | Also requires |
| --- | --- | --- |
| unset / `false` | Owner account only — the production path | `AdminUser:Password` |
| `true` | Owner account **plus** the full development member dataset | `AdminUser:Password` **and** `SeedData:MemberPassword` |

Before unit 027 this was a hardcoded `var isEmptyDb = true;`. It became configuration because
Azure DEV wants the full dataset and production must not have it. Hardcoding `false` instead would
have armed the bulk member seed on any fresh database anywhere, including a future production
restore. Unset, behaviour is identical to the old hardcoded value.

DEV sets `SeedData__SeedFullDataset=true`. Production sets nothing and is unaffected.

**Only `true` and `false` are accepted** (case-insensitive). Absent counts as `false`. Anything
else — `1`, `0`, `yes`, or an empty string — throws out of `GetValue<bool>` and the app refuses to
start. That is deliberate: a typo in the portal stops the host instead of silently choosing a
seed. Verified against `Microsoft.Extensions.Configuration.Binder` for both the in-memory and the
environment-variable provider:

| Value | `isEmptyDb` | Result |
| --- | --- | --- |
| absent | `true` | owner-only — **production** |
| `false` / `False` | `true` | owner-only |
| `true` / `True` / `TRUE` | `false` | full dataset — **DEV** |
| `""`, `1`, `0`, `yes` | — | `InvalidOperationException`, app does not start |

## GitHub Actions → Azure authentication

DEV uses **OIDC federation against a user-assigned managed identity**. No publish profile, no
client secret, nothing long-lived in the repository.

| Thing | Value |
| --- | --- |
| Identity | `rtub-dev-deploy` (user-assigned managed identity, `rtub_group`) |
| Federated credential | `github-dev-env` |
| Issuer | `https://token.actions.githubusercontent.com` |
| Subject | `repo:luisfpires18/RTUB:environment:development` |
| Audience | `api://AzureADTokenExchange` |
| Role | `Website Contributor`, scoped to the `rtub-dev` site only |

A **user-assigned managed identity** was chosen over an Entra app registration on purpose: it is
an ordinary Azure resource, so it is created, scoped and deleted with the rest of `rtub_group` and
needs no directory-level administration, and it can never grow a client secret.

The subject is the *environment* form, not the branch form, because the `deploy-dev` job declares
`environment: development` — GitHub then issues the token with
`repo:<owner>/<repo>:environment:<name>` as the subject. A branch-form credential would not match.

The identity's `Website Contributor` assignment is scoped to `rtub-dev` alone. It cannot touch the
production app, the production plan, or anything else in the subscription.

The job needs `permissions: { id-token: write, contents: read }` for GitHub to mint the token.

### Non-secret identifiers

Stored as **variables** (not secrets) on the `development` GitHub environment. Client, tenant and
subscription IDs are identifiers, not credentials; the federated-credential subject is what
actually gates access.

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

## App Service setting names

Names only. Values live in Azure.

### Required for the DEV app to start

| Name | Note |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Staging` |
| `ASPNETCORE_URLS` | must match `WEBSITES_PORT` |
| `WEBSITES_PORT` | the port the container listens on |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` — correct scheme behind the Azure front end |
| `ConnectionStrings__SqliteConnection` | must be under `/home` |
| `AdminUser__Password` | **startup-fatal on a fresh database.** `SeedMembersAsync` throws if unset. Not needed once the database has users. |

### Required because DEV opts into the full seed

| Name | Note |
| --- | --- |
| `SeedData__SeedFullDataset` | `true` |
| `SeedData__MemberPassword` | **startup-fatal on a fresh database** when the flag above is `true`. No default exists. |

### Required — Cloudflare R2

**R2 is not optional.** An earlier revision of this document claimed DEV could omit it because
the `IAmazonS3` client is a lazy singleton that only throws when first resolved. The first half is
true; the conclusion was wrong. It is resolved on the **home page**:

```
Index.razor  @page "/"
  └─ @inject ISlideshowService
       └─ SlideshowService(… IImageStorageService …)
            └─ CloudflareImageStorageService(… IAmazonS3 …)
                 └─ factory throws: "Cloudflare R2 credentials not configured"
```

`IAmazonS3` is constructor-injected into **11** `Cloudflare*StorageService` classes, which are in
turn constructor-injected into **13** domain services — `AlbumService`, `CommentService`,
`EventService`, `InstrumentService`, `ItemTypeConfigService`, `LogisticsCardService`,
`NaipeService`, `PostService`, `ProductService`, `SlideshowService`, `SongService`,
`TransactionService`, `UserProfileService`. Between them those cover most routable pages. The app
starts fine and `/health` answers 200 — then `/` returns 500.

Making it optional was considered and rejected: it would require every one of those services to
tolerate a no-op storage client, which changes application semantics broadly and converts real
storage failures into silence. These are required settings:

| Name | Note |
| --- | --- |
| `Cloudflare__R2__AccountId` | |
| `Cloudflare__R2__AccessKeyId` | |
| `Cloudflare__R2__SecretAccessKey` | |
| `Cloudflare__R2__Bucket` | **use a separate DEV bucket** |
| `Cloudflare__R2__PublicUrl` | also feeds the CSP `img-src`/`media-src` sources |

**Never give DEV production R2 credentials.** Create a separate DEV bucket and issue an R2 API
token scoped to that bucket alone. DEV then cannot read, overwrite or delete production media even
by accident, and the token can be rolled without touching production.

### Optional — absence degrades a feature, it does not stop startup

| Group | Names | Missing in DEV means |
| --- | --- | --- |
| Web Push / VAPID | `WebPush__VapidSubject`, `WebPush__VapidPublicKey`, `WebPush__VapidPrivateKey` | push disabled via `WebPushOptions.IsConfigured()`; in-app inbox messages still work |
| Email | `EmailSettings__SmtpServer`, `EmailSettings__SmtpPort`, `EmailSettings__SmtpUsername`, `EmailSettings__SmtpPassword`, `EmailSettings__EnableSsl`, `EmailSettings__SenderEmail`, `EmailSettings__SenderName`, `EmailSettings__RecipientEmail` | outbound mail fails on send |
| Database backup | `DatabaseBackup__Enabled`, `DatabaseBackup__AccessKeyId`, `DatabaseBackup__SecretAccessKey`, `DatabaseBackup__Bucket`, `DatabaseBackup__AccountId` | DEV sets `Enabled=false`; nothing else is read |
| Application Insights | `APPLICATIONINSIGHTS_CONNECTION_STRING` and the `ApplicationInsightsAgent_*` / `XDT_*` family | no telemetry |

**Never give DEV production backup credentials either.**

### `RemoteNavigationManager already initialized` is a symptom, not a bug

It appears only in the frame `ExceptionHandlerMiddleware[3] — "An exception was thrown attempting
to execute the error handler"`, immediately after an unhandled exception, and always with
`EndpointHtmlRenderer.InitializeStandardComponentServicesAsync` at the top of its stack.

`UseExceptionHandler("/Error")` re-executes the pipeline to render `/Error` as a Razor component
on the *same* `HttpContext`, whose `RemoteNavigationManager` the first render attempt already
initialized. No first exception, no second render, no message. Fix the underlying exception and it
disappears; there is nothing independent to fix here.

## Deployment smoke test

The same checks exist in two places: the `Smoke test` step of `deploy-dev`, which runs on every
dev deploy, and `scripts/smoke-azure-dev.sh`, which you run by hand. The script is read-only — it
never writes App Service configuration and never starts the app; if the site is stopped it prints
the start command and exits, because on Free tier every restart spends part of a 15/day
allowance.

`deploy-dev` asserts the deployment-visible contracts from units 025 and 026 after every deploy,
so a bad deploy fails the run instead of sitting there quietly broken:

1. `/health` returns 200 anonymously (polled, up to five minutes for a Free-tier cold start).
2. `/login` and `/manifest.webmanifest` are reachable.
3. HTML documents carry a `Content-Security-Policy` header.
4. `/service-worker.js` carries **no** `Content-Security-Policy` header. A CSP served with a
   worker script governs that worker's own fetches and would break the cross-origin caching the
   service worker does.

## `/health`

`app.MapHealthChecks("/health")` with a single `AddDbContextCheck<ApplicationDbContext>("database")`.
It is anonymous, read-only and does not mutate state, which is exactly what Azure's health check
and the CI smoke test need. `service-worker.js` lists `/health` in `NEVER_CACHE_PREFIXES`, so it
is network-only from the PWA's perspective and `offline.js` can rely on it. Do not expand it into
a health subsystem and do not put it behind authorization.

It is **suitable** for App Service's `healthCheckPath`, but see the Free-tier warning above before
wiring it there: on Free tier a failing probe restarts the instance, and the restart allowance is
small enough that an app which is not yet deployed will disable itself.

## Production

Production deployment was **not changed** by unit 027 and still works exactly as before:

- `master` push → `build-and-test` → `dotnet publish` → `azure/webapps-deploy@v3` to app `rtub`
- authenticated with the `AZURE_WEBAPP_PUBLISH_PROFILE` repository secret
- which requires **Basic Auth Publishing Credentials = On** on the production App Service

Production still pins `actions/checkout@v4`, `actions/setup-dotnet@v4`, `actions/setup-node@v4`
(Node 20) and `actions/upload-artifact@v4`. Those are stale, and modernizing them along with
migrating production to OIDC is the next deployment unit — deliberately not folded into this one,
so a DEV pipeline change can never take production down.

### Production carries the same native-asset packaging risk

It works today, but nothing in the production path defends it. Recorded for unit 028, **not
changed in 027**:

- Production publishes **portable** (`-c Release`, no RID), so its `libQuestPdfSkia.so` and
  `libe_sqlite3.so` live under `runtimes/linux-x64/native/` inside a 72 MB, 22-RID subtree, and
  the payload is ~330 MB rather than ~267 MB.
- Production has **no `WEBSITE_RUN_FROM_PACKAGE`**, so it deploys through Kudu's extract-and-rsync
  path — the same transport that truncated DEV's `wwwroot`.
- Production has **no deploy guard**. A partial transfer that drops `runtimes/` would produce the
  identical `DllNotFoundException` at `Program.cs:48` and exit 134, on the production site.

Production App Service is Linux (`DOTNETCORE|10.0`), so `-r linux-x64 --self-contained false`
applies to it unchanged. Migrating it is a production deployment change and belongs in 028.
