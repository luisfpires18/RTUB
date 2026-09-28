/**
 * Modal Helper - dialog focus, Escape and body scroll lock for the shared Modal component.
 *
 * Open dialogs form a stack (a dialog can open another one, e.g. a confirmation from an edit
 * form). Only the top dialog receives Tab and Escape. UI refactor 035; contract:
 * docs/design/RTUB_UI_REFACTOR.md section 22.
 *
 * lockBodyScroll/unlockBodyScroll remain for page code that locks the page itself (Messages);
 * the body stays locked while that lock or any dialog is active.
 */
window.modalHelper = (function () {
    'use strict';

    var dialogs = [];          // { id, ref, closeOnEscape, opener }
    var externalLock = false;  // lockBodyScroll/unlockBodyScroll callers
    var lockedScrollY = null;  // page position kept while the body is fixed (phones/tablets)

    var FOCUSABLE = 'a[href], area[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), ' +
        'select:not([disabled]), textarea:not([disabled]), iframe, [contenteditable="true"], ' +
        '[tabindex]:not([tabindex="-1"])';

    function applyLock() {
        var body = document.body;
        var shouldLock = externalLock || dialogs.length > 0;

        if (shouldLock && lockedScrollY === null) {
            // Below 1025px the lock is `position: fixed` (modals.css), which on its own would jump
            // the page to the top; offset the body so the page stays where the user left it.
            lockedScrollY = window.scrollY;
            body.style.top = '-' + lockedScrollY + 'px';
            body.classList.add('modal-open');
        } else if (!shouldLock && lockedScrollY !== null) {
            var y = lockedScrollY;
            lockedScrollY = null;
            body.classList.remove('modal-open');
            body.style.top = '';
            window.scrollTo({ top: y, left: 0, behavior: 'instant' });
        }
    }

    function top() {
        return dialogs.length ? dialogs[dialogs.length - 1] : null;
    }

    function focusables(root) {
        return Array.prototype.filter.call(root.querySelectorAll(FOCUSABLE), function (el) {
            return el.getClientRects().length > 0 && !el.closest('[inert]');
        });
    }

    // Tab and Shift+Tab stay inside the top dialog.
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Tab') return;
        var current = top();
        var root = current && document.getElementById(current.id);
        if (!root) return;

        var items = focusables(root);
        if (!items.length) {
            e.preventDefault();
            root.focus();
            return;
        }

        var first = items[0];
        var last = items[items.length - 1];
        var active = document.activeElement;
        var outside = !root.contains(active);

        if (e.shiftKey && (active === first || active === root || outside)) {
            e.preventDefault();
            last.focus();
        } else if (!e.shiftKey && (active === last || outside)) {
            e.preventDefault();
            first.focus();
        }
    }, true);

    // Escape closes the top dialog when it is dismissible. Listened for on window, after
    // document-level handlers, so a widget inside the dialog that handles Escape itself (and
    // prevents the default) keeps it - e.g. an open dropdown closes first.
    window.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape' || e.defaultPrevented) return;
        var current = top();
        if (!current || !current.closeOnEscape) return;
        e.preventDefault();
        current.ref.invokeMethodAsync('HandleEscape').catch(function () { /* circuit gone */ });
    });

    return {
        openDialog: function (id, dotNetRef, closeOnEscape) {
            if (dialogs.some(function (d) { return d.id === id; })) return;

            dialogs.push({
                id: id,
                ref: dotNetRef,
                closeOnEscape: !!closeOnEscape,
                opener: document.activeElement
            });
            applyLock();

            var root = document.getElementById(id);
            if (!root) return;
            var target = root.querySelector('[data-autofocus]') || root;
            target.focus({ preventScroll: true });
        },

        closeDialog: function (id) {
            var index = -1;
            for (var i = 0; i < dialogs.length; i++) {
                if (dialogs[i].id === id) { index = i; break; }
            }
            if (index < 0) return;

            var entry = dialogs.splice(index, 1)[0];
            applyLock();

            // Back to the control that opened it, if it is still on the page.
            var opener = entry.opener;
            if (opener && opener !== document.body && opener.isConnected && typeof opener.focus === 'function') {
                opener.focus({ preventScroll: true });
            }
        },

        lockBodyScroll: function () {
            externalLock = true;
            applyLock();
        },

        unlockBodyScroll: function () {
            externalLock = false;
            applyLock();
        },

        isBodyScrollLocked: function () {
            return document.body.classList.contains('modal-open');
        }
    };
})();
