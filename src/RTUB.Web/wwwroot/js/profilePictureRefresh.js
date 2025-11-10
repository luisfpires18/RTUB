// Profile Picture Refresh Helper
window.profilePictureRefresh = {
    // Force refresh of navbar avatar by updating the src attribute
    refreshNavbarAvatar: function() {
        console.log('[profilePictureRefresh] Refreshing navbar avatar');
        
        // Find the navbar avatar image
        const navbarAvatar = document.querySelector('.navbar-avatar');
        if (navbarAvatar) {
            const currentSrc = navbarAvatar.src;
            
            // Remove any existing version parameter
            let baseSrc = currentSrc.split('?')[0];
            
            // If the current src is the default avatar, don't add version parameter
            if (baseSrc.includes('default-avatar.webp')) {
                console.log('[profilePictureRefresh] Current image is default avatar, not refreshing');
                return;
            }
            
            // Add new version parameter to force reload
            const newVersion = new Date().getTime();
            const newSrc = `${baseSrc}?v=${newVersion}`;
            
            // Set up a temporary error handler
            const originalOnerror = navbarAvatar.onerror;
            navbarAvatar.onerror = function() {
                console.log('[profilePictureRefresh] Failed to load new image, falling back to default');
                // Restore original error handler
                navbarAvatar.onerror = originalOnerror;
                // Trigger the original error handler if it exists
                if (originalOnerror) {
                    originalOnerror.call(navbarAvatar);
                }
            };
            
            // Update the src to trigger reload
            navbarAvatar.src = newSrc;
            
            console.log('[profilePictureRefresh] Updated navbar avatar src:', newSrc);
        } else {
            console.log('[profilePictureRefresh] Navbar avatar not found');
        }
    }
};
