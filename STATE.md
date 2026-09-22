# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-22_

## Phase
Modernization unit **029 (sanitized production snapshot -> Azure DEV).** Core + storage ownership
merged to `dev` at `7f187940`. **Follow-up fix (DEV database path resolved from rtub-dev's own
setting) is COMPLETE, uncommitted, awaiting owner review.** 029 is an infrastructure unit -
production deployment is deliberately untouched, and production data is read but never written.

Durable detail lives in **`docs/cloudflare-r2-and-database-backups.md`** (029) and
**`docs/ci-cd-and-azure-environments.md`** (027), not here.

## Branch
The follow-up sits **uncommitted on `dev`** itself - `dev` was checked out when it started, and no
branch was created because the name/number is the owner's call (030 is reserved). Per the git
model, `git switch -c <work-branch>` before committing carries the working tree across.
`chore/001`-`chore/011`, `fix/012`-`fix/014`, `chore/015`, `fix/016`-`fix/018`, `perf/019`,
`fix/020`-`fix/028`, `chore/029` still present; delete when convenient.

## Unit 029 - sanitized production snapshot -> Azure DEV
Manual, `workflow_dispatch`-only refresh of `rtub-dev` from `rtub-db/database/current.db`.
**Contract, secret names, rollback behaviour and the Kudu mechanism are in
`docs/cloudflare-r2-and-database-backups.md`, "Refreshing Azure DEV from a sanitized production
snapshot".** What matters here:

- **Production is never mutated.** One `GET` against `rtub-db`, nothing else. No write to the
  bucket, no connection to the live production SQLite file, no production App Service involvement.
  The R2 credential is specified as a **dedicated read-only token scoped to `rtub-db`** - not the
  write-capable one the backup service runs with.
- **The snapshot is immutable input.** Opened `SqliteOpenMode.ReadOnly`, copied, and only the copy
  is written to. Its SHA-256 and length are compared before and after; a change fails the run.
- **`DatabaseSanitizer` is raw ADO.NET, not EF Core.** Deliberate: a production snapshot is
  normally a few migrations behind `dev`, so an EF/`UserManager` pass would throw `no such column`
  before sanitizing anything. Only columns present since the initial migration are touched, and
  the app's own startup migration brings the schema forward on first boot.
- **No hardcoded hash.** `PasswordHasher<ApplicationUser>` with default options, called once per
  user, so every row gets its own salt. `SecurityStamp` rotated per row.
- **Fails closed.** A missing DEV password is rejected before the destination is opened; any
  failed post-check deletes the output. Failure messages name a bad row by `AspNetUsers.Id` only -
  no password, hash, email or row content reaches a log.
- **`DevelopmentDataReset` was NOT removed.** It stays for synthetic/fresh DEV databases and for
  local use. After a 029 refresh, `DevelopmentDataReset__Enabled` should stay **off** on
  `rtub-dev` - the database arrives already sanitized, and leaving it on would re-hash every user
  on every cold start. The workflow reads and writes no App Service settings, so that is a
  one-time owner action.
- **DEV still holds real personal data after sanitization.** Only credentials and push endpoints
  are stripped; names, finances and history are kept on purpose. Treat `rtub-dev` access as
  production-equivalent.

### 029 validation
- `dotnet build --configuration Release` - clean, 0 warnings, 0 errors.
- `dotnet test` (all five suites, the CI command) - **4743 total, 0 failed, 4683 succeeded,
  60 skipped.** Delta is exactly **+21**, the new `DatabaseSanitizerTests`.
- **Proven red before green.** Mutating `Rewrite` to skip password hashing turns **9 of 21** red,
  including the fail-closed post-check; the file was restored and re-run green.
- End-to-end CLI run against a synthetic database: source SHA-256 byte-identical afterwards,
  `PushSubscriptions` 0, emails `{UserName}@rtub.pt`, `NormalizedEmail` uppercase, per-user
  distinct hashes, `journal_mode=delete`, no `-wal`/`-shm` left beside either file. No production
  data was used anywhere.
- Workflow YAML parses; trigger set asserted to be `workflow_dispatch` alone; every `run:` body
  extracted and checked with `bash -n`.
- `git diff --check` clean; secret scan clean; no credential, account id, endpoint or database
  file added.

### 029 follow-up - storage ownership (DEV must not mutate PROD objects)
A sanitized snapshot preserves production media URLs byte-for-byte, so a DEV database is full of
absolute production R2 URLs. **Invariant now enforced in code: an environment may only delete or
overwrite an object in its own bucket.** Reading another environment's objects stays allowed.
Detail in `docs/cloudflare-r2-and-database-backups.md`, "Storage ownership". What matters here:

- **Two independent mechanisms**, neither relying on the credential being incapable:
  1. **Bucket guard** in `AddStorageServices`. `appsettings.json` commits
     `Cloudflare:R2:Bucket = "rtub"` - **the production bucket** - so a non-production environment
     that forgets to override it inherits production's bucket and every upload and delete lands
     there. No per-object check can catch that. Startup is refused when the environment is not
     Production, the bucket equals the new committed `Cloudflare:R2:ProductionBucket`, **and** an
     R2 credential is configured. The credential condition is a capability check, not a name-based
     exemption for `Test`: it is what lets the integration-test host boot on the committed
     settings, and anything calling itself `Test` while holding real credentials is still refused.
  2. **Per-object origin check**. `StorageOriginResolver` classifies a stored URL as
     `CurrentEnvironment` / `ProductionReference` / `External` / `Unknown`; only the first is
     deletable, and `Unknown` (anything that is not an absolute http/https URL) fails closed.
- **The bug it closes:** `ExtractObjectKeyFromUrl` reads a URL's *path* and ignores its *host*, so
  on a cloned database every delete path handed back a production key and issued it against
  whatever bucket the environment pointed at. `CloudflareGalleryMediaStorageService` was worse -
  `mediaUrl.Replace($"{_publicUrl}/", "")` silently produced the **whole absolute URL** as the key
  for any foreign origin. All 8 delete-by-URL paths now go through `ResolveDeletableKey`.
- **No constructor and no DI registration of the 11 media services changed.** The resolver is
  built inside `BaseCloudflareStorageService` from the `IConfiguration` it already receives.
- **Production is unchanged.** Its URLs are under its own public origin, so they resolve to
  `CurrentEnvironment` and behave exactly as before; it configures no reference origin at all.
- **Public media:** CSP only. `Cloudflare:R2:ReferencePublicUrl` adds **one exact origin** to
  `img-src`/`media-src` only - never `script-src`/`connect-src`/`frame-src` - through the same
  `NormalizeOrigin` that drops wildcards and injected text. No credentials added; inherited URLs
  are never rewritten to DEV URLs.
- **Private files:** the DEV-misses-the-object problem is real for documents (`docs/{Env}/…`) and
  for album audio and lyric PDFs, whose keys carry **no environment segment** at all.
  `IReferenceStorageService` is the read-only path: no upload, delete, copy or move member, and
  `ReferenceStorageService` deliberately does **not** derive from `BaseStorageService` so it
  cannot inherit `PutObjectAsync`/`DeleteObjectAsync`/`DeleteObjectsBatchAsync`/`CopyObjectAsync`.
  Two gates: never in Production, and all four `Cloudflare:R2:Reference:*` keys required.
  Unconfigured it builds no S3 client. Receipts needed nothing - they are public URLs.
- **Owner Storage Maintenance page NOT built.** Its required contract is documented, including
  that inherited `ProductionReference` rows must be excluded from DEV orphan calculations.
- **`rtub-dev` must be given its own `Cloudflare__R2__Bucket`** plus the
  `Cloudflare__R2__ReferencePublicUrl` and `Cloudflare__R2__Reference__*` names. Owner action; no
  Cloudflare or Azure resource was created or changed.

### 029 follow-up validation
- `dotnet build --configuration Release` - clean, 0 warnings, 0 errors.
- `dotnet test` (all five suites) - **4786 total, 0 failed, 4726 succeeded, 60 skipped.**
  Delta **+43** on 029's 4743: 20 `StorageOwnershipTests`, 12 CSP, 10 `ProductionBucketGuardTests`,
  1 sanitizer URL-preservation test.
- **Proven red before green.** Relaxing `ResolveDeletableKey` to let non-`Unknown` origins through
  turns **6 of 20** ownership tests red; restored and re-run green. (A first attempt used
  `if (true)`, which does not compile under `TreatWarningsAsErrors` - `--no-build` then re-ran the
  old binary and reported a meaningless pass. The mutation above compiles.)
- The bucket guard was **caught by the suite**: its first form failed 243 integration tests,
  because the test host runs as `Test` on the committed `appsettings.json`. Fixed by gating on
  credentials rather than exempting an environment name.

### 029 first live validation (manual, by the owner, 2026-09-22) - one real bug found
Everything held except the target file: sanitized snapshot of **108 users, 49 PushSubscriptions
removed**, source unchanged; Kudu GET/PUT/DELETE worked; rollback copy valid (91 tables / 83
users); the sanitized DB replaced the real `/home/site/data/rtub-dev-v3.db`; WAL/SHM removed;
restart, `/health` 200, sanitized DEV login works; inherited production media renders read-only;
new DEV media lands in the DEV bucket and survives a refresh; `SKIP_AZ=1` smoke = PASS.

**The bug: the workflow hardcoded `DEV_DB_PATH: site/data/rtub-dev.db`**, but rtub-dev's
`ConnectionStrings__SqliteConnection` is `Data Source=/home/site/data/rtub-dev-v3.db`. The
workflow's logic therefore targeted an empty 4 KB SQLite file instead of the real 3.2 MB DB.
**Root cause:** the literal was taken from 027's database table in this file, which still said
`rtub-dev.db` - while this same file's 027 redeploy checklist had already told the owner to move
DEV to a fresh `rtub-dev-v3.db`. A setting that can change was copied into code as a constant.

**Fix:** the workflow no longer names a file. A new step, after the OIDC login and **before** the
stop, reads `ConnectionStrings__SqliteConnection` (one value, JMESPath-selected server-side,
masked, never printed), refuses if a same-named Connection strings blade entry would shadow it,
validates it with `scripts/resolve-dev-db-path.sh` (fails closed unless exactly
`Data Source=/home/site/data/<[A-Za-z0-9][A-Za-z0-9._-]*>.db`), and exports the Kudu path once via
`$GITHUB_ENV`. No `env:` default exists anywhere; replace and rollback refuse an unset/empty path
before fetching a token. The local runner file was renamed `rtub-dev.db` -> `sanitized.db` so
nothing in the workflow looks like the remote name. Rules in the backups doc.

Validation: resolver `--self-test` 38/38, and a permissive allow-list mutant turns 6 red including
the `$GITHUB_ENV` newline injection. The real step bodies, extracted from the YAML and run against
recording `az`/`curl` shims: the live value exports exactly `DEV_DB_PATH=site/data/rtub-dev-v3.db`;
missing / shadowed / outside-dir / traversal / injection / extra-keyword / `az`-failure all exit 1
with `$GITHUB_ENV` untouched; replace issues GET -> PUT `.rollback` -> PUT -> DELETE `-wal`/`-shm`
all on `rtub-dev-v3.db`; unset/empty path makes zero `az` or Kudu calls. New
`RefreshDevDatabaseWorkflowTests` (8): re-adding the stale job-level default turns it red.

**Harness incident, disclosed:** the first dry-run's shim directory was a `C:/...` path, whose
colon split the `PATH` entry, so the REAL `az` ran with the owner's local login - 8 x
`az webapp config appsettings list` (JMESPath-selected single value) and 8 x
`az webapp config connection-string list` (count only) against `rtub-dev`. **Read-only; no write
occurred;** the replace/rollback bodies were not run. They appear in the Activity Log as
`config/list` reads. The harness was rebuilt with an MSYS-form path, an empty `AZURE_CONFIG_DIR`
and a pre-flight that aborts unless both shims resolve first.

### 029 open item (needs a real workflow_dispatch run, cannot be verified from the repo)
The manual validation proved the mechanism, not the workflow's own identity or secrets. The first
real run proves: (1) the `rtub-dev-deploy` OIDC identity can call Kudu VFS
(`Microsoft.Web/sites/publish/Action`) and read settings (`Microsoft.Web/sites/config/list/Action`,
now needed by the resolve step) - `Website Contributor` grants both through `Microsoft.Web/sites/*`;
(2) the four GitHub secrets are set. **On a 401/403, add a role assignment carrying the single
missing action on the `rtub-dev` site - never broaden to `Contributor`.** No Azure resource was
created or changed by this unit.

## Owner decision (2026-09-21)
**Password-policy hardening is SKIPPED**, by instruction. Identity's password requirements were
not read for change and not touched by 021, 025 or 027. It remains available as a future unit.

## Owner decisions (unit 027)
1. **Create the Azure DEV resources now** - done, see below.
2. **Azure DEV gets its own Free F1 plan**, not a share of production's Basic B1. Production CPU
   and RAM are therefore never contended by DEV.
3. **Azure DEV seeds the full member dataset**, not owner-only bootstrap.
4. **Create the GitHub `development` environment and its non-secret OIDC variables** - done.

On (3): the owner chose the full dataset, which previously meant editing `var isEmptyDb = true;`
to `false` in `SeedData.cs`. That literal edit was **not** made. Hardcoding `false` would arm the
bulk member seed on *any* fresh database, production restores included. The switch is now
`SeedData:SeedFullDataset` configuration instead, defaulting to the old `true` value of
`isEmptyDb` when unset - production behaviour is byte-identical, and only `rtub-dev` opts in.

## Unit 027 record (merged at `816d222c`) - kept for the Azure DEV findings still open below
**Unit 027 - `dev` now has CI and an automatic deployment to a real, separate Azure DEV App
Service. Production's path is unchanged.**

### The premise that turned out to be stale
The brief said `dotnet test` reports **zero tests** in this repo. **It does not.** Verified by
running the existing CI command verbatim on this machine:

    dotnet test --no-build --configuration Release --results-directory ./coverage \
      --report-xunit-trx --coverage --coverage-output-format cobertura

    total: 4713   failed: 0   succeeded: 4653   skipped: 60

All five suites discovered and executed. Unit 011 already fixed this by putting
`{"test": {"runner": "Microsoft.Testing.Platform"}}` in `global.json`, which makes `dotnet test`
drive MTP instead of the VSTest host. **No change to the test command was needed or made.** No
`dotnet run --project`, no hand-invoked test executables, no MTP-specific workaround.

### Old CI behaviour
One workflow, `.github/workflows/ci.yml`, one job (`build-and-test`):
- triggers: PR to `main`/`master`, push to `main`/`master` - **`dev` ran no CI at all**
- .NET `10.0.x`, restore, Release build, the MTP test command above, coverage + TRX artifacts
- Node **20**, set up only on a master push
- on master push only: `dotnet publish` then `azure/webapps-deploy@v3` to app `rtub`,
  authenticated with the `AZURE_WEBAPP_PUBLISH_PROFILE` repository secret (Basic Auth)

### New CI triggers
- `build-and-test`: PR to `dev`/`master`/`main`, push to `dev`/`master`/`main`. Its production
  publish/deploy steps are left **byte-identical**, still guarded by `refs/heads/master`.
- `deploy-dev` (new job): push to `dev` only, `needs: build-and-test`, `environment: development`,
  `permissions: {id-token: write, contents: read}`, `concurrency: deploy-dev` with
  `cancel-in-progress`. Never runs on a pull request. Never touches the production app.

`deploy-dev` re-publishes rather than consuming an artifact from `build-and-test`, on purpose: it
keeps the production steps' `if:` expressions untouched, so this unit cannot regress production.
Cost is ~2 minutes per dev push.

### Test command used in GitHub Actions
Unchanged from the line quoted above. `deploy-dev` runs no tests of its own - `needs:
build-and-test` is what gates it, and that job runs all five suites.

### Node decision
**Node 22**, in the new `deploy-dev` job. Node is **required**, not decorative: `RTUB.csproj`'s
`BuildPixiTS` target runs `npm ci --ignore-scripts` + `npm run build:pixi` on `BeforePublish`, so
`dotnet publish` fails outright without it. Node 20 reached end of life in April 2026. 22 is the
repo's local toolchain version and satisfies `vite ^6`, `cross-env ^10` and `typescript ^5.7` as
pinned - **no frontend dependency was upgraded**. Node 24 is the step after 22 leaves maintenance
(April 2027). Production's `setup-node@v4` / Node 20 pin was left alone with the rest of the
production path.

### Azure DEV architecture - CREATED AND LIVE
| Thing | Value |
| --- | --- |
| Subscription | `Azure for Students`, tenant `ipbpt.onmicrosoft.com` |
| Resource group | `rtub_group` (same as production) |
| App Service | **`rtub-dev`** -> `https://rtub-dev.azurewebsites.net` |
| Plan | **`ASP-rtub-dev`** - Free **F1**, Linux, Italy North (new; production keeps `ASP-rtubgroup-848b`, Basic B1) |
| Runtime | `DOTNETCORE 10.0` |
| Environment | `ASPNETCORE_ENVIRONMENT=Staging` |
| HTTPS only | on |
| Run from package | `WEBSITE_RUN_FROM_PACKAGE=1` |
| Health check path | **unset** - see *Deployment smoke: NOT COMPLETED* below |

`Staging` is **not** `Development`: `Program.cs` branches on `IsDevelopment()`, so DEV gets HSTS,
the exception-handler page, response compression and 30-day static caching exactly like
production. `Staging` is **not** `Test` either, so migrations and seeding do run on startup.
There is no `appsettings.Staging.json` - `appsettings.json` defaults apply, which is why
`DatabaseBackup:Enabled` is `false` and DEV never reaches the production backup bucket.

Free F1 limits, per plan per day: 60 CPU-minutes, ~165 MB egress, 1 GB memory, and - the one that
matters here - **15 worker stop requests**. No Always On either, so ~20 min idle means a cold
start including the migration check.

### Deployment smoke: NOT COMPLETED - `rtub-dev` disabled itself, and it was my configuration
The app is **not** verified running. `rtub-dev` is `state: QuotaExceeded` / `usageState: Exceeded`
and serves `403 Site Disabled` until the Free-tier counters reset at **00:00 UTC**.

The counter that blew is **`WPStopRequests: 36 / 15`** - worker *stop* requests. `CpuTime` was
`0 / 3,600,000 ms` and `BytesSent` `0 / 173,015,040` - neither was touched. Total traffic sent to
the site during the whole unit was **two `curl` requests to `/health`**. No load test, nothing
resembling usage.

What actually happened, from the activity log:

| Time (UTC) | Event |
| --- | --- |
| 19:50:34 | `rtub-dev` created - empty, no code |
| 19:51:06 | app settings written -> restart |
| 19:51:36 | `httpsOnly` -> restart |
| **19:51:45** | **`healthCheckPath=/health` armed on an app with nothing deployed** |
| 19:58-19:59 | deploy #1 -> Kudu parallel rsync exit 123, failed |
| 19:59 -> 21:29 | ~90 min idle while STATE.md was being written |
| 21:29 | already `403 Site Disabled` |

Every App Service configuration write restarts the app, so ~6 of the 36 stops were the setup
writes themselves. The other ~30 were a restart loop: **the health check was armed seven minutes
before any code existed**, the first deploy then failed, and Azure probed `/health` every minute
for ninety minutes, restarting the instance each time it failed. On Free tier's allowance of 15
that is fatal within the hour.

**This is a configuration-ordering mistake, not evidence about the app or about F1.** An earlier
reading of this that blamed "the F1 memory ceiling" was wrong - it inferred memory from
`CpuTime 0` / `BytesSent 0` without checking what `WPStopRequests` counts. **F1 has not been
fairly tested**: the app has never had one clean boot attempt on it.

Remediation already applied: **`healthCheckPath` removed** from `rtub-dev`. Do not set it again
on Free tier, and never before a deploy has succeeded.

### Startup failure on B1 - native asset missing from the DEPLOYED tree
DEV was moved to B1 by the owner and produced a real container log:

    QuestPDF.Settings initialisation fails at Program.cs:48
    DllNotFoundException: Unable to load shared library 'QuestPdfSkia'
    probed: /home/site/wwwroot/runtimes/linux-x64/native/libQuestPdfSkia.so
    container exits 134

**The publish command was not at fault.** Proven locally, both publishes run on this machine:

| | `-c Release` (what CI ran) | `-r linux-x64 --self-contained false` |
| --- | --- | --- |
| total | 330.5 MB / 1291 files | **266.6 MB** / 1263 files |
| `runtimes/` | 22 RIDs, 71.8 MB | **absent** |
| `libQuestPdfSkia.so` | `runtimes/linux-x64/native/` **present, 6.5 MB** | publish **root** |
| `libe_sqlite3.so` | `runtimes/linux-x64/native/` | publish **root** |
| deps.json | `runtimeTargets` keyed by RID | `native`, target `…/linux-x64` |

Same ELF x86-64 binary in both, identical BuildID `c8d2c90f…`. The portable publish **does**
contain the linux-x64 library. The zip that was deployed contained it too - checked by reading the
zip's central directory: entry `runtimes/linux-x64/native/libQuestPdfSkia.so`, 6 839 384 bytes,
conformant forward-slash path.

So the artifact was correct and the file went missing **in transport**. Corroborating:
- the exception **probed the right path**, so deps.json RID resolution worked - the host built
  `runtimes/linux-x64/native/` into its search list and found nothing there;
- deploy #1's Kudu parallel rsync exited **123 - partial transfer**;
- deploy #2's status was never confirmed (the CLI lost the poll to an SSL error) and the site was
  already 403 before it began;
- `FileSystemStorage` read **298 MB** against a 330.5 MB publish - about 32 MB short, and
  `runtimes/` is 71.8 MB of deep, many-file subtree, exactly what an 8-thread rsync truncates.

`/home/site/wwwroot` therefore holds a **truncated tree from the failed rsync**, and QuestPDF -
initialised at `Program.cs:48`, before any service registration - is simply the first thing to
touch a file that is not there. `libe_sqlite3.so` would have failed one step later.

**QuestPDF was not upgraded.** 2024.10.3 is fine and ships no `qpdf`/`libqpdf` - `QuestPdfSkia`
is its only native library. The licence initialisation was not removed or weakened.

Also relevant: **`WEBSITE_RUN_FROM_PACKAGE=1` is set**, which mounts the zip instead of rsyncing
it and avoids the failing transport entirely. Nothing writes into `wwwroot` at runtime - every
`WebRootPath` use is a read - and SQLite is under `/home/site/data`, so read-only `wwwroot` costs
nothing.

Production's B1 plan is at roughly **78% average / 81% peak memory**, so moving `rtub-dev` onto it
to share is not a safe fallback. If F1 turns out genuinely not to fit after a fair retry, a
separate Basic B1 plan (~EUR 13/month) is the option with evidence behind it - production runs
RTUB on B1 today.

### Migration chain could not run from zero - FIXED
Once deployment worked, DEV still aborted at `Program.cs:234` with

    SQLite Error 1: 'no such column: "YearCaloiro"'

while EF rebuilt `AspNetUsers` through `ef_temp_AspNetUsers`. A completely fresh SQLite path
(`rtub-dev-v2.db`) failed identically, so it was never corrupt Azure state. **Reproduced locally
on an empty disposable database and fixed at the source.**

**Root cause: `20251026224954_AddMentorField`.** SQLite cannot add a self-referential foreign key
in place, so that migration hand-writes the table rebuild in raw SQL. Its
`CREATE TABLE "AspNetUsers_new"` column list omitted three columns the initial `Db` migration had
created — proven by diffing the two lists:

    initial Db AspNetUsers columns : 32
    AddMentorField rebuild columns : 30
    LOST: YearLeitao, YearCaloiro, YearTuno      NEW: MentorId

`DROP TABLE "AspNetUsers"` + rename then made the loss permanent and silent. Every `.Designer.cs`
snapshot from `Db` onward still declares the three, because snapshots come from the model, and
the model never stopped having them (`ApplicationUser.TempoDeTuno` reads `YearTuno`/`MonthTuno`
today).

Nothing detects the divergence until a migration asks **EF** to rebuild `AspNetUsers`, because
EF builds its `ef_temp_` table from that migration's snapshot and copies with
`INSERT … SELECT … FROM AspNetUsers`. **First failure is migration 16,
`20251105141307_RemoveIsActiveFromApplicationUser`** — a single `DropColumn("IsActive")`.
Confirmed against the half-migrated database: 15 migrations applied, last
`20251104005142_AddLastLoginDateToUser`, and `AspNetUsers` carrying `IsActive` but **no
`YearCaloiro`/`YearLeitao`/`YearTuno`**.

**Fix: carry the three columns through that rebuild** — added to the `CREATE TABLE` and to both
the `INSERT` column list and the `SELECT` list, in `Up` and in `Down`. 14 lines, one file. No
migration was regenerated, squashed or deleted, nothing was added to `Program.cs`, no
`EnsureCreated`, no suppressed errors, and `SeedData` was not touched.

**Why editing a historical migration is safe here.** `AddMentorField` is dated 2025-10-26 and is
long present in production's `__EFMigrationsHistory`, so EF will never execute it there again —
the edit is inert for every database that has already applied it. It changes behaviour only for
databases that have **not** yet applied it, which is exactly the broken case. Production also
demonstrably *has* the three columns: it is past migration 16, and migration 16 cannot succeed
against a table that lacks them. The fix makes a fresh install match what production already is.

Pre-existing and deliberately **not** touched: the same rebuild widens `FirstName`, `LastName`,
`Positions` and `Categories` from `NOT NULL` to `NULL`. Harmless to the migration chain — a
rebuild copies by column name, not nullability — and out of scope for this unit.

### Azure DEV boots, smoke is not green - three defects, all diagnosed
DEV now starts: all migrations applied, 83 users seeded, Kestrel on :8080, startup probe green,
site stays up. `/health` = 200. What is still wrong:

**1. Static files 404 - `Compress-Archive` writes BACKSLASH separators. (b)/(c), packaging.**
Symptom: `The WebRootPath was not found: /home/site/wwwroot/wwwroot`, and
`/manifest.webmanifest` + `/service-worker.js` both 404. Then, once run-from-package was taken
out of the picture, the deploy itself returned **HTTP 400**.

The rsync log named it exactly:

    rsync: [generator] recv_generator: failed to stat
    "/home/site/wwwroot/LatoFont\Lato-Black.ttf": Invalid argument (22)
    "/home/site/wwwroot/wwwroot\.well-known\assetlinks.json.br": Invalid argument (22)

Backslash is not legal on the SMB-backed `/home` share, so every entry with one is rejected.
Raw central-directory parse of the deployed `rtub-dev.zip`: **1231 backslash entries, 0 forward
slash**, `'wwwroot\manifest.webmanifest'`. Reproduced minimally - `Compress-Archive` on a
three-file tree emits `wwwroot\manifest.webmanifest`; Windows `bsdtar` on the same tree emits
`./wwwroot/manifest.webmanifest`.

**I called this wrong twice, and the reason matters.** `zipfile.ZipInfo.__init__` does
`filename.replace(os.sep, "/")` **when reading**, so on Windows every `zipfile`-based check
silently converts the bad names and reports "conformant". A PowerShell
`ZipArchiveEntry.FullName` listing had shown the backslashes correctly and I overrode it with the
Python result. **Verify zip separators by parsing the central directory bytes, never with
`zipfile` on Windows.**

That single defect explains both symptoms: the ~39 root-level entries have no separator, so the
app starts, migrates, seeds and answers `/health` - while `wwwroot/` never exists.

(a) publish output and (e) repo rules were genuinely ruled out: `publish-linux/wwwroot` holds
**1204 files** including both, no `.deployment` in the package, no static-asset override in
`RTUB.csproj`. CI is unaffected - `azure/webapps-deploy` packages on `ubuntu-latest`.

**Fix: `./scripts/package-azure-dev.sh`** packages with Python's zipfile (whose `os.sep` rewrite
is *correct* on write) and then refuses to emit an archive whose central directory contains a
backslash or is missing `wwwroot/manifest.webmanifest`, `wwwroot/service-worker.js`,
`libQuestPdfSkia.so` or `libe_sqlite3.so`. Verified on the real tree: **1263 entries, 0
backslash, all four present.**

**1b. `WEBSITE_RUN_FROM_PACKAGE=1` is separately wrong** - Microsoft: *"The run from package
feature is currently Windows only and is not yet supported in App Service for Linux."*
`rtub-dev` is Linux. 027 set it to `1` to dodge the rsync failure, i.e. to hide the backslash bug,
turning a loud failure into a silent one. Still to be removed, and the stale share underneath
cleared - `/home/site/wwwroot` holds an old **Windows** publish (`RTUB.exe`, `web.config`,
`hostingstart.html`, **zero directories**). Owner action; 027 does not modify Azure.

**2. Smoke state parser - trailing CR.** `az` on Windows emits CRLF, so `read -r` left
`SITE_STATE=$'Running\r'`. `case` fell through to the default branch, and printing the value made
the carriage return overwrite the start of the line - which is why it rendered as
`usageState=Normal` / `' FAIL  unexpected state 'Running`. Reproduced with `od -c`, fixed by
stripping CR, and pinned by `./scripts/smoke-azure-dev.sh --self-test`.

**3. R2 is required, not optional - earlier claim in this file was WRONG.**
`Index.razor` (`@page "/"`) injects `ISlideshowService` -> `SlideshowService(IImageStorageService)`
-> `CloudflareImageStorageService(IAmazonS3)` -> the factory throws. Confirmed from the live stack
trace: `ComponentFactory.CreatePropertyInjector` during `RenderEndpointComponent`, 24 occurrences.
`IAmazonS3` reaches **13** domain services through **11** storage services, so it covers most
routable pages. **Decision: A - R2 is genuinely required.** Not made optional: that would mean
every one of those services tolerating a no-op storage client, which changes semantics broadly and
turns real storage failures into silence. DEV needs `Cloudflare__R2__{AccountId,AccessKeyId,
SecretAccessKey,Bucket,PublicUrl}` against a **separate DEV bucket with a bucket-scoped token** -
never production credentials.

**`RemoteNavigationManager already initialized` is fallout, not a separate bug.** It is logged by
`ExceptionHandlerMiddleware[3]` ("An exception was thrown attempting to execute the error
handler"), always immediately after the R2 failure, with
`EndpointHtmlRenderer.InitializeStandardComponentServicesAsync` at the top of its stack:
`UseExceptionHandler("/Error")` re-renders `/Error` on an `HttpContext` whose
`RemoteNavigationManager` the first attempt already initialised. Fix R2 and it disappears. Not
touched.

### Database path/strategy
| | Production | DEV |
| --- | --- | --- |
| `ConnectionStrings__SqliteConnection` | `Data Source=/home/site/data/app.db` | 027 created `rtub-dev.db`; DEV now runs on `rtub-dev-v3.db` (see the redeploy checklist below). **Stale-prone - read the live setting, never hardcode it.** 029's first live refresh hit exactly this. |
| `DatabaseBackup__Enabled` | `true` | `false` |

Separate file, separate App Service, separate plan. Nothing shared with production. `/home` is the
Azure Files share - it survives restart, redeploy and scale. No migration was created by this unit.

### OIDC / federation status - CONFIGURED, NO OWNER ACTION LEFT
DEV authenticates with **OIDC against a user-assigned managed identity**. No publish profile, no
client secret, nothing long-lived in the repo.

| Thing | Value |
| --- | --- |
| Identity | `rtub-dev-deploy` (user-assigned managed identity, `rtub_group`) |
| Federated credential | `github-dev-env` |
| Issuer | `https://token.actions.githubusercontent.com` |
| Subject | `repo:luisfpires18/RTUB:environment:development` |
| Audience | `api://AzureADTokenExchange` |
| Role | `Website Contributor`, scoped to the **`rtub-dev` site only** |

A managed identity rather than an Entra app registration: it is an ordinary Azure resource, so it
lives and dies with `rtub_group`, needs no directory administration, and can never grow a secret.
(The tenant does permit app registrations - `allowedToCreateApps: true` - so this was a choice,
not a workaround.) The subject is the **environment** form, not the branch form, because the job
declares `environment: development`; a branch-form credential would not match.

GitHub environment **`development`** created, no approval gate. Non-secret identifiers stored as
**variables**, not secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`.

### Required setting NAMES (names only - values are in Azure, never in the repo)
Startup-fatal on a fresh DEV database: `AdminUser__Password`, and - because DEV opts into the full
seed - `SeedData__MemberPassword`.
Also set on `rtub-dev`: `ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_URLS`, `WEBSITES_PORT`,
`ASPNETCORE_FORWARDEDHEADERS_ENABLED`, `ConnectionStrings__SqliteConnection`,
`SeedData__SeedFullDataset`, `DatabaseBackup__Enabled`.

Deliberately **absent** in DEV, and none of them block startup - the R2 client is a lazy singleton
and push checks `WebPushOptions.IsConfigured()`, so each feature fails only when first used:
`Cloudflare__R2__*`, `WebPush__Vapid*`, `EmailSettings__*`, `DatabaseBackup__*` credentials,
Application Insights. **DEV must never be given production R2 or backup credentials.**

The two bootstrap passwords were generated randomly and written straight into `rtub-dev`'s
application settings. They were never printed, never committed and never placed in
`appsettings*.json`, workflow YAML or this file. Read them in Portal -> `rtub-dev` ->
Environment variables, or rotate them.

### What is automated vs. manual
Automated: build, all five test suites, publish (including the Pixi bundle build), Azure login,
DEV deploy, post-deploy smoke test - on every push to `dev`.
Manual: the merge from `dev` to `master`, and everything about production.

### Validation (unit 027)
- `dotnet build --configuration Release` - **clean**, 0 warnings, 0 errors.
- `dotnet test --no-build --configuration Release` - **4715 total, 0 failed, 4655 succeeded,
  60 skipped.** Baseline was 4713; **delta is exactly +2**, the two new `MigrationChainTests`.
- **Empty SQLite database migrates from zero.** `dotnet ef database update` against a fresh
  disposable file: **159 migrations applied**, last
  `20260723114241_AddPushNotificationsOptedOutToApplicationUser`, 91 tables,
  `PRAGMA quick_check` = `ok`, no leftover `ef_temp_*`/`_new`/`_old` tables, and all six
  `Year*`/`Month*` columns present on `AspNetUsers`.
- **Application starts against a genuinely empty database.** `ASPNETCORE_ENVIRONMENT=Staging`,
  `SeedData__SeedFullDataset=true`, connection string pointed at an empty disposable file: the
  app applied all 159 migrations *itself* through `Program.cs`, seeded **83 users and 4 roles**,
  logged `Application started`, and reported **zero** `fail:`/`SqliteException`/unhandled
  exceptions. That is the exact Azure DEV path.
- **The regression test was proven red before it was proven green.** Reverting the fix and
  re-running gives `2 failed` with
  `SqliteException : SQLite Error 1: 'no such column: "YearCaloiro"'` — the production error
  verbatim. The fix was then restored and byte-compared against a backup.
- Only disposable scratchpad SQLite files were used. `src/RTUB.Web/app.db` was never opened;
  every run set `ConnectionStrings__SqliteConnection` explicitly.
- `.github/workflows/ci.yml` parses as YAML; both jobs, their triggers, `needs`, `if`,
  `environment`, `permissions` and `concurrency` verified by structural inspection.
- The smoke-test step's shell body extracted and checked with `bash -n` - **syntax OK**.
- `git diff --check` - clean.
- Secret scan over the full diff and the new doc - clean. No Azure GUID (client, tenant or
  subscription ID) appears in any repository file; they live only in GitHub environment
  variables.
- No migration created. No application refactor.
- **Deployment smoke: NOT RUN** - see above.

## Deployment requirement — `AdminUser__Password` on a fresh database (unit 016)

**No immediate Azure action is required for this deploy.** Production `rtub` has an existing
database with users, so `InitializeAsync` returns before the bootstrap check is ever evaluated.
Deploying unit 016 to current production changes nothing at startup, and the existing Owner
account is unaffected — its password is whatever it was set to, not the removed default.

**Any fresh/empty database will now refuse to start without it.** Before any new App Service,
container, or local database created from scratch is first started, `AdminUser__Password` must be
supplied as an App Service application setting or equivalent secure configuration source, with a
real value — not a placeholder. Without it, seeding throws and no privileged account is created.
Like `ASPNETCORE_FORWARDEDHEADERS_ENABLED` below, this is portal/CLI configuration, is **not in
the repo**, is not applied by a code redeploy, and nothing in CI will warn if it is missing.

**Satisfied for Azure DEV (unit 027).** `AdminUser__Password` is set on `rtub-dev`, with a
randomly generated value written straight into App Service configuration — never printed,
committed or placed in any file. Read or rotate it in Portal → `rtub-dev` → Environment variables.

**`SeedData__MemberPassword`** is required wherever `SeedData:SeedFullDataset` is `true` against a
fresh database — which, since unit 027, is Azure DEV. It is set on `rtub-dev`, same handling as
above. Production does not set the flag and therefore never needs this key.

## Deployment requirement — Azure forwarded headers (CONFIRMED 2026-09-21, no longer blocking)

**Resolved.** `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` **is set** on App Service `rtub`
(resource group `rtub_group`), verified by the owner via Azure CLI on **2026-09-21**. Unit 014's
login rate limiter therefore partitions on the real client address in production, as designed.

**Production `rtub` is Azure App Service on Linux.** `curl -I https://rtub.azurewebsites.net/health`
returns `Server: Kestrel` — no IIS layer, so there is no `UseIISIntegration` auto-wiring of
forwarded headers. The setting above is Microsoft's documented switch for App Service Linux /
containers: the host wires `ForwardedHeadersMiddleware` itself, ahead of the app pipeline, with
cloud-appropriate settings. Nothing is in RTUB's own code for it — deliberately: no
`UseForwardedHeaders` call, no `ForwardedHeadersOptions`, no clearing of
`KnownProxies`/`KnownNetworks`, and no manual `X-Forwarded-For` parsing.

It is portal/CLI configuration and is **not in the repo** — `.github/workflows/ci.yml` only
publishes and deploys, and sets no app settings. So it is not reproduced by a redeploy of code
alone, and **any new App Service needs it set again.** Nothing in CI will warn if it is missing.

**Also set on `rtub-dev` (unit 027).** Azure DEV carries `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`
for the same reason, so the login rate limiter partitions on the real client address there too.

What `RemoteIpAddress` is, per environment:
- **Local / `dotnet run`** — the real client address; correct with no extra configuration.
- **Integration tests (`TestServer`)** — `null` for every request (no transport), so all callers
  share the `"unknown"` partition. `RemoteIpTestStartupFilter` (test host only) works around this
  by setting the address from an `X-Test-Remote-Ip` header; see *Previous validation (unit 014)*.
- **Azure App Service (current prod, Linux/Kestrel)** — **the real client address**, as of the
  2026-09-21 confirmation above.
- **Future Azure dev environment** — platform front end until the same setting is applied there.

### Consequence now live — `UseHttpsRedirection`
Turning the setting on makes `Request.IsHttps` true behind the proxy. The skip of
`UseHttpsRedirection` outside Development at `Program.cs:325` (comment: "HTTPS is handled at the
load balancer level") was the workaround for exactly the case that no longer applies. The skip is
now redundant rather than load-bearing. Still **not changed** — it is its own decision, and
removing it is a behavior change to the production request pipeline. Carried in *Deferred* below.

## Current task
None active. The 029 follow-up (DEV database path resolution) is complete, uncommitted on `dev`,
and awaiting owner review. The only 029 item left is the first real `workflow_dispatch` run.

## Next unit
**030 - production deployment modernization.** (Was numbered 028 before `fix/028/bet-test-isolation`
took that number.) Deliberately kept out of 027 and 029 so neither a DEV pipeline change nor a DEV
database refresh could ever take production down. Nothing in 029 touches production deployment,
master auto-deploy, release versioning or production rollback - all of that is this unit. Scope:
- migrate production from the `AZURE_WEBAPP_PUBLISH_PROFILE` Basic Auth secret to OIDC, the same
  way DEV now works (its own managed identity, scoped to the `rtub` site)
- only then delete the publish-profile secret and turn Basic Auth Publishing Credentials off
- bump the production job's stale action pins: `actions/checkout@v4`, `setup-dotnet@v4`,
  `setup-node@v4` (Node **20**, end-of-life since April 2026), `upload-artifact@v4`
- decide whether production should also run from package, as DEV does
- **close production's native-asset packaging risk**, found by 027 and deliberately left alone.
  Production App Service is Linux too, and all three of DEV's mitigations are missing there:
  it publishes **portable**, so `libQuestPdfSkia.so` and `libe_sqlite3.so` sit inside a 72 MB
  22-RID `runtimes/` subtree in a ~330 MB payload; it has **no `WEBSITE_RUN_FROM_PACKAGE`**, so it
  deploys through the same extract-and-rsync transport that truncated DEV; and it has **no deploy
  guard**. A partial transfer would abort production with the identical
  `DllNotFoundException` at `Program.cs:48` and exit 134. It works today - nothing defends it.
  `-r linux-x64 --self-contained false` applies to production unchanged.

Constraints 026 hands forward, all still live:
- `/service-worker.js` must keep being served **without** a CSP header (025's rule, still pinned
  by `NonDocumentResponse_CarriesNoContentSecurityPolicy`).
- `sw-register.js` is the **sole** service-worker registration owner. Anything needing the
  registration adopts it via `navigator.serviceWorker.ready`.
- No stylesheet may set `overflow-x: hidden` on `html`/`body` again, in any media query - it must
  be `clip`. Pinned by `NoStylesheet_SetsOverflowHiddenOnTheViewportRoot`.
- Service-worker caches must stay free of application HTML and of `/api`, `/auth`, `/_blazor`,
  `/hubs`, `/health`. `/health` in particular must stay network-only: `offline.js` uses it as its
  origin-reachability probe, and a cached 200 would resurrect the bounce bug.

**One confirmation is outstanding and needs a real device:** the MobileBottomNav fix is proven by
the cascade and by the computed `overflow-x` flipping under a runtime-ungated standalone query,
but desktop Chromium cannot reproduce iOS momentum-scroll drift and cannot emulate
`display-mode: standalone`. A run on an **installed iPhone PWA on Albums (`/music`)** would close
it. **Unit 027 is what unblocks this** - `https://rtub-dev.azurewebsites.net` is now a real
installable HTTPS origin carrying 026's code, so the iPhone check no longer needs production.

Also still available, deliberately not taken: **password-policy review / hardening** (Identity is
`RequiredLength = 4` with every complexity rule off, `AddIdentityServices`,
`ServiceCollectionExtensions.cs`) - the owner skipped it in 021.
Still queued, not security: **Microsoft 10.0.11 -> 10.0.12 servicing train** across `src/` + tests,
which also unblocks `MockQueryable.Moq 10.0.12`.

## Blockers
**None in the repository.** All three defects that stopped Azure DEV are fixed and proven
locally:

1. deployment transport truncated `wwwroot` - RID-specific publish + run-from-package + a
   deploy guard;
2. `libQuestPdfSkia.so` missing from the deployed tree - same fix, guard refuses to deploy
   without it;
3. **the migration chain could not run from zero** - `AddMentorField` fixed, proven red then
   green, covered by a new regression test.

What remains is an **owner action**, not a blocker: redeploy `rtub-dev` from the corrected
artifact and run the smoke script. The tree currently on the App Service is the truncated one and
its database has never completed migrating, so both must be replaced, not restarted.

The earlier Free-tier quota block has passed (it reset at 00:00 UTC) and DEV is on B1 by owner
decision. `healthCheckPath` stays empty until a deploy has succeeded.

### Redeploy + verify checklist (owner runs this; 027 did not execute it)
The tree currently on `rtub-dev` is the **truncated** one - it must be replaced, not restarted.
DEV is on B1 for now, by owner decision; return it to F1 only after a clean boot and a green
smoke run.

**Point DEV at a fresh database file before redeploying.** `rtub-dev-v2.db` was left
half-migrated by the failing chain - 15 migrations applied, `AspNetUsers` missing three columns -
and the fixed `AddMentorField` will not repair it, because that migration is already recorded in
its `__EFMigrationsHistory` and will never re-run. A new path (`rtub-dev-v3.db`) migrates cleanly
from zero. DEV holds no data worth keeping.

```bash
# 1. publish linux-x64, framework-dependent (~267 MB, natives at the root)
dotnet publish src/RTUB.Web/RTUB.csproj -c Release -r linux-x64 --self-contained false -o ./publish

# 2. same guard CI runs - refuses to go further if a native library is missing or truncated
for lib in libQuestPdfSkia.so libe_sqlite3.so; do
  f="./publish/$lib"; [ -f "$f" ] || f="./publish/runtimes/linux-x64/native/$lib"
  [ -s "$f" ] && file -L "$f" | grep -q 'ELF 64-bit.*x86-64' && echo "OK $f" || echo "MISSING $lib"
done

# 3. package. NEVER use Compress-Archive: it writes backslash separators, which Azure
#    Linux cannot unpack (HTTP 400 from rsync, or silent 404s on every static asset).
#    This script emits forward slashes and refuses to produce a broken archive.
#    Writes to TMPDIR, not the repo.
./scripts/package-azure-dev.sh ./publish
pkg="${TMPDIR:-/tmp}/rtub-dev.zip"

# 4. FIRST, one-time: run-from-package is Windows-only and must be off on this Linux app.
#    Leaving it set is what makes /manifest.webmanifest and /service-worker.js 404.
#    Also clear the stale share underneath the old mount - it still holds a Windows publish.
az webapp config appsettings delete -g rtub_group -n rtub-dev --setting-names WEBSITE_RUN_FROM_PACKAGE

# 5. deploy - ordinary extraction, the way production deploys
az webapp deploy -g rtub_group -n rtub-dev --type zip --src-path "$pkg"

# 6. verify
./scripts/smoke-azure-dev.sh
```

Also set the five `Cloudflare__R2__*` names before step 6 — see *R2 is required* above. `/` returns
500 without them, so smoke cannot go green. Use a **separate DEV bucket and a bucket-scoped
token**, never production credentials.

Step 5 is the one that previously failed silently. Confirm it before trusting step 6:
`az webapp log deployment show -n rtub-dev -g rtub_group` must end in a succeeded OneDeploy, not
an rsync error.

Or simply push the branch once it is authorised — `deploy-dev` does steps 1, 2, 4 and 5 itself.

Plain verification, no redeploy:

```bash
./scripts/smoke-azure-dev.sh
```

That script is **read-only**. It does A, B and E–F below, and for C it prints the start command
and stops rather than running it, because on Free tier every restart spends part of a 15/day
allowance.

| | Step | Expected |
| --- | --- | --- |
| A | `az webapp config show -n rtub-dev -g rtub_group --query healthCheckPath -o tsv` | **empty** |
| B | `az webapp show -n rtub-dev -g rtub_group --query "[state,usageState]" -o tsv` | `Running  Normal` — not `QuotaExceeded` |
| C | `az webapp start -n rtub-dev -g rtub_group` | **once only, and only if stopped** |
| D | wait ~60s | first boot runs migrations + the full seed on an empty DB |
| E | GET `/health`, `/login`, `/manifest.webmanifest`, `/service-worker.js` | all 200 |
| F | headers | CSP on HTML · **no** CSP on `/service-worker.js` · site still up afterwards |
| G | — | **no configuration writes at any point during the test** |

If the app boots and stays up, F1 is viable and nothing further is needed. If it crash-loops on
its own, that is the first real evidence about F1, and the plan decision follows from it.

Everything else in this unit is done: workflow, `SeedData` switch, docs, the App Service and its
settings, the managed identity, the federated credential, the role assignment, and the GitHub
`development` environment with its three variables. The first push to `dev` will deploy through
CI regardless.

## Pre-deploy bug queue
Defects that must be fixed **before the final production deployment**. Not part of the unit that
recorded them — each needs its own unit.

### LOG-1 — Logistics management controls missing for Moderator
**Recorded by 027. NOT implemented in 027.**

Observed: an **Admin** sees the full Logistics management set —
- Adicionar Lista
- list add
- edit
- delete

A **Moderator** does not get the same controls.

**Owner decision (2026-09-21): Moderators ARE allowed the same Logistics management permissions
as Admins for these Logistics actions.**

Scope guard, explicit: this applies to **Logistics functionality only**. Do **not** generalize
Moderator to Admin globally, and do not widen it into a role-model change. The fix is the
Logistics authorization checks and whatever gates the four controls above — nothing else.

Not investigated yet: no files identified, no root cause traced. 027 is an infrastructure unit
and deliberately did not look.

## Owner actions
**Owner action, not a blocker:** the historical GitGuardian incidents stay historical. The
literals remain in old commits, and unit 015 deliberately did **not** rewrite git history to clear
them. Mark those incidents "false positive / test credential" in GitGuardian by hand. 015 only
stops *future* commits from raising new ones. No GitGuardian ignore comment was added either.

**Deployment note for 025:** the policy is built from `Cloudflare:R2:PublicUrl` and
`Cloudflare:R2:AccountId`. Both are already required by the storage services, so **no new
configuration key is introduced** and no portal change is needed for this deploy. If either were
ever unset in an environment, CSP would omit that source - R2 images/audio/PDFs would be blocked
rather than the policy being weakened.

## Deferred / owner decisions

### Security — raised by 015, deliberately not changed
- ~~**Two hardcoded production password defaults in `src/`**~~ — **fixed by unit 016.** Both now
  fail closed; see *Last completed step* above.
- ~~**`ResetDevDataAsync` runs in EVERY environment except Production**~~ - **fixed by unit 017**,
  scope adjusted by **027**. Opt-in (`DevelopmentDataReset:Enabled`), disabled by default, no
  hardcoded hash, validated before any mutation. The environment guard is an **allow-list of
  Development and Staging** — 017 restricted it to Development, and 027 added Staging because
  that is the Azure DEV App Service, whose 83 seeded members exist to be reset to one shared
  development password on demand. Production, Test and any other environment name are a hard
  no-op that never even reads the password.
- ~~"the bulk member seed is dead code"~~ — **that earlier claim was wrong and is retracted.** It
  is an intentional manual full-seed developer path (`isEmptyDb`), now documented in place and
  covered by tests. Nothing to clean up.
- **Old commits still contain the removed literals.** History was not rewritten, by instruction.
- The `src/` scan was run for completeness only; **no production file was touched by 015** and
  production `appsettings*.json` contain no credential keys at all.

### Security — raised by 014, deliberately not changed
- **`RateLimiterOptions.OnRejected` is global, not per-policy.** Today only the `login` policy
  exists, so the Portuguese rejection body can only be reached by a login rejection. **A second
  policy added later must either share that message or move the callback onto its own policy.**
- **A 429 on a browser form POST renders as a bare text page**, not the styled `/login?error=...`
  page the endpoint's other failures use. Redirecting instead would mean answering 302, which
  contradicts the required 429. Left as-is; a status-code page for 429 is a UX decision.
- **Existing login tests share the `"unknown"` partition.** `AuthAntiforgeryTests` (5 login POSTs)
  and `CookieApiCsrfTests` (4) are under the limit of 10 and each class gets its own factory, so
  they are safe today. Adding a sixth login POST to one of those classes would start hitting 429 —
  use the `X-Test-Remote-Ip` header for a distinct partition if that happens.
- **`UseHttpsRedirection` is skipped outside Development** (`Program.cs:325`). **Now actionable:**
  `ASPNETCORE_FORWARDEDHEADERS_ENABLED` is confirmed on as of 2026-09-21, so `Request.IsHttps` is
  true behind the proxy and the skip no longer works around anything. Removing it is a production
  request-pipeline behavior change, so it is its own unit, not a ride-along.

### Security — reviewed by 013, deliberately not changed
- **`AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN")`** is still configured and still unused by
  every endpoint and client. 013 confirmed no endpoint needs it. Harmless; drop it, or keep it as
  the hook for a future header-token client.
- **`POST /api/push/broadcast` and `POST /api/push/send-to-selected` have no browser caller.**
  Dead HTTP surface, but role-gated and not CSRF-reachable — deletion is cleanup, not security.
- **`Set-Cookie` carries no `Secure` flag under the test host**, because the default is
  `CookieSecurePolicy.SameAsRequest` and the test client speaks http. Over https in production the
  flag is emitted. Not changed; an explicit `Always` is a separate hardening decision.
- ~~**Pre-existing integration-test flake, not caused by 013**~~ — **fixed by unit 018.** The
  shared `SqliteConnection` is gone and the seed now runs in `IHostedLifecycleService.StartingAsync`.
  Measured before/after in the same harness: **12 failures in 40 isolated runs → 0 in 40.** See
  *Last completed step*.
- ~~Out of scope by instruction and untouched: ... cookie validation / SQLite pressure ...~~ -
  **done by unit 019** (Option A, 5-minute write throttle). Still out of scope and untouched:
  password policy, MFA, security headers / CSP, PWA cache strategy, push architecture refactor,
  `Program.cs` cleanup.

### Raised by 019, deliberately not changed
- **`LastLoginDate` is named "last login" but means "last authenticated activity".** Unit 019 kept
  the name by owner decision. Renaming it (say to `LastActivityAt`) needs a migration plus an edit
  to `LoginStatusBadge`, `AvatarCard`, `UserCard`, `UserRoles`, `UserProfileService`,
  `LoginStatisticsButton` and `AuditLogAppender`. Its own unit if wanted.
- ~~**RTUB's `OnValidatePrincipal` replaces Identity's `SecurityStampValidator` entirely**~~ -
  **fixed by unit 020.** The handler now calls the framework validator after its own checks. The
  stricter per-request stamp check was kept, `ValidationInterval` stays at the framework default,
  and the principal refresh / `ShouldRenew` behaviour is back. See *Last completed step*.
- **The `SQLITE_LOCKED` retry loop was kept**, now correctly commented. The `IsInRoleAsync` pair is
  still 2 SELECTs and could be one join - not done, out of 019's scope.
- Known and accepted framework limitation (from 012): antiforgery tokens are bound to the user
  identity, so multiple tabs signed in as different users are unsupported. Documented ASP.NET Core
  behavior, not an RTUB defect.

### Raised by 020, deliberately not changed
- **`UserRoles.razor:613` and `RoleManagementService.cs:121` call `UpdateSecurityStampAsync` on a
  role change**, commented "force fresh cookies and token refresh". That is not what it does in
  stock Identity either: bumping the stamp makes `VerifySecurityStamp` fail, so it is a **forced
  logout**, not a claims refresh. With 020's composition a plain role change would now propagate
  to a live session within `ValidationInterval` without logging anyone out. Deciding whether
  either call site should drop the stamp bump is a UX/security decision, not a ride-along.
- **`ValidationInterval` is still the framework default (30 min) and still unconfigured.** Nothing
  in the repo sets `SecurityStampValidatorOptions`. Lowering it would only speed up claim refresh -
  every security check already runs per request - at 2 extra SELECTs per user per interval.
- **The `IsInRoleAsync` pair is still 2 SELECTs** and could be one join. Out of 020's scope, as it
  was out of 019's; the framework composition did not eliminate it.

### Raised by 021, deliberately not changed
- ~~**CSP is not enabled.**~~ - **done.** All four blockers 021 enumerated are cleared: `eval`
  (022), inline `<script>` and `on*` handlers (023, plus the JS-built handler 025 found), inline
  `<style>` and `style=` (024), and the runtime R2 origin (025 - which turned out to be *two*
  origins). The enforced header ships in 025. No report-only policy was ever needed.
- **`Cross-Origin-Opener-Policy` / `Cross-Origin-Embedder-Policy` / `Cross-Origin-Resource-Policy`
  were not added.** Outside the brief, and COOP in particular needs its own check of the
  `LoginPopup` flow and anything relying on `window.opener` before it can be called safe. Cheap to
  add later; not free to add blind.
- **`X-XSS-Protection` was not added.** It is removed from Chrome and Edge, ignored by Firefox,
  and its legacy filter mode was itself an exploitable primitive. Adding it is pure theatre.
- **`https://fonts.googleapis.com` is a dead `preconnect` + `dns-prefetch`** in `App.razor:24-25`:
  no stylesheet link, no `@font-face`, nothing loads from it. Two wasted connection hints. Delete
  them in whichever frontend unit is next in that file - not worth a unit of their own, and 021
  had no reason to touch `App.razor`.
- **`UseHttpsRedirection` is still skipped outside Development** (`Program.cs`). Unchanged for the
  third unit running; see the 014 entry above. Now that forwarded headers are confirmed on, this
  is a one-line change gated only on someone accepting a production pipeline behaviour change.

### Carried forward (unchanged)
- **`xUnit1051` suppressed, not adopted** (1634 sites). Its own unit if wanted: mechanical, but it
  touches nearly every test file, so it must not ride along with anything else.
- **`Microsoft.Testing.Extensions.Telemetry/2.4.0`** arrives transitively via `xunit.v3` and
  reports usage metrics to Microsoft. Opt out with `TESTINGPLATFORM_TELEMETRY_OPTOUT=1`.
- **`global.json` raises the SDK floor to 10.0.100** for running tests. No pin added.
- Running a test `.exe` directly gives xUnit's native console runner, not the MTP CLI, because
  `UseMicrosoftTestingPlatformRunner` was not set. `dotnet test` is unaffected.
- **Remaining dependency findings:**
  - Microsoft 10.0.11 → **10.0.12** train — one servicing bump across src + tests. Not started.
  - `MockQueryable.Moq 10.0.12` — held at 10.0.8; gated on the EF Core 10.0.12 bump above.
  - `AWSSDK.Core 4.0.3.8` → 4.0.102.6 and `AWSSDK.S3 4.0.10` → 4.0.103.3 — independent train.
  - `QuestPDF 2024.10.3` → 2026.9.0 — major train, likely breaking. Not started.
  - `AWSSDK.Core` is referenced directly by `RTUB.Integration.Tests`, `RTUB.Shared.Tests` and
    `RTUB.Web.Tests` but **used by none of them**. Three dead direct references — AWS train.
  - `Portable.BouncyCastle 1.9.0` — transitive via `WebPush 1.0.13`. Legacy id, not deprecated,
    no advisories. Note it if an advisory lands.
- **Test-suite hygiene:** `RTUB.Web.Tests` skips **56 of 791** tests (mostly `*PageTests` modal /
  button cases). Pre-existing.
- **Push subsystem debt:** `WebPushClient` is still newed up inside `PushNotificationService`, so
  it cannot be mocked and no test covers an actual send, a retry, or 404/410 cleanup. Also: two
  service-worker registration paths, unbounded `BroadcastAsync` fan-out, no `CancellationToken`,
  no delivery metrics.
### Raised by 022, deliberately not changed
- **`src/RTUB.Web/wwwroot/js/rtub.carousel.js` is dead.** A minified Bootstrap-Carousel fallback
  shim that self-registers on `DOMContentLoaded` and immediately bails when `bootstrap.Carousel`
  exists. It is referenced by **no** `<VersionedAsset>`, no `<script>` and no interop call, so it
  is never loaded at all. Delete it, or wire it up deliberately - 022 left it exactly as it was.
- **`PushNotificationsManager` is still constructed inside `pwaHelper.initializePushManager` and
  parked on `window.rtubPushManager`.** 022 kept that arrangement on purpose (it is the push
  architecture, not the eval cleanup). The push-modernization unit owns it.

- **Phase 1C:** remaining optional custom skills — deliberately not created.
- Work-branch cleanup (`chore/001`–`chore/011`) — delete when convenient.
- Two `graphifyy 0.9.56 + MCP` installs (isolated venv, Microsoft-Store Python user site). The
  Store one wins PATH and works; both are compatible, so neither needs removing.
- Pending feature work — unchanged, not part of any phase.

### Raised by 023, deliberately not changed
- **Two service-worker registration paths still exist.** 023 removed the `App.razor` duplicate, so
  page-load registration is `wwwroot/js/sw-register.js` alone - but
  `wwwroot/js/push-notifications.js:91` still calls `navigator.serviceWorker.register()` on demand
  during push opt-in, with **different options** (no `scope`, no `updateViaCache`). Pre-existing,
  and already flagged by the `rtub-push` skill. The push unit owns consolidating it.
- ~~`sw-register.js` toast styles~~ and ~~`offline.html` inline `<style>`~~ - **both done by 024.**
- ~~**`App.razor` still has the dead `fonts.googleapis.com` `preconnect` / `dns-prefetch`**~~ -
  **deleted by unit 025**, where `font-src` was decided. Re-verified dead first, and now pinned by
  `NoWebFontServiceIsReferenced`.
- **`rtub.carousel.js` is still dead** (raised by 022, unchanged).

### Raised by 024 (two items closed by 025)
- ~~**`memberMap.js` still builds an inline `onerror` handler inside its popup HTML string**~~ -
  **fixed by unit 025.** Swapped for `data-avatar-fallback`, and the whole class of bug is now
  swept repository-wide by `ApplicationJavaScript_BuildsNoMarkupCarryingInlineEventHandlers`
  rather than pinned file by file. Verified in the browser with a failing avatar URL.
- **`.meeting-today-badge` is orphaned CSS.** Now in `css/3-components/meeting-card.css`; no element
  has ever carried the class. Kept because deleting it is unrelated cleanup, and its absence from the
  markup is now recorded in `MeetingCardTests`.
- **The rare-tab badge animation is dead.** `animation: rare-tab-pulse` cannot resolve, because the
  keyframes exist only in scoped CSS where Blazor renames them. Broken before 024 and identically
  broken after. Fixing it would start an animation that has never run - a visual change, so it is the
  owner's call.
- **`Profile.razor`'s `overflow-x: hidden` intent is unimplemented.** The `:global(...)` rule was
  inert and was deleted. If horizontal overflow on the profile page was a real concern, it needs a
  real rule - that is a visual change, not a CSP one.
- **`dotnet test` does not work in this repo.** It reports "Zero tests ran" / exit 5 for every
  project, with or without a filter, so the test executables have to be run directly
  (`tests/<proj>/bin/<config>/net10.0/<proj>.exe`, filtered with `-class` / `-method`). Confirmed
  still broken in 025, which used the native executables throughout. Worth fixing, since CI and the
  recorded baselines depend on it.
- **`BetServiceTests.PlaceBetAsync_WithInsufficientBalance_ThrowsException` is flaky, not
  consistently failing.** 024 saw it fail in a full-project run and pass in isolation at
  `00e8009f`, and attributed it to shared-fixture ordering. On 025's Release run it **passed in
  both** - full 1997-test project run and isolation. Nothing in 025 touches `BetService` or any
  fixture. Still pre-existing, still its own unit; the ordering dependency is real even when the
  symptom does not appear.

### Found by 026
- ~~**`offline.js` redirects off the offline page whenever `navigator.onLine` is true**~~ -
  **FIXED before merge; see *E. Offline page bounced off itself* above.** It now probes `/health`
  with `cache: 'no-store'` and only a successful response redirects. Proven with the origin
  stopped and the machine still online, and negative-controlled against the pre-fix file.
- **`.no-scroll` sets `position: fixed` with no `top` offset** (`1-base/global.css`). Applying it
  scrolls the page back to the top and does not restore the offset on removal. It does **not**
  affect MobileBottomNav - `position: fixed` on `body` is not a containing-block trigger - so it is
  unrelated to the 026 bug. Pre-existing; a scroll-position fix is a behaviour change.

### Carried forward unchanged from 025
- ~~**`dotnet test` still does not work in this repo.**~~ - **WRONG, corrected by 027.** It works,
  and it discovers all five suites: `total: 4713, failed: 0, skipped: 60` on SDK `10.0.200`. The
  enabling piece is `global.json`'s `{"test": {"runner": "Microsoft.Testing.Platform"}}`, added
  by unit 011. 025 and 026 invoking the five native executables by hand was unnecessary, not
  required. CI has been running the `dotnet test` form correctly all along.
- **`BetServiceTests.PlaceBetAsync_WithInsufficientBalance_ThrowsException`** passed in 026's full
  Release run and again in 027's. Not touched, as instructed. Still its own unit.

### Found by 027, deliberately not changed
- **Five dead App Service settings on production `rtub`: `IDrive__AccessKey`, `IDrive__Bucket`,
  `IDrive__Endpoint`, `IDrive__SecretKey`** - `grep -rn "IDrive" src/` returns nothing. Left over
  from the storage provider that preceded Cloudflare R2. They are portal configuration, not repo
  content, and two of them are live credentials for whatever that account still is. Deleting them
  is an owner action on production configuration and is out of scope for a DEV unit. Worth doing:
  an unused credential is still a credential.
- **Production's workflow pins are stale**: `actions/checkout@v4`, `actions/setup-dotnet@v4`,
  `actions/setup-node@v4` with **Node 20**, end-of-life since April 2026, and
  `actions/upload-artifact@v4`. Current majors are v7 / v6 / v7 / v7. Not bumped here on purpose -
  those steps are the production deployment path. Folded into unit 028 above.
- **The skip of `UseHttpsRedirection` outside Development** (`Program.cs`) is now redundant on DEV
  too, for the same reason it is on production - `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` makes
  `Request.IsHttps` true behind the front end. Unchanged; still its own decision, already carried
  above.
- **`.deployment` sets `SCM_DO_BUILD_DURING_DEPLOYMENT=true`** but is inert for both environments:
  neither pipeline deploys the repository, they deploy `dotnet publish` output, which does not
  contain that file. Harmless, and removing it is not this unit's call.
- **Azure DEV runs from package, production does not.** `WEBSITE_RUN_FROM_PACKAGE=1` is set on
  `rtub-dev` because the ordinary extract-and-rsync deployment **failed** on it - Kudu's parallel
  rsync exited 123 unpacking the ~334 MB payload (182 MB of which is `wwwroot/sprites`) through
  the container's small local disk. Run-from-package mounts the zip instead of extracting it.
  Nothing in the app writes into `wwwroot` at runtime - every `WebRootPath` use is a read - and
  SQLite lives under `/home/site/data`, so the read-only `wwwroot` costs nothing. Whether
  production should do the same is listed under unit 028.
- **The publish payload is 334 MB, 182 MB of it `wwwroot/sprites`.** That is what makes deployment
  awkward on a small container and it is why run-from-package was needed. Moving game sprites to
  R2, or trimming them, would shrink every deploy for both environments. Not touched - it is an
  application change, not a pipeline one.
