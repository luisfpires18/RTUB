# React Events (`/events`, React track 011)

`/events` is a React agenda (`portal/src/Events.tsx`) and `/events/{id}` one event
(`EventDetail.tsx`). A member answers (Vou / Não vou) in a **modal** (`EventDialogs.tsx`), from a card's
quick reply or the event page, never on a page of its own. Both read a thin, viewer-aware API
(`Endpoints/EventEndpoints.cs` → `IEventAgendaService`). The old Blazor `Pages/Activities/Events.razor`
mixed a public agenda, member answers and every management tool; only management stayed Blazor,
moved unchanged in function to the members' **`/member/events`** (`Pages/Members/MemberEvents.razor`,
`[Authorize]`). DEV only; no schema change.

**Terminology.** Events use *enrollment* (Inscrições): `EventEnrollment*`, `eventsApi.getEnrollment` /
`saveEnrollment`, `/api/events/{id}/enrollment`. *Attendance* (Presenças) belongs to rehearsals and is not
used here.

**Routes.** `/events` and `/events/{id}` are React. `?respond=1` on an event opens the answer modal (used
by the `/member/events` buttons). `/events/{id}/enrollment` - the answer page of the first 011 build, which
reached DEV - is a temporary `302` to `/events/{id}?respond=1`; the draft `/events/{id}/attendance` never
reached `dev` and is not served (404). Not to be confused with the members' Blazor list of everyone's
answers with its admin tools, `/events/{id}/enrollments`.

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
| `Enrollments` | `Instrument`, `OtherInstruments` | int?; text? | 924 with an instrument | lists, instrument counters | own answer; "Quem vai" (going only) | members | no |
| `Enrollments` | `Notes` | text?, no limit | 67 set, max 144 | lists (all members) | own answer; "Quem vai" (all members, as before); ≤1000 on write | members | no |
| `Enrollments` | `EnrolledAt`, `CategoryAtEvent` | datetime; int? | snapshot on 246 | lists, statistics | not sent; still written by `EnrollmentService` | internal | no |
| `Trophies` | `Name`, `EventId` | text ≤200 | 8 on 4 festivals, max 23 chars | "Prémios" modal (all) | "Prémios" modal (agenda + event page), by name | public | no |
| `EventVideos` | `Url`, `Title`, `MimeType`, `SortOrder` | text ≤2048; ≤200?; ≤100 | 4 mp4 on 1 event, public R2 host, all titled | videos modal (all), upload by members | player + list (all) by `SortOrder` | public | no |
| `EventVideos` | `SizeBytes`, `CreatedByUserId` | long; text | - | delete rights | never sent | internal | no |
| `EventRepertoires` | `SongId`, `DisplayOrder`, `RepertoireDate` | int; int 1-1000; date | 43 rows on 7 events; 13 on a later day; 4 songs in private albums | member modal (titles per day) | member section, titles per day | members | no |
| `Discussions` / `Posts` | per event | - | 9 discussions | member count + page | member count + link | members | no |
| `EventContacts` | contact tracking | - | 0 rows | `/events/{id}/contacts` (members) | link-free, unchanged | members | no |

**Verdict: no schema change, no migration.** Nothing persisted was added, renamed or removed.

## Visibility and permissions (server-side, `EventAgendaService` + `EventsAuthorization`)

Roles inherit: Owner includes Admin, Admin includes Mod (`EventsAuthorization`).

- **Visitor:** name, dates, time, location, type, cancelled, image, trophies, videos, season. No
  description, cancellation reason, counts, repertoire, answers, user ids or notes; the `member`
  parts are `null`. Same set the old public page showed.
- **Signed-in member:** adds the description, the cancellation reason, their own answer, the counts,
  the repertoire and **Quem vai**: name, full name, avatar, category (or MAGISTER), instrument and
  note of everyone who answered, grouped as members going / Leitões / not going (the same the old
  members' list showed every member). Never user ids, emails or phones.
- **Answering:** any signed-in member, own row only. Open (not cancelled, last day not passed): Vou /
  Não vou, an instrument from their own instruments (primary preselected; every instrument for a
  Leitão with none), an optional note. Past: only someone who went may withdraw. Cancelled: nothing.
  Writes go through `IEnrollmentService`, so its push notifications, category snapshot and retirement
  update are unchanged.
- **Management** (`/member/events` tools, adding/removing others' enrollments on
  `/events/{id}/enrollments`): Admin or Owner (was `IsInRole("Admin")` only). Contact tracking
  (`/events/{id}/contacts`): Mod and above (was Admin or Mod). React shows "Gerir atuações" and
  "Gerir inscrições" to those roles; it hides nothing the server allows.

## API

| Endpoint | Who | Notes |
| --- | --- | --- |
| `GET /api/public/events/upcoming` | anyone | Home preview (010), now from the same service; next 3, no ids. |
| `GET /api/events` | anyone | `{ isMember, canManage, upcoming, past }`; `no-store`. |
| `GET /api/events/{id}` | anyone | One event + videos; `member` section (reason, repertoire, Quem vai) for members; 404 if missing. |
| `GET /api/events/{id}/enrollment` | member | 401 signed out, 404 missing. |
| `PUT /api/events/{id}/enrollment` | member | `{ willAttend, instrument, notes }`; `X-CSRF-TOKEN`; 400 field errors, 409 closed. |
| `DELETE /api/events/{id}/enrollment` | member | Withdraw from a past event; `X-CSRF-TOKEN`; 409 otherwise. |
| `POST /api/events/videos/{id}/plays` | anyone | Audits a play (`EventVideo` / `Played`, as before); `X-CSRF-TOKEN`. |

Order: upcoming by date then id, past newest first then id. A multi-day event stays upcoming until
its last day. Seasons run September-August. Images and videos are absolute public R2 URLs
(https or same-site only); no object key or credential is sent, nothing is uploaded from React.

## UI

- **Agenda:** upcoming date cards (the whole card opens the event); signed-in members get a quick-reply
  pill on each open date ("Responder", or "Vais" / "Não vais" once answered) that opens the answer
  modal in place, plus the confirmed count. Header: **Prémios** (modal with the prize history) and
  "Gerir atuações" / "Área de membros". Archive by season with search (name, place; description for
  members), season, type and "Só com vídeos" filters kept in the URL.
- **Event page:** back link and **Prémios** (top right, when the event won any: its prizes, then the
  history) → hero with image, facts → cancellation notice → about → Repertório → **Quem vai** → vídeos.
  Members get a side panel (first on a phone): their answer and "Responder" (the modal), Vão / Não vão
  / Músicas, and links to Quem vai, the Blazor discussion and management.
- **Modals:** the shared native `<dialog>` (`Dialog.tsx`, also used by Music): focus kept inside, Esc
  closes. The answer modal is two large choices, the instrument when going, a folded note, Cancelar /
  Guardar; the page or card refreshes in place after saving.

## Changed on purpose

- Upcoming dates are never hidden by the season filter (the old page defaulted to the current
  fiscal year for both lists); filters apply to the archive.
- Festivals without prizes are left out of "Prémios"; the old modal listed them with 0.
- A "não vou" answer no longer stores an instrument (the old form could; nothing read it).
- Visitors' search no longer matches descriptions (it was an oracle on member-only text).

## Old Blazor UI

- Retired from `/events`: the public page, its anonymous branches and the enrolment modal. The page
  itself is `/member/events`; its "Vou / Não vou / remover" buttons open the React answer modal
  (`/events/{id}?respond=1`).
- Retired in the 011 follow-up: the React answer page (`EventEnrollment.tsx`, now a modal) and the
  "Prémios" band on the agenda (now a button + modal).
- Still Blazor (members): `/member/events` (create, edit, delete, cancel/uncancel, image, email and
  push notices, trophies, video upload/rename/reorder/delete, repertoire editing, statistics, "Minhas
  Inscrições"), `/events/{id}/enrollments` (admin: add or remove someone's enrollment),
  `/discussion`, `/contacts`. Their back links open the React event page.

## Follow-ups

- Management in React (create/edit/cancel, trophies, videos, repertoire, notices, others'
  enrollments); then `/member/events` and `/events/{id}/enrollments` can go.
- Event images and videos are `PublicRead` objects: never listed beyond what visitors see, but
  reachable by URL (same as Gallery).
