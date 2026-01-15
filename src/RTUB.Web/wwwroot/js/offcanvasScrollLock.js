window.offcanvasScrollLock = (() => {
    const bodyClass = 'nav-offcanvas-open';

    const lockBody = () => document.body.classList.add(bodyClass);
    const unlockBody = () => document.body.classList.remove(bodyClass);

    const attachHandlers = (offcanvas) => {
        offcanvas.addEventListener('show.bs.offcanvas', lockBody);
        offcanvas.addEventListener('hidden.bs.offcanvas', unlockBody);
    };

    const init = () => {
        const offcanvasElements = document.querySelectorAll('.navbar .offcanvas');
        if (!offcanvasElements.length) return;

        offcanvasElements.forEach((offcanvas) => {
            attachHandlers(offcanvas);
        });
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    return { lockBody, unlockBody };
})();
