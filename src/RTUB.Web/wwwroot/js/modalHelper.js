/**
 * Modal Helper - Utilities for managing modals on mobile
 * Handles body scroll locking to prevent background scroll when modals are open
 */
window.modalHelper = {
    /**
     * Lock body scroll - prevents background scroll when modal is open
     * Critical for mobile to prevent double-scroll issues
     */
    lockBodyScroll: function() {
        // Only apply on mobile (max-width: 767px)
        if (window.innerWidth <= 767) {
            document.body.classList.add('no-scroll');
        }
    },
    
    /**
     * Unlock body scroll - restores normal scrolling when modal is closed
     */
    unlockBodyScroll: function() {
        document.body.classList.remove('no-scroll');
    },
    
    /**
     * Check if body scroll is currently locked
     * @returns {boolean} True if body has no-scroll class
     */
    isBodyScrollLocked: function() {
        return document.body.classList.contains('no-scroll');
    }
};
