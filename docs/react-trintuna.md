# React Trintuna (`/trintuna`)

The Trintuna gatherings ("Nº Almoço"): place, organizer, description, a date poll, Vou / Não vou (past ones too, with
who answered; worded Fui / Não fui on past ones), a Hall of Fame and a share link per gathering. Only the Owner and the
members the Owner lists see the page (task 153, Issue #364; task 154, Issue #366; task 155, Issue #368).
Code: `portal/src/pages/Trintuna.tsx`, `components/TrintunaDialogs.tsx` (gathering + access dialogs),
`components/TrintunaForm.tsx` (create / edit, "Marcar data"), `data/trintunaApi.ts`; `Endpoints/TrintunaEndpoints.cs` >
`ITrintunaService` (`TrintunaService.cs`, `.Gatherings.cs`, `.Answers.cs`), rules in `Helpers/TrintunaAuthorization.cs`.
Tables (migration `AddTrintunaGatherings`): `TrintunaAccesses`, `Gatherings`, `GatheringPollOptions`,
`GatheringPollVotes`, `GatheringParticipants`.
Tests: `Integration.Tests/Api/TrintunaApiTests*.cs` (`.HallOfFame.cs`: the Hall of Fame; `.Numbers.cs`: numbers and organizers), `Application.Tests/Helpers/TrintunaAuthorizationTests.cs`,
`Application.Tests/Data/SeedDataTrintunaTests.cs`, `MigrationChainTests.MigratedDatabase_HasNoTrintunaGatherings`,
`Web.Tests/ReactPortal/TrintunaFormSourceTests.cs`, `SharedSelectSourceTests.cs`, `MemberShellTests`, `MemberPagesTests`,
`ShellPagesTests`, `AccountEndpointTests`.

## Routes

| Route | Who |
| --- | --- |
| `/trintuna` | signed-in members reach the page; the Owner and listed members see the gatherings, anyone else "Não tem acesso a esta página."; visitors `302` to `/login?returnUrl=%2Ftrintuna` |
| `/trintuna?almoco={id}` | the same; opens that gathering's dialog (an id not on the board opens nothing and drops the query); visitors `302` to `/login?returnUrl=%2Ftrintuna%3Falmoco%3D{id}` |

Link: member menu (`MemberShell.tsx`, Tuna group "Trintuna"), shown when `GET /api/account/me` `menu.trintuna` is true
(Owner, or a `TrintunaAccess` row). The icon (`smile`) is a placeholder until the owner picks one.

## API (`/api/trintuna`, no-store; writes need `X-CSRF-TOKEN`, 400 without)

| Route | Who |
| --- | --- |
| `GET /` → `{ canManage, upcoming[], past[], hallOfFame[] }` | Owner, listed members |
| `POST /gatherings` `{ place, description, organizerUserId, pollDates[] }` (no number; one sent is ignored; 409 `trintuna:conflict` after 5 lost races) | Owner |
| `PUT /gatherings/{id}` (same body; the number never changes) | Owner; the gathering's organizer (organizer stays as stored) |
| `DELETE /gatherings/{id}` → 204 | Owner |
| `POST /gatherings/{id}/settle` `{ optionId }` | Owner; the gathering's organizer |
| `PUT /gatherings/{id}/vote` `{ optionId }` | Owner, listed members |
| `PUT /gatherings/{id}/presence` `{ willAttend }`, `DELETE` → 204 | Owner, listed members |
| `PUT /gatherings/{id}/attendees/{key}` → the gathering (Fui for that person), `DELETE` → 204 (removes that person's Fui) | past gathering only (409 `trintuna:closed` otherwise): Owner; the gathering's organizer while listed (403 otherwise) |
| `GET /access`, `PUT /access` `{ userIds[] }` | Owner |
| `GET /organizers` → `[{ id, displayName, avatarUrl }]` (Owner-role users and listed members, not expelled, by name) | Owner (401 / 404 / 403 as `/access`) |

## Permissions

| Who | See | Vote, Vou / Não vou | Edit a gathering, settle its date | Marcar quem foi (past) | Create, delete, access list |
| --- | --- | --- | --- | --- | --- |
| Visitor | 401 | 401 | 401 | 401 | 401 |
| Member not on the list (Admin and Mod included) | 404 | 404 | 404 | 404 | 404 |
| Listed member | yes | yes | only as its organizer (403 otherwise) | only as its organizer (403 otherwise) | 403 |
| Owner | yes | yes | all | all | yes |

## Rules

- Lists (Portugal day, `PortugalTime.Today`): Passados = historical or dated before today, newest first, then by number
  (descending); Próximos = the rest, dated soonest first, undated after, by number.
- The gathering DTO carries no user id except `organizerUserId`, and only for the Owner. The poll has the portal's
  `MeetingPoll` shape (rendered by `PollCard` with `onVote`), counts only, no voter names; each option also has its `date`.
  Each gathering lists who answered, `going[]` and `notGoing[]` (`{ key, displayName, avatarUrl }`, by name then `key`, no user ids; `key` is the same opaque per-member key as the Hall of Fame),
  shown with the "Quem vai / Quem foi" tiles of /events (`WhoIsGoing`); `goingCount` is the Vou count.
- Poll: 0-12 distinct dates (`pollDates`, `yyyy-MM-dd`). Saving diffs the options: kept dates keep their votes, removed
  dates lose theirs. A new vote replaces the earlier one. The poll is open while the gathering has no date and is not
  historical.
- The date: there is no date field. `POST /settle` with one of the gathering's own options sets `Date` and closes the
  poll (400 for another gathering's or an unknown option, 409 once settled or for a historical gathering). After that,
  votes are refused (409) and the dates no longer change (400). Changing or clearing a settled date is out of scope.
- Vou / Não vou works on every gathering, past and historical ones included (set, change, clear). Votes on the date
  poll stay closed (409) once settled or on a historical gathering.
- Number (task 155): automatic. Create gives `Number` = highest stored + 1; nobody (Owner included) types or edits it,
  and the form has no number field. Two creates racing for the same number: the loser hits the unique index (SQLite
  constraint, code 19) and retries with the next one, up to 5 tries, then 409 "Tente outra vez." (`trintuna:conflict`,
  portal text "Outro almoço foi criado ao mesmo tempo."). No migration: the column and its unique index stay.
- The answer buttons read Vou / Não vou on upcoming gatherings and Fui / Não fui on past ones (same radios and endpoints).
- Marcar quem foi (task 160): on a past gathering the Owner, or its organizer while listed, marks Fui for a person
  (`PUT /attendees/{key}`) or removes a Fui (`DELETE`). A mark is that person's ordinary answer row (`GatheringParticipant`,
  `WillAttend = true`), so "Quem foi", `goingCount` and the Hall of Fame count it with no extra code, and the member can
  still change it. `{key}` is the opaque row key (never a user id); the server accepts only a person who can see the page
  (Owner-role users and listed members, not expelled; otherwise 400 `key`). Marking someone who answered Não fui turns it
  into Fui; `DELETE` removes only a Fui (any Fui, the member's own answer included: they are the same row), never a Não
  fui, and is 204 when there is nothing to remove. The dialog shows "Marcar quem foi" (`TrintunaMarkWhoWent.tsx`:
  `MemberPicker` over the `hallOfFame[]` people, `PersonChip` per Fui with "Retirar Fui a …") only when `past && canEdit`.
- Hall of Fame (`hallOfFame[]`: `{ key, place, displayName, avatarUrl, count }`; `key` is an opaque per-member row key
  (SHA-256 of the user id, 16 hex), never the user id; "Hall of Fame" button, `HallOfFameDialog` in the /leaderboard
  row look): everyone who can see the page (Owner-role users and listed members, not expelled; the same set as
  `GET /organizers`), zeros included. `count` is the number of past gatherings (historical, or dated before today) the
  member answered Vou, read from the answers on every request (no stored counter); Vou on an upcoming gathering does
  not count. Dense ranking over every distinct count: everyone with a count shares its place, ordered by name (pt-PT,
  ignore case) then `key`; 5,5,3,0 gives 1 A and B, 2 C, 3 D. Medals for places 1-3, the number after. Members taken
  off the list or expelled are not ranked (their answers stay stored). The whole list comes in the board (one row per
  person who can see the page); the dialog shows 15 per page (`SimplePager`).
- Organizer: chosen by the Owner from `GET /organizers` (the Owner(s) and the access list); the server accepts the same
  set and refuses anyone else (400). A member taken off the list stays stored as organizer but can no longer open the
  page or edit.
- Share link (task 155): "Copiar link" in the gathering dialog copies `{origin}/trintuna?almoco={id}` (the id, stable
  if a number is ever reused); when the browser refuses the clipboard the link is shown to copy by hand. Opening a
  gathering sets `?almoco=` and closing drops it (`history.replaceState`, the /gallery pattern). Only the Owner and
  listed members see it; anyone else gets the "no access" notice. Installed app: the manifest scope is `/` and
  `launch_handler.client_mode` is `navigate-existing`, and the service worker leaves the query alone, so on Android the
  link opens in the app (Play Store TWA via `assetlinks.json`). iOS Home Screen apps never capture links: it opens in
  Safari (seen on the owner's iPhone, 2026-11-24). The only fix there is a native App Store wrapper published with
  `/.well-known/apple-app-site-association` (Universal Links); push notifications already open the installed app. No
  manifest or service-worker change.
- Access list: one list for the whole page; `PUT` replaces it (unknown or expelled ids: 400). Removing an account
  removes its rows (cascade); a deleted organizer leaves the gathering without one (SetNull).
- Seed (task 154): no migration inserts gatherings, so PROD gets none. The dev seed (`SeedData.Trintuna.cs`,
  `SeedTrintunaAsync`, full dataset only: `SeedData:SeedFullDataset=true` on a fresh database, set on local dev and Azure
  DEV, never on PROD) adds 1-6 (Vizela/Croquetes, Lamares/Bronha, Marco de Canaveses/Frango, Chaves/Conchita, Fafe/Coma,
  Mogadouro/Pássaro) as historical and 7º (Bragança/Jeans) under Próximos, all with no date and no organizer. Test
  databases start empty (tests add their own rows). DEV keeps the 7 rows the #365 version of the migration inserted,
  with 7º still historical (Passados): no SQL fix, by choice. Real dates and organizers are an owner check.
- Audit labels: `Gathering` "Almoço Trintuna", `TrintunaAccess` "Acesso Trintuna" (`ownerApi.ts`); display names in
  `ApplicationDbContext.EntityDisplay.cs` are deferred.
