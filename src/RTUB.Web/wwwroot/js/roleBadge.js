/**
 * Role Badge Mobile Interaction Handler
 * Provides tap-to-expand functionality for long role badges on mobile devices (≤480px)
 */

(function () {
    'use strict';

    // Check if viewport is mobile (≤480px)
    function isMobileView() {
        return window.innerWidth <= 480;
    }

    // Toggle badge expansion on mobile
    function toggleBadgeExpansion(badge) {
        if (!isMobileView()) {
            return; // Only works on mobile
        }

        const isExpanded = badge.classList.contains('expanded');
        
        // Collapse all other badges first
        document.querySelectorAll('.avatar-card-role-badge.expanded').forEach(otherBadge => {
            if (otherBadge !== badge) {
                otherBadge.classList.remove('expanded');
                otherBadge.setAttribute('aria-expanded', 'false');
            }
        });

        // Toggle current badge
        badge.classList.toggle('expanded');
        const nowExpanded = badge.classList.contains('expanded');
        badge.setAttribute('aria-expanded', nowExpanded.toString());
    }

    // Initialize badge click handlers
    function initializeBadgeHandlers() {
        document.querySelectorAll('.avatar-card-role-badge').forEach(badge => {
            // Set initial aria-expanded attribute
            if (!badge.hasAttribute('aria-expanded')) {
                badge.setAttribute('aria-expanded', 'false');
            }

            // Remove existing listeners to avoid duplicates
            badge.removeEventListener('click', handleBadgeClick);
            
            // Add click handler
            badge.addEventListener('click', handleBadgeClick);

            // Add keyboard support
            badge.addEventListener('keydown', handleBadgeKeydown);
        });
    }

    function handleBadgeClick(event) {
        // Prevent event from bubbling to parent card
        event.stopPropagation();
        toggleBadgeExpansion(event.currentTarget);
    }

    function handleBadgeKeydown(event) {
        // Support Enter and Space for accessibility
        if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault();
            event.stopPropagation();
            toggleBadgeExpansion(event.currentTarget);
        }
    }

    // Collapse badges when clicking outside on mobile
    function handleOutsideClick(event) {
        if (!isMobileView()) {
            return;
        }

        const clickedBadge = event.target.closest('.avatar-card-role-badge');
        if (!clickedBadge) {
            // Clicked outside any badge, collapse all
            document.querySelectorAll('.avatar-card-role-badge.expanded').forEach(badge => {
                badge.classList.remove('expanded');
                badge.setAttribute('aria-expanded', 'false');
            });
        }
    }

    // Reset badge states on window resize
    function handleResize() {
        if (!isMobileView()) {
            // Remove expanded class and reset aria-expanded on desktop
            document.querySelectorAll('.avatar-card-role-badge.expanded').forEach(badge => {
                badge.classList.remove('expanded');
                badge.setAttribute('aria-expanded', 'false');
            });
        }
    }

    // Initialize on DOM ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeBadgeHandlers);
    } else {
        initializeBadgeHandlers();
    }

    // Handle document clicks for outside click detection
    document.addEventListener('click', handleOutsideClick);

    // Handle window resize
    let resizeTimer;
    window.addEventListener('resize', function() {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(handleResize, 250);
    });

    // Export initialization function for Blazor to call after component updates
    window.RoleBadge = {
        initialize: initializeBadgeHandlers
    };
})();
