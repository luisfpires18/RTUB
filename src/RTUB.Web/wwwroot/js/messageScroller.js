window.messageScroller = {
    /**
     * Scroll to bottom of messages container - works reliably after DOM updates
     * @param {HTMLElement} element - The scrollable container element
     */
    scrollToBottom: function (element) {
        if (!element) return;

        // Use double requestAnimationFrame to ensure DOM is fully painted
        window.requestAnimationFrame(() => {
            window.requestAnimationFrame(() => {
                element.scrollTop = element.scrollHeight;
            });
        });
    },
    
    /**
     * Scroll to bottom with delay to ensure DOM is updated
     * @param {HTMLElement} element - The scrollable container element
     * @param {number} delayMs - Delay in milliseconds before scrolling
     */
    scrollToBottomDelayed: function (element, delayMs = 100) {
        if (!element) return;
        
        setTimeout(() => {
            window.requestAnimationFrame(() => {
                window.requestAnimationFrame(() => {
                    element.scrollTop = element.scrollHeight;
                });
            });
        }, delayMs);
    },
    
    /**
     * WeakMap to store event handlers for cleanup
     * @private
     */
    _focusHandlers: new WeakMap(),
    
    /**
     * Setup focus listener for mobile keyboard handling - scroll to bottom when keyboard opens
     * @param {HTMLElement} inputElement - The input/textarea element
     * @param {HTMLElement} containerElement - The scrollable container element
     */
    setupInputFocusScroll: function (inputElement, containerElement) {
        if (!inputElement || !containerElement) return;
        
        const KEYBOARD_ANIMATION_DELAY = 350; // ms - wait for mobile keyboard animation
        const KEYBOARD_RESIZE_DELAY = 500; // ms - wait for keyboard resize to complete
        
        // Remove existing listener if any to prevent duplicates
        const existingHandler = this._focusHandlers.get(inputElement);
        if (existingHandler) {
            inputElement.removeEventListener('focus', existingHandler);
            inputElement.removeEventListener('input', existingHandler.inputHandler);
            if (existingHandler.resizeHandler) {
                window.removeEventListener('resize', existingHandler.resizeHandler);
            }
        }
        
        // Scroll to bottom helper
        const scrollToBottom = () => {
            window.requestAnimationFrame(() => {
                window.requestAnimationFrame(() => {
                    containerElement.scrollTop = containerElement.scrollHeight;
                });
            });
        };
        
        // Create and store the handlers
        const focusHandler = () => {
            // On mobile, when keyboard opens, scroll to bottom after a delay
            if (window.innerWidth <= 767) {
                // Immediate scroll
                scrollToBottom();
                // Delayed scroll to handle keyboard animation
                setTimeout(scrollToBottom, KEYBOARD_ANIMATION_DELAY);
            }
        };
        
        // Also handle input events to keep scroll at bottom while typing
        const inputHandler = () => {
            if (window.innerWidth <= 767) {
                // Small delay to allow textarea to resize first
                setTimeout(scrollToBottom, 50);
            }
        };
        
        // Handle viewport resize (keyboard opening/closing) to keep messages visible
        const resizeHandler = () => {
            if (window.innerWidth <= 767 && document.activeElement === inputElement) {
                // Keyboard likely opened or closed - scroll to bottom after delay
                setTimeout(scrollToBottom, KEYBOARD_RESIZE_DELAY);
            }
        };
        
        // Store all handlers
        focusHandler.inputHandler = inputHandler;
        focusHandler.resizeHandler = resizeHandler;
        this._focusHandlers.set(inputElement, focusHandler);
        
        inputElement.addEventListener('focus', focusHandler);
        inputElement.addEventListener('input', inputHandler);
        window.addEventListener('resize', resizeHandler);
    }
};
