# React Trintuna (`/trintuna`)

The Trintuna gatherings ("Nº Almoço"): place, organizer, description, a date poll and Vou / Não vou. Only the Owner and
the members the Owner lists see the page (task 153, Issue #364).
Code: `portal/src/pages/Trintuna.tsx`, `components/TrintunaDialogs.tsx` (gathering + access dialogs),
`components/TrintunaForm.tsx` (create / edit, "Marcar data"), `data/trintunaApi.ts`; `Endpoints/TrintunaEndpoints.cs` >
`ITrintunaService` (`TrintunaService.cs`, `.Gatherings.cs`, `.Answers.cs`), rules in `Helpers/TrintunaAuthorization.cs`.
Tables (migration `AddTrintunaGatherings`): `TrintunaAccesses`, `Gatherings`, `GatheringPollOptions`,
`GatheringPollVotes`, `GatheringParticipants`.
Tests: `Integration.Tests/Api/TrintunaApiTests*.cs`, `Application.Tests/Helpers/TrintunaAuthorizationTests.cs`,
`MemberShellTests`, `MemberPagesTests`, `ShellPagesTests`, `AccountEndpointTests`.

## Routes

| Route | Who |
| --- | --- |
| `/trintuna` | signed-in members reach the page; the Owner and listed members see the gatherings, anyone else "Não tem acesso a esta página."; visitors `302` to `/login?returnUrl=%2Ftrintuna` |

Link: member menu (`MemberShell.tsx`, Tuna group "Trintuna"), shown when `GET /api/account/me` `menu.trintuna` is true
(Owner, or a `TrintunaAccess` row). The icon (`smile`) is a placeholder until the owner picks one.

## API (`/api/trintuna`, no-store; writes need `X-CSRF-TOKEN`, 400 without)

| Route | Who |
| --- | --- |
| `GET /` → `{ canManage, upcoming[], past[] }` | Owner, listed members |
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
  Presence shows the count of "Vou" only.
- Poll: 0-12 distinct dates (`pollDates`, `yyyy-MM-dd`). Saving diffs the options: kept dates keep their votes, removed
  dates lose theirs. A new vote replaces the earlier one. The poll is open while the gathering has no date and is not
  historical.
- The date: there is no date field. `POST /settle` with one of the gathering's own options sets `Date` and closes the
  poll (400 for another gathering's or an unknown option, 409 once settled or for a historical gathering). After that,
  votes are refused (409) and the dates no longer change (400). Changing or clearing a settled date is out of scope.
- Presence on a past gathering is refused (409).
- Organizer: chosen by the Owner from the access list (the Owner may also be one); anyone else is refused (400). A member
  taken off the list stays stored as organizer but can no longer open the page or edit.
- Access list: one list for the whole page; `PUT` replaces it (unknown or expelled ids: 400). Removing an account
  removes its rows (cascade); a deleted organizer leaves the gathering without one (SetNull).
- Seed: gatherings 1-7 (Vizela/Croquetes, Lamares/Bronha, Marco de Canaveses/Frango, Chaves/Conchita, Fafe/Coma,
  Mogadouro/Pássaro, Bragança/Jeans) come from `HasData` in `GatheringConfiguration` (in the migration and every
  `EnsureCreated` test database), marked `IsHistorical`, with no date and no organizer. Real dates and organizers are an
  owner check.
- Audit labels: `Gathering` "Almoço Trintuna", `TrintunaAccess` "Acesso Trintuna" (`ownerApi.ts`); display names in
  `ApplicationDbContext.EntityDisplay.cs` are deferred.
