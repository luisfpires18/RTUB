// Service Worker for Web Push Notifications and Asset Caching
// Handles push events, notification clicks, and offline asset caching

// Service Worker for Web Push Notifications and Asset Caching
// Handles push events, notification clicks, and offline asset caching
// Optimized for mobile PWA performance

// Cache version - increment when updating service worker
// Bumping this forces old caches to be deleted and new assets to be fetched
// v2.7.0 bumped deliberately, not mechanically: up to v2.6.0 this worker persisted
// *application HTML* (including authenticated pages) into DYNAMIC_CACHE and precached
// '/'. Existing clients therefore hold private HTML on disk. The activate handler
// deletes every `rtub-` cache outside the current set, so bumping the version is what
// actually purges those entries from installed clients.
// v2.8.0 (task 037): the Blazor host is gone, so its framework script left the precache list;
// the bump drops the copy older workers stored.
// v2.9.0 (task 046): new RTUB logo under the same /icons/* URLs. Images are cache-first here and never revalidated,
// so only a new IMAGE_CACHE / STATIC_CACHE name stops installed apps showing the old design.
const CACHE_VERSION = 'rtub-v2.9.0';
const STATIC_CACHE = `rtub-static-${CACHE_VERSION}`;
const DYNAMIC_CACHE = `rtub-dynamic-${CACHE_VERSION}`;
const IMAGE_CACHE = `rtub-images-${CACHE_VERSION}`;

// Paths that must never reach a cache, in either direction.
// Matched as prefixes against url.pathname, case-insensitively.
//   /api/      - JSON endpoints, many authenticated and user-specific
//   /auth/     - sign-in / sign-out / challenge round trips
//   /hubs/     - SignalR hubs
//   /health    - liveness probe, must always hit the origin
// Before v2.7.0 these fell through to the document branch (`!pathname.includes('.')`
// matches '/api/push/status', '/health', '/hubs/notifications', ...) and were cached.
const NEVER_CACHE_PREFIXES = ['/api/', '/auth/', '/hubs/', '/health'];

function isNeverCached(pathname) {
    const path = pathname.toLowerCase();
    return NEVER_CACHE_PREFIXES.some((prefix) => path === prefix.replace(/\/$/, '') || path.startsWith(prefix));
}

// Assets to cache on install for offline support.
// NOTE: '/' is deliberately NOT precached. It is application HTML whose content depends
// on the signed-in user, so persisting it leaks private content to a later visitor or to
// the same device after logout. offline.html is the only document fallback we need.
const STATIC_ASSETS = [
    '/offline.html',
    // offline.html loads its script and stylesheet externally (no inline JS, no inline
    // CSS); both must be precached or the fallback page renders unstyled and without
    // its connection-status behaviour.
    '/js/offline.js',
    '/css/offline.css',
    '/icons/rtub-logo-192.png',
    '/icons/rtub-logo-352.webp',
    '/icons/rtub-badge-96.png',
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
            .catch((error) => {
                console.error('[Service Worker] Failed to cache static assets:', error);
            })
            // Deliberately NO forced activation here. Doing it on install made every new
            // worker take over immediately, claim clients and trigger a reload mid-use.
            // The worker waits and activates on its own once every RTUB window is closed.
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
    
    const isSameOrigin = url.origin === self.location.origin;

    // Network-only, never cached in either direction:
    //  - non-GET requests (mutations; the Cache API cannot store them anyway)
    //  - our own /api, /auth, /hubs and /health paths
    // The prefix test is scoped to same-origin so a third-party URL that happens to
    // carry an '/api/...' path is judged on its own merits further down.
    if (request.method !== 'GET' || (isSameOrigin && isNeverCached(url.pathname))) {
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
                                // Fallback to cache on error. DYNAMIC_CACHE only holds
                                // scripts already fetched once online, so fall back to a
                                // cross-cache lookup to reach precached STATIC_ASSETS
                                // (e.g. /js/offline.js, needed by the offline page).
                                .catch(() => cached || caches.match(request));
                            
                            return cached || fetchPromise;
                        });
                })
        );
        return;
    }
    
    // Documents / navigations: Network Only, with offline.html as the failure fallback.
    //
    // The response is returned straight through and is NEVER written to a cache. Almost
    // every page here is authenticated and user-specific; before v2.7.0 each 200 was
    // persisted into DYNAMIC_CACHE, so a later offline visit - or the same device after
    // logout - could be served another session's rendered HTML. The offline experience
    // only needs offline.html, so there is nothing to gain by keeping the documents.
    // Scoped to same-origin: a cross-origin extension-less URL is not one of our pages,
    // and answering it with our offline.html would be worse than letting it fail.
    if (isSameOrigin &&
        (request.destination === 'document' || url.pathname === '/' ||
         !url.pathname.includes('.'))) {
        event.respondWith(
            fetch(request).catch(() => {
                return caches.match('/offline.html').then((offline) => {
                    return offline || new Response(
                        'Sem ligacao.',
                        {
                            status: 503,
                            statusText: 'Service Unavailable',
                            headers: { 'Content-Type': 'text/plain; charset=utf-8' }
                        }
                    );
                });
            })
        );
        return;
    }
    
    // Fallback for any other requests: pass through to network
    // This ensures all fetch events are handled with respondWith
    event.respondWith(fetch(request));
});

// ---------- Web Push (task 038, docs/react-push-v2.md) ----------
// The member turns push on and off in the React /profile; this worker only shows what arrives, opens the
// notification's page, and keeps a subscription alive when the browser rotates its endpoint.

// Push event - show the notification.
// CRITICAL: showNotification runs inside waitUntil, and nothing else does: iOS kills the worker quickly.
self.addEventListener('push', (event) => {
    let notificationData = {
        title: 'RTUB',
        body: 'Nova notificação',
        icon: '/icons/rtub-logo-192.png',
        badge: '/icons/rtub-badge-96.png',
        url: '/',
        tag: null
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
                tag: data.tag || null
            };
        } catch (e) {
            try {
                notificationData.body = event.data.text() || notificationData.body;
            } catch (textError) {
                console.error('[Service Worker] Unreadable push payload:', textError);
            }
        }
    }

    // A unique tag per notification: browsers silently replace a notification that has the same tag as one already
    // shown. The original tag is kept in data.baseTag for click routing.
    const baseTag = notificationData.tag || 'rtub-notification';
    const uniqueTag = baseTag + '-' + Date.now();

    // No vibrate pattern: the platform's default notification behaviour applies (a vibrate option made some iOS
    // versions drop the notification, and setting it only elsewhere needed a user-agent check).
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
        silent: false,
        timestamp: Date.now()
    };

    event.waitUntil(
        self.registration.showNotification(notificationData.title, notificationOptions)
            .catch((error) => {
                console.error('[Service Worker] Error showing notification:', error);
                // Last resort: a minimal notification
                return self.registration.showNotification('RTUB', {
                    body: notificationData.body || 'Nova notificação',
                    icon: '/icons/rtub-logo-192.png',
                    tag: 'rtub-fallback-' + Date.now()
                });
            })
    );
});

// The page a notification opens: its own same-origin URL; anything else (external, malformed) opens the home page.
function notificationTarget(data) {
    let url = (data && data.url) || '/';
    const tag = (data && (data.baseTag || data.tag)) || '';

    if (url === '/' && tag.startsWith('question')) {
        url = '/questions';
    }

    try {
        const target = new URL(url, self.location.origin);
        return target.origin === self.location.origin ? target.href : self.location.origin + '/';
    } catch (e) {
        return self.location.origin + '/';
    }
}

// Notification click - open (or focus and navigate) an RTUB window on the notification's page.
self.addEventListener('notificationclick', (event) => {
    event.notification.close();
    const urlToOpen = notificationTarget(event.notification.data);

    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(async (clientList) => {
            for (const client of clientList) {
                if (!client.url.startsWith(self.location.origin)) {
                    continue;
                }

                // An installed app may not support client.navigate(): sw-register.js navigates on this message.
                if ('navigate' in client) {
                    try {
                        await client.navigate(urlToOpen);
                        return 'focus' in client ? client.focus() : undefined;
                    } catch (navError) {
                        // fall through to the message
                    }
                }

                client.postMessage({ type: 'rtub:navigate', url: urlToOpen });
                return 'focus' in client ? client.focus() : undefined;
            }

            return clients.openWindow ? clients.openWindow(urlToOpen) : undefined;
        })
    );
});

// The antiforgery token every /api/push write needs (X-CSRF-TOKEN); the worker sends the member's cookies.
async function antiforgeryToken() {
    const response = await fetch('/api/public/antiforgery-token', { credentials: 'include', cache: 'no-store' });
    if (!response.ok) {
        throw new Error('antiforgery token ' + response.status);
    }
    return (await response.json()).token;
}

// Stores a subscription for the signed-in member, replacing the endpoint it took over from (same device).
async function sendSubscription(subscription, replacedEndpoint) {
    const json = subscription.toJSON();
    const response = await fetch('/api/push/subscribe', {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': await antiforgeryToken() },
        body: JSON.stringify({
            endpoint: json.endpoint,
            keys: json.keys,
            expirationTime: json.expirationTime ?? null,
            replaces: replacedEndpoint || null
        })
    });
    if (!response.ok) {
        throw new Error('subscribe ' + response.status);
    }
}

// The browser rotated (or dropped) this device's push endpoint. Keep the member subscribed, unless they turned push
// off: they asked for it on this device already, so no prompt is involved. If it fails, /profile shows the device as
// not subscribed and offers to turn it on again.
self.addEventListener('pushsubscriptionchange', (event) => {
    const keepSubscribed = async () => {
        const oldEndpoint = event.oldSubscription ? event.oldSubscription.endpoint : null;
        let subscription = event.newSubscription;

        if (!subscription) {
            const statusResponse = await fetch('/api/push/status', { credentials: 'include', cache: 'no-store' });
            if (!statusResponse.ok) {
                return;
            }
            const status = await statusResponse.json();
            if (!status.vapidPublicKey || status.optedOut) {
                return;
            }
            subscription = await self.registration.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: urlBase64ToUint8Array(status.vapidPublicKey)
            });
        }

        await sendSubscription(subscription, oldEndpoint);
    };

    event.waitUntil(keepSubscribed().catch((error) => {
        console.error('[Service Worker] Could not renew the push subscription:', error);
    }));
});

// Helper: URL-safe base64 string to Uint8Array (the VAPID public key)
function urlBase64ToUint8Array(base64String) {
    const padding = '='.repeat((4 - base64String.length % 4) % 4);
    const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
    const rawData = atob(base64);
    const outputArray = new Uint8Array(rawData.length);
    for (let i = 0; i < rawData.length; ++i) {
        outputArray[i] = rawData.charCodeAt(i);
    }
    return outputArray;
}
