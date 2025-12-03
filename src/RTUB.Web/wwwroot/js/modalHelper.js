/**
 * Modal Helper - Utilities for managing modals
 * Handles body scroll locking to prevent background scroll when modals are open
 * Works across all devices (mobile, tablet, desktop) with device-specific behavior controlled by CSS
 */
window.modalHelper = {
    /**
     * Lock body scroll - prevents background scroll when modal is open
     * Critical for mobile to prevent double-scroll issues
     * CSS applies device-specific behavior:
     * - Mobile/Tablet: overflow: hidden + position: fixed
     * - Desktop: overflow: hidden only
     */
    lockBodyScroll: function() {
        document.body.classList.add('modal-open');
    },
    
    /**
     * Unlock body scroll - restores normal scrolling when modal is closed
     */
    unlockBodyScroll: function() {
        document.body.classList.remove('modal-open');
    },
    
    /**
     * Check if body scroll is currently locked
     * @returns {boolean} True if body has modal-open class
     */
    isBodyScrollLocked: function() {
        return document.body.classList.contains('modal-open');
    }
};
