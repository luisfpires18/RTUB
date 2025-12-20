// PWA Helper
// Utilities for detecting PWA mode and managing PWA-specific features

window.pwaHelper = {
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
    }
};
