# React Classificação (`/leaderboard`)

"Tabela de Classificação": `portal/src/Leaderboard.tsx` over `/api/leaderboard` (`Endpoints/LeaderboardEndpoints.cs` > `ILeaderboardService`). No stored score and no schema: XP is computed on the fly from attendance and configuration.
Code: `Helpers/LeaderboardAuthorization.cs`, `Services/LeaderboardService.cs`, `RankingService`, `LeaderboardCommentService`.

| Route | Who |
| --- | --- |
| `/leaderboard` (`?fiscalYear=2025-2026`) | signed-in members; visitors `302` to `/login?returnUrl=%2Fleaderboard` |

Push notifications (first place, comment, like) all open this URL.

## Scoring

- `RankingService` adds `Ranking:XpPerRehearsal` (15) per attended rehearsal dated before today (UTC), and `Ranking:XpPerEventType` per event the member answered "vai" whose end date is before today (Festival 100, Aniversário 85, Nerba 60, Casamento/Batizado/Missa/Atuação 50, Arruada 45, Serenata 30, Convívio 25, Arraial 20; types without a value give 0).
- Level: the highest `Ranking:Levels` threshold reached (12 levels, "Lei Seca" 0 XP to "Rei da noite" 3500 XP). Configuration lives in `appsettings.json`; nothing edits it. `ApplicationUser.ExperiencePoints/Level` are not read here.
- Who is listed: every account in the users table, with no category, retired or expelled filter.
- Order: level, then XP, both descending; ties keep the users table's order (SQLite does not guarantee it). Positions 1..n are assigned before any search, so a search keeps each row's place. Top three get medals.
- Fiscal year: "Todos os anos" by default; years from 2025-2026, newest first, current marked "(ATUAL)"; 1 September to 31 August, still only before today. Rehearsal and event counts follow the year. Unknown year or member is refused (400 / 404), never a fallback.
- Search runs on the server (`?q=`): nickname, first name, last name or rank name, case-insensitive, accents significant. "Mostrar mais" adds 20 rows at a time.
- Details ("Detalhes da Classificação"): level and progress of the selected year, XP total and origin (counts x XP), the activity list of all time, and the rank card (current level, next level, XP to go).
- "Níveis de Alcoolismo" card, open by default: explanation, then the 12 levels (number, name, threshold); 1 column on phones, then 2 / 3 / 4 balanced columns. The reader's level for the year shown is marked "O teu nível" (from `me`, the reader's own row whatever the search; null when not listed).
- The story text above the levels is fixed in code (`LeaderboardService.Story`); nothing edits it. The `Labels` table is untouched (see [db-cleanup-inventory](db-cleanup-inventory.md)).

## Comments

`LeaderboardComment`, `LeaderboardCommentLike`. Any signed-in member reads and writes on any row (own included); text trimmed, 1-1000 characters; newest first; no edit. Likes toggle (with who liked); a deleted comment cannot be liked. The author or Admin/Owner soft-deletes. A comment pushes to the member commented on (not on one's own row); a like pushes to the comment's author (not on one's own comment).

## Permissions

| Who | Table, details, comment, like, delete own | Delete any comment |
| --- | --- | --- |
| Visitor | `401` | `401` |
| Member, Mod | yes | `403` |
| Admin, Owner | yes | yes |

## API (GET `no-store`; writes need `X-CSRF-TOKEN`)

| Endpoint | Does |
| --- | --- |
| `GET /api/leaderboard?fiscalYear=&q=` | `{ fiscalYears, fiscalYear, total, entries, levels, story, me }`; unknown year 400 |
| `GET /api/leaderboard/members/{id}?fiscalYear=` | the details; 404 when unknown |
| `GET /api/leaderboard/members/{id}/comments` | comments, newest first (author name and avatar, no ids) |
| `POST /api/leaderboard/members/{id}/comments` | `{ text }`; the new list; 400 empty / over 1000 |
| `POST /api/leaderboard/comments/{id}/like` | toggles; `{ liked }`; 404 when deleted |
| `DELETE /api/leaderboard/comments/{id}` | 204; 403 unless author or Admin/Owner |

## Follow-ups

- `XpSettings` and `RankingConfiguration` both bind the `Ranking` section (duplication).
