/**
 * Chord Diagram Renderer for HTML5 Canvas
 * Renders fretboard diagrams with finger positions, barres, and more
 */

/**
 * Renders a chord diagram on the specified canvas
 * @param {string} canvasId - The ID of the canvas element
 * @param {Object} fingeringData - The fingering data object containing strings, frets, fingers, baseFret, and barres
 * @param {Object} options - Optional rendering options
 * @returns {boolean} - True if rendering was successful
 */
export function renderChordDiagram(canvasId, fingeringData, options = {}) {
    try {
        const canvas = document.getElementById(canvasId);
        if (!canvas) {
            console.error(`Canvas with id "${canvasId}" not found`);
            return false;
        }

        const ctx = canvas.getContext('2d');
        if (!ctx) {
            console.error('Failed to get canvas context');
            return false;
        }

        // Default options
        const opts = {
            frets: options.frets || 5,
            showFingers: options.showFingers !== false,
            nutThickness: options.nutThickness || 8,
            stringSpacing: options.stringSpacing || 20,
            fretSpacing: options.fretSpacing || 30,
            colorScheme: options.colorScheme || 'light',
            ...options
        };

        // Calculate dimensions
        const numStrings = fingeringData.strings || 6;
        const canvasWidth = canvas.width;
        const canvasHeight = canvas.height;
        
        // Calculate scaling to fit canvas while maintaining aspect ratio
        const padding = 40;
        const topPadding = 60; // Extra space for open/muted string markers
        const bottomPadding = 40;
        const leftPadding = fingeringData.baseFret > 1 ? 60 : 40; // Extra space for fret numbers
        const rightPadding = 40;
        
        const availableWidth = canvasWidth - leftPadding - rightPadding;
        const availableHeight = canvasHeight - topPadding - bottomPadding;
        
        const fretboardWidth = (numStrings - 1) * opts.stringSpacing;
        const fretboardHeight = opts.frets * opts.fretSpacing;
        
        // Scale to fit
        const scaleX = availableWidth / fretboardWidth;
        const scaleY = availableHeight / fretboardHeight;
        const scale = Math.min(scaleX, scaleY);
        
        const scaledStringSpacing = opts.stringSpacing * scale;
        const scaledFretSpacing = opts.fretSpacing * scale;
        const scaledWidth = (numStrings - 1) * scaledStringSpacing;
        const scaledHeight = opts.frets * scaledFretSpacing;
        
        // Center the diagram
        const startX = leftPadding + (availableWidth - scaledWidth) / 2;
        const startY = topPadding;

        // Color scheme
        const colors = opts.colorScheme === 'dark' ? {
            background: '#1a1a1a',
            foreground: '#ffffff',
            fret: '#cccccc',
            finger: '#3F2A86',
            fingerText: '#ffffff',
            nut: '#ffffff',
            muted: '#ff4444'
        } : {
            background: '#ffffff',
            foreground: '#000000',
            fret: '#333333',
            finger: '#3F2A86',
            fingerText: '#ffffff',
            nut: '#000000',
            muted: '#ff0000'
        };

        // Clear canvas
        ctx.fillStyle = colors.background;
        ctx.fillRect(0, 0, canvasWidth, canvasHeight);

        // Draw nut (thick line at top if baseFret === 1)
        if (fingeringData.baseFret === 1) {
            ctx.fillStyle = colors.nut;
            ctx.fillRect(
                startX,
                startY - opts.nutThickness / 2,
                scaledWidth,
                opts.nutThickness
            );
        }

        // Draw fret numbers (left side if baseFret > 1)
        if (fingeringData.baseFret > 1) {
            ctx.fillStyle = colors.foreground;
            ctx.font = `bold ${14 * scale}px Arial`;
            ctx.textAlign = 'right';
            ctx.textBaseline = 'middle';
            ctx.fillText(
                fingeringData.baseFret.toString(),
                startX - 15,
                startY + scaledFretSpacing / 2
            );
        }

        // Draw frets (horizontal lines)
        ctx.strokeStyle = colors.fret;
        ctx.lineWidth = 2;
        for (let i = 0; i <= opts.frets; i++) {
            ctx.beginPath();
            ctx.moveTo(startX, startY + i * scaledFretSpacing);
            ctx.lineTo(startX + scaledWidth, startY + i * scaledFretSpacing);
            ctx.stroke();
        }

        // Draw strings (vertical lines)
        ctx.lineWidth = 1.5;
        for (let i = 0; i < numStrings; i++) {
            ctx.beginPath();
            ctx.moveTo(startX + i * scaledStringSpacing, startY);
            ctx.lineTo(startX + i * scaledStringSpacing, startY + scaledHeight);
            ctx.stroke();
        }

        // Draw barres (curved lines connecting strings)
        if (fingeringData.barres && fingeringData.barres.length > 0) {
            fingeringData.barres.forEach(barre => {
                const fretY = startY + (barre.fret - fingeringData.baseFret + 0.5) * scaledFretSpacing;
                const fromX = startX + (numStrings - barre.fromString) * scaledStringSpacing;
                const toX = startX + (numStrings - barre.toStringNumber) * scaledStringSpacing;
                
                ctx.strokeStyle = colors.finger;
                ctx.lineWidth = 10 * scale;
                ctx.lineCap = 'round';
                ctx.beginPath();
                ctx.moveTo(fromX, fretY);
                ctx.lineTo(toX, fretY);
                ctx.stroke();
            });
        }

        // Draw finger positions
        const fingerRadius = 8 * scale;
        fingeringData.frets.forEach((fret, stringIndex) => {
            const x = startX + (numStrings - 1 - stringIndex) * scaledStringSpacing;
            
            if (fret === -1) {
                // Muted string (X above nut)
                ctx.strokeStyle = colors.muted;
                ctx.lineWidth = 3;
                const markerSize = 8 * scale;
                const markerY = startY - 25;
                ctx.beginPath();
                ctx.moveTo(x - markerSize, markerY - markerSize);
                ctx.lineTo(x + markerSize, markerY + markerSize);
                ctx.moveTo(x + markerSize, markerY - markerSize);
                ctx.lineTo(x - markerSize, markerY + markerSize);
                ctx.stroke();
            } else if (fret === 0) {
                // Open string (hollow circle above nut)
                ctx.strokeStyle = colors.foreground;
                ctx.lineWidth = 2;
                const markerY = startY - 25;
                ctx.beginPath();
                ctx.arc(x, markerY, 6 * scale, 0, 2 * Math.PI);
                ctx.stroke();
            } else if (fret > 0) {
                // Finger position (filled circle at grid intersection)
                const fretPosition = fret - fingeringData.baseFret;
                const y = startY + (fretPosition + 0.5) * scaledFretSpacing;
                
                // Draw filled circle
                ctx.fillStyle = colors.finger;
                ctx.beginPath();
                ctx.arc(x, y, fingerRadius, 0, 2 * Math.PI);
                ctx.fill();
                
                // Draw finger number inside circle
                if (opts.showFingers && fingeringData.fingers[stringIndex] > 0) {
                    ctx.fillStyle = colors.fingerText;
                    ctx.font = `bold ${12 * scale}px Arial`;
                    ctx.textAlign = 'center';
                    ctx.textBaseline = 'middle';
                    ctx.fillText(fingeringData.fingers[stringIndex].toString(), x, y);
                }
            }
        });

        return true;
    } catch (error) {
        console.error('Error rendering chord diagram:', error);
        return false;
    }
}
