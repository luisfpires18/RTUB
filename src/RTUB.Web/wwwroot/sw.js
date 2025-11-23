// RTUB Service Worker for PWA support
// This is a minimal service worker to enable PWA installation on Android

const CACHE_NAME = 'rtub-v1';

// Install event - cache essential resources
self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => {
      return cache.addAll([
        '/',
        '/manifest.webmanifest',
        '/icons/rtub-logo-192.png',
        '/icons/rtub-logo-512.png',
        '/favicon.ico'
      ]);
    })
  );
  self.skipWaiting();
});

// Activate event - clean up old caches
self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((cacheNames) => {
      return Promise.all(
        cacheNames
          .filter((cacheName) => cacheName !== CACHE_NAME)
          .map((cacheName) => caches.delete(cacheName))
      );
    })
  );
  self.clients.claim();
});

// Fetch event - network first, fallback to cache
self.addEventListener('fetch', (event) => {
  // Skip caching for non-GET requests
  if (event.request.method !== 'GET') {
    return;
  }

  // Skip caching for API calls and dynamic content
  const url = new URL(event.request.url);
  if (url.pathname.startsWith('/api/') || 
      url.pathname.startsWith('/_blazor') ||
      url.pathname.includes('/auth/')) {
    return;
  }

  event.respondWith(
    fetch(event.request)
      .then((response) => {
        // Only cache successful responses
        if (response && response.ok) {
          // Clone the response before caching
          const responseToCache = response.clone();
          
          // Cache static resources (HTML, CSS, JS, images, icons)
          if (url.pathname.match(/\.(html|css|js|png|jpg|jpeg|gif|svg|webp|ico|json|webmanifest)$/i) ||
              url.pathname === '/') {
            caches.open(CACHE_NAME).then((cache) => {
              cache.put(event.request, responseToCache).catch((error) => {
                console.error('Failed to cache:', event.request.url, error);
              });
            });
          }
        }
        return response;
      })
      .catch(() => {
        // If network fails, try to get from cache
        return caches.match(event.request);
      })
  );
});
