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
        const navLinks = drawer.querySelectorAll('.nav-link:not(.dropdown-toggle), .dropdown-item');
        navLinks.forEach(link => {
            link.addEventListener('click', function () {
                // Small delay to allow navigation to start
                // Reduced delay matches Blazor's navigation timing better
                setTimeout(closeDrawer, 100);
            });
        });

        // Handle dropdown toggles in mobile drawer (custom in-place dropdowns)
        const dropdownToggles = drawer.querySelectorAll('[data-mobile-dropdown="true"]');
        dropdownToggles.forEach(toggle => {
            toggle.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();

                const currentItem = toggle.closest('.dropdown');
                if (!currentItem) {
                    return;
                }

                const currentMenu = currentItem.querySelector('.dropdown-menu');
                const isExpanded = toggle.getAttribute('aria-expanded') === 'true';

                drawer.querySelectorAll('.dropdown').forEach(item => {
                    const menu = item.querySelector('.dropdown-menu');
                    const trigger = item.querySelector('[data-mobile-dropdown="true"]');
                    if (menu && trigger && item !== currentItem) {
                        menu.classList.remove('show');
                        trigger.setAttribute('aria-expanded', 'false');
                    }
                });

                if (currentMenu) {
                    currentMenu.classList.toggle('show', !isExpanded);
                }
                toggle.setAttribute('aria-expanded', (!isExpanded).toString());
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
            drawer.querySelectorAll('.dropdown-menu.show').forEach(menu => menu.classList.remove('show'));
            drawer.querySelectorAll('[data-mobile-dropdown="true"]').forEach(toggle => {
                toggle.setAttribute('aria-expanded', 'false');
            });

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
