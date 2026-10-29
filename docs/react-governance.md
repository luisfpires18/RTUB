# React Órgãos Sociais (`/roles`, React tracks 008 and 016)

`/roles` is React (`portal/src/Governance.tsx`). The public mandate (008) is `GET /api/public/governance`.
**Since 016 it is the whole module:** signed-in members open the RGI there, and Mod, Admin and Owner add fiscal
years and assign or remove positions in modals on the same page (`GovernanceManage.tsx` →
`Endpoints/GovernanceEndpoints.cs` → `IGovernanceManagementService`). The members' Blazor `/member/roles`
(008-015, `MemberGovernance.razor`) is retired: a `302` to `/roles`, query kept. DEV only; **no schema change**.

| Route | Owner after 016 |
| --- | --- |
| `/roles` (`?fy=2024-2025` picks a mandate) | React: public mandate for everyone; RGI for members; management for Mod/Admin/Owner |
| `/member/roles` | `302` → `/roles` + query (GET/HEAD only) |

## Audit of the retired `/member/roles`

- **Data:** `FiscalYears` (`StartYear`, `EndYear = StartYear + 1`), `RoleAssignments` (`UserId`, `Position`,
  `StartYear`, `EndYear`, `Notes`, audit fields), `AspNetUsers` (`Positions`, `Categories`, role). Positions are a
  fixed enum of 13 in 5 bodies (`GovernanceService.Structure`): no body/position creation, ordering or labels in
  the database. Real data (read-only scratch copy, aggregates): 36 fiscal years (1991-2026), 124 assignments in
  16 years, 58 holders, no duplicate position per year, no expelled holder, no assignment without a fiscal year;
  the only Owner also holds Admin.
- **Fiscal years:** create only (no edit, no delete), from a list of 1991 up to the current fiscal year minus the
  existing ones; the service refuses duplicates and future years. Default shown: `?fy=`, else the current one,
  else the newest. Empty years were listed (the public selector lists only years with holders, 008).
- **Assignments:** `+` on an empty position → search members (first/last name, nickname or email; Leitões hidden)
  → `IRoleAssignmentValidationService` (no Leitão; no Caloiro as any President; the Conselho de Veteranos
  President must be a Tuno Veterano; one holder per position and year; one position per member and year,
  Ensaiador excepted) → `RoleAssignment` + `IRoleManagementService.PromoteUserForPositionAsync`. Remove (with
  confirmation) → delete + `DemoteUserForPositionAsync`. Both change the member's role and `Positions` only for
  the current fiscal year (Direção: Admin, Mod for a Caloiro; the three Presidents and Ensaiador: Admin; secretaries
  and relators: none; Owner never demoted; demotion forces a sign-out via the security stamp).
- **New mandate (2.1.7):** once the current fiscal year has a holder, `SyncCurrentMandateAsync` makes every member's
  `Positions` (and Admin / Mod, same promote / demote rules) follow that year only: last year's holders lose their
  seats, re-elected members keep the role. Runs after each current-year assignment and once at startup; a new fiscal
  year with no holders yet keeps the previous mandate. Sessions: a dropped Admin is signed out at once (security stamp
  + role probe); a new Admin / Mod gets the role on the next principal refresh (Identity's 30 min
  `ValidationInterval`) or at the next sign-in, no forced logout.
- **RGI:** `docs/rtub_rgi.pdf` in R2, a pre-signed URL in an iframe; "not available" on any storage error.
  Shown to `Member`, `Mod`, `Admin`. No text in the app or database: nothing to rewrite.
- **Access:** page `[Authorize]`; management `AuthorizeView Roles="Mod,Admin"`, UI only (the circuit handlers
  re-checked nothing). `?manage=1` opened the create modal.
- **Tests:** `MemberGovernancePageTests` (bUnit, page markup); `GovernanceTests` pinned the bridge. Public
  `/roles` shares only `GovernanceService.Structure`/`ToMember`; it never used the management services.

## Rules after 016 (`GovernanceAuthorization`, server-side)

| Who | Public mandate | RGI | Fiscal years, assignments |
| --- | --- | --- | --- |
| Visitor | yes | `401` | `401` |
| Member | yes | yes | `403` |
| Mod, Admin | yes | yes | yes (as before) |
| Owner | yes | yes | yes (**was** only with Admin as well) |

A signed-in account with none of these roles gets `403` on the RGI, as before. Every write needs the
antiforgery token in `X-CSRF-TOKEN`.

## API

| Endpoint | Who | Notes |
| --- | --- | --- |
| `GET /api/public/governance?fiscalYear=` | anyone | unchanged (008); public fields only |
| `GET /api/governance/rgi` | Member+ | `{ url }`, `null` when unavailable; `no-store` |
| `GET /api/governance/manage?fiscalYear=` | Mod+ | every fiscal year, the one shown, start years still creatable, bodies → positions (`position` enum name, `title`) → holders (`assignmentId`, display name, full name, avatar) |
| `GET /api/governance/members?q=` | Mod+ | ≤20 `{ id, displayName, fullName, avatarUrl }`; email matched, never returned |
| `POST /api/governance/years` `{ startYear }` | Mod+ | 400 `startYear` on duplicate, < 1991 or future |
| `POST /api/governance/assignments` `{ fiscalYear, position, userId }` | Mod+ | 400 `fiscalYear` / `position` (names only) / `userId` / `assignment` (validation message) |
| `DELETE /api/governance/assignments/{id}` | Mod+ | 404 when gone |

Writes answer with the refreshed management state. No email, phone, birth date, note or audit field leaves the
server; no EF entity is returned.

## Changed on purpose

- Owner manages and reads the RGI without also being Admin (inherits Admin, as in Events, Gallery, Rehearsals).
- Expelled members are no longer offered or accepted for a position (the old search listed them).
- The member search shows nickname, name and photo, not the email (it still matches on it).
- Every rule is enforced by the API, not only hidden in the UI.
- The RGI no longer disappears when no fiscal year exists, and a storage that cannot be built (local run without
  R2 credentials) answers "not available" instead of breaking the management endpoints.
- Copy: "Sem registo" for a vacant position (008 wording), public position titles ("1.º Tesoureiro").

- **#224:** the head is the shared `events-page__head` (intro left, controls at the right edge; stacked on phones), the
  year select and "Adicionar ano letivo" sit in one compact panel, and the button shows only while the server's
  `availableStartYears` (the rule `POST /api/governance/years` enforces) is not empty. The dialog keeps its "all
  registered" message and the server its validation.

## Follow-ups

- The old page's own styles went with its `.razor.css`; the shared `portal-governing-body-card` stays (Hall of Fame).
- The Hierarquia is still not shown (future "Conhece a Tuna").
