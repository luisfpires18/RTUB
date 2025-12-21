// Audio Player helper for RTUB
// Handles reliable audio playback with proper loading and error handling
window.rtubAudioPlayer = {
    /**
     * Play audio element by ID with proper loading and error handling
     * @param {string} audioElementId - ID of the audio element
     * @returns {Promise<boolean>} True if playback started successfully
     */
    playAudio: async function(audioElementId) {
        try {
            const audioElement = document.getElementById(audioElementId);
            if (!audioElement) {
                console.warn('Audio element not found:', audioElementId);
                return false;
            }

            // Wait for audio to be ready before playing
            if (audioElement.readyState < 2) {
                // readyState 2 = HAVE_CURRENT_DATA (enough data to play)
                console.log('Waiting for audio metadata to load...');
                await new Promise((resolve, reject) => {
                    const timeout = setTimeout(() => {
                        reject(new Error('Audio metadata load timeout'));
                    }, 10000); // 10 second timeout

                    const onCanPlay = function() {
                        clearTimeout(timeout);
                        audioElement.removeEventListener('canplay', onCanPlay);
                        audioElement.removeEventListener('error', onError);
                        resolve();
                    };

                    const onError = function() {
                        clearTimeout(timeout);
                        audioElement.removeEventListener('canplay', onCanPlay);
                        audioElement.removeEventListener('error', onError);
                        reject(new Error('Audio load error: ' + (audioElement.error?.message || 'Unknown error')));
                    };

                    audioElement.addEventListener('canplay', onCanPlay);
                    audioElement.addEventListener('error', onError);

                    // Trigger load if not already loading
                    audioElement.load();
                });
            }

            // Attempt to play
            console.log('Attempting to play audio...');
            const playPromise = audioElement.play();
            
            if (playPromise !== undefined) {
                await playPromise;
                console.log('Audio playback started successfully');
                return true;
            } else {
                // Older browser that doesn't return a promise
                console.log('Audio play initiated (no promise support)');
                return true;
            }
        } catch (error) {
            // Handle autoplay restrictions or other errors
            console.error('Audio playback failed:', error);
            
            // If autoplay was blocked, log user-friendly message
            if (error.name === 'NotAllowedError') {
                console.log('Autoplay was blocked by browser. User interaction may be required.');
            }
            
            return false;
        }
    },

    /**
     * Stop audio playback
     * @param {string} audioElementId - ID of the audio element
     */
    stopAudio: function(audioElementId) {
        try {
            const audioElement = document.getElementById(audioElementId);
            if (audioElement) {
                audioElement.pause();
                audioElement.currentTime = 0;
            }
        } catch (error) {
            console.warn('Failed to stop audio:', error);
        }
    }
};
