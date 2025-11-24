// Service Worker for Web Push Notifications
// Handles push events and notification clicks

// Cache version - increment when updating service worker
const CACHE_VERSION = 'rtub-v1';

// Install event - perform any setup needed
self.addEventListener('install', (event) => {
    console.log('[Service Worker] Installing...');
    // Skip waiting to activate immediately
    self.skipWaiting();
});

// Activate event - clean up old caches
self.addEventListener('activate', (event) => {
    console.log('[Service Worker] Activating...');
    event.waitUntil(
        clients.claim()
    );
});

// Push event - handle incoming push notifications
self.addEventListener('push', (event) => {
    console.log('[Service Worker] Push received');
    
    let notificationData = {
        title: 'RTUB Notification',
        body: 'You have a new notification',
        icon: '/icons/rtub-logo-192.png',
        badge: '/icons/rtub-logo-192.png',
        url: '/',
        tag: 'rtub-notification'
    };

    if (event.data) {
        try {
            const data = event.data.json();
            notificationData = {
                title: data.title || notificationData.title,
                body: data.body || notificationData.body,
                icon: data.icon || notificationData.icon,
                badge: data.icon || notificationData.badge,
                url: data.url || notificationData.url,
                tag: data.tag || notificationData.tag
            };
        } catch (e) {
            console.error('[Service Worker] Error parsing push data:', e);
            // Use default notification data if parsing fails
            notificationData.body = event.data.text();
        }
    }

    const promiseChain = self.registration.showNotification(
        notificationData.title,
        {
            body: notificationData.body,
            icon: notificationData.icon,
            badge: notificationData.badge,
            tag: notificationData.tag,
            data: {
                url: notificationData.url
            },
            requireInteraction: false,
            vibrate: [200, 100, 200]
        }
    );

    event.waitUntil(promiseChain);
});

// Notification click event - handle user clicking on notification
self.addEventListener('notificationclick', (event) => {
    console.log('[Service Worker] Notification clicked');
    
    event.notification.close();

    // Get the URL to open from the notification data
    const urlToOpen = event.notification.data?.url || '/';

    // Focus on existing window or open new one
    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true })
            .then((clientList) => {
                // Check if there's already a window open with this URL
                for (let i = 0; i < clientList.length; i++) {
                    const client = clientList[i];
                    if (client.url === urlToOpen && 'focus' in client) {
                        return client.focus();
                    }
                }
                // If no window is open, open a new one
                if (clients.openWindow) {
                    return clients.openWindow(urlToOpen);
                }
            })
    );
});

// Message event - handle messages from the client
self.addEventListener('message', (event) => {
    if (event.data && event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
});
