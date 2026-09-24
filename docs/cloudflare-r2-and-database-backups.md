# Cloudflare R2 & Database Backups

Reference for the R2 storage layer and the planned SQLite backup pipeline.
Companion to [backend-practices.md](backend-practices.md).

## R2 architecture

One bucket (`rtub`), one endpoint, **one singleton `IAmazonS3`** shared by all 11 storage
services. R2 endpoints are per-account, not per-bucket, so additional buckets in the same
account need only a different bucket name — not a second client.

Registered in `ServiceCollectionExtensions.AddInfrastructureServices`:

```
ServiceURL          = https://{Cloudflare:R2:AccountId}.r2.cloudflarestorage.com
ForcePathStyle      = true
AuthenticationRegion= "auto"
Credentials         = BasicAWSCredentials(AccessKeyId, SecretAccessKey)
```

R2 quirks the codebase already accounts for:

- `UseChunkEncoding = false` on every `PutObjectRequest` — required for R2.
- `DisablePayloadSigning = true` where the stream is non-seekable (Blazor uploads).
- No multipart/`TransferUtility` usage anywhere; all uploads are single `PutObject`.

### Class layout

| Layer | Type | Notes |
|---|---|---|
| Generic S3 | `Services/Storage/BaseStorageService<T>` | Takes an arbitrary bucket name. Put/Delete/List/BatchDelete/Presign/ObjectExists/GetObjectSize/ExtractObjectKeyFromUrl. |
| R2 + public media | `Services/Storage/BaseCloudflareStorageService<T>` | Reads `Cloudflare:R2:Bucket`; `UploadMediaAsync` forces `PublicRead` + immutable cache-control. **Not suitable for private objects.** |
| Concrete | `Services/Cloudflare*StorageService.cs` (11) | Interfaces in `Interfaces/I*StorageService.cs`, DI in `AddStorageServices`. |

### Access patterns

- **Public**: objects written `PublicRead`; URL = `{Cloudflare:R2:PublicUrl}/{objectKey}`.
- **Private-ish** (documents, receipts): pre-signed URLs, `Storage:UrlExpirationMinutes` (default 60).
- CORS is **not** configured in code. PixiJS CORS is sidestepped by `CdnProxyController`
  (`/api/cdn/image`, 1 h memory cache + 24 h browser cache), used from `Pages/MyTuno/Stage.razor`.

### Object key namespace (single bucket, shared by all environments)

`{env}` = `IHostEnvironment.EnvironmentName`, so Development and Production coexist.

```
images/{env}/{entityType}/...          images/{env}/gallery/{photos|videos}/...
events/{env}/{images|videos}/...       songs/{env}/videos/...
naipes/{env}/{videos|images}/{instr}/  item-configs/{env}/images/{typeKey}/...
receipts/{env}/...                     albums/{album}/{song}.mp3     (no env segment)
lyrics/{album}/{song}.pdf  (no env)    documents folder tree (audit-logged CRUD)
```

## Configuration keys

Env-var form uses `__` as separator. Only `Bucket` is committed; the rest are secrets
(User Secrets `fccae9d8-5630-4721-820a-d7487b90be65` locally, Azure App Settings in prod).

| Key | Committed? | Consumed by |
|---|---|---|
| `Cloudflare:R2:Bucket` (`"rtub"`) | `appsettings.json` | `BaseCloudflareStorageService` |
| `Cloudflare:R2:AccountId` | secret | S3 client ServiceURL |
| `Cloudflare:R2:AccessKeyId` | secret | S3 client |
| `Cloudflare:R2:SecretAccessKey` | secret | S3 client |
| `Cloudflare:R2:PublicUrl` | secret | 9 services + `CdnProxyController` + `Stage.razor` |
| `Storage:UrlExpirationMinutes` | default 60 | Document/Receipt presign |
| `ConnectionStrings:SqliteConnection` | env only; falls back to `Data Source=app.db` | DbContextFactory |

> `README.md`'s configuration section still documents `IDrive__*`. That is stale — storage
> moved to R2 in commit `517d6599`. Fix when next touching the README.

## Account-migration constraint

**Absolute public R2 URLs are persisted in the database** (`ImageUrl`, `MediaUrl`,
`VideoUrl`, `ReceiptUrl`, `PictureUrl` on Album, ApplicationUser, Event, GalleryMedia,
BetComment, ForgeComboConfig, …). Cloudflare has no cross-account bucket transfer, and a
new account issues a new `pub-<hash>.r2.dev` host, so any account move breaks every stored
URL unless either:

- a **custom domain** (e.g. `cdn.rtub.pt`) is bound to the bucket and stored URLs are
  rewritten to it once — future moves then become DNS-only; **preferred**, or
- stored URLs are rewritten to the new `pub-*.r2.dev` on every move.

Object copy itself is `rclone sync old:rtub new:rtub` (or `aws s3 sync` across two
endpoints) preserving keys exactly. Keeping the bucket name `rtub` avoids editing
committed config.

No Workers, Pages, `wrangler.toml`, or DNS config exists in this repo — anything beyond R2
(zone for `rtub.pt`, WAF, cache rules, email routing) must be checked in the dashboard.
`https://rtub.pt` appears in ~15 services only as a hardcoded app base-URL fallback.

## SQLite / WAL

- `SqliteConnectionInterceptor` sets `journal_mode=WAL`, `busy_timeout=30000`,
  `synchronous=NORMAL`, `temp_store=MEMORY`, `mmap_size=256MB`, `cache_size=-64000`.
- `Program.cs` sets `DefaultTimeout=30` and deliberately **does not** use `Cache=Shared`
  (shared-cache table locks deadlock with WAL under Blazor Server).
- `IDbContextFactory<ApplicationDbContext>` throughout; migrations + seed run at startup
  unless `EnvironmentName == "Test"`.

Because of WAL, backups must **never** be a file copy of `app.db` / `-wal` / `-shm`.

## Database backup pipeline

Implemented. Daily online snapshot via `Microsoft.Data.Sqlite`, uploaded to a private R2
bucket, retaining exactly two generations.

```
live SQLite --SqliteConnection.BackupDatabase()--> temp file in Path.GetTempPath()
  -> validate: PRAGMA quick_check + schema-object count + size-ratio vs live db
  -> upload to  database/incoming.db   (private, no-store, size-verified against local)
  -> server-side copy  current.db -> previous.db   (skipped when absent = first run)
  -> server-side copy  incoming.db -> current.db ; delete incoming.db
  -> finally: delete temp file (success or failure)
```

The staging key is the safety property: a snapshot only becomes `current.db` after it has
passed validation **and** its uploaded byte count matches the local file. Any earlier
failure returns before touching stored objects, so a failed or corrupt run can never
replace the last known-good backup.

### Files

| File | Role |
|---|---|
| `Configuration/DatabaseBackupOptions.cs` | Bound to the `DatabaseBackup` section |
| `Interfaces/IDatabaseBackupStorageService.cs` | Upload / copy / delete / exists / size / last-modified |
| `Services/Storage/DatabaseBackupStorageService.cs` | R2 implementation |
| `Services/DatabaseBackupBackgroundService.cs` | Scheduler + snapshot + validation + rotation |
| `Services/Storage/BaseStorageService.cs` | Gained `CopyObjectAsync` and `GetObjectLastModifiedUtcAsync` |
| `Extensions/ServiceCollectionExtensions.cs` | `AddDatabaseBackupServices` |

### Design decisions

- `DatabaseBackupStorageService` derives from `BaseStorageService<T>`, **not**
  `BaseCloudflareStorageService<T>` — the latter reads the media bucket name and its
  upload helper forces `PublicRead` plus a year-long immutable cache header.
- Its S3 client is a **keyed** singleton (`"R2Backup"`), separate from the media client,
  so the backup can use a token scoped to the backup bucket alone. Credentials fall back
  to the `Cloudflare:R2:*` values when no dedicated ones are set.
- Rotation is two server-side `CopyObject` calls — the snapshot is uploaded once.
- Nothing is registered at all when `DatabaseBackup:Enabled` is false, so dev and test
  environments can never write to the backup bucket.
- Scheduler follows the existing `BackgroundService` pattern (15 s startup delay,
  `CalculateNextRunTime`, `_lastRunDate` day guard, `IServiceScopeFactory` for scoped
  deps). It is registered by `AddDatabaseBackupServices`, not `AddBackgroundServices`,
  because registration is conditional.

### Multi-instance behaviour

Every scheduler in the app guards only with an in-process `_lastRunDate`, so a scaled-out
App Service fires each instance independently. For backups that could race the rotation
and leave `previous.db` == `current.db` (the live data is never at risk — only the older
generation).

Two cheap guards are in place instead of a lease table: **random jitter** before each run
(`MaxJitterSeconds`, default 120) and a **same-day check** on `current.db`'s
`LastModified` — an instance that finds today's backup already present skips entirely.

If the plan is ever genuinely scaled out, replace both with a single-row compare-and-swap
lease in the shared SQLite file, which is a true leader election.

### Operational notes

- **`Always On` must be enabled** on the App Service, or the process recycles while idle
  and the schedule never fires. There is no catch-up: a process that is not alive at 03:30 UTC
  skips that day entirely (`CalculateNextRunTime`). Production `rtub` has Always On **on**
  (checked 2026-09-24); `current.db`'s Last-Modified is the thing to check.
- The daily backup is not the release restore point: it can be a day old and rotates away the
  previous state within ~48 h. Before migrating an existing database the app takes its own
  pre-migration snapshot (`PreMigrationSnapshot`, same online-backup and validation code as
  here) under `/home/site/data/backups/pre-migration/`, keeping five; see
  `docs/release-and-rollback.md` → *Database*.
- SQLite lives on the `/home` Azure Files share; the temp snapshot goes to
  `Path.GetTempPath()` (`/tmp`, ephemeral) so backups don't double persistent storage.
- Live database was ~18 MB when this was built, so a single `PutObject` is right. Past
  roughly 100 MB, switch `UploadFileAsync` to `TransferUtility` multipart.
- `MinSizeRatio` (default 0.5) rejects a snapshot under half the live database size. A
  truncated copy can still be internally consistent and pass `quick_check`, so size is a
  separate signal.

### Manual (non-repo) prerequisites

Create the backup bucket **private** — no public access, no `r2.dev` domain; it holds
every user record. Issue a backup-scoped API token separate from the media token. Then set
these Azure App Settings:

| Setting | Value |
|---|---|
| `DatabaseBackup__Enabled` | `true` |
| `DatabaseBackup__Bucket` | the private bucket name |
| `DatabaseBackup__ScheduledTime` | `03:30` (UTC) |
| `DatabaseBackup__AccountId` | optional — falls back to `Cloudflare__R2__AccountId` |
| `DatabaseBackup__AccessKeyId` | optional — falls back to the media key |
| `DatabaseBackup__SecretAccessKey` | optional — falls back to the media secret |

### Restoring

`current.db` is a plain SQLite file. Download it, run `PRAGMA quick_check;` locally, stop
the App Service, replace `app.db`, and delete any stale `app.db-wal` / `app.db-shm`
sidecars before starting up again. Step by step, including the pre-migration snapshots and which
app release to deploy afterwards: `docs/release-and-rollback.md` → *Restoring the database*.

## Refreshing Azure DEV from a sanitized production snapshot

Manual, opt-in refresh of `rtub-dev` from the latest production backup, with credentials
and push endpoints stripped on the way. Implemented by unit **029**.

**Production is never mutated.** The pipeline issues exactly one request against the
`rtub-db` bucket — a `GET` of `database/current.db` — and nothing else. It does not write
to the bucket, does not connect to the live production SQLite file, and does not touch the
production App Service.

```
rtub-db/database/current.db   (read-only GET, immutable input)
  -> $RUNNER_TEMP/snapshot.db        never opened read-write; fingerprinted before and after
  -> $RUNNER_TEMP/sanitized.db       a SEPARATE file: copy, then sanitize, then validate
  -> resolve the DEV file            from rtub-dev's own ConnectionStrings__SqliteConnection
  -> /home/site/data/<that file>     app stopped, rollback copy kept, sidecars removed
  -> start + poll /health + scripts/smoke-azure-dev.sh
```

### Source

`database/current.db` in the private `rtub-db` bucket — the object
`DatabaseBackupBackgroundService` promotes once a snapshot has passed `quick_check`, the
size-ratio check and an uploaded-byte-count check. `previous.db` and `incoming.db` are not
read. Nothing is written back.

The workflow's R2 credential must be a **dedicated read-only token scoped to `rtub-db`**.
It is deliberately not the credential the backup service runs with: that one can write, and
a read-only token makes "production is never mutated" a property of the credential rather
than of the code.

### Sanitization contract

`DatabaseSanitizer` (`src/RTUB.Application/Services/DatabaseSanitizer.cs`), driven by
`tools/RTUB.DbSanitizer`. The source is opened `SqliteOpenMode.ReadOnly`, copied, and only
the copy is written to. Running with `--source` equal to `--destination` is refused.

Before anything is copied, the source must:

| Check | |
|---|---|
| exist and be non-empty | a zero-byte file is a *valid empty* SQLite database, so size is its own check |
| open as SQLite | |
| `PRAGMA quick_check` | `ok` |
| carry `AspNetUsers`, `PushSubscriptions`, `__EFMigrationsHistory` | absence means it is not an RTUB database |
| contain at least one user | |

Then, in one transaction against the copy:

| | Change |
|---|---|
| **A** | every `PushSubscriptions` row deleted |
| **B** | `Email` = `{UserName}@rtub.pt`; `NormalizedEmail` = the `UpperInvariantLookupNormalizer` form of it |
| **C** | `PasswordHash` = `PasswordHasher<ApplicationUser>.HashPassword(…)` of the configured DEV password — computed per user, so every row has its own salt. No hash is hardcoded or copied between rows. `SecurityStamp` rotated, which is what invalidates any cookie minted against production. |
| **D** | everything else preserved. Real names, finances, events, rehearsals, inventory and the migration history all survive — realistic DEV testing is the point. |

A user with no usable `UserName` cannot have a DEV address derived for it, so it is
**stripped** rather than left alone: `Email`, `NormalizedEmail` and `PasswordHash` are set
to `NULL`. A null `PasswordHash` is an account that cannot sign in. Keeping the production
address would leak it into DEV.

`PRAGMA journal_mode=DELETE` is applied to the copy so the artifact carries no `-wal`/`-shm`
sidecar. `SqliteConnectionInterceptor` puts the database back into WAL on first use in the app.

After sanitizing, the copy must pass, or the run fails and the output file is deleted:

- `PRAGMA quick_check` = `ok`
- `PushSubscriptions` count == 0
- users still exist; `__EFMigrationsHistory` still populated
- every usable user carries the expected `@rtub.pt` email and the matching `NormalizedEmail`
- every usable user's stored hash verifies against the DEV password through `PasswordHasher`
- **no** user still carries the password hash it had in the snapshot
- the source's SHA-256 and length are unchanged from before the run

**Fails closed throughout.** A missing DEV password is rejected before the destination is so
much as opened, and any failed check deletes the destination, so a half-sanitized database
cannot be shipped.

**Never logged**: the DEV password, any password hash, the R2 credentials, any production
email address, or any other row content. Failure messages identify a bad row by its
`AspNetUsers.Id` only.

### Why not reuse `SeedData.ResetDevDataAsync`

It applies the same contract, and it stays — see below. But it runs through EF Core and
`UserManager`, which requires the database's schema to match the **current** model. A
production snapshot is normally a few migrations behind `dev`, so an EF query against it
throws `no such column` before it sanitizes anything. `DatabaseSanitizer` goes through raw
ADO.NET and touches only columns that have existed since the initial migration, so it works
against an older schema; the app's own startup migration then brings that schema forward.

### Relationship to `DevelopmentDataReset` and to `SeedData`

Three different things, all of which stay:

| | What it produces | When |
|---|---|---|
| `SeedData` full dataset (`SeedData:SeedFullDataset`) | a **synthetic** DEV database: invented members from `SeedData:MemberPassword` | fresh/empty database, first startup |
| `DevelopmentDataReset` (`DevelopmentDataReset:Enabled`) | sanitizes **in place, at startup**, in Development or Staging | opt-in, one deliberate reset |
| Unit 029 refresh (this section) | **real production data**, sanitized before it ever reaches Azure | manual workflow run |

After a 029 refresh, `DevelopmentDataReset__Enabled` should stay **off** on `rtub-dev`: the
database arrives already sanitized, and leaving the switch on would re-hash every user on
every cold start of an App Service that sleeps after 20 idle minutes. It is not removed — it
is still the only way to sanitize a synthetic DEV database, or to re-sanitize one in place
without a workflow run. Nothing in the workflow reads or writes App Service settings, so
this is an owner action, once.

### Replacing the DEV database

**The file is never named in the workflow.** Which SQLite file `rtub-dev` opens is decided by
its own `ConnectionStrings__SqliteConnection` app setting, so that is what the workflow reads,
after the OIDC login and **before** anything is stopped. Below, `<db>` is whatever that setting
resolves to.

> The first live refresh is why. The workflow hardcoded `site/data/rtub-dev.db`, but `rtub-dev`
> had meanwhile been re-pointed at a fresh file (`rtub-dev-v3.db`, per unit 027's redeploy
> checklist). It backed up, replaced and cleaned the sidecars of an empty 4 KB database the app
> never opened, and the real one was untouched.

`scripts/resolve-dev-db-path.sh` validates the setting and fails closed. It accepts exactly
`Data Source=/home/site/data/<file>.db`:

| Refused | Why |
|---|---|
| setting missing or empty | nothing to resolve; the app itself would fall back to a relative `Data Source=app.db` (`Program.cs`), outside `/home/site/data` |
| any second keyword (`Mode`, `Cache`, `Password`, …) or two trailing `;` | `Mode=Memory` means no file; `Password` means the unencrypted sanitized copy cannot be opened |
| keyword other than `Data Source` (incl. the `DataSource`/`Filename` aliases), or no `=` at all | not the one accepted shape; also rejects SQL Server strings, `:memory:` and bare paths |
| path not under `/home/site/data/` — relative, `wwwroot`, look-alike or differently-cased directory, `file:` URI, quoted | the only durable directory `rtub-dev` keeps databases in |
| any `/` or `..` after `/home/site/data/` | no subdirectory, no traversal |
| name not matching `[A-Za-z0-9][A-Za-z0-9._-]*\.db` | no hidden file, space, `%`, CR or newline; a sidecar (`.db-wal`) or `.rollback` copy is not a database |

That allow-list is also what makes the result safe to append to `$GITHUB_ENV`: no newline can
smuggle in a second variable. The value is passed to the script through the environment, never
argv; the connection string is masked and never printed, and no rejection message echoes any part
of it. Only the validated path is logged.

Two further refusals happen in the workflow itself: the `az` read must succeed, and a
`SqliteConnection` entry on the **Connection strings** blade must not also exist — it reaches the
app as the same configuration key, and which of the two wins is undefined. Only a count is read
back for that check.

The validated `/home/site/data/<db>` is exported once as the Kudu VFS path `site/data/<db>`
(`DEV_DB_PATH`, through `$GITHUB_ENV`). There is deliberately no `env:` default for it anywhere,
and both destructive steps refuse an unset or empty value before acquiring a token.
`tests/RTUB.Web.Tests/Deployment/RefreshDevDatabaseWorkflowTests.cs` pins all of this, and the
workflow re-runs the resolver's `--self-test` before trusting it.

1. `az webapp stop` — SQLite must not be open while the file is swapped, and stopping
   checkpoints whatever WAL the running app holds.
2. `GET` the current `<db>` through Kudu's VFS API and `PUT` it back as `<db>.rollback`, on
   the same share. Deliberately not a workflow artifact: it is a whole database.
3. `PUT` the sanitized file over `<db>`. This is the only destructive moment, and it is
   flagged before it runs so a partial write still triggers the rollback step.
4. `DELETE` `<db>-wal` and `<db>-shm`. They describe pages of a database that no longer
   exists; SQLite would otherwise try to recover them into the new file.
5. `az webapp start`, then poll `/health` (the first boot migrates the snapshot forward,
   which is slow and is the intended path), then `scripts/smoke-azure-dev.sh` with
   `SKIP_AZ=1`.

**Rollback.** If the replacement fails, the workflow `PUT`s `rollback.db` back over `<db>`,
clears the sidecars and starts the app again. By hand afterwards: `PUT`
`/home/site/data/<db>.rollback` over `<db>` through the same VFS endpoint, delete the
sidecars, restart. A `.rollback` file is left behind by every run and is overwritten by the
next one.

Re-pointing `rtub-dev` at a different file needs **no workflow change**: change the app setting,
and the next refresh follows it.

**Kudu VFS, not `/api/command`.** `PUT`/`GET`/`DELETE` on `…scm.azurewebsites.net/api/vfs/`
is the documented single-file API and invokes no shell on the App Service. `If-Match: *` is
required on write; `Expect:` is cleared because Kudu does not answer the 100-continue that
curl sends for a large body. Authentication is the OIDC managed identity's own ARM token
(`az account get-access-token`), masked in the log — no publish profile and no Basic Auth
publishing credential, which stay off on `rtub-dev`.

**Proven.** The first real `workflow_dispatch` run succeeded on 2026-09-22 (run
`35773286498`, from `dev`, which is the default branch and therefore where the workflow is
dispatchable). The `rtub-dev-deploy` identity's `Website Contributor` on the `rtub-dev` site
covered both Kudu VFS (`Microsoft.Web/sites/publish/Action`) and the settings read
(`Microsoft.Web/sites/config/list/Action`); no role was added. Should a later run ever return
401/403, grant the single missing action on the `rtub-dev` site - never broaden the identity to
resource-group or subscription `Contributor`.

### Required secret and variable NAMES

Names only. No value belongs in this repository, and none is created by this unit.

| Name | Kind | Holds |
|---|---|---|
| `R2_DB_ENDPOINT` | secret | `https://<account-id>.r2.cloudflarestorage.com`. A **secret**, not a variable: it embeds the Cloudflare account id. |
| `R2_DB_READONLY_ACCESS_KEY_ID` | secret | Read-only token scoped to `rtub-db` |
| `R2_DB_READONLY_SECRET_ACCESS_KEY` | secret | ditto |
| `DEV_DATABASE_PASSWORD` | secret | Password every DEV account is reset to |
| `AZURE_CLIENT_ID` / `AZURE_TENANT_ID` / `AZURE_SUBSCRIPTION_ID` | variables | Already present on the `development` environment from unit 027 |

The bucket (`rtub-db`) and key (`database/current.db`) are not secrets and are in the
workflow.

### Rules

- **Manual only.** `.github/workflows/refresh-dev-database.yml` (**Database • Refresh DEV from
  PROD** in the Actions sidebar) triggers on
  `workflow_dispatch` alone — no `push`, no `pull_request`, no `schedule` — and requires the
  word `REFRESH` to be typed as an input.
- **No production database, sanitized or otherwise, is ever committed.** `.gitignore`
  covers `*.db`, `*.db-wal` and `*.db-shm`; the workflow deletes its local copies on exit
  and uploads no database artifact.
- **Azure DEV still holds real personal data after sanitization.** Only credentials and push
  endpoints are removed; names, finances and history are deliberately kept. Treat `rtub-dev`
  access as production-equivalent, and never give it production R2 or backup credentials.

## Storage ownership: DEV running on a production snapshot

A DEV database refreshed from a sanitized production snapshot is full of **absolute production R2
URLs**, because the sanitizer preserves them byte-for-byte (see *Sanitization contract*). DEV must
keep reading them and must never write over or delete the objects behind them.

### The invariant

> **An environment may only delete or overwrite an object in its own bucket.**
>
> Read access to another environment's objects is allowed. Write and delete are not, whatever the
> database says.

Enforced by two independent mechanisms. Either one alone stops a production mutation; neither
relies on the credential being incapable of it.

| | Mechanism | Catches |
|---|---|---|
| 1 | `GuardAgainstProductionBucket` in `AddStorageServices` | The whole environment being pointed at the production bucket |
| 2 | `StorageOriginResolver` via `BaseCloudflareStorageService.ResolveDeletableKey` | An individual delete aimed at an object this environment does not own |

### 1. The bucket guard

`appsettings.json` commits `Cloudflare:R2:Bucket` as `rtub` — **the production bucket** — so an
environment that forgets to override it inherits production's bucket and every upload and delete
lands there. No per-object check can catch that: the bucket genuinely is the one configured.

`AddStorageServices` therefore refuses to build the container when **all** of these hold:

- the environment is not `Production`, **and**
- `Cloudflare:R2:Bucket` equals `Cloudflare:R2:ProductionBucket` (committed alongside it), **and**
- an R2 credential is actually configured.

The credential condition is what lets the integration-test host boot on the committed settings:
with no credential the S3 client cannot reach any bucket, so there is nothing to refuse. It is a
capability check rather than an allow-list of environment names — anything calling itself `Test`
while holding real credentials is still refused.

Production is never checked, so it cannot be stopped from starting by this guard.

### 2. The per-object ownership check

`StorageObjectOrigin` classifies a stored URL by comparing its normalized `scheme://host[:port]`
against the configured origins:

| Origin | Meaning | Delete |
|---|---|---|
| `CurrentEnvironment` | under `Cloudflare:R2:PublicUrl` | **permitted** |
| `ProductionReference` | under `Cloudflare:R2:ReferencePublicUrl` | refused |
| `External` | any other host | refused |
| `Unknown` | not an absolute http/https URL at all | refused — **fail closed** |

Every delete-by-URL path now calls `ResolveDeletableKey` instead of `ExtractObjectKeyFromUrl`.
The latter reads the URL's *path* and ignores its *host*, so on a cloned database it happily
returned a production key. A refusal logs at Warning (object key only, never the full URL — no
foreign host reaches the log) and the caller drops the database reference without issuing any
remote call.

Production is unaffected: its stored URLs sit under its own public origin, so they resolve to
`CurrentEnvironment` and `ResolveDeletableKey` returns exactly what `ExtractObjectKeyFromUrl`
always did. Production configures no reference origin at all.

`StorageOriginResolver` is built inside `BaseCloudflareStorageService` from the `IConfiguration`
it already receives, so **no constructor and no DI registration of the eleven media services
changed.**

### Replacing a production-backed file in DEV

1. the production object is left untouched — no delete is issued;
2. the replacement is uploaded to the DEV bucket;
3. the database row is updated to the DEV public URL.

The old production URL simply stops being referenced. Inherited URLs are **never rewritten** to
DEV URLs as a batch operation.

### Public media

Public files are served straight from their absolute URL and need no credential. The only thing
that had to change is the **CSP**: `img-src`/`media-src` previously admitted just
`Cloudflare:R2:PublicUrl`, so inherited production media was blocked in DEV.

`Cloudflare:R2:ReferencePublicUrl` adds **one exact origin**, to `img-src` and `media-src` only —
never to `script-src`, `connect-src`, `frame-src` or `default-src`. It goes through the same
`NormalizeOrigin` as every other configured source, so a wildcard, a non-http scheme, injected
header text or a bare host all contribute nothing rather than something permissive. It is dropped
when it duplicates the environment's own origin.

Production sets no such key, so its policy is byte-identical to what unit 025 shipped — pinned by
a test.

`CdnProxyController` needed no change: it takes a *path*, not a URL, and always prefixes the
current environment's `PublicUrl`, so it can only ever fetch from this environment's bucket.

### Private files

Pre-signed paths are the case where reading a production object genuinely needs a credential.
Three are affected, and the problem is real, not theoretical:

| | Stored as | Why DEV misses it |
|---|---|---|
| Documents | object **key**, `docs/{Environment}/…` | the key carries `Production`, which the DEV bucket does not hold |
| Album audio | derived key, `albums/{album}/{song}.mp3` | **no environment segment** — same key in both buckets |
| Lyric PDFs | derived key, `lyrics/{album}/{song}.pdf` | **no environment segment** |

`IReferenceStorageService` is the read-only path. It has **no** upload, delete, copy or move
member, and `ReferenceStorageService` deliberately does **not** derive from `BaseStorageService`
— that base carries `PutObjectAsync`, `DeleteObjectAsync`, `DeleteObjectsBatchAsync` and
`CopyObjectAsync`, and inheriting it would put all four on a type whose entire purpose is that it
cannot mutate production. It calls exactly two S3 operations, `GetObjectMetadata` and
`GetPreSignedURL` (always `HttpVerb.GET`), and there is no third.

Two gates keep it off: it refuses to configure itself when the environment **is Production**, and
it needs all four of `Cloudflare:R2:Reference:{AccountId,AccessKeyId,SecretAccessKey,Bucket}`.
Unconfigured it builds no S3 client at all and answers "not found" to everything.

Reads try the current bucket first and fall back to the reference. The normal storage services
keep read/write/delete against **this environment's bucket only** — nothing was given generic
write access to both buckets.

> The reference credential must be a **read-only token scoped to the production bucket**. Never
> the production application's own write token.

Receipts needed nothing: they are public URLs, covered by the CSP reference origin.

### Required configuration NAMES

Names only; no value belongs in this repository.

| Name | Where | Holds |
|---|---|---|
| `Cloudflare:R2:ProductionBucket` | committed (`appsettings.json`) | Production bucket name, for the guard to compare against. Not a secret — `Cloudflare:R2:Bucket` was already committed. |
| `Cloudflare:R2:ReferencePublicUrl` | DEV/Staging only | Production public base URL. Environment-specific, so it is configured on the App Service, never committed. |
| `Cloudflare:R2:Reference:AccountId` | DEV/Staging only | Production account id |
| `Cloudflare:R2:Reference:AccessKeyId` | DEV/Staging only | **Read-only** token scoped to the production bucket |
| `Cloudflare:R2:Reference:SecretAccessKey` | DEV/Staging only | ditto |
| `Cloudflare:R2:Reference:Bucket` | DEV/Staging only | Production bucket name |

`rtub-dev` must also carry its **own** `Cloudflare__R2__Bucket`; the bucket guard refuses to start
otherwise.

### Future contract — Owner Storage Maintenance page

**Not implemented.** Recorded here so it is built to the same invariant. It should reuse the
existing `BaseStorageService.ListObjectsAsync` and `DeleteObjectsBatchAsync`.

**Production**
- orphan scan compares production bucket objects against **production** database references;
- deletion may affect production objects only after explicit Owner confirmation.

**DEV**
- orphan scan compares DEV bucket objects against **DEV-owned** database references only — a
  reference is DEV-owned when `StorageOriginResolver.Resolve` returns `CurrentEnvironment`;
- inherited `ProductionReference` rows are **excluded from DEV orphan calculations entirely**.
  Counting them would report every production object as a DEV orphan;
- "Delete all DEV files" operates on the DEV bucket only, and must never enumerate or delete
  production as part of that action.
