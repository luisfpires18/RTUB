# Cloudflare Account Migration Runbook

Move the RTUB R2 storage from the personal Cloudflare account to the dedicated RTUB
account, with no downtime for users.

Background and architecture: [cloudflare-r2-and-database-backups.md](cloudflare-r2-and-database-backups.md).

## Scope

**Migrate:** the `rtub` bucket only — 471 objects, 1.28 GB.

**Do not touch** (personal account, unrelated): `wedding-album`, `nvf-media`,
`nvf-source-archive`. Every command below names the `rtub` bucket explicitly so there is
no path by which another bucket is read or written.

There is no custom domain, so the new account issues a new `pub-<hash>.r2.dev` public
host. Absolute media URLs are stored in the database, so a one-time URL rewrite is part of
the migration (step 7).

## Why this ordering has no downtime

The old bucket stays public and serving throughout. The app is switched to the new bucket
**before** the URLs are rewritten, so at every moment either the old URLs still resolve
(old bucket alive) or the new ones do. Nothing is deleted until the rollback window
closes.

---

## 1. Create the destination bucket

New RTUB account → R2 → Create bucket:

- Name: **`rtub`** — identical name means `Cloudflare:R2:Bucket` in `appsettings.json`
  needs no change.
- Location: same hint as the current bucket.

Then bucket → Settings → **Public Development URL** → Enable. Record the
`https://pub-XXXXXXXX.r2.dev` value; this is the new `PublicUrl`.

> R2 ignores per-object canned ACLs — public access is this bucket-level toggle. The
> `CannedACL.PublicRead` in the upload code is a no-op on R2, so there is nothing
> per-object to replicate.

Also record the new **Account ID** (R2 overview page, right sidebar).

## 2. Create API tokens

**Old account** → R2 → Manage API Tokens → Create: permission **Object Read only**,
scoped to the `rtub` bucket. Read-only makes it impossible for the sync to modify the
source.

**New account** → R2 → Manage API Tokens → Create: permission **Object Read & Write**,
scoped to the `rtub` bucket.

Each token gives an Access Key ID + Secret Access Key. The new account's pair is what
goes into Azure in step 6.

## 3. Configure rclone

Two remotes, one per account. `rclone config` interactively, or write
`~/.config/rclone/rclone.conf` (Windows: `%APPDATA%\rclone\rclone.conf`):

```ini
[r2old]
type = s3
provider = Cloudflare
access_key_id = <OLD_ACCESS_KEY_ID>
secret_access_key = <OLD_SECRET_ACCESS_KEY>
endpoint = https://<OLD_ACCOUNT_ID>.r2.cloudflarestorage.com
region = auto
no_check_bucket = true

[r2new]
type = s3
provider = Cloudflare
access_key_id = <NEW_ACCESS_KEY_ID>
secret_access_key = <NEW_SECRET_ACCESS_KEY>
endpoint = https://<NEW_ACCOUNT_ID>.r2.cloudflarestorage.com
region = auto
no_check_bucket = true
```

Sanity check that each remote sees what you expect. Always name the bucket — the tokens
are scoped to `rtub`, so any command that implies **ListBuckets** (`rclone lsd r2old:`
with no bucket) returns `403 AccessDenied`. That is the scope working correctly, not a
broken config.

```bash
rclone size r2old:rtub
```

Expect `471 objects` / `1.28 GB`. If the count differs from the dashboard, stop and find
out why before syncing.

```bash
rclone lsd r2old:rtub
```

Lists the top-level prefixes: `albums`, `events`, `images`, `item-configs`, `lyrics`,
`naipes`, `receipts`, plus the documents folders.

## 4. Dry run

```bash
rclone sync r2old:rtub r2new:rtub --dry-run --progress
```

Review the output: every line should be a copy into `rtub`, and only keys you recognise
from the prefix table in the architecture doc (`images/`, `events/`, `songs/`, `naipes/`,
`item-configs/`, `receipts/`, `albums/`, `lyrics/`, documents).

## 5. Sync

```bash
rclone sync r2old:rtub r2new:rtub --progress --transfers 8 --checksum
```

Object keys are preserved byte-for-byte, so the folder structure is identical by
construction. At 1.28 GB this is minutes on a normal connection.

Verify:

```bash
rclone size r2new:rtub
```

```bash
rclone check r2old:rtub r2new:rtub --one-way
```

`rclone check` must report **0 differences**. Do not continue otherwise.

Spot-check one public object in a browser:
`https://pub-XXXXXXXX.r2.dev/images/Production/...` should render.

## 6. Point the app at the new account

Azure Portal → App Service `rtub` → Settings → Environment variables → App settings.
Update four values:

| Setting | New value |
|---|---|
| `Cloudflare__R2__AccountId` | new account ID |
| `Cloudflare__R2__AccessKeyId` | new token's access key |
| `Cloudflare__R2__SecretAccessKey` | new token's secret |
| `Cloudflare__R2__PublicUrl` | `https://pub-XXXXXXXX.r2.dev` (no trailing slash) |

`Cloudflare__R2__Bucket` stays `rtub`. Save → the App Service restarts.

After restart, confirm in the log stream that no
`Cloudflare R2 credentials not configured` / `account ID not configured` error was thrown
at startup — those are fatal at DI time.

At this point **uploads go to the new bucket** and **existing images still load from the
old one**, because the stored URLs still name the old host and that bucket is still
public. This is the safe window; take as long as you need before step 7.

Also update local dev secrets so development doesn't keep writing to the old account:

```bash
dotnet user-secrets set "Cloudflare:R2:AccountId" "<NEW_ACCOUNT_ID>" --project src/RTUB.Web
```

(repeat for `AccessKeyId`, `SecretAccessKey`, `PublicUrl`)

## 7. Rewrite stored URLs

### 7a. Back up the database first

Non-negotiable. This is a bulk `UPDATE` against production. Until the automatic backup
service exists, take a manual snapshot: Azure Portal → App Service → Advanced Tools
(Kudu) → Debug console → download `app.db` (plus `app.db-wal`) from the site's data
directory, or stop the app briefly and copy it.

### 7b. Count what will change

Run in the app's **Database Viewer** (read-only mode), substituting the old host:

```sql
SELECT 'Albums.ImageUrl' AS col, COUNT(*) AS n FROM Albums WHERE ImageUrl LIKE 'https://pub-OLD.r2.dev%'
UNION ALL SELECT 'AspNetUsers.ImageUrl', COUNT(*) FROM AspNetUsers WHERE ImageUrl LIKE 'https://pub-OLD.r2.dev%'
UNION ALL SELECT 'Bets.ThumbnailUrl', COUNT(*) FROM Bets WHERE ThumbnailUrl LIKE 'https://pub-OLD.r2.dev%'
UNION ALL SELECT 'BetComments.MediaUrl', COUNT(*) FROM BetComments WHERE MediaUrl LIKE 'https://pub-OLD.r2.dev%'
UNION ALL SELECT 'CommentImages.Url', COUNT(*) FROM CommentImages WHERE Url LIKE 'https://pub-OLD.r2.dev%'
UNION ALL SELECT 'Events.ImageUrl', COUNT(*) FROM Events WHERE ImageUrl LIKE 'https://pub-OLD.r2.dev%'
UNION ALL SELECT 'EventVideos.Url', COUNT(*) FROM EventVideos WHERE Url LIKE 'https://pub-OLD.r2.dev%'
UNION ALL SELECT 'ForgeComboConfigs.PictureUrl', COUNT(*) FROM ForgeComboConfigs WHERE PictureUrl LIKE 'https://pub-OLD.r2.dev%'
UNION ALL SELECT 'GalleryMedia.MediaUrl', COUNT(*) FROM GalleryMedia WHERE MediaUrl LIKE 'https://pub-OLD.r2.dev%'
UNION ALL SELECT 'GalleryMedia.ThumbnailUrl', COUNT(*) FROM GalleryMedia WHERE ThumbnailUrl LIKE 'https://pub-OLD.r2.dev%'
UNION ALL SELECT 'Games.ImageUrl', COUNT(*) FROM Games WHERE ImageUrl LIKE 'https://pub-OLD.r2.dev%'
UNION ALL SELECT 'Instruments.ImageUrl', COUNT(*) FROM Instruments WHERE ImageUrl LIKE 'https://pub-OLD.r2.dev%'
UNION ALL SELECT 'Instruments.ThumbnailUrl', COUNT(*) FROM Instruments WHERE ThumbnailUrl LIKE 'https://pub-OLD.r2.dev%'
UNION ALL SELECT 'ItemTypeConfigs.PictureUrl', COUNT(*) FROM ItemTypeConfigs WHERE PictureUrl LIKE 'https://pub-OLD.r2.dev%'
UNION ALL SELECT 'MeetingAtas.PdfStorageUrl', COUNT(*) FROM MeetingAtas WHERE PdfStorageUrl LIKE 'https://pub-OLD.r2.dev%'
UNION ALL SELECT 'MeetingAtaAttachments.FileUrl', COUNT(*) FROM MeetingAtaAttachments WHERE FileUrl LIKE 'https://pub-OLD.r2.dev%'
UNION ALL SELECT 'NaipeContents.Url', COUNT(*) FROM NaipeContents WHERE Url LIKE 'https://pub-OLD.r2.dev%'
UNION ALL SELECT 'NaipeTypeConfigs.PictureUrl', COUNT(*) FROM NaipeTypeConfigs WHERE PictureUrl LIKE 'https://pub-OLD.r2.dev%'
UNION ALL SELECT 'PostMedia.Url', COUNT(*) FROM PostMedia WHERE Url LIKE 'https://pub-OLD.r2.dev%'
UNION ALL SELECT 'Products.ImageUrl', COUNT(*) FROM Products WHERE ImageUrl LIKE 'https://pub-OLD.r2.dev%'
UNION ALL SELECT 'Slideshows.ImageUrl', COUNT(*) FROM Slideshows WHERE ImageUrl LIKE 'https://pub-OLD.r2.dev%'
UNION ALL SELECT 'SongVideos.Url', COUNT(*) FROM SongVideos WHERE Url LIKE 'https://pub-OLD.r2.dev%'
UNION ALL SELECT 'StageEnemies.SpritePath', COUNT(*) FROM StageEnemies WHERE SpritePath LIKE 'https://pub-OLD.r2.dev%'
UNION ALL SELECT 'Transactions.ReceiptUrl', COUNT(*) FROM Transactions WHERE ReceiptUrl LIKE 'https://pub-OLD.r2.dev%';
```

Keep this output. Re-running it after the updates must return all zeros.

### 7c. Run the updates

The Database Viewer's modify mode requires each statement to **start with** `UPDATE` and
allows only one statement per execution, so run these one at a time. Every statement is
idempotent and guarded by `LIKE`, so re-running is harmless and rows holding Spotify /
YouTube / Google Drive links or local `/sprites/` paths are never touched.

```sql
UPDATE Albums SET ImageUrl = replace(ImageUrl, 'https://pub-OLD.r2.dev', 'https://pub-NEW.r2.dev') WHERE ImageUrl LIKE 'https://pub-OLD.r2.dev%';
UPDATE AspNetUsers SET ImageUrl = replace(ImageUrl, 'https://pub-OLD.r2.dev', 'https://pub-NEW.r2.dev') WHERE ImageUrl LIKE 'https://pub-OLD.r2.dev%';
UPDATE Bets SET ThumbnailUrl = replace(ThumbnailUrl, 'https://pub-OLD.r2.dev', 'https://pub-NEW.r2.dev') WHERE ThumbnailUrl LIKE 'https://pub-OLD.r2.dev%';
UPDATE BetComments SET MediaUrl = replace(MediaUrl, 'https://pub-OLD.r2.dev', 'https://pub-NEW.r2.dev') WHERE MediaUrl LIKE 'https://pub-OLD.r2.dev%';
UPDATE CommentImages SET Url = replace(Url, 'https://pub-OLD.r2.dev', 'https://pub-NEW.r2.dev') WHERE Url LIKE 'https://pub-OLD.r2.dev%';
UPDATE Events SET ImageUrl = replace(ImageUrl, 'https://pub-OLD.r2.dev', 'https://pub-NEW.r2.dev') WHERE ImageUrl LIKE 'https://pub-OLD.r2.dev%';
UPDATE EventVideos SET Url = replace(Url, 'https://pub-OLD.r2.dev', 'https://pub-NEW.r2.dev') WHERE Url LIKE 'https://pub-OLD.r2.dev%';
UPDATE ForgeComboConfigs SET PictureUrl = replace(PictureUrl, 'https://pub-OLD.r2.dev', 'https://pub-NEW.r2.dev') WHERE PictureUrl LIKE 'https://pub-OLD.r2.dev%';
UPDATE GalleryMedia SET MediaUrl = replace(MediaUrl, 'https://pub-OLD.r2.dev', 'https://pub-NEW.r2.dev') WHERE MediaUrl LIKE 'https://pub-OLD.r2.dev%';
UPDATE GalleryMedia SET ThumbnailUrl = replace(ThumbnailUrl, 'https://pub-OLD.r2.dev', 'https://pub-NEW.r2.dev') WHERE ThumbnailUrl LIKE 'https://pub-OLD.r2.dev%';
UPDATE Games SET ImageUrl = replace(ImageUrl, 'https://pub-OLD.r2.dev', 'https://pub-NEW.r2.dev') WHERE ImageUrl LIKE 'https://pub-OLD.r2.dev%';
UPDATE Instruments SET ImageUrl = replace(ImageUrl, 'https://pub-OLD.r2.dev', 'https://pub-NEW.r2.dev') WHERE ImageUrl LIKE 'https://pub-OLD.r2.dev%';
UPDATE Instruments SET ThumbnailUrl = replace(ThumbnailUrl, 'https://pub-OLD.r2.dev', 'https://pub-NEW.r2.dev') WHERE ThumbnailUrl LIKE 'https://pub-OLD.r2.dev%';
UPDATE ItemTypeConfigs SET PictureUrl = replace(PictureUrl, 'https://pub-OLD.r2.dev', 'https://pub-NEW.r2.dev') WHERE PictureUrl LIKE 'https://pub-OLD.r2.dev%';
UPDATE MeetingAtas SET PdfStorageUrl = replace(PdfStorageUrl, 'https://pub-OLD.r2.dev', 'https://pub-NEW.r2.dev') WHERE PdfStorageUrl LIKE 'https://pub-OLD.r2.dev%';
UPDATE MeetingAtaAttachments SET FileUrl = replace(FileUrl, 'https://pub-OLD.r2.dev', 'https://pub-NEW.r2.dev') WHERE FileUrl LIKE 'https://pub-OLD.r2.dev%';
UPDATE NaipeContents SET Url = replace(Url, 'https://pub-OLD.r2.dev', 'https://pub-NEW.r2.dev') WHERE Url LIKE 'https://pub-OLD.r2.dev%';
UPDATE NaipeTypeConfigs SET PictureUrl = replace(PictureUrl, 'https://pub-OLD.r2.dev', 'https://pub-NEW.r2.dev') WHERE PictureUrl LIKE 'https://pub-OLD.r2.dev%';
UPDATE PostMedia SET Url = replace(Url, 'https://pub-OLD.r2.dev', 'https://pub-NEW.r2.dev') WHERE Url LIKE 'https://pub-OLD.r2.dev%';
UPDATE Products SET ImageUrl = replace(ImageUrl, 'https://pub-OLD.r2.dev', 'https://pub-NEW.r2.dev') WHERE ImageUrl LIKE 'https://pub-OLD.r2.dev%';
UPDATE Slideshows SET ImageUrl = replace(ImageUrl, 'https://pub-OLD.r2.dev', 'https://pub-NEW.r2.dev') WHERE ImageUrl LIKE 'https://pub-OLD.r2.dev%';
UPDATE SongVideos SET Url = replace(Url, 'https://pub-OLD.r2.dev', 'https://pub-NEW.r2.dev') WHERE Url LIKE 'https://pub-OLD.r2.dev%';
UPDATE StageEnemies SET SpritePath = replace(SpritePath, 'https://pub-OLD.r2.dev', 'https://pub-NEW.r2.dev') WHERE SpritePath LIKE 'https://pub-OLD.r2.dev%';
UPDATE Transactions SET ReceiptUrl = replace(ReceiptUrl, 'https://pub-OLD.r2.dev', 'https://pub-NEW.r2.dev') WHERE ReceiptUrl LIKE 'https://pub-OLD.r2.dev%';
```

Documents (`CloudflareDocumentStorageService`) store **object keys**, not URLs, and are
served through short-lived pre-signed URLs — nothing to rewrite there.

### 7d. Verify

Re-run the 7b count query — all zeros. Then browse the app: profile pictures, gallery,
event images, album art, naipes videos, a receipt PDF, a documents folder.

## 8. Rollback window

Leave the old bucket in place, public, untouched for ~30 days.

Rollback at any point before deletion = revert the four Azure app settings and re-run the
7c statements with OLD and NEW swapped. Objects uploaded to the new bucket during the
window would need copying back (`rclone sync r2new:rtub r2old:rtub`), which is why the
window should not drag on indefinitely.

After the window: delete the `rtub` bucket **in the personal account only**, and revoke
the old read-only API token.

## 9. Follow-ups

- The private database-backup bucket is created in the **new** account — see the backup
  section of [cloudflare-r2-and-database-backups.md](cloudflare-r2-and-database-backups.md).
- `README.md`'s configuration section still documents the retired `IDrive__*` variables;
  correct it to `Cloudflare__R2__*` when convenient.
- A cheap custom domain on the bucket would make any future account move DNS-only and
  retire step 7 permanently. Optional.
