# React Events (`/events`)

`/events` is the React agenda (`src/RTUB.Web/portal/src/Events.tsx`); `/events/{id}` is one event (`EventDetail.tsx`).
Members answer (Vou / Não vou) in a modal (`EventDialogs.tsx`), from a card's quick reply or the event page. Admin and
Owner create, edit, delete, manage the image, cancel or reactivate, and send notices in modals on the same pages.

Code: `Endpoints/EventEndpoints.cs` → `IEventAgendaService` (reads), `IEventAdminService` (Admin/Owner writes),
`Services/EventAgendaService.cs` (visibility), `Helpers/EventsAuthorization.cs`, `Services/EventRepertoireAdminService.cs`,
`Services/EventDiscussionBoardService.cs`, `Services/EventContactsAdminService.cs`, `Services/CloudflareEventVideoStorageService.cs`
(`IEventVideoStorageService`).

**Terminology.** Events use *enrollment* (inscrição): `EventEnrollment*`, `/api/events/{id}/enrollment`. Attendance
(presença) belongs to rehearsals. Participants are "Quem vai" before and during an event and "Quem foi" once it is over
(counts: Vão / Não vão, Foram / Não foram).

## Routes

| Route | Notes |
| --- | --- |
| `/events`, `/events/{id}` | React. `?respond=1` opens the answer modal. `?repertoire=1` opens the repertoire editor once. |
| `/events/{id}/enrollment` | `302` to `/events/{id}?respond=1` |
| `/events/{id}/enrollments` | `302` to `/events/{id}#who-title` |
| `/member/events` | `302` to `/events`, query kept. Requests' "criar atuação" (`?openModal=true&name=&location=&date=&description=`) opens the prefilled create form for Admin/Owner; others see the agenda. |
| `/events/my-enrollments` | Removed. "Onde estiveste" on `/events` replaces it. |
| `/events/{id}/discussion` | Members. A visitor gets `302` to `/login?returnUrl=…`. |
| `/events/{id}/contacts` | Mod and above, and Leitões (049). A visitor gets `302` to login. |

## Data

No schema change in the current rules. Key columns that constrain changes:

- `Events`: `Name`, `Location` (required, ≤200), `Date`, `EndDate`, `Type` (`EventType`), `IsCancelled`,
  `CancellationReason` (≤1000), `Description` (≤2000), `ImageUrl` (absolute public R2 URL, https or same-site only),
  `IsPublic` (migration `AddEventIsPublic`, column default `1`; the EF model has no default, so the app always writes its own value).
- `Enrollments`: `UserId`, `EventId` (unique together), `WillAttend`, `Instrument`, `OtherInstruments`, `Notes` (≤1000 on write),
  `EnrolledAt`, `CategoryAtEvent` (snapshot).
- `Trophies`: `Name` (≤200), `EventId`.
- `EventVideos`: `Url` (≤2048), `Title` (≤200), `MimeType` (≤100), `SizeBytes`, `SortOrder`, `CreatedByUserId`.
- `EventRepertoires`: `SongId`, `DisplayOrder` (1-1000 per day), `RepertoireDate`. Unique (`EventId`, `SongId`). Songs come from Music; titles only.
- `Discussions`, `Posts`, `Comments`, `Transportations`, `TransportationPassengers`, `PostMedia`, `CommentImages`. `PostMedia` and `CommentImages` are not written.
- `EventContacts`: `EventId`, `UserId` (a member), `IsContacted`, `WillAttend` (bool?), `Notes` (≤500), `ContactedAt`, `ContactedBy`.

Deleting an event cascades enrollments, trophies, video rows, repertoire, discussion and contacts. Video files stay in R2.

## Visibility

- `Events.IsPublic`. On create, events are public unless Admin/Owner tick "Privado (só membros)" (`EventInput.MembersOnly`, `bool?`).
  On update, an absent `MembersOnly` keeps the current visibility, so an edit never makes a private event public by omission.
- One rule, in `EventAgendaService.VisibleTo`: a signed-in member sees every event; anyone else sees public events only.
- A visitor gets `404` for a members-only `GET /api/events/{id}` and `POST /api/events/videos/{id}/plays`, and never receives it from `GET /api/events`.
  The home preview `GET /api/public/events/upcoming` is public-only for everyone.
- Push (new-event broadcast, notices) reaches only push subscriptions, which only signed-in members can create. Emails go to members only.
- Summary carries `membersOnly: true`; cards, archive rows and the event page show "Só membros".
- Visitor sees: name, dates, time, location, type, cancelled flag, image, trophies, videos, season. The `member` part is `null`.
- Member also sees: description, cancellation reason, own answer, counts, repertoire, and **Quem vai**: name, full name, avatar,
  category (or MAGISTER), instrument and note of each answerer, grouped as members going, Leitões, not going.
- Never user ids, emails or phone numbers in the agenda or event API.
- Visitor search does not match descriptions, because descriptions are member-only text.

## Permissions

Roles inherit: Owner includes Admin, Admin includes Mod (`EventsAuthorization`). Visitors get `401` on write endpoints; signed-in
users without the right get `403`. The agenda shows controls only when the API says `canManage` or `canEdit`. The server enforces every write.

| Who | What |
| --- | --- |
| Visitor | Public agenda only. No descriptions, counts, repertoire, enrollments. |
| Any signed-in member | Member view, Quem vai, own enrollment, statistics. |
| Active member on `/pending-actions` | First answer to own unanswered events (see Pending actions). |
| Mod and above; members with Leitão category (049) | Contacts list (`/events/{id}/contacts`): read, record and reset calls. |
| Mod, Admin, Owner | Create and edit an event, fields and image (`EventsAuthorization.CanEdit`, `canEdit` in the agenda DTO; the DTO also carries the type list). |
| Admin, Owner | Delete, cancel / reactivate, notices, prizes, videos, repertoire, add or remove anyone's enrollment, pin and lock discussion posts. |
| Owner only | Delete anyone's discussion post or comment. Admin does not. |
| Current Ensaiador or Magister (position, not role; 075) | Repertoire of an upcoming, not cancelled event (`EventDetailDto.CanManageRepertoire`). Nothing else. |

Mod gets 403 for delete, prizes, videos, repertoire and enrollments.

## Answering

- Any signed-in member answers for their own row only.
- Open (not cancelled, last day not passed): Vou / Não vou, an instrument from their own instruments (primary preselected; a Leitão with none gets every instrument), an optional note.
  A "Não vou" answer stores no instrument.
- Past: only someone who went may withdraw (`DELETE /api/events/{id}/enrollment`). Cancelled: nothing.
- Writes go through `IEnrollmentService`, so push notifications, the category snapshot and the activity status update still run.
- Editing someone else's answer is not offered. Admin/Owner add and remove through the manager (see Participants).

## Agenda and event page

- **Agenda:** upcoming date cards (the whole card opens the event). Members get a quick-reply pill ("Responder", or "Vais" / "Não vais"
  once answered) and the confirmed count. Admin/Owner get edit and delete on every card and archive row, **aviso** (not on cancelled dates),
  **cancelar / reativar** on upcoming cards, and **Adicionar atuação** in the header. Header also has **Prémios**, and for members **Onde estiveste**
  and **Estatísticas**. Archive by season, with search (name, place; description for members) and type and "Só com vídeos" filters kept in the URL.
  Upcoming dates are never hidden by the season filter; filters apply to the archive.
- **Event page:** back link; **Prémios** (when the event has prizes, or for Admin/Owner on any past festival); hero with image and facts;
  cancellation notice; about; Repertório; **Quem vai / Quem foi**; Vídeos. Members get a side panel (first on a phone) with their answer and
  "Responder", the counts, and links to Quem vai, Quem foi and the discussion. Admin/Owner get **Enviar aviso** and **Cancelar atuação / Reativar** in the top bar (upcoming only).
- **Quem vai / Quem foi:** avatar tiles (round photo, nickname, category, instrument, note in small italics). Members going first, then Leitões,
  then "Não vão / Não foram" folded.
- **Modals:** the shared native `<dialog>` (`Dialog.tsx`, also used by Music): focus stays inside, Esc closes. The answer modal is two large choices,
  the instrument when going, a folded note, Cancelar / Guardar. The page or card refreshes in place after saving.

## Create, edit, delete

- Fields: name, date and time (time default 19:00) or a date range without time, location, type, description, optional image.
- Rules: name and location required, ≤200; description ≤2000; end not before start. The server returns field errors.
- Create: `POST /api/events`, then push broadcast of the new event to subscribers.
- Edit: `PUT /api/events/{id}`. Image, cancellation and answers are kept.
- Delete: hard delete behind a confirm modal that names the event and what goes with it. Refused with `409 events:in-use` when NERBA orders exist (FK restrict).

## Image, cancel, reactivate, notices

- **Image:** pick (≤10 MB), crop 3:2 to WebP (JPEG where the browser cannot encode WebP), preview, then upload after the details save
  (`POST /api/events/{id}/image`). Key convention `images/{env}/events/{name}_{timestamp}.webp`; replacing deletes the old object.
  Server accepts WebP, JPEG or PNG by type and first bytes, ≤5 MB. If only the image fails, the dialog stays open as an edit of the saved event,
  so a retry never creates the event twice. **Remover imagem** calls `DELETE /api/events/{id}/image`, which deletes what this environment owns, then clears `ImageUrl`.
- **Cancel:** upcoming, not already cancelled; reason required, ≤1000. Deletes every enrollment. Optional email to confirmed and subscribed members.
  Past or already cancelled: `409`. The modal says enrollments are deleted; the email opt-in shows its count. An email failure never undoes the cancellation; it returns a warning.
- **Reactivate:** cancelled upcoming events only. Confirm modal says enrollments are not restored.
- **Email notice:** upcoming, not cancelled; "Nova atuação" or "Lembrete" template; confirmed and subscribed members; the email service's per-event rate limit.
  Emails link to `/events`. Sends run inside the request.
- **Push notice:** message ≤500, title = event name; active push subscribers, optionally Leitões and Caloiros only; audited as `PushNotificationSent`.
- Counts come first (`GET /api/events/{id}/notices`), then **Enviar…** → "Enviar a N membros? Sim, enviar". Any change goes back a step.

## Prizes

- `Trophies`: a prize is a name on one event (no type, order or category column).
- Add: past festivals only (a festival is past once its last day is over), `409` otherwise. Name trimmed, required, ≤200.
- Edit and delete: a prize id counts only under its own event. Existing prizes stay editable on any event. Delete asks inline.
- Duplicate names are allowed.
- Order: by name (current culture), then id.
- Read: names in every summary, visitors included. The Prémios modal shows the history. Festivals without prizes are left out.
- Every write returns the event's prizes; the page refreshes, so deleting the last prize removes the button for everyone except Admin/Owner.

## Videos

- Admin and Owner only: upload, rename, reorder, delete. Members can watch. Plays are audited (`POST /api/events/videos/{id}/plays`).
- Upload: past events only (`409` otherwise). Type `video/*` or extension `.mp4 .mov .m4v .webm .3gp .mkv .avi`. 1 B to 100 MB. Title required, ≤200.
  Storage key `events/{env}/videos/{eventId}_{timestamp}_{file}`, public-read, through `IEventVideoStorageService`. Next sort position. Push to every other member.
  A storage failure answers `500` and leaves no row. One request with a spinner; no byte progress.
- Rename: blank clears the title.
- Reorder: Subir / Descer buttons. The server takes the whole order once (`UpdateVideoOrderAsync`) and refuses a stale or partial one.
- Delete: stored object first, then row. Only objects this environment owns are deleted, so a DEV copy never touches the production bucket.
  A storage failure is logged; the row still goes.
- Where: Vídeos section on the event page. Admin/Owner see **Gerir vídeos** on a past event, or on any event that has videos.

## Repertoire

- Admin/Owner manage any event. The current Ensaiador or Magister manages an upcoming, not cancelled one (`CanManageRepertoire`).
  `?repertoire=1` opens the editor.
- Members see the Repertório section (titles per day). Visitors see none.
- Tabs: the event's days, plus any other day that already has songs, so empty days of a multi-day event can be filled.
- Song picker: `GET /api/events/{id}/repertoire/songs?q=`. Title search, accents and case ignored, prefix matches first, 20 at a time.
  Offers the songs Music shows the caller (the Owner sees every album; others do not see exclusive albums they are not listed on).
  Excludes songs already in the event or with the same title. A duplicate is refused with a field error.
- Add: appended after the day's last song. Remove one song, or clear a day (confirm).
- Reorder: Subir / Descer. The server takes the day's whole order (`UpdateRepertoireOrderAsync`) and refuses a stale or partial one.
- Clearing the last song leaves Admin/Owner an empty state with the button; members see no section.
- No song is created here, and no lyrics or audio.

## Participants and "Onde estiveste"

- Admin/Owner: **Gerir inscrições** → **Inscrever um membro**. Search by nickname or name (accents ignored, 20 at a time) over members who have not answered and are not expelled.
  Added as going with their primary instrument, no notification. Refused: a duplicate, a member who already answered, a cancelled event.
  The picker sends user ids to Admin/Owner only.
- Remove any answer (inline confirm). The manager sends no notification.
- Members change their own answer in the answer modal.
- **Onde estiveste** (member modal on `/events`): last 10 (or all) past, not cancelled events, Foste / Não foste, participation share.
  Built on `GET /api/events` (own answers only). Its "Próximas datas" answers stay on the agenda and event page.

## Statistics

- `GET /api/events/stats?from=&to=`, any signed-in member, modal **Estatísticas**. Default range: current season (1 Sep to 31 Aug), by the event's first day.
- Only "vou" answers count, split into already done (before today) and still to come.
- Share is over the range's past, not cancelled events.
- Category filter: Tunos (incl. Veteranos and Tunossauros) / Caloiros / Leitões. Search by name.
- Names, avatar and categories only. No user ids, emails or notes. `400` when `to` is before `from`.

## Discussion (members)

- One discussion per event. Posts: title optional (a blank title takes the first 60 characters of the text; 3-120 when given), body ≤5000,
  `LastActivityAt`, `IsEdited`, `IsPinned`, `IsLocked`, `IsDeleted` (soft delete), `MentionsJson`. Comments: flat under a post, ≤2000, soft delete.
- Lifts: a driver offers seats on a post (`VehicleDescription` ≤200, `TotalSeats` 2-20, `Notes` ≤500) with passengers.
- Any member reads, posts, comments and offers a lift. Edit: author (post, comment, own lift). Delete: author, or Owner for anyone's.
- Pin and lock: Admin or Owner. Locked post: no new comments (`400` with the reason).
- Seat passengers: the driver or Admin/Owner. A passenger may leave. Expelled members are not offered.
- Order: pinned first, then latest activity. Comments oldest first.
- Mentions `@username` resolve and push (`MentionService`). Highlighted in the text; no autocomplete.
- Notifications: new post → push to members going to an upcoming event; comment → post author and earlier commenters; mentions.
- Not offered: post media, search, pagination. Today a discussion holds at most a few posts.
- Every write answers the whole conversation.
- Code: `Services/EventDiscussionBoardService.cs`.

## Contacts

- Read and write: Mod and above, and any Leitão (category read from the database). Server-side, antiforgery on every write.
- Listed: every member who is not expelled. Order: contacted newest first, then by name.
- Record a call: `WillAttend` (yes, no, or undecided) and notes ≤500. An answer creates the member's enrollment (primary instrument, no notification) or flips an existing one.
- Repor clears the call and deletes the member's enrollment. The confirm says so.
- Phone shown is the member's `PhoneNumber`. Never public. Linked from the event page's member panel.

## Pending actions

`/pending-actions` answers a member's own unanswered events in place ([react-pending-actions](react-pending-actions.md)), through
`IEnrollmentService.CreateEnrollmentAsync`, create-only. Members may give a **first** answer to a past event of the current fiscal year
(Fui / Não fui). The event page still lets only someone who went withdraw. A "Fui" is the same enrollment an Admin adds in "Quem foi":
it counts as a performance and in XP at once, and the activity status is reconciled. Existing answers are never changed from there.

## Calendário: "Adicionar ao Google Calendar"

Shared by events, rehearsals and `/pending-actions`. `portal/src/calendar.ts` builds the link; `CalendarOffer.tsx` shows it.

- **When:** right after the server saves a member's **new** "vou" to an upcoming, not cancelled activity. Also on the event dialog, the rehearsal dialog,
  and a "Vou" under Próximas on `/pending-actions`. Never after "não", a past "Fui", a failed save, a page load, or re-saving a "vou" that was already a "vou".
- **Later:** an upcoming detail page shows "Adicionar ao calendário" to whoever is going, so "Agora não" is reversible.
- **Copy:** events "Inscrição guardada."; rehearsals "Presença registada, pendente de aprovação." Help text: "Confirma em Guardar no Google Calendar.
  Alterações ou cancelamentos na RTUB não atualizam esta cópia automaticamente." RTUB never says the item was added; it cannot see Google.
- **Link:** Google's template `https://calendar.google.com/calendar/render?action=TEMPLATE` with `text`, `dates`, `ctz=Europe/Lisbon`, `location`,
  and `details` = the item's RTUB page plus the independent-copy note. The RTUB page base comes from `Calendar:PublicOrigin`, sent to signed-in members by `/api/account/me`.
  Never `location.origin` or the request Host. Not configured, or not an absolute https origin, means no RTUB link.
  No notes, participants, descriptions, tokens or user data. No OAuth, no Calendar API; RTUB writes nothing to Google.
- **Times:** RTUB's Portugal wall clock. Google applies the zone, so summer and winter time and devices in other zones are right.
- **Dates:** rehearsal `start`–`end`; an end not after the start ends the next day (21:00 to 00:00). An event with a time and no end gets a
  **suggested** duration (1 h to 4 h, default 2 h), labelled as a suggestion. An event with no time, or spanning several days, is an all-day entry through its last day (end exclusive).
- **Opening:** only from the member's click (`target=_blank`, `noopener noreferrer`). "Copiar o link" (or "abrir o link" when copying fails) is the fallback for installed apps that block it.
  Closing, Esc or "Agora não" leaves the answer saved. Nothing is retried or re-sent.
- **Tests:** `npm run check:calendar` (in `src/RTUB.Web`) and `tests/RTUB.Web.Tests/ReactPortal/CalendarOfferPortalTests.cs`.

## API

Writes need `X-CSRF-TOKEN`. Outcomes: `400` with `errors.<field>` for bad input, `409` for wrong state, `404` for ids outside the event.

| Endpoint | Who | Notes |
| --- | --- | --- |
| `GET /api/public/events/upcoming` | anyone | Home preview: next 3, no ids. |
| `GET /api/events` | anyone | `{ isMember, canManage, upcoming, past }`; `no-store`. |
| `GET /api/events/stats?from=&to=` | member | Statistics (see above). |
| `GET /api/events/{id}` | anyone | One event with videos; `member` section for members; `404` if missing or members-only for a visitor. |
| `GET /api/events/{id}/enrollment` | member | `401` signed out, `404` missing. |
| `PUT /api/events/{id}/enrollment` | member | `{ willAttend, instrument, notes }`; `409` closed. |
| `DELETE /api/events/{id}/enrollment` | member | Withdraw from a past event; `409` otherwise. |
| `POST /api/events/videos/{id}/plays` | anyone | Audits a play. |
| `GET /api/events/{id}/discussion` | member | `{ canModerate, posts: [...] }`; no user ids, emails or phones. |
| `POST /api/events/{id}/discussion/posts`; `PUT`/`DELETE …/posts/{postId}` | member | `{ title, body }`; edit by author; delete by author or Owner. Answers the conversation. |
| `PUT /api/events/{id}/discussion/posts/{postId}/flags` | Admin/Owner | `{ pinned, locked }`. |
| `POST …/posts/{postId}/comments`; `PUT`/`DELETE …/discussion/comments/{commentId}` | member | `{ body }`; `400` on a locked post. Delete also by Owner. |
| `POST /api/events/{id}/discussion/transport`; `PUT …/posts/{postId}/transport` | member | `{ vehicle, seats, notes }`; edit by the driver. |
| `GET …/posts/{postId}/passengers/members?q=`; `POST …/passengers`; `DELETE …/passengers/{passengerId}` | driver or Admin/Owner; a passenger may remove self | User ids only to people who may seat others. |
| `GET /api/events/{id}/contacts`; `PUT`/`DELETE /api/events/{id}/contacts/{userId}` | Mod and above, Leitões | `PUT { willAttend, notes }` records the call and syncs the enrollment; `DELETE` resets it and removes the enrollment. |
| `GET /api/events/{id}/edit` | Admin/Owner | Edit form values. |
| `POST /api/events` | Admin/Owner | `{ name, date, time, endDate, location, type, description, membersOnly }`; `201` with the card; push broadcast. |
| `PUT /api/events/{id}` | Admin/Owner | Same body; `200` with the card. |
| `DELETE /api/events/{id}` | Admin/Owner | `204`; `409 events:in-use` with NERBA orders. |
| `POST /api/events/{id}/image` | Admin/Owner | Multipart `image`; `204`. |
| `DELETE /api/events/{id}/image` | Admin/Owner | `204`. |
| `POST /api/events/{id}/cancel` | Admin/Owner | `{ reason, notifyByEmail }`; `200 { sent, failed, warning }`. |
| `POST /api/events/{id}/reactivate` | Admin/Owner | `204`; `409` unless cancelled and upcoming. |
| `GET /api/events/{id}/notices` | Admin/Owner | Audience counts only. |
| `POST /api/events/{id}/notices` | Admin/Owner | `{ channel: "email", kind: "new" or "reminder" }` or `{ channel: "push", message, onlyLeitoesAndCaloiros }`; `200 { sent, failed, warning }`; `409` past or cancelled. |
| `GET /api/events/{id}/prizes`; `POST`; `PUT /{prizeId}`; `DELETE /{prizeId}` | Admin/Owner | `[{ id, name }]`; writes answer the prizes. `409` unless a past festival on add. |
| `GET /api/events/{id}/videos`; `POST` (multipart `file` + `title`); `PUT /{videoId}`; `POST /reorder` (`{ videoIds }`); `DELETE /{videoId}` | Admin/Owner | `POST` `409` unless past. Reorder `400` when stale. |
| `GET /api/events/{id}/repertoire`; `GET …/songs?q=`; `POST` (`{ songId, date }`); `POST …/reorder` (`{ date, itemIds }`); `DELETE /{itemId}`; `DELETE /days/{date}` | Admin/Owner | Songs: up to 20 `{ id, title, album }`. |
| `GET /api/events/{id}/enrollments` | Admin/Owner | `{ canAdd, going, leitoes, notGoing }`, each `{ id, participant }`. |
| `GET /api/events/{id}/enrollments/members?q=` | Admin/Owner | Up to 20 members, not answered, not expelled. |
| `POST /api/events/{id}/enrollments` | Admin/Owner | `{ userId }`; going, primary instrument, no notification; `409` cancelled. |
| `DELETE /api/events/{id}/enrollments/{enrollmentId}` | Admin/Owner | `200` the list. |

Order: upcoming by date then id; past newest first then id. A multi-day event stays upcoming until its last day.
"Today" is the calendar date in Portugal (`PortugalTime.Today`, Europe/Lisbon via the injected `TimeProvider`), never the server zone (Azure is UTC).
Stored event dates are local and are not converted. Seasons run September to August. Images and videos are absolute public R2 URLs.
No object key or credential is sent. The only uploads are the event image and videos, through the existing storage services.

## Follow-ups

- Discussion: no post media upload, no mention autocomplete, no search or pagination. Add them if members ask.
- `/member/events` and the old redirects are `302` while DEV is hybrid. Make them `301`, or drop them, at the PROD cutover.
- Notices are sent inside the HTTP request. A much larger audience would need a background job.
- Deleting an event leaves its video files in R2.
- Event images and videos are `PublicRead` objects: reachable by URL, though never listed beyond what visitors see. Private storage would need signed URLs.
