# Architecture Decision Records

One file per accepted architectural decision. Records are append-only: supersede, never rewrite.

Naming: `NNNN-short-slug.md` (e.g. `0001-sqlite-as-primary-store.md`).

Template:

```markdown
# NNNN — <Title>

- **Status:** Proposed | Accepted | Superseded by NNNN
- **Date:** YYYY-MM-DD

## Context
What forced the decision.

## Decision
What was decided.

## Consequences
What this costs and enables.
```

Write an ADR only for decisions that are expensive to reverse. Routine choices belong in the practice docs under `docs/`.
