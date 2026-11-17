# Frontend Practices

The frontend is React 19 + Vite + TypeScript (`src/RTUB.Web/portal/`). React owns every page; the only other page is
the static server page `/Error` (`wwwroot/error.html`).

| Topic | Doc |
| --- | --- |
| Route ownership, shell, member menu, build and commit rules | `docs/react-portal-pilot.md` |
| A module's API and permissions | `docs/react-*.md` (table in `CLAUDE.md`) |
| Service worker, manifest, offline, install | `docs/pwa-practices.md` |
| Push opt-in / opt-out and service-worker push handlers | `docs/react-push-v2.md` |

## Basics

- The server is the authority. Every permission is checked by the `/api/*` endpoint; the UI only hides what the API refuses.
- Reuse before writing: look in `portal/src` (`Dialog.tsx`, `icons.tsx`, the `*Ui.tsx` helpers, `styles.css` classes and
  variables) before adding a component or style. One stylesheet (`portal/src/styles.css`); no inline styles where a class will do.
- Every user-facing string is Portuguese (pt-PT).
- Check every change at phone width (about 375 px) and in the installed app: tap targets large enough, dialogs scroll inside themselves.
- No sideways page scroll, never masked. At 320-414 px the document is never wider than the viewport. Fix the
  overflowing element (`min-width: 0` on flex / grid children, wrapping action rows, `minmax(0, 1fr)` columns);
  `html` / `body` / `main` / `.shell` never get `overflow-x: hidden` or `clip` (pinned by `ResponsiveLayoutSourceTests`).
  A component may scroll sideways on its own only when reflow is worse, with a visible scrollbar, listed in
  `tests/responsive/README.md`. Page heads with actions use the shared `page__head events-page__head`.
  Check with `tests/responsive/responsive_audit.py` (Playwright; names the offending element).
- Safe area: `--safe-top` (iOS status bar of the installed app, 0 elsewhere) pads the sticky header and is part of
  `--header-h`; the drawer, footer and player pad by the other insets. Add insets only where an edge is reached.
- Accessibility: labelled controls (`aria-label` without visible text), `alt` on images, keyboard-reachable dialogs with visible focus.
- Writes go through the module's API client, which sends the antiforgery header (`X-CSRF-TOKEN`).

## Build and tests

- From `src/RTUB.Web`: `npm run check:portal`, then `npm run build:portal`. Commit the rebuilt `wwwroot/portal` with the
  source change (file names are content-hashed; publish does not rebuild it).
- There is no JS test runner. React behaviour is pinned by source-contract tests in `tests/RTUB.Web.Tests/ReactPortal/`
  and API tests in `tests/RTUB.Integration.Tests/Api/`.
