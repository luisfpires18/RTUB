# Permissions: /meetings

Source: `MeetingAccess`, enforced by `MeetingBoardService`. Page doc: [react-meetings](../react-meetings.md).
This page is driven by **cargos and Veterano status** more than by role.

- A **Leitão is refused the whole page**, whatever the role (Owner and Admin included).
- Task 075: Owner sees every meeting type (CV and Direção included), so Owner can open and write every body's atas.

| Action | Who |
| --- | --- |
| Create a meeting | Owner (any); each body's current president: Presidente da Mesa (AG / AGE), Magister (Direção), Presidente do CV (CV) — the union for several |
| Edit, delete, cancel, reactivate, email, push a meeting | the same: Owner any, each president their own body's |
| Propose a CV meeting | Veterano / Tunossauro or Magister (never the Presidente do CV) |
| Propose a Direção meeting | Owner, an Admin who is a Tuno, or a Direção position (Magister, Vice, Secretário, Tesoureiros) |
| Propose an Assembleia Geral | Owner, Magister, Presidente do CV, Presidente do Conselho Fiscal |
| See "Pedidos de Reuniões" | Veterano / Tunossauro or Magister |
| Accept / reject a request | Owner; CV: Presidente do CV; AG: Presidente da Mesa |
| Delete or remind about a request | its author, or Owner |
| Add participants | Admin or Owner (members who are neither Leitão nor Caloiro) |
| Remove a participation | yourself, or Owner for anyone |
| Write or edit an ata (draft) | Owner any type; Presidente da Mesa: AG / AGE; Magister: Direção; Presidente do CV: CV (+ the named secretaries / delegate / Tuno representative, see below). Admin alone does not |
| Publish an ata | whoever may write it (never automatic) |
| Read a published ata | Owner; CV: Veterano / Tunossauro, Magister or the meeting's Tuno representative; AG: anyone but a Leitão; Direção: anyone who sees it |

Drafts of atas are visible only to the people who may write them.

## Presidents and atas (task 075, #256 — supersedes A2's wording, A3 and A6 where they conflict)

The current president of each body creates and manages that body's meetings and writes its atas; Owner every type.
"Current" = `ApplicationUser.Positions`, the mandate in office. Admin alone gives none of this.

| Meeting type | Create / edit / delete / cancel / notify the meeting | Create / edit the ata (draft) |
| --- | --- | --- |
| Assembleia Geral Ordinária / Extraordinária | Presidente da Mesa da AG, or Owner | Presidente da Mesa da AG, or Owner (+ the 1.º / 2.º Secretário named on that ata, from the day after) |
| Reunião de Direção | **Magister** (the Direção's president: the position model has no separate "Presidente da Direção"), or Owner | Magister, or Owner (+ the Secretário named on that ata) |
| Conselho de Veteranos | Presidente do CV, or Owner | Presidente do CV, or Owner (+ the delegated writer, the meeting's Tuno representative, the secretary named on that ata) |

- **Union**: someone presiding over several bodies gets every one of them (no "the Mesa wins").
- **Owner sees every type** (CV and Direção included), so Owner's ata authority is reachable; no other president gains
  another body's meetings. The CV president sees CV meetings even if not a Veterano by time.
- **Publishing** an ata stays a separate operation, never automatic, allowed to the same people who may write that ata
  (unchanged rule).
- The extra writers in brackets are the pre-075 delegations (secretaries, delegate, Tuno representative), kept as they
  were; the owner's matrix names the presidents and Owner.
- **Ended mandate**: the position leaves `Positions` at the mandate sync, and with it every one of these rights.
- **Registering a past meeting** (a president catching up) was already possible (no future-only date rule); since 075
  such a meeting is **not pushed** as a new one (`MeetingService.CreateMeetingAsync` skips the broadcast when the date
  is before today in Portugal). No date bound was added.
- Changed from before: the Magister now creates and manages Direção meetings (before: nobody but Owner); an Admin with
  the Tuno category, and the other Direção positions, no longer get the Direção type in the create form (before: only
  reachable by someone who already presided over another body).
