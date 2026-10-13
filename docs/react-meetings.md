# React Reuniões (`/meetings`), task 034

The page is React (`portal/src/Meetings.tsx` and its dialogs, client `portal/src/meetingsApi.ts`) over
`Endpoints/MeetingEndpoints.cs` → `IMeetingBoardService` (`MeetingBoardService`, rules in `Helpers/MeetingAccess.cs`).
It keeps the old services underneath: `MeetingService` (visibility filter, push on a new meeting),
`MeetingRequestService` (push on a new request, a rejection and a reminder), `MeetingParticipationService`,
`MeetingAtaService` (`CanCreateOrEditAta`), `MeetingAtaConfirmationService`, `AtaPdfService`, `DocumentStorageService`,
`EmailNotificationService` + `IEmailTemplateRenderer`, `PushNotificationService` + `PushNotificationFactory`,
`AuditLogService`. It replaces the Blazor `Pages/Activities/Meetings.razor` (4 648 lines). DEV only; **no schema
change**, no migration.

| Route | Owner after 034 |
| --- | --- |
| `/meetings` | React, signed-in members (visitors: 302 to `/login?returnUrl=%2Fmeetings`); a Leitão gets "Acesso Restrito" |

Links: the member menu (`MemberShell.tsx`, Gestão group: "Reuniões", shown when `menu.management`); the Blazor
`MainLayout` "Gestão" dropdown links `/meetings` as a plain full-page link. Push notifications keep pointing at
`/meetings`.

## Audit (old page)

`[Authorize]`, `@rendermode InteractiveServer`. The page only **hid buttons**; none of the meeting, request,
participation or ata services checked who called them. Rules were spread over the page (≈12 gate methods),
`MeetingCard`, `MeetingRequestCard`, `MeetingService.ApplyVeteranoFilterAsync` (list) / `GetMeetingByIdAsync` (unused by
the page), `MeetingAtaService.CanCreateOrEditAta` and `MeetingAtaConfirmationService`.

- **Header actions**: "Criar Reunião", "Propor Reunião de CV", "Propor Reunião Direção", "Propor Assembleia Geral"
  (always drawn, disabled when not allowed); the same as a mobile bottom bar.
- **Filters**: search over title and statement; fiscal year (Sept-Aug, default the current one, "Todos os anos");
  fiscal years from the `FiscalYears` table, 2025 onwards, newest first.
- **Próximas Reuniões** (date ≥ today, soonest first) and **Reuniões Realizadas** (before today, newest first), 12 per
  page (6/12/18/24), each with an empty state.
- **Meeting card**: status (Agendada / Realizada / Cancelada), VOU / NÃO VOU (FUI / NÃO FUI once completed), type,
  date, time, location, organizer + first position, the CV Tuno representative; participants count; "Vou" / "Não vou"
  before the day; "remove my answer" from the day on; Ver Detalhes; Ata (writers, completed, not published); Ver Ata;
  managers: edit / delete, and Email / Push / Cancelar while upcoming, Reverter when cancelled.
- **Dialogs**: details; create / edit (type limited to what the member may create, title, date-time, location, CV Tuno
  representative, statement ≤ 5000); delete; email (subject + statement prefill, HTML preview, recipients / not
  receiving, optional CV Tuno); push (message ≤ 500, recipients with / without push, optional CV Tuno); cancel (reason ≤
  1000, optional email to members); participation (note); participants ("Vão / Não vão participar", notes, badges,
  delete; "Adicionar Membro" search for Admin / Owner); ata editor; ata view (+ confirm / refuse); publish confirm;
  proposal; request details / delete / reminder.
- **Pedidos de Reuniões** (requests): status filter (Pendente / Aceite / Rejeitado), the page's fiscal year on the
  proposed date, newest proposed first, 4 per page (4/8/12/16/20).
- **Ata**: number, actual start / end, location, AG quorum basis, president (holder of the position), secretaries (CV:
  chosen from those who said "Vou"; AG: the Mesa's secretaries), attendance (those who said "Vou"), Ordem de Trabalhos
  (title, discussion, decision, votes for / against / abstain, result), closing text, status. Save, Gerar PDF, Publicar
  (the PDF goes to Documentação: `docs/{Environment}/{FY}/Atas CV {FY}/`, `Atas AG {FY}/` or `Atas {FY}/`). Delete existed in code but was never
  reachable. Attachments: schema and service only, no UI.
- **Side effects**: push on a new meeting (by type), on a new request (Owners + the relevant president), on a rejected
  request (author) and on a reminder (Owners + president; Direção: Magister + Vice); manual meeting email and
  cancellation email (by type, members with "Notificações por email"); manual push (by type, audited
  `PushNotificationSent`); ata PDF upload on publish.

## Who is who (unchanged)

- **Veterano / Tunossauro** = `ApplicationUser.CurrentRole` (2+ / 6+ years from `YearTuno` / `MonthTuno`), **not** the
  category. **Tuno** (by time) = under 2 years. Leitão = the `Leitao` category. Caloiro, Tuno Honorário and Fundador have
  no rule of their own (they are judged by `YearTuno` and the Leitão category).
- **Direção positions**: Magister, Vice-Magister, Secretário, 1.º and 2.º Tesoureiro.
- **Owner / Admin** = the roles on the session (the old page's cached `IsInRole`). The list filter's "Admin with the
  Tuno category" reads the role from the database, as before.

## Decisions (034 review)

| | Decision |
| --- | --- |
| A1 | **Changed for security.** A draft ata (content, "Ver Ata", PDF) is only for whoever may write that ata; published atas follow the old reading rule. Before, any reader of the meeting could open a draft. |
| A2 | Admin alone never writes an ata (the page passed only "Owner" to `CanCreateOrEditAta`). |
| A3 | Owner keeps the current visibility limits: no CV / Direção meetings or requests it does not already see. |
| A4 | Requests section stays gated by Veterano / Tunossauro (by time) or Magister, showing every type. |
| A5 | A Tuno representative sees their CV meeting (the list's rule), detail included. |
| A6 | Only Owner, Presidente da Mesa (AG / AGE) and Presidente do CV (CV) create and manage meetings; the form's types are the old `CanCreateMeetingType` for them. |
| A7 | Participants and their notes stay visible to everyone who sees the meeting. |

## Permission matrix

Old = the Blazor page's effective behaviour; New = the API. They are the same everywhere **except A1** (draft atas).
"Sees" = the meeting is in `MeetingService.GetAllMeetingsAsync`'s list for that member.

**Visibility** (list, detail, participants, every per-meeting call; not seeing ⇒ 404):

| Meeting type | Seen by |
| --- | --- |
| AG / AGE | everyone but a Leitão |
| CV | Veterano / Tunossauro by time, Magister, and that meeting's Tuno representative |
| Direção | Direção positions, and Admins (role in the database) with the Tuno category |

**By member type** (no position, not Admin / Owner):

| Action | Visitor | Leitão | Caloiro | Tuno (< 2 y) | Veterano / Tunossauro |
| --- | --- | --- | --- | --- | --- |
| Open `/meetings`, any `/api/meetings*` | 302 / 401 | 403 "Acesso Restrito" | yes | yes | yes |
| AG / AGE meetings | – | – | yes | yes | yes |
| CV meetings | – | – | – | only the one they represent | yes |
| Direção meetings | – | – | – | – | – |
| Participants + notes (seen, not cancelled) | – | – | yes | yes | yes |
| Vou / Não vou (before the day) | – | – | yes | yes | yes |
| Remove own answer (seen, not cancelled) | – | – | yes | yes | yes |
| Read a **published** ata (after the day) | – | – | AG | AG + their CV | AG + CV |
| Read a **draft** ata | – | – | – (old: yes) | the CV they represent | – (old: yes) |
| Write / publish an ata | – | – | – | the CV they represent | – |
| Confirm / refuse a published ata | – | – | if "Vou" | if "Vou" | if "Vou" |
| Requests section (all types, author, description) | – | – | – | – | yes |
| Propose | – | – | – | – | CV |
| Delete own request; remind (pending) | – | – | – | – | yes |
| Create / edit / delete / cancel / email / push meetings | – | – | – | – | – |

**Roles and positions** (on top of the base type):

| Who | Extra |
| --- | --- |
| Mod | nothing |
| Admin | "Adicionar Membro" (anyone with categories who is not Leitão / Caloiro, as "Vou"). With the Tuno category: sees Direção meetings, proposes Direção. **No ata writing** (A2). Cannot remove others' answers. |
| Owner | Create any type; edit / delete / cancel / reactivate / email / push any meeting **it sees**; accept / reject any request **if it sees the section**; delete any request, remind on any pending one; remove anyone's answer; "Adicionar Membro"; write / publish any ata of a meeting it sees; propose AG and Direção. Not exempt from visibility (A3), the requests gate (A4) or the Leitão block. |
| Magister | sees CV + Direção; requests section; proposes CV, AG, Direção; writes Direção atas |
| Vice-Magister, Secretário, 1.º / 2.º Tesoureiro | see Direção; propose Direção |
| Presidente da Mesa da AG | creates AG / AGE; manages AG / AGE meetings; accepts / rejects AG requests (when it sees the section); writes AG atas |
| 1.º / 2.º Secretário da Mesa | write / publish an AG ata saved with them as secretary (from the day after the meeting) |
| Presidente do CV | creates CV; manages CV meetings; accepts / rejects CV requests; proposes AG; writes CV atas; no "Propor CV" |
| Presidente do Conselho Fiscal | proposes AG |
| Secretary named on a CV / Direção ata | write / publish that ata (from the day after the meeting) |
| Tuno representative of a CV meeting | sees it; writes / publishes its ata |

If someone is both Presidente da Mesa and Presidente do CV, the Mesa wins (AG only), as before.

**Per-meeting conditions** (unchanged): email / push / cancel only while the meeting is not cancelled and its day has
not passed; reactivate only a cancelled meeting; "Vou / Não vou" only before the day; the ata only from the day of the
meeting (writing) or the day after (reading), never for a cancelled meeting; a published ata cannot be edited; publish
needs a saved draft. On the meeting day itself the old page did not yet know the ata, so the secretaries named on it
write it from the next day (kept).

## API (`/api/meetings`, signed-in; writes need `X-CSRF-TOKEN`; every answer `no-store`)

| Method + path | Rule (beyond "signed in, not a Leitão") |
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
| `GET /{id}/ata` | sees it; after the day; not cancelled; old reading rule; published, or a draft the member may write (A1) |
| `GET /{id}/ata/edit`, `PUT /{id}/ata`, `POST /{id}/ata/publish` | sees it; on / after the day; not cancelled; may write (A2); not published for writes |
| `GET /{id}/ata/pdf` | writers (draft or published), or readers of a published ata (A1) |
| `POST /{id}/ata/confirmation` | readers of a published ata who said "Vou" |
| `GET /requests?status=&fy=&page=&pageSize=` | `CanSeeRequests` (403) |
| `POST /requests` | CV: `CanProposeCv`; Direção: `CanProposeDirecao`; AGE: `CanProposeAg`; AGO: never |
| `POST /requests/{id}/accept` / `reject` | `CanSeeRequests`; `CanDecide`; pending (409) |
| `DELETE /requests/{id}`, `POST /requests/{id}/reminder` | `CanSeeRequests`; author or Owner; reminder only pending |

Answers carry only what the member may use: `tunoRepresentativeId` only for managers; `ataStatus` only when the member
may open that ata (a draft is never revealed to a non-writer); recipient lists (names, pictures, years as Tuno; never
email addresses) only to managers; no cancellation reason (the old page never showed it).

## Intentional changes

1. **A1**, the only permission change: draft atas only for their writers (view, PDF, "Ver Ata").
2. **Validation enforced server-side**, as the old forms declared but never ran (their buttons sat outside the form):
   meeting type / title (≤ 200) / date / statement (≤ 5000) required, location ≤ 200; request title (≤ 200) / date /
   description (≤ 2000) required, location ≤ 200; participation note ≤ 500; cancel reason ≤ 1000; push ≤ 500; email
   subject ≤ 300 and body ≤ 10 000 (new caps); ata location (≤ 200) and start required, number ≤ 50, closing ≤ 5000, up
   to 100 agenda points (title ≤ 500, texts ≤ 5000, votes 0-100 000, result Aprovado / Rejeitado); a Tuno
   representative, secretary or added Tuno must be one the form offered.
3. **Search runs in memory** (the old one ran a case-insensitive `Contains` inside the SQL query).
4. **Editing a cancelled meeting keeps it cancelled** (the old edit form silently reactivated it).
5. A non-CV meeting keeps no Tuno representative (the old hidden select could keep one).
6. Duplicate email addresses are sent once.
7. The email preview is shown in a sandboxed frame (`sandbox=""`, `srcDoc`); the site's CSP forbids inline CSS, so the
   preview drops the template's `<style>` / `style=` and other-origin images first and shows the email's text unstyled.
8. UI only: the old mobile bottom bar is gone (the header buttons wrap); "Reverter" asks for confirmation; a short
   message confirms create, email, push, cancel and a proposal; request cards also show the request's title; a Direção
   ata is labelled "Constituição e Secretariado" / "Presidente" instead of the AG "Mesa" wording the old page reused.

Kept as the old page behaved (checked in review): "Adicionar Membro" never offers nor overwrites someone who already
answered ("Vou" or "Não vou"); editing a CV meeting keeps its saved Tuno representative even once that member is no
longer a plain Tuno (a *new* representative must be one the form offers); the form's types read "Assembleia Geral
Ordinária (AGO)", "(AGE)", "(CV)" as before.

## Deferred (recorded, not changed)

The fiscal-year filter ends on 31 August at midnight (a meeting later that day only shows under "Todos os anos"); Owner
does not see CV / Direção meetings or requests unless its own rule allows (A3); the requests section is the Veterano /
Magister rule for every type, so some approvers and Direção proposers cannot see their requests (A4); the Direção
branch of `CanCreateMeetingType` stays unreachable (A6); `Meeting.DelegatedAtaWriterMemberId` is never set; the ata
attachments and delete have no UI. Publishing puts the PDF in Documentação under `Atas CV {FY}`, `Atas AG {FY}` or, for
a Direção ata, `Atas {FY}`, and `DocumentationAuthorization.CanSeeFolder` only restricts the first two, so a published
Direção ata PDF can be downloaded there by every non-Leitão member (as before 034).

## Retired (034)

Deleted: `Pages/Activities/Meetings.razor` (+ `Meetings.razor.css`); Shared `Cards/MeetingCard.razor`,
`Cards/MeetingRequestCard.razor` (+ `.razor.css`), `Modals/MeetingParticipationModal.razor` (+ `.razor.css`);
`wwwroot/css/3-components/meeting-card.css` and its `site.css` import; the bUnit `MeetingsPageTests` and
`MeetingCardTests`. `MainLayout`'s "Gestão → Reuniões" is now a plain link with `data-enhance-nav="false"`. **10 Blazor
routes left** (11 before). `wwwroot/css/4-pages/meetings.css` stays, without the page's `.meeting-grid`: its
`.member-avatar-small` (32px) still sizes the avatars in the `/users` dialogs. Kept: every meeting service, repository,
entity and DTO, `AtaPdfService`, `EmailRecipientsPreview` (`/emails`), `EnrollmentCard` (now without a caller) and the
Application / Core / Integration meeting tests.
