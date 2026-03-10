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
    },

    /**
     * Play a repeating announcement alert sound using Web Audio API.
     * Three descending tones, repeated every 2 seconds until stopped.
     */
    _announcementInterval: null,

    playAnnouncementSound: function() {
        this.stopAnnouncementSound();

        const playOnce = () => {
            try {
                const ctx = new (window.AudioContext || window.webkitAudioContext)();
                const master = ctx.createGain();
                master.gain.setValueAtTime(0.8, ctx.currentTime);
                master.connect(ctx.destination);

                const tones = [
                    { freq: 1046.5, start: 0,    duration: 0.18 },
                    { freq: 880,    start: 0.20, duration: 0.18 },
                    { freq: 698.5,  start: 0.40, duration: 0.30 }
                ];

                tones.forEach(({ freq, start, duration }) => {
                    const osc = ctx.createOscillator();
                    const env = ctx.createGain();
                    osc.type = 'sine';
                    osc.frequency.setValueAtTime(freq, ctx.currentTime + start);
                    env.gain.setValueAtTime(0, ctx.currentTime + start);
                    env.gain.linearRampToValueAtTime(1, ctx.currentTime + start + 0.01);
                    env.gain.linearRampToValueAtTime(0, ctx.currentTime + start + duration);
                    osc.connect(env);
                    env.connect(master);
                    osc.start(ctx.currentTime + start);
                    osc.stop(ctx.currentTime + start + duration + 0.05);
                });

                setTimeout(() => ctx.close(), 1500);
            } catch (e) {
                console.warn('Announcement sound failed:', e);
            }
        };

        playOnce();
        this._announcementInterval = setInterval(playOnce, 2000);
    },

    stopAnnouncementSound: function() {
        if (this._announcementInterval) {
            clearInterval(this._announcementInterval);
            this._announcementInterval = null;
        }
    }
};
