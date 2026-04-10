// Service Worker for Web Push Notifications and Asset Caching
// Handles push events, notification clicks, and offline asset caching

// Service Worker for Web Push Notifications and Asset Caching
// Handles push events, notification clicks, and offline asset caching
// Optimized for mobile PWA performance

// Cache version - increment when updating service worker
// Bumping this forces old caches to be deleted and new assets to be fetched
const CACHE_VERSION = 'rtub-v2.4.4';
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
// CRITICAL: Must call showNotification synchronously within waitUntil
// to prevent iOS Safari from killing the service worker before display
self.addEventListener('push', (event) => {
    console.log('[Service Worker] Push received at', new Date().toISOString());
    
    let notificationData = {
        title: 'RTUB Notification',
        body: 'You have a new notification',
        icon: '/icons/rtub-logo-192.png',
        badge: '/icons/rtub-badge-96.png',
        url: '/',
        tag: null,
        unreadCount: null
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
                tag: data.tag || null,
                unreadCount: data.unreadCount != null ? data.unreadCount : null
            };
        } catch (e) {
            console.error('[Service Worker] Error parsing push data:', e);
            try {
                notificationData.body = event.data.text();
            } catch (textError) {
                console.error('[Service Worker] Error reading push text:', textError);
            }
        }
    }

    // CRITICAL FIX: Generate unique tag per notification to prevent collapsing
    // iOS and Android replace notifications with the same tag silently.
    // Append timestamp to ensure each notification gets its own slot.
    // Keep the base tag for grouping context (e.g., for notificationclick routing)
    const baseTag = notificationData.tag || 'rtub-notification';
    const uniqueTag = baseTag + '-' + Date.now();

    // Detect iOS to avoid setting vibrate (causes silent failures on iOS 16.4-17.x)
    const isIOS = /iPad|iPhone|iPod/.test(self.navigator?.userAgent || '') ||
        (/Macintosh/.test(self.navigator?.userAgent || '') && 'ontouchend' in self);

    // Build notification options - platform-compatible
    const notificationOptions = {
        body: notificationData.body,
        icon: notificationData.icon,
        badge: notificationData.badge,
        tag: uniqueTag,
        data: {
            url: notificationData.url,
            baseTag: baseTag,
            tag: uniqueTag,
            timestamp: Date.now()
        },
        requireInteraction: false,
        renotify: true,
        silent: false,           // Explicitly not silent — ensures Android shows popup/sound
        timestamp: Date.now()
    };

    // Add vibrate for Android only — iOS Safari fails silently if vibrate is present
    if (!isIOS) {
        notificationOptions.vibrate = [200, 100, 200];
    }

    // Show notification FIRST (critical for iOS — SW gets killed quickly)
    // Then notify open clients as a secondary action
    const showAndNotify = self.registration.showNotification(
        notificationData.title,
        notificationOptions
    ).then(() => {
        console.log('[Service Worker] Notification displayed:', uniqueTag);
        return clients.matchAll({ type: 'window', includeUncontrolled: true });
    }).then((clientList) => {
        clientList.forEach((client) => {
            client.postMessage({
                type: 'rtub:push-received',
                title: notificationData.title,
                tag: baseTag
            });
        });
        // Thread clientList into the next step to decide whether to update the badge.
        return clientList;
    }).then((clientList) => {
        // Only update the app badge from the service worker when the app is NOT open.
        // When the app is open, it will update the badge itself via RefreshUnreadMessages
        // (triggered by the rtub:push-received postMessage above), which uses the real DB count.
        // Using notifications.length here would set the badge to the OS notification tray count,
        // which diverges from the actual unread message count when notifications pile up.
        if ('setAppBadge' in self.navigator && clientList.length === 0) {
            // App is closed — use unreadCount from payload if available (accurate),
            // otherwise fall back to notification tray count (best available proxy).
            if (notificationData.unreadCount != null) {
                return self.navigator.setAppBadge(notificationData.unreadCount).catch(() => {});
            }
            return self.registration.getNotifications().then((notifications) => {
                return self.navigator.setAppBadge(notifications.length).catch(() => {});
            }).catch(() => {});
        }
    }).catch((error) => {
        console.error('[Service Worker] Error showing notification:', error);
        // Last resort: try a minimal notification
        return self.registration.showNotification('RTUB', {
            body: notificationData.body || 'Nova notificação',
            icon: '/icons/rtub-logo-192.png',
            tag: 'rtub-fallback-' + Date.now()
        });
    });

    event.waitUntil(showAndNotify);
});

// Notification click event - handle user clicking on notification
self.addEventListener('notificationclick', (event) => {
    console.log('[Service Worker] Notification clicked');
    
    event.notification.close();

    // Clear the app badge when the user taps a notification.
    // The Blazor app will re-set it to the accurate DB unread count once it opens.
    // Do NOT use notifications.length here — that counts OS notification tray items,
    // which diverges from the actual unread message count.
    if ('clearAppBadge' in self.navigator) {
        self.navigator.clearAppBadge().catch(() => {});
    }

    // Get the URL and tag from notification data
    const notificationPayload = event.notification.data || {};
    let urlToOpen = notificationPayload.url || '/';
    // Use baseTag for routing logic (without the unique timestamp suffix)
    const notificationTag = notificationPayload.baseTag || notificationPayload.tag || event.notification.tag || '';
    
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

// Push subscription change event - handle browser-initiated subscription rotation
// Chrome on Android periodically rotates push endpoints for security.
// Without this handler, the old endpoint becomes invalid (410 Gone) and
// the user silently stops receiving notifications until they manually re-subscribe.
self.addEventListener('pushsubscriptionchange', (event) => {
    console.log('[Service Worker] Push subscription changed');
    
    const resubscribe = async () => {
        try {
            // Get the new subscription using the same VAPID key
            const oldSubscription = event.oldSubscription;
            const newSubscription = event.newSubscription;
            
            if (newSubscription) {
                // Browser already created a new subscription, send it to server
                const subJson = newSubscription.toJSON();
                
                const response = await fetch('/api/push/subscribe', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    credentials: 'include',
                    body: JSON.stringify({
                        endpoint: subJson.endpoint,
                        keys: {
                            p256dh: subJson.keys.p256dh,
                            auth: subJson.keys.auth
                        },
                        expirationTime: newSubscription.expirationTime
                    })
                });
                
                if (response.ok) {
                    console.log('[Service Worker] New push subscription sent to server');
                } else {
                    console.error('[Service Worker] Failed to send new subscription:', response.status);
                }
            } else if (oldSubscription) {
                // No new subscription provided, try to create one
                // We need the VAPID key - fetch it from the server
                try {
                    const statusResponse = await fetch('/api/push/status', { credentials: 'include' });
                    if (statusResponse.ok) {
                        const status = await statusResponse.json();
                        if (status.vapidPublicKey) {
                            // Convert VAPID key and resubscribe
                            const applicationServerKey = urlBase64ToUint8Array(status.vapidPublicKey);
                            const subscription = await self.registration.pushManager.subscribe({
                                userVisibleOnly: true,
                                applicationServerKey: applicationServerKey
                            });
                            
                            const subJson = subscription.toJSON();
                            await fetch('/api/push/subscribe', {
                                method: 'POST',
                                headers: { 'Content-Type': 'application/json' },
                                credentials: 'include',
                                body: JSON.stringify({
                                    endpoint: subJson.endpoint,
                                    keys: {
                                        p256dh: subJson.keys.p256dh,
                                        auth: subJson.keys.auth
                                    },
                                    expirationTime: subscription.expirationTime
                                })
                            });
                            console.log('[Service Worker] Successfully re-subscribed after endpoint rotation');
                        }
                    }
                } catch (resubError) {
                    console.error('[Service Worker] Failed to re-subscribe:', resubError);
                    // Notify clients that subscription was lost so they can show re-subscribe UI
                    const clientList = await clients.matchAll({ type: 'window', includeUncontrolled: true });
                    clientList.forEach((client) => {
                        client.postMessage({ type: 'rtub:subscription-lost' });
                    });
                }
            }

            // Notify server to remove old subscription if we had one
            if (oldSubscription && oldSubscription.endpoint) {
                try {
                    await fetch('/api/push/unsubscribe', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        credentials: 'include',
                        body: JSON.stringify({ endpoint: oldSubscription.endpoint })
                    });
                } catch (e) {
                    // Ignore cleanup errors
                }
            }
        } catch (error) {
            console.error('[Service Worker] Error handling subscription change:', error);
            // Notify clients about the failure
            const clientList = await clients.matchAll({ type: 'window', includeUncontrolled: true });
            clientList.forEach((client) => {
                client.postMessage({ type: 'rtub:subscription-lost' });
            });
        }
    };
    
    event.waitUntil(resubscribe());
});

// Helper: Convert URL-safe base64 string to Uint8Array (for service worker context)
function urlBase64ToUint8Array(base64String) {
    const paddingLength = (4 - base64String.length % 4) % 4;
    const padding = '='.repeat(paddingLength);
    const base64 = (base64String + padding)
        .replace(/-/g, '+')
        .replace(/_/g, '/');

    const rawData = atob(base64);
    const outputArray = new Uint8Array(rawData.length);

    for (let i = 0; i < rawData.length; ++i) {
        outputArray[i] = rawData.charCodeAt(i);
    }
    return outputArray;
}
