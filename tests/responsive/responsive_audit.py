"""RTUB responsive / horizontal-overflow audit (issue #229).

Drives a running RTUB instance with Playwright (Chromium) at phone, tablet and desktop widths and fails
when a page scrolls sideways. It names the elements that cross the viewport instead of only saying
"page overflowed", and checks that open dialogs fit inside the viewport.

Run against a local instance (never production) - see tests/responsive/README.md:

    python tests/responsive/responsive_audit.py --base-url http://localhost:58870
    python tests/responsive/responsive_audit.py --only events-add,roles --widths 320,390 --shots out/

Signed-in scenarios need RTUB_AUDIT_USER / RTUB_AUDIT_PASSWORD (an Owner of the local test database, so
every admin dialog opens). Without them only the public scenarios run. Exit code 1 when any check fails.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable, Optional

from playwright.sync_api import Page, sync_playwright

PHONES = [320, 360, 375, 390, 414]
TABLET = 768
DESKTOP = 1280
TOLERANCE = 1  # px: sub-pixel rounding only

# ---------------------------------------------------------------------------------------------
# In-page diagnostics. Returns the document / body widths and the elements responsible for any
# overflow: an element is reported when it crosses the viewport (or its open dialog) and its parent
# does not, so the root cause is listed rather than every descendant. Elements inside a component
# that scrolls on its own (overflow-x auto / scroll / hidden / clip) are contained and not reported;
# such regions are listed separately as local scrollers.
# ---------------------------------------------------------------------------------------------
PROBE = r"""
() => {
  const vw = document.documentElement.clientWidth;
  const tol = %TOL%;
  const describe = (el) => {
    const cs = getComputedStyle(el);
    const r = el.getBoundingClientRect();
    let sel = el.tagName.toLowerCase();
    if (el.id) sel += '#' + el.id;
    const cls = typeof el.className === 'string' ? el.className.trim().split(/\s+/).filter(Boolean).slice(0, 3) : [];
    if (cls.length) sel += '.' + cls.join('.');
    const path = [];
    for (let p = el.parentElement, i = 0; p && i < 3; p = p.parentElement, i++) {
      const pc = typeof p.className === 'string' ? p.className.trim().split(/\s+/)[0] : '';
      path.push(p.tagName.toLowerCase() + (pc ? '.' + pc : ''));
    }
    return {
      el: sel, path: path.join(' < '),
      rect: { left: Math.round(r.left), right: Math.round(r.right), width: Math.round(r.width) },
      style: { width: cs.width, minWidth: cs.minWidth, maxWidth: cs.maxWidth, display: cs.display,
               position: cs.position, overflowX: cs.overflowX, whiteSpace: cs.whiteSpace,
               marginLeft: cs.marginLeft, marginRight: cs.marginRight, transform: cs.transform },
      text: (el.innerText || el.value || '').trim().slice(0, 60),
    };
  };
  const clips = (el) => /(auto|scroll|hidden|clip)/.test(getComputedStyle(el).overflowX);
  const visible = (el) => {
    const cs = getComputedStyle(el);
    if (cs.display === 'none' || cs.visibility === 'hidden') return false;
    const r = el.getBoundingClientRect();
    return r.width > 0 && r.height > 0;
  };
  const dialog = [...document.querySelectorAll('dialog[open]')].pop() || null;
  const bounds = (el) => {
    if (dialog && dialog.contains(el) && el !== dialog) {
      const d = dialog.getBoundingClientRect();
      return { left: d.left, right: d.right };
    }
    return { left: 0, right: vw };
  };
  const crosses = (el) => {
    const r = el.getBoundingClientRect();
    const b = bounds(el);
    // Content wholly left of the edge (an off-screen honeypot) never scrolls; partly cut off on the left is a defect.
    return r.right > b.right + tol || (r.left < b.left - tol && r.right > b.left);
  };
  const contained = (el) => {
    for (let p = el.parentElement; p && p !== document.body && p !== document.documentElement; p = p.parentElement) {
      if (p === dialog) return false;
      if (clips(p)) return true;
    }
    return false;
  };
  const offenders = [];
  const scrollers = [];
  for (const el of document.body.querySelectorAll('*')) {
    if (!visible(el)) continue;
    const cs = getComputedStyle(el);
    if (/(auto|scroll)/.test(cs.overflowX) && el.scrollWidth > el.clientWidth + tol && el !== document.documentElement && el !== document.body)
      scrollers.push(describe(el));
    if (!crosses(el) || contained(el)) continue;
    const parent = el.parentElement;
    if (parent && parent !== document.body && visible(parent) && crosses(parent) && !(dialog && parent === dialog)) continue;
    offenders.push(describe(el));
  }
  let dialogInfo = null;
  if (dialog) {
    const r = dialog.getBoundingClientRect();
    const vh = window.innerHeight;
    const body = dialog.querySelector('.modal__body');
    dialogInfo = {
      left: Math.round(r.left), right: Math.round(r.right), top: Math.round(r.top), bottom: Math.round(r.bottom),
      vw, vh, fits: r.left >= -tol && r.right <= vw + tol && r.top >= -tol && r.bottom <= vh + tol,
      bodyScrollsX: body ? body.scrollWidth > body.clientWidth + tol : false,
      dialogScrollsX: dialog.scrollWidth > dialog.clientWidth + tol,
      closeVisible: (() => { const c = dialog.querySelector('.modal__head .icon-btn'); if (!c) return null;
        const cr = c.getBoundingClientRect(); return cr.right <= vw + tol && cr.left >= -tol && cr.top >= -tol; })(),
    };
  }
  return {
    vw, docScroll: document.documentElement.scrollWidth, docClient: document.documentElement.clientWidth,
    bodyScroll: document.body.scrollWidth, bodyClient: document.body.clientWidth,
    offenders: offenders.slice(0, 12), scrollers: scrollers.slice(0, 12), dialog: dialogInfo,
  };
}
""".replace("%TOL%", str(TOLERANCE))

# Small touch targets (buttons / links / inputs under 32px tall) inside the open dialog or page: reported, never failing.
TOUCH = r"""
() => {
  const root = [...document.querySelectorAll('dialog[open]')].pop() || document.querySelector('main') || document.body;
  const small = [];
  for (const el of root.querySelectorAll('button, a[href], input:not([type=hidden]):not(.sr-only), select, textarea, [role=button]')) {
    const r = el.getBoundingClientRect();
    if (!r.width || !r.height || getComputedStyle(el).visibility === 'hidden') continue;
    if (el.matches('.sr-only') || (el.closest('label') && el.matches('input[type=checkbox], input[type=radio]'))) continue;
    if (r.height < 32 || r.width < 32) small.push({ el: el.tagName.toLowerCase() + (el.className ? '.' + String(el.className).split(' ')[0] : ''), w: Math.round(r.width), h: Math.round(r.height), text: (el.innerText || el.getAttribute('aria-label') || '').trim().slice(0, 30) });
  }
  return small.slice(0, 8);
}
"""


@dataclass
class Scenario:
    name: str
    path: str
    auth: bool = False
    action: Optional[Callable[[Page], None]] = None
    wait_for: Optional[str] = None  # selector that proves the page rendered
    dialog: bool = False  # the action opens a <dialog> that must fit
    optional: bool = False  # the control may legitimately be absent (e.g. no academic year left to add)


def click(name: str, exact: bool = False) -> Callable[[Page], None]:
    def run(page: Page) -> None:
        page.get_by_role("button", name=re.compile(name) if not exact else name).first.click(timeout=8000)
        page.wait_for_selector("dialog[open]", timeout=8000)
        page.wait_for_timeout(400)

    return run


def click_css(selector: str) -> Callable[[Page], None]:
    def run(page: Page) -> None:
        page.locator(selector).first.click()
        page.wait_for_selector("dialog[open]", timeout=8000)
        page.wait_for_timeout(400)

    return run


def first_link(selector: str) -> Callable[[Page], None]:
    def run(page: Page) -> None:
        page.locator(selector).first.click()
        page.wait_for_load_state("networkidle")
        page.wait_for_timeout(300)

    return run


def press(name: str) -> Callable[[Page], None]:
    """Clicks a button that changes the page in place (no dialog)."""

    def run(page: Page) -> None:
        page.get_by_role("button", name=name, exact=True).first.click(timeout=8000)
        page.wait_for_timeout(300)

    return run


def _selected_in_view(page: Page) -> None:
    """The selected member is a focused treeitem (or loose-member link) inside the viewport, and the path bar shows."""
    ok = page.evaluate(
        """() => {
          const el = document.activeElement;
          const card = el?.querySelector?.(':scope > .hnode .hnode__card') ?? el;
          if (!el || !(el.getAttribute('role') === 'treeitem' || el.classList.contains('hloose__member'))) return 'focus is not on a member';
          const r = card.getBoundingClientRect();
          if (r.bottom < 0 || r.top > innerHeight || r.right > document.documentElement.clientWidth + 1 || r.left < -1) return 'selected member off-screen';
          const bar = document.querySelector('.htree-selected');
          if (!bar) return 'no selected-path bar';
          const b = bar.getBoundingClientRect();
          if (r.bottom > b.top + 1 && r.top < b.bottom - 1) return 'selected member hidden under the path bar';
          return '';
        }"""
    )
    if ok:
        raise AssertionError(ok)


def hierarchy_search(page: Page) -> None:
    """#223: search a deep member, choose it: ancestors open, it is focused, in view, with its path."""
    deepest = page.evaluate(
        """() => [...document.querySelectorAll('[role=treeitem]')]
             .sort((a, b) => +b.getAttribute('aria-level') - +a.getAttribute('aria-level'))[0]
             ?.querySelector(':scope > .hnode strong')?.textContent ?? ''"""
    )
    page.get_by_role("button", name="Recolher tudo", exact=True).click()
    page.get_by_placeholder("Procurar membro").fill(deepest or "a")
    page.get_by_placeholder("Procurar membro").press("Enter")
    page.wait_for_timeout(700)
    _selected_in_view(page)


def hierarchy_keyboard(page: Page) -> None:
    """#223: one tab stop; arrows walk the tree; Right opens / enters, Left closes / goes up; Home, End, type-ahead."""
    page.locator("[role=tree] [role=treeitem][tabindex='0']").first.focus()
    stops = page.locator("[role=tree] [tabindex='0']").count()
    if stops != 1:
        raise AssertionError(f"{stops} tab stops in the tree")
    level = lambda: page.evaluate("() => document.activeElement.getAttribute('aria-level')")
    page.keyboard.press("ArrowRight")  # first root is open: enters its first afilhado
    if level() != "2":
        raise AssertionError("ArrowRight did not enter the first afilhado")
    page.keyboard.press("ArrowLeft")  # an open afilhado closes first; a closed one goes up to the padrinho
    if level() == "2":
        if page.evaluate("() => document.activeElement.getAttribute('aria-expanded')") != "false":
            raise AssertionError("ArrowLeft neither closed the afilhado nor went up")
        page.keyboard.press("ArrowLeft")
    if level() != "1":
        raise AssertionError("ArrowLeft did not go back to the padrinho")
    page.keyboard.press("End")
    page.wait_for_timeout(900)  # long smooth scrolls: let each finish, as a person would
    page.keyboard.press("Home")
    page.wait_for_timeout(900)
    page.keyboard.press("ArrowDown")
    page.wait_for_timeout(900)  # smooth scrolling (no reduced motion here)
    _selected_in_view(page)


def hierarchy_focus_branch(page: Page) -> None:
    """#223: choose a padrinho, focus their branch, then return to the whole hierarchy."""
    page.locator("[role=treeitem][aria-level='2'][aria-expanded]").first.focus()
    page.keyboard.press("ArrowDown")
    page.keyboard.press("ArrowUp")
    page.get_by_role("button", name="Focar ramo", exact=True).click(timeout=8000)
    page.wait_for_timeout(400)
    if page.locator("[role=treeitem][aria-level='1']").count() != 1:
        raise AssertionError("the focused branch is not shown alone")
    _selected_in_view(page)


def tracing_targets(page: Page) -> None:
    """#220: on phones the card's Detalhes and delete each answer a tap anywhere in a 44 x 44 square around them
    (their look stays compact; an invisible ring gives the area). Taps between the two never hit both."""
    if page.viewport_size["width"] >= TABLET:
        return
    problem = page.evaluate(
        """() => {
          const card = document.querySelector('.owner-log');
          if (!card) return 'no tracing card';
          for (const sel of ['.owner-log__buttons .btn', '.owner-log__buttons .icon-btn']) {
            const el = card.querySelector(sel);
            el.scrollIntoView({ block: 'center', behavior: 'instant' });
            const r = el.getBoundingClientRect();
            const cx = r.left + r.width / 2, cy = r.top + r.height / 2;
            const halfW = Math.max(r.width, 44) / 2 - 0.5, halfH = Math.max(r.height, 44) / 2 - 0.5;
            for (const [x, y] of [[cx, cy - halfH], [cx, cy + halfH], [cx - halfW, cy], [cx + halfW, cy]]) {
              const hit = document.elementFromPoint(x, y);
              if (!hit || !(hit === el || el.contains(hit))) return `${sel} misses a tap at ${Math.round(x)},${Math.round(y)} (${Math.round(r.width)}x${Math.round(r.height)})`;
            }
          }
          return '';
        }"""
    )
    if problem:
        raise AssertionError(problem)


SCENARIOS: list[Scenario] = [
    # Public (signed out)
    Scenario("home", "/"),
    Scenario("login", "/login"),
    Scenario("news-public", "/news"),
    Scenario("music-public", "/music"),
    Scenario("gallery-public", "/gallery"),
    Scenario("events-public", "/events"),
    Scenario("roles-public", "/roles"),
    Scenario("request", "/request"),
    Scenario("privacy", "/privacy"),
    Scenario("forgot-password", "/forgot-password"),
    # Signed in
    Scenario("home-member", "/", auth=True),
    Scenario("events", "/events", auth=True),
    Scenario("events-add", "/events", auth=True, action=click("Adicionar atuação"), dialog=True),
    Scenario("event-detail", "/events", auth=True, action=first_link("a[href^='/events/']:not([href*='my-'])")),
    Scenario("rehearsals", "/rehearsals", auth=True),
    Scenario("rehearsal-add", "/rehearsals", auth=True, action=click("^Adicionar ensaio$"), dialog=True),
    Scenario("members", "/members", auth=True),
    Scenario("members-add", "/members", auth=True, action=click("Adicionar membro"), dialog=True),
    Scenario("members-hierarchy", "/members/hierarchy", auth=True),
    Scenario("hierarchy-expanded", "/members/hierarchy", auth=True, action=press("Expandir tudo")),
    Scenario("hierarchy-search", "/members/hierarchy", auth=True, action=hierarchy_search),
    Scenario("hierarchy-keyboard", "/members/hierarchy", auth=True, action=hierarchy_keyboard),
    Scenario("hierarchy-focus-branch", "/members/hierarchy", auth=True, action=hierarchy_focus_branch),
    Scenario("members-map", "/members/map", auth=True),
    Scenario("hall-of-fame", "/hall-of-fame", auth=True),
    Scenario("leaderboard", "/leaderboard", auth=True),
    Scenario("leaderboard-detail", "/leaderboard", auth=True, action=click_css(".leaderboard-row"), dialog=True),
    Scenario("profile", "/profile", auth=True),
    Scenario("messages", "/messages", auth=True),
    Scenario("shop-public", "/shop"),
    Scenario("shop", "/shop", auth=True),
    Scenario("shop-add", "/shop", auth=True, action=click("Adicionar produto"), dialog=True),
    Scenario("inventory", "/inventory", auth=True),
    Scenario("documentation", "/documentation", auth=True),
    Scenario("logistics", "/logistics", auth=True),
    Scenario("logistics-add", "/logistics", auth=True, action=click("Criar quadro"), dialog=True),
    Scenario("treasury", "/treasury", auth=True),
    Scenario("treasury-calotes", "/treasury/calotes", auth=True),
    Scenario("treasury-calotes-add", "/treasury/calotes", auth=True, action=click("Adicionar calote"), dialog=True),
    Scenario("treasury-mbway", "/treasury/mbway", auth=True),
    Scenario("treasury-nerba", "/treasury/nerba", auth=True),
    Scenario("roles", "/roles", auth=True),
    Scenario("roles-add-year", "/roles", auth=True, action=click("Adicionar ano letivo"), dialog=True, optional=True),
    Scenario("news", "/news", auth=True),
    Scenario("news-add", "/news", auth=True, action=click("Nova publicação"), dialog=True),
    Scenario("music", "/music", auth=True),
    Scenario("gallery", "/gallery", auth=True),
    Scenario("gallery-upload", "/gallery", auth=True, action=click("Carregar foto ou vídeo"), dialog=True),
    Scenario("naipes", "/naipes", auth=True),
    Scenario("naipes-config", "/naipes/config", auth=True),
    Scenario("meetings", "/meetings", auth=True),
    Scenario("meetings-add", "/meetings", auth=True, action=click("Criar Reunião"), dialog=True),
    Scenario("requests", "/requests", auth=True),
    Scenario("questions", "/questions", auth=True),
    Scenario("owner-users", "/users", auth=True),
    Scenario("owner-tracing", "/owner/tracing", auth=True),
    Scenario("owner-tracing-targets", "/owner/tracing", auth=True, action=tracing_targets),
    Scenario("owner-db", "/owner/db", auth=True),
    Scenario("owner-emails", "/emails", auth=True),
    Scenario("owner-notifications", "/notifications", auth=True),
]


@dataclass
class Result:
    scenario: str
    width: int
    ok: bool
    problems: list[str] = field(default_factory=list)
    data: dict = field(default_factory=dict)


def sign_in(page: Page, base: str, user: str, password: str) -> None:
    page.goto(f"{base}/login", wait_until="networkidle")
    page.fill("#login-username", user)
    page.fill("#login-password", password)
    page.locator("form button[type=submit]").first.click()
    page.wait_for_url(lambda u: "/login" not in u, timeout=15000)
    page.wait_for_load_state("networkidle")


def audit(page: Page, base: str, sc: Scenario, width: int, shots: Optional[Path]) -> Result:
    page.goto(base + sc.path, wait_until="networkidle")
    page.wait_for_timeout(350)  # splash lift / lazy chunk paint
    if sc.action:
        try:
            sc.action(page)
        except Exception as exc:  # the control is missing at this width / role: a finding in itself
            if sc.optional and page.locator("dialog[open]").count() == 0:
                return Result(sc.name, width, True, [], {"skipped": "control absent (optional scenario)"})
            return Result(sc.name, width, False, [f"action failed: {str(exc).splitlines()[0][:160]}"])
    data = page.evaluate(PROBE)
    data["smallTargets"] = page.evaluate(TOUCH) if width < TABLET else []
    problems: list[str] = []
    overflow = max(data["docScroll"] - data["docClient"], data["bodyScroll"] - data["bodyClient"])
    if overflow > TOLERANCE:
        problems.append(f"global horizontal overflow {overflow}px")
    if sc.dialog:
        d = data["dialog"]
        if not d:
            problems.append("dialog did not open")
        else:
            if not d["fits"]:
                problems.append(f"dialog outside viewport {d}")
            if d["bodyScrollsX"] or d["dialogScrollsX"]:
                problems.append("dialog scrolls sideways")
            if d["closeVisible"] is False:
                problems.append("dialog close button off-screen")
    if data["offenders"] and not problems:
        problems.append(f"{len(data['offenders'])} element(s) cross the viewport")
    if shots:
        shots.mkdir(parents=True, exist_ok=True)
        page.screenshot(path=str(shots / f"{sc.name}-{width}.png"), full_page=not sc.dialog)
    return Result(sc.name, width, not problems, problems, data)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--base-url", default=os.environ.get("RTUB_AUDIT_URL", "http://localhost:58870"))
    ap.add_argument("--widths", default=",".join(map(str, PHONES + [TABLET, DESKTOP])))
    ap.add_argument("--only", help="comma-separated scenario names")
    ap.add_argument("--shots", type=Path, help="write a screenshot per scenario and width here")
    ap.add_argument("--json", type=Path, help="write the full results as JSON")
    args = ap.parse_args()

    base = args.base_url.rstrip("/")
    if re.search(r"rtub\.(pt|azurewebsites)|realtuna", base, re.I):
        print("Refusing to audit a deployed site: run it against a local instance.", file=sys.stderr)
        return 2
    widths = [int(w) for w in args.widths.split(",")]
    only = set(args.only.split(",")) if args.only else None
    user, password = os.environ.get("RTUB_AUDIT_USER"), os.environ.get("RTUB_AUDIT_PASSWORD")
    scenarios = [s for s in SCENARIOS if (not only or s.name in only) and (not s.auth or (user and password))]
    if not user or not password:
        print("RTUB_AUDIT_USER / RTUB_AUDIT_PASSWORD not set: signed-in scenarios skipped.")

    results: list[Result] = []
    with sync_playwright() as pw:
        browser = pw.chromium.launch()
        for signed_in in (False, True):
            group = [s for s in scenarios if s.auth == signed_in]
            if not group:
                continue
            ctx = browser.new_context(service_workers="block", viewport={"width": 390, "height": 844})
            page = ctx.new_page()
            if signed_in:
                sign_in(page, base, user, password)
            for sc in group:
                for w in widths:
                    page.set_viewport_size({"width": w, "height": 844 if w < TABLET else 1024 if w == TABLET else 900})
                    r = audit(page, base, sc, w, args.shots)
                    results.append(r)
                    mark = "ok  " if r.ok else "FAIL"
                    print(f"{mark} {sc.name:<22} {w:>5}px  {'; '.join(r.problems)}")
                    if not r.ok:
                        for o in r.data.get("offenders", [])[:5]:
                            print(f"       -> {o['el']}  [{o['path']}]  rect={o['rect']}  {json.dumps(o['style'])}")
            ctx.close()
        browser.close()

    failed = [r for r in results if not r.ok]
    print(f"\n{len(results) - len(failed)}/{len(results)} checks passed")
    scrollers = {(r.scenario, s["el"]) for r in results for s in r.data.get("scrollers", [])}
    if scrollers:
        print("Local horizontal scrollers (contained, not failures):")
        for name, el in sorted(scrollers):
            print(f"  {name}: {el}")
    if args.json:
        args.json.write_text(json.dumps([r.__dict__ for r in results], indent=1, ensure_ascii=False), encoding="utf-8")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
