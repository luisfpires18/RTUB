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
  and the schedule never fires.
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
sidecars before starting up again.
