/**
 * Browser Notifications API Integration
 * Handles browser push notifications for RTUB events
 */

window.browserNotifications = {
    /**
     * Check if browser supports notifications
     */
    isSupported: function () {
        return 'Notification' in window;
    },

    /**
     * Get current notification permission status
     * @returns {string} 'granted', 'denied', or 'default'
     */
    getPermissionStatus: function () {
        if (!this.isSupported()) {
            return 'denied';
        }
        return Notification.permission;
    },

    /**
     * Request notification permission from the user
     * @returns {Promise<string>} Permission status after request
     */
    requestPermission: async function () {
        if (!this.isSupported()) {
            return 'denied';
        }

        try {
            const permission = await Notification.requestPermission();
            return permission;
        } catch (error) {
            console.error('Error requesting notification permission:', error);
            return 'denied';
        }
    },

    /**
     * Show a browser notification
     * @param {string} title - Notification title
     * @param {string} body - Notification body text
     * @param {string} clickUrl - URL to navigate to when notification is clicked
     * @param {string} icon - Optional icon URL
     * @returns {boolean} True if notification was shown
     */
    show: function (title, body, clickUrl, icon) {
        if (!this.isSupported()) {
            console.warn('Browser notifications are not supported');
            return false;
        }

        if (Notification.permission !== 'granted') {
            console.warn('Notification permission not granted');
            return false;
        }

        try {
            const options = {
                body: body,
                icon: icon || '/favicon.ico',
                badge: '/favicon.ico',
                tag: 'rtub-event',
                requireInteraction: false,
                silent: false
            };

            const notification = new Notification(title, options);

            // Handle notification click
            notification.onclick = function (event) {
                event.preventDefault();
                window.focus();
                if (clickUrl) {
                    window.location.href = clickUrl;
                }
                notification.close();
            };

            // Auto-close after 10 seconds
            setTimeout(() => {
                notification.close();
            }, 10000);

            return true;
        } catch (error) {
            console.error('Error showing notification:', error);
            return false;
        }
    },

    /**
     * Show a notification for a new event
     * @param {string} eventName - Name of the event
     * @param {string} eventDate - Formatted date string
     * @param {string} eventId - Event ID for constructing the URL
     * @returns {boolean} True if notification was shown
     */
    showEventNotification: function (eventName, eventDate, eventId) {
        const title = 'Nova atuação criada';
        const body = `${eventName}\n${eventDate}`;
        const clickUrl = '/events';
        
        return this.show(title, body, clickUrl);
    }
};
