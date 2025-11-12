// Image Cropper JavaScript Interop
window.ImageCropperInterop = {
    cropper: null,
    imageElement: null,

    initializeCropper: function (imageElementId, aspectRatio) {
        const image = document.getElementById(imageElementId);
        if (!image) {
            console.error('Image element not found:', imageElementId);
            return false;
        }

        this.imageElement = image;

        // Destroy existing cropper if any
        if (this.cropper) {
            this.cropper.destroy();
        }

        // Convert null/undefined to NaN for free aspect ratio
        const ratio = (aspectRatio === null || aspectRatio === undefined) ? NaN : aspectRatio;

        // Detect if on mobile
        const isMobile = window.innerWidth <= 768;
        
        // Check if Cropper is available
        if (typeof Cropper === 'undefined') {
            console.error('Cropper.js library not loaded');
            return false;
        }
        
        try {
            // Initialize Cropper.js with mobile-friendly settings
            this.cropper = new Cropper(image, {
                aspectRatio: ratio, // NaN means free aspect ratio
                viewMode: isMobile ? 0 : 1, // More flexible on mobile
                dragMode: 'move',
                autoCropArea: 1,
                restore: false,
                guides: true,
                center: true,
                highlight: false,
                cropBoxMovable: true,
                cropBoxResizable: true,
                toggleDragModeOnDblclick: false,
                responsive: true,
                background: true,
                modal: true,
                minContainerWidth: isMobile ? 100 : 200,
                minContainerHeight: isMobile ? 100 : 200,
                checkCrossOrigin: false
            });

            return true;
        } catch (error) {
            console.error('Error initializing Cropper:', error);
            return false;
        }
    },

    getCroppedImageAsBase64: function (format, quality) {
        if (!this.cropper) {
            console.error('Cropper not initialized');
            return null;
        }

        try {
            // Get cropped canvas with max dimensions to prevent SignalR issues
            const canvas = this.cropper.getCroppedCanvas({
                maxWidth: 2048,
                maxHeight: 2048,
                imageSmoothingEnabled: true,
                imageSmoothingQuality: 'high'
            });

            if (!canvas) {
                console.error('Failed to get cropped canvas - canvas is null');
                return null;
            }

            // Return base64 string
            const base64 = canvas.toDataURL(format || 'image/jpeg', quality || 0.9);
            
            if (!base64) {
                console.error('Failed to convert canvas to base64');
                return null;
            }
            
            return base64;
        } catch (error) {
            console.error('Error in getCroppedImageAsBase64:', error);
            return null;
        }
    },

    getCroppedImageAsBlob: function (dotNetHelper, format, quality) {
        if (!this.cropper) {
            console.error('Cropper not initialized');
            return;
        }

        const canvas = this.cropper.getCroppedCanvas();
        if (!canvas) {
            console.error('Failed to get cropped canvas');
            return;
        }

        // Convert to Blob and return via callback
        canvas.toBlob(
            async function (blob) {
                if (blob) {
                    const reader = new FileReader();
                    reader.onloadend = function () {
                        const base64data = reader.result;
                        dotNetHelper.invokeMethodAsync('OnCroppedImageReady', base64data);
                    };
                    reader.readAsDataURL(blob);
                }
            },
            format || 'image/jpeg',
            quality || 0.9
        );
    },

    destroy: function () {
        if (this.cropper) {
            this.cropper.destroy();
            this.cropper = null;
        }
        this.imageElement = null;
    },

    reset: function () {
        if (this.cropper) {
            this.cropper.reset();
        }
    },

    rotate: function (degrees) {
        if (this.cropper) {
            this.cropper.rotate(degrees);
        }
    },

    zoom: function (ratio) {
        if (this.cropper) {
            this.cropper.zoom(ratio);
        }
    },

    setAspectRatio: function (ratio) {
        if (this.cropper) {
            const aspectRatio = (ratio === null || ratio === undefined) ? NaN : ratio;
            this.cropper.setAspectRatio(aspectRatio);
        }
    }
};
