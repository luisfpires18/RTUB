# Configuration runbook

Which settings RTUB needs in each environment, where each one lives, and how to audit and fill them **without
ever printing or committing a value**. Task 039.

- **Manifest (source of truth for names):** `scripts/config/required-settings.json` — every setting with its status
  per environment (`required` / `optional` / `absent` = must not be set / `n/a`), `secret` flag, source, DEV-vs-PROD
  rule (`match` / `differ` / `any`), committed or code default, placeholder and notes.
- **Scripts:** `scripts/config/audit-local-user-secrets.ps1`, `scripts/config/audit-azure-app-settings.ps1`,
  `scripts/config/generate-vapid-keys.ps1` (Windows PowerShell 5.1 or PowerShell 7).
- **Outputs:** `.local/config-audit/` — git-ignored (`.local/` in `.gitignore`). The scripts refuse to write anywhere
  else inside the repository. Reports hold presence, length and a SHA-256 fingerprint prefix, never a value;
  templates hold placeholders only. Keep them local anyway (a fingerprint of a weak password can be brute-forced).

## Where settings live

| Source | Holds | Never holds |
| --- | --- | --- |
| Committed `appsettings.json` / `appsettings.Production.json` | non-secret defaults: schedulers, rate limits, `WebPush:Enabled`, `Toggles:MapEnabled`, `Cloudflare:R2:Bucket` / `ProductionBucket`, `DatabaseBackup:Enabled` (`false`; `true` in Production) | any secret |
| **user-secrets** (local, `UserSecretsId` of `src/RTUB.Web/RTUB.csproj`) | local secrets and overrides | — (outside the repo) |
| **Azure App Settings** (`rtub-dev`, `rtub`) | every DEV / PROD secret and environment value | — (names in docs, values in Azure only) |
| GitHub environment variables / secrets | deploy identity (`AZURE_*`), `RELEASE_STORAGE_ACCOUNT`, the DEV-refresh secrets | app settings (no workflow sets them) |

`appsettings.Development.json` is **not** git-ignored: keep secrets out of it. Configuration precedence (low → high):
`appsettings.json`, `appsettings.{Environment}.json`, user-secrets (Development only), environment variables / App
Settings. `:` in a key becomes `__` in an environment variable or App Setting (`WebPush:VapidPublicKey` →
`WebPush__VapidPublicKey`).

## What each environment needs

Full list and notes: the manifest. The ones that matter:

| Group | Local | DEV (`rtub-dev`, `Staging`) | PROD (`rtub`, `Production`) |
| --- | --- | --- | --- |
| Core: `ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_URLS` / `WEBSITES_PORT`, `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, `WEBSITE_RUN_FROM_PACKAGE=1` | launchSettings.json | required | required |
| `ConnectionStrings__SqliteConnection` | optional (falls back to `src/RTUB.Web/app.db`: use a scratch copy) | required, under `/home/site/data/` | required, `Data Source=/home/site/data/app.db` |
| `AdminUser__Password` | required on an empty database | required | only while the database has no users |
| `SeedData__SeedFullDataset` / `SeedData__MemberPassword` | optional | `true` / required | unset or `false` / **absent** |
| `Cloudflare__R2__AccountId`, `AccessKeyId`, `SecretAccessKey`, `Bucket`, `PublicUrl` | optional (storage routes 500 without) | **required**, DEV's own bucket and bucket-scoped token | **required** |
| `Cloudflare__R2__ReferencePublicUrl`, `Cloudflare__R2__Reference__*` | — | optional: read-only view of production media after a DB refresh (all four or none) | **absent** |
| `EmailSettings__*` (server, port, SSL, username, password, sender, sender name, recipient) | optional | optional (real mail from DEV reaches real people after a refresh) | **required** for real outgoing email |
| `WebPush__Enabled`, `WebPush__VapidSubject`, `WebPush__VapidPublicKey`, `WebPush__VapidPrivateKey` | optional (own pair to test push) | **required**, DEV's own pair | **required**, PROD's own pair |
| `Toggles__MapEnabled`, `Geocoding__DisabledInTests` | defaults | `true` / `false` (defaults) | `true` / `false` (defaults) |
| `DatabaseBackup__Enabled` + `Bucket` (+ optional scoped `AccountId` / `AccessKeyId` / `SecretAccessKey`) | off | **`false`**, nothing else | **`true`**, `Bucket=rtub-db` |
| `DevelopmentDataReset__*` | opt-in, one run | off (`false` or unset) | **absent** |

Why:

- **R2 is required for media and storage routes.** Events, music, gallery, governance, naipes, documentation and
  treasury resolve `IAmazonS3`; without R2 the app starts and `/health` is 200, but those routes answer 500. A
  non-production environment with R2 credentials and the production bucket name refuses to start.
- **Email** is needed only for real outgoing mail; without it sends are skipped, startup is unaffected.
- **Maps need no API key.** The members map uses Leaflet with CARTO basemap tiles in the browser; addresses are
  geocoded server-side by Nominatim (OpenStreetMap), cached in the database. `Geocoding__DisabledInTests=true` only
  for test hosts.
- **DatabaseBackup is PROD-only.** DEV must never write to `rtub-db` (and must never hold production R2 or backup
  credentials). The PROD backup also needs Always On.
- **DEV may carry reference R2 settings** after a database refresh (`docs/cloudflare-r2-and-database-backups.md`):
  production media resolve read-only through `Cloudflare__R2__ReferencePublicUrl` / `Reference__*`. Never on PROD.
- **IDrive is historical.** Nothing reads `IDrive:*`; any `IDrive__*` user-secret or App Setting is dead and both
  audits flag it for deletion.
- **No messaging table drops.** `Conversations`, `Messages`, `MessageReactions` and `ConversationUserSettings` are
  preserved for Messaging v2 (`docs/messages-removal.md`). No setting drops or disables them; the Azure audit flags
  any setting whose name suggests one.
- Settings marked `unused` in the manifest (`Gallery__MaxVideoSizeMB`, `ImageCaching__*`, `UseSqlServer`,
  `LoginPopup__*`) are committed but read by no code; the gallery limit the code reads is `GalleryMedia__MaxVideoSize`
  (bytes).

## Local user-secrets

```powershell
dotnet user-secrets list --project src/RTUB.Web/RTUB.csproj
```

That command prints values: run it only on your own screen. To set one:

```powershell
dotnet user-secrets set --project src/RTUB.Web/RTUB.csproj "AdminUser:Password" "<LOCAL_ADMIN_PASSWORD>"
```

```powershell
dotnet user-secrets set --project src/RTUB.Web/RTUB.csproj "Cloudflare:R2:SecretAccessKey" "<LOCAL_R2_SECRET_ACCESS_KEY>"
```

```powershell
dotnet user-secrets remove --project src/RTUB.Web/RTUB.csproj "IDrive:AccessKey"
```

### Audit local (redacted)

```powershell
powershell -ExecutionPolicy Bypass -File scripts/config/audit-local-user-secrets.ps1
```

Writes `.local/config-audit/local-user-secrets.audit.md` / `.json` (present / missing, source = user-secrets,
appsettings fallback or code default, value length, SHA-256 fingerprint prefix, secret flag, warnings such as a secret
in a tracked appsettings file or a deprecated `IDrive:*` key) and `set-local-user-secrets.template.ps1`
(`dotnet user-secrets set` lines for the missing settings with `<LOCAL_...>` placeholders; required ones active,
optional ones commented; a guard line stops it until edited).

## Azure App Settings

### Audit DEV and PROD (read-only, redacted)

Needs `az login` (with a current MFA session) on an account that can read both apps:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/config/audit-azure-app-settings.ps1
```

Parameters: `-ResourceGroup rtub_group`, `-DevApp rtub-dev`, `-ProdApp rtub`, `-OutputDirectory .local/config-audit`.
`-IncludeValues` exists only to refuse: exporting values is never needed. The script reads each app's settings with
`az webapp config appsettings list` into memory, changes nothing, and writes:

- `dev-app-settings.audit.md` / `.json`, `prod-app-settings.audit.md` / `.json`: per setting the requirement, state
  (present / default / missing), secret flag, slot setting, length, fingerprint, the DEV/PROD rule and whether the two
  apps hold the same value (compared on full hashes in memory), plus warnings:
  - missing required settings, with summary lines for incomplete `WebPush__Vapid*`, `Cloudflare__R2__*`,
    `EmailSettings__*` and core settings;
  - `ASPNETCORE_ENVIRONMENT` not `Staging` / `Production`; `WEBSITE_RUN_FROM_PACKAGE` not `1`;
    `ASPNETCORE_FORWARDEDHEADERS_ENABLED` not `true`;
  - PROD `DatabaseBackup__Enabled` not `true`, DEV `DatabaseBackup__Enabled` `true`;
  - `ConnectionStrings__SqliteConnection` outside `/home/site/data/`;
  - a `differ` setting shared by DEV and PROD (e.g. the same R2 secret, VAPID key or bucket — including both falling
    back to the production bucket), a `match` setting that differs;
  - a setting that must not be in that environment (`SeedData__MemberPassword`, `Cloudflare__R2__Reference__*`,
    `DatabaseBackup__*` credentials on DEV, `DevelopmentDataReset__*` on PROD);
  - deprecated names: `IDrive__*`, `Games__*`, `MyTunoScaling__*`, anything that looks like a messaging drop (never allowed);
  - settings in Azure that the manifest does not know (names only).
- `set-dev-app-settings.template.ps1`, `set-prod-app-settings.template.ps1`: see below.

**Proof it redacts:** values never leave the in-memory hashtables; reports carry `length` and
`sha256:<12 hex>` only; `ConfigurationAuditTests` runs both audits on synthetic settings holding sentinel secrets and
fails if a sentinel appears in any output file. Never run `az webapp config appsettings list` without `--query` in a
shared terminal or a workflow: it prints every value (the repository is public, so are its workflow logs).

### Set DEV / PROD (placeholders → your values)

Each template is a `$settings` array: lines that were missing or wrong at audit time are active, the rest are
commented out. Replace every `<...>`; the template refuses to run while one is left, and calls

```powershell
az webapp config appsettings set -g rtub_group -n rtub-dev --settings @settings --output none
```

(`--output none` keeps az from echoing every setting back). Setting App Settings restarts the app. Settings that must
go come as a commented `az webapp config appsettings delete ... --setting-names ...` line. The DEV baseline, as
the template writes it:

```powershell
$settings = @(
    'ASPNETCORE_ENVIRONMENT=Staging'
    'ASPNETCORE_FORWARDEDHEADERS_ENABLED=true'
    'WEBSITE_RUN_FROM_PACKAGE=1'
    'ConnectionStrings__SqliteConnection=Data Source=/home/site/data/<dev-file>.db'
    'Cloudflare__R2__AccountId=<DEV_R2_ACCOUNT_ID>'
    'Cloudflare__R2__AccessKeyId=<DEV_R2_ACCESS_KEY_ID>'
    'Cloudflare__R2__SecretAccessKey=<DEV_R2_SECRET_ACCESS_KEY>'
    'Cloudflare__R2__Bucket=<DEV_R2_BUCKET>'
    'Cloudflare__R2__PublicUrl=https://pub-<dev-hash>.r2.dev'
    'WebPush__Enabled=true'
    'WebPush__VapidSubject=mailto:<contact-address>'
    'WebPush__VapidPublicKey=<DEV_VAPID_PUBLIC_KEY>'
    'WebPush__VapidPrivateKey=<DEV_VAPID_PRIVATE_KEY>'
    'Toggles__MapEnabled=true'
    'Geocoding__DisabledInTests=false'
    'DatabaseBackup__Enabled=false'
)
az webapp config appsettings set -g rtub_group -n rtub-dev --settings @settings --output none
```

PROD, same shape (plus email; no seed, reference or reset settings):

```powershell
$settings = @(
    'ASPNETCORE_ENVIRONMENT=Production'
    'ASPNETCORE_FORWARDEDHEADERS_ENABLED=true'
    'WEBSITE_RUN_FROM_PACKAGE=1'
    'ConnectionStrings__SqliteConnection=Data Source=/home/site/data/app.db'
    'Cloudflare__R2__AccountId=<PROD_R2_ACCOUNT_ID>'
    'Cloudflare__R2__AccessKeyId=<PROD_R2_ACCESS_KEY_ID>'
    'Cloudflare__R2__SecretAccessKey=<PROD_R2_SECRET_ACCESS_KEY>'
    'Cloudflare__R2__Bucket=<PROD_R2_BUCKET>'
    'Cloudflare__R2__PublicUrl=https://pub-<prod-hash>.r2.dev'
    'EmailSettings__SmtpServer=smtp.gmail.com'
    'EmailSettings__SmtpPort=587'
    'EmailSettings__EnableSsl=true'
    'EmailSettings__SmtpUsername=<PROD_SMTP_USERNAME>'
    'EmailSettings__SmtpPassword=<PROD_SMTP_PASSWORD>'
    'EmailSettings__SenderEmail=<PROD_SENDER_EMAIL>'
    'EmailSettings__SenderName=RTUB'
    'EmailSettings__RecipientEmail=<PROD_RECIPIENT_EMAIL>'
    'WebPush__Enabled=true'
    'WebPush__VapidSubject=mailto:<contact-address>'
    'WebPush__VapidPublicKey=<PROD_VAPID_PUBLIC_KEY>'
    'WebPush__VapidPrivateKey=<PROD_VAPID_PRIVATE_KEY>'
    'Toggles__MapEnabled=true'
    'Geocoding__DisabledInTests=false'
    'DatabaseBackup__Enabled=true'
    'DatabaseBackup__Bucket=rtub-db'
)
az webapp config appsettings set -g rtub_group -n rtub --settings @settings --output none
```

PROD changes are owner actions: production workflows never set App Settings (`ReleaseWorkflowTests` pins that).

## VAPID keys

```powershell
powershell -ExecutionPolicy Bypass -File scripts/config/generate-vapid-keys.ps1
```

Prints `WebPush__VapidSubject=mailto:<contact-address>`, a base64url public key (65-byte uncompressed P-256 point) and
a base64url private key (32 bytes), using .NET's `ECDsa` (no Node, no network, no file written). Store them with
`dotnet user-secrets set` (local) or the template above (Azure); never in a tracked file, issue, PR or log.

**Rotation breaks existing subscriptions.** Every browser subscription is bound to the public key it was created
with (`applicationServerKey`); the push service rejects messages signed by another key. After a rotation every device
stops receiving pushes until its member turns push on again in `/profile` ("Ativar novamente"). Use one **stable pair
per environment**, separate for local, DEV and PROD (the Azure audit warns if DEV and PROD share one), and rotate PROD
only for a leaked key.

## Related

- `docs/ci-cd-and-azure-environments.md` → *App Service setting names* (DEV/PROD matrix).
- `docs/cloudflare-r2-and-database-backups.md` (R2, backups, DEV refresh and reference storage).
- `docs/react-push-v2.md` (push client), `.claude/skills/rtub-push/SKILL.md` (VAPID contract).
