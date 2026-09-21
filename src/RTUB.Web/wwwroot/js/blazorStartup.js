// Blazor circuit startup
//
// Moved verbatim out of the inline <script> block that used to close MainLayout.razor,
// so that a strict CSP script-src does not need 'unsafe-inline'.
//
// ORDERING CONTRACT: blazor.web.js is loaded with autostart="false" and defines the
// global `Blazor` synchronously. This file is a classic (non-defer, non-async) script
// referenced AFTER it in MainLayout.razor, so `Blazor` is guaranteed to exist here.
// It must stay the only caller of Blazor.start() - a second call throws.
(function () {
    'use strict';

    // Configure Blazor to use absolute path for SignalR hub
    // This prevents 404 errors when on /admin/* or other sub-paths
    // Server timeout and keep-alive match server-side HubOptions for stable stage farming sessions
    Blazor.start({
        circuit: {
            configureSignalR: function (builder) {
                builder.withUrl("/_blazor");
                builder.withServerTimeout(300000);
                builder.withKeepAliveInterval(15000);
            }
        }
    });

    // Reload button inside ReconnectModal. This cannot be a Blazor @onclick handler:
    // the modal is only visible once the circuit is already down, so no server-side
    // event can be dispatched. Delegated because the modal markup is rendered by Blazor.
    document.addEventListener('click', function (e) {
        if (e.target.closest('.reconnect-reload')) {
            location.reload();
        }
    });
})();
