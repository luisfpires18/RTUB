// Push Notifications Manager
// Handles service worker registration, permission requests, and subscription management

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
     */
    async initialize() {
        try {
            // Check if push notifications are supported
            if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
                console.warn('Push notifications are not supported in this browser');
                return false;
            }

            // Check feature status from the server
            const status = await this.checkFeatureStatus();
            if (!status.isEnabled || !status.isConfigured) {
                console.log('Push notifications are not enabled or configured');
                return false;
            }

            this.vapidPublicKey = status.vapidPublicKey;
            this.isEnabled = status.isEnabled;
            this.isConfigured = status.isConfigured;

            // Register service worker
            await this.registerServiceWorker();

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
     * Registers the service worker
     */
    async registerServiceWorker() {
        try {
            this.registration = await navigator.serviceWorker.register('/service-worker.js');
            console.log('Service Worker registered successfully');

            // Wait for the service worker to be ready and active
            await navigator.serviceWorker.ready;
            
            // Ensure we have the active registration
            const registration = await navigator.serviceWorker.ready;
            this.registration = registration;
            
            console.log('Service Worker is ready and active');

            // Check if already subscribed
            this.subscription = await this.registration.pushManager.getSubscription();
            
            return this.registration;
        } catch (error) {
            console.error('Service Worker registration failed:', error);
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

            // Check if already subscribed
            let subscription = await this.registration.pushManager.getSubscription();

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
                    console.log('First few bytes:', Array.from(applicationServerKey.slice(0, 5)));
                    
                    // CRITICAL: Create the options object inline and pass directly to subscribe
                    // Some browsers may have issues if options are passed as a reference
                    console.log('Calling pushManager.subscribe with userVisibleOnly: true and applicationServerKey');
                    
                    subscription = await this.registration.pushManager.subscribe({
                        userVisibleOnly: true,
                        applicationServerKey: applicationServerKey
                    });
                } catch (subError) {
                    // Log detailed error information for debugging
                    console.error('PUSH SUBSCRIBE ERROR', subError, subError.name, subError.message);
                    throw new Error(`Push subscribe failed: ${subError.name}: ${subError.message}`);
                }
            }

            // Send subscription to server
            await this.sendSubscriptionToServer(subscription);

            this.subscription = subscription;
            console.log('Successfully subscribed to push notifications');
            
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
     * Sends subscription to the server
     */
    async sendSubscriptionToServer(subscription) {
        const response = await fetch('/api/push/subscribe', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            credentials: 'include',
            body: JSON.stringify({
                endpoint: subscription.endpoint,
                keys: {
                    p256dh: this.arrayBufferToBase64(subscription.getKey('p256dh')),
                    auth: this.arrayBufferToBase64(subscription.getKey('auth'))
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
     * Sends a test notification
     */
    async sendTestNotification(title = 'Test Notification', body = 'This is a test notification from RTUB') {
        const response = await fetch('/api/push/send-test', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            credentials: 'include',
            body: JSON.stringify({
                title: title,
                body: body,
                icon: '/icons/rtub-logo-192.png',
                url: '/'
            })
        });

        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.error || 'Failed to send test notification');
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
     * Converts ArrayBuffer to base64 string
     */
    arrayBufferToBase64(buffer) {
        const bytes = new Uint8Array(buffer);
        let binary = '';
        for (let i = 0; i < bytes.byteLength; i++) {
            binary += String.fromCharCode(bytes[i]);
        }
        return window.btoa(binary);
    }
}

// Export as global variable
window.PushNotificationsManager = PushNotificationsManager;
