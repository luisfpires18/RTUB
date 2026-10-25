# Permissions: /meetings

Source: `MeetingAccess`, enforced by `MeetingBoardService`. Page doc: [react-meetings](../react-meetings.md).
This page is driven by **cargos and Veterano status** more than by role.

- A **Leitão is refused the whole page**, whatever the role (Owner and Admin included).
- Owner is not exempt from what a meeting type shows (CV / Direção).

| Action | Who |
| --- | --- |
| Create a meeting | Owner, Presidente da Mesa (AG / AGE only), Presidente do CV (CV only) |
| Edit, delete, cancel, reactivate, email, push a meeting | Owner (any); the Presidente da Mesa for AG / AGE; the Presidente do CV for CV |
| Propose a CV meeting | Veterano / Tunossauro or Magister (never the Presidente do CV) |
| Propose a Direção meeting | Owner, an Admin who is a Tuno, or a Direção position (Magister, Vice, Secretário, Tesoureiros) |
| Propose an Assembleia Geral | Owner, Magister, Presidente do CV, Presidente do Conselho Fiscal |
| See "Pedidos de Reuniões" | Veterano / Tunossauro or Magister |
| Accept / reject a request | Owner; CV: Presidente do CV; AG: Presidente da Mesa |
| Delete or remind about a request | its author, or Owner |
| Add participants | Admin or Owner (members who are neither Leitão nor Caloiro) |
| Remove a participation | yourself, or Owner for anyone |
| Write or edit an ata | **Owner only** (Admin alone does not) |
| Read a published ata | Owner; CV: Veterano / Tunossauro, Magister or the meeting's Tuno representative; AG: anyone but a Leitão; Direção: anyone who sees it |

Drafts of atas are visible only to the people who may write them.
