# React Trintuna (`/trintuna`)

The Trintuna gatherings ("Nº Almoço"): place, organizer, description, a date poll, Vou / Não vou (past ones too, with
who answered) and a Top 3. Only the Owner and the members the Owner lists see the page (task 153, Issue #364; task 154,
Issue #366).
Code: `portal/src/pages/Trintuna.tsx`, `components/TrintunaDialogs.tsx` (gathering + access dialogs),
`components/TrintunaForm.tsx` (create / edit, "Marcar data"), `data/trintunaApi.ts`; `Endpoints/TrintunaEndpoints.cs` >
`ITrintunaService` (`TrintunaService.cs`, `.Gatherings.cs`, `.Answers.cs`), rules in `Helpers/TrintunaAuthorization.cs`.
Tables (migration `AddTrintunaGatherings`): `TrintunaAccesses`, `Gatherings`, `GatheringPollOptions`,
`GatheringPollVotes`, `GatheringParticipants`.
Tests: `Integration.Tests/Api/TrintunaApiTests*.cs` (`.Top.cs`: the Top 3), `Application.Tests/Helpers/TrintunaAuthorizationTests.cs`,
`Application.Tests/Data/SeedDataTrintunaTests.cs`, `MigrationChainTests.MigratedDatabase_HasNoTrintunaGatherings`,
`Web.Tests/ReactPortal/TrintunaFormSourceTests.cs`, `SharedSelectSourceTests.cs`, `MemberShellTests`, `MemberPagesTests`,
`ShellPagesTests`, `AccountEndpointTests`.

## Routes

| Route | Who |
| --- | --- |
| `/trintuna` | signed-in members reach the page; the Owner and listed members see the gatherings, anyone else "Não tem acesso a esta página."; visitors `302` to `/login?returnUrl=%2Ftrintuna` |

Link: member menu (`MemberShell.tsx`, Tuna group "Trintuna"), shown when `GET /api/account/me` `menu.trintuna` is true
(Owner, or a `TrintunaAccess` row). The icon (`smile`) is a placeholder until the owner picks one.

## API (`/api/trintuna`, no-store; writes need `X-CSRF-TOKEN`, 400 without)

| Route | Who |
| --- | --- |
| `GET /` → `{ canManage, upcoming[], past[], top[] }` | Owner, listed members |
| `POST /gatherings` | Owner |
| `PUT /gatherings/{id}` | Owner; the gathering's organizer (number and organizer stay as stored) |
| `DELETE /gatherings/{id}` → 204 | Owner |
| `POST /gatherings/{id}/settle` `{ optionId }` | Owner; the gathering's organizer |
| `PUT /gatherings/{id}/vote` `{ optionId }` | Owner, listed members |
| `PUT /gatherings/{id}/presence` `{ willAttend }`, `DELETE` → 204 | Owner, listed members |
| `GET /access`, `PUT /access` `{ userIds[] }` | Owner |

## Permissions

| Who | See | Vote, Vou / Não vou | Edit a gathering, settle its date | Create, delete, access list |
| --- | --- | --- | --- | --- |
| Visitor | 401 | 401 | 401 | 401 |
| Member not on the list (Admin and Mod included) | 404 | 404 | 404 | 404 |
| Listed member | yes | yes | only as its organizer (403 otherwise) | 403 |
| Owner | yes | yes | all | yes |

## Rules

- Lists (Portugal day, `PortugalTime.Today`): Passados = historical or dated before today, newest first, then by number
  (descending); Próximos = the rest, dated soonest first, undated after, by number.
- The gathering DTO carries no user id except `organizerUserId`, and only for the Owner. The poll has the portal's
  `MeetingPoll` shape (rendered by `PollCard` with `onVote`), counts only, no voter names; each option also has its `date`.
  Each gathering lists who answered, `going[]` and `notGoing[]` (`{ displayName, avatarUrl }`, by name, no user ids),
  shown with the "Quem vai / Quem foi" tiles of /events (`WhoIsGoing`); `goingCount` is the Vou count.
- Poll: 0-12 distinct dates (`pollDates`, `yyyy-MM-dd`). Saving diffs the options: kept dates keep their votes, removed
  dates lose theirs. A new vote replaces the earlier one. The poll is open while the gathering has no date and is not
  historical.
- The date: there is no date field. `POST /settle` with one of the gathering's own options sets `Date` and closes the
  poll (400 for another gathering's or an unknown option, 409 once settled or for a historical gathering). After that,
  votes are refused (409) and the dates no longer change (400). Changing or clearing a settled date is out of scope.
- Vou / Não vou works on every gathering, past and historical ones included (set, change, clear). Votes on the date
  poll stay closed (409) once settled or on a historical gathering.
- Top 3 (`top[]`: `{ key, place, displayName, avatarUrl, count }`; `key` is an opaque per-member row key (SHA-256 of the user id, 16 hex), never the user id, "Top 3" button, `TopDialog` in the /leaderboard row look):
  `count` is the number of past gatherings (historical, or dated before today) the member answered Vou, read from the
  answers on every request (no stored counter, no input). Dense ranking: the 3 highest distinct counts get places 1-3,
  everyone with that count shares the place (and the medal), ordered by name; 0 is never shown. 5,5,3,2 gives gold A
  and B, silver C, bronze D. Members later taken off the access list still count (owner check pending).
- Organizer: chosen by the Owner from the access list (the Owner may also be one); anyone else is refused (400). A member
  taken off the list stays stored as organizer but can no longer open the page or edit.
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
