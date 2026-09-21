// Dynamic style bridge
//
// Two runtime values in RTUB cannot be expressed as a fixed CSS class: a
// progress bar's percentage and a Logistics label's user-picked colour. Both
// used to be written as markup inline style attributes, which a strict
// style-src blocks.
//
// CSP governs the style ATTRIBUTE and <style> ELEMENTS, not the CSSOM: writing
// el.style.setProperty(...) from script is explicitly not an inline-style
// violation. So the values travel as data-* attributes and are applied here as
// CSS custom properties.
//
// Deliberately NOT a generic "apply this CSS" sink — that would hand back the
// exact capability CSP removes. Each attribute accepts one narrowly validated
// shape and nothing else; anything that fails validation is ignored.

(function () {
    'use strict';

    var PCT = /^\d{1,3}(\.\d+)?$/;
    var HEX = /^#[0-9a-fA-F]{6}$/;

    var READERS = [
        {
            attr: 'data-fill-pct',
            prop: '--fill-pct',
            parse: function (v) {
                if (!PCT.test(v)) return null;
                var n = parseFloat(v);
                if (!isFinite(n)) return null;
                return Math.min(100, Math.max(0, n)) + '%';
            }
        },
        {
            attr: 'data-swatch',
            prop: '--swatch',
            parse: function (v) {
                return HEX.test(v) ? v : null;
            }
        }
    ];

    var SELECTOR = READERS.map(function (r) { return '[' + r.attr + ']'; }).join(',');

    function applyTo(el) {
        for (var i = 0; i < READERS.length; i++) {
            var r = READERS[i];
            if (!el.hasAttribute(r.attr)) continue;
            var raw = el.getAttribute(r.attr);
            var cacheKey = '__rtubDs_' + r.attr;
            if (el[cacheKey] === raw) continue;
            el[cacheKey] = raw;
            var value = r.parse(raw);
            if (value === null) {
                el.style.removeProperty(r.prop);
            } else {
                el.style.setProperty(r.prop, value);
            }
        }
    }

    function sweep() {
        var els = document.querySelectorAll(SELECTOR);
        for (var i = 0; i < els.length; i++) {
            applyTo(els[i]);
        }
    }

    var queued = false;
    function schedule() {
        if (queued) return;
        queued = true;
        requestAnimationFrame(function () {
            queued = false;
            sweep();
        });
    }

    function start() {
        sweep();
        // Blazor rewrites the DOM on every render, so re-apply after mutations.
        // attributeFilter keeps attribute churn cheap; the sweep is coalesced
        // into one pass per animation frame.
        new MutationObserver(schedule).observe(document.body, {
            childList: true,
            subtree: true,
            attributes: true,
            attributeFilter: READERS.map(function (r) { return r.attr; })
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start);
    } else {
        start();
    }
})();
