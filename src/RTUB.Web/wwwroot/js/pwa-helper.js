// PWA Helper
// Utilities for detecting PWA mode and managing PWA-specific features

window.pwaHelper = {
    /**
     * Mobile breakpoint - matches Bootstrap's md breakpoint
     */
    MOBILE_BREAKPOINT: 768,

    /**
     * Checks if the app is running in PWA/standalone mode
     * Works for both iOS and Android
     */
    isPwaMode: function() {
        // Check if running in standalone mode (installed PWA)
        const isStandalone = window.matchMedia('(display-mode: standalone)').matches;
        
        // iOS Safari specific check
        const isIosStandalone = window.navigator.standalone === true;
        
        return isStandalone || isIosStandalone;
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
     * Returns true if app is in PWA mode and user hasn't been prompted
     */
    shouldShowPushPrompt: function() {
        return this.isPwaMode() && !this.hasBeenPrompted();
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

document.addEventListener('DOMContentLoaded', () => {
    if (window.pwaHelper) {
        window.pwaHelper.applyPwaModeClass();
    }
});

window.addEventListener('resize', () => {
    if (window.pwaHelper) {
        window.pwaHelper.applyPwaModeClass();
    }
});
