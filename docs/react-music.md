# React Music

`/music` and its album pages are React plus a thin API. Shell, route table and module rules: [react-portal-pilot](react-portal-pilot.md).
Code: `Helpers/MusicAuthorization.cs`, `Services/MusicService.cs`, `Endpoints/MusicEndpoints.cs`, `DTOs/MusicDtos.cs`, portal `Music*.tsx`.
Tests: `MusicApiTests`, `MusicServiceCooldownTests`, `PortalRouteTests`.

## Routes

| Path | Notes |
| --- | --- |
| `/music` | Album grid. Visitors: public albums; members: + private + their exclusive albums; Owner: all. |
| `/music/albums/{id}` | One album: songs, search, player, lyrics, links, videos (members), management. |
| `/music/learn` | Aprender Músicas, members only: songs that have a chart, one grid per album; a card opens `/music/play/{songId}`, whose back link returns here. In the member menu. The Owner adds, replaces, removes a chart here ("Incluir dedilhado" toggle) and sends the "new song" push (bell). |
| `/music/play/{songId}` | Aprender (below). Members only; a visitor is sent to sign in. |
| `/music/songs/{id}` | `302` to `/music/albums/{id}`, query kept, GET/HEAD only. |
| `/api/music/...` | API (below). |

## Permissions

| | Visitor | Member | Mod | Admin | Owner |
| --- | --- | --- | --- | --- | --- |
| See public albums | yes | yes | yes | yes | yes |
| See private albums | | yes | yes | yes | yes |
| See an exclusive album | | if listed | if listed | if listed | yes |
| Play, lyrics, links | on albums they see | | | | |
| Videos: watch, upload | | yes | yes | yes | yes |
| Videos: delete | | own | own | any | any |
| Statistics (songs, albums) | | yes | yes | yes | yes |
| Statistics per member | | | | yes | yes |
| Create/edit album and song | | | yes | yes | yes |
| Delete album or song | | | | yes | yes |
| Exclusive flag and access list | | | | | yes |
| Play-along chart: view | | yes | yes | yes | yes |
| Play-along chart: replace, remove, notify | | | | | yes |

- Owner is checked explicitly wherever Admin is. Every rule also limits writes to albums the caller can see.
- A visitor asking for a members-only album gets `401` (the page offers the login); a member asking for an exclusive album they are not on gets `404`.
- The event repertoire editor (`EventRepertoireAdminService`) is also open to the current Ensaiador / Magister for upcoming events; its song picker shows only what Music shows them. See [react-events](react-events.md).

## Data

No Music-specific schema beyond `SongPlayAlongs`. Tables: `Albums`, `AlbumAccesses`, `Songs`, `SongYouTubeUrls`, `SongVideos`, `SongPlayCounts`, `SongPlayAlongs`; `EventRepertoires` references songs.

- Album ordering: dated ascending, undated last, then Id. Song ordering: track number ascending, none last, then Id.
- `Album.Description` limit disagrees (entity 1000, EF config 500); SQLite enforces neither; the API keeps 1000.
- `AlbumAccesses` rows are ignored unless the album is exclusive (stale rows may remain after exclusivity is turned off).
- `Song.Duration` is only used for the play cooldown and is unset today, so the 30 s default applies.
- Links (Spotify, YouTube) are http(s) only, in and out. Lyrics and lyric authors are never rendered as markup.
- Deleting a song cascades to `EventRepertoires`. Deleting an album cascades songs, links, videos, plays and repertoire rows, and deletes the cover.
- `CreatedAt/By`, `UpdatedAt/By` and uploader ids never leave the server (`canDelete` replaces the uploader id).

## API (`/api/music`)

Reads are open (answers depend on the caller); every write, plays included, needs the antiforgery token in `X-CSRF-TOKEN` (`GET /api/public/antiforgery-token`). Everything is `Cache-Control: no-store`. No entity, other member's user id, object key or audit field leaves the server. Unexpected failures answer `500` as a generic JSON problem; details go to the log.

| Method | Path | Who | |
| --- | --- | --- | --- |
| GET | `/albums` | all | visible albums + the caller's permissions |
| GET | `/albums/{id}` | all | album, songs, permissions; `videoCount` for members |
| GET | `/songs/{id}/lyrics` | all | `{pdfUrl, text}` |
| GET | `/songs/{id}/audio` | all | short-lived audio link, nothing counted (player preloads neighbours) |
| POST | `/songs/{id}/plays` | all | counts unless in cooldown; `{audioUrl, counted, playCount}`; `404` + `music:audio-unavailable` without audio |
| GET | `/songs/{id}/videos` | members | list with `canDelete` |
| POST | `/videos/{id}/plays` | members | audit row, 30 s cooldown |
| GET | `/statistics` | members | songs/albums over visible albums; `members` for Admin/Owner |
| GET | `/members` | Owner | access-list picker |
| GET | `/albums/{id}/edit`, `/songs/{id}/edit` | Mod+ | form data |
| POST / PUT | `/albums`, `/albums/{id}` | Mod+ | multipart: fields + optional `cover` (WebP/JPEG/PNG, 5 MB max) |
| DELETE | `/albums/{id}` | Admin, Owner | |
| POST | `/albums/{id}/songs` | Mod+ | JSON |
| PUT / DELETE | `/songs/{id}` | Mod+ / Admin, Owner | |
| GET | `/playalongs` | members | `{ songs, canManage }`: songs with a chart in visible albums, newest album first; `canManage` = Owner |
| GET | `/songs/{id}/playalong` | members | the chart; `404` when none |
| PUT / DELETE | `/songs/{id}/playalong` | Owner | JSON chart (512 KB max), validated and rewritten by `PlayAlongChart`; replaces / removes |
| POST | `/songs/{id}/playalong/notify` | Owner | push "Nova música para aprender": optional `message` (300 max); everyone, or the list of an exclusive album; `404` without a chart |
| POST | `/songs/{id}/videos` | members | multipart `file` (video, 100 MB max) + `title`; notifies members |
| DELETE | `/videos/{id}` | uploader, Admin, Owner | |

## Media (Cloudflare R2)

- Audio and lyric PDFs: private bucket, pre-signed URLs (`IAudioStorageService` / `ILyricStorageService`, 60 min), created per request for a song the caller may see. The object key is derived from album and song titles inside the service, so renaming affects it.
- Covers and videos: absolute URLs on the public R2 host, as stored.
- Uploads: `IImageStorageService` (`albums/` key from the title) and `SongService.AddVideoAsync`; deletes through `AlbumService` / `SongService`.
- CSP: `media-src` and `frame-src` allow the R2 endpoint, `img-src` the public host.

## Player and cooldown

- Cooldown (server): per listener (member id, or client IP for visitors) and song, for the song's `Duration`, 30 s when unknown. A repeat inside it still plays, it is just not counted. `IMemoryCache` + one lock: per app instance (see the `ponytail:` note in `MusicService`).
- Player: fixed bar with previous/next over the album's songs that have audio, auto-advance, and the Media Session card (title, album, cover, play/pause, previous/next, seek; no seek-forward/back so iOS shows previous/next). Handlers and metadata are released when the player closes or the page goes away (iOS fix, keep it).

## Aprender (Tocar junto)

A member plays a song's chords along with its recording at `/music/play/{songId}`, opened from the "Aprender" button a song shows when it has a chart (`hasPlayAlong` in the album payload, always `false` for visitors).

- Who: any signed-in member who can see the song's album (same visibility as Music, exclusive included). Only the Owner uploads, replaces or removes a chart.
- Chart storage: one row per song in `SongPlayAlongs` (migration `AddSongPlayAlongs`: additive, cascade FK to `Songs`, unique `SongId`; the previous app version ignores it). A table, not a `Songs` column, so no song query carries 20 KB of JSON.
- Chart format, version 1 = `PlayAlongChartDto` (`MusicDtos.cs`): duration, bpm, beats, and sections > lines > words with a time each and chords (`label` as the songbook writes it, `name` in letters, `time`, and the word / letter it sits on). Optional `picking: true` enables Dedilhado.
- Trust boundary: `PlayAlongChart.Validate` (`Helpers/PlayAlongChart.cs`) checks every field (counts, lengths, time ranges, chord names by pattern, chord times in order); the stored JSON is the chart written back from the typed model, so unknown fields are dropped. The audit log records who changed a chart and when, with the chart redacted (`AuditConfiguration.RedactedProperties`).
- Making a chart: offline, `tools/playalong` (README there): songbook PDF + recording > vocal split, forced alignment, beat tracking > `chart.json`, reviewed by ear before upload. Needs PyTorch and a 1.5 GB model, so it never runs on the server. Song files are never committed; charts are uploaded by hand per environment, never seeded.
- Page (`MusicPlayAlong.tsx`): strings across the screen, lowest on top (6 guitarra; 4 bandolim, cavaquinho, baixo; choice kept in `localStorage`); each chord arrives at the line as a block of fret numbers; lyric line with chords and the sung word lit; a section timeline that seeks; 60 / 80 / 100 % speed (`playbackRate`, pitch kept); "Repetir secção"; space bar plays / pauses; screen stays awake while playing. One `requestAnimationFrame` loop reads the audio clock; React state changes only when the line or chord does. Audio is the `GET /songs/{id}/audio` link: opening the page counts no play.
- Chord shapes are in `MusicPlayAlong.tsx` (`SHAPES`). A chord not there shows its name without fingering. Cavaquinho assumes D G B D tuning; baixo has no table (shows the chord's root, or the note after the slash).
- Dedilhado (guitarra only, switch "Acordes" / "Dedilhado"): each chord block shows picked notes instead of the whole chord, from one built-in pattern (`pickingOf`): bass, 3rd string, 2nd + 1st together, 3rd string, then the same on the second bass string; the two bass strings follow the chord. Offered only when the chart has `picking: true`, ticked by the Owner per song ("Incluir dedilhado"); the page then opens in Dedilhado.
- A recording whose length differs from the chart's by more than 2 s shows "A gravação mudou…": the chart was made for another file.

## Follow-ups

- Bandolim and cavaquinho shapes need a player's review.
- Not built: microphone scoring, melody (sheets carry chords only), an in-app chart editor, a stored picking pattern per song.
- Not verified: real R2 audio against the play-along page; installed iPhone / Android app (speed change, landscape, wake lock).
- No upload progress bar for videos (plain "A enviar…" state).
- Album paging was dropped (few albums fit one grid); add it back if the count grows.
