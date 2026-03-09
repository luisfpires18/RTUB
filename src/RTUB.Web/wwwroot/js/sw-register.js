// Service Worker Registration
// Registers the service worker for PWA functionality and offline support
// This script ensures the service worker is registered universally, not just for push notifications
// Includes automatic update detection, toast notification, and forced cache refresh

(function() {
    'use strict';

    // Guard against infinite reload loops when a new SW takes control
    var refreshing = false;

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

        var toast = document.createElement('div');
        toast.id = 'rtub-sw-update-toast';
        toast.setAttribute('role', 'alert');
        toast.setAttribute('aria-live', 'assertive');
        toast.innerHTML =
            '<div style="display:flex;align-items:center;gap:10px;flex-wrap:wrap;">' +
                '<span style="flex:1;min-width:0;">Nova versão disponível!</span>' +
                '<button id="rtub-sw-update-btn" style="' +
                    'background:#fff;color:#1a1a2e;border:none;border-radius:8px;' +
                    'padding:8px 18px;font-weight:600;font-size:14px;cursor:pointer;' +
                    'white-space:nowrap;' +
                '">Atualizar</button>' +
                '<button id="rtub-sw-dismiss-btn" style="' +
                    'background:transparent;color:#fff;border:1px solid rgba(255,255,255,0.4);' +
                    'border-radius:8px;padding:8px 12px;font-size:13px;cursor:pointer;' +
                    'white-space:nowrap;' +
                '">Depois</button>' +
            '</div>';

        // Toast styling — fixed bottom bar, matches RTUB dark theme
        toast.style.cssText =
            'position:fixed;bottom:0;left:0;right:0;z-index:999999;' +
            'background:linear-gradient(135deg,#1a1a2e,#16213e);color:#fff;' +
            'padding:14px 20px;font-family:inherit;font-size:15px;' +
            'box-shadow:0 -2px 12px rgba(0,0,0,0.3);' +
            'animation:rtub-toast-slide-up 0.3s ease-out;' +
            'border-top:2px solid #e94560;';

        // Inject slide-up animation if not already present
        if (!document.getElementById('rtub-sw-toast-style')) {
            var style = document.createElement('style');
            style.id = 'rtub-sw-toast-style';
            style.textContent =
                '@keyframes rtub-toast-slide-up {' +
                    'from { transform: translateY(100%); opacity: 0; }' +
                    'to { transform: translateY(0); opacity: 1; }' +
                '}';
            document.head.appendChild(style);
        }

        document.body.appendChild(toast);

        // "Atualizar" button — tell the waiting SW to skip waiting and take control
        document.getElementById('rtub-sw-update-btn').addEventListener('click', function() {
            if (waitingSW) {
                waitingSW.postMessage({ type: 'SKIP_WAITING' });
            }
            toast.remove();
        });

        // "Depois" button — dismiss toast, user will get it next time
        document.getElementById('rtub-sw-dismiss-btn').addEventListener('click', function() {
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

    // Register immediately for PWABuilder detection
    registerServiceWorker();
    
    // Also register on load as fallback
    window.addEventListener('load', registerServiceWorker);

    // Listen for service worker controller change (new SW activated)
    // Reload the page once so users get fresh assets from the new cache
    navigator.serviceWorker.addEventListener('controllerchange', function() {
        if (refreshing) return;
        refreshing = true;
        console.log('[SW Register] New service worker activated, reloading for fresh content...');
        window.location.reload();
    });
})();
