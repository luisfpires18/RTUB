/**
 * Media Preview Helpers
 * Plays/pauses the muted hover previews on media cards.
 * Callers pass DATA (an element reference or a card id) - never script.
 */
window.rtubMediaPreview = {
    /**
     * Plays a <video> element passed in directly (Blazor ElementReference).
     * Returns the play() promise so callers keep today's rejection behaviour.
     */
    play: function (video) {
        return video?.play();
    },

    /**
     * Pauses a <video> element passed in directly (Blazor ElementReference).
     */
    pause: function (video) {
        video?.pause();
    },

    /**
     * Plays the <video> inside the gallery card carrying the given id.
     * The id is used as DATA in a safely escaped attribute selector.
     */
    playInGalleryCard: function (galleryId) {
        return this.play(this._galleryVideo(galleryId));
    },

    /**
     * Pauses the <video> inside the gallery card carrying the given id.
     */
    pauseInGalleryCard: function (galleryId) {
        this.pause(this._galleryVideo(galleryId));
    },

    _galleryVideo: function (galleryId) {
        return document.querySelector('[data-gallery-id="' + CSS.escape(String(galleryId)) + '"] video');
    }
};
