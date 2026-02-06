// Service Worker for Web Push Notifications and Asset Caching
// Handles push events, notification clicks, and offline asset caching

// Service Worker for Web Push Notifications and Asset Caching
// Handles push events, notification clicks, and offline asset caching
// Optimized for mobile PWA performance

// Cache version - increment when updating service worker
const CACHE_VERSION = 'rtub-v28';
const STATIC_CACHE = `rtub-static-${CACHE_VERSION}`;
const DYNAMIC_CACHE = `rtub-dynamic-${CACHE_VERSION}`;
const IMAGE_CACHE = `rtub-images-${CACHE_VERSION}`;
const OFFLINE_PAGE = '/offline';

// Assets to cache on install for offline support
const STATIC_ASSETS = [
    '/',
    '/offline.html',
    '/icons/rtub-logo-192.png',
    '/icons/rtub-logo-512.png',
    '/icons/rtub-badge-96.png',
    '/images/default-avatar.webp',
    '/manifest.webmanifest',
    '/_framework/blazor.web.js'
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
            .then(() => {
                // Claim clients immediately for better offline support
                return clients.claim();
            })
            .then(() => {
                // Ensure offline page is cached
                return caches.open(STATIC_CACHE).then(cache => {
                    return cache.add('/offline.html').catch(() => {
                        // Ignore if already cached or fails
                    });
                });
            })
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
                            // Return cached page or offline fallback
                            if (cached) {
                                return cached;
                            }
                            // For navigation requests, return offline page
                            if (request.mode === 'navigate') {
                                return caches.match('/offline.html') || caches.match('/');
                            }
                            return caches.match('/');
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
        badge: '/icons/rtub-badge-96.png',
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
                badge: data.badge || notificationData.badge,
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
                    url: notificationData.url,
                    tag: notificationData.tag
                },
                requireInteraction: false,
                renotify: true,
                timestamp: Date.now(),
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

    // Get the URL and tag from notification data
    const notificationPayload = event.notification.data || {};
    let urlToOpen = notificationPayload.url || '/';
    const notificationTag = notificationPayload.tag || event.notification.tag || '';
    
    // Fallback: if URL is missing or root, check tag to determine correct destination
    if (!urlToOpen || urlToOpen === '/') {
        if (notificationTag.startsWith('question')) {
            urlToOpen = '/questions';
        } else if (notificationTag.startsWith('message')) {
            urlToOpen = '/messages';
        }
    }
    console.log('[Service Worker] Raw URL from notification:', urlToOpen, 'Tag:', notificationTag);
    
    // Ensure the URL is absolute
    if (!urlToOpen.startsWith('http')) {
        urlToOpen = new URL(urlToOpen, self.location.origin).href;
    }
    
    // Security: Validate URL is same-origin to prevent open redirect
    const urlObj = new URL(urlToOpen);
    if (urlObj.origin !== self.location.origin) {
        console.warn('[Service Worker] Blocked navigation to external URL:', urlToOpen);
        urlToOpen = self.location.origin; // Fallback to root of same origin
    }
    console.log('[Service Worker] Final URL to open:', urlToOpen);

    // For PWA standalone mode, we need a different approach
    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true })
            .then(async (clientList) => {
                console.log('[Service Worker] Found', clientList.length, 'open windows');
                
                // First, try to find an existing window that we can navigate
                // This is important for PWA mode where clients.openWindow might not work correctly
                for (let i = 0; i < clientList.length; i++) {
                    const client = clientList[i];
                    console.log('[Service Worker] Client', i, 'URL:', client.url, 'visibilityState:', client.visibilityState);
                    
                    // Check if this is an RTUB window (same origin)
                    if (client.url.startsWith(self.location.origin)) {
                        console.log('[Service Worker] Found RTUB window, navigating to:', urlToOpen);
                        
                        // Try to use client.navigate() if available (more reliable)
                        if ('navigate' in client) {
                            try {
                                await client.navigate(urlToOpen);
                                if ('focus' in client) {
                                    return client.focus();
                                }
                                return;
                            } catch (navError) {
                                console.log('[Service Worker] client.navigate failed, falling back to postMessage:', navError);
                            }
                        }
                        
                        // Fallback: Send a message to the client to navigate
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
