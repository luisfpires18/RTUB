# Frontend Practices

The frontend is **React 19 + Vite + TypeScript** (`src/RTUB.Web/portal/`). React owns every page; the only other HTML
page is the static server page `/Error` (`wwwroot/error.html`). **There is no Blazor**: the host was retired in task
037 (no `.razor` components, `App.razor`, `MainLayout`, `/_blazor`, `blazor.web.js`, `RTUB.Shared` or bUnit). The
Blazor / Razor guidance this file used to hold is in git history only; do not follow or reintroduce it.

Authoritative rules live elsewhere — this file only routes and lists the cross-cutting basics:

| Topic | Doc |
| --- | --- |
| Route ownership, shell, member menu, build and commit rules, replacement and wording rules | `docs/react-portal-pilot.md` |
| A module's API, permissions and data audit | `docs/react-*.md` (table in `CLAUDE.md`) |
| Service worker, manifest, offline, install | `docs/pwa-practices.md` |
| Push opt-in / opt-out (`/profile`) and the service-worker push handlers | `docs/react-push-v2.md` |

## Basics

- **Server is the authority.** Every permission is checked by the `/api/*` endpoint; the UI only hides links and
  buttons the API already refuses.
- **Reuse before writing.** Look in `portal/src` (`Dialog.tsx`, `icons.tsx`, the `*Ui.tsx` helpers, `styles.css`
  classes and CSS variables) before adding a component or style.
- **Portuguese (pt-PT)** for every user-facing string.
- **Mobile first in practice:** check every change at phone width (≈375 px) and in the installed app; no horizontal
  overflow, tap targets large enough, dialogs scroll inside themselves.
- **No sideways page scroll, never masked (#229).** At 320-414 px the document is never wider than the viewport. Fix the
  element that overflows (`min-width: 0` on flex / grid children, wrapping action rows, `minmax(0, 1fr)` columns);
  `html` / `body` / `main` / `.shell` never get `overflow-x: hidden` or `clip` (pinned by `ResponsiveLayoutSourceTests`).
  A component may scroll sideways on its own only when reflow is worse, with a visible scrollbar, listed in
  `tests/responsive/README.md`. Page heads with actions use the shared `page__head events-page__head`. Check with
  `tests/responsive/responsive_audit.py` (Playwright, names the offending element).
- **Safe area:** `--safe-top` (the iOS status bar of the installed app, 0 elsewhere) pads the sticky header and is part
  of `--header-h`; the drawer, footer and player pad by the other insets. Add insets only where an edge is reached.
- **Accessibility:** labelled controls (`aria-label` when there is no visible text), `alt` on images, keyboard
  reachable dialogs with a visible focus.
- **No inline styles** where a class will do; one stylesheet (`portal/src/styles.css`).
- **Writes** go through the module's API client, which sends the antiforgery header (`X-CSRF-TOKEN`).
- **Build:** from `src/RTUB.Web`, `npm run check:portal` then `npm run build:portal`; commit the rebuilt
  `wwwroot/portal` with the source change (file names are content-hashed; publish does not rebuild it).
- **Tests:** there is no JS test runner; React behaviour is pinned by source-contract tests in
  `tests/RTUB.Web.Tests/ReactPortal/` and by the API tests in `tests/RTUB.Integration.Tests/Api/`.
