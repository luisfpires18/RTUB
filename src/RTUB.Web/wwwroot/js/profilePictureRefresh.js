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
            
            // Add new version parameter
            const newVersion = new Date().getTime();
            navbarAvatar.src = `${baseSrc}?v=${newVersion}`;
            
            console.log('[profilePictureRefresh] Updated navbar avatar src:', navbarAvatar.src);
        } else {
            console.log('[profilePictureRefresh] Navbar avatar not found');
        }
    }
};
