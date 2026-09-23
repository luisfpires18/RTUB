# Release, versioning and rollback

How RTUB gets to production, how to name what is running, and how to go back. Setting and
secret **names** only; values live in Azure and GitHub. Decision record:
`docs/architecture/adr/0001-production-release-and-rollback.md`. Environments, identities and
workflows in detail: `docs/ci-cd-and-azure-environments.md`.

## At a glance

| I want to… | Do this |
| --- | --- |
| ship work to DEV | merge a PR into `dev` → **Deploy • DEV** runs by itself |
| release to production | bump `VERSION` on `dev`, PR `dev → master`, merge with **Create a merge commit** → **Deploy • PROD** runs by itself |
| after every production release | merge `master` back into `dev`, then move `dev`'s `VERSION` to the next unreleased version (2.0.0 released → `2.0.1`) |
| know what production runs | `GET https://rtub.azurewebsites.net/api/version` → `{"version":"2.0.0","commit":"<sha>"}` |
| roll production back to 2.0.0 | `gh workflow run rollback-prod.yml --ref master -f version=2.0.0 -f confirm=2.0.0` |
| see which versions can be rolled back to | run **Rollback • PROD** with *dry run* ticked |
| ship an urgent fix | `hotfix/NNN/slug` from `master` → PR into `master` (bump PATCH) → merge `master` back into `dev` → move `dev`'s `VERSION` past the hotfix |

## Branch model (permanent)

```
feat/* fix/* chore/*  ──PR──▶  dev  ──▶ Deploy • DEV   (rtub-dev, automatic)
                                │
                                └──PR, merge commit──▶  master  ──▶ Deploy • PROD  (rtub, automatic)
                                                          │
                     dev  ◀──────── merge master back ────┘   after EVERY production release,
                                                              then bump dev's VERSION (see Versions)

hotfix/*  ──PR──▶  master  ──▶ Deploy • PROD  ──▶ then master back into dev, and bump dev's VERSION
```

- **`dev`** is the GitHub default branch and the integration branch. Neither `dev` nor `master`
  is ever deleted.
- **`master`** is production. Every push to it is a production release.
- **Merge `dev → master` with "Create a merge commit"** - not squash, not rebase. Those rewrite
  the commits, and `dev` would then carry history `master` does not.
- **After every production release, merge `master` back into `dev`.** The release merge commit
  exists only on `master`; until it is merged back, `dev` is not a descendant of what production
  runs. The same applies after a hotfix, where it matters more: the fix itself is only on
  `master`.
- **Then, before normal development continues, move `dev`'s `VERSION` to the next unreleased
  version** (*After a release: move `dev` to the next version*, below). On a `VERSION` conflict
  during the sync, take the higher one - and bump it if it is not above what was just released.
- Branch names: `<type>/<NNN>/<slug>`, `NNN` from the global sequence (`CLAUDE.md`).

## Versions

**The root `VERSION` file is the release version** - Semantic Versioning `MAJOR.MINOR.PATCH`,
nothing else: no `v`, no prerelease, no build metadata, one line. It is what you say out loud:
"deploy 2.0.1", "production is on 2.0.1", "roll back to 2.0.0".

| Bump | When | Example |
| --- | --- | --- |
| PATCH | fixes only | 2.0.0 → 2.0.1 |
| MINOR | backward-compatible features | 2.0.1 → 2.1.0 |
| MAJOR | breaking release (a contract migration, a removed feature, a changed URL scheme) | 2.1.0 → 3.0.0 |

- **Every push to `master` needs a higher `VERSION` than the previous `master` commit.** The PR
  check *VERSION is bumped* (in **CI • Build & Test**, PRs into `master` only) and the first job
  of **Deploy • PROD** both refuse otherwise. A version whose deploy failed is still spent - it may
  already be archived - so the fix goes out as the next PATCH.
- `Directory.Build.props` reads `VERSION` into every build and fails the build if it is not
  `MAJOR.MINOR.PATCH`. The SDK appends the full commit SHA, so `/api/version` reports both.
- DEV builds report `<VERSION>-dev.<run number>` - a SemVer prerelease of the version `dev` is
  working towards - and are never archived.
- **A version never points to different bytes or a different commit.** The archive is written once
  per version and refuses anything else (below).
- After a verified production deploy, **Deploy • PROD** tags the commit `v<version>` (best effort -
  rollback never reads tags, it reads the archive).

### After a release: move `dev` to the next version

**After every successful production release and its `master → dev` sync-back, bump `VERSION` on
`dev` to the next unreleased version before any other work merges into `dev`.**

| Production just released | `dev`'s `VERSION` becomes | DEV then reports |
| --- | --- | --- |
| 2.0.0 | **2.0.1** by default | `2.0.1-dev.<run>` |
| 2.0.0 | 2.1.0, if the next release is already known to be MINOR | `2.1.0-dev.<run>` |
| 2.0.0 | 3.0.0, if the next release is already known to be MAJOR | `3.0.0-dev.<run>` |

Never leave `dev` on the released number. `2.0.0-dev.<run>` is a *prerelease of* 2.0.0, and SemVer
ranks every prerelease **below** its release: newer DEV code would look older than production.

The same applies after a hotfix: once `master` (say 2.0.1) is merged back, `dev`'s `VERSION` must be
above it - 2.0.2, unless `dev` is already on 2.1.0 or higher.

The bump is an ordinary PR into `dev`. It is what the next release PR will carry, so the
*VERSION is bumped* check passes without a second bump - unless the release grew into a MINOR or
MAJOR, in which case raise it again before the `dev → master` PR.

## Releasing to production

1. On `dev`: `VERSION` already holds the next version from the last sync-back; raise it (PR as
   usual) if this release turned out MINOR or MAJOR. Check the release's migrations against the
   N-1 rule below.
2. Open a PR `dev → master`. **CI • Build & Test** and *VERSION is bumped* must pass.
3. Merge it with **Create a merge commit**.
4. Watch **Deploy • PROD**. Its summary shows version, commit, artifact SHA-256 and the version that
   was live before.
5. Merge `master` back into `dev`.
6. Bump `dev`'s `VERSION` to the next unreleased version (2.0.0 released → `2.0.1`, or `2.1.0` /
   `3.0.0` when that is already the plan) before anything else merges into `dev`.

### What Deploy • PROD does

```
version    VERSION is valid SemVer and higher than the previous master commit's
ci         CI • Build & Test, on this exact commit
package    the ONLY build: dotnet publish -r linux-x64 --self-contained false (Node 22),
           ONE zip, guards run on that zip, release.json written. No Azure token in this job.
deploy     (environment production, OIDC, concurrency group "production")
           archive put  -> the zip + release.json, create-only
           archive get  -> read it BACK and verify sha256 + size
           schema gate  -> the live release's migrations must all be known to this one
           deploy       -> azure/webapps-deploy with that zip
           smoke        -> wait until /api/version = this version AND this commit, then
                           /health, /login, manifest, service worker (no CSP), CSP on HTML, still up
tag        v<version> on the commit (best effort)
```

The deploy job deploys the zip it read back from the archive, through the same fetch Rollback •
PROD uses - so every release exercises the rollback path. Nothing that deploys ever runs `dotnet
publish`.

**The zip guards** (`scripts/release.sh verify`): `RTUB.dll`, deps and runtimeconfig present;
`libQuestPdfSkia.so` and `libe_sqlite3.so` present, non-empty and ELF64 x86-64; manifest, service
worker and offline page present; at least 500 files under `wwwroot/`; no backslash or
path-traversal entry names.

**"Tested" means:** the five test suites run on the exact commit; the one zip built from it is
guarded, archived and never rebuilt; the smoke test runs against those exact bytes in production.
The suites do not execute the linux-x64 publish output itself - the smoke test is what checks the
bytes.

## The release archive

A private Azure Blob container, `releases` in the storage account named by the `production`
environment variable `RELEASE_STORAGE_ACCOUNT`. Microsoft Entra ID only (shared keys disabled), no
public access, 180-day time-based immutability: a blob cannot be overwritten or deleted inside its
retention period, by anyone.

```
releases/
  2.0.0/rtub-2.0.0.zip     blob metadata: version, commit, sha256
  2.0.0/release.json       written LAST - its presence means "archived"
```

`release.json`: `version`, `commit` (full), `shortCommit`, `artifact`, `sha256`, `sizeBytes`,
`githubRunId`, `builtAtUtc`, `versionEndpoint`, `migrations` (the `[Migration]` IDs EF Core
applies) and `newestMigration`.

`scripts/release-archive.sh put` is create-only:

| Already in the archive for this version | Result |
| --- | --- |
| nothing | archived |
| same commit, same SHA-256 (a re-run) | no-op, continues |
| another commit | **fails**: bump `VERSION` |
| same commit, different bytes (the commit was rebuilt) | **fails**: redeploy the archived one with Rollback • PROD |
| a zip with no `release.json` (an interrupted run) | completed only if the stored zip has the same SHA-256 and commit, else **fails** |

A re-run of a failed **deploy** job ("Re-run failed jobs") reuses the same package - it never
rebuilds. "Re-run all jobs" rebuilds, and the archive then refuses the new bytes, as it should.

Why a storage account and not what already exists: GitHub artifacts and Release assets are public
for this public repository and artifacts expire after 90 days; App Service keeps the last five
run-from-package zips under `/home/data/SitePackages`, timestamp-named, purged by Kudu, beside the
app it would restore, and on this Linux app with no `packagename.txt` to select one - no supported
way to "reselect" an old package. See the ADR.

## Rolling back

**Rollback • PROD** redeploys an archived release. It never builds - no .NET, no Node, no publish.

```bash
gh workflow run rollback-prod.yml --ref master -f version=2.0.0 -f confirm=2.0.0
```

(or Actions → **Rollback • PROD** → Run workflow → branch `master`.)

1. Checks: `version` is `MAJOR.MINOR.PATCH`, `confirm` repeats it, the run is on `master`.
2. Lists the archive, fetches the target, verifies SHA-256 and size.
3. Reads the live version and compares migrations: if the live release applied migrations the
   target does not know - or the live version cannot be determined - it **refuses** unless
   `allow_newer_schema` is ticked. Tick it only when every one of those migrations followed the
   N-1 rule. Otherwise do not roll the app back; restore the database (below).
4. *Dry run* stops here: nothing deployed. Use it to see what can be rolled back to.
5. Deploys the archived zip and waits until `/api/version` reports the target version **and**
   commit, then runs the smoke contracts. Any failure fails the run.

Rolling back to the version already live is a plain redeploy - that is how production is moved
onto run-from-package.

**1.0.0** is meant to be the pre-030 production build (commit `bc8d33ba`), captured from the
running app and archived by hand before the first 030 release. It predates `/api/version`, so its
`release.json` says `"versionEndpoint": false` and the smoke test waits for `/api/version` to
return 404 instead.

### One-time: archive the running pre-030 build as 1.0.0

Before the first 030 release, so 2.0.0 has something exact to roll back to. Read-only on
production. Needs the archive to exist and, temporarily, your own *Storage Blob Data
Contributor* on the `releases` container (remove it afterwards).

```bash
export RELEASE_STORAGE_ACCOUNT=<archive account>
token=$(az account get-access-token --query accessToken -o tsv)
# the files the running app serves, zipped by Kudu - nothing is rebuilt
curl -sS --fail -H "Authorization: Bearer $token" \
  -o rtub-1.0.0.zip https://rtub.scm.azurewebsites.net/api/zip/site/wwwroot/
unset token

bash scripts/release.sh verify rtub-1.0.0.zip        # natives, PWA assets, wwwroot, entry names
# migrations: identical IDs on master and dev at 030 (only AddMentorField's body changed)
RELEASE_VERSION_ENDPOINT=false bash scripts/release.sh manifest rtub-1.0.0.zip 1.0.0 \
  "$(git rev-parse bc8d33ba)" manual src/RTUB.Web/Migrations release.json
bash scripts/release-archive.sh put 1.0.0 rtub-1.0.0.zip release.json
bash scripts/release-archive.sh get 1.0.0 ./check      # reads it back and verifies
```

If `verify` refuses the capture, do not archive it: the first release then has no archived
predecessor and a failure is fixed forward.

## When a deploy fails

There is **no automatic rollback**. The run fails and its summary prints the Rollback • PROD command
for the version that was live before. Automatic rollback was rejected: production is one B1
instance with no slots, so a rollback is just another cold restart; migrations may already have
run; a slow cold start would make it flap; and whether to go back is a judgement call.

Then: read the run log, and either roll back, or fix forward with the next PATCH version.

## Hotfix

1. Branch `hotfix/NNN/slug` from `master`; fix; bump `VERSION` (PATCH).
2. PR into `master`, merge with a merge commit → Deploy • PROD.
3. **Merge `master` back into `dev`** straight away.
4. Make `dev`'s `VERSION` higher than the hotfix's: production 2.0.0, hotfix released as 2.0.1 →
   `dev` goes to 2.0.2 (or stays on 2.1.0 / 3.0.0 if it is already there).

## Database

Production SQLite: `/home/site/data/app.db` (App Service setting
`ConnectionStrings__SqliteConnection`), on the persistent `/home` share. EF Core migrations run at
startup, before the app serves a request (`Program.cs`).

### Before a production deployment

- **No migrations in the release** (compare `newestMigration` in the release.json files): the deploy
  does not touch the schema. A known-good daily backup is still the safety net for data written by
  the new code - check `rtub-db/database/current.db` is from today. That backup only runs if the
  process is alive at 03:30 UTC, which needs **Always On**.
- **Migrations in the release**: they must follow the N-1 rule, and the app takes its own
  pre-migration snapshot at startup. Also check the daily backup as above.

Nothing in the pipeline ever reads, copies or downloads the production database.

### Pre-migration snapshot

When migrations are pending and the database already has migration history, `Program.cs` calls
`PreMigrationSnapshot.Take` before `MigrateAsync`:

- SQLite online backup API (`SqliteConnection.BackupDatabase`) - never a file copy of a live WAL
  database - then converted to a single self-contained file (`journal_mode=DELETE`);
- validated (`PRAGMA quick_check`, schema-object count, size against the live file);
- written to `/home/site/data/backups/pre-migration/<utc>-before-<last pending migration>.db`
  (via `.partial` and a rename, so a crash never leaves something that looks like a restore point);
- the newest **5** are kept.

**If the snapshot cannot be taken or validated, startup aborts and nothing is migrated** - the
deploy's smoke test fails, and the previous release still matches the schema, so rolling back is
safe. The app never rolls a database back by itself.

### Migrations: the N-1 rule

An app rollback never changes the schema: the older build starts against the newer schema, runs
none of the newer migrations and ignores their history rows. So **every migration must work with
the release immediately before it** (expand/contract):

| Fine in one release (expand) | Needs two releases (contract) |
| --- | --- |
| add a table | drop or rename a table |
| add a nullable column | drop or rename a column |
| add a column with a database default | make a column `NOT NULL` without a default |
| add a non-unique index | add a unique index or constraint over existing data |
| a backfill the previous code tolerates | change a column's type or meaning; renumber an enum; destructive data rewrites |

For a contract change: release N stops using the old shape (and still tolerates it); a later
release N+1 removes it. N+1 is a MAJOR bump, noted in its PR. **Rolling back across a contract
release is not an app rollback** - Rollback • PROD will refuse it by default - it is a database
restore to a snapshot taken before N+1, losing everything written since.

SQLite cannot `ALTER` most things; EF Core rebuilds the table instead. A rebuild is only
expand-compatible if the rebuilt table keeps every column the previous release reads and writes.

### Restoring the database (manual)

Only when the data or schema itself must go back - never as part of an app rollback. Everything
written since the snapshot is lost.

1. Pick the source: the newest `/home/site/data/backups/pre-migration/*.db` taken before the bad
   release (exact pre-migration state), or `rtub-db/database/current.db` / `previous.db` (daily).
   Check it: `PRAGMA quick_check;` must say `ok`.
2. `az webapp stop -n rtub -g rtub_group`.
3. Through Kudu's VFS API (`https://rtub.scm.azurewebsites.net/api/vfs/`, bearer token from `az
   account get-access-token`, `If-Match: *`): keep a copy of the current file (`GET app.db`, `PUT
   app.db.rollback`), `PUT` the restore source over `site/data/app.db`, `DELETE`
   `site/data/app.db-wal` and `site/data/app.db-shm`. Leftover sidecars would replay pages of the
   old database into the restored one.
4. Deploy the release that matches the restored schema with Rollback • PROD, then
   `az webapp start` if the app is still stopped.
5. Check `/api/version`, `/health` and a login.

The same VFS pattern is what **Database • Refresh DEV from PROD** automates for DEV
(`docs/cloudflare-r2-and-database-backups.md`).

## Break-glass: deploy an archived release by hand

Only if the workflows themselves are broken. With the owner's own Azure login:

```bash
az storage blob download --auth-mode login --account-name <archive account> \
  --container-name releases --name 2.0.0/rtub-2.0.0.zip --file rtub-2.0.0.zip
# compare with release.json's sha256 before going further
az webapp deploy -g rtub_group -n rtub --type zip --src-path rtub-2.0.0.zip
SKIP_AZ=1 APP=rtub EXPECT_VERSION=2.0.0 EXPECT_COMMIT=<sha from release.json> ./scripts/smoke-azure.sh
```
