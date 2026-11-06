# Permissions: /members, hierarchy

Source: `MembersAuthorization`. Page doc: [react-members](../react-members.md).

| Who | What |
| --- | --- |
| Visitor | `401` |
| Any member (Leitão included) | directory, member details (contacts), active members, birthdays, hierarchy |
| Admin, Owner | create and edit members, instruments, alcunha, expel / reactivate Leitões, "Tornar ativo", push reminder, delete Leitões |
| Owner only | delete any member; change complete Leitão / Caloiro / Tuno date pairs |
| Mod (task 076) | "Completar informação": Cidade, Contacto, instruments, the missing Leitão / Caloiro / Tuno dates, Padrinho — nothing else (see below) |

## Mod: "Completar informação" (task 076, #257)

System role **Mod** (and Admin / Owner) may complete approved members' information through its own limited endpoint;
the full edit stays Admin / Owner.

| Field | Mod may | Applies to (`MemberInfoRules`) |
| --- | --- | --- |
| Cidade, Contacto | edit | every member in scope |
| Instruments (add, remove, primary) | edit (the existing instrument endpoints, now Mod too) | everyone but a Tuno Honorário |
| Entrada na Tuna (Leitão date) | fill when missing; a complete pair stays Owner / Magister | Leitão, Caloiro, Tuno; not Fundador / Honorário |
| Passagem a Caloiro | same | Caloiro, Tuno; not Fundador / Honorário (a Leitão is never asked early) |
| Passagem a Tuno | same | Tuno; not Fundador (always December 1991) |
| Padrinho | edit (Tuno or above; nobody their own) | Leitão, Caloiro, Tuno; not Fundador / Honorário |

- **Allow-list by contract**: `PUT /api/members/{id}/info` (`MemberInfoInput`) has only those fields; anything else in the
  body (name, email, category, role, position, expelled / active status, password) is not bound and changes nothing. A
  field that does not apply must stay empty (`400`); changing a complete date pair without Owner / Magister is `400`
  (refused, not silently ignored as the full form does).
- **Concurrency**: the GET returns the member's `Stamp` (Identity's concurrency stamp); a save with an older one is
  `409` ("Os dados deste membro mudaram entretanto") and nothing is written. Validation errors keep what was typed.
- **Audit**: the change is the same `ApplicationUser` update as the full form, audited with the actor and the fields.
- **Scope**: not expelled, with a Leitão / Caloiro / Tuno category (an honorary-only account is out); otherwise `404`
  for a Mod.
- **Not given to Mod**: create, delete, the full edit (`GET /edit`, `PUT /{id}`), "Definir alcunha", expel / reactivate,
  push reminder, categories, roles, positions, activity status.
- `/members`: a Mod gets "Completar informação" in a member's details (Admin / Owner keep "Editar" and the rest);
  `/members?info={id}` opens it once (from "As minhas ações pendentes" → Membros).
