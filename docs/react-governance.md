# React Órgãos Sociais (`/roles`)

`/roles` is React (`portal/src/Governance.tsx`, `GovernanceManage.tsx`). It shows the public mandate, the RGI for
members, and lets Mod, Admin and Owner add fiscal years and assign or remove positions in modals on the same page.
Code: `Endpoints/GovernanceEndpoints.cs`, `Helpers/GovernanceAuthorization.cs`, `IGovernanceManagementService`,
`GovernanceService.Structure`.

| Route | Behavior |
| --- | --- |
| `/roles` (`?fy=2024-2025` picks a mandate) | public mandate for everyone; RGI for members; management for Mod/Admin/Owner |
| `/member/roles` | `302` to `/roles`, query kept (GET/HEAD only) |

## Permissions

Enforced server-side by `GovernanceAuthorization`.

| Who | Public mandate | RGI | Fiscal years, assignments |
| --- | --- | --- | --- |
| Visitor | yes | `401` | `401` |
| Member | yes | yes | `403` |
| Mod, Admin | yes | yes | yes |
| Owner | yes | yes | yes (inherits Admin) |

Every write needs the antiforgery token in `X-CSRF-TOKEN`. The API enforces every rule; the UI only hides buttons.

## API

| Endpoint | Who | Notes |
| --- | --- | --- |
| `GET /api/public/governance?fiscalYear=` | anyone | public fields only; the selector lists only years with holders |
| `GET /api/governance/rgi` | Member+ | `{ url }`, `null` when unavailable; `no-store` |
| `GET /api/governance/manage?fiscalYear=` | Mod+ | every fiscal year, the one shown, start years still creatable, bodies, positions (`position` enum name, `title`), holders (`assignmentId`, display name, full name, avatar) |
| `GET /api/governance/members?q=` | Mod+ | at most 20 `{ id, displayName, fullName, avatarUrl }`; email is matched, never returned; expelled members excluded |
| `POST /api/governance/years` `{ startYear }` | Mod+ | 400 `startYear` on duplicate, < 1991 or future |
| `POST /api/governance/assignments` `{ fiscalYear, position, userId }` | Mod+ | 400 `fiscalYear` / `position` / `userId` / `assignment` (validation message) |
| `DELETE /api/governance/assignments/{id}` | Mod+ | 404 when gone |

Writes answer with the refreshed management state. No email, phone, birth date, note or audit field leaves the server;
no EF entity is returned.

## Data and rules

- Tables: `FiscalYears` (`StartYear`, `EndYear = StartYear + 1`), `RoleAssignments` (`UserId`, `Position`, `StartYear`,
  `EndYear`, `Notes`, audit fields), `AspNetUsers` (`Positions`, `Categories`, role). Positions are a fixed enum of 13 in
  5 bodies (`GovernanceService.Structure`); no body or position creation in the database.
- Fiscal years: create only (no edit, no delete), from 1991 up to the current fiscal year, minus existing ones. Default
  shown: `?fy=`, else the current one, else the newest. The head is the shared `events-page__head`; "Adicionar ano
  letivo" shows only while `availableStartYears` is not empty.
- Assignment: `+` on an empty position, search members (first/last name, nickname or email; Leitões hidden), then
  `IRoleAssignmentValidationService`: no Leitão; no Caloiro as any President; the Conselho de Veteranos President must
  be a Tuno Veterano; one holder per position and year; one position per member and year, Ensaiador excepted. Then
  `RoleAssignment` + `IRoleManagementService.PromoteUserForPositionAsync`. Remove (with confirmation) deletes +
  `DemoteUserForPositionAsync`.
- Promote / demote change the member's role and `Positions` only for the current fiscal year: Direção gets Admin (Mod
  for a Caloiro); the three Presidents and Ensaiador get Admin; secretaries and relators none. Owner is never demoted.
  Demotion forces a sign-out via the security stamp.
- New mandate: once the current fiscal year has a holder, `SyncCurrentMandateAsync` makes every member's `Positions`
  (and Admin / Mod, same rules) follow that year only: last year's holders lose their seats, re-elected members keep the
  role. Runs after each current-year assignment and once at startup; a new fiscal year with no holders yet keeps the
  previous mandate. A dropped Admin is signed out at once (security stamp + role probe); a new Admin / Mod gets the role
  on the next principal refresh (Identity's 30 min `ValidationInterval`) or next sign-in.
- RGI: `docs/rtub_rgi.pdf` in R2, a pre-signed URL in an iframe; "not available" on any storage error, including a
  storage that cannot be built (local run without R2 credentials), which must not break the management endpoints. The
  RGI does not depend on a fiscal year existing. No RGI text in the app or database.
- Copy: "Sem registo" for a vacant position; public position titles ("1.º Tesoureiro"). The shared
  `portal-governing-body-card` is also used by Hall of Fame.

## Follow-ups

- The Hierarquia is still not shown (future "Conhece a Tuna").
