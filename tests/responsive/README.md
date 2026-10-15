# Responsive audit (#229)

`responsive_audit.py` opens every portal route family and the main dialogs at **320, 360, 375, 390, 414, 768 and
1280 px** with Playwright (Chromium) and fails when:

- the document or body is wider than the viewport (1 px rounding tolerance) — **global horizontal scrolling**;
- an open dialog leaves the viewport, scrolls sideways, or its close button is off-screen;
- an element crosses the viewport (or its dialog) without being inside a component that scrolls on its own.

Each failure names the responsible element: the outermost one that crosses while its parent does not, with its
rectangle and its width / min-width / overflow / position / white-space / margins / transform. Contained horizontal
scrollers and small touch targets on phones are listed for review, never failed.

It is not part of `dotnet test` (it needs a running app and a browser). Run it before shipping layout work.

## Run

Use a local instance only (the script refuses deployed hosts), on a **copy** of the database with
`DevelopmentDataReset` enabled (`docs/configuration-runbook.md`), so any role signs in with the development password:

```bash
pip install playwright && python -m playwright install chromium
export RTUB_AUDIT_USER=<an Owner of the local test database>
export RTUB_AUDIT_PASSWORD=<its development password>
python tests/responsive/responsive_audit.py --base-url http://localhost:58870
python tests/responsive/responsive_audit.py --only events-add,roles --widths 320,390 --shots /tmp/shots --json /tmp/audit.json
```

Without the two variables only the signed-out scenarios run. On Git Bash for Windows set `MSYS_NO_PATHCONV=1`.

## Adding a screen

Append a `Scenario(name, path, auth=..., action=..., dialog=...)` to `SCENARIOS`. `click("Label")` opens a dialog by
its button's accessible name; `click_css(selector)` by a selector. `optional=True` when the control may rightly be
absent (`roles-add-year`: no academic year left to add); otherwise a missing control fails. Keep scenarios to route
families and shared primitives, not every page variant.

## Intentional local horizontal scroll

Allowed only when reflow is worse, inside the component, with a visible scrollbar:

| Where | Why |
| --- | --- |
| `/profile` → "Percurso na Tuna" (`.tuna-path`, ≥ 700 px) | a horizontal timeline; a long career does not fit a tablet row. Phones get the vertical timeline. |
