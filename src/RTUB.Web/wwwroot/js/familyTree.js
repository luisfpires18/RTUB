// Family Tree centering and viewport management
window.RTUB = window.RTUB || {};

(function() {
    'use strict';

    // Center the family tree on mobile using the scroll container
    RTUB.centerFamilyTree = function() {
        const scrollContainer = document.getElementById('familyTreeScroll');
        const viewport = document.getElementById('familyTreeViewport');
        const container = document.getElementById('familyTreeContainer');
        
        if (!scrollContainer || !viewport || !container) {
            return;
        }

        // Check if we're on mobile (max-width: 768px)
        const isMobile = window.innerWidth <= 768;
        
        if (isMobile) {
            // On mobile, center the tree horizontally in the scroll container
            const scrollWidth = container.scrollWidth;
            const containerWidth = scrollContainer.clientWidth;
            
            // Center position: scroll to show middle of the content
            const centerScroll = Math.max(0, (scrollWidth - containerWidth) / 2);
            
            // Set scroll position to center
            scrollContainer.scrollLeft = centerScroll;
        } else {
            // On desktop, no need to scroll (tree is already centered via CSS)
            scrollContainer.scrollLeft = 0;
        }
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
