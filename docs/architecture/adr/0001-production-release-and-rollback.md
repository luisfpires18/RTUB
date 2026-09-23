# 0001 — Production release, artifact archive and rollback

- **Status:** Accepted
- **Date:** 2026-09-22

## Context

Until unit 030, production (`rtub`, Azure App Service Linux, one Basic B1 instance, SQLite on the
`/home` share) was deployed from inside the CI job on every `master` push with a publish-profile
secret, a portable `dotnet publish`, no guards, no smoke test and no version. Nothing it deployed
was kept, so the only way back was to rebuild an old commit. DEV had already moved to OIDC, a
linux-x64 publish, deploy guards, run-from-package and a smoke test (units 027–029).

Constraints that shaped the choice:

- **The repository is public.** Workflow logs, workflow artifacts and GitHub Release assets are
  readable by anyone (artifacts by any signed-in user), and artifacts expire after at most 90 days.
- **B1 has no deployment slots**, so there is no swap-back.
- **App Service's own package store is not a release archive.** With `WEBSITE_RUN_FROM_PACKAGE=1`,
  Kudu keeps the last five deployed zips in `/home/data/SitePackages`, named by timestamp and purged
  on every deploy (observed on `rtub-dev`). Microsoft documents a `packagename.txt` that selects the
  running package, but on this Linux app there is none, so there is no supported way to select an
  older one. Kudu cannot redeploy a zip deployment from its history.
- **Migrations run at app startup** and cannot be reversed by redeploying an older build.

## Decision

1. **SemVer from one file.** The root `VERSION` (`MAJOR.MINOR.PATCH`) is the release version, stamped
   into every build by `Directory.Build.props`; the SDK appends the commit SHA. `GET /api/version`
   serves `{version, commit}`. Every push to `master` must raise it.
2. **Build once.** A production commit is published once (linux-x64, framework-dependent), into ONE
   zip; the deploy guards run on that zip. The build job holds no Azure token.
3. **Private, immutable archive.** That zip and its `release.json` go to a private Azure Blob
   container (Entra ID only, no shared key, time-based immutability) as `releases/<version>/`,
   create-only: a version can never point to other bytes or another commit.
4. **Deploy from the archive.** Deploy • PROD reads the zip back from the archive, verifies its
   SHA-256, deploys it, and smoke-tests that `/api/version` reports that version and commit.
   Rollback • PROD is the same fetch-verify-deploy-verify path for an older version, dispatched by
   hand, and never builds.
5. **Production OIDC.** A dedicated user-assigned managed identity with a federated credential for
   the GitHub `production` environment (restricted to `master`), holding Website Contributor on
   the `rtub` site only and Storage Blob Data Contributor on the archive container only. The
   publish profile and SCM basic auth are retired once this is proven.
6. **Run from package** on production too, for atomic deploys - adopted after the OIDC path is
   proven, by redeploying the current version through Rollback • PROD.
7. **No automatic rollback.** A failed smoke test fails the run and names the rollback command.
8. **Databases move forward only.** Migrations follow an N-1 (expand/contract) rule; the app takes a
   validated online-backup snapshot before migrating and refuses to migrate without one; restoring
   a database is a manual procedure. Rollback • PROD refuses a target that does not know every
   migration the live release applied unless explicitly overridden.

## Consequences

- Rolling back is naming a version: "roll back to 2.0.0". Anything archived can be redeployed,
  byte-identical, without a build, for at least the 180-day immutability period.
- One small new resource (a storage account) and one role assignment; no new secrets anywhere.
- `WEBSITE_RUN_FROM_PACKAGE=1` (local package) is a one-way door: Microsoft does not support moving
  such an app to a remote package URL. Not needed.
- A release whose deploy fails still consumes its version (it is archived first); the fix ships as
  the next PATCH.
- Rolling back across a contract migration is a database restore with data loss, by design.
- The five test suites test the commit, not the linux-x64 publish output; the production smoke test
  is what checks the exact bytes.
