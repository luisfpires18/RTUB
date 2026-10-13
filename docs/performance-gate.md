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
