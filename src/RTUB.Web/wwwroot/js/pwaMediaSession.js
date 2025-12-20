// PWA Media Session API - Next/Previous Controls for Lockscreen
// Only active in PWA installed mode (iOS/Android)
window.pwaMediaSession = {
    // Queue management
    queue: [],
    currentIndex: 0,
    audioElement: null,
    
    // Callbacks to Blazor
    callbacks: {
        onNext: null,
        onPrev: null,
        onPlay: null,
        onPause: null,
        onSeekTo: null
    },
    
    // Track if we're in PWA mode
    isPwaMode: false,
    
    /**
     * Detects if app is running in installed PWA mode
     * @returns {boolean} True if running as installed PWA
     */
    detectPwaMode: function() {
        // Check standard display mode
        const isStandalone = window.matchMedia('(display-mode: standalone)').matches;
        
        // iOS Safari specific check
        const isIosStandalone = window.navigator.standalone === true;
        
        return isStandalone || isIosStandalone;
    },
    
    /**
     * Initialize the PWA Media Session module
     * @param {string} audioElementId - ID of the audio element
     */
    init: function(audioElementId) {
        this.isPwaMode = this.detectPwaMode();
        
        // Only proceed if in PWA mode
        if (!this.isPwaMode) {
            console.log('Not in PWA mode - Media Session handlers not registered');
            return false;
        }
        
        // Only proceed if Media Session API is available
        if (!('mediaSession' in navigator)) {
            console.warn('Media Session API not available');
            return false;
        }
        
        // Get audio element reference
        this.audioElement = document.getElementById(audioElementId);
        if (!this.audioElement) {
            console.warn('Audio element not found:', audioElementId);
            return false;
        }
        
        // Attach event listeners to audio element
        this.attachAudioEventListeners();
        
        console.log('PWA Media Session initialized');
        return true;
    },
    
    /**
     * Set the playback queue
     * @param {Array} queue - Array of track objects {id, title, artist, album, artworkUrl}
     * @param {number} currentIndex - Index of current track in queue
     */
    setQueue: function(queue, currentIndex) {
        if (!this.isPwaMode) return;
        
        this.queue = queue || [];
        this.currentIndex = currentIndex || 0;
        
        console.log('Queue set:', this.queue.length, 'tracks, current index:', this.currentIndex);
    },
    
    /**
     * Set now playing metadata
     * @param {object} metadata - Track metadata {title, artist, album, artworkUrl}
     */
    setNowPlaying: function(metadata) {
        if (!this.isPwaMode) return;
        if (!('mediaSession' in navigator)) return;
        
        try {
            const artwork = metadata.artworkUrl ? [
                { src: metadata.artworkUrl, sizes: '96x96', type: 'image/png' },
                { src: metadata.artworkUrl, sizes: '192x192', type: 'image/png' },
                { src: metadata.artworkUrl, sizes: '512x512', type: 'image/png' }
            ] : [];
            
            navigator.mediaSession.metadata = new MediaMetadata({
                title: metadata.title || '',
                artist: metadata.artist || '',
                album: metadata.album || '',
                artwork: artwork
            });
            
            console.log('Media metadata set:', metadata.title);
        } catch (e) {
            console.warn('Failed to set media metadata:', e);
        }
    },
    
    /**
     * Bind handlers for media session actions
     * Handlers work client-side without callbacks to reduce SignalR dependency
     */
    bindHandlers: function() {
        if (!this.isPwaMode) return;
        if (!('mediaSession' in navigator)) return;
        
        try {
            // Play handler
            navigator.mediaSession.setActionHandler('play', () => {
                console.log('Media Session: play');
                if (this.audioElement) {
                    this.audioElement.play();
                }
            });
            
            // Pause handler
            navigator.mediaSession.setActionHandler('pause', () => {
                console.log('Media Session: pause');
                if (this.audioElement) {
                    this.audioElement.pause();
                }
            });
            
            // Next track handler
            navigator.mediaSession.setActionHandler('nexttrack', () => {
                console.log('Media Session: nexttrack');
                this.handleNext();
            });
            
            // Previous track handler
            navigator.mediaSession.setActionHandler('previoustrack', () => {
                console.log('Media Session: previoustrack');
                this.handlePrevious();
            });
            
            // DO NOT register seekbackward/seekforward on iOS
            // iOS prioritizes seek over next/prev, so we want next/prev buttons instead
            
            // Seek to (optional - for progress bar seeking)
            try {
                navigator.mediaSession.setActionHandler('seekto', (details) => {
                    console.log('Media Session: seekto', details.seekTime);
                    if (this.audioElement && details.seekTime !== undefined) {
                        this.audioElement.currentTime = details.seekTime;
                    }
                });
            } catch (e) {
                // Not all browsers support seekto
            }
            
            console.log('Media Session handlers bound');
        } catch (e) {
            console.warn('Failed to bind media session handlers:', e);
        }
    },
    
    /**
     * Handle Next track action
     * Loads next track and updates UI client-side
     */
    handleNext: function() {
        // Check if we can go to next track
        if (this.currentIndex < this.queue.length - 1) {
            this.currentIndex++;
            const nextTrack = this.queue[this.currentIndex];
            
            console.log('Playing next track:', nextTrack.title);
            
            // Load and play next track
            this.loadTrack(nextTrack);
        } else {
            console.log('At end of queue - no next track');
            // Stop playback at end (no wrap)
            if (this.audioElement) {
                this.audioElement.pause();
            }
        }
    },
    
    /**
     * Handle Previous track action
     * If current time > 3s: restart current track
     * Otherwise: go to previous track
     */
    handlePrevious: function() {
        const currentTime = this.audioElement ? this.audioElement.currentTime : 0;
        
        // If more than 3 seconds into track, restart it
        if (currentTime > 3) {
            console.log('Restarting current track (>3s)');
            if (this.audioElement) {
                this.audioElement.currentTime = 0;
                const playPromise = this.audioElement.play();
                if (playPromise !== undefined) {
                    playPromise.catch(e => console.error('Play failed:', e));
                }
            }
        } else {
            // Go to previous track if available
            if (this.currentIndex > 0) {
                this.currentIndex--;
                const prevTrack = this.queue[this.currentIndex];
                
                console.log('Playing previous track:', prevTrack.title);
                
                // Load and play previous track
                this.loadTrack(prevTrack);
            } else {
                console.log('At start of queue - restarting current track');
                // At start of queue, just restart current track
                if (this.audioElement) {
                    this.audioElement.currentTime = 0;
                    const playPromise = this.audioElement.play();
                    if (playPromise !== undefined) {
                        playPromise.catch(e => console.error('Play failed:', e));
                    }
                }
            }
        }
    },
    
    /**
     * Load a track into the audio element
     * @param {object} track - Track object with {audioUrl, title, artist, album, artworkUrl}
     */
    loadTrack: function(track) {
        if (!this.audioElement) return;
        
        // Update audio source
        this.audioElement.src = track.audioUrl;
        this.audioElement.load();
        
        // Play with promise handling (browsers can block autoplay)
        const playPromise = this.audioElement.play();
        if (playPromise !== undefined) {
            playPromise.then(() => {
                console.log('Playback started successfully');
            }).catch(error => {
                console.error('Playback failed:', error);
                // User interaction required - log for debugging
                console.log('Note: Browser may have blocked autoplay. User needs to interact with page.');
            });
        }
        
        // Update metadata
        this.setNowPlaying({
            title: track.title,
            artist: track.artist || '',
            album: track.album || '',
            artworkUrl: track.artworkUrl || ''
        });
    },
    
    /**
     * Attach event listeners to audio element for position state updates
     */
    attachAudioEventListeners: function() {
        if (!this.audioElement) return;
        if (!this.isPwaMode) return;
        if (!('mediaSession' in navigator)) return;
        
        // Update position state on various events
        const updatePositionState = () => {
            try {
                if (this.audioElement && !isNaN(this.audioElement.duration)) {
                    navigator.mediaSession.setPositionState({
                        duration: this.audioElement.duration,
                        playbackRate: this.audioElement.playbackRate,
                        position: this.audioElement.currentTime
                    });
                }
            } catch (e) {
                // Fail silently - some browsers don't support setPositionState
            }
        };
        
        // Attach listeners
        this.audioElement.addEventListener('loadedmetadata', updatePositionState);
        this.audioElement.addEventListener('timeupdate', updatePositionState);
        this.audioElement.addEventListener('play', updatePositionState);
        this.audioElement.addEventListener('pause', updatePositionState);
        this.audioElement.addEventListener('ratechange', updatePositionState);
        
        // Auto-play next track when current track ends
        this.audioElement.addEventListener('ended', () => {
            console.log('Track ended - auto-playing next track');
            this.handleNext();
        });
        
        console.log('Audio event listeners attached');
    },
    
    /**
     * Update current index (when track changes externally)
     * @param {number} newIndex - New current index
     */
    updateCurrentIndex: function(newIndex) {
        if (!this.isPwaMode) return;
        this.currentIndex = newIndex;
        console.log('Current index updated to:', newIndex);
    },
    
    /**
     * Clear all handlers and reset state
     */
    cleanup: function() {
        if (!this.isPwaMode) return;
        if (!('mediaSession' in navigator)) return;
        
        try {
            // Clear handlers
            navigator.mediaSession.setActionHandler('play', null);
            navigator.mediaSession.setActionHandler('pause', null);
            navigator.mediaSession.setActionHandler('nexttrack', null);
            navigator.mediaSession.setActionHandler('previoustrack', null);
            navigator.mediaSession.setActionHandler('seekto', null);
            
            // Clear metadata
            navigator.mediaSession.metadata = null;
            
            console.log('Media Session cleanup complete');
        } catch (e) {
            console.warn('Failed to cleanup media session:', e);
        }
    }
};
