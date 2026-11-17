# Performance gate

Run this gate before a PROD release that changes the shell, a route's data loading, assets or the service worker.

## How to measure

Local DEV-copy database (`.local/ui-review/app.db`), Release build, `ASPNETCORE_ENVIRONMENT=Staging` (compression and
the 30-day static cache on, as on Azure DEV), R2 with placeholder credentials (no account reached; the storage round
trip still costs network time, as on DEV). Lighthouse 12, headless Chrome, cold HTTP cache and service worker cleared
before every run, median of 3 runs, desktop preset and default mobile (simulated slow 4G, 4x CPU). Signed in as an
Owner through the real `/login` form. Routes to audit: `/`, `/events`, an event, `/messages`, `/profile`, `/members`,
`/rehearsals`, `/logistics` and a board, `/naipes`.

Gate: mobile performance and CLS must not regress from the last gate run (mobile 94-98, CLS about 0, TBT 0-30 ms, LCP
about 2.25-3.0 s). Local Best Practices 89-93 is not real: the copied database stores production R2 media URLs and the
CSP blocks that domain.

## Rules that keep the numbers

Pinned by `tests/RTUB.Web.Tests/ReactPortal/PerformanceGateSourceTests.cs`.

- Splash, header brand, Home badge and system avatars use `/icons/rtub-logo-352.webp` (45 KB), not the 512 px PNG. A
  hidden `<img>` still downloads, so Home's hero emblem uses `<picture>` for phones. Manifest, apple-touch and push icons
  are unchanged.
- No layout shift on mount: `.boot` uses padding (a margin collapses through `<body>`); `main { min-height:
  calc(100vh - var(--header-h)) }` keeps the footer below the fold; `/members` actions render while the list loads
  (only "Adicionar membro" waits for `canManage`); the event hero has `aspect-ratio: 3 / 2`.
- Logistics: board changes reload the board only (`?files=false`); the R2 folder listing (the slowest call in the
  app) loads on open, on "Tentar novamente" and after a file upload or delete (`/file-list`).
- Cookie validation (`OnValidatePrincipal`) asks the database about the Admin role only when the cookie claims it. The
  unread badge polls every 30 s; `/api/version` is folded into `/api/account/me`, which starts before React mounts.
- Static files (`/images/*`, ETag/304, `no-cache`) are served before authentication. Brotli/gzip `Optimal`;
  `immutable` on `/portal/assets/*`. Both Fraunces files are served from `wwwroot/fonts/` (30-day cache) and preloaded
  from `index.html`.
- Service worker: hashed assets cache-first, navigation preload, no `respondWith` for `/api`, `/auth`, `/hubs`,
  `/health`, non-GET and non-http requests; registered on `load`.
- Hero logo: 512 px for 1x and the 1024 px for 2x with `fetchpriority="high"`; default avatar is 256 px.
- The linux-x64 publish uses `-p:PublishReadyToRun=true` (cold start on a one-core B1).
- Intro (`.splash*`, `:root.intro`, `revealApp`, the italic face it uses) is by owner decision shown on every session.
- Owner decisions: cookie validation is gated but not cached; no client router until measurements justify it.

## Deferred findings

Severity: H every request / page / unbounded growth, M one main page or write, L rare. *verified* = read in source.

| # | Sev | Finding | Where | Fix |
|---|---|---|---|---|
| A3 | H | Unread count = instr scan on `Conversations.Participants` + COUNT over all messages with `!ReadBy.Contains(me)` (substring list, unindexable), per poll. *verified* | `MessagingService.cs` `GetUnreadAsync` | per-user read watermark (`ConversationUserSettings`, additive column; confirm against `docs/react-messages.md`) then indexed COUNT |
| A4 | H | Messages list: GROUP BY MAX(Id) over all my messages + a second query although `Conversation.LastMessageId` is kept on send. *verified* | `MessagingService.cs` `ListItemsAsync` | join on `LastMessageId`, MAX only where null |
| A5 | H | Thread poll (10 s) reloads the whole page: `Include(Sender)` full rows, list item recomputed, people twice; `ORDER BY Id DESC` sorts in a temp B-tree. *verified* | `MessagingService.cs` `DetailAsync`, `Messages.tsx` | `?after=<lastId>` delta poll, projected sender, index `(ConversationId, Id)` |
| B2 | M | `/api/members`: whole `AspNetUsers` as entities, all `MemberInstruments`, whole `RoleAssignments` cut in memory, re-run per keystroke. *verified* | `MemberDirectoryService.cs`, `Members.tsx` | project columns, `Where(StartYear)` in SQL, search client-side (the whole directory is returned anyway) |
| B3 | M | Leaderboard recomputes every member's XP per request and per search keystroke; two redundant aggregates. | `LeaderboardService.cs`, `RankingService.cs`, `Leaderboard.tsx` | search client-side; 60 s cache per fiscal year |
| B4 | M | `/profile` about 14 queries (own row 3x, mentor / instruments 2x, whole `RoleAssignments`). | `MyProfileService.cs` | load once, pass down |
| B5 | M | Meetings: `GetAllMeetingsAsync(..., int.MaxValue)` + two full user rows, FY / search in memory; `VisibleMeetingAsync` loads all meetings to find one. | `MeetingBoardService.cs`, `MeetingService.cs` | filter / page in SQL, `FirstOrDefaultAsync` |
| B6 | M | Naipes board: `Include(Comments)` + `Include(PlayCounts)` (every play ever) to compute two counts; type configs re-read per GET. | `NaipeContentRepository.cs`, `NaipeBoardService.cs` | project the counts (DTO change) |
| B7 | M | Members map: one `GeocodingCaches` query per distinct city. | `MemberMapService.cs` | load the table once |
| B10 | M | Hall of Fame: all users + whole `RoleAssignments` O(U x A) + entire `Enrollments` with `Include(Event)`. | `HallOfFameService.cs` | SQL GROUP BY, 10 min cache |
| B11 | M | Rehearsals agenda: GROUP BY over the whole `RehearsalAttendances` table per load; detail full user rows. | `RehearsalAgendaService.cs` | season window, projected attendees |
| B12 | L | Questions list includes every reply body + author to count replies. | `QuestionRepository.cs` | project `Replies.Count()` |
| B14 | L | Owner / admin lists in memory (`/users`, `/requests`, treasury, audit-log facets + `Changes.Contains`, report history per-row loads, logistics R2 listing per open). | various | per page when touched |
| C2 | H | Push fan-out inline in the member's request: sequential sends with 0.6 / 1.8 / 5.4 s back-off sleeps on 429. *verified* | `PushNotificationService.cs`, callers in enrollment / attendance / messaging / gallery | `Channel<PushJob>` + one `BackgroundService`, bounded concurrency; query only the selected users' subscriptions |
| C3 | M | Audited `SaveChangesAsync`: name-lookup SELECTs, the write, then a second write transaction for `AuditLogs`; 4 change-tracker scans. *verified* | `ApplicationDbContext.cs` | one transaction, single `DetectChanges` (security-relevant: own commit, full suite) |
| C4 | M | Event / rehearsal answer about 30-38 commands (`LoadEnrollmentAsync` twice, inline reconcile, inline push). | `EventAgendaService.cs`, `EnrollmentService.cs`, `MemberStatusService.cs` | after C2: reload once, reconcile on the channel |
| C5 | M | Bulk email inside the admin request; SMTP serialised; a new `SmtpClient` per message. | `EmailNotificationService.cs` | second lane on the C2 channel, or accept |
| C6 | L | `AddMemoryCache` without `SizeLimit`; ata / report PDFs cached 1 h unbounded on a 1.75 GB B1. | `ServiceCollectionExtensions.cs`, `AtaPdfService.cs`, `ReportPdfService.cs` | `SizeLimit` + `Size` |
| D1 | H | Gallery tiles (24 / page) and the Home preview (5) load R2 originals (up to 10 MB); `ThumbnailUrl` never generated; no dimensions, so masonry reflows. *verified* | `Gallery.tsx`, `Home.tsx`, `GalleryManage.tsx` | client-side resize at upload (reuse `Cropper.tsx`): main up to 1600 px + 480 px thumb + width / height (schema) |
| D8 | M | One 184 KB / 14,377-line stylesheet on every page. *verified* | `styles.css` | coverage pass, then per-area CSS imported by the lazy chunks (splash / intro rules stay in the main sheet) |
| D9 | M | Route waterfall: HTML, entry, `import()` chunk, mount, first API call. | `main.tsx`, `Program.cs` | `modulepreload` for the route's chunk from Vite's manifest |
| D11 | M | Avatars 600 px shown at 24-72 px everywhere (`/members` loads about 60). *verified* | `membersApi.ts`, `MemberShell.tsx` | 128 px variant at crop time + DTOs (storage change) |
| D13-15 | L | `icons.tsx` ships all 89 paths; `musicApi.ts` duplicates `call()`; management dialogs in public chunks; `rtub-logo-maskable-512.png` 254 KB (manifest only); Fraunces italic (80 KB) loads on Home for one `<em>`. | | when touched |
| E1 | H | PROD Always On off: cold start after 20 min idle. | Azure | owner action (also needed by the daily backup) |
| E3 | L | HTTP/2 state of both App Services undocumented. | Azure | `az webapp config show --query http20Enabled` |
| F3 | L | `.Date.Date` filters bypass the date indexes; `PushSubscriptions.Endpoint`, `AuditLogs.Action` unindexed. | repositories, reminder jobs | range compares; two indexes |
| - | - | Compression beyond `Optimal`: build-time precompression (Node `zlib`) + sibling-serving middleware, or `MapStaticAssets` (serves Vite-hashed names as `no-cache` + ETag unless overridden). | | spike after measuring |
| - | - | Client router (every link is a full navigation); cached cookie validation. | | owner: measure first / declined |

Accessibility contrast to fix when touched: `.lx-status--todo` (3.48:1), `.agenda-card__year` (4.42:1),
`.tuna-path__years` (3.89:1).

## Looks bad but is fine

R2 uploads already carry `Cache-Control: public, max-age=31536000, immutable`; global `SplitQuery` is right for SQLite;
the boot cost is the JIT and the reconcile, not seeding (`SeedData` returns after one `AnyAsync`); the PRAGMA batch per
connection open costs microseconds (kept: Microsoft.Data.Sqlite's command timeout is a polling loop, not
`sqlite3_busy_timeout`); `UseResponseCaching` does store the static files it may.
