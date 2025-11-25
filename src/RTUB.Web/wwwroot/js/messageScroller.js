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
    
    // Setup focus listener for mobile keyboard handling - scroll to bottom when keyboard opens
    setupInputFocusScroll: function (inputElement, containerElement) {
        if (!inputElement || !containerElement) return;
        
        const KEYBOARD_ANIMATION_DELAY = 350; // ms - wait for mobile keyboard animation
        
        inputElement.addEventListener('focus', () => {
            // On mobile, when keyboard opens, scroll to bottom after a delay
            if (window.innerWidth <= 767) {
                setTimeout(() => {
                    window.requestAnimationFrame(() => {
                        containerElement.scrollTop = containerElement.scrollHeight;
                    });
                }, KEYBOARD_ANIMATION_DELAY);
            }
        });
    }
};
