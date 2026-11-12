# Release, versioning and rollback

How RTUB gets to production, how versions are named, and how to go back. Setting and secret names
only; values live in Azure and GitHub. Decision record:
`docs/architecture/adr/0001-production-release-and-rollback.md`. Environments, identities and
workflows: [ci-cd-and-azure-environments](ci-cd-and-azure-environments.md).

## At a glance

| I want to | Do this |
| --- | --- |
| ship work to DEV | merge a PR into `dev`; Deploy • DEV runs by itself |
| release to production | bump `VERSION` on `dev`, PR `dev` to `master`, merge with Create a merge commit; Deploy • PROD runs by itself |
| after every production release | merge `master` back into `dev`, then move `dev`'s `VERSION` to the next unreleased version |
| know what production runs | `GET https://rtub.azurewebsites.net/api/version` returns `{"version":"2.0.0","commit":"<sha>"}` |
| roll production back | `gh workflow run rollback-prod.yml --ref master -f version=2.0.0 -f confirm=2.0.0` |
| list versions that can be rolled back to | run Rollback • PROD with dry run ticked |
| ship an urgent fix | see Hotfix |

## Branch model

```
feat/* fix/* chore/*  --PR-->  dev  --> Deploy • DEV   (rtub-dev, automatic)
                                |
                                +--PR, merge commit-->  master  --> Deploy • PROD  (rtub, automatic)
                                                          |
                     dev  <------- merge master back -----+   after EVERY production release,
                                                              then bump dev's VERSION

hotfix/*  --PR-->  master  --> Deploy • PROD  --> then master back into dev, bump dev's VERSION
```

- `dev` is the GitHub default and integration branch. Neither `dev` nor `master` is ever deleted.
- `master` is production. Every push to it is a production release.
- Merge `dev` to `master` with Create a merge commit, not squash or rebase: those rewrite the
  commits and `dev` would carry history `master` does not.
- After every production release (and every hotfix) merge `master` back into `dev`. The release
  merge commit exists only on `master`; the hotfix itself is only on `master`.
- Then, before normal development continues, move `dev`'s `VERSION` to the next unreleased version
  (below). On a `VERSION` conflict during the sync, take the higher one and bump it if it is not
  above what was just released.
- Branch names: `<type>/<NNN>/<slug>`, `NNN` from the global sequence (`CLAUDE.md`).

## Versions

The root `VERSION` file is the release version: SemVer `MAJOR.MINOR.PATCH`, one line, no `v`, no
prerelease, no build metadata.

| Bump | When | Example |
| --- | --- | --- |
| PATCH | fixes only | 2.0.0 to 2.0.1 |
| MINOR | backward-compatible features | 2.0.1 to 2.1.0 |
| MAJOR | breaking release (a contract migration, a removed feature, a changed URL scheme) | 2.1.0 to 3.0.0 |

- Every push to `master` needs a higher `VERSION` than the previous `master` commit. The PR check
  *VERSION is bumped* (in CI • Build & Test, PRs into `master` only) and the first job of Deploy •
  PROD both refuse otherwise. A version whose deploy failed is still spent (it may already be
  archived): the fix goes out as the next PATCH.
- A missing `VERSION` at the previous `master` commit counts as "no earlier release", but only when
  GitHub answers 404 for a commit that exists; any other read failure fails the check
  (`release.sh previous-version`).
- `Directory.Build.props` reads `VERSION` into every build and fails the build if it is not
  `MAJOR.MINOR.PATCH`. The SDK appends the full commit SHA, so `/api/version` reports both.
- DEV builds report `<VERSION>-dev.<run number>` and are never archived.
- A version never points to different bytes or a different commit: the archive is written once per
  version and refuses anything else.
- After a verified production deploy, Deploy • PROD tags the commit `v<version>` (best effort;
  rollback reads the archive, never tags).

### After a release: move `dev` to the next version

After every production release and its `master` to `dev` sync-back, bump `VERSION` on `dev` before
any other work merges into `dev`.

| Production just released | `dev`'s `VERSION` becomes |
| --- | --- |
| 2.0.0 | 2.0.1 by default |
| 2.0.0 | 2.1.0 if the next release is known to be MINOR |
| 2.0.0 | 3.0.0 if the next release is known to be MAJOR |

Never leave `dev` on the released number: `2.0.0-dev.<run>` is a prerelease of 2.0.0, SemVer ranks
it below 2.0.0, so newer DEV code would look older than production. After a hotfix, `dev` must be
above the hotfix version (hotfix 2.0.1 means `dev` goes to 2.0.2, unless it is already on 2.1.0 or
higher).

The bump is an ordinary PR into `dev`. The next release PR carries it, so *VERSION is bumped*
passes without a second bump, unless the release grew into a MINOR or MAJOR.

## Releasing to production

1. On `dev`, `VERSION` already holds the next version; raise it if this release is MINOR or MAJOR.
   Check the release's migrations against the N-1 rule below.
2. Optional: to announce the release to members, write `releases/<VERSION>.json` (see Release
   notes). `bash scripts/release.sh notes-check "$(cat VERSION)" releases` says whether it passes.
3. Open a PR `dev` to `master`. CI • Build & Test and *VERSION is bumped* (which also checks the
   notes) must pass.
4. Merge with Create a merge commit.
5. Watch Deploy • PROD. Its summary shows version, commit, artifact SHA-256 and the version that
   was live before.
6. Merge `master` back into `dev`, then bump `dev`'s `VERSION`.

## Release notes

After a release, every signed-in member sees "Nova atualização <version>" once (startup queue,
`portal/src/Startup.tsx`, before the notification reminder). "Entendido" stores a receipt per
member and version (`PendingActionDismissals`, kind `release.notes`, source = version), so it
follows the account across devices. The footer's "Ver novidades da versão" reopens the running
release's notes without touching the receipt. Visitors never get notes (`/api/releases/notes` is
401).

- Source: `releases/<VERSION>.json` at the repository root, written by hand from what was actually
  merged, pt-PT, user-facing ("Podes agora...", "Alterámos...", "Corrigimos..."). Never commit
  messages, technical, security or private details. Plain text only: no markup, no links. Fields:
  `version` (equal to the file name and `VERSION`), `title` (max 80 chars), `items` (1 to 12
  strings, each max 240 chars, one line). `scripts/release.sh notes-check` enforces this;
  `ReleaseNotesService` ignores a file that fails.
- Bundled, never fetched: `RTUB.csproj` copies `releases/*.json` into the publish output, so the
  one immutable zip carries its own notes. The running build reads only that folder, once per
  process (`ReleaseNotesService`, `AppContext.BaseDirectory/releases`). A stale service worker or
  cached frontend can only show the live build's own notes, and a rollback restores its own.
- Optional, validated when present: a release without a file ships and shows no dialog. When the
  file exists, `notes-check --optional` validates it on the PR into `master` and in the `version`
  job of Deploy • PROD, and the package step refuses a production zip that does not carry it.
- What a member sees: bundled notes with a version at or below the running build's and above the
  highest the member acknowledged, newest first, at most 5 per dialog (the running release first,
  the skipped ones under "Desde a última vez..."). SemVer comparison, never text. A receipt for a
  version newer than the running build (after a rollback) hides the dialog. The acknowledgement
  accepts only the running version (400 otherwise) and covers the older pending ones. A failed
  receipt is shown as an error and asked again next time; reading is never an acknowledgement.
- DEV: a `-dev.N` build hides its notes unless the App Setting `ReleaseNotes__ShowPrerelease` is
  `true`. It is `true` on `rtub-dev` and `rtub`; in PROD it is a no-op because a production build
  never carries a prerelease suffix.

## What Deploy • PROD does

```
version    VERSION is valid SemVer and higher than the previous master commit's
           (none there is the first versioned release);
           releases/<VERSION>.json, when present, passes notes-check
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
PROD uses, so every release exercises the rollback path. Nothing that deploys runs `dotnet publish`.

Zip guards (`scripts/release.sh verify`): `RTUB.dll`, deps and runtimeconfig present;
`libQuestPdfSkia.so` and `libe_sqlite3.so` present, non-empty and ELF64 x86-64; manifest, service
worker and offline page present; at least 200 files under `wwwroot/`; no backslash or
path-traversal entry names.

"Tested" means: every test suite runs on the exact commit; the one zip built from it is guarded,
archived and never rebuilt; the smoke test runs against those exact bytes in production. The
suites do not execute the linux-x64 publish output; the smoke test checks the bytes.

## The release archive

A private Azure Blob container, `releases`, in the storage account named by the `production`
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
| another commit | fails: bump `VERSION` |
| same commit, different bytes (the commit was rebuilt) | fails: redeploy the archived one with Rollback • PROD |
| a zip with no `release.json` (an interrupted run) | completed only if the stored zip has the same SHA-256 and commit, else fails |

Re-run failed jobs on a failed deploy reuses the same package. Re-run all jobs rebuilds, and the
archive then refuses the new bytes, as it should.

Why a storage account: GitHub artifacts and Release assets are public for this public repository
and artifacts expire after 90 days. App Service keeps only the last five run-from-package zips
under `/home/data/SitePackages`, timestamp-named, purged by Kudu, beside the app it would restore,
with no `packagename.txt` to select one on this Linux app. See the ADR.

## Rolling back

Rollback • PROD redeploys an archived release. It never builds (no .NET, no Node, no publish).

```bash
gh workflow run rollback-prod.yml --ref master -f version=2.0.0 -f confirm=2.0.0
```

(or Actions, Rollback • PROD, Run workflow, branch `master`.)

1. Checks: `version` is `MAJOR.MINOR.PATCH`, `confirm` repeats it, the run is on `master`.
2. Lists the archive, fetches the target, verifies SHA-256 and size.
3. Reads the live version and compares migrations. If the live release applied migrations the
   target does not know, or the live version cannot be determined, it refuses unless
   `allow_newer_schema` is ticked. Tick it only when every one of those migrations followed the
   N-1 rule; otherwise restore the database (below).
4. Dry run stops here: nothing deployed.
5. Deploys the archived zip and waits until `/api/version` reports the target version and commit,
   then runs the smoke contracts. Any failure fails the run.

Rolling back to the version already live is a plain redeploy.

## When a deploy fails

There is no automatic rollback. The run fails and its summary prints the Rollback • PROD command
for the version that was live before. Automatic rollback was rejected: production is one B1
instance with no slots, so a rollback is just another cold restart; migrations may already have
run; a slow cold start would make it flap; and going back is a judgement call.

Read the run log, then either roll back or fix forward with the next PATCH.

## Hotfix

1. Branch `hotfix/NNN/slug` from `master`; fix; bump `VERSION` (PATCH).
2. PR into `master`, merge with a merge commit; Deploy • PROD runs.
3. Merge `master` back into `dev` straight away, then make `dev`'s `VERSION` higher than the
   hotfix's (see After a release).

## Database

Production SQLite: `/home/site/data/app.db` (App Service setting
`ConnectionStrings__SqliteConnection`), on the persistent `/home` share. EF Core migrations run at
startup, before the app serves a request (`Program.cs`). Nothing in the pipeline reads, copies or
downloads the production database.

Before a production deployment:

- No migrations in the release (compare `newestMigration` in the release.json files): the deploy
  does not touch the schema. A known-good daily backup is still the safety net for data written by
  the new code: check `rtub-db/database/current.db` is from today. That backup only runs if the
  process is alive at 03:30 UTC, which needs Always On.
- Migrations in the release: they must follow the N-1 rule, and the app takes its own
  pre-migration snapshot at startup. Also check the daily backup.

### Pre-migration snapshot

When migrations are pending and the database already has migration history, `Program.cs` calls
`PreMigrationSnapshot.Take` before `MigrateAsync`:

- SQLite online backup API (`SqliteConnection.BackupDatabase`), never a file copy of a live WAL
  database, then converted to a single self-contained file (`journal_mode=DELETE`);
- validated (`PRAGMA quick_check`, schema-object count, size against the live file);
- written to `/home/site/data/backups/pre-migration/<utc>-before-<last pending migration>.db`
  (via `.partial` and a rename, so a crash never leaves something that looks like a restore point);
- the newest 5 are kept.

If the snapshot cannot be taken or validated, startup aborts and nothing is migrated: the deploy's
smoke test fails, and the previous release still matches the schema, so rolling back is safe. The
app never rolls a database back by itself.

### Migrations: the N-1 rule

Rolling production back across a contract (drop) migration is a full database restore from the
pre-migration backup, never an app-only rollback and never EF Down. Drop rules and the removals
already in production: `docs/db-cleanup-inventory.md`.

An app rollback never changes the schema: the older build starts against the newer schema, runs
none of the newer migrations and ignores their history rows. So every migration must work with the
release immediately before it (expand/contract):

| Fine in one release (expand) | Needs two releases (contract) |
| --- | --- |
| add a table | drop or rename a table |
| add a nullable column | drop or rename a column |
| add a column with a database default | make a column `NOT NULL` without a default |
| add a non-unique index | add a unique index or constraint over existing data |
| a backfill the previous code tolerates | change a column's type or meaning; renumber an enum; destructive data rewrites |

For a contract change: release N stops using the old shape (and still tolerates it); a later
release N+1 removes it. N+1 is a MAJOR bump, noted in its PR. Rolling back across a contract
release is not an app rollback (Rollback • PROD refuses it by default): it is a database restore
to a snapshot taken before N+1, losing everything written since.

SQLite cannot `ALTER` most things; EF Core rebuilds the table instead. A rebuild is only
expand-compatible if the rebuilt table keeps every column the previous release reads and writes.

### Restoring the database

Only when the data or schema itself must go back, never as part of an app rollback. Everything
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

The same VFS pattern is what Database • Refresh DEV from PROD automates for DEV
([cloudflare-r2-and-database-backups](cloudflare-r2-and-database-backups.md)).

## Break-glass: deploy an archived release by hand

Only if the workflows themselves are broken. With the owner's own Azure login:

```bash
az storage blob download --auth-mode login --account-name <archive account> \
  --container-name releases --name 2.0.0/rtub-2.0.0.zip --file rtub-2.0.0.zip
# compare with release.json's sha256 before going further
az webapp deploy -g rtub_group -n rtub --type zip --src-path rtub-2.0.0.zip
SKIP_AZ=1 APP=rtub EXPECT_VERSION=2.0.0 EXPECT_COMMIT=<sha from release.json> ./scripts/smoke-azure.sh
```
