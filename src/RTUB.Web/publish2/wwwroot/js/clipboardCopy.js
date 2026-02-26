// Clipboard Copy Helper
// Provides clipboard API wrapper with fallback support for PWA

window.clipboardCopy = {
    /**
     * Copies text to clipboard using multiple fallback methods
     * @param {string} text - The text to copy to clipboard
     * @returns {Promise<void>}
     */
    copyText: function (text) {
        // Method 1: Try execCommand first (more reliable for user gestures)
        // This is synchronous and maintains the user gesture context
        try {
            const textArea = document.createElement('textarea');
            textArea.value = text;
            
            // Make it invisible but still focusable
            textArea.style.position = 'fixed';
            textArea.style.top = '0';
            textArea.style.left = '0';
            textArea.style.width = '2em';
            textArea.style.height = '2em';
            textArea.style.padding = '0';
            textArea.style.border = 'none';
            textArea.style.outline = 'none';
            textArea.style.boxShadow = 'none';
            textArea.style.background = 'transparent';
            
            document.body.appendChild(textArea);
            textArea.focus();
            textArea.select();
            
            // Try to copy the text
            let successful = false;
            try {
                successful = document.execCommand('copy');
            } catch (e) {
                console.warn('execCommand threw error:', e);
            }
            
            document.body.removeChild(textArea);
            
            if (successful) {
                console.log('Copy successful via execCommand');
                return Promise.resolve();
            }
            
            console.warn('execCommand failed, trying Clipboard API');
        } catch (err) {
            console.warn('execCommand method failed:', err);
        }
        
        // Method 2: Try modern Clipboard API as fallback
        if (navigator.clipboard && navigator.clipboard.writeText) {
            return navigator.clipboard.writeText(text)
                .then(() => {
                    console.log('Copy successful via Clipboard API');
                })
                .catch(err => {
                    console.error('Clipboard API also failed:', err);
                    // Method 3: Show prompt as last resort
                    return this.showUrlPrompt(text);
                });
        }
        
        // All automated methods failed - show prompt
        return this.showUrlPrompt(text);
    },
    
    /**
     * Shows a prompt with the URL as a fallback when clipboard fails
     * @param {string} text - The text to show in the prompt
     * @returns {Promise<void>}
     */
    showUrlPrompt: function (text) {
        // Use a simple alert/prompt approach as fallback
        // In PWA, this is often the most reliable method
        if (window.confirm('Não foi possível copiar automaticamente. Clique OK para ver o link e copiá-lo manualmente.')) {
            // Show the URL in a prompt so user can copy it manually
            prompt('Copie este link:', text);
            return Promise.resolve();
        }
        return Promise.reject(new Error('Cópia cancelada pelo utilizador'));
    }
};
