// Scroll to top functionality for navigation
window.scrollToTop = {
    // Scroll to top with instant behavior
    scrollInstant: function() {
        window.scrollTo({ top: 0, behavior: 'instant' });
    },
    
    // Scroll to top with smooth behavior
    scrollSmooth: function() {
        window.scrollTo({ top: 0, behavior: 'smooth' });
    }
};
