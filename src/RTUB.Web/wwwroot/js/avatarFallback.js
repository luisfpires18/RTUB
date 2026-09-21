// Avatar image fallback
//
// Replaces the inline onerror="this.onerror=null; this.src='...'" attributes that
// used to sit on every avatar <img>. A strict CSP script-src blocks inline event
// handler attributes, so the same behaviour is delivered by one delegated listener.
//
// Mark an <img> with data-avatar-fallback to opt in.
//
// The 'error' event does not bubble, so the listener is registered in the capture
// phase. This file is loaded from <head> in App.razor so the listener is attached
// before any avatar element is parsed.
(function () {
    'use strict';

    var FALLBACK_SRC = '/images/default-avatar.webp';

    document.addEventListener('error', function (e) {
        var img = e.target;
        if (!img || img.tagName !== 'IMG' || !img.hasAttribute('data-avatar-fallback')) return;

        // Mirrors the old `this.onerror = null` guard: if the fallback image itself
        // fails to load, do not retry and spin forever.
        img.removeAttribute('data-avatar-fallback');
        img.src = FALLBACK_SRC;
    }, true);
})();
