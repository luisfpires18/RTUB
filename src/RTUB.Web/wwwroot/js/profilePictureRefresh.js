// Profile Picture Refresh Helper
window.profilePictureRefresh = {
    // Update navbar avatar with the new image URL
    // The new URL will be fetched with ETag support for efficient caching
    refreshNavbarAvatar: function(newImageUrl) {
        console.log('[profilePictureRefresh] Refreshing navbar avatar with URL:', newImageUrl);
        
        // Find the navbar avatar image
        const navbarAvatar = document.querySelector('.navbar-avatar');
        if (navbarAvatar) {
            // Update the src to the new image URL
            // The browser will fetch it with ETag support for efficient caching (304 responses)
            navbarAvatar.src = newImageUrl;
            
            console.log('[profilePictureRefresh] Updated navbar avatar src:', newImageUrl);
        } else {
            console.log('[profilePictureRefresh] Navbar avatar not found');
        }
    }
};
