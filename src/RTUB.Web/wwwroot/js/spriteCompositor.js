/**
 * spriteCompositor.js — Layered character sprite compositor for My Tuno.
 *
 * Creates a PIXI.Container from multiple PNG layers (body, eyes, hair, clothes, weapon)
 * with runtime tinting for colors. Used across all battle modules and static previews.
 *
 * Layer data shape (from CharacterSpriteLayers DTO):
 *   {
 *     bodyPath:    "/sprites/games/my-tuno/layers/body/base.svg",
 *     bodyTint:    "#F5D6C3",
 *     eyesPath:    "/sprites/games/my-tuno/layers/eyes/base.svg",
 *     eyesTint:    "#4A90D9",
 *     hairPath:    "/sprites/games/my-tuno/layers/hair/short.svg" | null,
 *     hairTint:    "#3B2F2F",
 *     clothesPath: "/sprites/games/my-tuno/layers/clothes/casual.svg",
 *     weaponPath:  "/sprites/games/my-tuno/layers/weapons/sword_1h.svg" | null
 *   }
 */
(function () {
    'use strict';

    // Session-level cache bust, reused from the loading page
    const SESSION_CACHE_BUST = `?v=${Date.now()}`;
    // Track loaded aliases globally to avoid redundant fetches
    const loadedLayerAliases = new Set();

    /**
     * Converts a hex color string (#RRGGBB) to a PixiJS numeric tint.
     * @param {string} hex - e.g. "#FF00AA"
     * @returns {number} - e.g. 0xFF00AA
     */
    function hexToTint(hex) {
        if (!hex || hex.length !== 7 || hex[0] !== '#') return 0xFFFFFF;
        return parseInt(hex.substring(1), 16);
    }

    /**
     * Resolves a layer data object from either PascalCase (C# serialization) or camelCase.
     * @param {object} data - Raw layer data from Blazor
     * @returns {object} - Normalized camelCase layer data
     */
    function normalizeLayers(data) {
        if (!data) return null;
        return {
            bodyPath:    data.bodyPath    ?? data.BodyPath    ?? null,
            bodyTint:    data.bodyTint    ?? data.BodyTint    ?? '#F5D6C3',
            eyesPath:    data.eyesPath    ?? data.EyesPath    ?? null,
            eyesTint:    data.eyesTint    ?? data.EyesTint    ?? '#4A90D9',
            hairPath:    data.hairPath    ?? data.HairPath    ?? null,
            hairTint:    data.hairTint    ?? data.HairTint    ?? '#3B2F2F',
            clothesPath: data.clothesPath ?? data.ClothesPath ?? null,
            weaponPath:  data.weaponPath  ?? data.WeaponPath  ?? null
        };
    }

    /**
     * Pre-loads all layer assets into PIXI.Assets cache.
     * Safe to call multiple times — already-loaded assets are skipped.
     * @param {object} layerData - Normalized or raw layer data
     * @param {string} [cacheBust] - Optional cache bust string (defaults to session bust)
     */
    async function preloadLayers(layerData, cacheBust) {
        const layers = normalizeLayers(layerData);
        if (!layers) return;

        const bust = cacheBust || SESSION_CACHE_BUST;
        const paths = [
            layers.bodyPath,
            layers.eyesPath,
            layers.hairPath,
            layers.clothesPath,
            layers.weaponPath
        ].filter(Boolean);

        for (const path of paths) {
            const alias = `layer_${path}`;
            if (loadedLayerAliases.has(alias)) continue;
            try {
                await PIXI.Assets.load({ alias, src: path + bust });
                loadedLayerAliases.add(alias);
            } catch (e) {
                console.warn(`Sprite layer failed to load: ${path}`, e.message);
            }
        }
    }

    /**
     * Creates a PIXI.Container with stacked sprite layers for a character.
     * Each layer is a PIXI.Sprite positioned at the container origin.
     * Tintable layers (body, eyes, hair) get `.tint` applied.
     *
     * The container behaves like a single sprite: set anchor-like positioning
     * on the container itself (x, y) and use `.scale` on it.
     *
     * @param {object} layerData - Normalized or raw layer data
     * @returns {PIXI.Container} - Container with child sprites (z-ordered bottom to top)
     */
    function createCharacterContainer(layerData) {
        const layers = normalizeLayers(layerData);
        const container = new PIXI.Container();

        if (!layers) return container;

        // Layer order (bottom to top): body → clothes → eyes → hair → weapon
        const layerDefs = [
            { path: layers.bodyPath,    tint: hexToTint(layers.bodyTint),  name: 'body' },
            { path: layers.clothesPath, tint: null,                        name: 'clothes' },
            { path: layers.eyesPath,    tint: hexToTint(layers.eyesTint),  name: 'eyes' },
            { path: layers.hairPath,    tint: hexToTint(layers.hairTint),  name: 'hair' },
            { path: layers.weaponPath,  tint: null,                        name: 'weapon' }
        ];

        for (const def of layerDefs) {
            if (!def.path) continue;
            const alias = `layer_${def.path}`;
            try {
                const tex = PIXI.Assets.get(alias);
                if (!tex) continue;
                const sprite = new PIXI.Sprite(tex);
                sprite.anchor.set(0.5, 1); // bottom-center anchor (same as character sprites)
                if (def.tint != null) {
                    sprite.tint = def.tint;
                }
                sprite.label = def.name;
                container.addChild(sprite);
            } catch (_) {
                // Layer not loaded — skip silently
            }
        }

        return container;
    }

    /**
     * Updates tints on an existing character container (e.g. for live preview).
     * @param {PIXI.Container} container - Container created by createCharacterContainer
     * @param {object} layerData - Updated layer data with new tint values
     */
    function updateContainerTints(container, layerData) {
        const layers = normalizeLayers(layerData);
        if (!layers || !container) return;

        const tintMap = {
            body: hexToTint(layers.bodyTint),
            eyes: hexToTint(layers.eyesTint),
            hair: hexToTint(layers.hairTint)
        };

        for (const child of container.children) {
            const name = child.label;
            if (name && tintMap[name] != null) {
                child.tint = tintMap[name];
            }
        }
    }

    /**
     * Checks whether layer data is available (non-null with at least a body path).
     * @param {object} data - Raw data from battle object
     * @returns {boolean}
     */
    function hasLayers(data) {
        if (!data) return false;
        const bp = data.bodyPath ?? data.BodyPath;
        return !!bp;
    }

    // ── Public API ──────────────────────────────────────────────────────────
    window.spriteCompositor = {
        preloadLayers,
        createCharacterContainer,
        updateContainerTints,
        hexToTint,
        normalizeLayers,
        hasLayers
    };
})();
