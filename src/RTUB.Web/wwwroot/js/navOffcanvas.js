// App shell behavior: navigation drawer and skip link (MainLayout).
//
// Moved verbatim out of the inline <script> block that used to close MainLayout.razor,
// so that a strict CSP script-src does not need 'unsafe-inline'. The skip-link and
// aria-expanded handlers were added by UI refactor 034. Listeners are delegated to the
// document so they keep working after Blazor enhanced navigation patches the layout.
(function () {
    'use strict';

    // Auto-dismiss Bootstrap offcanvas on mobile when a navigation link is clicked.
    // Without this, the offcanvas backdrop persists after Blazor navigates via SignalR,
    // causing the page to appear dimmed until the user taps the backdrop.
    document.addEventListener('click', function (e) {
        // Ignore dropdown toggle buttons - they manage their own open/close state
        if (e.target.closest('[data-bs-toggle="dropdown"]')) return;

        var link = e.target.closest('#topNav a[href], #topNav button[type="submit"]');
        if (!link) return;

        var offcanvasEl = document.getElementById('topNav');
        if (offcanvasEl && offcanvasEl.classList.contains('show')) {
            var instance = bootstrap.Offcanvas.getInstance(offcanvasEl);
            if (instance) instance.hide();
        }
    });

    // Skip link: move focus into <main>. A plain "#content" resolves against <base href="/">,
    // so following it would navigate to the home page instead of the current page's content.
    // Capture phase, so the default is prevented before Blazor's link interception (a
    // bubbling document listener that ignores defaultPrevented clicks) sees the click.
    document.addEventListener('click', function (e) {
        var skip = e.target.closest('.skip-link');
        if (!skip) return;

        var main = document.getElementById('content');
        if (!main) return;

        e.preventDefault();
        main.focus();
        main.scrollIntoView();
    }, true);

    // Bootstrap's offcanvas does not maintain aria-expanded on its toggle; keep the menu
    // button's state in step with the drawer for assistive technology.
    function setExpanded(e, expanded) {
        if (!e.target || e.target.id !== 'topNav') return;
        document.querySelectorAll('[data-bs-target="#topNav"]').forEach(function (toggle) {
            toggle.setAttribute('aria-expanded', expanded ? 'true' : 'false');
        });
    }

    document.addEventListener('show.bs.offcanvas', function (e) { setExpanded(e, true); });
    document.addEventListener('hidden.bs.offcanvas', function (e) { setExpanded(e, false); });
})();
