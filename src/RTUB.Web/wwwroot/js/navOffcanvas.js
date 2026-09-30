// Mobile navigation offcanvas auto-dismiss
//
// Moved verbatim out of the inline <script> block that used to close MainLayout.razor,
// so that a strict CSP script-src does not need 'unsafe-inline'.
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
})();
