# React Reuniões (`/meetings`)

Code: `portal/src/Meetings.tsx` (+ dialogs), `portal/src/meetingsApi.ts`, `Endpoints/MeetingEndpoints.cs` →
`IMeetingBoardService` (`MeetingBoardService`), rules in `Helpers/MeetingAccess.cs`. Underneath: `MeetingService`
(visibility filter, push on a new meeting), `MeetingRequestService` (push on a new request, rejection, reminder),
`MeetingParticipationService`, `MeetingAtaService` (`CanCreateOrEditAta`), `MeetingAtaConfirmationService`,
`AtaPdfService`, `DocumentStorageService`, `EmailNotificationService` + `IEmailTemplateRenderer`,
`PushNotificationService`, `AuditLogService`. Services do not check the caller; `MeetingBoardService` is the only gate.

Route: `/meetings`, signed-in members. Visitors: 302 to `/login?returnUrl=%2Fmeetings`; a Leitão gets "Acesso Restrito"
(whatever the role). Member menu: `MemberShell.tsx`, Gestão group, shown when `menu.management`. Push notifications
point at `/meetings`.

## Page

- Search (title, statement; runs in memory) and fiscal-year filter (Sept-Aug, default current, "Todos os anos"; years
  from the `FiscalYears` table, 2025 onwards).
- "Próximas Reuniões" (date >= today, soonest first) and "Reuniões Realizadas" (before today, newest first), 12 per page.
- Requests ("Pedidos de Reuniões"): status filter (Pendente / Aceite / Rejeitado), fiscal year on the proposed date,
  newest first, 4 per page.
- Ata: number, actual start / end, location, AG quorum basis, president, secretaries (CV: chosen from those who said
  "Vou"; AG: the Mesa's secretaries), attendance, Ordem de Trabalhos (discussion, decision, votes, result), closing
  text. Save, Gerar PDF, Publicar. Publish uploads the PDF to Documentação: `docs/{Environment}/{FY}/Atas CV {FY}/`,
  `Atas AG {FY}/` or (Direção) `Atas {FY}/`. A Direção ata is labelled "Constituição e Secretariado" / "Presidente".
  Attachments exist in schema and service only, no UI; delete has no UI either.
- Side effects: push on a new meeting (by type), new request (Owners + the relevant president), rejected request
  (author), reminder (Owners + president; Direção: Magister + Vice). Manual meeting email and cancellation email (by
  type, members with "Notificações por email"; duplicate addresses sent once). Manual push (by type, audited
  `PushNotificationSent`). Past meetings (date before today in Portugal) are not pushed as new
  (`MeetingService.CreateMeetingAsync`).
- Email preview is a sandboxed frame (`sandbox=""`, `srcDoc`). The site CSP forbids inline CSS, so the preview drops the
  template's `<style>` / `style=` and other-origin images and shows the text unstyled.
- Deep links from "As minhas ações pendentes": `/meetings?meeting={id}&ata=1` opens the ata editor if the member may
  write it (else the details); `/meetings?new={Type}` opens "Nova reunião" with that type if allowed. Parameters are
  removed once read; the server decides again on save.
- Editing a cancelled meeting keeps it cancelled. A non-CV meeting keeps no Tuno representative. Editing a CV meeting
  keeps its saved representative even if no longer a plain Tuno (a new one must be offered by the form). "Adicionar
  Membro" never offers or overwrites someone who already answered.

## Permissions

Driven by cargos (positions) and Veterano status more than by role. Veterano / Tunossauro = `ApplicationUser.CurrentRole`
(2+ / 6+ years from `YearTuno` / `MonthTuno`), not the category. Tuno (by time) = under 2 years. Leitão = the `Leitao`
category. Caloiro, Tuno Honorário and Fundador have no rule of their own. Direção positions: Magister, Vice-Magister,
Secretário, 1.º and 2.º Tesoureiro. Owner / Admin are the roles on the session; the list filter's "Admin with the Tuno
category" reads the role from the database. "Current" president = the position in `ApplicationUser.Positions`; when a
mandate ends the position leaves `Positions` and every right below goes with it.

### Permission matrix

Visibility (list, detail, participants, every per-meeting call; not seeing it => 404):

| Meeting type | Seen by |
| --- | --- |
| AG / AGE | everyone but a Leitão |
| CV | Veterano / Tunossauro by time, Magister, that meeting's Tuno representative, the Presidente do CV |
| Direção | Direção positions, Admins (role in the database) with the Tuno category |

Owner sees every type.

By member type (no position, not Admin / Owner):

| Action | Visitor | Leitão | Caloiro | Tuno (< 2 y) | Veterano / Tunossauro |
| --- | --- | --- | --- | --- | --- |
| Open `/meetings`, any `/api/meetings*` | 302 / 401 | 403 | yes | yes | yes |
| AG / AGE meetings | - | - | yes | yes | yes |
| CV meetings | - | - | - | only the one they represent | yes |
| Direção meetings | - | - | - | - | - |
| Participants + notes (seen, not cancelled) | - | - | yes | yes | yes |
| Vou / Não vou (before the day); remove own answer | - | - | yes | yes | yes |
| Read a published ata (after the day) | - | - | AG | AG + their CV | AG + CV |
| Read a draft ata | - | - | - | the CV they represent | - |
| Write / publish an ata | - | - | - | the CV they represent | - |
| Confirm / refuse a published ata | - | - | if "Vou" | if "Vou" | if "Vou" |
| Requests section (all types) | - | - | - | - | yes |
| Propose | - | - | - | - | CV |
| Delete own request; remind (pending) | - | - | - | - | yes |
| Create / edit / delete / cancel / email / push meetings | - | - | - | - | - |

A draft ata (content, "Ver Ata", PDF) is only for whoever may write it; published atas follow the reading rule above.
Participants and their notes are visible to everyone who sees the meeting.

### Roles and positions

On top of the base type. Someone holding several presidencies gets the union of all of them.

| Who | Extra |
| --- | --- |
| Mod | nothing |
| Admin | "Adicionar Membro" (anyone with categories who is not Leitão / Caloiro, as "Vou"). With the Tuno category: sees Direção meetings, proposes Direção. Never writes an ata. Cannot remove others' answers. |
| Owner | Creates any type; edits / deletes / cancels / reactivates / emails / pushes any meeting; accepts / rejects any request (if it sees the section); deletes any request, reminds on any pending one; removes anyone's answer; "Adicionar Membro"; writes / publishes any ata; proposes AG and Direção. Not exempt from the requests gate or the Leitão block. |
| Presidente da Mesa da AG | creates and manages AG / AGE; accepts / rejects AG requests (when it sees the section); writes AG atas |
| Magister | creates and manages Direção meetings (the Direção's president; there is no separate "Presidente da Direção"); sees CV + Direção; requests section; proposes CV, AG, Direção; writes Direção atas |
| Presidente do CV | creates and manages CV; accepts / rejects CV requests; proposes AG; writes CV atas; no "Propor CV" |
| Vice-Magister, Secretário, 1.º / 2.º Tesoureiro | see Direção; propose Direção |
| Presidente do Conselho Fiscal | proposes AG |
| 1.º / 2.º Secretário da Mesa | write / publish an AG ata saved with them as secretary (from the day after the meeting) |
| Secretary named on a CV / Direção ata | write / publish that ata (from the day after the meeting) |
| Tuno representative of a CV meeting | sees it; writes / publishes its ata (CV atas also allow the delegated writer, which is never set today) |

Who creates / manages meetings and who writes atas:

| Meeting type | Create / edit / delete / cancel / email / push | Ata draft |
| --- | --- | --- |
| AGO / AGE | Presidente da Mesa da AG, Owner | same (+ the 1.º / 2.º Secretário named on the ata) |
| Direção | Magister, Owner | same (+ the Secretário named on the ata) |
| CV | Presidente do CV, Owner | same (+ delegated writer, Tuno representative, secretary named on the ata) |

Create-form types are limited to what the member may create (`CanCreateMeetingType`); the Direção type is offered only
to the Magister and Owner. Propose rules: CV = Veterano / Tunossauro or Magister (never the Presidente do CV); Direção =
Owner, an Admin who is a Tuno, or a Direção position; AG = Owner, Magister, Presidente do CV, Presidente do Conselho
Fiscal. Accept / reject: Owner; CV by the Presidente do CV; AG by the Presidente da Mesa. Add participants: Admin or
Owner. Remove a participation: yourself, or Owner for anyone. Publishing an ata is separate, never automatic, and
allowed to whoever may write it.

Per-meeting conditions: email / push / cancel only while the meeting is not cancelled and its day has not passed;
reactivate only a cancelled meeting; "Vou / Não vou" only before the day; an ata is written from the day of the meeting
(secretaries named on it: from the next day) and read from the day after, never for a cancelled meeting; a published ata
cannot be edited; publish needs a saved draft.

## API

`/api/meetings`, signed-in. Writes need `X-CSRF-TOKEN`; every answer is `no-store`. Beyond "signed in, not a Leitão":

| Method + path | Rule |
| --- | --- |
| `GET /?fy=&q=` | the meetings the member sees; `fy` = `YYYY-YYYY`, `all`, or missing (current) |
| `GET /{id}` | sees it (404 otherwise) |
| `GET /form` | `CanCreateMeetings` (403) |
| `POST /` | `CanCreateMeetings`; type in the form's types |
| `PUT /{id}` | sees it; `CanManage`; type in the form's types |
| `DELETE /{id}` | sees it; `CanManage` |
| `GET` / `POST /{id}/cancel` | sees it; `CanManage`; upcoming, not cancelled (409) |
| `POST /{id}/uncancel` | sees it; `CanManage`; cancelled (409) |
| `GET /{id}/email`, `POST /{id}/email/preview`, `POST /{id}/email` | sees it; `CanManage`; upcoming, not cancelled |
| `GET` / `POST /{id}/push` | sees it; `CanManage`; upcoming, not cancelled |
| `PUT /{id}/participation` | sees it; before the day; not cancelled |
| `GET /{id}/participants` | sees it; not cancelled |
| `DELETE /{id}/participants/{pid}` | sees it; not cancelled; own answer or Owner |
| `GET /{id}/participants/candidates?q=`, `POST /{id}/participants` | sees it; not cancelled; Admin or Owner |
| `GET /{id}/ata` | sees it; after the day; not cancelled; published, or a draft the member may write |
| `GET /{id}/ata/edit`, `PUT /{id}/ata`, `POST /{id}/ata/publish` | sees it; on / after the day; not cancelled; may write; not published for writes |
| `GET /{id}/ata/pdf` | writers (draft or published), or readers of a published ata |
| `POST /{id}/ata/confirmation` | readers of a published ata who said "Vou" |
| `GET /requests?status=&fy=&page=&pageSize=` | `CanSeeRequests` (403) |
| `POST /requests` | CV: `CanProposeCv`; Direção: `CanProposeDirecao`; AGE: `CanProposeAg`; AGO: never |
| `POST /requests/{id}/accept` / `reject` | `CanSeeRequests`; `CanDecide`; pending (409) |
| `DELETE /requests/{id}`, `POST /requests/{id}/reminder` | `CanSeeRequests`; author or Owner; reminder only pending |

Answers carry only what the member may use: `tunoRepresentativeId` only for managers; `ataStatus` only when the member
may open that ata; recipient lists (names, pictures, years as Tuno; never email addresses) only to managers; no
cancellation reason.

Server-side limits: meeting title <= 200, statement <= 5000, location <= 200; request title <= 200, description <= 2000;
participation note <= 500; cancel reason <= 1000; push <= 500; email subject <= 300, body <= 10 000; ata location <= 200
(and start required), number <= 50, closing <= 5000, up to 100 agenda points (title <= 500, texts <= 5000, votes
0-100 000, result Aprovado / Rejeitado). A Tuno representative, secretary or added Tuno must be one the form offered.

## Follow-ups

- The fiscal-year filter ends 31 August at midnight; a meeting later that day only shows under "Todos os anos".
- The requests section uses the Veterano / Magister rule for every type, so some approvers and Direção proposers cannot
  see their requests.
- A published Direção ata PDF sits in `Atas {FY}`, and `DocumentationAuthorization.CanSeeFolder` only restricts the CV
  and AG folders, so every non-Leitão member can download it.
- `Meeting.DelegatedAtaWriterMemberId` is never set; ata attachments and delete have no UI.
