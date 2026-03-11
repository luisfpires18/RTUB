// OPTIMIZATION: Lazy-loaded module for unread messages badge functionality
// Only loads when user is authenticated and badge component is rendered
(function () {
    'use strict';
    
    const eventType = 'rtub:push-received';
    let isRegistered = false;
    let storedDotNetRef = null;
    let messageHandler = null;

    function attachHandler(dotNetRef) {
        // OPTIMIZATION: Check service worker support early
        if (!('serviceWorker' in navigator)) {
            console.warn('[UnreadMessages] Service Worker not supported');
            return;
        }

        // OPTIMIZATION: Store handler reference for potential cleanup
        messageHandler = (event) => {
            if (!event?.data || event.data.type !== eventType) {
                return;
            }

            if (!dotNetRef) {
                return;
            }

            dotNetRef.invokeMethodAsync('RefreshUnreadMessages')
                .catch((error) => {
                    // Silently ignore failures to avoid breaking the UI if the circuit is gone
                    console.debug('[UnreadMessages] Failed to refresh:', error);
                });
        };

        navigator.serviceWorker.addEventListener('message', messageHandler);
    }

    window.rtubUnreadMessages = {
        register(dotNetRef) {
            // OPTIMIZATION: Prevent duplicate registrations
            if (isRegistered || !dotNetRef) {
                return;
            }

            isRegistered = true;
            storedDotNetRef = dotNetRef;

            if (navigator.serviceWorker?.ready) {
                navigator.serviceWorker.ready
                    .then(() => attachHandler(dotNetRef))
                    .catch((error) => {
                        console.error('[UnreadMessages] Service Worker registration failed:', error);
                    });
            } else {
                attachHandler(dotNetRef);
            }
        },

        // Called from Inbox page when messages are marked as read
        refresh() {
            if (storedDotNetRef) {
                storedDotNetRef.invokeMethodAsync('RefreshUnreadMessages')
                    .catch((error) => {
                        // Silently ignore failures
                        console.debug('[UnreadMessages] Failed to refresh:', error);
                    });
            }
        },
        
        // OPTIMIZATION: Cleanup method for disposal
        dispose() {
            if (messageHandler && navigator.serviceWorker) {
                navigator.serviceWorker.removeEventListener('message', messageHandler);
            }
            isRegistered = false;
            storedDotNetRef = null;
            messageHandler = null;
        },

        /**
         * Clears message notifications from the notification tray for a specific conversation.
         * This is called when a user reads messages in a conversation (from Inbox or Profile).
         * Works in both PWA and browser mode.
         * @param {number|string} conversationId - The conversation ID to clear notifications for
         */
        async clearNotification(conversationId) {
            // Validate conversationId
            if (conversationId === null || conversationId === undefined || conversationId === '') {
                console.debug('[UnreadMessages] Invalid conversationId:', conversationId);
                return;
            }

            // Check if the Notification API is available
            if (!('Notification' in window)) {
                console.debug('[UnreadMessages] Notification API not supported');
                return;
            }

            // Check if service worker is available
            if (!('serviceWorker' in navigator)) {
                console.debug('[UnreadMessages] Service Worker not supported');
                return;
            }

            try {
                const registration = await navigator.serviceWorker.ready;
                
                // Get all notifications from this service worker
                const notifications = await registration.getNotifications();
                
                // Close notifications that match the message tag for this conversation
                const tagToClose = `message-${conversationId}`;
                
                for (const notification of notifications) {
                    if (this._matchesTag(notification, tagToClose)) {
                        notification.close();
                        console.debug('[UnreadMessages] Cleared notification for conversation:', conversationId);
                    }
                }
            } catch (error) {
                console.debug('[UnreadMessages] Failed to clear notification:', error);
            }
        },

        /**
         * Clears all message notifications from the notification tray.
         * This is useful when the user views the messages page.
         */
        async clearAllMessageNotifications() {
            // Check if the Notification API is available
            if (!('Notification' in window)) {
                console.debug('[UnreadMessages] Notification API not supported');
                return;
            }

            // Check if service worker is available
            if (!('serviceWorker' in navigator)) {
                console.debug('[UnreadMessages] Service Worker not supported');
                return;
            }

            try {
                const registration = await navigator.serviceWorker.ready;
                
                // Get all notifications from this service worker
                const notifications = await registration.getNotifications();
                
                // Close all notifications that start with "message-"
                for (const notification of notifications) {
                    if (this._matchesTagPrefix(notification, 'message-')) {
                        notification.close();
                    }
                }
                console.debug('[UnreadMessages] Cleared all message notifications');
            } catch (error) {
                console.debug('[UnreadMessages] Failed to clear all message notifications:', error);
            }
        },

        /**
         * Helper function to check if a notification matches a specific tag
         * @param {Notification} notification - The notification to check
         * @param {string} tag - The tag to match
         * @returns {boolean} True if the notification matches the tag
         */
        _matchesTag(notification, tag) {
            return notification.tag === tag ||
                   (notification.data && notification.data.tag === tag) ||
                   (notification.data && notification.data.baseTag === tag);
        },

        /**
         * Helper function to check if a notification's tag starts with a prefix
         * @param {Notification} notification - The notification to check
         * @param {string} prefix - The tag prefix to match
         * @returns {boolean} True if the notification's tag starts with the prefix
         */
        _matchesTagPrefix(notification, prefix) {
            return (notification.tag && notification.tag.startsWith(prefix)) ||
                   (notification.data && notification.data.tag && notification.data.tag.startsWith(prefix)) ||
                   (notification.data && notification.data.baseTag && notification.data.baseTag.startsWith(prefix));
        }
    };
})();
