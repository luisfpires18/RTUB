// Service Worker Registration
// Registers the service worker for PWA functionality and offline support
// This script ensures the service worker is registered universally, not just for push notifications
// Includes automatic update detection, toast notification, and forced cache refresh

(function() {
    'use strict';

    // Guard against infinite reload loops when a new SW takes control
    var refreshing = false;

    // Whether this page was already controlled when the script ran. On a first-ever
    // install the worker's clients.claim() fires controllerchange for an uncontrolled
    // page; reloading there is a pointless extra navigation, not an update.
    var hadControllerAtStartup = !!navigator.serviceWorker.controller;

    // registerServiceWorker() used to run twice (immediately + on window load). register()
    // is idempotent, but each call attached another 'updatefound' listener, another
    // 'visibilitychange' listener and another update-check timer chain - which is how a
    // single update could raise two toasts. One owner, one set of listeners.
    var registrationStarted = false;

    // Minimum interval between SW update checks (5 minutes) to avoid hammering the server
    var UPDATE_CHECK_DEBOUNCE_MS = 5 * 60 * 1000;
    var lastUpdateCheck = 0;

    // Check if service workers are supported
    if (!('serviceWorker' in navigator)) {
        console.log('Service Workers are not supported in this browser');
        return;
    }

    // --- Update Toast Banner ---
    // Self-contained toast that appears when a new SW version is ready
    // Portuguese text to match the app language
    function showUpdateToast(waitingSW) {
        // Don't show duplicate toasts
        if (document.getElementById('rtub-sw-update-toast')) return;

        // Built as DOM nodes with classes only. All styling (including the slide-up
        // keyframes) lives in css/3-components/sw-update-toast.css, so a strict
        // style-src needs neither 'unsafe-inline' nor an injected <style> element.
        var toast = document.createElement('div');
        toast.id = 'rtub-sw-update-toast';
        toast.className = 'rtub-sw-toast';
        toast.setAttribute('role', 'alert');
        toast.setAttribute('aria-live', 'assertive');

        var row = document.createElement('div');
        row.className = 'rtub-sw-toast__row';

        var text = document.createElement('span');
        text.className = 'rtub-sw-toast__text';
        text.textContent = 'Nova versão disponível!';

        var updateBtn = document.createElement('button');
        updateBtn.id = 'rtub-sw-update-btn';
        updateBtn.type = 'button';
        updateBtn.className = 'rtub-sw-toast__update';
        updateBtn.textContent = 'Atualizar';

        var dismissBtn = document.createElement('button');
        dismissBtn.id = 'rtub-sw-dismiss-btn';
        dismissBtn.type = 'button';
        dismissBtn.className = 'rtub-sw-toast__dismiss';
        dismissBtn.textContent = 'Depois';

        row.appendChild(text);
        row.appendChild(updateBtn);
        row.appendChild(dismissBtn);
        toast.appendChild(row);

        document.body.appendChild(toast);

        // "Atualizar" button — tell the waiting SW to skip waiting and take control
        updateBtn.addEventListener('click', function() {
            if (waitingSW) {
                waitingSW.postMessage({ type: 'SKIP_WAITING' });
            }
            toast.remove();
        });

        // "Depois" button — dismiss toast, user will get it next time
        dismissBtn.addEventListener('click', function() {
            toast.remove();
        });
    }

    // --- Handle a newly found waiting/installing SW ---
    function trackInstallingWorker(registration) {
        var sw = registration.installing;
        if (!sw) return;

        sw.addEventListener('statechange', function() {
            if (sw.state === 'installed' && navigator.serviceWorker.controller) {
                // New SW is installed but waiting — show update prompt
                console.log('[SW Register] New service worker installed, waiting to activate');
                showUpdateToast(sw);
            }
        });
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

                    // If there's already a waiting SW (e.g., from a previous visit), show toast immediately
                    if (registration.waiting) {
                        showUpdateToast(registration.waiting);
                    }

                    // Watch for new installing workers (triggered by registration.update())
                    registration.addEventListener('updatefound', function() {
                        console.log('[SW Register] Update found, tracking new worker...');
                        trackInstallingWorker(registration);
                    });

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

    // Listen for service worker controller change (new SW activated)
    // Reload the page once so users get fresh assets from the new cache
    navigator.serviceWorker.addEventListener('controllerchange', function() {
        if (refreshing) return;
        // First-ever install: clients.claim() takes control of a page that was never
        // controlled. Nothing changed for the user, so do not reload.
        if (!hadControllerAtStartup) return;
        refreshing = true;
        console.log('[SW Register] New service worker activated, reloading for fresh content...');
        window.location.reload();
    });
})();
