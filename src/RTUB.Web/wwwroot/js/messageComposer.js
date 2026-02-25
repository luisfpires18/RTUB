/**
 * JavaScript-side message composer that handles textarea input natively
 * without Blazor Server round-trips per keystroke.
 *
 * In Blazor Server, every @oninput and @onkeypress event creates a SignalR
 * round-trip to the server and back. For fast typing this causes:
 *   - Broken/swallowed characters (server value overwrites browser value)
 *   - Input freezes while waiting for the round-trip
 *   - Compounded latency from concurrent event processing
 *
 * This module handles all input events in JavaScript and only calls back
 * to .NET when:
 *   1. User presses Enter (without Shift) to send a message
 *   2. Typing state changes (started → stopped, debounced at 3 seconds)
 *   3. User blurs the textarea (stops typing)
 */
window.messageComposer = {
    /** @private */ _dotNetRef: null,
    /** @private */ _textarea: null,
    /** @private */ _isTyping: false,
    /** @private */ _typingTimer: null,
    /** @private */ _handlers: null,
    /** @private */ _TYPING_TIMEOUT: 3000,

    /**
     * Attach the composer to a textarea element.
     * @param {HTMLTextAreaElement} textarea - The message input element
     * @param {object} dotNetRef - DotNetObjectReference for .NET callbacks
     */
    init: function (textarea, dotNetRef) {
        this.dispose();

        if (!textarea || !(textarea instanceof HTMLTextAreaElement)) return;

        this._textarea = textarea;
        this._dotNetRef = dotNetRef;

        var self = this;

        this._handlers = {
            keydown: function (e) {
                if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    var text = self._textarea.value.trim();
                    if (text && !self._textarea.disabled) {
                        // Stop typing indicator before sending
                        self._stopTypingQuiet();
                        self._dotNetRef.invokeMethodAsync('JsSendMessage');
                    }
                }
            },

            input: function () {
                var text = self._textarea.value.trim();
                if (!text) {
                    self._stopTyping();
                    return;
                }

                // Notify .NET only on the initial transition to "typing"
                if (!self._isTyping) {
                    self._isTyping = true;
                    self._dotNetRef.invokeMethodAsync('JsTypingStarted');
                }

                // Reset the idle timer — fires "stop typing" after 3 s of inactivity
                clearTimeout(self._typingTimer);
                self._typingTimer = setTimeout(function () {
                    self._stopTyping();
                }, self._TYPING_TIMEOUT);
            },

            blur: function () {
                self._stopTyping();
            }
        };

        textarea.addEventListener('keydown', this._handlers.keydown);
        textarea.addEventListener('input', this._handlers.input);
        textarea.addEventListener('blur', this._handlers.blur);
    },

    /**
     * Stop typing and notify .NET.
     * @private
     */
    _stopTyping: function () {
        if (this._isTyping) {
            this._isTyping = false;
            clearTimeout(this._typingTimer);
            if (this._dotNetRef) {
                this._dotNetRef.invokeMethodAsync('JsTypingStopped');
            }
        }
    },

    /**
     * Stop typing locally without notifying .NET.
     * Used before JsSendMessage so the send handler can stop typing on the C# side.
     * @private
     */
    _stopTypingQuiet: function () {
        this._isTyping = false;
        clearTimeout(this._typingTimer);
    },

    /**
     * Get the current textarea value and immediately clear it.
     * Combines two operations into a single interop call.
     * @returns {string}
     */
    getTextAndClear: function () {
        if (!this._textarea) return '';
        var text = this._textarea.value;
        this._textarea.value = '';
        this._stopTypingQuiet();
        return text;
    },

    /**
     * Get the current textarea value.
     * @returns {string}
     */
    getText: function () {
        return this._textarea ? this._textarea.value : '';
    },

    /**
     * Clear the textarea after sending.
     */
    clear: function () {
        if (this._textarea) {
            this._textarea.value = '';
        }
    },

    /**
     * Reset composer state when switching conversations.
     * Clears text and typing flags without calling back to .NET
     * (the caller handles the .NET-side typing stop).
     */
    reset: function () {
        clearTimeout(this._typingTimer);
        this._isTyping = false;
        if (this._textarea) {
            this._textarea.value = '';
        }
    },

    /**
     * Remove all event listeners and release references.
     */
    dispose: function () {
        if (this._textarea && this._handlers) {
            this._textarea.removeEventListener('keydown', this._handlers.keydown);
            this._textarea.removeEventListener('input', this._handlers.input);
            this._textarea.removeEventListener('blur', this._handlers.blur);
        }
        clearTimeout(this._typingTimer);
        this._dotNetRef = null;
        this._textarea = null;
        this._handlers = null;
        this._isTyping = false;
    }
};
