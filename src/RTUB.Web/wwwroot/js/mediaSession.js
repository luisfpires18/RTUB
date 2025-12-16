// Media Session API helper for RTUB music player and video player
// Sets metadata for lock screen / system media overlay
window.rtubMediaSession = {
    // Track active video element to clean up event listeners
    activeVideoElement: null,
    activeVideoHandler: null,

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

    setVideoMetadata: function (title, subtitle, artworkUrl) {
        try {
            if (!('mediaSession' in navigator)) return;

            // Use provided artwork or fallback to RTUB logo
            const finalArtwork = artworkUrl && artworkUrl.length > 0
                ? artworkUrl
                : '/icons/rtub-logo-512.png';

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
                title: title || 'Vídeo RTUB',
                artist: '', // intentionally empty
                album: subtitle || '',
                artwork: [
                    { src: finalArtwork, sizes: '96x96', type: imageType },
                    { src: finalArtwork, sizes: '192x192', type: imageType },
                    { src: finalArtwork, sizes: '512x512', type: imageType }
                ]
            });
        } catch (e) {
            console.warn('MediaSession not available', e);
        }
    },

    // Attach metadata to a video element by its ID or CSS selector
    // This will set metadata when the video starts playing
    attachToVideo: function (videoSelector, title, subtitle, artworkUrl) {
        try {
            // Clean up previous video listener if any
            if (this.activeVideoElement && this.activeVideoHandler) {
                this.activeVideoElement.removeEventListener('play', this.activeVideoHandler);
            }

            // Find the video element
            const videoElement = document.querySelector(videoSelector);
            if (!videoElement) {
                console.warn('Video element not found:', videoSelector);
                return;
            }

            // Store reference for cleanup
            this.activeVideoElement = videoElement;

            // Create handler that sets metadata on play
            this.activeVideoHandler = () => {
                this.setVideoMetadata(title, subtitle, artworkUrl);
            };

            // Attach event listener
            videoElement.addEventListener('play', this.activeVideoHandler);

            // If video is already playing, set metadata immediately
            if (!videoElement.paused) {
                this.setVideoMetadata(title, subtitle, artworkUrl);
            }

            // Also clear metadata when video ends or is paused
            videoElement.addEventListener('ended', () => {
                this.clearMetadata();
            });

        } catch (e) {
            console.warn('Failed to attach to video', e);
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
