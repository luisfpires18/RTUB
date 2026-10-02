# React Classificação (`/leaderboard`, React track 019)

`/leaderboard` ("Tabela de Classificação") is React (`portal/src/Leaderboard.tsx`) over `/api/leaderboard`
(`Endpoints/LeaderboardEndpoints.cs` → `ILeaderboardService`), replacing the Blazor `Pages/Activities/Leaderboard.razor`.
DEV only; **no schema change**, no migration: XP is still computed on the fly from attendance and configuration.

| Route | Owner after 019 |
| --- | --- |
| `/leaderboard` (`?fiscalYear=2025-2026`) | React, signed-in members (visitors: 302 to `/login?returnUrl=%2Fleaderboard`) |

Links: the Blazor `MainLayout` nav ("Classificação"), the React `/profile` actions, and the push notifications (first
place, comment, like), all to the same URL.

## Audit (old page, 932 lines, `[Authorize]`)

- **Data**: no stored score. `RankingService` adds `Ranking:XpPerRehearsal` (15) per attended rehearsal dated before
  today (UTC) and `Ranking:XpPerEventType` per event the member answered "vai" whose (end) date is before today
  (Festival 100, Aniversário 85, Nerba 60, Casamento/Batizado/Missa/Atuação 50, Arruada 45, Serenata 30, Convívio 25,
  Arraial 20; types without a value give 0). The level is the highest `Ranking:Levels` threshold reached (12 levels,
  "Lei Seca" 0 XP … "Rei da noite" 3500 XP). Configuration lives in `appsettings.json`; the page never edited it.
  `ApplicationUser.ExperiencePoints/Level` are not read here.
- **Who**: every account in the users table: no category, retired or expelled filter (Leitões, Tunos Honorários,
  expelled and role-less accounts are all listed). Kept.
- **Order**: level, then XP, both descending; equal rows keep the users table's order; positions 1..n assigned before
  any search (a search keeps each row's place). Top three shown with medals.
- **Filters**: search (client-side before; nickname, first name, last name or rank name, case-insensitive, accents
  significant) and fiscal year ("Todos os anos" default; the years from 2025-2026, newest first, the current one marked
  "(ATUAL)"; 1 September - 31 August, still only before today). The rehearsal and event counts follow the year.
- **Row**: position, avatar, nickname, full name, "Ensaios"/"Atuações" attended, "Nível N – rank", XP.
- **Details** ("Detalhes da Classificação"): level and progress of the selected year (as the row), XP total and origin
  (rehearsals count × XP, each event type count × XP) and the activity list **of all time** (the old modal always used
  today), the rank card (current level, next level, XP to go, progress).
- **Comments** (`LeaderboardComment`, `LeaderboardCommentLike`): any signed-in member reads and writes on anyone's row
  (own included); text trimmed, 1-1000 characters; newest first; likes toggle (with who liked); the author or
  Admin/Owner soft-deletes (with confirmation); no edit. A comment pushes to the member commented on (not on one's own
  row); a like pushes to the comment's author (not on one's own comment).
- **Configuration**: only the `ranking_story` label (title ≤ 200, content ≤ 5000, both required, active flag) above the
  levels, edit button for Admin (UI-only `AuthorizeView Roles="Admin"`), shown only while active. Levels and XP values
  are read-only configuration.
- **Tests before**: bUnit `LeaderboardPageTests` (title, loading, empty state; retired with the page); service tests for
  `RankingService`, `MemberStatisticsService`, `LeaderboardCommentService` are unchanged.

## Rules now (`LeaderboardAuthorization`, server-side)

| Who | Table, details, comments, comment, like, delete own | Delete any comment, edit the explanation |
| --- | --- | --- |
| Visitor | `401` (page: 302 to sign in) | `401` |
| Member, Mod | yes | `403` |
| Admin, Owner | yes | yes (Owner inherits Admin; the old edit button was Admin only) |

## API (GET `no-store`; writes need `X-CSRF-TOKEN`)

| Endpoint | Does |
| --- | --- |
| `GET /api/leaderboard?fiscalYear=&q=` | `{ fiscalYears, fiscalYear, total, entries, levels, story, canEditStory }`; unknown year → 400 |
| `GET /api/leaderboard/members/{id}?fiscalYear=` | the details; 404 when unknown |
| `GET /api/leaderboard/members/{id}/comments` | the comments, newest first (author name and avatar, no ids) |
| `POST /api/leaderboard/members/{id}/comments` | `{ text }` → the new list; 400 empty / over 1000 |
| `POST /api/leaderboard/comments/{id}/like` | toggles → `{ liked }`; 404 when deleted |
| `DELETE /api/leaderboard/comments/{id}` | 204; 403 unless author or Admin/Owner |
| `PUT /api/leaderboard/story` | `{ title, content, isActive }`, Admin/Owner |

## Changed on purpose

- The search runs on the server (`?q=`), same fields and positions; "Mostrar mais" (20 at a time) instead of numbered
  pages.
- The levels are a plain list instead of the snake-shaped grid with connectors.
- A deleted comment can no longer be liked (the old repository allowed it, the UI never offered it); unknown members
  and fiscal years are refused instead of falling back.

## Follow-ups

- `LeaderboardCard`, `LeaderboardCommentItem` (and its CSS) in `RTUB.Shared` have no caller since the page went;
  `RankCard` is still used by the Blazor profile. Not removed here.
- `XpSettings` and `RankingConfiguration` both bind the `Ranking` section (pre-existing duplication).
- Equal level and XP keep the users table's order, which SQLite does not guarantee (as before).
