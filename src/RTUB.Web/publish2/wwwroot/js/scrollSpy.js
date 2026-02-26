/**
 * Scroll Spy & Smooth Scroll Module for RTUB Landing Page
 * Uses IntersectionObserver for performant section tracking.
 * Reports the active section back to Blazor via DotNetObjectReference.
 */
window.rtubScrollSpy = {
    _observer: null,
    _dotNetRef: null,
    _sectionIds: [],
    _heroObserver: null,

    /**
     * Initialize the scroll spy on portal sections.
     * @param {object} dotNetRef - DotNetObjectReference for callbacks
     * @param {string[]} sectionIds - Array of section element IDs to observe
     */
    init: function (dotNetRef, sectionIds) {
        this.dispose(); // clean up any previous instance

        this._dotNetRef = dotNetRef;
        this._sectionIds = sectionIds;

        var visibleSections = {};

        this._observer = new IntersectionObserver(
            function (entries) {
                entries.forEach(function (entry) {
                    visibleSections[entry.target.id] = entry.intersectionRatio;
                });

                // Find the section with the highest visibility ratio
                var bestId = null;
                var bestRatio = 0;
                for (var i = 0; i < sectionIds.length; i++) {
                    var id = sectionIds[i];
                    var ratio = visibleSections[id] || 0;
                    if (ratio > bestRatio) {
                        bestRatio = ratio;
                        bestId = id;
                    }
                }

                if (bestId && dotNetRef) {
                    dotNetRef.invokeMethodAsync('OnActiveSectionChanged', bestId);
                }
            },
            {
                root: null,
                rootMargin: '-60px 0px -30% 0px',
                threshold: [0, 0.1, 0.25, 0.5, 0.75, 1.0]
            }
        );

        // Observe each section
        for (var i = 0; i < sectionIds.length; i++) {
            var el = document.getElementById(sectionIds[i]);
            if (el) {
                this._observer.observe(el);
            }
        }

        // Show/hide fixed nav bar based on hero visibility
        this._initHeroObserver();
    },

    /**
     * Toggle the fixed section nav visibility when the hero scrolls out of view.
     */
    _initHeroObserver: function () {
        var heroEl = document.querySelector('.homepage-hero');
        var navEl = document.querySelector('.portal-sticky-nav');
        if (!heroEl || !navEl) return;

        this._heroObserver = new IntersectionObserver(
            function (entries) {
                entries.forEach(function (entry) {
                    if (entry.isIntersecting) {
                        // Hero is visible — hide the fixed nav
                        navEl.classList.remove('visible');
                    } else {
                        // Hero scrolled away — show the fixed nav
                        navEl.classList.add('visible');
                    }
                });
            },
            { threshold: 0 }
        );

        this._heroObserver.observe(heroEl);
    },

    /**
     * Smooth-scroll to a section by ID.
     * @param {string} sectionId - The element ID to scroll to
     */
    scrollToSection: function (sectionId) {
        var el = document.getElementById(sectionId);
        if (el) {
            // Pre-reveal the target section so it's visible when we arrive
            el.classList.add('in-view');

            // Account for fixed nav heights
            var navEl = document.querySelector('.portal-sticky-nav');
            var navVisible = navEl && navEl.classList.contains('visible');
            var fixedNavHeight = navVisible ? navEl.offsetHeight : 0;
            var offset = fixedNavHeight + 16;

            var elementPosition = el.getBoundingClientRect().top + window.pageYOffset;
            window.scrollTo({
                top: elementPosition - offset,
                behavior: 'smooth'
            });
        }
    },

    /**
     * Clean up observer and references.
     */
    dispose: function () {
        if (this._observer) {
            this._observer.disconnect();
            this._observer = null;
        }
        if (this._heroObserver) {
            this._heroObserver.disconnect();
            this._heroObserver = null;
        }
        this._dotNetRef = null;
        this._sectionIds = [];
    }
};
