window.imageRefresh = {
    forceRefresh: function(imageSelector) {
        const images = document.querySelectorAll(imageSelector);
        images.forEach(img => {
            try {
                // Ensure we have a valid src before proceeding
                if (!img.src || img.src === '') {
                    return;
                }
                
                // Parse the current URL and update the cache-bust parameter
                // Use window.location.origin as base to handle both relative and absolute URLs
                const url = new URL(img.src, window.location.origin);
                // Remove any existing 'v' parameter and add a new one with current timestamp
                url.searchParams.delete('v');
                url.searchParams.set('v', Date.now());
                img.src = url.toString();
            } catch (error) {
                // Fallback for any unexpected URL parsing errors
                console.warn('Failed to parse image URL:', error);
                
                // Remove existing v= parameter more precisely using URLSearchParams approach
                let newSrc = img.src;
                const urlParts = newSrc.split('?');
                if (urlParts.length > 1) {
                    // Parse existing params
                    const params = new URLSearchParams(urlParts[1]);
                    params.delete('v');
                    params.set('v', Date.now());
                    newSrc = urlParts[0] + '?' + params.toString();
                } else {
                    // No existing params, just append
                    newSrc = newSrc + '?v=' + Date.now();
                }
                img.src = newSrc;
            }
        });
    }
};
