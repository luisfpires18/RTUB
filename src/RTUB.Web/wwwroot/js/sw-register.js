// Service Worker Registration
// Registers the service worker for PWA functionality and offline support
// This script ensures the service worker is registered universally, not just for push notifications
// Updates are silent: update checks let the browser download a new worker in the background; it
// waits and takes over once every RTUB window has closed. No prompt and no forced reload: pages are
// network-only and their assets are hashed (React build) or ?v= versioned (VersionedAsset), so a new
// release always loads new asset URLs, whichever worker is in control.

(function() {
    'use strict';

    // registerServiceWorker() used to run twice (immediately + on window load). register()
    // is idempotent, but each call attached another 'updatefound' listener, another
    // 'visibilitychange' listener and another update-check timer chain. One owner, one set
    // of listeners.
    var registrationStarted = false;

    // Minimum interval between SW update checks (5 minutes) to avoid hammering the server
    var UPDATE_CHECK_DEBOUNCE_MS = 5 * 60 * 1000;
    var lastUpdateCheck = 0;

    // Check if service workers are supported
    if (!('serviceWorker' in navigator)) {
        console.log('Service Workers are not supported in this browser');
        return;
    }

    // --- Debounced SW update check ---
    function checkForUpdate(registration) {
        var now = Date.now();
        if (now - lastUpdateCheck < UPDATE_CHECK_DEBOUNCE_MS) return;
        lastUpdateCheck = now;
        registration.update().catch(function(err) {
            console.warn('[SW Register] Update check failed:', err);
        });
    }

    // A tapped push notification, when the open window cannot be navigated by the worker itself (some installed
    // apps): go to the notification's page. Same-origin only; the worker already checked, this is the second lock.
    navigator.serviceWorker.addEventListener('message', function(event) {
        if (event.data && event.data.type === 'rtub:navigate' && typeof event.data.url === 'string') {
            var target = new URL(event.data.url, window.location.origin);
            if (target.origin === window.location.origin) {
                window.location.href = target.href;
            }
        }
    });

    // A tapped push whose page this window is not on (task 058): the worker stored it for a minute before opening
    // the app (service-worker.js rememberPushTarget). An installed app may have started on its start_url instead, or
    // been resumed without the worker's navigation reaching it. Read once, deleted, same-origin only.
    var PUSH_NAV_CACHE = 'rtub-notification-target';
    var PUSH_NAV_KEY = '/__push-target';
    var PUSH_NAV_MAX_AGE_MS = 60 * 1000;

    function followPushTarget() {
        if (!('caches' in window)) return;
        caches.match(PUSH_NAV_KEY, { cacheName: PUSH_NAV_CACHE })
            .then(function(response) {
                if (!response) return;
                return response.json().then(function(pending) {
                    return caches.open(PUSH_NAV_CACHE)
                        .then(function(cache) { return cache.delete(PUSH_NAV_KEY); })
                        .then(function() {
                            if (!pending || typeof pending.url !== 'string' || Date.now() - pending.at > PUSH_NAV_MAX_AGE_MS) return;
                            var target = new URL(pending.url, window.location.origin);
                            if (target.origin === window.location.origin && target.href !== window.location.href) {
                                window.location.replace(target.href);
                            }
                        });
                });
            })
            .catch(function() { /* no pending push target */ });
    }

    followPushTarget();
    document.addEventListener('visibilitychange', function() {
        if (!document.hidden) {
            followPushTarget();
        }
    });

    // Registers the worker, once; see the call at the bottom for when.
    function registerServiceWorker() {
        if (registrationStarted) return;
        registrationStarted = true;

        if ('serviceWorker' in navigator) {
            navigator.serviceWorker.register('/service-worker.js', {
                scope: '/',
                updateViaCache: 'none'
            })
                .then(function(registration) {
                    console.log('Service Worker registered successfully:', registration.scope);

                    // --- Update check schedule ---
                    // 1) Quick initial check 30s after load to catch updates on app open
                    setTimeout(function() {
                        checkForUpdate(registration);
                    }, 30 * 1000);

                    // 2) Periodic check every hour (only when page is visible)
                    function scheduleUpdate() {
                        setTimeout(function() {
                            if (!document.hidden) {
                                checkForUpdate(registration);
                            }
                            scheduleUpdate();
                        }, 60 * 60 * 1000);
                    }
                    scheduleUpdate();

                    // 3) Check on visibility change — critical for iOS PWAs that suspend on minimize
                    //    When user returns to the app, we check for updates immediately (debounced)
                    document.addEventListener('visibilitychange', function() {
                        if (!document.hidden) {
                            checkForUpdate(registration);
                        }
                    });
                })
                .catch(function(error) {
                    console.error('Service Worker registration failed:', error);
                });
        }
    }

    // Registered once the page has loaded (task 054): installing the worker precaches ~150 KB and claims the page,
    // and doing that while the bundle and fonts were still arriving took bandwidth off the first paint. A page that
    // is already complete (the script ran late) registers at once. registerServiceWorker() is single-shot.
    if (document.readyState === 'complete') {
        registerServiceWorker();
    } else {
        window.addEventListener('load', registerServiceWorker);
    }
})();
