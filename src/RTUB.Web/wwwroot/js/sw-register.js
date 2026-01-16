// Service Worker Registration
// Registers the service worker for PWA functionality and offline support
// This script ensures the service worker is registered universally, not just for push notifications

(function() {
    'use strict';

    // Check if service workers are supported
    if (!('serviceWorker' in navigator)) {
        console.log('Service Workers are not supported in this browser');
        return;
    }

    // Handle messages from the service worker (e.g., navigation from push notifications)
    navigator.serviceWorker.addEventListener('message', function(event) {
        if (event.data && event.data.type === 'rtub:navigate') {
            console.log('[SW Register] Received navigate message:', event.data.url);
            // Navigate to the URL using window.location
            // This works for both regular and PWA mode
            window.location.href = event.data.url;
        }
    });

    // Register service worker on page load
    window.addEventListener('load', function() {
        navigator.serviceWorker.register('/service-worker.js')
            .then(function(registration) {
                console.log('Service Worker registered successfully:', registration.scope);
                
                // Check for updates periodically (every hour) only when page is visible
                // This prevents unnecessary resource usage when tab is in background
                function scheduleUpdate() {
                    setTimeout(function() {
                        // Only update if page is visible to avoid unnecessary resource usage
                        if (!document.hidden) {
                            registration.update();
                        }
                        // Schedule next update
                        scheduleUpdate();
                    }, 60 * 60 * 1000);
                }
                scheduleUpdate();
            })
            .catch(function(error) {
                console.error('Service Worker registration failed:', error);
            });
    });

    // Listen for service worker updates
    navigator.serviceWorker.addEventListener('controllerchange', function() {
        console.log('Service Worker updated, reloading page...');
        // Optionally reload the page when a new service worker takes control
        // window.location.reload();
    });
})();
