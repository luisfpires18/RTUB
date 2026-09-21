// Offline fallback page behaviour
//
// Moved verbatim out of the inline <script> block in offline.html, and replaces the
// inline onclick on the "Tentar Novamente" link, so that a strict CSP script-src does
// not need 'unsafe-inline'.
//
// This file is precached by service-worker.js (STATIC_ASSETS) because offline.html is
// served from the cache while the network is unavailable.
//
// ORIGIN REACHABILITY, NOT DEVICE CONNECTIVITY
// -------------------------------------------
// This page used to redirect to '/' whenever navigator.onLine was true. That flag only
// reports whether the device has *a* network interface up - it says nothing about whether
// RTUB is reachable. The real failure it caused: the origin is stopped, the service worker
// correctly serves offline.html, navigator.onLine stays true, and the page bounces to '/'
// one second later - which fails, lands back here, and bounces again.
//
// The origin is therefore probed directly, with /health. service-worker.js lists /health in
// NEVER_CACHE_PREFIXES, so the request always goes to the network and a cached 200 can never
// stand in for a live one; `cache: 'no-store'` does the same for the browser's HTTP cache.
// Only a successful response counts as reachable. Anything else - rejection, timeout, 4xx,
// 5xx - leaves the user on this page.
(function () {
    'use strict';

    const STATUS_CHECKING = 'A verificar ligação...';
    const STATUS_OFFLINE = 'Ainda offline';
    const STATUS_RESTORED = 'Ligação restaurada! A recarregar...';

    // Unchanged from the original: the user sees the "restored" message before the page moves.
    const REDIRECT_DELAY_MS = 1000;
    // Unchanged from the original.
    const POLL_INTERVAL_MS = 3000;
    // An unreachable origin often fails fast, but a black-holed connection does not fail at
    // all. Without this the probe could stay pending past the next poll for ever.
    const PROBE_TIMEOUT_MS = 5000;

    const statusEl = document.getElementById('status');

    // Guards against overlapping probes from the three callers below (first check, the
    // 'online' event, the interval). One in-flight probe at a time is enough.
    let probeInFlight = false;
    // Once the redirect is scheduled nothing may schedule a second one, and no later probe
    // result may overwrite the message. This is also what keeps the page from redirecting
    // repeatedly: the only navigation away from here happens once, after a proven-reachable
    // origin, and the interval is stopped at the same moment.
    let redirecting = false;
    // Assigned at the bottom, once the handlers exist. clearInterval(null) is a no-op, so
    // originReachable() is safe even in the impossible case of firing before that.
    let pollTimer = null;

    function setStatus(text) {
        if (statusEl) {
            statusEl.textContent = text;
        }
    }

    function originReachable() {
        if (redirecting) {
            return;
        }
        redirecting = true;
        clearInterval(pollTimer);
        setStatus(STATUS_RESTORED);
        setTimeout(function () {
            window.location.href = '/';
        }, REDIRECT_DELAY_MS);
    }

    function originUnreachable() {
        if (!redirecting) {
            setStatus(STATUS_OFFLINE);
        }
    }

    function checkConnection() {
        if (redirecting || probeInFlight) {
            return;
        }

        // No interface at all: the probe cannot succeed, so do not spend a request on it.
        if (!navigator.onLine) {
            originUnreachable();
            return;
        }

        probeInFlight = true;

        const controller = new AbortController();
        const timeout = setTimeout(function () {
            controller.abort();
        }, PROBE_TIMEOUT_MS);

        fetch('/health', { cache: 'no-store', signal: controller.signal })
            .then(function (response) {
                if (response && response.ok) {
                    originReachable();
                } else {
                    originUnreachable();
                }
            })
            .catch(function () {
                // Rejected, aborted by the timeout, or blocked - all mean "not reachable".
                originUnreachable();
            })
            .finally(function () {
                clearTimeout(timeout);
                probeInFlight = false;
            });
    }

    // "Tentar Novamente" - reload the current page rather than following the href.
    const retryEl = document.getElementById('retry');
    if (retryEl) {
        retryEl.addEventListener('click', function (e) {
            e.preventDefault();
            window.location.reload();
        });
    }

    window.addEventListener('online', checkConnection);
    window.addEventListener('offline', originUnreachable);

    setStatus(STATUS_CHECKING);

    // Check immediately
    checkConnection();

    // Periodic check
    pollTimer = setInterval(checkConnection, POLL_INTERVAL_MS);
})();
