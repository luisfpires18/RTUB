/**
 * Survive Mode — survivor.io-inspired game engine built on PixiJS.
 *
 * The player spawns center-map on a large scrollable map.
 * Enemies swarm from all edges. Dodge them until the timer runs out.
 * Beat the timer → next level. Get touched → game over.
 *
 * Global API: window.surviveModeGame
 *   .start(containerId, levelData, dotNetRef)
 *   .nextLevel(levelData)
 *   .destroy()
 *   .pause() / .resume()
 */
(function () {
    'use strict';

    // ─── Constants ────────────────────────────────────────────────
    const PLAYER_RADIUS = 24;
    const ENEMY_HIT_RADIUS = 24;
    const ELITE_SCALE = 1.6;
    const ELITE_SPEED_MULT = 1.4;
    const PLAYER_MAX_HP = 100;
    const ENEMY_DAMAGE = 10;          // each hit takes 10% HP
    const ELITE_DAMAGE = 20;          // elite hits take 20%
    const INVULN_DURATION = 0.8;      // seconds of invulnerability after being hit
    const BACKGROUND_TILE_SIZE = 512;
    const MINIMAP_SIZE = 100;
    const MINIMAP_MARGIN = 8;
    const TRAIL_ALPHA = 0.15;
    const INVULN_FLASH_MS = 150;
    const XP_ORB_SPEED = 200;
    const XP_ORB_RADIUS = 5;
    const XP_PICKUP_RADIUS = 50;

    // Auto-attack constants
    const ATTACK_RANGE = 180;
    const ATTACK_COOLDOWN = 0.45;       // seconds between attacks
    const BASE_ATTACK_DAMAGE = 1;
    const PROJECTILE_SPEED = 350;
    const PROJECTILE_RADIUS = 4;
    const PROJECTILE_LIFETIME = 1.5;
    const BASE_ENEMY_HP = 2;
    const ELITE_HP_MULT = 3;
    const ENEMY_HP_PER_LEVEL = 1.0;     // extra HP per level

    // Boss constants
    const BOSS_HP_MULT = 80;            // boss HP = base enemy HP * this
    const BOSS_SCALE = 2.5;             // bosses are much bigger than normal enemies
    const BOSS_SPEED_MULT = 0.75;       // bosses are slower but not too slow
    const BOSS_DAMAGE = 50;             // boss contact damage
    const BOSS_HIT_RADIUS = 56;         // big hitbox

    // ─── Upgrade System ───────────────────────────────────────────
    // Dynamic coin thresholds: 5, 10, 25, 65, then +45 each
    const UPGRADE_THRESHOLDS = [5, 10, 25, 65];
    const UPGRADE_THRESHOLD_STEP = 45; // after predefined thresholds, +45 each
    const UPGRADE_DEFS = [
        {
            id: 'moveSpeed',
            icon: '🏃',
            title: 'Pés Rápidos',
            desc: '+15% velocidade de movimento',
            apply: (scene) => { scene.playerSpeed *= 1.15; }
        },
        {
            id: 'atkSpeed',
            icon: '⚡',
            title: 'Fogo Rápido',
            desc: '+20% velocidade de ataque',
            apply: (scene) => { scene.attackCooldownMult *= 0.80; }
        },
        {
            id: 'hp',
            icon: '❤️',
            title: 'Vitalidade',
            desc: '+10 HP Máximo e cura 5',
            apply: (scene) => {
                scene.maxHP += 10;
                scene.playerHP = Math.min(scene.playerHP + 5, scene.maxHP);
            }
        },
        {
            id: 'damage',
            icon: '⚔️',
            title: 'Tiro Potente',
            desc: '2x dano de ataque',
            apply: (scene) => { scene.attackDamage *= 2; }
        },
        {
            id: 'coinRate',
            icon: '🪙',
            title: 'Febre do Ouro',
            desc: '2x moedas por inimigo',
            maxPicks: 3,
            apply: (scene) => { scene.coinDropMult *= 2; }
        },
        {
            id: 'atkRange',
            icon: '🎯',
            title: 'Olho de Águia',
            desc: '+10% alcance de ataque',
            apply: (scene) => { scene.attackRange *= 1.10; }
        },
        {
            id: 'magnet',
            icon: '🧲',
            title: 'Íman de Moedas',
            desc: 'Moedas voam para ti de longe',
            unique: true,
            apply: (scene) => { scene.magnetRadius += 150; }
        },
        {
            id: 'companion',
            icon: '🐷',
            title: 'Leitão',
            desc: 'Um leitão pronto a ser praxado e atacar inimigos próximos',
            unique: true,
            apply: (scene) => { scene.spawnCompanion(); }
        }
    ];

    // Session-level cache bust — set once per page load so the browser
    // can reuse HTTP-cached sprites across level transitions.
    const SESSION_CACHE_BUST = `?v=${Date.now()}`;

    // Audio
    let bgMusic = null;
    let bgMusicLoaded = false;
    let audioEnabled = true;

    // SFX audio pool — reuses Audio elements to avoid GC pressure from creating
    // a new Audio() on every hit, death, or win sound effect.
    const sfxPool = {};
    const SFX_POOL_SIZE = 4;
    const SFX_MAP = {
        win: '/audio/games/my-tuno/survive/win.mp3',
        death: '/audio/games/my-tuno/survive/death.mp3',
        hit: '/audio/games/my-tuno/survive/hit.mp3'
    };
    // Cache-bust version — refreshes audio once per page session
    const audioCacheBuster = `?v=${Date.now()}`;
    function getPooledAudio(type) {
        const src = SFX_MAP[type];
        if (!src) return null;
        if (!sfxPool[type]) {
            sfxPool[type] = [];
            for (let i = 0; i < SFX_POOL_SIZE; i++) {
                const a = new Audio(src + audioCacheBuster);
                a.volume = 0.5;
                sfxPool[type].push(a);
            }
        }
        // Find an audio element that's finished or hasn't started
        for (const a of sfxPool[type]) {
            if (a.paused || a.ended) {
                a.currentTime = 0;
                return a;
            }
        }
        // All busy — reuse the first one
        const a = sfxPool[type][0];
        a.currentTime = 0;
        return a;
    }

    // ─── Game State ───────────────────────────────────────────────
    let app = null;
    let scene = null;
    let gameActive = false;
    let gamePaused = false;
    let dotNetRef = null;

    // ─── Utilities ────────────────────────────────────────────────
    function clamp(val, min, max) { return Math.max(min, Math.min(max, val)); }
    function dist(a, b) { return Math.sqrt((a.x - b.x) ** 2 + (a.y - b.y) ** 2); }
    function lerp(a, b, t) { return a + (b - a) * t; }
    function randomBetween(min, max) { return Math.random() * (max - min) + min; }
    function formatTime(s) {
        const m = Math.floor(s / 60);
        const sec = Math.floor(s % 60);
        return `${m}:${sec.toString().padStart(2, '0')}`;
    }

    // ─── Object Pool ──────────────────────────────────────────────
    // Generic pool to reuse PIXI display objects and avoid GC pressure.
    class ObjectPool {
        constructor(factory, reset, initialSize = 0) {
            this._factory = factory; // () => new object
            this._reset = reset;     // (obj) => reset object for reuse
            this._pool = [];
            for (let i = 0; i < initialSize; i++) {
                this._pool.push(this._factory());
            }
        }
        get() {
            if (this._pool.length > 0) {
                const obj = this._pool.pop();
                return obj;
            }
            return this._factory();
        }
        release(obj) {
            this._reset(obj);
            this._pool.push(obj);
        }
        get size() { return this._pool.length; }
    }

    // ─── Spatial Hash Grid ────────────────────────────────────────
    // Partition entities into grid cells for O(n·k) collision lookups
    // instead of brute-force O(n²).
    class SpatialGrid {
        constructor(cellSize) {
            this.cellSize = cellSize;
            this.cells = new Map();
        }
        _key(cx, cy) { return `${cx},${cy}`; }
        clear() { this.cells.clear(); }
        insert(entity) {
            const cx = Math.floor(entity.x / this.cellSize);
            const cy = Math.floor(entity.y / this.cellSize);
            const key = this._key(cx, cy);
            let cell = this.cells.get(key);
            if (!cell) { cell = []; this.cells.set(key, cell); }
            cell.push(entity);
        }
        /** Return all entities in the same cell and 8 neighbours. */
        query(x, y) {
            const cx = Math.floor(x / this.cellSize);
            const cy = Math.floor(y / this.cellSize);
            const result = [];
            for (let dx = -1; dx <= 1; dx++) {
                for (let dy = -1; dy <= 1; dy++) {
                    const cell = this.cells.get(this._key(cx + dx, cy + dy));
                    if (cell) {
                        for (let i = 0; i < cell.length; i++) result.push(cell[i]);
                    }
                }
            }
            return result;
        }
    }

    // ─── SurviveScene ─────────────────────────────────────────────
    class SurviveScene {
        constructor(containerId, levelData) {
            this.containerId = containerId;
            this.data = levelData;

            // Level config (Blazor sends camelCase JSON)
            this.level = levelData.level;
            this.biomeName = levelData.biomeName;
            this.timerDuration = levelData.timerDurationSeconds;
            this.baseEnemyCount = levelData.baseEnemyCount;
            this.maxEnemyCount = levelData.maxEnemyCount;
            this.spawnInterval = levelData.spawnIntervalSeconds;
            this.enemySpeed = levelData.enemySpeed;
            this.maxEnemySpeed = levelData.maxEnemySpeed;
            this.playerSpeed = levelData.playerSpeed;
            this.enemyScale = levelData.enemyScale;
            this.hasElites = levelData.hasEliteEnemies;
            this.eliteChance = levelData.eliteSpawnChance;
            this.mapWidth = levelData.mapWidth;
            this.mapHeight = levelData.mapHeight;
            this.vpWidth = levelData.viewportWidth;
            this.vpHeight = levelData.viewportHeight;
            this.backgroundPath = levelData.backgroundPath;
            this.enemySprites = levelData.enemySprites || [];
            this.playerSpritePath = levelData.playerSpritePath;
            this.bossSprites = levelData.bossSprites || [];
            this.isFinalLevel = levelData.isFinalLevel || false;
            this.spawnRampPerMinute = levelData.spawnRampPerMinute || 0.20;
            this.speedRampPerMinute = levelData.speedRampPerMinute || 0.10;

            // Runtime state
            this.timeRemaining = this.timerDuration;
            this.timeElapsed = 0;
            this.enemiesKilled = 0;
            this.xpOrbsCollected = 0;
            this.totalSpawned = 0;
            this.spawnTimer = 0;
            this.alive = true;
            this.won = false;
            this.speedRampTimer = 0;
            this.currentEnemySpeed = this.enemySpeed;

            // Containers
            this.worldContainer = null;
            this.uiContainer = null;
            this.enemies = [];
            this._enemyContainerPool = [];
            this._spatialGrid = new SpatialGrid(128);
            this.xpOrbs = [];
            this.particles = [];
            this.projectiles = [];

            // Object pools (initialised in init() once worldContainer exists)
            this._projectilePool = null;
            this._orbPool = null;
            this._particlePool = null;

            // Auto-attack state
            this.attackCooldown = 0;
            this.attackDamage = BASE_ATTACK_DAMAGE;
            this.attackCooldownMult = 1.0;
            this.attackRange = ATTACK_RANGE;

            // Player HP
            this.playerHP = PLAYER_MAX_HP;
            this.maxHP = PLAYER_MAX_HP;
            this.invulnTimer = 0;

            // Upgrade system state
            this.upgradeIndex = 0; // index into UPGRADE_THRESHOLDS
            this.nextUpgradeAt = UPGRADE_THRESHOLDS[0];
            this.upgradesPicked = 0;
            this.maxPowerUps = Math.floor(this.timerDuration / 60); // limit powers to timer minutes (8min = 8 max powers)
            this.pickedUpgradeIds = new Set(); // track unique upgrades already picked
            this.upgradePickCounts = {};       // track pick count per upgrade id
            this.coinDropMult = 1;    // how many coins per kill
            this.magnetRadius = XP_PICKUP_RADIUS; // base magnet radius
            this.companions = [];     // companion PIG entities
            this.upgradePaused = false;
            this.upgradeOverlay = null;

            // Per-minute difficulty ramp
            this.lastMinuteRamp = 0;  // last minute mark we applied ramp
            this.spawnRampBonus = 0;  // extra spawns added per minute
            this.speedRampBonus = 0;  // extra speed multiplier from minutes

            // Boss state
            this.midBossSpawned = false;
            this.finalBossSpawned = false;
            this.activeBoss = null;       // current boss enemy reference
            this.bossHPBar = null;        // UI HP bar element
            this.bossHPBarBg = null;
            this.bossHPBarFill = null;
            this.bossHPBarText = null;
            this.timerStopped = false;    // timer stops at 0 until final boss is killed

            // Input
            this.keys = {};
            this.touchActive = false;
            this.touchTarget = { x: 0, y: 0 };
            this.joystick = null;
            this.joystickActive = false;
            this.joystickAngle = 0;
            this.joystickMagnitude = 0;

            // Player
            this.player = null;
            this.playerX = this.mapWidth / 2;
            this.playerY = this.mapHeight / 2;

            // Camera — start centered on the player
            this.camX = clamp(this.playerX - this.vpWidth / 2, 0, this.mapWidth - this.vpWidth);
            this.camY = clamp(this.playerY - this.vpHeight / 2, 0, this.mapHeight - this.vpHeight);
        }

        async init() {
            const container = document.getElementById(this.containerId);
            if (!container) throw new Error(`Container #${this.containerId} not found`);

            // Clear container
            container.innerHTML = '';

            // Create PixiJS app
            if (!app) {
                app = new PIXI.Application();
                await app.init({
                    width: this.vpWidth,
                    height: this.vpHeight,
                    backgroundColor: 0x1a1a2e,
                    antialias: true,
                    resolution: window.devicePixelRatio || 1,
                    autoDensity: true,
                });
            } else {
                app.renderer.resize(this.vpWidth, this.vpHeight);
            }

            container.appendChild(app.canvas);
            app.canvas.style.width = '100%';
            app.canvas.style.maxWidth = this.vpWidth + 'px';
            app.canvas.style.height = 'auto';
            app.canvas.style.borderRadius = '8px';

            // World container (scrollable) 
            this.worldContainer = new PIXI.Container();
            app.stage.addChild(this.worldContainer);

            // UI container (fixed to viewport)
            this.uiContainer = new PIXI.Container();
            app.stage.addChild(this.uiContainer);

            // Initialise object pools
            this._initPools();

            // Set initial camera position so the first frame is centered on the player
            this.worldContainer.position.set(-this.camX, -this.camY);

            await this.loadAssets();
            this.createBackground();
            this.createPlayer();
            this.createUI();
            this.setupInput();
            this.spawnInitialEnemies();
            this.playMusic();

            // Start game loop
            this.alive = true;
            this.won = false;
            gameActive = true;
            app.ticker.add(this.update, this);
        }

        async loadAssets() {
            const assets = [];
            // Use a session-level cache bust (set once per page load) so the browser
            // can reuse HTTP-cached sprites across level transitions.
            // Aliases are path-based (stable) to avoid VRAM leaks from unique timestamps.
            const cacheBust = SESSION_CACHE_BUST;

            // Background
            if (this.backgroundPath) {
                this._bgAlias = `surviveBg_${this.backgroundPath}`;
                assets.push({ alias: this._bgAlias, src: this.backgroundPath + cacheBust });
            }

            // Player sprite
            if (this.playerSpritePath) {
                this._playerAlias = `survivePlayer_${this.playerSpritePath}`;
                assets.push({ alias: this._playerAlias, src: this.playerSpritePath + cacheBust });
            }

            // Enemy sprites
            this.enemySpriteAliases = [];
            for (let i = 0; i < this.enemySprites.length; i++) {
                const alias = `surviveEnemy_${i}_${this.enemySprites[i]}`;
                assets.push({ alias, src: this.enemySprites[i] + cacheBust });
                this.enemySpriteAliases.push(alias);
            }

            // Boss sprites
            this.bossSpriteAliases = [];
            for (let i = 0; i < this.bossSprites.length; i++) {
                const alias = `surviveBoss_${i}_${this.bossSprites[i]}`;
                assets.push({ alias, src: this.bossSprites[i] + cacheBust });
                this.bossSpriteAliases.push(alias);
            }

            if (assets.length > 0) {
                try {
                    await PIXI.Assets.load(assets);
                } catch (e) {
                    console.warn('Some survive mode assets failed to load:', e);
                }
            }
        }

        createBackground() {
            // Tiled background using color based on biome
            // Extend beyond map edges so camera never shows empty space
            const biomeColors = {
                'Forest': 0x2d5a27,
                'Swamp': 0x3a4a2a,
                'Mountains': 0x6b6b6b,
                'Snowy': 0xd0e0f0,
                'Tropical': 0x3a8a5a,
                'Caverns': 0x2a2a3a,
                'Desert': 0xc4a35a,
                'Volcanic': 0x4a1a1a,
                'Ruins': 0x4a4a3a,
                'Dark': 0x1a1a2a,
                'Void': 0x0a0a1a
            };
            const bgColor = biomeColors[this.biomeName] || 0x2d5a27;

            // Over-size ground so camera edges always have color
            const pad = Math.max(this.vpWidth, this.vpHeight);
            const ground = new PIXI.Graphics();
            ground.rect(-pad, -pad, this.mapWidth + pad * 2, this.mapHeight + pad * 2);
            ground.fill(bgColor);
            this.worldContainer.addChild(ground);

            // Grid lines for spatial awareness (only inside map bounds)
            const grid = new PIXI.Graphics();
            grid.setStrokeStyle({ width: 1, color: 0xffffff, alpha: 0.05 });
            for (let x = 0; x <= this.mapWidth; x += 100) {
                grid.moveTo(x, 0);
                grid.lineTo(x, this.mapHeight);
            }
            for (let y = 0; y <= this.mapHeight; y += 100) {
                grid.moveTo(0, y);
                grid.lineTo(this.mapWidth, y);
            }
            grid.stroke();
            this.worldContainer.addChild(grid);

            // Map boundary markers
            const border = new PIXI.Graphics();
            border.setStrokeStyle({ width: 4, color: 0xff4444, alpha: 0.6 });
            border.rect(0, 0, this.mapWidth, this.mapHeight);
            border.stroke();
            this.worldContainer.addChild(border);

            // Biome decorations (scattered dots representing terrain features)
            const decorations = new PIXI.Graphics();
            const decoColors = {
                'Forest': 0x1a4a17, 'Swamp': 0x2a3a1a, 'Mountains': 0x8a8a8a,
                'Snowy': 0xffffff, 'Tropical': 0x2a6a3a, 'Caverns': 0x3a3a5a,
                'Desert': 0xd4b36a, 'Volcanic': 0x6a2a1a, 'Ruins': 0x5a5a4a,
                'Dark': 0x2a2a4a, 'Void': 0x1a1a3a
            };
            const decoColor = decoColors[this.biomeName] || 0x1a4a17;
            for (let i = 0; i < 200; i++) {
                const x = Math.random() * this.mapWidth;
                const y = Math.random() * this.mapHeight;
                const r = 2 + Math.random() * 6;
                decorations.circle(x, y, r);
            }
            decorations.fill({ color: decoColor, alpha: 0.3 });
            this.worldContainer.addChild(decorations);

            // Try loading background texture as tiled overlay across the map
            try {
                const bgAlias = this._bgAlias || `surviveBg_${this.level}`;
                const bgTexture = PIXI.Assets.get(bgAlias);
                if (bgTexture) {
                    // Tile the background image across the entire map
                    const tileW = this.vpWidth;
                    const tileH = this.vpHeight;
                    for (let tx = 0; tx < this.mapWidth; tx += tileW) {
                        for (let ty = 0; ty < this.mapHeight; ty += tileH) {
                            const bgSprite = new PIXI.Sprite(bgTexture);
                            bgSprite.width = tileW;
                            bgSprite.height = tileH;
                            bgSprite.alpha = 0.15;
                            bgSprite.position.set(tx, ty);
                            this.worldContainer.addChild(bgSprite);
                        }
                    }
                }
            } catch (_) { /* no background texture */ }
        }

        createPlayer() {
            const playerContainer = new PIXI.Container();

            // Try to use player sprite
            let playerSprite = null;
            try {
                const tex = PIXI.Assets.get(this._playerAlias || 'survivePlayer');
                if (tex) {
                    playerSprite = new PIXI.Sprite(tex);
                    playerSprite.anchor.set(0.5, 0.5);
                    const maxSize = PLAYER_RADIUS * 4;
                    const scale = Math.min(maxSize / playerSprite.width, maxSize / playerSprite.height);
                    playerSprite.scale.set(scale);
                    playerContainer.addChild(playerSprite);
                }
            } catch (_) { }

            if (!playerSprite) {
                // Fallback: circle player
                const gfx = new PIXI.Graphics();
                gfx.circle(0, 0, PLAYER_RADIUS);
                gfx.fill(0x4fc3f7);
                gfx.setStrokeStyle({ width: 2, color: 0xffffff });
                gfx.stroke();
                playerContainer.addChild(gfx);

                // Direction indicator
                const arrow = new PIXI.Graphics();
                arrow.moveTo(PLAYER_RADIUS, 0);
                arrow.lineTo(PLAYER_RADIUS - 6, -5);
                arrow.lineTo(PLAYER_RADIUS - 6, 5);
                arrow.closePath();
                arrow.fill(0xffffff);
                playerContainer.addChild(arrow);
                this.playerArrow = arrow;
            }

            // Glow effect
            const glow = new PIXI.Graphics();
            glow.circle(0, 0, PLAYER_RADIUS + 6);
            glow.fill({ color: 0x4fc3f7, alpha: 0.15 });
            playerContainer.addChildAt(glow, 0);
            this.playerGlow = glow;

            playerContainer.position.set(this.playerX, this.playerY);
            this.worldContainer.addChild(playerContainer);
            this.player = playerContainer;
        }

        createUI() {
            // ── Timer bar + text merged (top center) ──
            const timerBarW = 240;
            const timerBarH = 24;
            const timerBarX = this.vpWidth / 2 - timerBarW / 2;
            const timerBarY = 10;
            const timerBarBg = new PIXI.Graphics();
            timerBarBg.roundRect(timerBarX, timerBarY, timerBarW, timerBarH, 6);
            timerBarBg.fill({ color: 0x000000, alpha: 0.7 });
            this.uiContainer.addChild(timerBarBg);

            this.timerBar = new PIXI.Graphics();
            this.timerBarWidth = timerBarW;
            this.timerBarHeight = timerBarH;
            this.timerBarX = timerBarX;
            this.timerBarY = timerBarY;
            this.updateTimerBar();
            this.uiContainer.addChild(this.timerBar);

            this.timerText = new PIXI.Text({
                text: formatTime(this.timerDuration),
                style: {
                    fontFamily: 'Arial', fontSize: 14, fontWeight: 'bold',
                    fill: 0xffffff, align: 'center'
                }
            });
            this.timerText.anchor.set(0.5, 0.5);
            this.timerText.position.set(this.vpWidth / 2, timerBarY + timerBarH / 2);
            this.uiContainer.addChild(this.timerText);

            // ── HP bar (red, below timer) ──
            const hpBarW = 240;
            const hpBarH = 14;
            const hpBarX = this.vpWidth / 2 - hpBarW / 2;
            const hpBarY = timerBarY + timerBarH + 4;
            const hpBarBg = new PIXI.Graphics();
            hpBarBg.roundRect(hpBarX, hpBarY, hpBarW, hpBarH, 5);
            hpBarBg.fill({ color: 0x1a0000, alpha: 0.8 });
            this.uiContainer.addChild(hpBarBg);

            this.hpBar = new PIXI.Graphics();
            this.hpBarWidth = hpBarW;
            this.hpBarHeight = hpBarH;
            this.hpBarX = hpBarX;
            this.hpBarY = hpBarY;
            this.updateHPBar();
            this.uiContainer.addChild(this.hpBar);

            this.hpText = new PIXI.Text({
                text: `${this.playerHP}/${this.maxHP}`,
                style: {
                    fontFamily: 'Arial', fontSize: 10, fontWeight: 'bold',
                    fill: 0xffffff, align: 'center'
                }
            });
            this.hpText.anchor.set(0.5, 0.5);
            this.hpText.position.set(this.vpWidth / 2, hpBarY + hpBarH / 2);
            this.uiContainer.addChild(this.hpText);

            // ── Level / Biome (top left) ──
            const levelBg = new PIXI.Graphics();
            levelBg.roundRect(8, 10, 160, 24, 5);
            levelBg.fill({ color: 0x000000, alpha: 0.6 });
            this.uiContainer.addChild(levelBg);

            const levelText = new PIXI.Text({
                text: `Lv.${this.level} — ${this.biomeName}`,
                style: {
                    fontFamily: 'Arial', fontSize: 13, fontWeight: 'bold',
                    fill: 0xffcc00
                }
            });
            levelText.position.set(14, 13);
            this.uiContainer.addChild(levelText);

            // ── Stats panel (below level label) ──
            const statsBg = new PIXI.Graphics();
            statsBg.roundRect(8, 38, 160, 52, 5);
            statsBg.fill({ color: 0x000000, alpha: 0.5 });
            this.uiContainer.addChild(statsBg);

            this.statsText = new PIXI.Text({
                text: this._buildStatsString(),
                style: {
                    fontFamily: 'Arial', fontSize: 10,
                    fill: 0xcccccc, lineHeight: 13
                }
            });
            this.statsText.position.set(14, 41);
            this.uiContainer.addChild(this.statsText);

            // ── Kill counter (top right) ──
            const killBg = new PIXI.Graphics();
            killBg.roundRect(this.vpWidth - 120, 10, 112, 24, 5);
            killBg.fill({ color: 0x000000, alpha: 0.6 });
            this.uiContainer.addChild(killBg);

            this.killText = new PIXI.Text({
                text: `☠ 0`,
                style: {
                    fontFamily: 'Arial', fontSize: 13, fontWeight: 'bold',
                    fill: 0xff6666
                }
            });
            this.killText.position.set(this.vpWidth - 114, 13);
            this.uiContainer.addChild(this.killText);

            // ── Coin counter (below kill counter, top right) ──
            const orbBg = new PIXI.Graphics();
            orbBg.roundRect(this.vpWidth - 120, 38, 112, 24, 5);
            orbBg.fill({ color: 0x000000, alpha: 0.6 });
            this.uiContainer.addChild(orbBg);

            this.orbText = new PIXI.Text({
                text: `🪙 0`,
                style: {
                    fontFamily: 'Arial', fontSize: 13, fontWeight: 'bold',
                    fill: 0xffd700
                }
            });
            this.orbText.position.set(this.vpWidth - 114, 41);
            this.uiContainer.addChild(this.orbText);

            // Minimap (bottom right)
            this.createMinimap();

            // Mobile joystick
            this.createJoystick();
        }

        createMinimap() {
            const x = this.vpWidth - MINIMAP_SIZE - MINIMAP_MARGIN;
            const y = this.vpHeight - MINIMAP_SIZE - MINIMAP_MARGIN;

            const mmBg = new PIXI.Graphics();
            mmBg.roundRect(x, y, MINIMAP_SIZE, MINIMAP_SIZE, 4);
            mmBg.fill({ color: 0x000000, alpha: 0.5 });
            mmBg.setStrokeStyle({ width: 1, color: 0xffffff, alpha: 0.3 });
            mmBg.stroke();
            this.uiContainer.addChild(mmBg);

            // Persistent minimap graphics — redrawn each frame, never destroyed
            this._mmPlayerDot = new PIXI.Graphics();
            this._mmEnemyDots = new PIXI.Graphics();
            this._mmVpRect = new PIXI.Graphics();

            this.minimapContainer = new PIXI.Container();
            this.minimapContainer.position.set(x, y);
            this.minimapContainer.addChild(this._mmEnemyDots);
            this.minimapContainer.addChild(this._mmPlayerDot);
            this.minimapContainer.addChild(this._mmVpRect);
            this.uiContainer.addChild(this.minimapContainer);

            this.mmX = x;
            this.mmY = y;
        }

        createJoystick() {
            // Virtual joystick for mobile (bottom left)
            const joyX = 80;
            const joyY = this.vpHeight - 80;
            const joyRadius = 50;

            const joyBg = new PIXI.Graphics();
            joyBg.circle(0, 0, joyRadius);
            joyBg.fill({ color: 0xffffff, alpha: 0.1 });
            joyBg.setStrokeStyle({ width: 2, color: 0xffffff, alpha: 0.2 });
            joyBg.stroke();
            joyBg.position.set(joyX, joyY);
            joyBg.eventMode = 'static';
            this.uiContainer.addChild(joyBg);

            const joyKnob = new PIXI.Graphics();
            joyKnob.circle(0, 0, 18);
            joyKnob.fill({ color: 0xffffff, alpha: 0.4 });
            joyKnob.position.set(joyX, joyY);
            this.uiContainer.addChild(joyKnob);

            this.joystick = { bg: joyBg, knob: joyKnob, x: joyX, y: joyY, radius: joyRadius };

            // Touch events for joystick
            joyBg.on('pointerdown', (e) => {
                this.joystickActive = true;
                this.updateJoystick(e);
            });
            joyBg.on('pointermove', (e) => {
                if (this.joystickActive) this.updateJoystick(e);
            });
            joyBg.on('pointerup', () => this.resetJoystick());
            joyBg.on('pointerupoutside', () => this.resetJoystick());
        }

        updateJoystick(e) {
            const pos = e.getLocalPosition(this.uiContainer);
            const dx = pos.x - this.joystick.x;
            const dy = pos.y - this.joystick.y;
            const d = Math.sqrt(dx * dx + dy * dy);
            const maxD = this.joystick.radius;
            const clamped = Math.min(d, maxD);

            this.joystickAngle = Math.atan2(dy, dx);
            this.joystickMagnitude = clamped / maxD;

            this.joystick.knob.position.set(
                this.joystick.x + Math.cos(this.joystickAngle) * clamped,
                this.joystick.y + Math.sin(this.joystickAngle) * clamped
            );
        }

        resetJoystick() {
            this.joystickActive = false;
            this.joystickMagnitude = 0;
            this.joystick.knob.position.set(this.joystick.x, this.joystick.y);
        }

        setupInput() {
            // Keyboard
            this._onKeyDown = (e) => {
                this.keys[e.key.toLowerCase()] = true;
                e.preventDefault();
            };
            this._onKeyUp = (e) => {
                this.keys[e.key.toLowerCase()] = false;
            };
            window.addEventListener('keydown', this._onKeyDown);
            window.addEventListener('keyup', this._onKeyUp);

            // Mouse / touch for movement on the canvas
            if (app && app.canvas) {
                this._onPointerDown = (e) => {
                    // Only for non-joystick area
                    const rect = app.canvas.getBoundingClientRect();
                    const scaleX = this.vpWidth / rect.width;
                    const scaleY = this.vpHeight / rect.height;
                    const localX = (e.clientX - rect.left) * scaleX;
                    const localY = (e.clientY - rect.top) * scaleY;
                    
                    // Don't activate if clicking joystick area
                    if (localX < 140 && localY > this.vpHeight - 140) return;

                    this.touchActive = true;
                    this.touchTarget.x = localX + this.camX;
                    this.touchTarget.y = localY + this.camY;
                };
                this._onPointerMove = (e) => {
                    if (!this.touchActive) return;
                    const rect = app.canvas.getBoundingClientRect();
                    const scaleX = this.vpWidth / rect.width;
                    const scaleY = this.vpHeight / rect.height;
                    this.touchTarget.x = (e.clientX - rect.left) * scaleX + this.camX;
                    this.touchTarget.y = (e.clientY - rect.top) * scaleY + this.camY;
                };
                this._onPointerUp = () => { this.touchActive = false; };

                app.canvas.addEventListener('pointerdown', this._onPointerDown);
                app.canvas.addEventListener('pointermove', this._onPointerMove);
                app.canvas.addEventListener('pointerup', this._onPointerUp);
            }
        }

        spawnInitialEnemies() {
            for (let i = 0; i < this.baseEnemyCount; i++) {
                this.spawnEnemy();
            }
        }

        _getEnemyContainer() {
            if (this._enemyContainerPool.length > 0) {
                const c = this._enemyContainerPool.pop();
                c.visible = true;
                c.alpha = 1;
                return c;
            }
            return new PIXI.Container();
        }

        _releaseEnemyContainer(container) {
            if (!container) return;
            if (container.parent) container.parent.removeChild(container);
            // Remove all children (sprite, glow, crown etc.) — textures are kept in PIXI.Assets cache
            while (container.children.length > 0) {
                const child = container.children[0];
                container.removeChild(child);
                if (child.destroy) {
                    try { child.destroy({ children: false, texture: false, baseTexture: false }); } catch (_) {}
                }
            }
            container.visible = false;
            if (this._enemyContainerPool.length < 50) {
                this._enemyContainerPool.push(container);
            } else {
                try { container.destroy({ children: true }); } catch (_) {}
            }
        }

        spawnEnemy() {
            if (this.enemies.length >= this.maxEnemyCount) return;

            // Spawn from map edges, outside the viewport
            const side = Math.floor(Math.random() * 4);
            let ex, ey;
            const margin = 80;

            switch (side) {
                case 0: // top
                    ex = Math.random() * this.mapWidth;
                    ey = Math.max(0, this.camY - margin);
                    break;
                case 1: // right
                    ex = Math.min(this.mapWidth, this.camX + this.vpWidth + margin);
                    ey = Math.random() * this.mapHeight;
                    break;
                case 2: // bottom
                    ex = Math.random() * this.mapWidth;
                    ey = Math.min(this.mapHeight, this.camY + this.vpHeight + margin);
                    break;
                case 3: // left
                    ex = Math.max(0, this.camX - margin);
                    ey = Math.random() * this.mapHeight;
                    break;
            }

            const isElite = this.hasElites && Math.random() < this.eliteChance;
            const scale = isElite ? this.enemyScale * ELITE_SCALE : this.enemyScale;
            const speed = isElite
                ? this.currentEnemySpeed * ELITE_SPEED_MULT
                : this.currentEnemySpeed * (0.8 + Math.random() * 0.4);

            const enemyContainer = this._getEnemyContainer();
            let sprite = null;

            // Try to use enemy sprite texture
            if (this.enemySpriteAliases && this.enemySpriteAliases.length > 0) {
                const alias = this.enemySpriteAliases[Math.floor(Math.random() * this.enemySpriteAliases.length)];
                try {
                    const tex = PIXI.Assets.get(alias);
                    if (tex) {
                        sprite = new PIXI.Sprite(tex);
                        sprite.anchor.set(0.5, 0.5);
                        const maxSize = ENEMY_HIT_RADIUS * 4 * scale;
                        const s = Math.min(maxSize / sprite.width, maxSize / sprite.height);
                        sprite.scale.set(s);
                        enemyContainer.addChild(sprite);
                    }
                } catch (_) { }
            }

            if (!sprite) {
                // Fallback: colored circle
                const gfx = new PIXI.Graphics();
                const radius = ENEMY_HIT_RADIUS * scale;
                gfx.circle(0, 0, radius);
                gfx.fill(isElite ? 0xff4444 : 0xe53935);
                if (isElite) {
                    gfx.setStrokeStyle({ width: 2, color: 0xffff00 });
                    gfx.stroke();
                }
                enemyContainer.addChild(gfx);
            }

            // Elite glow
            if (isElite) {
                const glow = new PIXI.Graphics();
                glow.circle(0, 0, ENEMY_HIT_RADIUS * scale + 4);
                glow.fill({ color: 0xff4444, alpha: 0.2 });
                enemyContainer.addChildAt(glow, 0);
            }

            enemyContainer.position.set(ex, ey);
            this.worldContainer.addChild(enemyContainer);

            this.enemies.push({
                container: enemyContainer,
                x: ex,
                y: ey,
                speed: speed,
                isElite: isElite,
                scale: scale,
                hitRadius: ENEMY_HIT_RADIUS * scale,
                wobblePhase: Math.random() * Math.PI * 2,
                alive: true,
                hp: (() => {
                    const baseHP = BASE_ENEMY_HP + this.level * ENEMY_HP_PER_LEVEL;
                    const minuteBonus = 1 + Math.floor(this.timeElapsed / 60) * 0.15; // +15% HP per minute
                    const hp = baseHP * minuteBonus;
                    return Math.ceil(isElite ? hp * ELITE_HP_MULT : hp);
                })(),
                maxHp: (() => {
                    const baseHP = BASE_ENEMY_HP + this.level * ENEMY_HP_PER_LEVEL;
                    const minuteBonus = 1 + Math.floor(this.timeElapsed / 60) * 0.15;
                    const hp = baseHP * minuteBonus;
                    return Math.ceil(isElite ? hp * ELITE_HP_MULT : hp);
                })()
            });

            this.totalSpawned++;
        }

        spawnBoss(isFinal) {
            // Clear all existing enemies before boss spawns
            for (const enemy of this.enemies) {
                if (enemy.container) {
                    this._releaseEnemyContainer(enemy.container);
                }
            }
            this.enemies = [];

            // Pick a random boss sprite
            const bossContainer = this._getEnemyContainer();
            let sprite = null;

            if (this.bossSpriteAliases && this.bossSpriteAliases.length > 0) {
                const alias = this.bossSpriteAliases[Math.floor(Math.random() * this.bossSpriteAliases.length)];
                try {
                    const tex = PIXI.Assets.get(alias);
                    if (tex) {
                        sprite = new PIXI.Sprite(tex);
                        sprite.anchor.set(0.5, 0.5);
                        const maxSize = BOSS_HIT_RADIUS * 4;
                        const s = Math.min(maxSize / sprite.width, maxSize / sprite.height);
                        sprite.scale.set(s);
                        bossContainer.addChild(sprite);
                    }
                } catch (_) { }
            }

            if (!sprite) {
                // Fallback: big red circle
                const gfx = new PIXI.Graphics();
                gfx.circle(0, 0, BOSS_HIT_RADIUS);
                gfx.fill(isFinal ? 0xaa0000 : 0xcc2222);
                gfx.setStrokeStyle({ width: 3, color: 0xffcc00 });
                gfx.stroke();
                bossContainer.addChild(gfx);
            }

            // Boss glow aura
            const glow = new PIXI.Graphics();
            glow.circle(0, 0, BOSS_HIT_RADIUS + 10);
            glow.fill({ color: isFinal ? 0xff0000 : 0xff6600, alpha: 0.25 });
            bossContainer.addChildAt(glow, 0);

            // Crown indicator for final boss
            if (isFinal) {
                const crown = new PIXI.Text({
                    text: '👑',
                    style: { fontSize: 20 }
                });
                crown.anchor.set(0.5, 1);
                crown.position.set(0, -BOSS_HIT_RADIUS - 5);
                bossContainer.addChild(crown);
            }

            // Spawn boss near the player but at a safe distance
            const angle = Math.random() * Math.PI * 2;
            const spawnDist = 350;
            const bx = clamp(this.playerX + Math.cos(angle) * spawnDist, BOSS_HIT_RADIUS, this.mapWidth - BOSS_HIT_RADIUS);
            const by = clamp(this.playerY + Math.sin(angle) * spawnDist, BOSS_HIT_RADIUS, this.mapHeight - BOSS_HIT_RADIUS);

            bossContainer.position.set(bx, by);
            this.worldContainer.addChild(bossContainer);

            const minuteBonus = 1 + Math.floor(this.timeElapsed / 60) * 0.20; // +20% HP per minute survived
            const bossHP = Math.ceil((BASE_ENEMY_HP + this.level * ENEMY_HP_PER_LEVEL) * BOSS_HP_MULT * minuteBonus * (isFinal ? 2.0 : 1));

            const boss = {
                container: bossContainer,
                x: bx,
                y: by,
                speed: this.currentEnemySpeed * BOSS_SPEED_MULT,
                isElite: false,
                isBoss: true,
                isFinalBoss: isFinal,
                scale: BOSS_SCALE,
                hitRadius: BOSS_HIT_RADIUS,
                wobblePhase: Math.random() * Math.PI * 2,
                alive: true,
                hp: bossHP,
                maxHp: bossHP
            };

            this.enemies.push(boss);
            this.activeBoss = boss;
            this.totalSpawned++;

            // Show boss HP bar in UI
            this.showBossHPBar(isFinal);

            // Flash screen warning
            this.flashScreen(isFinal ? 0xff0000 : 0xff6600);
        }

        showBossHPBar(isFinal) {
            // Remove old bar if any
            this.hideBossHPBar();

            const barWidth = 300;
            const barHeight = 16;
            const x = (this.vpWidth - barWidth) / 2;
            const y = 70;

            // Background
            this.bossHPBarBg = new PIXI.Graphics();
            this.bossHPBarBg.roundRect(x - 2, y - 2, barWidth + 4, barHeight + 4, 4);
            this.bossHPBarBg.fill({ color: 0x000000, alpha: 0.7 });
            this.uiContainer.addChild(this.bossHPBarBg);

            // Fill
            this.bossHPBarFill = new PIXI.Graphics();
            this.bossHPBarFill.roundRect(x, y, barWidth, barHeight, 3);
            this.bossHPBarFill.fill(isFinal ? 0xff2222 : 0xff6600);
            this.uiContainer.addChild(this.bossHPBarFill);

            // Label
            this.bossHPBarText = new PIXI.Text({
                text: isFinal ? '💀 FINAL BOSS' : '⚔️ BOSS',
                style: {
                    fontFamily: 'Arial', fontSize: 12, fontWeight: 'bold',
                    fill: 0xffffff, stroke: { color: 0x000000, width: 2 }
                }
            });
            this.bossHPBarText.anchor.set(0.5, 0);
            this.bossHPBarText.position.set(this.vpWidth / 2, y - 18);
            this.uiContainer.addChild(this.bossHPBarText);

            this._bossBarX = x;
            this._bossBarY = y;
            this._bossBarW = barWidth;
            this._bossBarH = barHeight;
            this._bossBarFinal = isFinal;
        }

        updateBossHPBar() {
            if (!this.activeBoss || !this.bossHPBarFill) return;

            const ratio = Math.max(0, this.activeBoss.hp / this.activeBoss.maxHp);
            const barWidth = this._bossBarW * ratio;

            // Redraw fill
            this.bossHPBarFill.clear();
            if (barWidth > 0) {
                this.bossHPBarFill.roundRect(this._bossBarX, this._bossBarY, barWidth, this._bossBarH, 3);
                this.bossHPBarFill.fill(this._bossBarFinal ? 0xff2222 : 0xff6600);
            }
        }

        hideBossHPBar() {
            if (this.bossHPBarBg) { try { this.uiContainer.removeChild(this.bossHPBarBg); } catch (_) { } this.bossHPBarBg = null; }
            if (this.bossHPBarFill) { try { this.uiContainer.removeChild(this.bossHPBarFill); } catch (_) { } this.bossHPBarFill = null; }
            if (this.bossHPBarText) { try { this.uiContainer.removeChild(this.bossHPBarText); } catch (_) { } this.bossHPBarText = null; }
        }

        flashScreen(color) {
            const flash = new PIXI.Graphics();
            flash.rect(0, 0, this.vpWidth, this.vpHeight);
            flash.fill({ color: color, alpha: 0.35 });
            this.uiContainer.addChild(flash);
            setTimeout(() => {
                if (this.uiContainer) {
                    try { this.uiContainer.removeChild(flash); } catch (_) { }
                }
            }, 300);
        }

        _buildStatsString() {
            const atkSpd = (1 / this.attackCooldownMult).toFixed(1);
            return `⚔ DMG ${this.attackDamage}  ⚡ SPD x${atkSpd}\n🎯 RNG ${Math.round(this.attackRange)}  🏃 MOV ${Math.round(this.playerSpeed)}\n🪙 DROP x${this.coinDropMult}  ❤ HP ${this.maxHP}`;
        }

        // ─── Object Pools ─────────────────────────────────────────
        _initPools() {
            // Projectile pool: reuse Graphics for player/companion projectiles
            this._projectilePool = new ObjectPool(
                () => {
                    const gfx = new PIXI.Graphics();
                    return gfx;
                },
                (gfx) => {
                    gfx.clear();
                    gfx.alpha = 1;
                    gfx.visible = false;
                },
                30 // pre-allocate
            );

            // XP orb pool
            this._orbPool = new ObjectPool(
                () => {
                    const gfx = new PIXI.Graphics();
                    return gfx;
                },
                (gfx) => {
                    gfx.clear();
                    gfx.alpha = 1;
                    gfx.visible = false;
                },
                40
            );

            // Particle pool
            this._particlePool = new ObjectPool(
                () => {
                    const gfx = new PIXI.Graphics();
                    return gfx;
                },
                (gfx) => {
                    gfx.clear();
                    gfx.alpha = 1;
                    gfx.visible = false;
                },
                50
            );
        }

        _getProjectileGfx(color, radius) {
            const gfx = this._projectilePool.get();
            gfx.clear();
            gfx.circle(0, 0, radius);
            gfx.fill(color);
            gfx.circle(0, 0, radius + 2);
            gfx.fill({ color: color, alpha: 0.3 });
            gfx.visible = true;
            gfx.alpha = 1;
            return gfx;
        }

        _releaseProjectileGfx(gfx) {
            gfx.visible = false;
            if (gfx.parent) gfx.parent.removeChild(gfx);
            this._projectilePool.release(gfx);
        }

        _getOrbGfx() {
            const gfx = this._orbPool.get();
            gfx.clear();
            gfx.circle(0, 0, XP_ORB_RADIUS + 1);
            gfx.fill(0xffd700);
            gfx.circle(0, 0, XP_ORB_RADIUS - 1);
            gfx.fill(0xffb300);
            gfx.circle(0, 0, 2);
            gfx.fill(0xffd700);
            gfx.visible = true;
            gfx.alpha = 1;
            return gfx;
        }

        _releaseOrbGfx(gfx) {
            gfx.visible = false;
            if (gfx.parent) gfx.parent.removeChild(gfx);
            this._orbPool.release(gfx);
        }

        _getParticleGfx(color) {
            const gfx = this._particlePool.get();
            gfx.clear();
            gfx.circle(0, 0, 2 + Math.random() * 3);
            gfx.fill(color || 0xff4444);
            gfx.visible = true;
            gfx.alpha = 1;
            return gfx;
        }

        _releaseParticleGfx(gfx) {
            gfx.visible = false;
            if (gfx.parent) gfx.parent.removeChild(gfx);
            this._particlePool.release(gfx);
        }

        spawnXPOrb(x, y) {
            const gfx = this._getOrbGfx();
            gfx.position.set(x, y);
            this.worldContainer.addChild(gfx);
            this.xpOrbs.push({ gfx, x, y, lifetime: 8.0 });
        }

        spawnDeathParticles(x, y, color) {
            for (let i = 0; i < 6; i++) {
                const angle = (Math.PI * 2 / 6) * i + Math.random() * 0.5;
                const speed = 40 + Math.random() * 60;
                const gfx = this._getParticleGfx(color);
                gfx.position.set(x, y);
                this.worldContainer.addChild(gfx);
                this.particles.push({
                    gfx,
                    vx: Math.cos(angle) * speed,
                    vy: Math.sin(angle) * speed,
                    lifetime: 0.5 + Math.random() * 0.3,
                    age: 0
                });
            }
        }

        update(ticker) {
            if (!this.alive || this.won || gamePaused || this.upgradePaused) return;

            const dt = ticker.deltaMS / 1000;

            // Update timers
            this.timeElapsed += dt;
            if (!this.timerStopped) {
                this.timeRemaining -= dt;
            }
            this.spawnTimer += dt;

            // Per-minute difficulty ramp: biome-specific spawn & speed scaling
            const currentMinute = Math.floor(this.timeElapsed / 60);
            if (currentMinute > this.lastMinuteRamp) {
                const newMinutes = currentMinute - this.lastMinuteRamp;
                this.spawnRampBonus += newMinutes * this.spawnRampPerMinute;
                this.speedRampBonus += newMinutes * this.speedRampPerMinute;
                this.lastMinuteRamp = currentMinute;
            }

            // Speed ramp: enemies get faster over time within the level
            this.speedRampTimer += dt;
            const rampFactor = 1 + (this.speedRampTimer / this.timerDuration) * 0.8 + this.speedRampBonus;
            this.currentEnemySpeed = Math.min(this.enemySpeed * rampFactor, this.maxEnemySpeed);

            // Boss spawn at half-time (only if we have boss sprites and not final level)
            const hasBosses = this.bossSpriteAliases && this.bossSpriteAliases.length > 0 && !this.isFinalLevel;
            if (hasBosses && !this.midBossSpawned && this.timeRemaining <= this.timerDuration / 2) {
                this.midBossSpawned = true;
                this.spawnBoss(false);
            }

            // Check timer reaching zero
            if (this.timeRemaining <= 0) {
                this.timeRemaining = 0;

                if (this.isFinalLevel || !hasBosses) {
                    // Void level or no bosses: timer expiry = instant win
                    this.won = true;
                    this.onWin();
                    return;
                }

                // Spawn final boss if not yet spawned
                if (!this.finalBossSpawned) {
                    this.finalBossSpawned = true;
                    this.timerStopped = true;
                    this.spawnBoss(true);
                }
                // If final boss is spawned, we wait for it to die (handled in enemy kill logic)
            }

            // Update boss HP bar
            if (this.activeBoss) {
                this.updateBossHPBar();
                // Pulse boss aura glow
                if (this.activeBoss.container && this.activeBoss.container.children[0]) {
                    this.activeBoss.container.children[0].alpha = 0.15 + Math.sin(this.timeElapsed * 4) * 0.1;
                }
            }

            // Invulnerability tick
            if (this.invulnTimer > 0) {
                this.invulnTimer -= dt;
                // Flash player
                if (this.player) this.player.alpha = (Math.floor(this.timeElapsed / 0.08) % 2 === 0) ? 0.4 : 1.0;
            } else if (this.player) {
                this.player.alpha = 1.0;
            }

            // Update UI
            this.timerText.text = formatTime(this.timeRemaining);
            this.updateTimerBar();
            this.updateHPBar();
            if (this.hpText) this.hpText.text = `${Math.ceil(this.playerHP)}/${this.maxHP}`;
            this.killText.text = `☠ ${this.enemiesKilled}`;
            if (this.orbText) this.orbText.text = `🪙 ${this.xpOrbsCollected}`;
            if (this.statsText) this.statsText.text = this._buildStatsString();

            // Player movement
            this.updatePlayer(dt);

            // Camera follow
            this.updateCamera();

            // Spawn enemies (more as time passes, biome-specific scaling)
            if (this.spawnTimer >= this.spawnInterval) {
                this.spawnTimer = 0;
                // Ramp up spawns: starts at 3, scales with time and biome ramp
                const timeScale = Math.floor(this.timeElapsed / 5);
                const rampMult = 1 + this.spawnRampBonus; // multiplicative: 1 + accumulated ramp
                const baseSpawn = Math.ceil((3 + timeScale) * rampMult);
                // Additional burst every 35s
                const burstBonus = Math.floor(this.timeElapsed / 35) * 5;
                const toSpawn = Math.min(baseSpawn + burstBonus, 30);
                for (let i = 0; i < toSpawn; i++) {
                    this.spawnEnemy();
                }
            }

            // Update enemies
            this.updateEnemies(dt);

            // Auto-attack nearest enemy
            this.updateAutoAttack(dt);

            // Update companion PIGs
            this.updateCompanions(dt);

            // Update projectiles
            this.updateProjectiles(dt);

            // Update XP orbs
            this.updateXPOrbs(dt);

            // Update particles
            this.updateParticles(dt);

            // Collision detection
            this.rebuildSpatialGrid();
            this.checkCollisions();

            // Update minimap
            this.updateMinimap();

            // Player glow pulse
            if (this.playerGlow) {
                this.playerGlow.alpha = 0.1 + Math.sin(this.timeElapsed * 3) * 0.08;
            }
        }

        updatePlayer(dt) {
            let dx = 0, dy = 0;

            // Keyboard input (WASD or arrow keys)
            if (this.keys['w'] || this.keys['arrowup']) dy -= 1;
            if (this.keys['s'] || this.keys['arrowdown']) dy += 1;
            if (this.keys['a'] || this.keys['arrowleft']) dx -= 1;
            if (this.keys['d'] || this.keys['arrowright']) dx += 1;

            // Joystick input
            if (this.joystickActive && this.joystickMagnitude > 0.1) {
                dx = Math.cos(this.joystickAngle) * this.joystickMagnitude;
                dy = Math.sin(this.joystickAngle) * this.joystickMagnitude;
            }

            // Touch/click-to-move
            if (this.touchActive) {
                const tdx = this.touchTarget.x - this.playerX;
                const tdy = this.touchTarget.y - this.playerY;
                const td = Math.sqrt(tdx * tdx + tdy * tdy);
                if (td > 5) {
                    dx = tdx / td;
                    dy = tdy / td;
                }
            }

            // Normalize diagonal movement
            const mag = Math.sqrt(dx * dx + dy * dy);
            if (mag > 0) {
                dx /= mag;
                dy /= mag;
            }

            // Apply movement
            this.playerX += dx * this.playerSpeed * dt;
            this.playerY += dy * this.playerSpeed * dt;

            // Clamp to map bounds
            this.playerX = clamp(this.playerX, PLAYER_RADIUS, this.mapWidth - PLAYER_RADIUS);
            this.playerY = clamp(this.playerY, PLAYER_RADIUS, this.mapHeight - PLAYER_RADIUS);

            this.player.position.set(this.playerX, this.playerY);

            // Rotate player direction indicator
            if (this.playerArrow && mag > 0) {
                this.playerArrow.rotation = Math.atan2(dy, dx);
            }
        }

        updateCamera() {
            // Smooth camera follow
            const targetCamX = this.playerX - this.vpWidth / 2;
            const targetCamY = this.playerY - this.vpHeight / 2;

            this.camX = lerp(this.camX, targetCamX, 0.1);
            this.camY = lerp(this.camY, targetCamY, 0.1);

            // Clamp camera to map bounds
            this.camX = clamp(this.camX, 0, this.mapWidth - this.vpWidth);
            this.camY = clamp(this.camY, 0, this.mapHeight - this.vpHeight);

            this.worldContainer.position.set(-this.camX, -this.camY);
        }

        updateEnemies(dt) {
            const cullMargin = 200; // hide enemies well outside viewport
            const camLeft = this.camX - cullMargin;
            const camRight = this.camX + this.vpWidth + cullMargin;
            const camTop = this.camY - cullMargin;
            const camBottom = this.camY + this.vpHeight + cullMargin;

            for (const enemy of this.enemies) {
                if (!enemy.alive) continue;

                // Move toward player
                const dx = this.playerX - enemy.x;
                const dy = this.playerY - enemy.y;
                const d = Math.sqrt(dx * dx + dy * dy);

                if (d > 1) {
                    // Add slight wobble for organic movement
                    enemy.wobblePhase += dt * 3;
                    const wobbleX = Math.sin(enemy.wobblePhase) * 15;
                    const wobbleY = Math.cos(enemy.wobblePhase * 0.7) * 15;

                    const moveX = (dx / d + wobbleX / d) * enemy.speed * dt;
                    const moveY = (dy / d + wobbleY / d) * enemy.speed * dt;

                    enemy.x += moveX;
                    enemy.y += moveY;

                    // Clamp to map
                    enemy.x = clamp(enemy.x, 0, this.mapWidth);
                    enemy.y = clamp(enemy.y, 0, this.mapHeight);

                    enemy.container.position.set(enemy.x, enemy.y);

                    // Face player
                    enemy.container.scale.x = dx > 0 ? Math.abs(enemy.container.scale.x) : -Math.abs(enemy.container.scale.x);
                }

                // Visibility culling: hide containers outside viewport to skip rendering
                const visible = enemy.x >= camLeft && enemy.x <= camRight &&
                                enemy.y >= camTop && enemy.y <= camBottom;
                enemy.container.visible = visible;
            }
        }

        updateXPOrbs(dt) {
            for (let i = this.xpOrbs.length - 1; i >= 0; i--) {
                const orb = this.xpOrbs[i];
                orb.lifetime -= dt;

                // Attracted to player when close
                const dx = this.playerX - orb.x;
                const dy = this.playerY - orb.y;
                const d = Math.sqrt(dx * dx + dy * dy);

                if (d < this.magnetRadius) {
                    // Attract toward player
                    const speed = XP_ORB_SPEED * (1 - d / this.magnetRadius);
                    orb.x += (dx / d) * speed * dt;
                    orb.y += (dy / d) * speed * dt;
                    orb.gfx.position.set(orb.x, orb.y);

                    // Picked up
                    if (d < PLAYER_RADIUS) {
                        this._releaseOrbGfx(orb.gfx);
                        this.xpOrbs.splice(i, 1);
                        this.xpOrbsCollected++;
                        // Check upgrade threshold
                        if (this.xpOrbsCollected >= this.nextUpgradeAt) {
                            this.showUpgradePopup();
                        }
                        continue;
                    }
                }

                // Expire
                if (orb.lifetime <= 0) {
                    this._releaseOrbGfx(orb.gfx);
                    this.xpOrbs.splice(i, 1);
                    continue;
                }

                // Fade when close to expiring
                if (orb.lifetime < 2) {
                    orb.gfx.alpha = orb.lifetime / 2;
                }
            }
        }

        updateParticles(dt) {
            for (let i = this.particles.length - 1; i >= 0; i--) {
                const p = this.particles[i];
                p.age += dt;
                p.gfx.position.x += p.vx * dt;
                p.gfx.position.y += p.vy * dt;
                p.gfx.alpha = 1 - (p.age / p.lifetime);

                if (p.age >= p.lifetime) {
                    this._releaseParticleGfx(p.gfx);
                    this.particles.splice(i, 1);
                }
            }
        }

        // ─── Auto-Attack System ───────────────────────────────────
        updateAutoAttack(dt) {
            this.attackCooldown -= dt;
            if (this.attackCooldown > 0) return;

            // Find nearest alive enemy within range
            let nearest = null;
            let nearestDist = this.attackRange;

            for (const enemy of this.enemies) {
                if (!enemy.alive) continue;
                const d = dist({ x: this.playerX, y: this.playerY }, { x: enemy.x, y: enemy.y });
                if (d < nearestDist) {
                    nearestDist = d;
                    nearest = enemy;
                }
            }

            if (!nearest) return;

            // Fire projectile toward nearest enemy
            this.attackCooldown = ATTACK_COOLDOWN * this.attackCooldownMult;
            const dx = nearest.x - this.playerX;
            const dy = nearest.y - this.playerY;
            const d = Math.sqrt(dx * dx + dy * dy);
            const vx = (dx / d) * PROJECTILE_SPEED;
            const vy = (dy / d) * PROJECTILE_SPEED;

            const gfx = this._getProjectileGfx(0x4fc3f7, PROJECTILE_RADIUS);
            gfx.position.set(this.playerX, this.playerY);
            this.worldContainer.addChild(gfx);

            this.projectiles.push({
                gfx,
                x: this.playerX,
                y: this.playerY,
                vx,
                vy,
                damage: this.attackDamage,
                lifetime: PROJECTILE_LIFETIME,
                age: 0
            });

            // Rotate player arrow toward the target
            if (this.playerArrow) {
                this.playerArrow.rotation = Math.atan2(dy, dx);
            }
        }

        updateProjectiles(dt) {
            for (let i = this.projectiles.length - 1; i >= 0; i--) {
                const proj = this.projectiles[i];
                proj.age += dt;
                proj.x += proj.vx * dt;
                proj.y += proj.vy * dt;
                proj.gfx.position.set(proj.x, proj.y);

                // Remove if expired or out of map
                if (proj.age >= proj.lifetime ||
                    proj.x < -50 || proj.x > this.mapWidth + 50 ||
                    proj.y < -50 || proj.y > this.mapHeight + 50) {
                    this._releaseProjectileGfx(proj.gfx);
                    this.projectiles.splice(i, 1);
                    continue;
                }

                // Check collision with enemies
                let hit = false;
                for (let j = this.enemies.length - 1; j >= 0; j--) {
                    const enemy = this.enemies[j];
                    if (!enemy.alive) continue;

                    // Skip enemies already hit by this projectile
                    if (proj.hitEnemies && proj.hitEnemies.has(enemy)) continue;

                    const d = dist({ x: proj.x, y: proj.y }, { x: enemy.x, y: enemy.y });
                    if (d < PROJECTILE_RADIUS + enemy.hitRadius) {
                        // Hit enemy!
                        enemy.hp -= proj.damage;

                        // Flash enemy white briefly
                        enemy.container.alpha = 0.5;
                        setTimeout(() => {
                            if (enemy.container) enemy.container.alpha = 1;
                        }, 80);

                        if (enemy.hp <= 0) {
                            // Enemy killed
                            enemy.alive = false;
                            this.spawnDeathParticles(enemy.x, enemy.y, enemy.isBoss ? 0xff6600 : (enemy.isElite ? 0xffff00 : 0xff4444));
                            // Drop coins based on coinDropMult (bosses drop extra)
                            const coinCount = enemy.isBoss ? this.coinDropMult * 5 : this.coinDropMult;
                            for (let c = 0; c < coinCount; c++) {
                                const ox = (c === 0) ? 0 : (Math.random() - 0.5) * 30;
                                const oy = (c === 0) ? 0 : (Math.random() - 0.5) * 30;
                                this.spawnXPOrb(enemy.x + ox, enemy.y + oy);
                            }
                            this._releaseEnemyContainer(enemy.container);
                            this.enemies.splice(j, 1);
                            this.enemiesKilled++;
                            this.playSFX('hit');

                            // Boss kill handling
                            if (enemy.isBoss) {
                                this.hideBossHPBar();
                                if (this.activeBoss === enemy) this.activeBoss = null;

                                if (enemy.isFinalBoss) {
                                    // Final boss killed → level complete!
                                    this.won = true;
                                    this.onWin();
                                    return;
                                }
                            }
                        }

                        // Projectile pierces through — don't remove it
                        // Track which enemies this projectile already hit
                        if (!proj.hitEnemies) proj.hitEnemies = new Set();
                        proj.hitEnemies.add(enemy);
                    }
                }
            }
        }

        rebuildSpatialGrid() {
            this._spatialGrid.clear();
            for (let i = 0; i < this.enemies.length; i++) {
                const e = this.enemies[i];
                if (e.alive) this._spatialGrid.insert(e);
            }
        }

        checkCollisions() {
            if (this.invulnTimer > 0) return; // invulnerable, skip

            const nearby = this._spatialGrid.query(this.playerX, this.playerY);
            for (let i = 0; i < nearby.length; i++) {
                const enemy = nearby[i];
                if (!enemy.alive) continue;

                const d = dist({ x: this.playerX, y: this.playerY }, { x: enemy.x, y: enemy.y });
                if (d < PLAYER_RADIUS + enemy.hitRadius) {
                    // Take damage
                    const dmg = enemy.isBoss ? BOSS_DAMAGE : (enemy.isElite ? ELITE_DAMAGE : ENEMY_DAMAGE);
                    this.playerHP -= dmg;
                    this.invulnTimer = INVULN_DURATION;
                    this.playSFX('hit');

                    // Knockback: push player away from enemy
                    const kbDist = 40;
                    const angle = Math.atan2(this.playerY - enemy.y, this.playerX - enemy.x);
                    this.playerX += Math.cos(angle) * kbDist;
                    this.playerY += Math.sin(angle) * kbDist;
                    this.playerX = clamp(this.playerX, PLAYER_RADIUS, this.mapWidth - PLAYER_RADIUS);
                    this.playerY = clamp(this.playerY, PLAYER_RADIUS, this.mapHeight - PLAYER_RADIUS);
                    this.player.position.set(this.playerX, this.playerY);

                    if (this.playerHP <= 0) {
                        this.playerHP = 0;
                        this.alive = false;
                        this.onDeath();
                        return;
                    }
                    return; // only one hit per frame
                }
            }
        }

        updateHPBar() {
            if (!this.hpBar) return;
            this.hpBar.clear();
            const progress = clamp(this.playerHP / this.maxHP, 0, 1);
            const barW = this.hpBarWidth * progress;

            // Always red-themed: bright red → dark red as HP drops
            let color;
            if (progress > 0.6) color = 0xe53935;
            else if (progress > 0.3) color = 0xc62828;
            else color = 0xb71c1c;

            this.hpBar.roundRect(this.hpBarX, this.hpBarY, barW, this.hpBarHeight, 5);
            this.hpBar.fill(color);
        }

        updateTimerBar() {
            if (!this.timerBar) return;
            this.timerBar.clear();
            const progress = clamp(this.timeRemaining / this.timerDuration, 0, 1);
            const barW = this.timerBarWidth * progress;

            // Color: green → yellow → red
            let color;
            if (progress > 0.5) color = 0x43a047;
            else if (progress > 0.25) color = 0xffb300;
            else color = 0xe53935;

            this.timerBar.roundRect(this.timerBarX, this.timerBarY, barW, this.timerBarHeight, 6);
            this.timerBar.fill(color);
        }

        updateMinimap() {
            if (!this.minimapContainer) return;

            const scaleX = MINIMAP_SIZE / this.mapWidth;
            const scaleY = MINIMAP_SIZE / this.mapHeight;

            // Redraw player dot (blue)
            this._mmPlayerDot.clear();
            this._mmPlayerDot.circle(this.playerX * scaleX, this.playerY * scaleY, 3);
            this._mmPlayerDot.fill(0x4fc3f7);

            // Redraw enemy dots (red) — batch into single draw call
            this._mmEnemyDots.clear();
            for (const enemy of this.enemies) {
                if (!enemy.alive) continue;
                this._mmEnemyDots.circle(enemy.x * scaleX, enemy.y * scaleY, enemy.isElite ? 2 : 1);
            }
            this._mmEnemyDots.fill(0xe53935);

            // Redraw viewport rect
            this._mmVpRect.clear();
            this._mmVpRect.rect(this.camX * scaleX, this.camY * scaleY,
                this.vpWidth * scaleX, this.vpHeight * scaleY);
            this._mmVpRect.setStrokeStyle({ width: 1, color: 0xffffff, alpha: 0.5 });
            this._mmVpRect.stroke();
        }

        // ─── Companion PIG System ─────────────────────────────────
        spawnCompanion() {
            const companionIdx = this.companions.length;
            const container = new PIXI.Container();

            // Pig body (pink circle)
            const body = new PIXI.Graphics();
            body.circle(0, 0, 12);
            body.fill(0xffb6c1);
            body.setStrokeStyle({ width: 1.5, color: 0xff69b4 });
            body.stroke();
            container.addChild(body);

            // Pig snout
            const snout = new PIXI.Graphics();
            snout.ellipse(10, 0, 5, 4);
            snout.fill(0xff9999);
            snout.circle(12, -1.5, 1);
            snout.fill(0xcc6666);
            snout.circle(12, 1.5, 1);
            snout.fill(0xcc6666);
            container.addChild(snout);

            // Pig ears
            const ear = new PIXI.Graphics();
            ear.moveTo(-5, -10);
            ear.lineTo(0, -16);
            ear.lineTo(5, -10);
            ear.closePath();
            ear.fill(0xff8da1);
            container.addChild(ear);

            // Pig eyes
            const eyes = new PIXI.Graphics();
            eyes.circle(3, -4, 2);
            eyes.fill(0x222222);
            container.addChild(eyes);

            container.position.set(this.playerX, this.playerY);
            this.worldContainer.addChild(container);

            this.companions.push({
                container: container,
                x: this.playerX,
                y: this.playerY,
                orbitAngle: companionIdx * (Math.PI * 2 / Math.max(this.companions.length + 1, 1)),
                attackCooldown: 0,
                attackRange: this.attackRange,
                orbitRadius: 50 + companionIdx * 20
            });
        }

        updateCompanions(dt) {
            if (this.companions.length === 0) return;

            for (const comp of this.companions) {
                // Orbit around player
                comp.orbitAngle += dt * 1.5;
                const targetX = this.playerX + Math.cos(comp.orbitAngle) * comp.orbitRadius;
                const targetY = this.playerY + Math.sin(comp.orbitAngle) * comp.orbitRadius;

                // Smoothly follow 
                comp.x = lerp(comp.x, targetX, dt * 5);
                comp.y = lerp(comp.y, targetY, dt * 5);
                comp.container.position.set(comp.x, comp.y);

                // Face movement direction
                const dx = targetX - comp.x;
                if (Math.abs(dx) > 0.5) {
                    comp.container.scale.x = dx > 0 ? 1 : -1;
                }

                // Auto-attack
                comp.attackCooldown -= dt;
                if (comp.attackCooldown <= 0) {
                    // Find nearest enemy
                    let nearest = null;
                    let nearestDist = comp.attackRange;

                    for (const enemy of this.enemies) {
                        if (!enemy.alive) continue;
                        const d = dist({ x: comp.x, y: comp.y }, { x: enemy.x, y: enemy.y });
                        if (d < nearestDist) {
                            nearestDist = d;
                            nearest = enemy;
                        }
                    }

                    if (nearest) {
                        // Mirror player's attack speed and damage
                        comp.attackCooldown = ATTACK_COOLDOWN * this.attackCooldownMult;
                        const pdx = nearest.x - comp.x;
                        const pdy = nearest.y - comp.y;
                        const pd = Math.sqrt(pdx * pdx + pdy * pdy);
                        const vx = (pdx / pd) * PROJECTILE_SPEED;
                        const vy = (pdy / pd) * PROJECTILE_SPEED;

                        // Pink projectile
                        const gfx = this._getProjectileGfx(0xff69b4, 3);
                        gfx.position.set(comp.x, comp.y);
                        this.worldContainer.addChild(gfx);

                        this.projectiles.push({
                            gfx,
                            x: comp.x,
                            y: comp.y,
                            vx, vy,
                            damage: this.attackDamage, // mirror player damage
                            lifetime: PROJECTILE_LIFETIME,
                            age: 0
                        });
                    }
                }
            }
        }

        // ─── Upgrade Popup System ─────────────────────────────────
        showUpgradePopup() {
            if (this.upgradePaused) return; // already showing

            // Check if we've reached the power limit (mirrors timer in minutes)
            if (this.upgradesPicked >= this.maxPowerUps) {
                return; // no more powers allowed this level
            }

            this.upgradePaused = true;

            // Advance to next threshold
            this.upgradeIndex++;
            if (this.upgradeIndex < UPGRADE_THRESHOLDS.length) {
                this.nextUpgradeAt = UPGRADE_THRESHOLDS[this.upgradeIndex];
            } else {
                this.nextUpgradeAt += UPGRADE_THRESHOLD_STEP;
            }

            // Pick 3 random upgrades, excluding unique already picked and those at maxPicks
            const available = UPGRADE_DEFS.filter(u => {
                if (u.unique && this.pickedUpgradeIds.has(u.id)) return false;
                if (u.maxPicks && (this.upgradePickCounts[u.id] || 0) >= u.maxPicks) return false;
                return true;
            });
            const shuffled = [...available].sort(() => Math.random() - 0.5);
            const choices = shuffled.slice(0, 3);

            if (choices.length === 0) {
                this.upgradePaused = false;
                return;
            }

            // Dark overlay
            const overlay = new PIXI.Container();
            const bg = new PIXI.Graphics();
            bg.rect(0, 0, this.vpWidth, this.vpHeight);
            bg.fill({ color: 0x000000, alpha: 0.75 });
            bg.eventMode = 'static'; // block clicks through
            overlay.addChild(bg);

            // Title
            const title = new PIXI.Text({
                text: 'ESCOLHE UM UPGRADE',
                style: {
                    fontFamily: 'Arial', fontSize: 22, fontWeight: 'bold',
                    fill: 0xffd700, align: 'center',
                    dropShadow: { color: 0x000000, blur: 4, distance: 2 }
                }
            });
            title.anchor.set(0.5, 0.5);
            title.position.set(this.vpWidth / 2, this.vpHeight * 0.18);
            overlay.addChild(title);

            // Cards
            const cardW = Math.min(130, (this.vpWidth - 60) / 3);
            const cardH = 160;
            const gap = 12;
            const totalW = cardW * 3 + gap * 2;
            const startX = (this.vpWidth - totalW) / 2;
            const cardY = this.vpHeight / 2 - cardH / 2;

            choices.forEach((upg, idx) => {
                const cx = startX + idx * (cardW + gap);
                const card = new PIXI.Container();
                card.eventMode = 'static';
                card.cursor = 'pointer';

                // Card background
                const cardBg = new PIXI.Graphics();
                cardBg.roundRect(0, 0, cardW, cardH, 10);
                cardBg.fill({ color: 0x1a1a3e, alpha: 0.95 });
                cardBg.setStrokeStyle({ width: 2, color: 0xffd700, alpha: 0.8 });
                cardBg.stroke();
                card.addChild(cardBg);

                // Hover highlight (hidden by default)
                const hoverBg = new PIXI.Graphics();
                hoverBg.roundRect(0, 0, cardW, cardH, 10);
                hoverBg.fill({ color: 0x2a2a5e, alpha: 0.95 });
                hoverBg.setStrokeStyle({ width: 3, color: 0xffee55 });
                hoverBg.stroke();
                hoverBg.visible = false;
                card.addChild(hoverBg);

                // Icon
                const icon = new PIXI.Text({
                    text: upg.icon,
                    style: { fontSize: 36 }
                });
                icon.anchor.set(0.5, 0.5);
                icon.position.set(cardW / 2, 35);
                card.addChild(icon);

                // Title
                const tText = new PIXI.Text({
                    text: upg.title,
                    style: {
                        fontFamily: 'Arial', fontSize: 14, fontWeight: 'bold',
                        fill: 0xffffff, align: 'center',
                        wordWrap: true, wordWrapWidth: cardW - 16
                    }
                });
                tText.anchor.set(0.5, 0);
                tText.position.set(cardW / 2, 62);
                card.addChild(tText);

                // Description
                const dText = new PIXI.Text({
                    text: upg.desc,
                    style: {
                        fontFamily: 'Arial', fontSize: 11,
                        fill: 0xbbbbbb, align: 'center',
                        wordWrap: true, wordWrapWidth: cardW - 16
                    }
                });
                dText.anchor.set(0.5, 0);
                dText.position.set(cardW / 2, 90);
                card.addChild(dText);

                // Click handler
                card.on('pointerdown', () => {
                    this.applyUpgrade(upg);
                    this.uiContainer.removeChild(overlay);
                    this.upgradeOverlay = null;
                    this.upgradePaused = false;
                });

                // Hover effects
                card.on('pointerover', () => {
                    hoverBg.visible = true;
                    cardBg.visible = false;
                });
                card.on('pointerout', () => {
                    hoverBg.visible = false;
                    cardBg.visible = true;
                });

                card.position.set(cx, cardY);
                overlay.addChild(card);
            });

            this.upgradeOverlay = overlay;
            this.uiContainer.addChild(overlay);
        }

        applyUpgrade(upg) {
            upg.apply(this);
            this.upgradesPicked++;
            this.upgradePickCounts[upg.id] = (this.upgradePickCounts[upg.id] || 0) + 1;
            if (upg.unique) this.pickedUpgradeIds.add(upg.id);

            // Brief flash effect to confirm selection
            const flash = new PIXI.Graphics();
            flash.rect(0, 0, this.vpWidth, this.vpHeight);
            flash.fill({ color: 0xffd700, alpha: 0.2 });
            this.uiContainer.addChild(flash);
            setTimeout(() => {
                if (this.uiContainer) {
                    try { this.uiContainer.removeChild(flash); } catch (_) { }
                }
            }, 200);
        }

        onWin() {
            gameActive = false;
            const bossKill = this.finalBossSpawned;
            const title = bossKill ? 'BOSS DEFEATED!' : 'SURVIVED!';
            this.showMessage(title, 0x43a047, `Level ${this.level} Complete!`);

            // Play victory sound effect
            this.playSFX('win');

            // Notify .NET
            setTimeout(() => {
                if (dotNetRef) {
                    try {
                        dotNetRef.invokeMethodAsync('OnLevelComplete', this.enemiesKilled, this.timeElapsed, this.xpOrbsCollected);
                    } catch (e) {
                        console.error('Failed to invoke OnLevelComplete:', e);
                    }
                }
            }, 2000);
        }

        onDeath() {
            gameActive = false;
            this.spawnDeathParticles(this.playerX, this.playerY, 0x4fc3f7);

            // Flash player red
            if (this.player) this.player.alpha = 0.3;

            this.showMessage('SURVIVAL ENDED', 0xe53935, `Survived ${formatTime(this.timeElapsed)}`);

            // Play death sound
            this.playSFX('death');

            // Notify .NET
            setTimeout(() => {
                if (dotNetRef) {
                    try {
                        dotNetRef.invokeMethodAsync('OnPlayerDeath', this.enemiesKilled, this.timeElapsed, this.xpOrbsCollected);
                    } catch (e) {
                        console.error('Failed to invoke OnPlayerDeath:', e);
                    }
                }
            }, 2000);
        }

        showMessage(title, color, subtitle) {
            const overlay = new PIXI.Graphics();
            overlay.rect(0, 0, this.vpWidth, this.vpHeight);
            overlay.fill({ color: 0x000000, alpha: 0.6 });
            this.uiContainer.addChild(overlay);

            const text = new PIXI.Text({
                text: title,
                style: {
                    fontFamily: 'Arial', fontSize: 48, fontWeight: 'bold',
                    fill: color, stroke: { color: 0x000000, width: 4 },
                    align: 'center'
                }
            });
            text.anchor.set(0.5, 0.5);
            text.position.set(this.vpWidth / 2, this.vpHeight / 2 - 20);
            this.uiContainer.addChild(text);

            if (subtitle) {
                const sub = new PIXI.Text({
                    text: subtitle,
                    style: {
                        fontFamily: 'Arial', fontSize: 18,
                        fill: 0xffffff, align: 'center'
                    }
                });
                sub.anchor.set(0.5, 0.5);
                sub.position.set(this.vpWidth / 2, this.vpHeight / 2 + 30);
                this.uiContainer.addChild(sub);
            }
        }

        playMusic() {
            if (!audioEnabled) return;
            try {
                if (bgMusic) {
                    bgMusic.currentTime = 0;
                    bgMusic.play().catch(() => { });
                    return;
                }
                // Use the dedicated survival music track
                bgMusic = new Audio('/sound/survival_battle.mp3' + audioCacheBuster);
                bgMusic.loop = true;
                bgMusic.volume = 0.3;
                bgMusic.play().catch(() => { });
                bgMusicLoaded = true;
            } catch (_) { }
        }

        playSFX(type) {
            if (!audioEnabled) return;
            try {
                const audio = getPooledAudio(type);
                if (audio) audio.play().catch(() => { });
            } catch (_) { }
        }

        cleanup() {
            // Remove input handlers
            window.removeEventListener('keydown', this._onKeyDown);
            window.removeEventListener('keyup', this._onKeyUp);
            if (app && app.canvas) {
                if (this._onPointerDown) app.canvas.removeEventListener('pointerdown', this._onPointerDown);
                if (this._onPointerMove) app.canvas.removeEventListener('pointermove', this._onPointerMove);
                if (this._onPointerUp) app.canvas.removeEventListener('pointerup', this._onPointerUp);
            }

            // Remove ticker
            if (app && app.ticker) {
                try { app.ticker.remove(this.update, this); } catch (_) { }
            }

            // Release pooled objects still active
            for (const proj of this.projectiles) {
                if (proj.gfx) { proj.gfx.visible = false; if (proj.gfx.parent) proj.gfx.parent.removeChild(proj.gfx); }
            }
            for (const orb of this.xpOrbs) {
                if (orb.gfx) { orb.gfx.visible = false; if (orb.gfx.parent) orb.gfx.parent.removeChild(orb.gfx); }
            }
            for (const p of this.particles) {
                if (p.gfx) { p.gfx.visible = false; if (p.gfx.parent) p.gfx.parent.removeChild(p.gfx); }
            }

            // Clear containers
            if (this.worldContainer) {
                try { this.worldContainer.removeChildren(); } catch (_) { }
            }
            if (this.uiContainer) {
                try { this.uiContainer.removeChildren(); } catch (_) { }
            }

            this.enemies = [];
            // Drain enemy container pool
            for (const c of this._enemyContainerPool) {
                try { c.destroy({ children: true }); } catch (_) {}
            }
            this._enemyContainerPool = [];
            this.xpOrbs = [];
            this.particles = [];
            this.projectiles = [];
            this.keys = {};

            // Pools are discarded on cleanup since graphics belong to the old stage
            this._projectilePool = null;
            this._orbPool = null;
            this._particlePool = null;
        }

        destroy() {
            this.cleanup();

            // Stop music
            if (bgMusic) {
                try { bgMusic.pause(); bgMusic.currentTime = 0; } catch (_) { }
                bgMusic = null;
                bgMusicLoaded = false;
            }

            // Destroy PixiJS app
            if (app) {
                try {
                    app.stage.removeChildren();
                    app.destroy(true, { children: true, texture: false, baseTexture: false });
                } catch (_) { }
                app = null;
            }

            gameActive = false;
            scene = null;
        }
    }

    // ─── Public API ───────────────────────────────────────────────
    window.surviveModeGame = {
        /**
         * Start a new survive mode game.
         * @param {string} containerId - DOM element ID
         * @param {object} levelData - Level config from server
         * @param {object} netRef - DotNet object reference for callbacks
         */
        async start(containerId, levelData, netRef) {
            dotNetRef = netRef;

            if (scene) {
                scene.destroy();
            }

            scene = new SurviveScene(containerId, levelData);
            await scene.init();
        },

        /**
         * Start the next level without recreating the PixiJS app.
         * @param {object} levelData - Level config from server
         */
        async nextLevel(levelData) {
            if (scene) {
                scene.cleanup();
            }

            scene = new SurviveScene(scene ? scene.containerId : 'surviveGameContainer', levelData);
            await scene.init();
        },

        /**
         * Pause the game.
         */
        pause() {
            gamePaused = true;
        },

        /**
         * Resume the game.
         */
        resume() {
            gamePaused = false;
        },

        /**
         * Toggle audio.
         * @param {boolean} enabled
         */
        setAudioEnabled(enabled) {
            audioEnabled = enabled;
            if (bgMusic) {
                if (enabled) {
                    bgMusic.play().catch(() => { });
                } else {
                    bgMusic.pause();
                }
            }
        },

        /**
         * Destroy the game and clean up everything.
         */
        destroy() {
            if (scene) {
                scene.destroy();
                scene = null;
            }
            dotNetRef = null;
        },

        /**
         * Check if a game is currently active.
         */
        isActive() {
            return gameActive;
        }
    };
})();
