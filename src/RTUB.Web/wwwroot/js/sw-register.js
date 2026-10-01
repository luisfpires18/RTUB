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

    // Handle messages from the service worker (e.g., navigation from push notifications)
    navigator.serviceWorker.addEventListener('message', function(event) {
        if (event.data && event.data.type === 'rtub:navigate') {
            console.log('[SW Register] Received navigate message:', event.data.url);
            // Navigate to the URL using window.location
            // This works for both regular and PWA mode
            window.location.href = event.data.url;
        }

        // Handle subscription-lost events from service worker
        // This fires when Chrome rotates the push endpoint and re-subscription fails
        if (event.data && event.data.type === 'rtub:subscription-lost') {
            console.warn('[SW Register] Push subscription was lost');
            // Mark subscription as lost so the UI can prompt re-subscription
            if (window.pwaHelper && typeof window.pwaHelper.markSubscriptionLost === 'function') {
                window.pwaHelper.markSubscriptionLost();
            }
        }
    });

    // Register service worker immediately (not waiting for load event)
    // This ensures PWABuilder and other tools can detect it
    // Also register on load as fallback for older browsers
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

    // Register immediately for PWABuilder detection. registerServiceWorker() is
    // single-shot, so the load-event fallback below is a no-op once this has run.
    registerServiceWorker();
    window.addEventListener('load', registerServiceWorker);
})();
