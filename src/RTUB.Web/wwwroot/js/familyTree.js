// Family Tree centering and viewport management
window.RTUB = window.RTUB || {};

(function() {
    'use strict';

    // Center the family tree viewport on the first root node
    RTUB.centerFamilyTree = function() {
        const viewport = document.getElementById('familyTreeViewport');
        const container = document.getElementById('familyTreeContainer');
        
        if (!viewport || !container) {
            return;
        }

        // Find the first root node
        const rootNode = container.querySelector('[data-is-root="true"]');
        
        if (!rootNode) {
            return;
        }

        // Get positions
        const viewportRect = viewport.getBoundingClientRect();
        const rootRect = rootNode.getBoundingClientRect();
        const containerRect = container.getBoundingClientRect();
        
        // Calculate the offset needed to center the root node
        // Account for the difference between container and viewport positions
        const rootOffsetInContainer = rootRect.left - containerRect.left;
        const targetScroll = rootOffsetInContainer - (viewportRect.width / 2) + (rootRect.width / 2);
        
        // Scroll to center the root, but don't go negative
        viewport.scrollLeft = Math.max(0, targetScroll);
    };

    // Re-center on window resize or orientation change
    let resizeTimeout;
    window.addEventListener('resize', function() {
        clearTimeout(resizeTimeout);
        resizeTimeout = setTimeout(function() {
            RTUB.centerFamilyTree();
        }, 250);
    });

    // Re-center on orientation change
    window.addEventListener('orientationchange', function() {
        setTimeout(function() {
            RTUB.centerFamilyTree();
        }, 300);
    });
})();
