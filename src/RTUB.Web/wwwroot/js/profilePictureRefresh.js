// Profile Picture Refresh Helper
window.profilePictureRefresh = {
    // Force refresh of navbar avatar by reloading the page
    // This allows the browser to fetch the updated image with ETag support (304 responses)
    refreshNavbarAvatar: function() {
        console.log('[profilePictureRefresh] Refreshing navbar avatar by reloading page');
        
        // Simply reload the page to get the updated avatar
        // The browser will use ETags to efficiently cache unchanged resources
        window.location.reload();
    }
};
