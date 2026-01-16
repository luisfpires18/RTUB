// Service Worker for Web Push Notifications and Asset Caching
// Handles push events, notification clicks, and offline asset caching

// Cache version - increment when updating service worker
const CACHE_VERSION = 'rtub-v7';
const STATIC_CACHE = `rtub-static-${CACHE_VERSION}`;
const DYNAMIC_CACHE = `rtub-dynamic-${CACHE_VERSION}`;
const IMAGE_CACHE = `rtub-images-${CACHE_VERSION}`;

// Assets to cache on install for offline support
const STATIC_ASSETS = [
    '/',
    '/icons/rtub-logo-192.png',
    '/icons/rtub-logo-512.png',
    '/images/default-avatar.webp',
    '/manifest.webmanifest'
];

// Install event - cache critical static assets
self.addEventListener('install', (event) => {
    console.log('[Service Worker] Installing...');
    event.waitUntil(
        caches.open(STATIC_CACHE)
            .then((cache) => {
                console.log('[Service Worker] Caching static assets');
                return cache.addAll(STATIC_ASSETS);
            })
            .then(() => self.skipWaiting())
            .catch((error) => {
                console.error('[Service Worker] Failed to cache static assets:', error);
                // Skip waiting even if caching fails
                return self.skipWaiting();
            })
    );
});

// Activate event - clean up old caches
self.addEventListener('activate', (event) => {
    console.log('[Service Worker] Activating...');
    const currentCaches = [STATIC_CACHE, DYNAMIC_CACHE, IMAGE_CACHE];
    
    event.waitUntil(
        caches.keys()
            .then((cacheNames) => {
                return Promise.all(
                    cacheNames
                        .filter((cacheName) => {
                            // Delete old cache versions - improved maintainability
                            return cacheName.startsWith('rtub-') && !currentCaches.includes(cacheName);
                        })
                        .map((cacheName) => {
                            console.log('[Service Worker] Deleting old cache:', cacheName);
                            return caches.delete(cacheName);
                        })
                );
            })
            .then(() => clients.claim())
    );
});

// Fetch event - implement caching strategies
self.addEventListener('fetch', (event) => {
    const { request } = event;
    const url = new URL(request.url);
    
    // Skip non-HTTP(S) requests (e.g., chrome-extension://, moz-extension://)
    // Cache API only supports http and https schemes
    // Pass through to network without caching
    if (!url.protocol.startsWith('http')) {
        event.respondWith(fetch(request));
        return;
    }
    
    // Skip non-GET requests and Blazor SignalR connections
    // Pass through to network without caching
    if (request.method !== 'GET' || url.pathname.includes('/_blazor')) {
        event.respondWith(fetch(request));
        return;
    }
    
    // Cache strategy for images: Cache First, Network Fallback
    if (request.destination === 'image' || url.pathname.match(/\.(jpg|jpeg|png|gif|webp|svg|ico)$/i)) {
        event.respondWith(
            caches.match(request)
                .then((cached) => {
                    if (cached) {
                        return cached;
                    }
                    return fetch(request)
                        .then((response) => {
                            // Only cache successful responses
                            if (response && response.status === 200) {
                                const responseClone = response.clone();
                                caches.open(IMAGE_CACHE).then((cache) => {
                                    cache.put(request, responseClone);
                                });
                            }
                            return response;
                        });
                })
                .catch(() => {
                    // Return default avatar if offline and it's an avatar/profile image
                    if (url.pathname.includes('avatar') || url.pathname.includes('profile')) {
                        return caches.match('/images/default-avatar.webp');
                    }
                    // For other images, return a basic response to avoid "Failed to convert value to 'Response'" error
                    return new Response('', { status: 404, statusText: 'Not Found' });
                })
        );
        return;
    }
    
    // Cache strategy for static assets (CSS, JS): Stale While Revalidate
    if (request.destination === 'style' || request.destination === 'script' ||
        url.pathname.match(/\.(css|js)$/i)) {
        event.respondWith(
            caches.open(DYNAMIC_CACHE)
                .then((cache) => {
                    return cache.match(request)
                        .then((cached) => {
                            const fetchPromise = fetch(request)
                                .then((response) => {
                                    if (response && response.status === 200) {
                                        cache.put(request, response.clone());
                                    }
                                    return response;
                                })
                                .catch(() => cached); // Fallback to cache on error
                            
                            return cached || fetchPromise;
                        });
                })
        );
        return;
    }
    
    // Default: Network First, Cache Fallback for HTML pages
    if (request.destination === 'document' || url.pathname === '/' || 
        !url.pathname.includes('.')) {
        event.respondWith(
            fetch(request)
                .then((response) => {
                    if (response && response.status === 200) {
                        const responseClone = response.clone();
                        caches.open(DYNAMIC_CACHE).then((cache) => {
                            cache.put(request, responseClone);
                        });
                    }
                    return response;
                })
                .catch(() => {
                    return caches.match(request)
                        .then((cached) => {
                            return cached || caches.match('/');
                        });
                })
        );
        return;
    }
    
    // Fallback for any other requests: pass through to network
    // This ensures all fetch events are handled with respondWith
    event.respondWith(fetch(request));
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

    const notifyClients = async () => {
        await self.registration.showNotification(
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

        const clientList = await clients.matchAll({ type: 'window', includeUncontrolled: true });
        clientList.forEach((client) => {
            client.postMessage({ type: 'rtub:push-received' });
        });
    };

    event.waitUntil(notifyClients());
});

// Notification click event - handle user clicking on notification
self.addEventListener('notificationclick', (event) => {
    console.log('[Service Worker] Notification clicked');
    
    event.notification.close();

    // Get the URL to open from the notification data
    let urlToOpen = event.notification.data?.url || '/';
    console.log('[Service Worker] Raw URL from notification:', urlToOpen);
    
    // Ensure the URL is absolute
    if (!urlToOpen.startsWith('http')) {
        urlToOpen = new URL(urlToOpen, self.location.origin).href;
    }
    console.log('[Service Worker] Final URL to open:', urlToOpen);

    // For PWA standalone mode, we need a different approach
    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true })
            .then((clientList) => {
                console.log('[Service Worker] Found', clientList.length, 'open windows');
                
                // First, try to find an existing window that we can navigate
                // This is important for PWA mode where clients.openWindow might not work correctly
                for (let i = 0; i < clientList.length; i++) {
                    const client = clientList[i];
                    console.log('[Service Worker] Client', i, 'URL:', client.url, 'visibilityState:', client.visibilityState);
                    
                    // Check if this is an RTUB window (same origin)
                    if (client.url.startsWith(self.location.origin)) {
                        console.log('[Service Worker] Found RTUB window, sending navigate message');
                        // Send a message to the client to navigate
                        client.postMessage({ 
                            type: 'rtub:navigate', 
                            url: urlToOpen 
                        });
                        // Focus the window
                        if ('focus' in client) {
                            return client.focus();
                        }
                        return;
                    }
                }
                
                // No existing window found, open a new one
                if (clients.openWindow) {
                    console.log('[Service Worker] No existing window, opening new one:', urlToOpen);
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
