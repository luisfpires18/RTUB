(function () {
    const eventType = 'rtub:push-received';
    let isRegistered = false;
    let storedDotNetRef = null;

    function attachHandler(dotNetRef) {
        if (!('serviceWorker' in navigator)) {
            return;
        }

        const handleMessage = (event) => {
            if (!event?.data || event.data.type !== eventType) {
                return;
            }

            if (!dotNetRef) {
                return;
            }

            dotNetRef.invokeMethodAsync('RefreshUnreadMessages')
                .catch(() => {
                    // Silently ignore failures to avoid breaking the UI if the circuit is gone
                });
        };

        navigator.serviceWorker.addEventListener('message', handleMessage);
    }

    window.rtubUnreadMessages = {
        register(dotNetRef) {
            if (isRegistered || !dotNetRef) {
                return;
            }

            isRegistered = true;
            storedDotNetRef = dotNetRef;

            if (navigator.serviceWorker?.ready) {
                navigator.serviceWorker.ready.then(() => attachHandler(dotNetRef));
            } else {
                attachHandler(dotNetRef);
            }
        },

        // Called from Inbox page when messages are marked as read
        refresh() {
            if (storedDotNetRef) {
                storedDotNetRef.invokeMethodAsync('RefreshUnreadMessages')
                    .catch(() => {
                        // Silently ignore failures
                    });
            }
        }
    };
})();
