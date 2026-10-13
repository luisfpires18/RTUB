# React profile (`/profile`), members map (`/members/map`) and Hall of Fame (`/hall-of-fame`), task 032

Three member-area pages are React (`portal/src/Profile.tsx`, `portal/src/MembersMap.tsx`, `portal/src/HallOfFame.tsx`,
client `portal/src/memberAreaApi.ts`) over `Endpoints/MemberAreaEndpoints.cs` → `IMyProfileService`, `IMemberMapService`
and `IHallOfFameService`, replacing the Blazor `Pages/Members/Profile.razor`, `Pages/Members/MemberMap.razor` and
`Pages/Activities/HallOfFame.razor`. DEV only; **no schema change**, no migration.

| Route | Owner after 032 |
| --- | --- |
| `/profile` | React. Signed in: the member's own profile and editor. Visitor: the "área reservada" card and the sign-in link (as since 004) |
| `/member/profile` | 302 → `/profile` (for everyone) |
| `/members/map` | React, signed-in members (visitors: 302 to `/login?returnUrl=%2Fmembers%2Fmap`); Leitões included |
| `/member/map` | 302 → `/members/map` |
| `/hall-of-fame` | React, signed-in members (visitors: 302 to `/login?returnUrl=%2Fhall-of-fame`) |

Links: the member menu (`MemberShell.tsx`, Tuna group: "Mapa de membros" and "Hall of Fame"; Membro group: "Perfil"),
`/members` → "Mapa", `/leaderboard` → "Hall of Fame", `/profile` → "Ver a classificação". The Blazor `MainLayout` links
`/hall-of-fame` and `/profile` as plain full-page links.

## Audit (old pages)

**`/hall-of-fame`** (918 lines, `[Authorize]`): twelve panels, every account counted (no category or expelled filter):
Última passagem a Caloiro / a Tuno (latest month-year pair, "MMMM yyyy" in pt-PT), Mais afilhados (members naming them
as padrinho), Mais cargos (role assignments), Mais vezes Magister, Mais instrumentos, Mais / Menos tempo a Leitão
(Leitão → Caloiro months) and a Caloiro (Caloiro → Tuno), Mais ensaios (attended, past, not-cancelled rehearsals), Mais
atuações ("vou" on past, not-cancelled events, `EndDate ?? Date`). Ties: every member sharing the value. Durations count
only when positive. Counts need at least one. No winner → "Sem dados suficientes".

**`/member/map`** (377 lines + `memberMap.js`, `[Authorize]`, Leitões included): every account; grouped by the profile's
city text (trimmed, case-insensitive); coordinates only from the geocoding cache (`CachedGeocodingService`: a miss is
queued for `BackgroundGeocodingWorker` and listed as "Em fila para geocodificação"); members without a city listed;
dark CARTO tiles; a popup per city with up to 10 members (name, full name, avatar). Leaflet and the map script were
loaded on **every** Blazor page from `MainLayout` (unpkg + SRI). The popup built HTML strings from member names.

**`/member/profile`** (1434 lines + `ProfileHeader`, `ProfileSection`, `ProfileField`, `RankCard`, `UnifiedTimeline`,
`MonthYearPicker`, `ImageCropper` (Cropper.js from cdnjs), `PushNotificationToggle`):
- Header: avatar, nickname / full name, category and position badges, "Carregar Foto", "Alterar Palavra-passe";
  a warning when `RequirePasswordChange`.
- Rank card (level, rank name, XP, progress to the next level). Pessoal: names, nickname, email, birth date (+ age),
  phone, city, degree (the entity's own annotations: names, nickname, email, phone required). The nickname input was
  disabled for a member who is only a Leitão (UI only).
- Tuna: instruments (hidden from view for a Tuno Honorário; add / remove / primary saved at once), padrinho (hidden for
  Leitões and Fundadores; eligible = category Tuno, never yourself), Leitão / Caloiro / Tuno dates (none for a Fundador;
  Caloiro once past Leitão; Tuno once Tuno or above); a complete pair was disabled and the save ignored a change to it.
  Percurso na Tuna (timeline) and Estado na Tuna (active / retired, progress, last rehearsal / event).
- Photo: up to 10 MB, cropped 1:1 to WebP, `UserProfileService.UpdateProfilePictureAsync` (deletes the old image, stores
  under the member's `profile` folder by username). Email notifications on / off (`Subscribed`). Password: current, new
  (8-100), confirmation; clears `RequirePasswordChange`. The session was **not** refreshed: RTUB checks the security
  stamp on every request, so the member was signed out on the next page.
- The push toggle (`PushNotificationToggle`) lived only here.

## Rules now (server-side)

| Who | `/api/me/*` | `/api/members/map` | `/api/hall-of-fame` |
| --- | --- | --- | --- |
| Visitor | 401 | 401 | 401 |
| Any signed-in member (Leitões included) | their own profile only | yes | yes |

There is no id in any `/api/me` route: the member is the session's. Writes need `X-CSRF-TOKEN` (400 without).

- **Pessoal** (`MyProfileService.UpdatePersonalAsync`): the entity's annotations; values trimmed (blank → empty);
  birth date 1900-today; a member who is **only a Leitão cannot change the nickname** (400 `nickname`); an email another
  account uses → 400 `email` ("Este email já está associado a outra conta."). The username does not follow the nickname
  (as before).
- **Tuna** (`UpdateTunaAsync`): the padrinho must be a Tuno or above (`CanBeMentor`) and never yourself (400
  `mentorId`); shown when not a Leitão, a Fundador or a Tuno Honorário. Dates: shown as before; a **complete pair is
  locked** (a change → 400 with "Não podes alterar as datas…"); an open pair takes a month/year between 1990 and this
  year. Pairs the member cannot see are never changed.
- **Instruments**: add (first = primary; duplicate / unknown → 400), remove (the primary's removal promotes the next),
  primary; another member's instrument id → 404.
- **Photo** (`POST /api/me/photo`, multipart `photo`): WebP, JPEG or PNG, by type **and** magic bytes, at most 10 MB;
  stored by `UserProfileService` exactly as before (old image deleted, same R2 profile folder).
- **Email notifications**: `PUT /api/me/subscription`.
- **Password**: current required, new 8-100 and confirmed; clears `RequirePasswordChange`; then the endpoint calls
  `SignInManager.RefreshSignInAsync`, so the member stays signed in.
- **Map**: city-level only (name, avatar, full name; no id, email, phone, address or per-member coordinate). Every
  account, expelled ones included, as before (see *Follow-ups*).
- **Hall of Fame**: the old calculations, moved to `HallOfFameService`, unchanged.

## API (GET `no-store`; writes need `X-CSRF-TOKEN`)

| Endpoint | Does |
| --- | --- |
| `GET /api/me/profile` | `{ member (the directory's MemberDetail), personal, tuna, instruments, instrumentOptions, subscribed, requirePasswordChange, rank }` |
| `PUT /api/me/profile/personal` | `{ firstName, lastName, nickname, email, phoneNumber, dateOfBirth, city, degree }` → the profile |
| `PUT /api/me/profile/tuna` | `{ mentorId, yearLeitao, monthLeitao, yearCaloiro, monthCaloiro, yearTuno, monthTuno }` → the profile |
| `GET /api/me/mentors?q=` | padrinho candidates (Tunos and above, not yourself), at most 20 |
| `POST /api/me/instruments` | `{ instrument }` → the instruments |
| `DELETE /api/me/instruments/{id}` | → the instruments |
| `PUT /api/me/instruments/{id}/primary` | → the instruments |
| `POST /api/me/photo` | multipart `photo` → `{ avatarUrl }` |
| `PUT /api/me/subscription` | `{ subscribed }` → `{ subscribed }` |
| `POST /api/me/password` | `{ currentPassword, newPassword, confirmPassword }` → 204 and a refreshed sign-in cookie |
| `GET /api/members/map` | `{ total, cities: [{ name, latitude, longitude, members }], withoutCity, pending }` |
| `GET /api/hall-of-fame` | `{ categories: [{ key, title, winners, value }] }`, the twelve keys in the old order; `value` null when nobody qualifies |

## Changed on purpose

- **The session survives a password change** (refreshed sign-in); before, the next request signed the member out.
- **Rules are enforced by the server**: the only-Leitão nickname lock and the locked date pairs now refuse with a message
  (before: a disabled input, and a silent ignore of a changed locked pair).
- **An email in use** gets a clear message (before: the save failed silently). Values are trimmed.
- **The padrinho must be Tuno or above** (`CanBeMentor`, as the member admin); before: category Tuno. A Tuno Honorário
  no longer edits a padrinho (hidden from view before, cleared by the member admin anyway).
- **Photo bytes are checked** (magic bytes), not only the declared type; JPEG/PNG are accepted where the browser cannot
  encode WebP (the React cropper's fallback).
- **The map loads Leaflet only on `/members/map`** (npm `leaflet`, its own `vendor-leaflet` chunk), not on every Blazor
  page; popups are DOM nodes with names as text, so no member data is ever written as markup.
- **Basemap (task 040):** OpenStreetMap standard tiles (`https://tile.openstreetmap.org/{z}/{x}/{y}.png`, attribution
  "© OpenStreetMap contributors"), no API key; darkened by a CSS filter on the tile pane only. CARTO's dark tiles
  (`basemaps.cartocdn.com`) started showing "API KEY REQUIRED" and were dropped. The URL is one constant in
  `MembersMap.tsx`; its host is the only tile host in the CSP `img-src`.
- `/member/profile` redirects for everyone, including visitors (before: 302 to sign in).

## Retired

Blazor: `Profile.razor`, `MemberMap.razor`, `HallOfFame.razor` (+ CSS); `ProfileHeader`, `ProfileSection`, `RankCard`,
`UnifiedTimeline`, `MonthYearPicker`, `ImageCropper`, `PushNotificationToggle` (+ CSS); `wwwroot/js/memberMap.js`,
`wwwroot/js/imageCropper.js`, `css/4-pages/member-map.css`, `css/3-components/month-year-picker.css`; the unpkg Leaflet
and cdnjs Cropper.js tags (and the cdnjs preconnect) in `MainLayout`. Tests: their bUnit tests and the placeholder
`Integration.Tests/Pages/ProfilePageTests.cs`. Kept: `ProfileField` (used by Naipes; retired with it in 033), `GeocodingCache`, the geocoding
queue and worker, `push-notifications.js`, `PushNotificationPrompt`, every push endpoint and sender.

## Push notifications

Not part of 032 (the only push toggle was on the retired Blazor profile). Since **task 038** the React `/profile` has
its own section, "Notificações push", after "Notificações por email": per-device opt-in / opt-out, every repair state,
no prompt on load. Detail: `docs/react-push-v2.md`.

## Data audit

No schema change. Writes touch only the signed-in member's `AspNetUsers` row (names, nickname, email + normalized,
phone, birth date, city, degree, padrinho, dates, `Subscribed`, `ImageUrl`, password hash / security stamp,
`RequirePasswordChange`) and their `MemberInstruments`; R2: the member's profile image (old one deleted), as before.
The map and the Hall of Fame are read-only (a map read can queue a city for geocoding, as before).

## Tests

`Integration.Tests/Api/MemberAreaApiTests.cs`: profile (visitors 401, own profile only, validation, email in use,
only-Leitão nickname, locked dates, padrinho rules and search, instruments, photo type / bytes / size and old-photo
deletion on the fake storage, email toggle, password checks + `RequirePasswordChange` + the session kept, antiforgery),
map (visitors, Leitões read, grouping, without city, pending, no contact fields, expelled kept), Hall of Fame (visitors,
the twelve keys, ties, positive durations, past / not-cancelled activity, empty records) and the retirement sweep.
Route tests in `PortalRouteTests` and `MemberPagesTests`; menu contract in `MemberShellTests`; the map's popup contract
in `ContentSecurityPolicyTests`.

## Follow-ups (not done here)

- The map counts expelled accounts (and the Hall of Fame every account), as before: decide whether to leave them out.
- (resolved in 042) `script-src` / `style-src` are `'self'` only: the unused cdnjs and unpkg sources are gone.
- (resolved in 037) `wwwroot/js/profilePictureRefresh.js`, `ProfilePictureUpdateService` and `wwwroot/lib/cropperjs` were
  deleted with the Blazor host.
- (resolved in 038) Push v2: React opt-in / opt-out and the push cleanup (`docs/react-push-v2.md`).

## Profile polish, batch 3 (task 044)
React only, no API / schema change: everything comes from `GET /api/profile` (`MyProfile`: `member.timeline`, `member.state`,
`tuna`, `instruments`).
- Header: the avatar (with the change-photo button on its corner: same pick, square crop and upload as before) lives in the
  identity card; the standalone "Foto" section and "Entraste como …" are gone. Title is a plain `page__title`.
- "Informação da Tuna" (was "Tuna"): Instrumentos (read-only; editing stays in the "Instrumentos" section below), Padrinho,
  **Percurso na Tuna** (`TunaPath`: connected milestone cards from the server's `member.timeline`, `accent` = top-edge
  colour, `state` = current steps; a row from 700px, a column on a phone), Estado na Tuna (status badge, the server's
  progress text / bar, Último ensaio, Última atuação, the activities list). Edit still opens the padrinho / dates form; the
  month-level dates are no longer listed read-only (the timeline shows years; the form still has the months).
- Badges: one `MemberBadge` / `.member-badge` (dark pill, light uppercase text, thin outline) for every page, header chip
  included. Membership kinds and positions are not colours any more; only `--active` (purple outline) and `--retired` (muted).
- "Terminar sessão" uses `btn btn--ghost btn--sm` (`SignOutButton` takes `buttonClassName`); "As minhas inscrições" left /profile in 044 (the page is gone).

### Batch 4 (task 044)
- Header: `[inbox] [category badge] [profile chip] [menu]`. The badge (`MemberCategory`, first of `/api/account/me` `categories`) is its
  own control, the shared `MemberBadge` with the kind derived from the label (`badgeKind`); the chip is avatar + name only.
  On phones the tagline is hidden so the four controls fit.
- `MemberBadge` takes `kind` (tuno, veterano, tunossauro, tunohonorario, fundador, caloiro, leitao, position, active, retired):
  the original per-kind colours are back everywhere (Batch 3 had flattened them); veterano / tunossauro gain a lavender outline.
- Percurso: `TunaPath` variant from the server's `accent` (`role` / `magister` → `position`): leitão grey, caloiro light, position
  purple with a star marker, tuno black, veterano / tunossauro dark with a purple outline; thin neutral rail, markers on it; a
  row from 700px (six milestones fit at 1440px), a column with a vertical rail on phones.
- Informação da Tuna is flat: label / value rows, ruled blocks (Percurso, Estado), no nested cards.
