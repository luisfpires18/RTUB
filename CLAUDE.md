# CLAUDE.md — RTUB

Traffic controller. Routing rules only. Detailed standards live in the docs linked below — read them on demand, do not copy them here.

## Read first
1. `STATE.md` — current phase, branch, task, blockers. **Always read at session start.**
2. `docs/architecture/system-index.md` — where things live.
3. Task-relevant doc from the map below. Nothing else by default.

## Stack
Blazor Interactive Server on .NET 10 · EF Core 10 · SQLite · ASP.NET Identity · SignalR · PWA (service worker + Web Push) · PixiJS/TypeScript mini-game (MyTuno) · Cloudflare R2 object storage · xUnit + Moq + FluentAssertions + bUnit.

Clean Architecture: `RTUB.Core` (entities/enums) → `RTUB.Application` (services, repositories, EF Core) → `RTUB.Shared` (reusable Razor) → `RTUB.Web` (host, pages, hub, controllers).

## Context discipline
- Architecture / cross-layer tracing (callers, blast radius, layering): `graphify explain "<sym>"` and `graphify affected "<sym>"` first, while `graphify-out/` is current. Not `graphify query` — it is lexical and noisy.
- Exact symbols, config, packages, and anything crossing the C#↔JS interop boundary: `grep` / targeted reads first. The graph has no edge across `JSRuntime.Invoke*` string dispatch.
- Verify Graphify findings against source before changing code. Never re-scan the repo to answer a scoped question.
- Load only the skills the current task needs.
- Caveman/concise execution: do the work, skip the narration. Report outcomes, not plans.
- Keep `CLAUDE.md` and `STATE.md` small. New durable detail belongs in a focused doc or ADR, not here.

## Change rules
- **Preserve behavior** unless the task explicitly changes it.
- **No opportunistic refactoring.** Unrelated findings get one line in `STATE.md` (Deferred), not a fix.
- Stay inside the requested scope. Out-of-scope problems get recorded, not solved.
- `InventoryService` and `ApplicationDbContext` are partial classes split across files — edit the right partial.
- EF Core: one context per operation via `IDbContextFactory<ApplicationDbContext>`.

## Git model
- `dev` = GitHub default + integration branch. `feat`/`fix`/`chore` branches merge into `dev` → Deploy • DEV.
- `master` = production. A `dev → master` PR (merged with a **merge commit**) is a release → Deploy • PROD. Root `VERSION` (SemVer) must go up.
- After **every** production release, and after every `hotfix/*` → `master`, merge `master` back into `dev`, then bump `dev`'s `VERSION` to the next unreleased version (2.0.0 released → 2.0.1, or 2.1.0/3.0.0 if planned) — never leave `dev` on a released number.
- Branch name: `<type>/<NNN>/<slug>` — `NNN` is the next unused number in the global sequence.
- Never push, merge, open PRs, touch remotes, delete branches, or force-push without explicit authorization in the current request.
- Never discard or reset pre-existing working-tree changes.

## Security
Repository stays **secret-free**. Never commit passwords, API keys, access tokens, OAuth secrets, private keys, VAPID private keys, production credentials, or secret-bearing connection strings. Synthetic placeholders only. Inspect `git diff` for accidental secrets before finishing.

## Validation — change-aware
Run only what the change can break.
- Docs/tooling/config only → parse/lint the touched files. No test suite.
- Application code → `dotnet build`, then the affected test project(s).
- Full `dotnet test` only when the change is broad or before shipping.
- Always: `git diff --check`, secret scan, review final `git diff` and `git status`.
- Rebuild Graphify (`graphify extract . --code-only`) only when application structure changes — not for docs/tooling edits.

## Authoritative docs
| Topic | Source |
| --- | --- |
| General repo conventions | `.github/copilot-instructions.md` |
| Backend / C# / EF Core standards | `docs/backend-practices.md` |
| Blazor / Razor / CSS standards | `docs/frontend-practices.md` |
| PWA, service worker, push | `docs/pwa-practices.md` |
| MyTuno game domain & balancing | `docs/my_tuno/` |
| R2 storage & database backups | `docs/cloudflare-r2-and-database-backups.md` |
| CI/CD, Azure DEV & production deploy | `docs/ci-cd-and-azure-environments.md` |
| Releases, versions, rollback, DB restore | `docs/release-and-rollback.md` |
| Repository routing map | `docs/architecture/system-index.md` |
| Architecture decisions (ADRs) | `docs/architecture/adr/` |
| Role guidance for AI agents | `.github/agents/` |
