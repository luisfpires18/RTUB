/**
 * Mobile Navigation Drawer
 * Handles the slide-in drawer for mobile navigation (from right side, ChatGPT-style)
 */

(function () {
    'use strict';

    // Initialize drawer when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initDrawer);
    } else {
        initDrawer();
    }

    function initDrawer() {
        const toggler = document.querySelector('.navbar-toggler');
        const drawer = document.querySelector('.mobile-nav-drawer');
        const backdrop = document.querySelector('.mobile-nav-backdrop');
        const closeBtn = document.querySelector('.mobile-nav-close');

        if (!toggler || !drawer || !backdrop) {
            console.warn('Mobile nav drawer elements not found');
            return;
        }

        // Open drawer
        toggler.addEventListener('click', function (e) {
            e.preventDefault();
            openDrawer();
        });

        // Close drawer - backdrop click
        backdrop.addEventListener('click', function (e) {
            if (e.target === backdrop) {
                closeDrawer();
            }
        });

        // Close drawer - close button
        if (closeBtn) {
            closeBtn.addEventListener('click', function (e) {
                e.preventDefault();
                closeDrawer();
            }); 
        }

        // Close drawer - ESC key
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && drawer.classList.contains('show')) {
                closeDrawer();
            }
        });

        // Close drawer when clicking nav links (optional, for better UX)
        const navLinks = drawer.querySelectorAll('.nav-link:not(.dropdown-toggle)');
        navLinks.forEach(link => {
            link.addEventListener('click', function () {
                // Small delay to allow navigation to start
                // Reduced delay matches Blazor's navigation timing better
                setTimeout(closeDrawer, 100);
            });
        });

        // Handle dropdown toggles in mobile drawer
        const dropdownToggles = drawer.querySelectorAll('.dropdown-toggle');
        dropdownToggles.forEach(toggle => {
            toggle.addEventListener('click', function (e) {
                // Let Bootstrap handle dropdown, but prevent drawer close
                e.stopPropagation();
            });
        });
    }

    function openDrawer() {
        const drawer = document.querySelector('.mobile-nav-drawer');
        const backdrop = document.querySelector('.mobile-nav-backdrop');
        const body = document.body;

        if (drawer && backdrop) {
            // Prevent body scroll
            body.style.overflow = 'hidden';
            
            // Show backdrop
            backdrop.classList.add('show');
            
            // Show drawer with slight delay for animation
            setTimeout(() => {
                drawer.classList.add('show');
            }, 10);

            // Update aria attributes
            const toggler = document.querySelector('.navbar-toggler');
            if (toggler) {
                toggler.setAttribute('aria-expanded', 'true');
            }
        }
    }

    function closeDrawer() {
        const drawer = document.querySelector('.mobile-nav-drawer');
        const backdrop = document.querySelector('.mobile-nav-backdrop');
        const body = document.body;

        if (drawer && backdrop) {
            // Hide drawer
            drawer.classList.remove('show');
            
            // Use transitionend event for more reliable timing
            // Fallback to timeout if event doesn't fire
            let transitionEnded = false;
            const handleTransitionEnd = () => {
                if (!transitionEnded) {
                    transitionEnded = true;
                    backdrop.classList.remove('show');
                    body.style.overflow = '';
                }
            };
            
            drawer.addEventListener('transitionend', handleTransitionEnd, { once: true });
            
            // Fallback timeout (slightly longer than CSS transition: 300ms)
            setTimeout(() => {
                handleTransitionEnd();
            }, 350);

            // Update aria attributes
            const toggler = document.querySelector('.navbar-toggler');
            if (toggler) {
                toggler.setAttribute('aria-expanded', 'false');
            }
        }
    }

    // Re-initialize on Blazor navigation (for SPA behavior)
    if (window.Blazor) {
        window.Blazor.addEventListener('enhancedload', initDrawer);
    }
})();
