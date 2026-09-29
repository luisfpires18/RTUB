/**
 * Modal Helper - dialog focus, Escape and body scroll lock for the shared Modal component.
 *
 * Open dialogs form a stack (a dialog can open another one, e.g. a confirmation from an edit
 * form). Only the top dialog receives Tab and Escape. UI refactor 035; contract:
 * docs/design/RTUB_UI_REFACTOR.md section 22.
 *
 * lockBodyScroll/unlockBodyScroll remain for page code that locks the page itself (Messages);
 * the body stays locked while that lock or any dialog is active.
 *
 * UI refactor 037 (docs/design/RTUB_UI_REFACTOR.md section 24): leaving a page safely.
 * RTUB routes statically (per-page interactivity, enhanced navigation), so Back/Forward and link
 * clicks never reach an interactive NavigationLock. This file handles them, in the capture phase,
 * before enhanced navigation runs:
 * - Back/Forward with a dialog open: the navigation is stopped, the dialog page's URL is put back
 *   with one pushState (no extra entries are added ahead of time) and the top dialog is asked to
 *   close, as Escape does - a dirty one asks "Descartar alterações?" first.
 * - Back/Forward or an in-app link while a form reports unsaved changes: the same stop, then the
 *   form's guard asks; "Descartar" continues to where the user was going.
 * Reload, tab close and external links use the browser's own prompt (NavigationLock); the
 * service-worker update toast holds its reload while hasUnsavedChanges().
 */
window.modalHelper = (function () {
    'use strict';

    var dialogs = [];          // { id, ref, closeOnEscape, opener }
    var externalLock = false;  // lockBodyScroll/unlockBodyScroll callers
    var lockedScrollY = null;  // page position kept while the body is fixed (phones/tablets)
    var guards = {};           // unsaved-change guard id -> { ref, dirty, url }
    var allowNextPop = false;  // our own history.back() after the user chose to discard

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

    function dirtyGuard() {
        for (var id in guards) {
            if (guards[id].dirty) return guards[id];
        }
        return null;
    }

    function ask(guard) {
        return guard.ref.invokeMethodAsync('ConfirmLeave').catch(function () { return true; /* circuit gone */ });
    }

    window.addEventListener('popstate', function (e) {
        if (allowNextPop) { allowNextPop = false; return; }

        var current = top();
        var guard = current ? null : dirtyGuard();
        if (!current && !guard) return;

        // Keep enhanced navigation from rendering the other page and restore this page's URL.
        e.stopImmediatePropagation();
        history.pushState(null, '', current ? current.url : guard.url);

        if (current) {
            current.ref.invokeMethodAsync('HandleBack').catch(function () { /* circuit gone */ });
        } else {
            ask(guard).then(function (leave) {
                if (leave) { allowNextPop = true; history.back(); }
            });
        }
    }, true);

    document.addEventListener('click', function (e) {
        if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
        var link = e.target.closest && e.target.closest('a[href]');
        if (!link || link.hasAttribute('download') || (link.target && link.target !== '_self')) return;
        var url = new URL(link.href, location.href);
        if (url.origin !== location.origin) return; // leaving the app: the browser's own prompt
        if (url.pathname === location.pathname && url.search === location.search && url.hash) return;

        var guard = dirtyGuard();
        if (!guard) return;
        e.preventDefault();
        e.stopImmediatePropagation();
        ask(guard).then(function (leave) {
            if (leave) Blazor.navigateTo(url.href);
        });
    }, true);

    // Where focus goes when the element that opened a dialog is gone (e.g. the card of the item
    // just deleted): the page heading, else <main> - never <body>.
    function fallbackFocus() {
        var candidates = [document.querySelector('main h1'), document.querySelector('main')];
        for (var i = 0; i < candidates.length; i++) {
            var el = candidates[i];
            if (!el) continue;
            if (!el.hasAttribute('tabindex') && !el.matches(FOCUSABLE)) el.setAttribute('tabindex', '-1');
            el.focus({ preventScroll: true });
            return;
        }
    }

    return {
        openDialog: function (id, dotNetRef, closeOnEscape) {
            if (dialogs.some(function (d) { return d.id === id; })) return;

            dialogs.push({
                id: id,
                ref: dotNetRef,
                closeOnEscape: !!closeOnEscape,
                opener: document.activeElement,
                url: location.href
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

            // Back to the control that opened it, if it is still on the page; otherwise to a
            // predictable place instead of <body>. A dialog opened from a dialog returns into it.
            var opener = entry.opener;
            if (opener && opener !== document.body && opener.isConnected && typeof opener.focus === 'function') {
                opener.focus({ preventScroll: true });
            } else if (dialogs.length) {
                var below = document.getElementById(top().id);
                if (below) below.focus({ preventScroll: true });
            } else if (opener && opener !== document.body) {
                fallbackFocus();
            }
        },

        /** An UnsavedChangesGuard (dotNetRef answers ConfirmLeave) and whether it is dirty now. */
        setUnsavedChanges: function (id, dotNetRef, dirty) {
            var guard = guards[id] || (guards[id] = { ref: dotNetRef, dirty: false, url: location.href });
            if (dirty && !guard.dirty) guard.url = location.href;
            guard.dirty = !!dirty;
        },

        removeGuard: function (id) {
            delete guards[id];
        },

        hasUnsavedChanges: function () {
            return !!dirtyGuard();
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
