/**
 * Scroll Helper
 * Scrolls an element into view by id. The id is passed as DATA, never as script.
 */
window.rtubScroll = {
    /**
     * Smoothly scrolls the element with the given id to the top of the viewport.
     * No-op when the element does not exist.
     */
    toElement: function (elementId) {
        document.getElementById(elementId)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
};
