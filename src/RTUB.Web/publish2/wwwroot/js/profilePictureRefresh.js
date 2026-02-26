// Profile Picture Refresh Helper
window.profilePictureRefresh = {
    // Update navbar avatar with the new image URL
    // The new URL will be fetched with ETag support for efficient caching
    refreshNavbarAvatar: function(newImageUrl) {
        const navbarAvatar = document.querySelector('.navbar-avatar');
        if (navbarAvatar) {
            navbarAvatar.src = newImageUrl;
        }
    }
};
