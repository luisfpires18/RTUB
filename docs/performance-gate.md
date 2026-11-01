# Performance gate before PROD (task 045)

Measured 2026-10-13 on `dev` @ `f1fcfab7` (044 merged) against the local DEV-copy database (`.local/ui-review/app.db`),
Release build, `ASPNETCORE_ENVIRONMENT=Staging` (compression and the 30-day static cache on, as on Azure DEV), R2 with
placeholder credentials (no account reached: the storage round trip still costs network time, as on DEV).
Lighthouse 12, headless Chrome, cold HTTP cache and service worker cleared before every run, median of 3 runs, desktop
preset and default mobile (simulated slow 4G, 4x CPU). Signed in as an Owner through the real `/login` form.

## Root causes

| # | Finding | Cost | Fix |
|---|---|---|---|
| 1 | Splash `<img>` (static in `index.html`, on every cold load) was `/icons/rtub-logo-512.png`, **465 KB**, shown at 176px; the header logo was the 192px PNG (86 KB) at 40px. On mobile the PNG competed with the JS for bandwidth, so every route had LCP ≈ 4.35 s. | P1 | `/icons/rtub-logo-352.webp` (45 KB, from the same 512px source) for the splash, header brand, Home badge / record, News and Messages system avatar. Home's hero emblem (hidden on phones, but a hidden `<img>` still downloads) gets it through `<picture>` on ≤599px. Manifest / apple-touch / push icons unchanged. |
| 2 | CLS 0.2-0.8 on every route: `.boot`'s 96px top margin collapsed through `<body>`, which jumped when React mounted (0.10); the footer sat right under the page loader and jumped when data arrived (0.2-0.5); `/members` actions appeared only with data and pushed the search bar (0.37 on phones). | P1 | `.boot` uses padding; `main { min-height: calc(100vh - var(--header-h)) }` (footer starts below the fold; app-screen pages keep their own layout); `/members` actions render while the list loads (only "Adicionar membro" waits for `canManage`). |
| 3 | `/logistics/{id}`: the R2 folder listing (ListObjectsV2) is the only slow call in the app (1.7 s median, up to 3-5 s). 044 already took it off the board's critical path (`?files=false` + `/file-list`), but every card / list change still re-listed R2 with the board. | P1 | Board changes reload the board only; the file list loads on open, on "Tentar novamente" and after a file upload / delete. |

The backend was not a bottleneck: every API the audited routes call answers in ≤ 8 ms median (board 4 ms, 5 queries
whatever the card count, `AsNoTracking`, assignment counts batched; messaging list batched; members 8 ms / 9.5 KB br).

## Before / after (median of 3)

| Route | Desktop perf | Mobile perf | Mobile LCP | CLS (mobile) | Cold transfer |
|---|---|---|---|---|---|
| `/` (public) | 97 → 100 | 76 → 94 | 5.25 → 3.00 s | 0.117 → 0 | 774 → 354 KB |
| `/events` (public) | 79 → 100 | 61 → 96 | 4.50 → 2.62 s | 0.575 → 0 | 711 → 291 KB |
| `/events` | 79 → 100 | 62 → 96 | 4.35 → 2.62 s | 0.579 → 0.004 | 711 → 291 KB |
| `/events/54` | 84 → 100 | 62 → 98 | 4.35 → 2.25 s | 0.631 → 0.004 | 715 → 295 KB |
| `/messages` | 99 → 100 | 79 → 96 | 4.35 → 2.46 s | 0.137 → 0.029 | 705 → 285 KB |
| `/profile` | 79 → 100 | 62 → 98 | 4.35 → 2.25 s | 0.616 → 0.004 | 716 → 296 KB |
| `/members` | 80 → 100 | 61 → 96 | 4.35 → 2.47 s | 0.792 → 0.004 | 708 → 288 KB |
| `/rehearsals` | 88 → 100 | 67 → 98 | 4.35 → 2.25 s | 0.350 → 0.004 | 703 → 284 KB |
| `/logistics` | 87 → 100 | 66 → 98 | 4.35 → 2.25 s | 0.394 → 0.004 | 699 → 279 KB |
| `/logistics/2` | 84 → 100 | 61 → 98 | 4.35 → 2.25 s | 0.659 → 0.004 | 708 → 288 KB |
| `/naipes` | 89 → 100 | 63 → 98 | 4.35 → 2.25 s | 0.536 → 0.004 | 706 → 286 KB |

TBT is 0-30 ms everywhere, before and after. Logistics board, card status change (network settled): 2.1-2.3 s with
an R2 listing per change → 29-55 ms, board request only. Event Timing on that interaction: ≤ 64 ms.

## Deferred

- P2: member avatars are the cropper's 600×600 WebP shown at 32-72px (`/members` loads ~60); needs a server-side
  thumbnail variant (storage change), lazy loading is already in place.
- P2: `default-avatar.webp` 1024×1024 (73 KB). One-time per device (cached). (`favicon.ico` 128 KB and the icon PNG
  metadata were resolved by 046, the new logo: favicon 11 KB, `rtub-logo-352.webp` 34 KB.)
- P2: Fraunces italic (80 KB) loads on Home for one `<em>`; global CSS is 155 KB (28 KB gzip), one sheet.
- P3 / a11y: colour contrast of `.lx-status--todo` (3.48:1), `.agenda-card__year` (4.42:1), `.tuna-path__years`
  (3.89:1). (Home `.record img` 101×192: fixed in 046, `height: auto`.)
- Local-only, not real: Best Practices 89-93 = CSP blocking the production R2 media domain stored in the copied DB.

## Task 054 (2026-10-30): quick wins from the performance audit

Audit of `dev` @ `33f25f3e` (2026-10-29; host pipeline, EF / SQLite layer, hot services, React shell, service worker,
assets, publish, Azure settings). Where the time went: every request from a signed-in browser ran 3 SELECTs in the cookie
validator before its handler, multiplied by the shell's polls (10 s unread + 30 s announcement ≈ 36 SQL statements a
minute per idle member tab); read paths loading whole tables or full `AspNetUsers` rows; the write path doing audit,
member-status reconcile, push fan-out and SMTP inline in the request; assets (gallery originals, 600² avatars, both
Fraunces files on every route, compression at `Fastest`, a 142 KB hero logo, a 72 KB default avatar in the precache);
PROD Always On off and no ReadyToRun on a one-core B1. Owner decisions: the launch opening stays as designed (every
session), cookie validation is gated but not cached, a client router waits for measurements.

### What 054 changed

| Area | Change | Measured |
|---|---|---|
| Per request | `OnValidatePrincipal` asks the database about the Admin role only when the cookie claims it | 3 → 1 SELECT per signed-in request (non-admins); admins unchanged |
| Polling | unread badge 10 → 30 s; `/api/version` folded into `/api/account/me`; that request starts before React mounts | idle member tab 8 → 4 requests a minute; one request fewer per page load |
| `/images/*` | `ImagesController` retired; static-file middleware (ETag / 304, `no-cache`) before authentication | no cookie validation per image request |
| Compression | Brotli / gzip `Optimal` (q4 / level 6, were `Fastest`); `immutable` on `/portal/assets/*` | critical path ≈ 30 KB smaller per cold load (gzip-1 161 KB vs gzip-9 131 KB, measured on the built assets) |
| Service worker | `rtub-v2.11.0`: hashed assets cache-first (no revalidation fetch / cache write per navigation, ≈ 0.5 MB), navigation preload, no `respondWith` for `/api`, `/auth`, `/hubs`, `/health`, non-GET, non-http and the final pass-through; registered on `load` | worker start-up off the API path; the install's ≈ 150 KB precache no longer competes with the first paint |
| Fonts | both Fraunces files from `wwwroot/fonts/` (30-day cache) and preloaded from `index.html`; faces unchanged | same bytes, requested ≈ 1 RTT earlier |
| Images | hero logo: 512 px for 1x (58.6 KB) + the 1024 px (145.9 KB) for 2x, `fetchpriority="high"`; default avatar 1024² → 256² | 145.9 → 58.6 KB on 1x desktops; 73.7 → 6.5 KB on every install |
| Entry chunk | `call` moved to `api.ts` (the unread badge no longer drags the events client in) | 86.1 → 81.3 KB (29.6 → 28.3 KB gzip) |
| Queries | event counts by SQL `GROUP BY` (every WillAttend / repertoire / post row streamed before); public preview `Take`s in SQL; event page not-going count from the "Quem vai" rows; gallery facets on page 1 only; `ToLookup` in the member-status read path; 20 undisposed `CreateDbContext()` sites `await using`; one pooled `HttpClient` for WebPush (a client per request scope before); member-status reconcile skips the boot run while the table has rows | `/events`: no row streaming; `/gallery` "Mostrar mais": 3 queries fewer; boot: ≈ 2 commands per member fewer |
| `/events` page | search debounced, `history.replaceState` out of the state updater (Safari's 100-per-30 s limit); event hero `aspect-ratio: 3 / 2` | no CLS from the hero on phones |
| Build | `-p:PublishReadyToRun=true` on the linux-x64 publish | cold start no longer JIT-compiles the whole app (B1, one core) |

Not re-measured with Lighthouse in 054; the 045 table above stays the baseline until the next gate run. Nothing of the
intro (splash markup, `.splash*` / `:root.intro` rules, `revealApp`, the italic face it uses) changed.

### Deferred findings (next tasks)

Severity: H every request / page / unbounded growth, M one main page or write, L rare. *verified* = read in source.

| # | Sev | Finding | Where | Fix |
|---|---|---|---|---|
| A3 | H | Unread count = instr scan on `Conversations.Participants` + COUNT over all messages with `!ReadBy.Contains(me)` (substring list, unindexable), per poll. *verified* | `MessagingService.cs` `GetUnreadAsync` | per-user read watermark (`ConversationUserSettings`, additive column; confirm against `docs/messages-removal.md`) → indexed COUNT |
| A4 | H | Messages list: GROUP BY MAX(Id) over all my messages + a second query although `Conversation.LastMessageId` is kept on send. *verified* | `MessagingService.cs` `ListItemsAsync` | join on `LastMessageId`, MAX only where null |
| A5 | H | Thread poll (10 s) reloads the whole page: `Include(Sender)` full rows, list item recomputed, people twice; `ORDER BY Id DESC` sorts in a temp B-tree. *verified* | `MessagingService.cs` `DetailAsync`, `Messages.tsx` | `?after=<lastId>` delta poll, projected sender, index `(ConversationId, Id)` |
| B2 | M | `/api/members`: whole `AspNetUsers` as entities, all `MemberInstruments`, whole `RoleAssignments` cut in memory, re-run per keystroke. *verified* | `MemberDirectoryService.cs`, `Members.tsx` | project columns, `Where(StartYear)` in SQL, search client-side (the whole directory is returned anyway) |
| B3 | M | Leaderboard recomputes every member's XP per request and per search keystroke; two redundant aggregates. | `LeaderboardService.cs`, `RankingService.cs`, `Leaderboard.tsx` | search client-side; 60 s cache per fiscal year |
| B4 | M | `/profile` ≈ 14 queries (own row 3×, mentor / instruments 2×, whole `RoleAssignments`). | `MyProfileService.cs` | load once, pass down |
| B5 | M | Meetings: `GetAllMeetingsAsync(…, int.MaxValue)` + two full user rows, FY / search in memory; `VisibleMeetingAsync` loads all meetings to find one. | `MeetingBoardService.cs`, `MeetingService.cs` | filter / page in SQL, `FirstOrDefaultAsync` |
| B6 | M | Naipes board: `Include(Comments)` + `Include(PlayCounts)` (every play ever) to compute two counts; type configs re-read per GET. | `NaipeContentRepository.cs`, `NaipeBoardService.cs` | project the counts (DTO change) |
| B7 | M | Members map: one `GeocodingCaches` query per distinct city (disposal fixed in 054). | `MemberMapService.cs` | load the table once |
| B10 | M | Hall of Fame: all users + whole `RoleAssignments` O(U×A) + entire `Enrollments` with `Include(Event)`. | `HallOfFameService.cs` | SQL GROUP BY, 10 min cache |
| B11 | M | Rehearsals agenda: GROUP BY over the whole `RehearsalAttendances` table per load; detail full user rows. | `RehearsalAgendaService.cs` | season window, projected attendees |
| B12 | L | Questions list includes every reply body + author to count replies. | `QuestionRepository.cs` | project `Replies.Count()` |
| B14 | L | Owner / admin lists in memory (`/users`, `/requests`, treasury, audit-log facets + `Changes.Contains`, report history per-row loads, logistics R2 listing per open). | various | per page when touched |
| C2 | H | Push fan-out inline in the member's request: sequential sends with 0.6 / 1.8 / 5.4 s back-off sleeps on 429. *verified* | `PushNotificationService.cs`, callers in enrollment / attendance / messaging / gallery | `Channel<PushJob>` + one `BackgroundService`, bounded concurrency; query only the selected users' subscriptions |
| C3 | M | Audited `SaveChangesAsync`: name-lookup SELECTs, the write, then a second write transaction for `AuditLogs`; 4 change-tracker scans. *verified* | `ApplicationDbContext.cs` | one transaction, single `DetectChanges` (security-relevant: own commit, full suite) |
| C4 | M | Event / rehearsal answer ≈ 30-38 commands (`LoadEnrollmentAsync` twice, inline reconcile, inline push). | `EventAgendaService.cs`, `EnrollmentService.cs`, `MemberStatusService.cs` | after C2: reload once, reconcile on the channel |
| C5 | M | Bulk email inside the admin request; SMTP serialised; a new `SmtpClient` per message. | `EmailNotificationService.cs` | second lane on the C2 channel, or accept |
| C6 | L | `AddMemoryCache` without `SizeLimit`; ata / report PDFs cached 1 h unbounded on a 1.75 GB B1. | `ServiceCollectionExtensions.cs`, `AtaPdfService.cs`, `ReportPdfService.cs` | `SizeLimit` + `Size` |
| D1 | H | Gallery tiles (24 / page) and the Home preview (5) load R2 originals (≤ 10 MB); `ThumbnailUrl` never generated; no dimensions → masonry reflow. *verified* | `Gallery.tsx`, `Home.tsx`, `GalleryManage.tsx` | client-side resize at upload (reuse `Cropper.tsx`): main ≤ 1600 px + 480 px thumb + width / height (schema) |
| D8 | M | One 184 KB / 14,377-line stylesheet on every page. *verified* | `styles.css` | coverage pass, then per-area CSS imported by the lazy chunks (the splash / intro rules stay in the main sheet) |
| D9 | M | Route waterfall: HTML → entry → `import()` chunk → mount → first API call. | `main.tsx`, `Program.cs` | `modulepreload` for the route's chunk from Vite's manifest |
| D11 | M | Avatars 600² shown at 24-72 px everywhere. *verified* | `membersApi.ts`, `MemberShell.tsx` | 128 px variant at crop time + DTOs |
| D13-15 | L | `icons.tsx` ships all 89 paths; `musicApi.ts` duplicates `call()`; management dialogs in public chunks; `rtub-logo-maskable-512.png` 254 KB (manifest only). | | when touched |
| E1 | H | PROD Always On off: cold start after 20 min idle. | Azure | owner action (also needed by the daily backup) |
| E3 | L | HTTP/2 state of both App Services undocumented. | Azure | `az webapp config show --query http20Enabled` |
| F3 | L | `.Date.Date` filters bypass the date indexes; `PushSubscriptions.Endpoint`, `AuditLogs.Action` unindexed. | repositories, reminder jobs | range compares; two indexes |
| – | – | Compression beyond `Optimal`: build-time precompression (Node `zlib`) + sibling-serving middleware, or `MapStaticAssets` (serves Vite-hashed names as `no-cache` + ETag unless overridden). | | spike after measuring |
| – | – | Client router (every link is a full navigation); cached cookie validation. | | owner: measure first / declined |

Looks bad but is fine: R2 uploads already carry `Cache-Control: public, max-age=31536000, immutable`; global
`SplitQuery` is right for SQLite; the boot cost is the JIT and the reconcile, not seeding (`SeedData` returns after one
`AnyAsync`); the PRAGMA batch per connection open costs microseconds (kept: Microsoft.Data.Sqlite's command timeout is a
polling loop, not `sqlite3_busy_timeout`); `UseResponseCaching` does store the static files it may.
