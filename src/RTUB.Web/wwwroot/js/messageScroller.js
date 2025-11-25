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
    }
};
