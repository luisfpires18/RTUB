/**
 * Homepage Helpers
 * Behaviour that belongs to the landing page only: the hero carousel and the
 * scroll-reveal pass over .portal-section elements.
 */
window.rtubHome = {
    /**
     * Starts the Bootstrap carousel on #homeCarousel.
     * No-op when the element is absent or Bootstrap has not loaded.
     */
    initCarousel: function () {
        const carouselElement = document.getElementById('homeCarousel');
        if (carouselElement && window.bootstrap) {
            const carousel = new bootstrap.Carousel(carouselElement, {
                ride: 'carousel',
                interval: 5000,
                pause: 'hover',
                wrap: true,
                touch: true,
                keyboard: true
            });
            carousel.cycle();
        }
    },

    /**
     * Marks already-visible portal sections as in-view and observes the rest,
     * revealing each one as it scrolls into range.
     */
    revealSections: function () {
        const sections = document.querySelectorAll('.portal-section');
        const revealObserver = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) {
                    entry.target.classList.add('in-view');
                    revealObserver.unobserve(entry.target);
                }
            });
        }, { threshold: 0.05, rootMargin: '0px 0px 80px 0px' });

        sections.forEach(function (section) {
            const rect = section.getBoundingClientRect();
            if (rect.top < window.innerHeight + 80 && rect.bottom > 0) {
                section.classList.add('in-view');
            } else {
                revealObserver.observe(section);
            }
        });
    }
};
