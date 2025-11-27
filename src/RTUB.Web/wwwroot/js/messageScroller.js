window.messageScroller = {
    /**
     * Mobile breakpoint - matches CSS @media (max-width: 767px)
     * @private
     */
    _MOBILE_BREAKPOINT: 767,
    
    /**
     * Track if Visual Viewport handler is set up
     * @private
     */
    _viewportHandlerSetup: false,
    
    /**
     * Store reference to the thread panel element for viewport resizing
     * @private
     */
    _threadPanelElement: null,
    
    /**
     * Store reference to input element for focus handling
     * @private
     */
    _inputElement: null,
    
    /**
     * Setup Visual Viewport resize handler to keep thread panel sized correctly
     * when the mobile keyboard opens/closes.
     * 
     * On iOS Safari, when the keyboard opens:
     * - The Visual Viewport shrinks but the Layout Viewport doesn't
     * - position: fixed elements use the Layout Viewport
     * - This causes the fixed container to extend behind the keyboard
     * - The header can scroll out of view as the page adjusts
     * 
     * Solution: Resize the fixed container to match the Visual Viewport height
     */
    setupViewportResize: function() {
        if (this._viewportHandlerSetup || !window.visualViewport) return;
        
        const self = this;
        
        const updateViewportHeight = () => {
            // Only apply on mobile
            if (window.innerWidth > self._MOBILE_BREAKPOINT) return;
            
            // Use cached element or find it (cache for performance on frequent events)
            if (!self._threadPanelElement || !document.contains(self._threadPanelElement)) {
                self._threadPanelElement = document.querySelector('.rtub-messages__thread-panel');
            }
            
            const threadPanel = self._threadPanelElement;
            if (!threadPanel) return;
            
            // Get the visual viewport height (accounts for keyboard)
            const viewportHeight = window.visualViewport.height;
            const viewportOffsetTop = window.visualViewport.offsetTop;
            
            // Set the thread panel height to match visual viewport
            // and position it at the visual viewport offset
            threadPanel.style.height = viewportHeight + 'px';
            threadPanel.style.top = viewportOffsetTop + 'px';
            threadPanel.style.bottom = 'auto';
        };
        
        // Initial setup - set viewport size immediately
        updateViewportHeight();
        
        // Listen for Visual Viewport resize (keyboard open/close)
        window.visualViewport.addEventListener('resize', updateViewportHeight);
        window.visualViewport.addEventListener('scroll', updateViewportHeight);
        
        // Also handle focus events on message input to proactively resize
        // BEFORE the keyboard animation starts - this prevents the visual glitch
        const handleInputFocus = () => {
            if (window.innerWidth > self._MOBILE_BREAKPOINT) return;
            
            // Immediately set the current viewport dimensions
            updateViewportHeight();
            
            // Also update multiple times during keyboard animation to stay in sync
            // Keyboard animation typically takes 250-350ms on iOS
            setTimeout(updateViewportHeight, 50);
            setTimeout(updateViewportHeight, 100);
            setTimeout(updateViewportHeight, 200);
            setTimeout(updateViewportHeight, 350);
        };
        
        // Find and attach to message input
        const attachInputFocusHandler = () => {
            const input = document.querySelector('.rtub-messages__composer-input');
            if (input && input !== self._inputElement) {
                if (self._inputElement) {
                    self._inputElement.removeEventListener('focus', handleInputFocus);
                }
                self._inputElement = input;
                input.addEventListener('focus', handleInputFocus);
            }
        };
        
        // Initial attachment
        attachInputFocusHandler();
        
        // Re-attach when DOM changes (e.g., navigating between conversations)
        const observer = new MutationObserver(attachInputFocusHandler);
        observer.observe(document.body, { childList: true, subtree: true });
        
        this._viewportHandlerSetup = true;
    },
    
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
     * Scroll to bottom only if user is near the end of the conversation.
     * This prevents jarring scroll jumps when user is reading older messages.
     * @param {HTMLElement} element - The scrollable container element
     * @param {number} threshold - Distance from bottom (in pixels) to consider "near end" (default: 100)
     */
    scrollToBottomIfNearEnd: function (element, threshold) {
        if (!element) return;
        
        // Default threshold to 100px if not provided
        var pixelThreshold = threshold || 100;
        
        // Check if user is near the bottom (within threshold)
        var isNearBottom = (element.scrollHeight - (element.scrollTop + element.clientHeight)) <= pixelThreshold;
        
        if (isNearBottom) {
            window.requestAnimationFrame(() => {
                window.requestAnimationFrame(() => {
                    element.scrollTop = element.scrollHeight;
                });
            });
        }
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
     * This ensures messages stay visible when the virtual keyboard appears.
     * 
     * IMPORTANT: On iOS (both browser and PWA), when the keyboard opens:
     * - The viewport shrinks or the page scrolls
     * - Messages at the bottom may become hidden
     * - This handler scrolls the message container to keep latest messages visible
     * 
     * @param {HTMLElement} inputElement - The input/textarea element
     * @param {HTMLElement} containerElement - The scrollable container element
     */
    setupInputFocusScroll: function (inputElement, containerElement) {
        // Setup viewport resize handler for iOS keyboard handling
        this.setupViewportResize();
        
        // Store reference to this for use in nested functions
        const MOBILE_BREAKPOINT = this._MOBILE_BREAKPOINT;
        
        // Validate that both parameters are actual DOM elements
        // Blazor's ElementReference may pass objects that aren't valid DOM elements
        if (!inputElement || !containerElement) return;
        if (!(inputElement instanceof HTMLElement) || !(containerElement instanceof HTMLElement)) return;
        if (typeof inputElement.addEventListener !== 'function') return;
        
        const KEYBOARD_ANIMATION_DELAY = 350; // ms - wait for mobile keyboard animation
        const KEYBOARD_RESIZE_DELAY = 500; // ms - wait for keyboard resize to complete
        // PWA mode on iOS can have delayed viewport changes after keyboard opens
        // 800ms covers the full animation cycle observed on iOS 15+ PWA standalone mode
        const EXTRA_SCROLL_DELAY = 800;
        
        // Debounce timer to prevent excessive scroll calls
        let scrollDebounceTimer = null;
        
        // Remove existing listener if any to prevent duplicates
        const existingHandler = this._focusHandlers.get(inputElement);
        if (existingHandler) {
            inputElement.removeEventListener('focus', existingHandler);
            inputElement.removeEventListener('input', existingHandler.inputHandler);
            if (existingHandler.resizeHandler) {
                window.removeEventListener('resize', existingHandler.resizeHandler);
            }
            if (existingHandler.visualViewportHandler) {
                window.visualViewport?.removeEventListener('resize', existingHandler.visualViewportHandler);
            }
        }
        
        // Scroll to bottom helper - aggressively scrolls multiple times
        const scrollToBottom = () => {
            window.requestAnimationFrame(() => {
                window.requestAnimationFrame(() => {
                    containerElement.scrollTop = containerElement.scrollHeight;
                });
            });
        };
        
        // Debounced scroll with multiple attempts at key intervals
        // Uses a single timer chain instead of multiple parallel timers
        const scrollMultipleTimes = () => {
            // Clear any existing debounce timer
            if (scrollDebounceTimer) {
                clearTimeout(scrollDebounceTimer);
            }
            
            // Immediate scroll
            scrollToBottom();
            
            // Chain of delayed scrolls at key intervals:
            // 0ms (immediate) -> 350ms -> 500ms -> 800ms
            scrollDebounceTimer = setTimeout(() => {
                scrollToBottom(); // 350ms
                setTimeout(() => {
                    scrollToBottom(); // 500ms (350 + 150)
                    setTimeout(scrollToBottom, 300); // 800ms (500 + 300)
                }, 150);
            }, KEYBOARD_ANIMATION_DELAY);
        };
        
        // Create and store the handlers
        const focusHandler = () => {
            // On mobile, when keyboard opens, scroll to bottom multiple times
            // to ensure messages stay visible even with small chat histories
            if (window.innerWidth <= MOBILE_BREAKPOINT) {
                scrollMultipleTimes();
            }
        };
        
        // Also handle input events to keep scroll at bottom while typing
        const inputHandler = () => {
            if (window.innerWidth <= MOBILE_BREAKPOINT) {
                // Small delay to allow textarea to resize first
                setTimeout(scrollToBottom, 50);
            }
        };
        
        // Handle viewport resize (keyboard opening/closing) to keep messages visible
        const resizeHandler = () => {
            if (window.innerWidth <= MOBILE_BREAKPOINT && document.activeElement === inputElement) {
                // Keyboard likely opened or closed - scroll to bottom multiple times
                scrollMultipleTimes();
            }
        };
        
        // Handle visualViewport changes (more reliable on iOS for keyboard detection)
        const visualViewportHandler = () => {
            if (window.innerWidth <= MOBILE_BREAKPOINT && document.activeElement === inputElement) {
                // Visual viewport changed - keyboard likely opened/closed
                scrollMultipleTimes();
            }
        };
        
        // Store all handlers
        focusHandler.inputHandler = inputHandler;
        focusHandler.resizeHandler = resizeHandler;
        focusHandler.visualViewportHandler = visualViewportHandler;
        this._focusHandlers.set(inputElement, focusHandler);
        
        inputElement.addEventListener('focus', focusHandler);
        inputElement.addEventListener('input', inputHandler);
        window.addEventListener('resize', resizeHandler);
        
        // Use visualViewport API if available (more reliable for keyboard detection on iOS)
        if (window.visualViewport) {
            window.visualViewport.addEventListener('resize', visualViewportHandler);
        }
    }
};
