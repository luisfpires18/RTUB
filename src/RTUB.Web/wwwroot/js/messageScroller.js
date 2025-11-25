window.messageScroller = {
    scrollToBottom: function (element) {
        if (!element) return;

        window.requestAnimationFrame(() => {
            element.scrollTop = element.scrollHeight;
        });
    }
};
