/**
 * villageTimer — client-side resource counter for My Tuno Village.
 * Updates displayed resource counts every second using production rates,
 * keeping the UI smooth between server syncs.
 * Build progress is handled server-side via System.Threading.Timer (Destilaria pattern).
 */
window.villageTimer = (() => {
    let intervalId = null;
    let dotNetRef = null;
    let resources = { wood: 0, stone: 0, food: 0 };
    let rates = { wood: 0, stone: 0, food: 0 };
    let storageCap = 900;
    let syncCounter = 0;

    function tick() {
        resources.wood = Math.min(storageCap, resources.wood + rates.wood);
        resources.stone = Math.min(storageCap, resources.stone + rates.stone);
        resources.food = Math.min(storageCap, resources.food + rates.food);

        const wEl = document.getElementById('village-res-wood');
        const sEl = document.getElementById('village-res-stone');
        const fEl = document.getElementById('village-res-food');
        if (wEl) wEl.textContent = Math.floor(resources.wood);
        if (sEl) sEl.textContent = Math.floor(resources.stone);
        if (fEl) fEl.textContent = Math.floor(resources.food);

        // Periodic server sync every 30 seconds
        syncCounter++;
        if (syncCounter >= 30 && dotNetRef) {
            syncCounter = 0;
            dotNetRef.invokeMethodAsync('OnPeriodicSync').catch(() => {});
        }
    }

    return {
        start(ref, res, prodRates, cap) {
            this.stop();
            dotNetRef = ref;
            resources = { ...res };
            rates = { ...prodRates };
            storageCap = cap;
            syncCounter = 0;
            intervalId = setInterval(tick, 1000);
        },

        stop() {
            if (intervalId) {
                clearInterval(intervalId);
                intervalId = null;
            }
            dotNetRef = null;
        },

        sync(res, prodRates, cap) {
            resources = { ...res };
            rates = { ...prodRates };
            storageCap = cap;
            syncCounter = 0;
        }
    };
})();
