# Cloudflare R2 and database backups

R2 storage layer, SQLite backups, the DEV refresh from production, and how DEV safely reads production
media. Companion to [backend-practices.md](backend-practices.md). Every setting per environment:
[configuration-runbook](configuration-runbook.md).

## R2 architecture

One bucket (`rtub`), one endpoint, one singleton `IAmazonS3` shared by all 11 storage services. R2 endpoints are
per-account, not per-bucket, so more buckets in the same account need only a different bucket name.

Registered in `ServiceCollectionExtensions.AddInfrastructureServices`:

```
ServiceURL          = https://{Cloudflare:R2:AccountId}.r2.cloudflarestorage.com
ForcePathStyle      = true
AuthenticationRegion= "auto"
Credentials         = BasicAWSCredentials(AccessKeyId, SecretAccessKey)
```

R2 quirks the codebase accounts for: `UseChunkEncoding = false` on every `PutObjectRequest`;
`DisablePayloadSigning = true` where the stream is non-seekable; no multipart/`TransferUtility` (all uploads are a
single `PutObject`).

| Layer | Type | Notes |
|---|---|---|
| Generic S3 | `Services/Storage/BaseStorageService<T>` | Any bucket name. Put/Delete/List/BatchDelete/Presign/ObjectExists/GetObjectSize/ExtractObjectKeyFromUrl/CopyObjectAsync/GetObjectLastModifiedUtcAsync. |
| R2 + public media | `Services/Storage/BaseCloudflareStorageService<T>` | Reads `Cloudflare:R2:Bucket`; `UploadMediaAsync` forces `PublicRead` + immutable cache-control. Not suitable for private objects. |
| Concrete | `Services/Cloudflare*StorageService.cs` (11) | Interfaces in `Interfaces/I*StorageService.cs`, DI in `AddStorageServices`. |

Access patterns:

- Public: objects written `PublicRead`; URL = `{Cloudflare:R2:PublicUrl}/{objectKey}`.
- Private-ish (documents, receipts): pre-signed URLs, `Storage:UrlExpirationMinutes` (default 60).
- CORS is not configured in code.
- Left in the bucket, read by nothing: `images/{env}/bets/`, `images/{env}/bet-comments/`, `item-configs/{env}/images/`
  (`docs/db-cleanup-inventory.md`).

### Object keys (single bucket, shared by all environments)

`{env}` = `IHostEnvironment.EnvironmentName`, so Development and Production coexist.

```
images/{env}/{entityType}/...          images/{env}/gallery/{photos|videos}/...
events/{env}/{images|videos}/...       songs/{env}/videos/...
naipes/{env}/{videos|images}/{instr}/  item-configs/{env}/images/{typeKey}/...
receipts/{env}/...                     albums/{album}/{song}.mp3     (no env segment)
lyrics/{album}/{song}.pdf  (no env)    documents folder tree (audit-logged CRUD)
```

### Configuration keys

Env-var form uses `__`. Only `Bucket` and `ProductionBucket` are committed; the rest are secrets (user-secrets
locally, Azure App Settings in DEV/PROD).

| Key | Committed? | Consumed by |
|---|---|---|
| `Cloudflare:R2:Bucket` (`"rtub"`) | `appsettings.json` | `BaseCloudflareStorageService` |
| `Cloudflare:R2:AccountId`, `AccessKeyId`, `SecretAccessKey` | secret | S3 client |
| `Cloudflare:R2:PublicUrl` | secret | 8 services |
| `Storage:UrlExpirationMinutes` | default 60 | Document/Receipt presign |
| `ConnectionStrings:SqliteConnection` | env only; falls back to `Data Source=app.db` | DbContextFactory |

IDrive is historical only: no code reads `IDrive:*`, and any `IDrive__*` App Service setting is dead
(`scripts/config/audit-azure-app-settings.ps1` warns about it).

### Account-migration constraint

Absolute public R2 URLs are persisted in the database (`ImageUrl`, `MediaUrl`, `VideoUrl`, `ReceiptUrl`,
`PictureUrl` on Album, ApplicationUser, Event, GalleryMedia, ...). Cloudflare has no cross-account bucket transfer,
and a new account issues a new `pub-<hash>.r2.dev` host, so any account move breaks every stored URL unless either:

- a custom domain (e.g. `cdn.rtub.pt`) is bound to the bucket and stored URLs are rewritten to it once (future moves
  are then DNS-only); preferred, or
- stored URLs are rewritten to the new `pub-*.r2.dev` on every move.

Object copy is `rclone sync old:rtub new:rtub` (or `aws s3 sync` across two endpoints) preserving keys exactly.
Keeping the bucket name `rtub` avoids editing committed config.

No Workers, Pages, `wrangler.toml` or DNS config exists in this repo; anything beyond R2 (zone for `rtub.pt`, WAF,
cache rules, email routing) must be checked in the dashboard. `https://rtub.pt` appears in ~15 services only as a
hardcoded app base-URL fallback.

## SQLite and WAL

PRAGMAs, timeouts and the no-`Cache=Shared` rule: `.claude/skills/rtub-sqlite/SKILL.md`. Because of WAL, backups
must never be a file copy of `app.db` / `-wal` / `-shm`.

## Database backup pipeline

Daily online snapshot via `Microsoft.Data.Sqlite`, uploaded to a private R2 bucket, retaining exactly two
generations.

```
live SQLite --SqliteConnection.BackupDatabase()--> temp file in Path.GetTempPath()
  -> validate: PRAGMA quick_check + schema-object count + size-ratio vs live db
  -> upload to  database/incoming.db   (private, no-store, size-verified against local)
  -> server-side copy  current.db -> previous.db   (skipped when absent = first run)
  -> server-side copy  incoming.db -> current.db ; delete incoming.db
  -> finally: delete temp file (success or failure)
```

The staging key is the safety property: a snapshot becomes `current.db` only after it passed validation and its
uploaded byte count matches the local file. Any earlier failure returns before touching stored objects, so a failed
or corrupt run never replaces the last known-good backup.

Code: `Configuration/DatabaseBackupOptions.cs` (`DatabaseBackup` section), `Interfaces/IDatabaseBackupStorageService.cs`,
`Services/Storage/DatabaseBackupStorageService.cs`, `Services/DatabaseBackupBackgroundService.cs` (scheduler,
snapshot, validation, rotation), `AddDatabaseBackupServices` in `Extensions/ServiceCollectionExtensions.cs`.

Design decisions:

- `DatabaseBackupStorageService` derives from `BaseStorageService<T>`, not `BaseCloudflareStorageService<T>`: the
  latter reads the media bucket name and its upload helper forces `PublicRead` plus a year-long immutable cache header.
- Its S3 client is a keyed singleton (`"R2Backup"`), separate from the media client, so the backup can use a token
  scoped to the backup bucket alone. Credentials fall back to the `Cloudflare:R2:*` values when no dedicated ones are set.
- Rotation is two server-side `CopyObject` calls; the snapshot is uploaded once.
- Nothing is registered when `DatabaseBackup:Enabled` is false, so DEV and test can never write to the backup bucket.
- The scheduler follows the `BackgroundService` pattern (15 s startup delay, `CalculateNextRunTime`, `_lastRunDate`
  day guard, `IServiceScopeFactory`). It is registered by `AddDatabaseBackupServices`, not `AddBackgroundServices`,
  because registration is conditional.
- `MinSizeRatio` (default 0.5) rejects a snapshot under half the live database size: a truncated copy can still be
  internally consistent and pass `quick_check`.
- The live database is ~18 MB, so a single `PutObject` is right. Past roughly 100 MB, switch `UploadFileAsync` to
  `TransferUtility` multipart.

Multi-instance behavior: schedulers guard only with an in-process `_lastRunDate`, so a scaled-out App Service fires
every instance. For backups that could race the rotation and leave `previous.db` equal to `current.db` (live data
is never at risk). Two cheap guards instead of a lease table: random jitter before each run (`MaxJitterSeconds`,
default 120) and a same-day check on `current.db`'s `LastModified` (an instance that finds today's backup skips). If
the plan is ever genuinely scaled out, replace both with a single-row compare-and-swap lease in the shared SQLite file.

Operational notes:

- Always On must be enabled on the App Service, or the process recycles while idle and the schedule never fires.
  There is no catch-up: a process not alive at 03:30 UTC skips that day. Production `rtub` has Always On off, so
  backups may be silently skipped; enabling it is an owner action, and `current.db`'s Last-Modified is the thing to
  check.
- The daily backup is not the release restore point: it can be a day old and rotates away the previous state within
  ~48 h. Before migrating an existing database the app takes its own pre-migration snapshot (`PreMigrationSnapshot`,
  same backup and validation code) under `/home/site/data/backups/pre-migration/`; see
  [release-and-rollback](release-and-rollback.md) (Database).
- SQLite lives on the `/home` Azure Files share; the temp snapshot goes to `Path.GetTempPath()` (`/tmp`, ephemeral).

Setup (outside the repo): create the backup bucket private (no public access, no `r2.dev` domain; it holds every user
record), issue a backup-scoped API token separate from the media token, then set `DatabaseBackup__Enabled=true`,
`DatabaseBackup__Bucket`, `DatabaseBackup__ScheduledTime` (`03:30`, UTC) and optionally `DatabaseBackup__AccountId`,
`DatabaseBackup__AccessKeyId`, `DatabaseBackup__SecretAccessKey` (each falls back to the media value).

Restoring: `current.db` is a plain SQLite file. Download it, run `PRAGMA quick_check;`, stop the App Service, replace
`app.db`, delete stale `app.db-wal` / `app.db-shm`, start. Step by step, including pre-migration snapshots and which
release to deploy afterwards: [release-and-rollback](release-and-rollback.md) (Restoring the database).

## Refreshing Azure DEV from a sanitized production snapshot

Manual, opt-in refresh of `rtub-dev` from the latest production backup, with credentials and push endpoints stripped
on the way.

Production is never mutated. The pipeline issues exactly one request against the `rtub-db` bucket, a `GET` of
`database/current.db`. It does not write to the bucket, connect to the live production SQLite file, or touch the
production App Service.

```
rtub-db/database/current.db   (read-only GET, immutable input)
  -> $RUNNER_TEMP/snapshot.db        never opened read-write; fingerprinted before and after
  -> $RUNNER_TEMP/sanitized.db       a SEPARATE file: copy, then sanitize, then validate
  -> resolve the DEV file            from rtub-dev's own ConnectionStrings__SqliteConnection
  -> /home/site/data/<that file>     app stopped, rollback copy kept, sidecars removed
  -> start + poll /health + scripts/smoke-azure-dev.sh
```

Source: `database/current.db` in the private `rtub-db` bucket (the object `DatabaseBackupBackgroundService` promotes
after validation). `previous.db` and `incoming.db` are not read. The workflow's R2 credential must be a dedicated
read-only token scoped to `rtub-db`, deliberately not the backup service's writable one, so "production is never
mutated" is a property of the credential rather than of the code.

### Sanitization contract

`DatabaseSanitizer` (`src/RTUB.Application/Services/DatabaseSanitizer.cs`), driven by `tools/RTUB.DbSanitizer`. The
source is opened `SqliteOpenMode.ReadOnly`, copied, and only the copy is written to. `--source` equal to
`--destination` is refused.

Before anything is copied, the source must exist and be non-empty (a zero-byte file is a valid empty SQLite
database), open as SQLite, pass `PRAGMA quick_check` = `ok`, carry `AspNetUsers`, `PushSubscriptions` and
`__EFMigrationsHistory` (absence means it is not an RTUB database), and contain at least one user.

Then, in one transaction against the copy:

| | Change |
|---|---|
| A | every `PushSubscriptions` row deleted |
| B | `Email` = `{UserName}@rtub.pt`; `NormalizedEmail` = the `UpperInvariantLookupNormalizer` form of it |
| C | `PasswordHash` = `PasswordHasher<ApplicationUser>.HashPassword(...)` of the configured DEV password, computed per user (own salt per row; no hash hardcoded or copied between rows). `SecurityStamp` rotated, which invalidates any cookie minted against production. |
| D | everything else preserved: real names, finances, events, rehearsals, inventory and migration history survive, so DEV testing is realistic. |

A user with no usable `UserName` cannot get a DEV address, so it is stripped: `Email`, `NormalizedEmail` and
`PasswordHash` set to `NULL` (an account that cannot sign in). Keeping the production address would leak it into DEV.

`PRAGMA journal_mode=DELETE` is applied to the copy so the artifact carries no `-wal`/`-shm`; the app's
`SqliteConnectionInterceptor` restores WAL on first use.

After sanitizing, the copy must pass or the run fails and the output file is deleted: `quick_check` = `ok`;
`PushSubscriptions` count 0; users and `__EFMigrationsHistory` still populated; every usable user has the expected
`@rtub.pt` email and matching `NormalizedEmail`; every usable user's hash verifies against the DEV password; no user
still carries its snapshot hash; the source's SHA-256 and length are unchanged.

Fails closed throughout: a missing DEV password is rejected before the destination is opened, and any failed check
deletes the destination. Never logged: the DEV password, any password hash, R2 credentials, any production email, any
other row content. Failure messages identify a bad row by `AspNetUsers.Id` only.

Why not reuse `SeedData.ResetDevDataAsync`: it applies the same contract, but runs through EF Core and `UserManager`,
which require the schema to match the current model. A production snapshot is normally a few migrations behind
`dev`, so an EF query throws `no such column` first. `DatabaseSanitizer` uses raw ADO.NET on columns that exist since
the initial migration; the app's startup migration then brings the schema forward.

### Seed, reset and refresh

| | What it produces | When |
|---|---|---|
| `SeedData` full dataset (`SeedData:SeedFullDataset`) | a synthetic DEV database (invented members from `SeedData:MemberPassword`) | fresh/empty database, first startup |
| `DevelopmentDataReset` (`DevelopmentDataReset:Enabled`) | sanitizes in place at startup, in Development or Staging | opt-in, one deliberate reset |
| Refresh (this section) | real production data, sanitized before it reaches Azure | manual workflow run |

After a refresh keep `DevelopmentDataReset__Enabled` off on `rtub-dev`: the database arrives sanitized, and leaving it
on would re-hash every user on every cold start of an App Service that sleeps after 20 idle minutes. It stays for
sanitizing a synthetic DEV database or re-sanitizing in place. The workflow never touches App Service settings, so
turning it off is an owner action.

### Replacing the DEV database

The file is never named in the workflow. Which SQLite file `rtub-dev` opens is decided by its own
`ConnectionStrings__SqliteConnection`, so the workflow reads that after the OIDC login and before anything is
stopped (`<db>` below). Hardcoding it once replaced a file the app never opened.

`scripts/resolve-dev-db-path.sh` validates the setting and fails closed. It accepts exactly
`Data Source=/home/site/data/<file>.db`:

| Refused | Why |
|---|---|
| setting missing or empty | nothing to resolve (the app itself would fall back to a relative `Data Source=app.db`) |
| any second keyword (`Mode`, `Cache`, `Password`, ...) or two trailing `;` | `Mode=Memory` means no file; `Password` means the sanitized copy cannot be opened |
| keyword other than `Data Source` (incl. `DataSource`/`Filename` aliases), or no `=` | not the one accepted shape; also rejects SQL Server strings, `:memory:`, bare paths |
| path not under `/home/site/data/` (relative, `wwwroot`, look-alike or differently-cased directory, `file:` URI, quoted) | the only durable directory `rtub-dev` keeps databases in |
| any `/` or `..` after `/home/site/data/` | no subdirectory, no traversal |
| name not matching `[A-Za-z0-9][A-Za-z0-9._-]*\.db` | no hidden file, space, `%`, CR or newline; a sidecar (`.db-wal`) or `.rollback` copy is not a database |

The allow-list is also what makes the result safe to append to `$GITHUB_ENV` (no newline can smuggle in a second
variable). The value reaches the script through the environment, never argv; the connection string is masked and no
rejection message echoes any part of it. Two further refusals in the workflow: the `az` read must succeed, and no
`SqliteConnection` entry may exist on the Connection strings blade (same configuration key, undefined winner).

The validated path is exported once as the Kudu VFS path `site/data/<db>` (`DEV_DB_PATH`, via `$GITHUB_ENV`), with no
`env:` default anywhere; both destructive steps refuse an unset or empty value before acquiring a token.
`tests/RTUB.Web.Tests/Deployment/RefreshDevDatabaseWorkflowTests.cs` pins this, and the workflow re-runs the
resolver's `--self-test` first.

1. `az webapp stop`: SQLite must not be open during the swap; stopping checkpoints the running app's WAL.
2. `GET` the current `<db>` through Kudu's VFS API and `PUT` it back as `<db>.rollback` on the same share (not a
   workflow artifact: it is a whole database).
3. `PUT` the sanitized file over `<db>`. The only destructive moment; flagged before it runs so a partial write
   still triggers the rollback step.
4. `DELETE` `<db>-wal` and `<db>-shm` (they describe pages of a database that no longer exists).
5. `az webapp start`, poll `/health` (first boot migrates the snapshot forward, which is slow and intended), then
   `scripts/smoke-azure-dev.sh` with `SKIP_AZ=1`.

Rollback: if the replacement fails, the workflow `PUT`s `rollback.db` back over `<db>`, clears the sidecars and starts
the app. By hand: `PUT` `/home/site/data/<db>.rollback` over `<db>` through the same VFS endpoint, delete the
sidecars, restart. Every run leaves a `.rollback` file, overwritten by the next. Re-pointing `rtub-dev` at another
file needs no workflow change.

Kudu VFS, not `/api/command`: `PUT`/`GET`/`DELETE` on `...scm.azurewebsites.net/api/vfs/` is the documented
single-file API and invokes no shell. `If-Match: *` is required on write; `Expect:` is cleared because Kudu does not
answer curl's 100-continue for a large body. Authentication is the OIDC managed identity's ARM token (`az account
get-access-token`), masked in the log; no publish profile or Basic Auth credential (both off on `rtub-dev`). The
`rtub-dev-deploy` identity's Website Contributor on the `rtub-dev` site covers both Kudu VFS
(`Microsoft.Web/sites/publish/Action`) and the settings read (`Microsoft.Web/sites/config/list/Action`). On a later
401/403, grant the single missing action on the `rtub-dev` site, never resource-group or subscription Contributor.

Names only (no value belongs in this repository):

| Name | Kind | Holds |
|---|---|---|
| `R2_DB_ENDPOINT` | secret | `https://<account-id>.r2.cloudflarestorage.com` (a secret: it embeds the account id) |
| `R2_DB_READONLY_ACCESS_KEY_ID`, `R2_DB_READONLY_SECRET_ACCESS_KEY` | secrets | read-only token scoped to `rtub-db` |
| `DEV_DATABASE_PASSWORD` | secret | password every DEV account is reset to |
| `AZURE_CLIENT_ID` / `AZURE_TENANT_ID` / `AZURE_SUBSCRIPTION_ID` | variables | on the `development` environment |

The bucket (`rtub-db`) and key (`database/current.db`) are not secrets and are in the workflow.

Rules:

- Manual only. `.github/workflows/refresh-dev-database.yml` (Database • Refresh DEV from PROD) triggers on
  `workflow_dispatch` alone (no `push`, `pull_request` or `schedule`) and requires the word `REFRESH` typed as input.
- No production database, sanitized or not, is ever committed. `.gitignore` covers `*.db`, `*.db-wal`, `*.db-shm`;
  the workflow deletes its local copies on exit and uploads no database artifact.
- DEV still holds real personal data after sanitization (names, finances, history are kept). Treat `rtub-dev`
  access as production-equivalent, and never give it production R2 or backup credentials.

## Storage ownership: DEV running on a production snapshot

A refreshed DEV database is full of absolute production R2 URLs, because the sanitizer preserves them byte for byte.
DEV must keep reading them and must never write over or delete the objects behind them.

Invariant: an environment may only delete or overwrite an object in its own bucket. Read access to another
environment's objects is allowed; write and delete are not, whatever the database says. Two independent mechanisms
enforce it; either alone stops a production mutation, and neither relies on the credential being incapable of it.

| | Mechanism | Catches |
|---|---|---|
| 1 | `GuardAgainstProductionBucket` in `AddStorageServices` | the whole environment pointed at the production bucket |
| 2 | `StorageOriginResolver` via `BaseCloudflareStorageService.ResolveDeletableKey` | an individual delete aimed at an object this environment does not own |

### 1. The bucket guard

`appsettings.json` commits `Cloudflare:R2:Bucket` as `rtub`, the production bucket, so an environment that forgets to
override it would upload and delete in production; no per-object check can catch that. `AddStorageServices` refuses
to build the container when all hold: the environment is not `Production`; `Cloudflare:R2:Bucket` equals
`Cloudflare:R2:ProductionBucket` (committed alongside it); an R2 credential is configured. The credential condition
lets the integration-test host boot on committed settings (no credential, nothing reachable); it is a capability
check, not an allow-list of environment names, so anything calling itself `Test` with real credentials is still
refused. Production is never checked. `rtub-dev` must carry its own `Cloudflare__R2__Bucket`.

### 2. The per-object ownership check

`StorageObjectOrigin` classifies a stored URL by its normalized `scheme://host[:port]`:

| Origin | Meaning | Delete |
|---|---|---|
| `CurrentEnvironment` | under `Cloudflare:R2:PublicUrl` | permitted |
| `ProductionReference` | under `Cloudflare:R2:ReferencePublicUrl` | refused |
| `External` | any other host | refused |
| `Unknown` | not an absolute http/https URL | refused (fail closed) |

Every delete-by-URL path calls `ResolveDeletableKey`, not `ExtractObjectKeyFromUrl` (which reads the path and ignores
the host, so on a cloned database it returned a production key). A refusal logs at Warning (object key only, never
the full URL) and the caller drops the database reference without any remote call. Production is unaffected: its URLs
resolve to `CurrentEnvironment`, and it configures no reference origin. `StorageOriginResolver` is built inside
`BaseCloudflareStorageService` from the `IConfiguration` it already receives, so the eleven media services'
constructors and DI are unchanged.

Replacing a production-backed file in DEV: the production object is left untouched (no delete), the replacement is
uploaded to the DEV bucket, the row is updated to the DEV public URL. Inherited URLs are never batch-rewritten.

### Public media

Public files are served from their absolute URL and need no credential; only the CSP matters.
`Cloudflare:R2:ReferencePublicUrl` adds one exact origin to `img-src` and `media-src` only (never `script-src`,
`connect-src`, `frame-src`, `default-src`), through the same `NormalizeOrigin` as every other source, so a wildcard,
non-http scheme, injected header text or bare host contributes nothing. It is dropped when it duplicates the
environment's own origin. Production sets no such key; a test pins its policy as unchanged.

### Private files

Pre-signed paths are where reading a production object needs a credential:

| | Stored as | Why DEV misses it |
|---|---|---|
| Documents | object key, `docs/{Environment}/...` | the key carries `Production`, which the DEV bucket does not hold |
| Album audio | derived key, `albums/{album}/{song}.mp3` | no environment segment: same key in both buckets |
| Lyric PDFs | derived key, `lyrics/{album}/{song}.pdf` | no environment segment |

`IReferenceStorageService` is the read-only path. It has no upload, delete, copy or move member, and
`ReferenceStorageService` deliberately does not derive from `BaseStorageService` (inheriting would put
`PutObjectAsync`, `DeleteObjectAsync`, `DeleteObjectsBatchAsync` and `CopyObjectAsync` on a type whose purpose is
that it cannot mutate production). It calls exactly two S3 operations, `GetObjectMetadata` and `GetPreSignedURL`
(always `HttpVerb.GET`). It refuses to configure itself in Production and needs all four of
`Cloudflare:R2:Reference:{AccountId,AccessKeyId,SecretAccessKey,Bucket}`; unconfigured it builds no S3 client and
answers "not found". Reads try the current bucket first, then the reference; normal storage services keep
read/write/delete against this environment's bucket only. The reference credential must be a read-only token scoped
to the production bucket, never the production application's write token. Receipts are public URLs covered by the CSP
origin.

### Reference configuration names

| Name | Where | Holds |
|---|---|---|
| `Cloudflare:R2:ProductionBucket` | committed (`appsettings.json`) | production bucket name for the guard to compare against (not a secret) |
| `Cloudflare:R2:ReferencePublicUrl` | DEV/Staging App Service only | production public base URL |
| `Cloudflare:R2:Reference:AccountId`, `AccessKeyId`, `SecretAccessKey`, `Bucket` | DEV/Staging only | production account id; read-only token scoped to the production bucket; production bucket name |

## Follow-ups

- Owner Storage Maintenance page (not built): reuse `BaseStorageService.ListObjectsAsync` / `DeleteObjectsBatchAsync`
  and the same invariant. Production orphan scan compares production objects against production references, deletes
  only after explicit Owner confirmation. DEV compares DEV bucket objects against DEV-owned references only
  (`StorageOriginResolver.Resolve` = `CurrentEnvironment`), excludes inherited `ProductionReference` rows entirely,
  and "Delete all DEV files" never enumerates or deletes production.
