// Media Session API helper for RTUB music player
// Sets metadata for lock screen / system media overlay
window.rtubMediaSession = {
    setNowPlayingMetadata: function (title, album, artworkUrl, fallbackArtworkUrl) {
        try {
            if (!('mediaSession' in navigator)) return;

            const finalArtwork = artworkUrl && artworkUrl.length > 0
                ? artworkUrl
                : (fallbackArtworkUrl || '');

            // Detect image type from URL extension
            const getImageType = function(url) {
                if (!url) return 'image/png';
                const lowerUrl = url.toLowerCase();
                if (lowerUrl.includes('.jpg') || lowerUrl.includes('.jpeg')) return 'image/jpeg';
                if (lowerUrl.includes('.webp')) return 'image/webp';
                if (lowerUrl.includes('.svg')) return 'image/svg+xml';
                return 'image/png'; // default fallback
            };

            const imageType = getImageType(finalArtwork);

            navigator.mediaSession.metadata = new MediaMetadata({
                title: title || '',
                artist: '', // intentionally empty
                album: album || '',
                artwork: finalArtwork
                    ? [
                        { src: finalArtwork, sizes: '96x96', type: imageType },
                        { src: finalArtwork, sizes: '192x192', type: imageType },
                        { src: finalArtwork, sizes: '512x512', type: imageType }
                    ]
                    : []
            });
        } catch (e) {
            console.warn('MediaSession not available', e);
        }
    },

    clearMetadata: function () {
        try {
            if (!('mediaSession' in navigator)) return;
            navigator.mediaSession.metadata = null;
        } catch (e) {
            console.warn('MediaSession not available', e);
        }
    }
};
