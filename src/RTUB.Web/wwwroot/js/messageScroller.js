window.messageScroller = {
    scrollToBottom: function (element) {
        if (!element) return;

        window.requestAnimationFrame(() => {
            element.scrollTop = element.scrollHeight;
        });
    },
    
    // Scroll to bottom with delay to ensure DOM is updated
    scrollToBottomDelayed: function (element, delayMs = 100) {
        if (!element) return;
        
        setTimeout(() => {
            window.requestAnimationFrame(() => {
                element.scrollTop = element.scrollHeight;
            });
        }, delayMs);
    },
    
    // Setup focus listener for mobile keyboard handling
    setupInputFocusScroll: function (inputElement, containerElement) {
        if (!inputElement || !containerElement) return;
        
        inputElement.addEventListener('focus', () => {
            // On mobile, when keyboard opens, scroll to bottom
            if (window.innerWidth <= 767) {
                setTimeout(() => {
                    window.requestAnimationFrame(() => {
                        containerElement.scrollTop = containerElement.scrollHeight;
                    });
                }, 300); // Wait for keyboard animation
            }
        });
    },
    
    // Setup viewport height tracking for mobile keyboard
    setupViewportHeight: function () {
        // Function to update CSS variable with actual viewport height
        const updateViewportHeight = () => {
            // Use visualViewport if available (better for keyboard handling)
            const vh = window.visualViewport 
                ? window.visualViewport.height 
                : window.innerHeight;
            document.documentElement.style.setProperty('--vh', `${vh * 0.01}px`);
        };
        
        // Update on load
        updateViewportHeight();
        
        // Update on resize
        window.addEventListener('resize', updateViewportHeight);
        
        // Update on visualViewport resize (when keyboard opens/closes)
        if (window.visualViewport) {
            window.visualViewport.addEventListener('resize', updateViewportHeight);
        }
    }
};
