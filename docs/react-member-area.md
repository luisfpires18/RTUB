# React profile (`/profile`), members map (`/members/map`) and Hall of Fame (`/hall-of-fame`)

React: `portal/src/Profile.tsx`, `MembersMap.tsx`, `HallOfFame.tsx`, client `memberAreaApi.ts`. Code:
`Endpoints/MemberAreaEndpoints.cs` → `IMyProfileService` (`MyProfileService`), `IMemberMapService`,
`IHallOfFameService` (`HallOfFameService`); photo storage in `UserProfileService.UpdateProfilePictureAsync`.

| Route | Behavior |
| --- | --- |
| `/profile` | signed in: the member's own profile and editor; visitor: the "área reservada" card and sign-in link |
| `/member/profile` | 302 to `/profile` (everyone) |
| `/members/map` | signed-in members (visitors: 302 to `/login?returnUrl=%2Fmembers%2Fmap`); Leitões included |
| `/member/map` | 302 to `/members/map` |
| `/hall-of-fame` | signed-in members (visitors: 302 to `/login?returnUrl=%2Fhall-of-fame`) |

Links: the member menu (`MemberShell.tsx`: "Mapa de membros" and "Hall of Fame" in Tuna, "Perfil" in Membro), `/members` →
"Mapa", `/leaderboard` → "Hall of Fame", `/profile` → "Ver a classificação".

## Permissions

Enforced server-side.

| Who | `/api/me/*` | `/api/members/map` | `/api/hall-of-fame` |
| --- | --- | --- | --- |
| Visitor | 401 | 401 | 401 |
| Any signed-in member (Leitão included) | their own profile only | yes | yes |

No role gives extra rights here. There is no id in any `/api/me` route: the member is the session's. A member who is
only a Leitão cannot change the nickname. Writes need `X-CSRF-TOKEN` (400 without).

## API (GET `no-store`; writes need `X-CSRF-TOKEN`)

| Endpoint | Does |
| --- | --- |
| `GET /api/me/profile` | `{ member (the directory's MemberDetail), personal, tuna, instruments, instrumentOptions, instrumentPictures, subscribed, requirePasswordChange, rank }`; `tuna.mentorAvatarUrl` is the padrinho's safe avatar URL or null; `instrumentPictures` maps instrument to its Naipes picture URL (`NaipeTypeConfigs`) |
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
| `GET /api/hall-of-fame` | `{ categories: [{ key, title, winners, value }] }`, twelve keys; `value` null when nobody qualifies |

## Profile rules

- Pessoal (`UpdatePersonalAsync`): the entity's annotations (names, nickname, email, phone required); values trimmed
  (blank becomes empty); birth date 1900-today; an only-Leitão changing the nickname is 400 `nickname`; an email another
  account uses is 400 `email` ("Este email já está associado a outra conta."). The username does not follow the nickname.
- Tuna (`UpdateTunaAsync`): the padrinho must be Tuno or above (`CanBeMentor`) and never yourself (400 `mentorId`);
  shown when not a Leitão, a Fundador or a Tuno Honorário. Dates: none for a Fundador; Caloiro once past Leitão; Tuno
  once Tuno or above. A complete pair is locked (a change is 400 "Não podes alterar as datas..."); an open pair takes a
  month/year between 1990 and this year. Pairs the member cannot see are never changed.
- Instruments: hidden for a Tuno Honorário; add (first = primary; duplicate / unknown is 400), remove (removing the
  primary promotes the next), primary; another member's instrument id is 404.
- Photo (`POST /api/me/photo`): WebP, JPEG or PNG, checked by declared type and magic bytes, at most 10 MB; the React
  cropper (1:1, WebP) falls back to JPEG/PNG where the browser cannot encode WebP. Stored by `UserProfileService` under
  the member's `profile` folder by username; the old image is deleted.
- Email notifications: `Subscribed` via `PUT /api/me/subscription`.
- Password: current required, new 8-100 and confirmed; clears `RequirePasswordChange`; then
  `SignInManager.RefreshSignInAsync` keeps the member signed in (RTUB checks the security stamp on every request).
- Writes touch only the signed-in member's `AspNetUsers` row and `MemberInstruments`, plus the R2 profile image.
- Push opt-in lives in the "Notificações push" section after "Notificações por email": [react-push-v2](react-push-v2.md).
  Face ID / fingerprint sign-in is enrolled here too: [react-identity](react-identity.md) → *Passkeys*.

## Profile UI

All from `GET /api/me/profile` (`member.timeline`, `member.state`, `tuna`, `instruments`).

- Header: `[inbox] [category badge] [profile chip] [menu]`. The badge (`MemberCategory`, first of `/api/account/me`
  `categories`) is the shared `MemberBadge`, kind derived from the label (`badgeKind`); the chip is avatar + name only.
  On phones the tagline is hidden. The avatar (with the change-photo button on its corner) lives in the identity card.
  "Terminar sessão" uses `btn btn--ghost btn--sm` (`SignOutButton` takes `buttonClassName`).
- `MemberBadge` / `.member-badge` kinds: tuno, veterano, tunossauro, tunohonorario, fundador, caloiro, leitao, position,
  active, retired (per-kind colours; veterano / tunossauro with a lavender outline; `--active` purple outline, `--retired`
  muted).
- Pessoal: one tile per value. Informação da Tuna is flat (label / value rows, ruled blocks, no nested cards):
  instrument chips with their Naipes picture (primary first), padrinho → member avatars with an arrow, Percurso na Tuna,
  Estado na Tuna (status badge, the server's progress text / bar, Último ensaio / Última atuação tiles, activities list).
  Edit opens the padrinho / dates form (months are only in the form; the timeline shows years).
- Percurso na Tuna (`TunaPath`): connected milestone cards from `member.timeline`; variant from the server's `accent`
  (`role` / `magister` → `position`): leitão grey, caloiro light, position purple with a star marker, tuno black,
  veterano / tunossauro dark with a purple outline; a row from 700 px, a column with a vertical rail on phones.
- Instrumentos: a card per instrument (pick principal, remove) and the free ones as picture buttons to add (no select).
- The shared `ActivityList` (profile + member details) is a dated feed: date chip per item, events gold, rehearsals
  purple, scrolls inside past ~560 px.

## Members map

- City-level only: name, avatar, full name; no id, email, phone, address or per-member coordinate. Every account,
  expelled ones included. Grouped by the profile's city text (trimmed, case-insensitive); members without a city are
  listed; popups show up to 10 members per city.
- Coordinates come only from the geocoding cache (`CachedGeocodingService`): a miss is queued for
  `BackgroundGeocodingWorker` and listed as "Em fila para geocodificação". A map read can queue a city.
- Leaflet loads only on `/members/map` (npm `leaflet`, its own `vendor-leaflet` chunk). Popups are DOM nodes with names
  as text, so member data is never written as markup.
- Basemap: OpenStreetMap standard tiles (`https://tile.openstreetmap.org/{z}/{x}/{y}.png`, attribution "© OpenStreetMap
  contributors"), no API key, darkened by a CSS filter on the tile pane only. One constant in `MembersMap.tsx`; its host
  is the only tile host in the CSP `img-src`. CARTO dark tiles were dropped (they began demanding an API key).

## Hall of Fame

`HallOfFameService`; twelve panels, every account counted (no category or expelled filter): Última passagem a Caloiro / a
Tuno (latest month-year pair, "MMMM yyyy" in pt-PT), Mais afilhados (members naming them as padrinho), Mais cargos (role
assignments), Mais vezes Magister, Mais instrumentos, Mais / Menos tempo a Leitão (Leitão to Caloiro months) and a
Caloiro (Caloiro to Tuno), Mais ensaios (attended, past, not-cancelled rehearsals), Mais atuações ("vou" on past,
not-cancelled events, `EndDate ?? Date`). Ties: every member sharing the value. Durations count only when positive;
counts need at least one. No winner: "Sem dados suficientes".

## Tests

`Integration.Tests/Api/MemberAreaApiTests.cs`; routes in `PortalRouteTests` and `MemberPagesTests`; menu contract in
`MemberShellTests`; the map's popup contract in `ContentSecurityPolicyTests`.

## Follow-ups

- The map counts expelled accounts (and the Hall of Fame every account): decide whether to leave them out.
