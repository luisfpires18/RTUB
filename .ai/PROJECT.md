# Project

Name: RTUB
Repository: https://github.com/luisfpires18/rtub (public)
Stack: ASP.NET Core on .NET 10, EF Core 10, SQLite, ASP.NET Identity, PWA with Web Push, React 19 + Vite portal; Azure hosting. Details: `CLAUDE.md` "Stack".
Integration branch: `dev` (default). Every merge into `dev` deploys DEV. `master` is production; a `dev → master` merge-commit PR is a release (`docs/release-and-rollback.md`).
State: `.ai/STATE.md` (moved from the repository root on 2026-11-11). Where things live: `docs/architecture/system-index.md`.

## How the shared workflow maps here
- Branch names follow `CLAUDE.md` "Git model": `<type>/<NNN>/<slug>`, `NNN` the next unused number in the global sequence.
- Merging a task into `dev` on Codex ACCEPTED is allowed (it deploys DEV). Releases to `master`, hotfixes and `VERSION` bumps after a release need the owner's explicit request.
- Out-of-scope findings go to `.ai/STATE.md` (Deferred), as `CLAUDE.md` "Change rules" says.

## Shared workflow modules
`media` from luisfpires18/ai-workflow, for UI screenshots.

## Skills
Only these:
- Core, always on: `ponytail` (mandatory), `caveman`, `context-mode`, `humanizer`, `code-audit`, `tech-debt-audit`.
- Frontend: `taste-skill` and `impeccable` (mandatory for UI work), `frontend-design`, `web-design-guidelines`.
- Project skills in `.claude/skills/`: `graphify`, `rtub-push`, `rtub-sqlite`, `webapp-testing`.
- `security-guidance`: security review. MCP servers in `.mcp.json`, and Azure.
- `.github/agents/` holds GitHub Copilot agent profiles; Claude does not use them.

`.claude/settings.json` (web preset merged in) turns off the game-engine plugins here.

## Tests for a task
As `CLAUDE.md` "Validation — change-aware": docs, tooling or config only → parse/lint the touched files; application code → `dotnet build` and the affected test projects; full `dotnet test` when the change is broad. Always `git diff --check`, a secret scan, and a final review of `git diff` and `git status`. CI (`ci.yml`) runs on PRs.

## Review overrides
Codex effort `medium` for changes to authentication, authorization, Web Push or VAPID handling, the database schema or migrations, and deployment workflows.
