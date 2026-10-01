# React Events (`/events`, React track 011)

`/events` is a React agenda (`portal/src/Events.tsx`) and `/events/{id}` one event
(`EventDetail.tsx`). A member answers (Vou / Não vou) in a **modal** (`EventDialogs.tsx`), from a card's
quick reply or the event page, never on a page of its own. Admin/Owner **create, edit and delete**
events in React modals on the agenda (011.5), and manage the **image, cancel / reactivate and email /
push notices** there too (012A, `IEventAdminService`). Everything reads a thin, viewer-aware API
(`Endpoints/EventEndpoints.cs` → `IEventAgendaService`). The old Blazor `Pages/Activities/Events.razor`
moved to the members' **`/member/events`** (`Pages/Members/MemberEvents.razor`, `[Authorize]`), kept only
for the management React does not have yet (see Old Blazor UI). DEV only; no schema change.

**Terminology.** Events use *enrollment* (Inscrições): `EventEnrollment*`, `eventsApi.getEnrollment` /
`saveEnrollment`, `/api/events/{id}/enrollment`. *Attendance* (Presenças) belongs to rehearsals and is not
used here. Participants: **"Quem vai"** before and during an event, **"Quem foi"** once it is over (the
counts follow: Vão / Não vão, Foram / Não foram).

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
| `Events` | `ImageUrl` | text? | 13 set, all on the public R2 host | card image | hero, https/same-site only; set / replaced / removed by Admin/Owner (012A) | public | no |
| `Events` | `CreatedBy/At`, `UpdatedBy/At` | audit | - | - | never sent | internal | no |
| `Enrollments` | `UserId`, `EventId`, `WillAttend` | text; int; bool | 1085 (769 going, 316 not); 0 duplicates, 0 orphans, 0 on cancelled events | counts, lists, own badge | own answer; going / not-going counts | own row + counts: members | no |
| `Enrollments` | `Instrument`, `OtherInstruments` | int?; text? | 924 with an instrument | lists, instrument counters | own answer; "Quem vai" (going only) | members | no |
| `Enrollments` | `Notes` | text?, no limit | 67 set, max 144 | lists (all members) | own answer; "Quem vai" (all members, as before); ≤1000 on write | members | no |
| `Enrollments` | `EnrolledAt`, `CategoryAtEvent` | datetime; int? | snapshot on 246 | lists, statistics | not sent; still written by `EnrollmentService` | internal | no |
| `Trophies` | `Name`, `EventId` | text ≤200 | 8 on 4 festivals, max 23 chars | "Prémios" modal (all) | "Prémios" modal (agenda + event page), by name; managed there by Admin/Owner (012B) | public (names); ids for Admin/Owner only | no |
| `EventVideos` | `Url`, `Title`, `MimeType`, `SortOrder` | text ≤2048; ≤200?; ≤100 | 4 mp4 on 1 event, public R2 host, all titled | videos modal (all), upload by members | player + list (all) by `SortOrder`; managed by Admin/Owner on the event page (012C) | public | no |
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
- **Management:** Admin or Owner (was `IsInRole("Admin")` only) - the React create/edit/delete, image,
  cancel / reactivate and notices, and the remaining `/member/events` and `/events/{id}/enrollments` tools. Mod and Member get 403 from the
  API. Contact tracking (`/events/{id}/contacts`): Mod and above (was Admin or Mod). The agenda shows
  the controls only when the API says `canManage`; the server enforces it on every write.

## Create, edit, delete (Admin/Owner, 011.5) - audited against the old page

| | Old Blazor page | React now |
| --- | --- | --- |
| Fields | name, date + time (default 19:00) or a date range without time, location, type, description, optional cropped image | same, except the image |
| Rules | entity annotations: name and location required ≤200, description ≤2000, end not before start | same, server-side, as field errors |
| Create | `EventService.CreateEventAsync`, then a push broadcast of the new event to everyone | same calls (push to a fake in tests; no VAPID keys outside DEV/PROD) |
| Edit | `UpdateEventAsync` (or `UpdateEventWithImageAsync` when a new image was cropped) | `UpdateEventAsync`; image, cancellation and answers kept |
| Delete | `DeleteEventAsync`: deletes the image from R2 and **hard-deletes** the row; the database cascades enrollments, trophies, video rows, repertoire, discussion and contacts (video files stay in R2) | same call, behind a confirm modal naming the event and what goes with it |
| Delete with NERBA orders | FK `RESTRICT`: unhandled error | refused, `409` "tem encomendas NERBA" (1 real event has orders) |
| Image | optional; picked file ≤10 MB, cropped 3:2 to WebP; stored at `images/{env}/events/{name}_{timestamp}.webp`; replacing deletes the old object; no remove | see below (012A) |
| Cancel / reactivate | separate actions | see below (012A) |

## Image, cancel / reactivate, notices (Admin/Owner, 012A) - audited against the old page

| | Old Blazor `/member/events` | React now |
| --- | --- | --- |
| Who | `IsInRole("Admin")`, buttons hidden only | Admin or Owner, enforced in `EventAdminService` (401 visitor, 403 Member/Mod); antiforgery on every write |
| Image | crop 3:2 → WebP 0.85 (source ≤10 MB); saved with the event, or on create; old image deleted on replace | form section **Imagem**: pick (image, ≤10 MB), crop 3:2 (the Music `Cropper`, now with a ratio) → WebP, JPEG where the browser cannot encode WebP; preview before saving. Uploaded after the details save (`POST /{id}/image` → existing `SetEventImageAsync`: same key convention, old image deleted). Server: WebP/JPEG/PNG by type **and** first bytes, ≤5 MB. If only the image fails, the dialog stays open as an edit of the saved event, so a retry never creates it twice |
| Remove image | not offered | new: **Remover imagem** → `DELETE /{id}/image` (`RemoveEventImageAsync`: deletes what this environment owns, then clears `ImageUrl`) |
| Cancel | upcoming, not cancelled; reason required; **deletes every enrollment**; optional cancellation email to confirmed + subscribed members | same rules, now server-side (past or already cancelled: 409); reason ≤1000; the modal says inscrições are deleted; the email opt-in shows its count; an email failure never undoes the cancellation (shown as a warning) |
| Reactivate | one click, no confirm, no notice; enrollments not restored | confirm modal saying exactly that; cancelled upcoming events only |
| Email notice | upcoming, not cancelled; "Nova atuação" or "Lembrete" template; confirmed + subscribed members; the email service's per-event rate limit | same; the rate-limit text reaches the modal |
| Push notice | message ≤500 (title = event name); active push subscribers, optionally Leitões and Caloiros only; audited `PushNotificationSent` | same, same audit |
| Before sending | recipient names listed | counts only (`GET /{id}/notices`), then **Enviar…** → "Enviar a N membros? Sim, enviar"; any change goes back a step |
| Cancelled events | badge; no answers | unchanged: `Cancelada` on card and page, reason for members; answers 409 |

Emails link to `/events`, as before. Sends run inside the request, as in the Blazor circuit. Tests replace
email, push and storage with recording fakes: nothing is sent or written to R2.

## Prizes (Admin/Owner, 012B) - audited against the old page

The `Trophies` table: `Id`, `Name` (required, ≤200), `EventId` (FK, cascade on event delete) and the
audit columns (`CreatedAt/By`, `UpdatedAt/By`, filled by the context). No type, order or category column:
a prize is a name on one event. Writes go through the existing `TrophyService`; no storage, email or push.

| | Old Blazor `/member/events` | React now |
| --- | --- | --- |
| Who | buttons inside `AuthorizeView Roles="Admin,Owner"`; the service itself checked nothing | Admin or Owner, enforced in `EventAdminService` (401 visitor, 403 Member/Mod); antiforgery on every write |
| Where | a prizes button on **past Festival** cards → per-event modal with Adicionar / Editar / Eliminar (members saw it read-only) | the event page's **Prémios** modal: Admin/Owner get the event's prizes with rename and delete in place, and an add field on a past festival (`CanManagePrizes`); the button also shows for them on a past festival with no prize yet |
| Add | past festivals only (the only cards with the button); name not trimmed; errors swallowed | past festivals only, now server-side (409 otherwise; a festival is past once its last day is over); name trimmed, required, ≤200, shown as a field error |
| Edit / delete | rename (name only); hard delete after a confirm | same; an inline confirm; a prize id only counts under its own event; existing prizes stay editable on any event |
| Order | by name | by name (current culture), then id, everywhere |
| Read | everyone, per event; members' "Prémios por Evento" statistics | unchanged: names in every summary (visitors included), history modal on the agenda and the event page |

Every write answers the event's prizes; the page refreshes behind, so a first prize shows the Prémios
button and the history, and deleting the last one removes it for everyone but Admin/Owner (who keep the
button on a past festival to add again). Duplicate names are allowed, as before.

## Videos (Admin/Owner, 012C) - audited against the old page

`EventVideos`: `EventId`, `Url` (≤2048, the public R2 URL), `Title` (≤200, optional), `MimeType` (≤100),
`SizeBytes`, `SortOrder`, `CreatedByUserId`, audit columns; FK cascade on event delete. No thumbnail
column. Storage: `IEventVideoStorageService`, key `events/{env}/videos/{eventId}_{timestamp}_{file}`,
public-read; delete only removes objects this environment owns (a DEV copy never deletes from the
production bucket) and a storage failure is logged, the row still goes.

| | Old Blazor `/member/events` | React now |
| --- | --- | --- |
| Who | **upload: any signed-in member**; rename / delete: the uploader or Admin/Owner; reorder: Admin/Owner (drag-and-drop) | **Admin or Owner** for all of it, enforced in `EventAdminService` (401 visitor, 403 Member/Mod); antiforgery on every write. Members no longer upload (this task's rule); see Changed on purpose |
| Where | videos button on past cards → modal (upload, list, rename, delete, drag) | event page: **Gerir vídeos** in the Vídeos section (shown to Admin/Owner on a past event, or on any event that has videos) → modal |
| Upload | past events (only those cards had the button); `video/*`, ≤100 MB, title required; `EventService.AddVideoAsync` (storage key above, next sort position, push to every other member); errors swallowed | same rules, now server-side: past event (409 otherwise), a `video/*` type or a known extension (.mp4 .mov .m4v .webm .3gp .mkv .avi), 1 B-100 MB, title required ≤200; same `AddVideoAsync`, so the same key, position and push. A storage failure answers 500 and leaves no row |
| Rename | title; blank clears it | same (`UpdateVideoTitleAsync`) |
| Reorder | drag-and-drop | Subir / Descer buttons; the server takes the whole order once (`UpdateVideoOrderAsync`) and refuses a stale or partial one |
| Delete | storage then row (`DeleteVideoAsync`), no confirm | same call, inline confirm |
| Watch | everyone; plays audited | unchanged: the event page player, `POST /api/events/videos/{id}/plays` |

Every write answers the event's videos (id and title only); the page refreshes behind, so a first video
shows the section and deleting the last one leaves Admin/Owner an empty state (visitors no section).
The upload is one request with a spinner ("A enviar…"); no byte progress.

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
| `GET /api/events/{id}/edit` | Admin/Owner | The edit form's values; 401 / 403 otherwise. |
| `POST /api/events` | Admin/Owner | `{ name, date, time, endDate, location, type, description }`; `X-CSRF-TOKEN`; `201` with the agenda card; 400 field errors; push broadcast. |
| `PUT /api/events/{id}` | Admin/Owner | Same body; `X-CSRF-TOKEN`; `200` with the card. |
| `DELETE /api/events/{id}` | Admin/Owner | `X-CSRF-TOKEN`; `204`; `409 events:in-use` with NERBA orders. |
| `POST /api/events/{id}/image` | Admin/Owner | multipart `image` (WebP/JPEG/PNG, ≤5 MB); `X-CSRF-TOKEN`; `204`; 400 `errors.image`. |
| `DELETE /api/events/{id}/image` | Admin/Owner | `X-CSRF-TOKEN`; `204`. |
| `POST /api/events/{id}/cancel` | Admin/Owner | `{ reason, notifyByEmail }`; `X-CSRF-TOKEN`; `200 { sent, failed, warning }`; 400 `errors.reason`; 409 past or already cancelled. |
| `POST /api/events/{id}/reactivate` | Admin/Owner | `X-CSRF-TOKEN`; `204`; 409 unless cancelled and upcoming. |
| `GET /api/events/{id}/notices` | Admin/Owner | Audience counts only (email, push, push Leitões e Caloiros). |
| `GET /api/events/{id}/prizes` | Admin/Owner | `[{ id, name }]` in display order (012B). |
| `POST /api/events/{id}/prizes` | Admin/Owner | `{ name }`; `X-CSRF-TOKEN`; `200` the event's prizes; 400 `errors.name`; 409 unless a past festival. |
| `PUT /api/events/{id}/prizes/{prizeId}` | Admin/Owner | `{ name }`; `X-CSRF-TOKEN`; `200` the prizes; 404 if the prize is not this event's. |
| `DELETE /api/events/{id}/prizes/{prizeId}` | Admin/Owner | `X-CSRF-TOKEN`; `200` the prizes left; 404 as above. |
| `GET /api/events/{id}/videos` | Admin/Owner | `[{ id, title }]` in play order (012C). |
| `POST /api/events/{id}/videos` | Admin/Owner | multipart `file` (≤100 MB) + `title`; `X-CSRF-TOKEN`; `200` the videos; 400 `errors.file` / `errors.title`; 409 unless past. |
| `PUT /api/events/{id}/videos/{videoId}` | Admin/Owner | `{ title }` (blank clears); `X-CSRF-TOKEN`; `200` the videos; 404 if not this event's. |
| `POST /api/events/{id}/videos/reorder` | Admin/Owner | `{ videoIds }`, every id once; `X-CSRF-TOKEN`; `200`; 400 `errors.videoIds` when stale. |
| `DELETE /api/events/{id}/videos/{videoId}` | Admin/Owner | `X-CSRF-TOKEN`; `200` the videos left (stored file deleted as before). |
| `POST /api/events/{id}/notices` | Admin/Owner | `{ channel: "email", kind: "new" or "reminder" }` or `{ channel: "push", message, onlyLeitoesAndCaloiros }`; `X-CSRF-TOKEN`; `200 { sent, failed, warning }`; 400 field errors (`notice` for an empty audience or the email rate limit); 409 past or cancelled. |

Order: upcoming by date then id, past newest first then id. A multi-day event stays upcoming until
its last day. Seasons run September-August. Images and videos are absolute public R2 URLs
(https or same-site only); no object key or credential is sent. The only uploads are the Admin/Owner event
image (012A) and videos (012C), through the existing storage services.

## UI

- **Agenda:** upcoming date cards (the whole card opens the event); signed-in members get a quick-reply
  pill on each open date ("Responder", or "Vais" / "Não vais" once answered) that opens the answer
  modal in place, plus the confirmed count; no participant action on cards. Admin/Owner get small
  edit / delete buttons on every card and archive row, plus **aviso** (bell, not on cancelled dates) and
  **cancelar / reativar** on upcoming cards, and **Adicionar atuação** in the header; all are modals and
  the list refreshes in place. Header: **Prémios** (modal with the prize
  history) and "Área de membros" (members). Archive by season with search (name, place; description for
  members), season, type and "Só com vídeos" filters kept in the URL.
- **Event page:** back link and **Prémios** (top right, when the event won any - or, for Admin/Owner, on
  any past festival: its prizes, editable by them, then the history) → hero with image, facts → cancellation notice → about → Repertório → **Quem vai / Quem foi**
  → vídeos. Members get a side panel (first on a phone): their answer and "Responder" (the modal), the
  counts, and links to Quem vai / Quem foi and the discussion. Admin/Owner get **Enviar aviso** and
  **Cancelar atuação / Reativar** in the top bar (upcoming only). No link to the Blazor management pages.
- **Quem vai / Quem foi:** avatar-led tiles (a large round photo, then nickname, category and
  instrument, the note in small italics), members going, then Leitões, then "Não vão / Não foram"
  folded.
- **Modals:** the shared native `<dialog>` (`Dialog.tsx`, also used by Music): focus kept inside, Esc
  closes. The answer modal is two large choices, the instrument when going, a folded note, Cancelar /
  Guardar; the page or card refreshes in place after saving.

## Changed on purpose

- Upcoming dates are never hidden by the season filter (the old page defaulted to the current
  fiscal year for both lists); filters apply to the archive.
- Festivals without prizes are left out of "Prémios"; the old modal listed them with 0.
- A "não vou" answer no longer stores an instrument (the old form could; nothing read it).
- Visitors' search no longer matches descriptions (it was an oracle on member-only text).
- 012C: only Admin/Owner upload, rename, reorder and delete event videos (members could upload and
  manage their own on the old page). Existing videos keep their uploader; nothing was deleted.

## Old Blazor UI

- Retired from `/events`: the public page, its anonymous branches and the enrolment modal. The page
  itself is `/member/events`; its "Vou / Não vou / remover" buttons open the React answer modal
  (`/events/{id}?respond=1`).
- Retired in the 011 follow-ups: the React answer page (`EventEnrollment.tsx`, now a modal), the
  "Prémios" band (now a button + modal), and every link from the React event page to the Blazor
  management (011.5).
- Still Blazor, reached from the agenda's "Área de membros": `/member/events` for what React does not
  do yet - repertoire editing, statistics (including the read-only "Prémios por Evento"), "Minhas Inscrições"; `/events/{id}/enrollments` (add or remove someone's enrollment); `/discussion`; `/contacts`.
  Its image picker and cropper, the cancel / reactivate buttons and the email and push notice modals
  were removed in 012A (the shared `EventCard` shows those buttons only when a page wires them), so each
  of these has one way to do it: the React agenda. Its edit form still saves details and still
  announces a new event by push, as before. 012B removed its per-event prizes button and modal (add /
  edit / delete): prizes are managed only in the React Prémios modal. 012C removed its video upload,
  rename, delete and drag-and-drop reorder; its videos modal only lists and plays them.

## Follow-ups

- Advanced management in React: repertoire editing, statistics, "Minhas Inscrições", others'
  enrollments (012D-E); then `/member/events` and
  `/events/{id}/enrollments` can go.
- Notices are sent inside the HTTP request, as the Blazor circuit did; a much larger audience would want
  a background job.
- Deleting an event leaves its video files in R2 (the rows go, as before).
- Event images and videos are `PublicRead` objects: never listed beyond what visitors see, but
  reachable by URL (same as Gallery). Private storage would need signed URLs.
