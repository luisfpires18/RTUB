// Family Tree centering and viewport management
window.RTUB = window.RTUB || {};

(function() {
    'use strict';

    // Center the family tree viewport on the root node or center of tree
    RTUB.centerFamilyTree = function() {
        const viewport = document.getElementById('familyTreeViewport');
        const container = document.getElementById('familyTreeContainer');
        
        if (!viewport || !container) {
            return;
        }

        // On mobile, the container has 50vw padding on each side
        // We want to scroll so the tree content (not padding) is centered in the viewport
        
        // Calculate the scroll position to center the content
        // The container's scroll width includes the padding
        const scrollWidth = container.scrollWidth;
        const viewportWidth = viewport.clientWidth;
        
        // Center position: scroll to show middle of the scrollable content
        const centerScroll = (scrollWidth - viewportWidth) / 2;
        
        // Set scroll position to center
        viewport.scrollLeft = centerScroll;
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
