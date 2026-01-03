/**
 * TunerEngine - Web Audio API based pitch detection
 * Uses autocorrelation algorithm for accurate pitch detection
 */
class TunerEngine {
    constructor() {
        this.audioContext = null;
        this.analyser = null;
        this.microphone = null;
        this.mediaStream = null;
        this.rafId = null;
        this.isRunning = false;
        this.onPitchDetectedCallback = null;
        
        // Audio configuration - increased for better accuracy
        this.fftSize = 4096; // Increased from 2048 for better low-frequency resolution
        this.bufferLength = this.fftSize;
        this.buffer = new Float32Array(this.bufferLength);
        
        // Pitch detection parameters
        this.minFrequency = 70;  // Lowered from 80 Hz for bass strings
        this.maxFrequency = 1200; // Hz
        this.clarityThreshold = 0.90; // Increased from 0.85 to reduce false positives
        this.rmsThreshold = 0.01; // Increased from 0.005 to ignore very quiet sounds
        
        // Smoothing for stability - increased history
        this.frequencyHistory = [];
        this.historySize = 8; // Increased from 5 to 8 for more smoothing
    }

    /**
     * Initialize audio context and request microphone access
     * @returns {Promise<void>}
     */
    async start() {
        if (this.isRunning) {
            console.warn('Tuner is already running');
            return;
        }

        try {
            // Request microphone access
            this.mediaStream = await navigator.mediaDevices.getUserMedia({
                audio: {
                    echoCancellation: false,
                    autoGainControl: false,
                    noiseSuppression: false,
                    latency: 0
                }
            });

            // Create audio context
            this.audioContext = new (window.AudioContext || window.webkitAudioContext)();
            
            // Create analyser node
            this.analyser = this.audioContext.createAnalyser();
            this.analyser.fftSize = this.fftSize;
            this.analyser.smoothingTimeConstant = 0;

            // Connect microphone to analyser
            this.microphone = this.audioContext.createMediaStreamSource(this.mediaStream);
            this.microphone.connect(this.analyser);

            this.isRunning = true;
            
            // Start detection loop
            this.detectPitchLoop();
            
            console.log('Tuner started successfully');
        } catch (error) {
            console.error('Error starting tuner:', error);
            this.stop();
            throw error;
        }
    }

    /**
     * Stop the tuner and cleanup resources
     */
    stop() {
        this.isRunning = false;
        
        // Clear frequency history
        this.frequencyHistory = [];

        // Cancel animation frame
        if (this.rafId) {
            cancelAnimationFrame(this.rafId);
            this.rafId = null;
        }

        // Disconnect and cleanup audio nodes
        if (this.microphone) {
            this.microphone.disconnect();
            this.microphone = null;
        }

        if (this.analyser) {
            this.analyser.disconnect();
            this.analyser = null;
        }

        // Close audio context
        if (this.audioContext) {
            this.audioContext.close();
            this.audioContext = null;
        }

        // Stop media stream
        if (this.mediaStream) {
            this.mediaStream.getTracks().forEach(track => track.stop());
            this.mediaStream = null;
        }

        console.log('Tuner stopped');
    }

    /**
     * Main pitch detection loop
     */
    detectPitchLoop() {
        if (!this.isRunning) {
            return;
        }

        const result = this.detectPitch();
        
        if (result && result.clarity >= this.clarityThreshold && this.onPitchDetectedCallback) {
            // Add to history for smoothing
            this.frequencyHistory.push(result.frequency);
            if (this.frequencyHistory.length > this.historySize) {
                this.frequencyHistory.shift();
            }
            
            // Only send update if we have enough samples
            if (this.frequencyHistory.length >= this.historySize) {
                const avgFrequency = this.frequencyHistory.reduce((a, b) => a + b, 0) / this.frequencyHistory.length;
                
                // Check if note is stable (not jumping octaves/notes)
                // Calculate variance to reject wild jumps but allow natural vibrato
                const minFreq = Math.min(...this.frequencyHistory);
                const maxFreq = Math.max(...this.frequencyHistory);
                const freqRange = maxFreq - minFreq;
                
                // Only reject if frequency range is huge (> 30 Hz indicates note jumping)
                // Natural vibrato is usually < 10 Hz, allow up to 30 Hz for safety
                if (freqRange < 30) {
                    this.onPitchDetectedCallback({ 
                        frequency: avgFrequency,
                        clarity: result.clarity 
                    });
                }
            }
        }

        // Schedule next detection - throttled to ~20fps for smoother updates
        setTimeout(() => {
            this.rafId = requestAnimationFrame(() => this.detectPitchLoop());
        }, 50); // 50ms = 20 updates per second
    }

    /**
     * Detect pitch using autocorrelation algorithm
     * @returns {{frequency: number, clarity: number}|null}
     */
    detectPitch() {
        if (!this.analyser) {
            return null;
        }

        // Get time domain data
        this.analyser.getFloatTimeDomainData(this.buffer);

        // Autocorrelation
        const sampleRate = this.audioContext.sampleRate;
        const correlations = this.autoCorrelate(this.buffer, sampleRate);

        if (!correlations) {
            return null;
        }

        const { frequency, clarity } = correlations;

        // Validate frequency range
        if (frequency < this.minFrequency || frequency > this.maxFrequency) {
            return null;
        }

        return { frequency, clarity };
    }

    /**
     * Autocorrelation algorithm for pitch detection
     * @param {Float32Array} buffer - Audio buffer
     * @param {number} sampleRate - Sample rate in Hz
     * @returns {{frequency: number, clarity: number}|null}
     */
    autoCorrelate(buffer, sampleRate) {
        // Find the first zero crossing
        let size = buffer.length;
        let maxSamples = Math.floor(size / 2);
        let bestOffset = -1;
        let bestCorrelation = 0;
        let foundGoodCorrelation = false;

        // Calculate RMS (Root Mean Square) to detect silence
        let rms = 0;
        for (let i = 0; i < size; i++) {
            const val = buffer[i];
            rms += val * val;
        }
        rms = Math.sqrt(rms / size);

        // If signal is too quiet, return null
        if (rms < this.rmsThreshold) {
            return null;
        }

        // Find the first zero crossing after the peak
        let lastCorrelation = 1;
        for (let offset = 1; offset < maxSamples; offset++) {
            let correlation = 0;

            for (let i = 0; i < maxSamples; i++) {
                correlation += Math.abs(buffer[i] - buffer[i + offset]);
            }

            correlation = 1 - (correlation / maxSamples);

            if (correlation > 0.90 && correlation > lastCorrelation) {
                foundGoodCorrelation = true;
                if (correlation > bestCorrelation) {
                    bestCorrelation = correlation;
                    bestOffset = offset;
                }
            } else if (foundGoodCorrelation) {
                // Found the best correlation, break
                break;
            }

            lastCorrelation = correlation;
        }

        if (bestCorrelation > 0.01 && bestOffset !== -1) {
            const frequency = sampleRate / bestOffset;
            return {
                frequency: frequency,
                clarity: bestCorrelation
            };
        }

        return null;
    }

    /**
     * Register callback for pitch detection updates
     * @param {Function} callback - Function to call when pitch is detected
     */
    onPitchDetected(callback) {
        this.onPitchDetectedCallback = callback;
    }

    /**
     * Check if tuner is currently running
     * @returns {boolean}
     */
    get running() {
        return this.isRunning;
    }
}

// Global instance
let tunerInstance = null;

/**
 * Start the tuner with a callback
 * @param {DotNetObjectReference} dotNetHelper - .NET object reference for callbacks
 */
export async function startTuner(dotNetHelper) {
    if (tunerInstance) {
        tunerInstance.stop();
    }
    
    tunerInstance = new TunerEngine();
    
    // Set up callback
    tunerInstance.onPitchDetected((result) => {
        dotNetHelper.invokeMethodAsync('UpdatePitch', result.frequency);
    });
    
    await tunerInstance.start();
}

/**
 * Stop the tuner
 */
export function stopTuner() {
    if (tunerInstance) {
        tunerInstance.stop();
        tunerInstance = null;
    }
}

/**
 * Check if tuner is running
 */
export function isTunerRunning() {
    return tunerInstance !== null && tunerInstance.running;
}

