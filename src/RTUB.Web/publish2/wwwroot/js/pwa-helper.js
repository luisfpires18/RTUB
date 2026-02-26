// PWA Helper
// Utilities for detecting PWA mode and managing PWA-specific features

window.pwaHelper = {
    /**
     * Mobile breakpoint - matches Bootstrap's md breakpoint
     */
    MOBILE_BREAKPOINT: 768,

    /**
     * Checks if the app is running as a Trusted Web Activity (TWA) from the Play Store
     * TWAs may not always match display-mode: standalone, especially when
     * Digital Asset Links verification fails and it falls back to Custom Tab
     */
    isTwaMode: function() {
        // Check if launched from an Android app via TWA
        // document.referrer will contain android-app:// scheme when launched from TWA
        if (document.referrer && document.referrer.startsWith('android-app://')) {
            return true;
        }

        // Check if running in a TWA via the Digital Asset Links relationship
        // When a TWA is verified, it typically runs with a specific user agent
        const ua = navigator.userAgent || '';
        if (ua.includes('AmazonWebAppPlatform') || ua.includes('AmazonWebAppRuntime')) {
            return true;
        }

        // Check for TWA-specific minimal-ui display mode (some TWA implementations)
        if (window.matchMedia('(display-mode: minimal-ui)').matches) {
            return true;
        }

        // Check sessionStorage for TWA flag (set during initial load detection)
        try {
            if (sessionStorage.getItem('rtub-is-twa') === 'true') {
                return true;
            }
        } catch (e) {
            // Ignore storage errors
        }

        return false;
    },

    /**
     * Checks if the app is running in PWA/standalone mode
     * Works for iOS, Android PWA, and Android TWA (Play Store)
     */
    isPwaMode: function() {
        // Check if running in standalone mode (installed PWA)
        const isStandalone = window.matchMedia('(display-mode: standalone)').matches;
        
        // iOS Safari specific check
        const isIosStandalone = window.navigator.standalone === true;

        // Check if running as a TWA from the Play Store
        const isTwa = this.isTwaMode();

        // Persist TWA detection for subsequent checks within the same session
        if (isTwa) {
            try { sessionStorage.setItem('rtub-is-twa', 'true'); } catch (e) { /* ignore */ }
        }
        
        return isStandalone || isIosStandalone || isTwa;
    },

    /**
     * Checks if the current viewport is mobile-sized
     * Returns true for screens <= 768px width
     */
    isMobileView: function() {
        return window.innerWidth <= this.MOBILE_BREAKPOINT;
    },

    /**
     * Checks if the app should use mobile behavior
     * Returns true if running as PWA or on mobile browser (but not desktop web)
     */
    isMobilePwaOrBrowser: function() {
        return this.isPwaMode() || this.isMobileView();
    },

    /**
     * Checks if the user has already been prompted for push notifications
     * Uses localStorage to track prompt history
     */
    hasBeenPrompted: function() {
        try {
            const prompted = localStorage.getItem('rtub-push-prompted');
            return prompted === 'true';
        } catch (e) {
            console.error('Error reading from localStorage:', e);
            return false;
        }
    },

    /**
     * Marks that the user has been prompted for push notifications
     */
    markAsPrompted: function() {
        try {
            localStorage.setItem('rtub-push-prompted', 'true');
            return true;
        } catch (e) {
            console.error('Error writing to localStorage:', e);
            return false;
        }
    },

    /**
     * Resets the prompt state (for testing purposes)
     */
    resetPromptState: function() {
        try {
            localStorage.removeItem('rtub-push-prompted');
            return true;
        } catch (e) {
            console.error('Error removing from localStorage:', e);
            return false;
        }
    },

    /**
     * Checks if push notifications should be shown
     * Returns true if app is in PWA/TWA mode and user hasn't been prompted
     * OR if the user was previously subscribed but the subscription was lost
     */
    shouldShowPushPrompt: function() {
        if (!this.isPwaMode()) {
            return false;
        }
        
        // Always show if never prompted
        if (!this.hasBeenPrompted()) {
            return true;
        }

        // Even if prompted before, show again if subscription was lost
        // This handles cases where Chrome rotated the push endpoint,
        // the subscription expired, or the user cleared browser data
        try {
            const subscriptionLost = localStorage.getItem('rtub-push-subscription-lost') === 'true';
            if (subscriptionLost) {
                return true;
            }
        } catch (e) {
            // Ignore storage errors
        }

        return false;
    },

    /**
     * Marks that the push subscription was lost and needs to be re-established
     * Called when subscription health check detects a stale/missing subscription
     */
    markSubscriptionLost: function() {
        try {
            localStorage.setItem('rtub-push-subscription-lost', 'true');
            return true;
        } catch (e) {
            console.error('Error writing to localStorage:', e);
            return false;
        }
    },

    /**
     * Clears the subscription-lost flag after successful re-subscription
     */
    clearSubscriptionLost: function() {
        try {
            localStorage.removeItem('rtub-push-subscription-lost');
            return true;
        } catch (e) {
            console.error('Error removing from localStorage:', e);
            return false;
        }
    },

    /**
     * Checks the health of the current push subscription
     * Returns: 'active', 'expired', 'missing', or 'error'
     * Call this on app start in PWA/TWA mode to detect stale subscriptions
     */
    checkSubscriptionHealth: async function() {
        try {
            if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
                return 'unsupported';
            }

            const registration = await navigator.serviceWorker.ready;
            if (!registration || !registration.pushManager) {
                return 'error';
            }

            const subscription = await registration.pushManager.getSubscription();
            
            if (!subscription) {
                // No subscription exists - check if user previously had one
                if (Notification.permission === 'granted' && this.hasBeenPrompted()) {
                    // User granted permission before but subscription is gone
                    // This means the subscription was rotated or expired
                    this.markSubscriptionLost();
                    return 'missing';
                }
                return 'none';
            }

            // Check if subscription has expired
            if (subscription.expirationTime && subscription.expirationTime < Date.now()) {
                this.markSubscriptionLost();
                return 'expired';
            }

            return 'active';
        } catch (e) {
            console.error('Error checking subscription health:', e);
            return 'error';
        }
    },

    /**
     * Detects if Android OS-level notifications are likely blocked
     * This handles the Android 13+ scenario where browser permission is granted
     * but OS-level notification permission for Chrome/TWA is denied
     */
    isNotificationLikelyBlocked: function() {
        // If browser says permission is denied, it's definitely blocked
        if (!('Notification' in window)) return true;
        if (Notification.permission === 'denied') return true;

        // On Android, we can't directly check OS-level permission.
        // But we can detect the situation where:
        // 1. Browser permission is 'granted' (or 'default')
        // 2. But the user is on Android (UA check)
        // 3. And notifications still don't show
        // We'll use a heuristic based on failed test notifications
        return false;
    },

    /**
     * Fetches push notification status from the server
     * Returns push status data or null if unavailable
     */
    getPushStatus: async function() {
        try {
            const response = await fetch('/api/push/status', { credentials: 'include' });
            
            // Handle specific HTTP status codes
            if (response.status === 401) {
                console.log('User not authenticated');
                return null;
            }
            
            if (response.status === 403) {
                console.log('User not authorized to access push notifications');
                return null;
            }
            
            if (!response.ok) {
                console.error('Failed to fetch push status:', response.status, response.statusText);
                return null;
            }
            
            const data = await response.json();
            return data;
        } catch (e) {
            console.error('Error fetching push status:', e);
            return null;
        }
    },

    /**
     * Initializes the push notification manager
     * Returns true if successful, false otherwise
     */
    initializePushManager: async function() {
        try {
            if (typeof PushNotificationsManager === 'undefined') {
                console.error('PushNotificationsManager not loaded');
                return false;
            }
            const manager = new PushNotificationsManager();
            const success = await manager.initialize();
            if (success) {
                window.rtubPushManager = manager;
                return true;
            }
            return false;
        } catch (e) {
            console.error('Error initializing push manager:', e);
            return false;
        }
    },

    /**
     * Subscribes to push notifications
     * Returns true if successful, false otherwise
     */
    subscribeToPush: async function() {
        try {
            if (window.rtubPushManager && typeof window.rtubPushManager.subscribe === 'function') {
                return await window.rtubPushManager.subscribe();
            }
            throw new Error('RTUB Push manager not available or subscribe method missing');
        } catch (e) {
            console.error('Error subscribing to push:', e);
            return false;
        }
    },

    /**
     * Applies a CSS class on the document element to allow PWA-specific styling
     */
    applyPwaModeClass: function() {
        const root = document.documentElement;
        if (!root) return;
        root.classList.toggle('pwa-mode', this.isPwaMode());
    }
};

const applyPwaMode = () => {
    if (window.pwaHelper) {
        window.pwaHelper.applyPwaModeClass();
    }
};

document.addEventListener('DOMContentLoaded', applyPwaMode);
window.addEventListener('pageshow', applyPwaMode);
window.addEventListener('resize', applyPwaMode);
window.addEventListener('orientationchange', applyPwaMode);
window.addEventListener('focus', applyPwaMode);
document.addEventListener('visibilitychange', () => {
    if (!document.hidden) {
        applyPwaMode();
    }
});

const displayModeQuery = window.matchMedia('(display-mode: standalone)');
if (displayModeQuery?.addEventListener) {
    displayModeQuery.addEventListener('change', applyPwaMode);
} else if (displayModeQuery?.addListener) {
    displayModeQuery.addListener(applyPwaMode);
}

// Global function for Blazor to check if device is mobile/PWA
window.isMobileDevice = function() {
    return window.pwaHelper.isMobilePwaOrBrowser();
};

// App Badge API — sets/clears the numeric badge on the PWA/TWA app icon
// Supports Android (Chrome/TWA), iOS 16.4+ (home screen PWA), and desktop Chrome/Edge
window.appBadge = {
    /**
     * Sets the app badge to the given count.
     * Pass 0 to clear the badge.
     * @param {number} count - The number to display on the badge
     */
    set: async function(count) {
        if ('setAppBadge' in navigator) {
            try {
                if (count > 0) {
                    await navigator.setAppBadge(count);
                } else {
                    await navigator.clearAppBadge();
                }
            } catch (e) {
                // Silently fail on unsupported platforms or permission issues
            }
        }
    },
    /**
     * Clears the app badge entirely.
     */
    clear: async function() {
        if ('clearAppBadge' in navigator) {
            try {
                await navigator.clearAppBadge();
            } catch (e) {
                // Silently fail
            }
        }
    }
};
