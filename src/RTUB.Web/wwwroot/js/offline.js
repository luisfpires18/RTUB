// Offline fallback page behaviour
//
// Moved verbatim out of the inline <script> block in offline.html, and replaces the
// inline onclick on the "Tentar Novamente" link, so that a strict CSP script-src does
// not need 'unsafe-inline'.
//
// This file is precached by service-worker.js (STATIC_ASSETS) because offline.html is
// served from the cache while the network is unavailable.
(function () {
    'use strict';

    // Check if back online
    function updateStatus() {
        const statusEl = document.getElementById('status');
        if (navigator.onLine) {
            statusEl.textContent = 'Ligação restaurada! A recarregar...';
            setTimeout(() => window.location.href = '/', 1000);
        } else {
            statusEl.textContent = 'Ainda offline';
        }
    }

    // "Tentar Novamente" - reload the current page rather than following the href.
    const retryEl = document.getElementById('retry');
    if (retryEl) {
        retryEl.addEventListener('click', function (e) {
            e.preventDefault();
            window.location.reload();
        });
    }

    window.addEventListener('online', updateStatus);
    window.addEventListener('offline', updateStatus);

    // Check immediately
    updateStatus();

    // Periodic check
    setInterval(updateStatus, 3000);
})();
