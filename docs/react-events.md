# React Events (`/events`, React track 011)

`/events` is a React agenda (`portal/src/Events.tsx`), `/events/{id}` one event (`EventDetail.tsx`) and
`/events/{id}/attendance` a member's own answer on its own page (`EventAttendance.tsx`, no modal). They
read a thin, viewer-aware API (`Endpoints/EventEndpoints.cs` → `IEventAgendaService`). The old Blazor
`Pages/Activities/Events.razor` mixed a public agenda, member answers and every management tool; only
management stayed Blazor, moved unchanged in function to the members' **`/member/events`**
(`Pages/Members/MemberEvents.razor`, `[Authorize]`). DEV only; no schema change.

## Audit (real `app.db`, read-only on a scratch copy, aggregates only)

| Table / entity | Field | Type, limit, null | Real data | Old Blazor UI | React | Visibility | Schema change |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `Events` | `Name`, `Location` | text ≤200, required | 57 rows; max 65 / 54 chars | card, details | card, page | public | no |
| `Events` | `Date`, `EndDate` | datetime; datetime? | 2025-09 → 2026-10; 5 upcoming; 10 multi-day, 0 same-day/invalid end; 11 at midnight (= no time) | card, filters | dates as local text; time only when set; end only when a later day | public | no |
| `Events` | `Type` | int (`EventType`, 11 values) | 9 types used; Atuação 30, Festival 8 | filter, details | tag, filter | public | no |
| `Events` | `IsCancelled`, `CancellationReason` | bool; text ≤1000 | 9 cancelled, 8 with a reason | badge (all); reason in member details | badge (all); reason members only | public / members | no |
| `Events` | `Description` | text ≤2000, NOT NULL | 23 non-empty, max 462 | member details modal, past search | member section, member search | members | no |
| `Events` | `ImageUrl` | text? | 13 set, all on the public R2 host | card image | hero, https/same-site only | public | no |
| `Events` | `CreatedBy/At`, `UpdatedBy/At` | audit | - | - | never sent | internal | no |
| `Enrollments` | `UserId`, `EventId`, `WillAttend` | text; int; bool | 1085 (769 going, 316 not); 0 duplicates, 0 orphans, 0 on cancelled events | counts, lists, own badge | own answer; going / not-going counts | own row + counts: members | no |
| `Enrollments` | `Instrument`, `OtherInstruments` | int?; text? | 924 with an instrument | lists, instrument counters | own answer only | member (own) | no |
| `Enrollments` | `Notes` | text?, no limit | 67 set, max 144 | lists (all members) | own answer only; ≤1000 on write | member (own) | no |
| `Enrollments` | `EnrolledAt`, `CategoryAtEvent` | datetime; int? | snapshot on 246 | lists, statistics | not sent; still written by `EnrollmentService` | internal | no |
| `Trophies` | `Name`, `EventId` | text ≤200 | 8 on 4 festivals, max 23 chars | "Prémios" modal (all) | Prémios band, event page (all), by name | public | no |
| `EventVideos` | `Url`, `Title`, `MimeType`, `SortOrder` | text ≤2048; ≤200?; ≤100 | 4 mp4 on 1 event, public R2 host, all titled | videos modal (all), upload by members | player + list (all) by `SortOrder` | public | no |
| `EventVideos` | `SizeBytes`, `CreatedByUserId` | long; text | - | delete rights | never sent | internal | no |
| `EventRepertoires` | `SongId`, `DisplayOrder`, `RepertoireDate` | int; int 1-1000; date | 43 rows on 7 events; 13 on a later day; 4 songs in private albums | member modal (titles per day) | member section, titles per day | members | no |
| `Discussions` / `Posts` | per event | - | 9 discussions | member count + page | member count + link | members | no |
| `EventContacts` | contact tracking | - | 0 rows | `/events/{id}/contacts` (members) | link-free, unchanged | members | no |

**Verdict: no schema change, no migration.** Nothing persisted was added, renamed or removed.

## Visibility and permissions (server-side, `EventAgendaService` + `EventsAuthorization`)

- **Visitor:** name, dates, time, location, type, cancelled, image, trophies, videos, season. No
  description, cancellation reason, counts, repertoire, answers, user ids or notes; the `member`
  parts are `null`. Same set the old public page showed (it also showed nothing else to visitors).
- **Signed-in member:** adds the description, the cancellation reason, their *own* answer,
  going / not-going / repertoire / discussion counts and the repertoire titles. Other members'
  names, instruments and notes stay on the members' Blazor `/events/{id}/enrollments`.
- **Answering (`/events/{id}/attendance`):** any signed-in member, own row only. Open (not
  cancelled, last day not passed): going / not going, an instrument from their own instruments
  (every instrument for a Leitão with none), a note. Past: only someone who went may withdraw.
  Cancelled: nothing. Writes go through `IEnrollmentService`, so its push notifications, category
  snapshot and retirement update are unchanged.
- **Management:** Admin only, as before, on `/member/events` (`IsInRole("Admin")`; an Owner without
  Admin still has no tools there, recorded in STATE). React shows "Gerir atuações" to Admins and
  "Área de membros" / "Carregar vídeos" to other members; it hides nothing that the server allows.

## API

| Endpoint | Who | Notes |
| --- | --- | --- |
| `GET /api/public/events/upcoming` | anyone | Home preview (010), now from the same service; next 3, no ids. |
| `GET /api/events` | anyone | `{ isMember, canManage, upcoming, past }`; `no-store`. |
| `GET /api/events/{id}` | anyone | One event + videos; `member` section for members; 404 if missing. |
| `GET /api/events/{id}/attendance` | member | 401 signed out, 404 missing. |
| `PUT /api/events/{id}/attendance` | member | `{ willAttend, instrument, notes }`; `X-CSRF-TOKEN`; 400 field errors, 409 closed. |
| `DELETE /api/events/{id}/attendance` | member | Withdraw from a past event; `X-CSRF-TOKEN`; 409 otherwise. |
| `POST /api/events/videos/{id}/plays` | anyone | Audits a play (`EventVideo` / `Played`, as before); `X-CSRF-TOKEN`. |

Order: upcoming by date then id, past newest first then id. A multi-day event stays upcoming until
its last day. Seasons run September-August. Images and videos are absolute public R2 URLs
(https or same-site only); no object key or credential is sent, nothing is uploaded from React.

## UI

Agenda: upcoming date cards (date block, type, cancelled, the member's "Vais / Não vais",
confirmed count for members), a gold "Prémios" band (festivals with prizes, newest first), and the
archive by season with search (name, place; description for members), season, type and "Só com
vídeos" filters kept in the URL. Event page: hero with image, facts, cancellation notice, about,
repertoire, prizes, videos; members get a side panel with their answer, counts and links.
Attendance page: two large choices, "Vou tocar" + instrument, note, inline confirmation for
withdrawing; states for signed out, missing, closed and failures.

## Changed on purpose

- Upcoming dates are never hidden by the season filter (the old page defaulted to the current
  fiscal year for both lists); filters apply to the archive.
- Festivals without prizes are left out of "Prémios"; the old modal listed them with 0.
- A "não vou" answer no longer stores an instrument (the old form could; nothing read it).
- Visitors' search no longer matches descriptions (it was an oracle on member-only text).

## Old Blazor UI

- Retired from `/events`: the public page, its anonymous branches and the enrolment modal
  (`ParticipationModal` + remove confirmation). The page itself is `/member/events`; its
  "Vou / Não vou / remover" buttons open the React attendance page.
- Still Blazor (members): `/member/events` (create, edit, delete, cancel/uncancel, image, email and
  push notices, trophies, video upload/rename/reorder/delete, repertoire editing, adding members,
  statistics, "Minhas Inscrições"), `/events/{id}/enrollments`, `/discussion`, `/contacts`. Their
  back links now open the React event page. Requests → "create event" opens `/member/events`.

## Follow-ups

- Management in React (create/edit/cancel, trophies, videos, repertoire, notices); then
  `/member/events` can go, and with it the duplicate member list there.
- Participants list (names, instruments, notes) in React; today `/events/{id}/enrollments`.
- Event images and videos are `PublicRead` objects: never listed beyond what visitors see, but
  reachable by URL (same as Gallery).
