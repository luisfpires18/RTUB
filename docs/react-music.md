# React Music (React track, task 006)

`/music` and its album pages are React + a thin API since task 006 (DEV only). The Blazor Music UI
is retired. Shell, route table and module rules: `docs/react-portal-pilot.md`.

## Routes

| Path | Owner | Notes |
| --- | --- | --- |
| `/music` | **React** | Album grid. Visitors: public albums; members: + private + their exclusive albums; Owner: all. |
| `/music/albums/{id}` | **React** | One album: songs, search, player, lyrics, links, videos (members), management. |
| `/music/songs/{id}` | redirect | Retired Blazor album page; `302` → `/music/albums/{id}`, query kept, GET/HEAD only. |
| `/api/music/...` | API | `src/RTUB.Web/Endpoints/MusicEndpoints.cs` (below). |

Pinned by `PortalRouteTests` and `MusicApiTests`.

## What the Blazor pages did (audit, before 006)

`Pages/Media/Albums.razor` (`/music`) and `Songs.razor` (`/music/songs/{id}`), with `SongCard`:
public/private album sections with paging (6/12/18/24), album create/edit (Admin|Mod; exclusive +
member picker Owner-only; cover through a 1:1 cropper, WebP), album delete (button Admin-only, but the
handler accepted Mod), statistics modal (any member: top songs/albums with paging 5-25; Admin:
per-member detail), song grid with search (title / lyric author / music author), Play (only when
`HasMusic`), lyrics (songbook PDF from R2, else stored text), links (Spotify + YouTube), videos
(members: watch, upload ≤100 MB, delete own; Admin/Owner delete any), song create/edit (Admin, Owner,
Mod), delete (Admin, Owner), a fixed player with lock-screen controls, and an audit-log row per play.

Defects found and not carried over:
- The album page never checked exclusive access: any member could open an exclusive album by URL.
- Visitors saw exclusive albums that were not also private (`GetPublicAlbumsAsync` = `!IsPrivate`).
- Saving an exclusive album as a Mod or Admin silently cleared `IsExclusive`.
- Statistics listed songs of exclusive albums to members not on the list.
- The play cooldown lived in the Blazor circuit (`SongPlayService`, scoped): a reload reset it.
- Stored links were rendered as `href` unchecked, and author fields through `MarkupString`.
- Private album, signed out: redirect to `/Identity/Account/Login`, not this app's `/login`.

## Data audit (real `app.db`, read-only; counts only)

No migration. The React area reads and writes the existing tables through the existing entities.

| Table | Field | Type / limit / null | Real data | Blazor | React | Visibility | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Albums | Title | text ≤200, required | 7 rows, max 21 chars | yes | yes | public | |
| Albums | Year | int?, 1900..now | 4 dated, 3 undated | yes | yes | public | ordering: dated ascending, undated last, then Id |
| Albums | Description | text? ≤1000 (entity) / 500 (EF config) | 3 set, max 25 | yes | yes | public | limits disagree; SQLite enforces neither; API keeps the entity's 1000 |
| Albums | ImageUrl | text? ≤500 | 7, all on the public R2 host | yes | yes (`coverUrl`) | public | absolute public URL; only http(s) is passed on |
| Albums | IsPrivate | bool | 3 private | yes | yes | members | |
| Albums | IsExclusive | bool | 0 exclusive | yes | yes | Owner edits | |
| AlbumAccesses | AlbumId, UserId, AddedAt | FK, user id ≤450 | 18 rows, all on album 12, which is **not** exclusive | yes | Owner form only | internal | stale (left when exclusivity was turned off); ignored unless the album is exclusive; kept |
| Songs | Title | text ≤200, required | 95 rows, max 37 | yes | yes | album's | also part of the R2 audio/lyric object key |
| Songs | TrackNumber | int? 1..999 | 14 null (one album), 5 duplicate pairs | yes | yes | album's | ordering: number ascending, none last, then Id |
| Songs | LyricAuthor, MusicAuthor, Adaptation | text? ≤200 | 79, 79, 1 set | yes | yes | album's | |
| Songs | Lyrics | text? ≤10000 | 52 set, max 1484 | yes | on demand | album's | not in the album payload |
| Songs | Duration | int? seconds | 0 set | no | cooldown only | internal | so every cooldown is the 30 s default today |
| Songs | SpotifyUrl | text? ≤500 | 2 set | yes | yes (`links`) | album's | http(s) only, in and out |
| Songs | HasMusic | bool | 83 true | yes | yes (`hasAudio`) | album's | gates Play |
| SongYouTubeUrls | Url, Description | ≤500 / ≤200 | 10 rows on 8 songs | yes | yes (`links`) | album's | Description unused |
| SongVideos | Url, Title, MimeType, SizeBytes, SortOrder, CreatedByUserId | | 2 rows, 1 song, mp4, public R2 host, SizeBytes 0 | yes | yes | members | `canDelete` instead of the uploader id; SizeBytes not exposed |
| SongPlayCounts | SongId, UserId?, PlayedAt | | 2920 plays, 671 anonymous, 41 members | yes | counts | members (totals) / Admin (per member) | one row per counted play |
| EventRepertoires | SongId | FK | 43 rows, 22 songs | no | no | Events | deleting a song cascades here (unchanged) |
| all | CreatedAt/By, UpdatedAt/By | audit | | no | no | internal | never exposed |

Roles in the database: 1 Owner (also Admin), 11 Admin, 3 Mod, 94 Member.

## Rules (`RTUB.Application/Helpers/MusicAuthorization.cs`, enforced in `MusicService`)

| | Visitor | Member | Mod | Admin | Owner |
| --- | --- | --- | --- | --- | --- |
| See public albums | ✓ | ✓ | ✓ | ✓ | ✓ |
| See private albums | | ✓ | ✓ | ✓ | ✓ |
| See an exclusive album | | if listed | if listed | if listed | ✓ |
| Play, lyrics, links | on albums they see | | | | |
| Videos: watch, upload | | ✓ | ✓ | ✓ | ✓ |
| Videos: delete | | own | own | any | any |
| Statistics (songs, albums) | | ✓ | ✓ | ✓ | ✓ |
| Statistics per member | | | | ✓ | ✓ |
| Create/edit album, create/edit song | | | ✓ | ✓ | ✓ |
| Delete album or song | | | | ✓ | ✓ |
| Exclusive flag and access list | | | | | ✓ |

Owner is checked explicitly wherever Admin is. Every rule above also limits writes to albums the
caller can see. A visitor asking for a members-only album gets `401` (the page offers the login);
a member asking for an exclusive album they are not on gets `404`.

## API (`/api/music`)

Reads are open (answers depend on the caller); every write - plays included - needs the antiforgery
token in `X-CSRF-TOKEN` (`GET /api/public/antiforgery-token`). Everything is `Cache-Control: no-store`.
DTOs: `RTUB.Application/DTOs/MusicDtos.cs`; no entity, user id of another member, object key or
audit field leaves the server. Unexpected failures (e.g. storage unreachable) answer `500` as a generic
JSON problem; details only go to the log.

| Method | Path | Who | |
| --- | --- | --- | --- |
| GET | `/albums` | all | visible albums + the caller's permissions |
| GET | `/albums/{id}` | all | album, songs (links filtered to http(s)), permissions; `videoCount` for members |
| GET | `/songs/{id}/lyrics` | all | `{pdfUrl, text}` |
| GET | `/songs/{id}/audio` | all | short-lived audio link, nothing counted (player preloads neighbours) |
| POST | `/songs/{id}/plays` | all | counts unless in cooldown; `{audioUrl, counted, playCount}`; `404` + `music:audio-unavailable` without audio |
| GET | `/songs/{id}/videos` | members | list with `canDelete` |
| POST | `/videos/{id}/plays` | members | audit row, 30 s cooldown |
| GET | `/statistics` | members | songs/albums over visible albums; `members` for Admin/Owner |
| GET | `/members` | Owner | access-list picker |
| GET | `/albums/{id}/edit`, `/songs/{id}/edit` | Mod+ | form data |
| POST / PUT | `/albums`, `/albums/{id}` | Mod+ | multipart: fields + optional `cover` (WebP/JPEG/PNG ≤5 MB) |
| DELETE | `/albums/{id}` | Admin, Owner | cascades songs, links, videos, plays, repertoire rows; deletes the cover |
| POST | `/albums/{id}/songs` | Mod+ | JSON |
| PUT / DELETE | `/songs/{id}` | Mod+ / Admin, Owner | |
| POST | `/songs/{id}/videos` | members | multipart `file` (video, ≤100 MB) + `title`; notifies as before |
| DELETE | `/videos/{id}` | uploader, Admin, Owner | |

## Media (Cloudflare R2)

- **Audio and lyric PDFs** come from the private bucket as pre-signed URLs (existing
  `IAudioStorageService` / `ILyricStorageService`, 60 min), created per request for a song the caller
  may see. The object key is derived from album and song titles inside the service, as before.
- **Covers and videos** are absolute URLs on the public R2 host, as stored.
- **Uploads** go through the existing `IImageStorageService` (`albums/` key from the title) and
  `SongService.AddVideoAsync`; deletes through `AlbumService`/`SongService`. Tests and the browser
  checks used fakes / placeholder credentials: nothing was uploaded to or deleted from R2.
- CSP unchanged: `media-src` and `frame-src` already allow the R2 endpoint, `img-src` the public host.

## Player and cooldown

- **Cooldown (server):** per listener (member id, or client IP for visitors) and song, for the song's
  `Duration`, 30 s when unknown (always today). A repeat inside it still plays, it is just not
  counted. `IMemoryCache` + one lock: per app instance (see the `ponytail:` note in `MusicService`).
- **Player:** fixed bar with previous/next over the album's songs that have audio, auto-advance at
  the end, and the Media Session card (title, album, cover, play/pause, previous/next, seek; no
  seek-forward/back so iOS shows previous/next). Handlers and metadata are released when the player
  closes or the page goes away (the iOS fix of `fix/030`, carried over).

## Retired in 006

Pages `Albums.razor`, `Songs.razor` (+ scoped CSS); `SongCard`, `SongCardSkeleton`;
`MediaQueueService`, `QueueTrack`, `AudioPlayerInterop`; the circuit-only services
`SongPlayService`, `SongUrlCacheService`, `SongValidationService`, `AlbumStatisticsService`,
`AlbumFilterService`, `AlbumImageService`; their tests (`AlbumsPageTests`, `SongsPageTests`,
`SongCardTests`, `MediaQueueServiceTests`, `MusicPagesTests`). Kept: entities, repositories,
`AlbumService`, `SongService`, `SongContentService`, storage services. New tests: `MusicApiTests`
(22), `MusicServiceCooldownTests` (3), route tests in `PortalRouteTests`.

## Remaining Music follow-ups

- Global CSS of the retired pages (`3-components/music.css`, `song-card.css`, `audio-player.css`)
  and the PWA queue part of `pwaMediaSession.js` / `MediaSessionInterop` are no longer used by Music;
  other pages still use parts of them. Clean up with care.
- Member listening stats inside the member area (`/member/profile`) stay Blazor.
- Album paging (6-24 per page) was dropped: 7 albums fit one grid. Add it back if the count grows.
- No upload progress bar for videos (a plain "A enviar…" state).
- `AlbumAccesses` rows of album 12 are stale; decide whether to delete them (data change, not done).
