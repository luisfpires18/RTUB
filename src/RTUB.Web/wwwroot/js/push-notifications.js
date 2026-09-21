// Push Notifications Manager
// Handles permission requests and push subscription management.
// It does NOT own the service worker - /js/sw-register.js is the sole registration owner;
// this manager adopts that registration via navigator.serviceWorker.ready.
// Supports PWA standalone mode and TWA (Trusted Web Activity) from the Play Store

class PushNotificationsManager {
    constructor() {
        this.registration = null;
        this.subscription = null;
        this.vapidPublicKey = null;
        this.isEnabled = false;
        this.isConfigured = false;
    }

    /**
     * Initializes the push notifications manager
     * Checks feature status and registers service worker if enabled
     * OPTIMIZATION: Early return for unsupported browsers to avoid unnecessary checks
     */
    async initialize() {
        try {
            // OPTIMIZATION: Early return if push notifications are not supported
            if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
                console.warn('Push notifications are not supported in this browser');
                return false;
            }

            // Detect platform for platform-specific behavior
            this.isIOS = /iPad|iPhone|iPod/.test(navigator.userAgent) ||
                (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
            this.isTWA = document.referrer.includes('android-app://') ||
                (window.matchMedia && window.matchMedia('(display-mode: standalone)').matches && /Android/.test(navigator.userAgent));
            this.isAndroid = /Android/.test(navigator.userAgent);
            
            if (this.isIOS) {
                console.log('[Push] iOS device detected');
            }
            if (this.isTWA) {
                console.log('[Push] TWA mode detected');
            }

            // Check feature status from the server with timeout to prevent hanging
            const status = await Promise.race([
                this.checkFeatureStatus(),
                new Promise((_, reject) => 
                    setTimeout(() => reject(new Error('Feature status check timeout')), 8000)
                )
            ]);
            
            if (!status.isEnabled || !status.isConfigured) {
                console.log('Push notifications are not enabled or configured');
                return false;
            }

            this.vapidPublicKey = status.vapidPublicKey;
            this.isEnabled = status.isEnabled;
            this.isConfigured = status.isConfigured;

            // Register service worker
            await this.registerServiceWorker();

            // Start periodic subscription health check (every 30 min)
            this._startHealthCheck();

            return true;
        } catch (error) {
            console.error('Error initializing push notifications:', error);
            return false;
        }
    }

    /**
     * Checks the push notification feature status from the server
     */
    async checkFeatureStatus() {
        const response = await fetch('/api/push/status', {
            credentials: 'include'
        });

        if (!response.ok) {
            throw new Error('Failed to check push notification status');
        }

        return await response.json();
    }

    /**
     * Adopts the service worker registration owned by /js/sw-register.js.
     *
     * This manager deliberately never calls the service worker registration API.
     * sw-register.js is the single registration owner; a second register() call here
     * produced a duplicate registration path with different options (no explicit scope,
     * no updateViaCache) and duplicated the update/lifecycle wiring. Waiting instead
     * keeps the same guarantee this method always provided: the returned registration
     * has an *active* worker, which iOS Safari requires before pushManager.subscribe().
     */
    async registerServiceWorker() {
        try {
            // navigator.serviceWorker.ready never rejects and never resolves if nothing
            // ever registers, so bound the wait rather than hanging initialize() forever.
            const registration = await Promise.race([
                navigator.serviceWorker.ready,
                new Promise((_, reject) =>
                    setTimeout(() => reject(new Error('Service worker did not become ready')), 10000)
                )
            ]);

            this.registration = registration;
            console.log('Service Worker is ready and active');

            // Check if already subscribed
            this.subscription = await this.registration.pushManager.getSubscription();

            return this.registration;
        } catch (error) {
            console.error('Service Worker is not available for push:', error);
            throw error;
        }
    }

    /**
     * Requests notification permission from the user
     */
    async requestPermission() {
        if (!('Notification' in window)) {
            console.warn('Notifications not supported');
            return false;
        }

        const permission = await Notification.requestPermission();
        return permission === 'granted';
    }

    /**
     * Subscribes to push notifications
     * Enhanced for iOS and Android TWA reliability
     */
    async subscribe() {
        try {
            // Check if already initialized
            if (!this.registration) {
                console.log('Registration not found, initializing...');
                const initialized = await this.initialize();
                if (!initialized) {
                    throw new Error('Push notifications are not available');
                }
            }

            // Verify registration has pushManager
            if (!this.registration || !this.registration.pushManager) {
                throw new Error('Push manager not available on registration');
            }

            // Verify VAPID key is available
            if (!this.vapidPublicKey) {
                throw new Error('VAPID public key is not configured');
            }

            // Request permission if not already granted
            if (Notification.permission !== 'granted') {
                console.log('Requesting notification permission...');
                const granted = await this.requestPermission();
                if (!granted) {
                    throw new Error('Notification permission denied');
                }
            }

            console.log('Notification permission:', Notification.permission);
            console.log('Registration state:', this.registration.active ? 'active' : 'not active');

            // IMPORTANT: On iOS, ensure the service worker is fully active before subscribing
            // iOS Safari can have timing issues where the SW isn't ready yet
            if (this.isIOS) {
                const swReady = await navigator.serviceWorker.ready;
                this.registration = swReady;
                console.log('[Push] iOS: Service worker ready confirmed');
            }

            // Check if already subscribed
            let subscription = await this.registration.pushManager.getSubscription();

            if (subscription) {
                // Validate existing subscription is still good
                // On Android TWA, subscriptions can become stale after app updates
                try {
                    await this.sendSubscriptionToServer(subscription);
                    this.subscription = subscription;
                    console.log('Existing subscription synced with server');

                    if (window.pwaHelper && typeof window.pwaHelper.clearSubscriptionLost === 'function') {
                        window.pwaHelper.clearSubscriptionLost();
                    }
                    if (window.pwaHelper && typeof window.pwaHelper.clearOptedOut === 'function') {
                        window.pwaHelper.clearOptedOut();
                    }
                    return true;
                } catch (syncError) {
                    console.warn('Existing subscription sync failed, creating new one:', syncError);
                    // Unsubscribe the stale one and create fresh
                    try { await subscription.unsubscribe(); } catch (e) { /* ignore */ }
                    subscription = null;
                }
            }

            if (!subscription) {
                // Create new subscription
                try {
                    // Convert VAPID public key to Uint8Array
                    const applicationServerKey = this.urlBase64ToUint8Array(this.vapidPublicKey);
                    
                    // Validate that applicationServerKey was successfully converted
                    if (!applicationServerKey || applicationServerKey.length === 0) {
                        throw new Error('Failed to convert VAPID public key to Uint8Array');
                    }
                    
                    console.log('VAPID key converted. Length:', applicationServerKey.length);
                    
                    // Subscribe with retry for TWA/Android where timing issues can occur
                    let retries = this.isTWA ? 3 : 1;
                    let lastError = null;
                    
                    for (let attempt = 1; attempt <= retries; attempt++) {
                        try {
                            subscription = await this.registration.pushManager.subscribe({
                                userVisibleOnly: true,
                                applicationServerKey: applicationServerKey
                            });
                            break; // Success
                        } catch (subError) {
                            lastError = subError;
                            console.warn(`[Push] Subscribe attempt ${attempt}/${retries} failed:`, subError.message);
                            if (attempt < retries) {
                                await new Promise(r => setTimeout(r, 1000 * attempt));
                                // Re-acquire registration in case it went stale
                                this.registration = await navigator.serviceWorker.ready;
                            }
                        }
                    }
                    
                    if (!subscription) {
                        throw new Error(`Push subscribe failed after ${retries} attempts: ${lastError?.name}: ${lastError?.message}`);
                    }
                } catch (subError) {
                    console.error('PUSH SUBSCRIBE ERROR', subError, subError.name, subError.message);
                    throw new Error(`Push subscribe failed: ${subError.name}: ${subError.message}`);
                }
            }

            // Send subscription to server with retry
            let serverSynced = false;
            for (let attempt = 1; attempt <= 3; attempt++) {
                try {
                    await this.sendSubscriptionToServer(subscription);
                    serverSynced = true;
                    break;
                } catch (serverError) {
                    console.warn(`[Push] Server sync attempt ${attempt}/3 failed:`, serverError.message);
                    if (attempt < 3) {
                        await new Promise(r => setTimeout(r, 1000 * attempt));
                    }
                }
            }
            
            if (!serverSynced) {
                console.error('[Push] Failed to sync subscription with server after 3 attempts');
                // Don't throw - subscription is still valid locally
                // It will be synced on next health check
            }

            this.subscription = subscription;
            console.log('Successfully subscribed to push notifications');

            // Clear any subscription-lost/opted-out flags since we have a valid subscription now
            if (window.pwaHelper && typeof window.pwaHelper.clearSubscriptionLost === 'function') {
                window.pwaHelper.clearSubscriptionLost();
            }
            if (window.pwaHelper && typeof window.pwaHelper.clearOptedOut === 'function') {
                window.pwaHelper.clearOptedOut();
            }

            return true;
        } catch (error) {
            console.error('Error subscribing to push notifications:', error);
            throw error;
        }
    }

    /**
     * Unsubscribes from push notifications
     */
    async unsubscribe() {
        try {
            if (!this.subscription) {
                this.subscription = await this.registration.pushManager.getSubscription();
            }

            if (this.subscription) {
                // Notify server
                await this.sendUnsubscribeToServer(this.subscription.endpoint);

                // Unsubscribe from push manager
                await this.subscription.unsubscribe();
                this.subscription = null;

                // Mark as explicitly opted out so self-healing/recovery logic
                // (health check, validateAndRefreshSubscription) never silently
                // re-creates this subscription - unsubscribing does not revoke
                // Notification.permission, which stays 'granted' (especially on iOS).
                if (window.pwaHelper && typeof window.pwaHelper.markOptedOut === 'function') {
                    window.pwaHelper.markOptedOut();
                }

                console.log('Successfully unsubscribed from push notifications');
                return true;
            }

            return false;
        } catch (error) {
            console.error('Error unsubscribing from push notifications:', error);
            throw error;
        }
    }

    /**
     * Checks if currently subscribed to push notifications
     */
    async isSubscribed() {
        if (!this.registration) {
            return false;
        }

        const subscription = await this.registration.pushManager.getSubscription();
        return subscription !== null;
    }

    /**
     * Gets the current subscription
     */
    async getSubscription() {
        if (!this.registration) {
            return null;
        }

        return await this.registration.pushManager.getSubscription();
    }

    /**
     * Validates and refreshes the push subscription if needed
     * Handles expired/rotated subscriptions by creating a new one
     * Returns: 'active' | 'refreshed' | 'missing' | 'error'
     */
    async validateAndRefreshSubscription() {
        try {
            if (!this.registration || !this.registration.pushManager) {
                return 'error';
            }

            const subscription = await this.registration.pushManager.getSubscription();

            if (!subscription) {
                // No subscription exists. If the user explicitly opted out, this is
                // expected - do not silently resubscribe them.
                if (window.pwaHelper && typeof window.pwaHelper.isOptedOut === 'function' && window.pwaHelper.isOptedOut()) {
                    console.log('[Push] User opted out, skipping silent resubscribe');
                    return 'missing';
                }

                // If permission was previously granted, the subscription was lost
                if (Notification.permission === 'granted' && this.vapidPublicKey) {
                    console.log('Push subscription lost, attempting to re-subscribe...');
                    try {
                        await this.subscribe();
                        // Clear the subscription-lost flag
                        if (window.pwaHelper) {
                            window.pwaHelper.clearSubscriptionLost();
                        }
                        return 'refreshed';
                    } catch (resubError) {
                        console.error('Failed to re-subscribe:', resubError);
                        if (window.pwaHelper) {
                            window.pwaHelper.markSubscriptionLost();
                        }
                        return 'error';
                    }
                }
                return 'missing';
            }

            // Check if subscription has expired
            if (subscription.expirationTime && subscription.expirationTime < Date.now()) {
                console.log('Push subscription expired, refreshing...');
                // Unsubscribe the old one
                try {
                    await subscription.unsubscribe();
                } catch (e) {
                    // Ignore unsubscribe errors for expired subscriptions
                }

                // Create a new subscription
                try {
                    await this.subscribe();
                    if (window.pwaHelper) {
                        window.pwaHelper.clearSubscriptionLost();
                    }
                    return 'refreshed';
                } catch (resubError) {
                    console.error('Failed to refresh expired subscription:', resubError);
                    if (window.pwaHelper) {
                        window.pwaHelper.markSubscriptionLost();
                    }
                    return 'error';
                }
            }

            // Subscription exists and is valid - ensure server has it
            // This handles cases where the server lost the subscription
            // (e.g., server received 410 Gone but client still has it)
            try {
                await this.sendSubscriptionToServer(subscription);
                this.subscription = subscription;
            } catch (syncError) {
                console.warn('Failed to sync subscription with server:', syncError);
                // Non-fatal - subscription may still work
            }

            return 'active';
        } catch (error) {
            console.error('Error validating subscription:', error);
            return 'error';
        }
    }

    /**
     * Sends subscription to the server
     * Uses the subscription's toJSON() method which provides keys in proper base64url format
     */
    async sendSubscriptionToServer(subscription) {
        // Use toJSON() which returns keys in the correct base64url encoding
        // Manual ArrayBuffer→base64 conversion can produce standard base64 with +/= chars
        // that cause silent push delivery failures on some Android devices
        const subJson = subscription.toJSON();
        
        const response = await fetch('/api/push/subscribe', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
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

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.error || 'Failed to subscribe');
        }

        return await response.json();
    }

    /**
     * Sends unsubscribe request to the server
     */
    async sendUnsubscribeToServer(endpoint) {
        const response = await fetch('/api/push/unsubscribe', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            credentials: 'include',
            body: JSON.stringify({
                endpoint: endpoint
            })
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.error || 'Failed to unsubscribe');
        }

        return await response.json();
    }

    /**
     * Converts URL-safe base64 string to Uint8Array
     */
    urlBase64ToUint8Array(base64String) {
        const paddingLength = (4 - base64String.length % 4) % 4;
        const padding = '='.repeat(paddingLength);
        const base64 = (base64String + padding)
            .replace(/\-/g, '+')
            .replace(/_/g, '/');

        const rawData = window.atob(base64);
        const outputArray = new Uint8Array(rawData.length);

        for (let i = 0; i < rawData.length; ++i) {
            outputArray[i] = rawData.charCodeAt(i);
        }
        return outputArray;
    }

    /**
     * Converts ArrayBuffer to base64url string (URL-safe, no padding)
     * This is the correct encoding for Web Push subscription keys
     */
    arrayBufferToBase64Url(buffer) {
        const bytes = new Uint8Array(buffer);
        let binary = '';
        for (let i = 0; i < bytes.byteLength; i++) {
            binary += String.fromCharCode(bytes[i]);
        }
        return window.btoa(binary)
            .replace(/\+/g, '-')
            .replace(/\//g, '_')
            .replace(/=+$/, '');
    }

    /**
     * Converts ArrayBuffer to standard base64 string
     * @deprecated Use arrayBufferToBase64Url or subscription.toJSON() instead
     */
    arrayBufferToBase64(buffer) {
        const bytes = new Uint8Array(buffer);
        let binary = '';
        for (let i = 0; i < bytes.byteLength; i++) {
            binary += String.fromCharCode(bytes[i]);
        }
        return window.btoa(binary);
    }

    /**
     * Starts a periodic health check that validates the push subscription
     * and re-syncs with the server if needed. This catches:
     * - Subscriptions silently revoked by the browser (common on iOS)
     * - Endpoint rotations that the pushsubscriptionchange event missed
     * - Server-side subscription records lost due to 410 Gone cleanup
     * Runs every 30 minutes while the page is visible
     */
    _startHealthCheck() {
        if (this._healthCheckTimer) return; // Already running
        
        const HEALTH_CHECK_INTERVAL = 30 * 60 * 1000; // 30 minutes
        
        const runCheck = async () => {
            try {
                // Only check when page is visible to save battery
                if (document.hidden) return;
                
                // Only check if we think we're subscribed
                if (Notification.permission !== 'granted') return;
                if (!this.registration || !this.registration.pushManager) return;
                
                const subscription = await this.registration.pushManager.getSubscription();
                
                if (!subscription) {
                    // If the user explicitly opted out, this is expected - do not
                    // silently resubscribe them on the periodic health check either.
                    if (window.pwaHelper && typeof window.pwaHelper.isOptedOut === 'function' && window.pwaHelper.isOptedOut()) {
                        console.log('[Push Health] User opted out, skipping recovery');
                        return;
                    }

                    // Subscription was silently lost!
                    console.warn('[Push Health] Subscription lost, attempting recovery...');
                    if (this.vapidPublicKey) {
                        try {
                            await this.subscribe();
                            console.log('[Push Health] Subscription recovered successfully');
                        } catch (e) {
                            console.error('[Push Health] Recovery failed:', e);
                            if (window.pwaHelper && typeof window.pwaHelper.markSubscriptionLost === 'function') {
                                window.pwaHelper.markSubscriptionLost();
                            }
                        }
                    }
                } else {
                    // Subscription exists, sync with server (handles server-side cleanup)
                    try {
                        await this.sendSubscriptionToServer(subscription);
                        this.subscription = subscription;
                    } catch (e) {
                        console.warn('[Push Health] Server sync failed:', e.message);
                    }
                }
            } catch (e) {
                console.warn('[Push Health] Check failed:', e);
            }
        };
        
        this._healthCheckTimer = setInterval(runCheck, HEALTH_CHECK_INTERVAL);
        
        // Also run on visibility change (when user returns to the app)
        // This is especially important for iOS which aggressively suspends PWAs
        document.addEventListener('visibilitychange', () => {
            if (!document.hidden) {
                // Small delay to let the page settle
                setTimeout(runCheck, 2000);
            }
        });
        
        // Run initial check after a short delay
        setTimeout(runCheck, 5000);
    }
}

// Export as global variable
window.PushNotificationsManager = PushNotificationsManager;
