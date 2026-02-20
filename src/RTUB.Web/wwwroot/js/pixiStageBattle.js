/**
 * Stage Battle PixiJS Scene
 * Different layout from Arena - player at bottom, enemies at top
 * Supports multiple enemies, backgrounds, and region-specific sprites
 */
(function () {
    'use strict';

    const DEFAULT_WIDTH = 800;
    const DEFAULT_HEIGHT = 500;
    const DEFAULT_EVENT_INTERVAL = 400; // Reduced from 600ms to 400ms for faster battles

    let stageApp = null;
    let stageScene = null;
    
    // Global audio state - persists between battles
    let globalAudioEnabled = true;
    let globalSfxVolume = 0.5;
    let backgroundMusic = null;
    let backgroundMusicGainNode = null;
    let currentMusicType = null; // 'stage' or 'boss' — tracks which track is playing
    // Cache decoded AudioBuffers so music files are only fetched/decoded once per session
    const audioBufferCache = {};
    // Cache-bust version — refreshes audio once per page session
    const audioCacheBuster = `?v=${Date.now()}`;

    // Session-level cache bust — set once per page load so the browser
    // can reuse HTTP-cached sprites across stage transitions.
    const SESSION_CACHE_BUST = `?v=${Date.now()}`;
    // Track which asset paths are already loaded in PIXI.Assets to skip re-fetches
    const loadedAssetAliases = new Set();

    // Shared AudioContext — reused across StageBattleScene instances to avoid leaks
    let sharedAudioContext = null;
    function getSharedAudioContext() {
        if (!sharedAudioContext || sharedAudioContext.state === 'closed') {
            try {
                sharedAudioContext = new (window.AudioContext || window.webkitAudioContext)();
            } catch (e) {
                console.warn('AudioContext not available:', e);
                return null;
            }
        }
        return sharedAudioContext;
    }

    const defaultSprites = {
        player: '/sprites/games/my-tuno/default_tuno.png',
        background: '/sprites/games/my-tuno/backgrounds/forest.png',
        enemies: {
            normal: '/sprites/games/my-tuno/enemies/forest/monkey.png',
            boss: '/sprites/games/my-tuno/enemies/forest/boss_1_bear.png'
        }
    };

    const getEventField = (evt, field) => {
        if (!evt) return undefined;
        // Try PascalCase, camelCase, then lowercase
        return evt[field] ?? evt[field[0].toLowerCase() + field.slice(1)] ?? evt[field.toLowerCase()];
    };

    const resolveEvents = (battleData) => {
        if (!battleData) return [];
        const eventsJson = battleData.EventsJson ?? battleData.eventsJson ?? battleData.eventsjson;
        if (eventsJson && typeof eventsJson === 'string') {
            try {
                const parsed = JSON.parse(eventsJson);
                // EventsJson may be the full replay wrapper {Events:[...], ...} or just the events array
                if (Array.isArray(parsed)) return parsed;
                if (parsed && Array.isArray(parsed.Events)) return parsed.Events;
                return [];
            } catch (e) {
                console.error('Failed to parse EventsJson:', e);
                return [];
            }
        }
        if (Array.isArray(battleData)) return battleData;
        return battleData.events ?? battleData.Events ?? [];
    };

    /** Format a number with K/M/B/T abbreviation */
    const formatNum = (n) => {
        if (n == null) return '0';
        const abs = Math.abs(n);
        const sign = n < 0 ? '-' : '';
        if (abs >= 1e12) return sign + (abs / 1e12).toFixed(abs % 1e12 === 0 ? 0 : 2).replace(/\.?0+$/, '') + 'T';
        if (abs >= 1e9)  return sign + (abs / 1e9).toFixed(abs % 1e9 === 0 ? 0 : 2).replace(/\.?0+$/, '') + 'B';
        if (abs >= 1e6)  return sign + (abs / 1e6).toFixed(abs % 1e6 === 0 ? 0 : 2).replace(/\.?0+$/, '') + 'M';
        if (abs >= 1e3)  return sign + (abs / 1e3).toFixed(abs % 1e3 === 0 ? 0 : 2).replace(/\.?0+$/, '') + 'K';
        return sign + Math.round(abs).toString();
    };

    class StageBattleScene {
        constructor(container, data) {
            this.container = container;
            this.eventsList = data?.events ?? [];
            this.dotNetRef = data?.dotNetRef ?? null;
            this.eventInterval = data?.eventInterval ?? DEFAULT_EVENT_INTERVAL;
            this.currentEventIndex = 0;
            
            this.playerMaxHp = 100;
            this.playerCurrentHp = 100;
            this.enemyMaxHp = 100;
            this.enemyCurrentHp = 100;
            this.enemyHPs = [];
            this.isInitialSetup = true;
            
            this.playerSprite = null;
            this.enemySprites = [];
            this.backgroundSprite = null;
            
            this.playerHpBar = null;
            this.enemyHpBars = [];
            this.stageText = null;
            
            this.isPlaying = false;
            this.playbackSpeed = 1;
            this.battleFinished = false;
            
            // Handle both PascalCase (from C#) and camelCase property names
            this.stageNumber = data?.StageNumber ?? data?.stageNumber ?? 1;
            this.enemyType = data?.EnemyType ?? data?.enemyType ?? 'normal';
            this.enemyCount = data?.EnemyCount ?? data?.enemyCount ?? 1;
            this.playerName = data?.PlayerName ?? data?.playerName ?? 'Player';
            this.enemyName = data?.EnemyName ?? data?.enemyName ?? 'Enemy';
            this.backgroundPath = data?.BackgroundPath ?? data?.backgroundPath ?? defaultSprites.background;
            this.playerSpritePath = data?.PlayerSpritePath ?? data?.playerSpritePath ?? defaultSprites.player;
            
            // Get enemy sprites - check both PascalCase and camelCase
            const enemySpritesData = data?.EnemySprites ?? data?.enemySprites;
            if (enemySpritesData && Array.isArray(enemySpritesData)) {
                this.enemySpritePaths = enemySpritesData;
            } else {
                const singlePath = data?.EnemySpritePath ?? data?.enemySpritePath ?? defaultSprites.enemies[this.enemyType] ?? defaultSprites.enemies.normal;
                this.enemySpritePaths = Array(this.enemyCount).fill(singlePath);
            }
            
            // Get enemy placements (0=Terrestrial, 1=Aerial)
            const placementsData = data?.EnemyPlacements ?? data?.enemyPlacements;
            if (placementsData && Array.isArray(placementsData)) {
                this.enemyPlacements = placementsData;
            } else {
                this.enemyPlacements = Array(this.enemyCount).fill(0); // Default all terrestrial
            }
            
            this.enemyHPs = Array(this.enemyCount).fill(null).map(() => ({ current: 100, max: 100 }));
            
            // Idle animation settings
            this.idleAnimationTime = 0;
            this.enemyIdleOffsets = []; // Store original Y positions for idle bob
            
            // Attack animation flags — prevent idle from overriding lunge positions
            this._playerAttacking = false;
            this._enemyAttacking = {}; // keyed by enemy index
            
            // Speed bar system - time-based combat
            this.playerActionTime = 3.5; // Reduced from 5.0 to 3.5 seconds for faster combat
            this.enemyActionTimes = Array(this.enemyCount).fill(3.5);
            this.playerSpeedBarTimer = 3500; // In milliseconds
            this.enemySpeedBarTimers = Array(this.enemyCount).fill(3500);
            this.playerSpeedBar = null;
            this.enemySpeedBars = [];
            this.battleStartTime = 0;
            this.currentSimTime = 0;
            this.battleSpeed = 1.0;
            this.battleEvents = null;
            
            // Use global audio state to persist settings between stages
            this.audioEnabled = globalAudioEnabled;
            this.sfxVolume = globalSfxVolume;
            
            // Shot buff visual
            this.hasShotBuff = data?.HasShotBuff ?? data?.hasShotBuff ?? false;
            this.playerAura = null;
            this.audioContext = null;
            // Timer tracking for cleanup
            this._timeoutIds = [];
            this._rafIds = [];

            // PIXI.Text pool for floating text
            this._textPool = [];

            // ── Interactive mode (spells / Blade Crafter style) ─────────────
            this.interactiveMode = data?.interactiveMode ?? data?.InteractiveMode ?? false;
            this.spells = data?.spells ?? data?.Spells ?? [];
            this.interactivePlayerHP = data?.playerHP ?? data?.PlayerHP ?? null;
            this.interactivePlayerMaxHP = data?.playerMaxHP ?? data?.PlayerMaxHP ?? null;
            this.interactivePlayerActionTime = data?.playerActionTime ?? data?.PlayerActionTime ?? null;
            this.interactiveEnemies = data?.enemies ?? data?.Enemies ?? [];

            // Pending request flags (prevent double-fire during async calls)
            this._playerAttackPending = false;
            this._enemyAttackPending = Array(this.enemyCount).fill(false);
            this._spellPending = false;
            this._cooldownTickAccum = 0;
            this._consumableTickAccum = 0; // real-time accumulator (unaffected by battle speed)

            // Spell bar UI elements
            this.spellButtons = [];
            this.spellCooldowns = {}; // client-side cooldown tracking {attackId: remainingSeconds}
            this.spellBarContainer = null;

            // ── Consumable in-fight bar ─────────────────────────────────────
            this.consumableBarContainer = null;
            this.consumableButtons = [];
            this._consumablePending = false;
            const cData = data?.consumables ?? data?.Consumables ?? {};
            this.consumableQuantities = {
                fino: cData.fino ?? cData.Fino ?? 0,
                caneca: cData.caneca ?? cData.Caneca ?? 0,
                cigarro: cData.cigarro ?? cData.Cigarro ?? 0,
                canhao: cData.canhao ?? cData.Canhao ?? 0,
                shot: cData.shot ?? cData.Shot ?? 0,
                penalty: cData.penalty ?? cData.Penalty ?? 0
            };
            // Track which buffs are currently active (to disable buttons + show active visuals)
            const abData = data?.activeBuffs ?? data?.ActiveBuffs ?? {};
            this.activeBuffs = {
                cigarro: !!(abData.cigarro ?? abData.Cigarro),
                canhao: !!(abData.canhao ?? abData.Canhao),
                shot: !!(abData.shot ?? abData.Shot),
                penalty: !!(abData.penalty ?? abData.Penalty)
            };
            // Consumable image URLs from Cloudflare CDN (fallback to local SVGs)
            const defaultConsumableImages = {
                fino: '/images/consumables/fino.svg',
                caneca: '/images/consumables/caneca.svg',
                cigarro: '/images/consumables/cigarro.svg',
                canhao: '/images/consumables/canhao.svg'
            };
            const ciData = data?.consumableImages ?? data?.ConsumableImages ?? {};
            this.consumableImages = {
                fino: ciData.fino ?? ciData.Fino ?? defaultConsumableImages.fino,
                caneca: ciData.caneca ?? ciData.Caneca ?? defaultConsumableImages.caneca,
                cigarro: ciData.cigarro ?? ciData.Cigarro ?? defaultConsumableImages.cigarro,
                canhao: ciData.canhao ?? ciData.Canhao ?? defaultConsumableImages.canhao
            };
            console.log('[StageBattle] Consumable images:', JSON.stringify(this.consumableImages));

            // Consumable cooldowns — persisted across stages
            const ccData = data?.consumableCooldowns ?? data?.ConsumableCooldowns ?? {};
            this.consumableCooldowns = {
                fino: ccData.fino ?? ccData.Fino ?? 0,
                caneca: ccData.caneca ?? ccData.Caneca ?? 0,
                cigarro: ccData.cigarro ?? ccData.Cigarro ?? 0,
                canhao: ccData.canhao ?? ccData.Canhao ?? 0,
                shot: ccData.shot ?? ccData.Shot ?? 0,
                penalty: ccData.penalty ?? ccData.Penalty ?? 0
            };
            
            this.setupAudio();
            this.initPixi();
        }

        setupAudio() {
            this.audioContext = getSharedAudioContext();
            
            if (this.audioContext) {
                // Resume if suspended (browsers require user gesture)
                if (this.audioContext.state === 'suspended') {
                    this.audioContext.resume().catch(() => {});
                }
                // Start background music if not already playing
                // Music plays continuously throughout the entire run — no switching on boss appearance
                if (!backgroundMusic) {
                    this.loadBackgroundMusic();
                }
            } else {
                this.audioEnabled = false;
            }
        }
        
        async loadBackgroundMusic() {
            try {
                // Pick the right track: boss gets boss_battle.mp3, everything else gets stage_battle.mp3
                const isBoss = this.enemyType && this.enemyType.toLowerCase() === 'boss';
                const musicFile = isBoss ? '/sound/boss_battle.mp3' + audioCacheBuster : '/sound/stage_battle.mp3' + audioCacheBuster;
                currentMusicType = isBoss ? 'boss' : 'stage';

                // Use cached AudioBuffer if available, otherwise fetch and decode once
                let audioBuffer = audioBufferCache[musicFile];
                if (!audioBuffer) {
                    const response = await fetch(musicFile);
                    const arrayBuffer = await response.arrayBuffer();
                    audioBuffer = await this.audioContext.decodeAudioData(arrayBuffer);
                    audioBufferCache[musicFile] = audioBuffer;
                }
                
                // Create gain node for volume control
                backgroundMusicGainNode = this.audioContext.createGain();
                backgroundMusicGainNode.connect(this.audioContext.destination);
                backgroundMusicGainNode.gain.value = this.audioEnabled ? 0.3 : 0;
                
                // Create and start looping background music
                backgroundMusic = this.audioContext.createBufferSource();
                backgroundMusic.buffer = audioBuffer;
                backgroundMusic.loop = true;
                backgroundMusic.connect(backgroundMusicGainNode);
                backgroundMusic.start(0);
            } catch (e) {
                console.warn('Could not load background music:', e);
            }
        }

        async initPixi() {
            // Clear container to prevent multiple canvases
            while (this.container.firstChild) {
                this.container.removeChild(this.container.firstChild);
            }

            // Use container's actual dimensions (fullscreen) instead of fixed size
            const containerW = this.container.clientWidth || DEFAULT_WIDTH;
            const containerH = this.container.clientHeight || DEFAULT_HEIGHT;
            
            this.app = new PIXI.Application();
            await this.app.init({
                width: containerW,
                height: containerH,
                backgroundColor: 0x1a1a1a,
                antialias: true,
                resizeTo: this.container
            });

            this.container.appendChild(this.app.canvas);
            this.stage = this.app.stage;

            // Log GL context loss but let PixiJS handle recovery automatically.
            // The battle continues — the ticker resumes once the context is restored.
            this._onContextLost = (e) => {
                console.warn('WebGL context lost — battle continues on restore');
                e.preventDefault(); // request automatic context restore
            };
            this.app.canvas.addEventListener('webglcontextlost', this._onContextLost);

            this._onContextRestored = () => {
                console.log('WebGL context restored');
            };
            this.app.canvas.addEventListener('webglcontextrestored', this._onContextRestored);

            // Handle window resize — rebuild layout when container dimensions change
            this._onResize = () => {
                if (!this.app || !this.stage) return;
                // PixiJS resizeTo handles canvas size; we just need to reposition sprites
                // For now, the layout scales via the aspect ratio
            };
            window.addEventListener('resize', this._onResize);

            await this.loadAssets();
            this.create();
        }

        async loadAssets() {
            // Use path-based aliases so the same sprite is loaded only once per session.
            // SESSION_CACHE_BUST is set once at page load — the browser HTTP-caches
            // responses across stage transitions, eliminating redundant network fetches.
            this.bgAlias = `bg_${this.backgroundPath}`;
            this._playerAlias = `player_${this.playerSpritePath}`;

            // Load each asset individually so a 404 on one sprite doesn't crash everything.
            // On failure, fall back to a known-good default sprite.
            const tryLoad = async (alias, src, fallbackAlias) => {
                if (loadedAssetAliases.has(alias)) return;
                try {
                    await PIXI.Assets.load({ alias, src: src + SESSION_CACHE_BUST });
                    loadedAssetAliases.add(alias);
                } catch (e) {
                    console.warn(`Sprite 404, using fallback: ${src}`, e.message);
                    // Point the alias at the fallback texture so Sprite.from(alias) works
                    if (fallbackAlias && loadedAssetAliases.has(fallbackAlias)) {
                        try {
                            const fallbackTex = PIXI.Assets.get(fallbackAlias);
                            if (fallbackTex) PIXI.Assets.cache.set(alias, fallbackTex);
                            loadedAssetAliases.add(alias);
                        } catch (_) { /* fallback also failed, will use PIXI white texture */ }
                    }
                }
            };

            // Background and player — use defaults as fallbacks
            const defaultBgAlias = `bg_${defaultSprites.background}`;
            const defaultPlayerAlias = `player_${defaultSprites.player}`;
            await tryLoad(defaultBgAlias, defaultSprites.background, null);
            await tryLoad(defaultPlayerAlias, defaultSprites.player, null);
            await tryLoad(this.bgAlias, this.backgroundPath, defaultBgAlias);
            await tryLoad(this._playerAlias, this.playerSpritePath, defaultPlayerAlias);

            // Enemy sprites — fall back to the biome's default normal enemy
            this.enemySpriteAliases = [];
            const defaultEnemyPath = defaultSprites.enemies.normal;
            const defaultEnemyAlias = `enemy_${defaultEnemyPath}`;
            await tryLoad(defaultEnemyAlias, defaultEnemyPath, null);

            if (this.enemySpritePaths && Array.isArray(this.enemySpritePaths)) {
                for (let i = 0; i < this.enemySpritePaths.length; i++) {
                    const alias = `enemy_${this.enemySpritePaths[i]}`;
                    this.enemySpriteAliases.push(alias);
                    await tryLoad(alias, this.enemySpritePaths[i], defaultEnemyAlias);
                }
            }
        }

        create() {
            stageScene = this;

            const width = this.app.screen.width;
            const height = this.app.screen.height;

            // Mobile portrait: player bottom-center, enemies top-center
            this.isMobile = width <= height || width < 500;

            this.backgroundSprite = PIXI.Sprite.from(this.bgAlias);
            this.backgroundSprite.width = width;
            this.backgroundSprite.height = height;
            this.backgroundSprite.x = width / 2;
            this.backgroundSprite.y = height / 2;
            this.backgroundSprite.anchor.set(0.5);
            this.stage.addChild(this.backgroundSprite);

            const overlay = new PIXI.Graphics();
            overlay.rect(0, 0, width, height);
            overlay.fill({ color: 0x000000, alpha: 0.3 });
            this.stage.addChild(overlay);
            this._overlay = overlay;

            this.createPlayer(width, height);
            this.createEnemies(width, height);
            this.createHudBars(width, height);

            if (this.interactiveMode) {
                this.initInteractiveState();
                this.createSpellBar();
                this.createConsumableBar();
                this.startInteractiveBattle();
            } else {
                this.preprocessInitialEvents();
                this.startTimedBattle();
            }
            
            // Add update loop with ticker
            this.app.ticker.add(() => this.update());
        }

        preprocessInitialEvents() {
            let firstPlayerAttackIndex = -1;
            for (let i = 0; i < this.eventsList.length; i++) {
                const evt = this.eventsList[i];
                const evtType = getEventField(evt, 'Type');
                const attacker = getEventField(evt, 'Attacker');
                if (evtType === 'Attack' && (attacker === 'Attacker' || attacker === 'Player')) {
                    firstPlayerAttackIndex = i;
                    break;
                }
            }

            for (let i = 0; i < this.eventsList.length && i < firstPlayerAttackIndex; i++) {
                const evt = this.eventsList[i];
                const evtType = getEventField(evt, 'Type');
                const maxHP = getEventField(evt, 'MaxHP');
                
                if (evtType === 'HPUpdate' && maxHP) {
                    this.processInitialHPEvent(evt);
                }
            }

            this.currentEventIndex = firstPlayerAttackIndex >= 0 ? firstPlayerAttackIndex : 0;
            this.isInitialSetup = false;
            
            // Initialize speed bar timers
            this.playerSpeedBarTimer = this.playerActionTime * 1000;
            for (let i = 0; i < this.enemyCount; i++) {
                this.enemySpeedBarTimers[i] = this.enemyActionTimes[i] * 1000;
            }
        }

        processInitialHPEvent(evt) {
            const character = getEventField(evt, 'Character');
            const hp = getEventField(evt, 'HP');
            const maxHP = getEventField(evt, 'MaxHP');
            const actionTime = getEventField(evt, 'ActionTime') ?? getEventField(evt, 'actionTime');

            if (character === 'Attacker' || character === 'Player') {
                this.playerMaxHp = maxHP;
                this.playerCurrentHp = hp;
                if (actionTime) this.playerActionTime = actionTime;
                if (this.playerHpBar) {
                    const ratio = Math.max(0, hp / maxHP);
                    this.playerHpBar.bar.width = this.playerHpBar.maxWidth * ratio;
                }
                if (this.playerHpBar?.text) {
                    this.playerHpBar.text.text = `${formatNum(hp)}/${formatNum(maxHP)}`;
                }
            } else if (character.startsWith('Enemy')) {
                const enemyIndex = parseInt(character.replace('Enemy', ''));
                if (!isNaN(enemyIndex) && enemyIndex >= 0 && enemyIndex < this.enemyHPs.length) {
                    this.enemyHPs[enemyIndex].max = maxHP;
                    this.enemyHPs[enemyIndex].current = hp;
                    if (actionTime) this.enemyActionTimes[enemyIndex] = actionTime;
                    const hpBarData = this.enemyHpBars[enemyIndex];
                    if (hpBarData && hpBarData.text) {
                        hpBarData.bar.width = hpBarData.maxWidth;
                        hpBarData.text.text = `${formatNum(hp)}/${formatNum(maxHP)}`;
                        hpBarData.text.visible = true;
                    }
                    // Init boss HUD bar
                    if (this.bossHpBar && enemyIndex === 0) {
                        const ratio = Math.max(0, hp / maxHP);
                        this.bossHpBar.bar.width = this.bossHpBar.maxWidth * ratio;
                        this.bossHpBar.text.text = `${formatNum(hp)} / ${formatNum(maxHP)} HP`;
                    }
                }
            } else if (character === 'Defender' && this.enemyCount === 1) {
                const enemyIndex = 0;
                this.enemyHPs[enemyIndex].max = maxHP;
                this.enemyHPs[enemyIndex].current = hp;
                if (actionTime) this.enemyActionTimes[enemyIndex] = actionTime;
                const hpBarData = this.enemyHpBars[enemyIndex];
                if (hpBarData && hpBarData.text) {
                    hpBarData.bar.width = hpBarData.maxWidth;
                    hpBarData.text.text = `${formatNum(hp)}/${formatNum(maxHP)}`;
                    hpBarData.text.visible = true;
                }
                // Init boss HUD bar
                if (this.bossHpBar) {
                    const ratio = Math.max(0, hp / maxHP);
                    this.bossHpBar.bar.width = this.bossHpBar.maxWidth * ratio;
                    this.bossHpBar.text.text = `${formatNum(hp)} / ${formatNum(maxHP)} HP`;
                }
            }
        }

        createPlayer(width, height) {
            // Reserve space at bottom for spell bar + consumable bar
            const bottomBarReserve = Math.min(140, height * 0.15);
            const groundOffset = bottomBarReserve + 10;

            let playerX, playerY;
            if (this.isMobile) {
                // Mobile: player at bottom center
                playerX = width * 0.5;
                playerY = height - groundOffset;
            } else {
                // Desktop: player on the left
                playerX = width * 0.25;
                playerY = height - groundOffset;
            }
            
            this.playerSprite = PIXI.Sprite.from(this._playerAlias);
            this.playerSprite.anchor.set(0.5, 1);
            this.playerSprite.x = playerX;
            this.playerSprite.y = playerY;
            
            // Mobile: smaller player to leave room for enemies; Desktop: unchanged
            const maxSpriteHeight = this.isMobile ? height * 0.25 : height * 0.45;
            const scale = Math.min(1, maxSpriteHeight / this.playerSprite.height);
            this.playerSprite.scale.set(scale);
            
            this.playerX = playerX;
            this.playerDisplayHeight = this.playerSprite.height;
            
            // Add blue aura BEFORE sprite so it renders behind
            if (this.hasShotBuff) {
                const auraSize = this.playerDisplayHeight * 0.7;
                this.playerAura = new PIXI.Graphics();
                this.playerAura.circle(0, 0, auraSize);
                this.playerAura.fill({ color: 0x44bbff, alpha: 0.35 });
                this.playerAura.x = playerX;
                this.playerAura.y = playerY - this.playerDisplayHeight / 2;
                this.stage.addChild(this.playerAura);
            }
            
            this.stage.addChild(this.playerSprite);

            // Player HP and speed bars are drawn in createHudBars() (top of canvas)

            // Store base position for idle bobbing animation
            this.playerIdleOffset = {
                baseX: playerX,
                baseY: playerY,
                phase: Math.PI, // Offset phase from enemies
                bobAmplitude: 3,
                swayAmplitude: 2
            };
        }

        /** Draw player HP bar and speed bar below the HTML top bar overlay (WOO-style). */
        createHudBars(width, height) {
            const isMobile = this.isMobile;
            const barWidth = isMobile ? Math.min(220, width * 0.32) : Math.min(400, width * 0.40);
            const barHeight = isMobile ? Math.min(22, height * 0.035) : Math.min(36, height * 0.055);
            // Push below the HTML top bar overlay
            const topBarHeight = 54;
            const paddingTop = topBarHeight + 8;
            const paddingLeft = Math.min(16, width * 0.03);

            // == HP Bar ==
            // Dark rounded background
            const hpBg = new PIXI.Graphics();
            hpBg.roundRect(paddingLeft, paddingTop, barWidth, barHeight, barHeight / 2);
            hpBg.fill({ color: 0x1a1a1a, alpha: 0.85 });
            hpBg.stroke({ color: 0x333333, width: 1 });
            this.stage.addChild(hpBg);

            // HP fill (green gradient look)
            const hpFill = new PIXI.Graphics();
            hpFill.roundRect(0, 0, barWidth, barHeight, barHeight / 2);
            hpFill.fill(0x4caf50);
            hpFill.x = paddingLeft;
            hpFill.y = paddingTop;
            this.stage.addChild(hpFill);

            // HP border
            const hpBorder = new PIXI.Graphics();
            hpBorder.roundRect(paddingLeft, paddingTop, barWidth, barHeight, barHeight / 2);
            hpBorder.stroke({ width: 1.5, color: 0x66bb6a });
            this.stage.addChild(hpBorder);

            // HP text centered on bar (e.g. "1.28K / 1.28K HP")
            const hpFontSize = isMobile ? Math.min(12, barHeight * 0.55) : Math.min(16, barHeight * 0.5);
            const hpText = new PIXI.Text({
                text: '',
                style: {
                    fontFamily: 'Arial, sans-serif', fontSize: hpFontSize, fontWeight: 'bold',
                    fill: 0xffffff,
                    stroke: { color: 0x000000, width: 2 }
                }
            });
            hpText.anchor.set(0.5, 0.5);
            hpText.x = paddingLeft + barWidth / 2;
            hpText.y = paddingTop + barHeight / 2;
            this.stage.addChild(hpText);

            this.playerHpBar = {
                bar: hpFill, barBg: hpBg, border: hpBorder,
                text: hpText, maxWidth: barWidth, barHeight: barHeight,
                x: paddingLeft, y: paddingTop
            };

            // == Speed Bar (below HP) ==
            const speedBarHeight = isMobile ? Math.min(10, height * 0.015) : Math.min(18, height * 0.025);
            const speedBarY = paddingTop + barHeight + 3;

            const speedBg = new PIXI.Graphics();
            speedBg.roundRect(paddingLeft, speedBarY, barWidth, speedBarHeight, speedBarHeight / 2);
            speedBg.fill({ color: 0x111111, alpha: 0.85 });
            this.stage.addChild(speedBg);

            const speedFill = new PIXI.Graphics();
            speedFill.roundRect(0, 0, barWidth, speedBarHeight, speedBarHeight / 2);
            speedFill.fill(0x00bcd4);
            speedFill.x = paddingLeft;
            speedFill.y = speedBarY;
            this.stage.addChild(speedFill);

            // Speed countdown text (shows remaining seconds: "3.2s") — centered INSIDE the bar
            const speedFontSize = isMobile ? Math.min(8, speedBarHeight * 0.8) : Math.min(14, speedBarHeight * 0.8);
            const speedText = new PIXI.Text({
                text: '',
                style: {
                    fontFamily: 'Arial, sans-serif', fontSize: speedFontSize, fontWeight: 'bold',
                    fill: 0xffffff,
                    stroke: { color: 0x000000, width: 2 }
                }
            });
            speedText.anchor.set(0.5, 0.5);
            speedText.x = paddingLeft + barWidth / 2;
            speedText.y = speedBarY + speedBarHeight / 2;
            this.stage.addChild(speedText);

            this.playerSpeedBar = {
                bar: speedFill, barBg: speedBg, maxWidth: barWidth,
                text: speedText, barHeight: speedBarHeight
            };

            // == Boss HUD bars (top-right, mirrored, red) ==
            const isBossMode = this.enemyType && this.enemyType.toLowerCase() === 'boss';
            if (isBossMode) {
                const rightX = width - paddingLeft - barWidth;

                // Boss HP background
                const bossHpBg = new PIXI.Graphics();
                bossHpBg.roundRect(rightX, paddingTop, barWidth, barHeight, barHeight / 2);
                bossHpBg.fill({ color: 0x1a1a1a, alpha: 0.85 });
                bossHpBg.stroke({ color: 0x333333, width: 1 });
                this.stage.addChild(bossHpBg);

                // Boss HP fill (red)
                const bossHpFill = new PIXI.Graphics();
                bossHpFill.roundRect(0, 0, barWidth, barHeight, barHeight / 2);
                bossHpFill.fill(0xf44336);
                bossHpFill.x = rightX;
                bossHpFill.y = paddingTop;
                this.stage.addChild(bossHpFill);

                // Boss HP border (red)
                const bossHpBorder = new PIXI.Graphics();
                bossHpBorder.roundRect(rightX, paddingTop, barWidth, barHeight, barHeight / 2);
                bossHpBorder.stroke({ width: 1.5, color: 0xef5350 });
                this.stage.addChild(bossHpBorder);

                // Boss HP text
                const bossHpText = new PIXI.Text({
                    text: '',
                    style: {
                        fontFamily: 'Arial, sans-serif', fontSize: hpFontSize, fontWeight: 'bold',
                        fill: 0xffffff,
                        stroke: { color: 0x000000, width: 2 }
                    }
                });
                bossHpText.anchor.set(0.5, 0.5);
                bossHpText.x = rightX + barWidth / 2;
                bossHpText.y = paddingTop + barHeight / 2;
                this.stage.addChild(bossHpText);

                this.bossHpBar = {
                    bar: bossHpFill, barBg: bossHpBg, border: bossHpBorder,
                    text: bossHpText, maxWidth: barWidth, barHeight: barHeight,
                    x: rightX, y: paddingTop
                };

                // Boss Speed Bar (below HP)
                const bossSpeedBg = new PIXI.Graphics();
                bossSpeedBg.roundRect(rightX, speedBarY, barWidth, speedBarHeight, speedBarHeight / 2);
                bossSpeedBg.fill({ color: 0x111111, alpha: 0.85 });
                this.stage.addChild(bossSpeedBg);

                const bossSpeedFill = new PIXI.Graphics();
                bossSpeedFill.roundRect(0, 0, barWidth, speedBarHeight, speedBarHeight / 2);
                bossSpeedFill.fill(0x00bcd4);
                bossSpeedFill.x = rightX;
                bossSpeedFill.y = speedBarY;
                this.stage.addChild(bossSpeedFill);

                const bossSpeedText = new PIXI.Text({
                    text: '',
                    style: {
                        fontFamily: 'Arial, sans-serif', fontSize: speedFontSize, fontWeight: 'bold',
                        fill: 0xffffff,
                        stroke: { color: 0x000000, width: 2 }
                    }
                });
                bossSpeedText.anchor.set(0.5, 0.5);
                bossSpeedText.x = rightX + barWidth / 2;
                bossSpeedText.y = speedBarY + speedBarHeight / 2;
                this.stage.addChild(bossSpeedText);

                this.bossSpeedBar = {
                    bar: bossSpeedFill, barBg: bossSpeedBg,
                    text: bossSpeedText, maxWidth: barWidth, barHeight: speedBarHeight
                };
            }
        }

        createEnemies(width, height) {
            this.enemySprites = [];
            this.enemyHpBars = [];
            this.enemyIdleOffsets = []; // Store base positions for idle animation
            
            const isMobile = this.isMobile;
            // Reserve space at bottom for spell bar + consumable bar
            const bottomBarReserve = Math.min(140, height * 0.15);
            const groundOffset = bottomBarReserve + 10;

            let enemyX, baseEnemyY;
            if (isMobile) {
                // Mobile: enemies at top center area — push down so sprites + HP bars clear top bar
                enemyX = width * 0.5;
                const topBarHeight = 60; // HTML overlay top bar
                baseEnemyY = topBarHeight + height * 0.32;
            } else {
                // Desktop: enemies on the right
                enemyX = width * 0.72;
                baseEnemyY = height - groundOffset;
            }
            
            // Pass placements to calculate positions
            const positions = this.calculateEnemyPositions(isMobile, this.enemyCount, enemyX, baseEnemyY, width, height, this.enemyPlacements);
            
            // Determine scale factor based on enemy count
            // More enemies = smaller sprites to fit them all
            const isBoss = this.enemyType && this.enemyType.toLowerCase() === 'boss';
            let countScaleFactor = 1.0;
            if (isMobile) {
                // Mobile: much more aggressive scaling for large groups
                if (this.enemyCount >= 9) {
                    countScaleFactor = 0.38;
                } else if (this.enemyCount >= 8) {
                    countScaleFactor = 0.42;
                } else if (this.enemyCount >= 7) {
                    countScaleFactor = 0.48;
                } else if (this.enemyCount >= 6) {
                    countScaleFactor = 0.52;
                } else if (this.enemyCount >= 5) {
                    countScaleFactor = 0.60;
                } else if (this.enemyCount >= 4) {
                    countScaleFactor = 0.75;
                } else if (this.enemyCount >= 3) {
                    countScaleFactor = 0.85;
                }
            } else {
                // Desktop: keep existing values
                if (this.enemyCount >= 6) {
                    countScaleFactor = 0.55;
                } else if (this.enemyCount >= 5) {
                    countScaleFactor = 0.65;
                } else if (this.enemyCount >= 4) {
                    countScaleFactor = 0.85;
                } else if (this.enemyCount >= 3) {
                    countScaleFactor = 0.92;
                }
            }
            
            // Boss gets a size boost
            const bossBoost = isBoss ? 1.25 : 1.0;

            for (let i = 0; i < this.enemyCount; i++) {
                const pos = positions[i];
                if (!pos) continue; // Skip if no position calculated
                
                const isAerial = pos.isAerial || (this.enemyPlacements && this.enemyPlacements[i] === 1);
                
                // Store base position for idle animation
                // Aerial enemies bob more and have faster horizontal sway
                this.enemyIdleOffsets.push({
                    baseX: pos.x,
                    baseY: pos.y,
                    phase: i * (Math.PI / 2), // Stagger phases
                    isAerial: isAerial,
                    bobAmplitude: isAerial ? 6 : 3, // Aerial bob more
                    swayAmplitude: isAerial ? 4 : 2
                });
                
                // Use the path-based alias stored during loadAssets
                const alias = this.enemySpriteAliases && this.enemySpriteAliases[i] 
                    ? this.enemySpriteAliases[i] 
                    : `enemy_${this.enemySpritePaths[i]}`;
                const enemy = PIXI.Sprite.from(alias);
                enemy.anchor.set(0.5, 1);
                enemy.x = pos.x;
                enemy.y = pos.y;
                
                // Calculate base scale from height — mobile enemies bigger to be clearly visible
                const mobileScale = isMobile ? 0.28 : 0.40;
                const maxSpriteHeight = height * mobileScale;
                const baseScale = Math.min(1, maxSpriteHeight / enemy.height);
                
                // Apply count factor and boss boost
                const finalScale = baseScale * countScaleFactor * bossBoost;
                enemy.scale.set(finalScale);
                
                this.stage.addChild(enemy);
                this.enemySprites.push(enemy);
                
                // enemy.height is already scaled after scale.set(), no need to multiply again
                const hpBarY = pos.y - enemy.height - 5;
                const hpBarWidth = isMobile ? 35 : 60;
                const hpBarHeight = isMobile ? 4 : 8;
                
                const barBg = new PIXI.Graphics();
                barBg.rect(pos.x - hpBarWidth / 2, hpBarY - hpBarHeight / 2, hpBarWidth, hpBarHeight);
                barBg.fill(0x333333);
                this.stage.addChild(barBg);
                
                const bar = new PIXI.Graphics();
                bar.rect(0, 0, hpBarWidth, hpBarHeight);
                bar.fill(0xff4444);
                bar.x = pos.x - hpBarWidth / 2;
                bar.y = hpBarY - hpBarHeight / 2;
                this.stage.addChild(bar);
                
                const showText = !isMobile || this.enemyCount <= 2;
                const hpText = new PIXI.Text({
                    text: '',
                    style: {
                        fontSize: isMobile ? 7 : 10,
                        fontFamily: 'Arial, sans-serif',
                        fill: 0xffffff,
                        stroke: { color: 0x000000, width: 2 }
                    }
                });
                hpText.anchor.set(0.5);
                hpText.x = pos.x;
                hpText.y = hpBarY - 8;
                hpText.visible = showText;
                this.stage.addChild(hpText);
                
                this.enemyHpBars.push({
                    bar: bar,
                    barBg: barBg,
                    text: hpText,
                    maxWidth: hpBarWidth,
                    enemyIndex: i
                });
                
                // Create Speed bar below HP bar for enemy (smaller)
                const speedBarHeight = isMobile ? 2 : 4;
                const speedBarY = hpBarY + hpBarHeight / 2 + 2;
                
                const speedBarBg = new PIXI.Graphics();
                speedBarBg.rect(pos.x - hpBarWidth / 2, speedBarY, hpBarWidth, speedBarHeight);
                speedBarBg.fill(0x222222);
                this.stage.addChild(speedBarBg);
                
                const speedBarFill = new PIXI.Graphics();
                speedBarFill.rect(0, 0, hpBarWidth, speedBarHeight);
                speedBarFill.fill(0x00bcd4); // Cyan for speed
                speedBarFill.x = pos.x - hpBarWidth / 2;
                speedBarFill.y = speedBarY;
                this.stage.addChild(speedBarFill);
                
                this.enemySpeedBars.push({
                    bar: speedBarFill,
                    barBg: speedBarBg,
                    maxWidth: hpBarWidth,
                    enemyIndex: i
                });
            }

            // Hide per-sprite HP/speed bars for bosses (the HUD bar handles it)
            if (isBoss && this.enemyCount === 1) {
                this.enemyHpBars.forEach(d => {
                    d.bar.visible = false; d.barBg.visible = false; d.text.visible = false;
                });
                this.enemySpeedBars.forEach(d => {
                    d.bar.visible = false; d.barBg.visible = false;
                });
            }
        }

        calculateEnemyPositions(isMobile, enemyCount, baseX, baseY, width, height, placements) {
            const positions = [];
            
            // Separate aerial and terrestrial enemies
            const aerialIndices = [];
            const terrestrialIndices = [];
            
            for (let i = 0; i < enemyCount; i++) {
                if (placements && placements[i] === 1) {
                    aerialIndices.push(i);
                } else {
                    terrestrialIndices.push(i);
                }
            }
            
            // Adjust spacing based on enemy count - tighter when more enemies
            let hSpacing, vSpacing;
            if (enemyCount >= 9) {
                hSpacing = isMobile ? 45 : 105;
                vSpacing = isMobile ? 55 : 105;
            } else if (enemyCount >= 8) {
                hSpacing = isMobile ? 48 : 115;
                vSpacing = isMobile ? 58 : 115;
            } else if (enemyCount >= 7) {
                hSpacing = isMobile ? 52 : 125;
                vSpacing = isMobile ? 62 : 125;
            } else if (enemyCount >= 6) {
                hSpacing = isMobile ? 58 : 140;
                vSpacing = isMobile ? 68 : 140;
            } else if (enemyCount >= 5) {
                hSpacing = isMobile ? 70 : 165;
                vSpacing = isMobile ? 80 : 165;
            } else if (enemyCount >= 4) {
                hSpacing = isMobile ? 75 : 175;
                vSpacing = isMobile ? 85 : 175;
            } else {
                hSpacing = isMobile ? 90 : 190;
                vSpacing = isMobile ? 100 : 175;
            }
            
            const aerialOffset = isMobile ? 80 : 200; // How high aerial enemies fly
            
            // Calculate X bounds to keep enemies on screen
            const maxX = width - 40;
            // On mobile (vertical layout), enemies are centered — allow full width
            // On desktop, don't go past middle of screen (player is on left)
            const minX = isMobile ? 40 : width * 0.45;
            
            // Calculate positions for each enemy
            const tempPositions = Array(enemyCount).fill(null);
            
            // Position terrestrial enemies in bottom area
            if (terrestrialIndices.length > 0) {
                const count = terrestrialIndices.length;
                let startX = baseX - ((count - 1) * hSpacing) / 2;
                
                // Ensure rightmost enemy stays on screen
                const rightmostX = startX + (count - 1) * hSpacing;
                if (rightmostX > maxX) {
                    startX -= (rightmostX - maxX);
                }
                // Ensure leftmost enemy doesn't go too far left
                if (startX < minX) {
                    startX = minX;
                }
                
                for (let i = 0; i < count; i++) {
                    const idx = terrestrialIndices[i];
                    tempPositions[idx] = { 
                        x: startX + i * hSpacing, 
                        y: baseY,
                        isAerial: false 
                    };
                }
            }
            
            // Position aerial enemies in top area (flying)
            if (aerialIndices.length > 0) {
                const count = aerialIndices.length;
                let startX = baseX - ((count - 1) * hSpacing) / 2;
                const aerialY = baseY - aerialOffset;
                
                // Ensure rightmost enemy stays on screen
                const rightmostX = startX + (count - 1) * hSpacing;
                if (rightmostX > maxX) {
                    startX -= (rightmostX - maxX);
                }
                // Ensure leftmost enemy doesn't go too far left
                if (startX < minX) {
                    startX = minX;
                }
                
                for (let i = 0; i < count; i++) {
                    const idx = aerialIndices[i];
                    tempPositions[idx] = { 
                        x: startX + i * hSpacing, 
                        y: aerialY,
                        isAerial: true 
                    };
                }
            }
            
            // If all same type with 5+ enemies, use staggered formation
            if (enemyCount >= 5 && (aerialIndices.length === enemyCount || terrestrialIndices.length === enemyCount)) {
                const baseYForType = aerialIndices.length === enemyCount ? baseY - aerialOffset : baseY;
                const smallVOffset = isMobile ? 55 : 130;
                
                // For 5 enemies: staggered 2-1-2 pattern with wider spread
                if (enemyCount === 5) {
                    let startX = baseX - hSpacing;
                    if (startX + hSpacing * 2 > maxX) startX = maxX - hSpacing * 2;
                    if (startX - hSpacing < minX) startX = minX + hSpacing;
                    
                    tempPositions[0] = { x: startX - hSpacing * 0.7, y: baseYForType - smallVOffset, isAerial: aerialIndices.length === enemyCount };
                    tempPositions[1] = { x: startX + hSpacing * 0.7, y: baseYForType - smallVOffset, isAerial: aerialIndices.length === enemyCount };
                    tempPositions[2] = { x: startX, y: baseYForType - smallVOffset / 2, isAerial: aerialIndices.length === enemyCount };
                    tempPositions[3] = { x: startX - hSpacing * 0.7, y: baseYForType, isAerial: aerialIndices.length === enemyCount };
                    tempPositions[4] = { x: startX + hSpacing * 0.7, y: baseYForType, isAerial: aerialIndices.length === enemyCount };
                }
                // For 6 enemies: 3-3 rows
                else if (enemyCount === 6) {
                    let startX = baseX - hSpacing;
                    if (startX + hSpacing * 2 > maxX) startX = maxX - hSpacing * 2;
                    if (startX - hSpacing < minX) startX = minX + hSpacing;
                    
                    tempPositions[0] = { x: startX - hSpacing, y: baseYForType - smallVOffset, isAerial: aerialIndices.length === enemyCount };
                    tempPositions[1] = { x: startX, y: baseYForType - smallVOffset, isAerial: aerialIndices.length === enemyCount };
                    tempPositions[2] = { x: startX + hSpacing, y: baseYForType - smallVOffset, isAerial: aerialIndices.length === enemyCount };
                    tempPositions[3] = { x: startX - hSpacing, y: baseYForType, isAerial: aerialIndices.length === enemyCount };
                    tempPositions[4] = { x: startX, y: baseYForType, isAerial: aerialIndices.length === enemyCount };
                    tempPositions[5] = { x: startX + hSpacing, y: baseYForType, isAerial: aerialIndices.length === enemyCount };
                }
                // For 7 enemies: 3-1-3 pattern
                else if (enemyCount === 7) {
                    const isA = aerialIndices.length === enemyCount;
                    tempPositions[0] = { x: baseX - hSpacing, y: baseYForType - smallVOffset, isAerial: isA };
                    tempPositions[1] = { x: baseX,              y: baseYForType - smallVOffset, isAerial: isA };
                    tempPositions[2] = { x: baseX + hSpacing, y: baseYForType - smallVOffset, isAerial: isA };
                    tempPositions[3] = { x: baseX,              y: baseYForType - smallVOffset / 2, isAerial: isA };
                    tempPositions[4] = { x: baseX - hSpacing, y: baseYForType, isAerial: isA };
                    tempPositions[5] = { x: baseX,              y: baseYForType, isAerial: isA };
                    tempPositions[6] = { x: baseX + hSpacing, y: baseYForType, isAerial: isA };
                }
                // For 8 enemies: 3-2-3 pattern
                else if (enemyCount === 8) {
                    const isA = aerialIndices.length === enemyCount;
                    tempPositions[0] = { x: baseX - hSpacing, y: baseYForType - smallVOffset, isAerial: isA };
                    tempPositions[1] = { x: baseX,              y: baseYForType - smallVOffset, isAerial: isA };
                    tempPositions[2] = { x: baseX + hSpacing, y: baseYForType - smallVOffset, isAerial: isA };
                    tempPositions[3] = { x: baseX - hSpacing * 0.5, y: baseYForType - smallVOffset / 2, isAerial: isA };
                    tempPositions[4] = { x: baseX + hSpacing * 0.5, y: baseYForType - smallVOffset / 2, isAerial: isA };
                    tempPositions[5] = { x: baseX - hSpacing, y: baseYForType, isAerial: isA };
                    tempPositions[6] = { x: baseX,              y: baseYForType, isAerial: isA };
                    tempPositions[7] = { x: baseX + hSpacing, y: baseYForType, isAerial: isA };
                }
                // For 9 enemies: 3-3-3 pattern
                else if (enemyCount >= 9) {
                    const isA = aerialIndices.length === enemyCount;
                    tempPositions[0] = { x: baseX - hSpacing, y: baseYForType - smallVOffset, isAerial: isA };
                    tempPositions[1] = { x: baseX,              y: baseYForType - smallVOffset, isAerial: isA };
                    tempPositions[2] = { x: baseX + hSpacing, y: baseYForType - smallVOffset, isAerial: isA };
                    tempPositions[3] = { x: baseX - hSpacing, y: baseYForType - smallVOffset / 2, isAerial: isA };
                    tempPositions[4] = { x: baseX,              y: baseYForType - smallVOffset / 2, isAerial: isA };
                    tempPositions[5] = { x: baseX + hSpacing, y: baseYForType - smallVOffset / 2, isAerial: isA };
                    tempPositions[6] = { x: baseX - hSpacing, y: baseYForType, isAerial: isA };
                    tempPositions[7] = { x: baseX,              y: baseYForType, isAerial: isA };
                    tempPositions[8] = { x: baseX + hSpacing, y: baseYForType, isAerial: isA };
                }
            }
            
            // If 4 enemies of same type, use 2x2 grid
            if (enemyCount === 4 && (aerialIndices.length === 4 || terrestrialIndices.length === 4)) {
                const baseYForType = aerialIndices.length === 4 ? baseY - aerialOffset : baseY;
                const smallVOffset = isMobile ? 55 : 130;
                // Desktop: use full hSpacing so each enemy has its own column
                const colGap = isMobile ? hSpacing / 2 : hSpacing * 0.6;
                
                let centerX = baseX;
                if (centerX + colGap > maxX) centerX = maxX - colGap;
                if (centerX - colGap < minX) centerX = minX + colGap;
                
                tempPositions[0] = { x: centerX - colGap, y: baseYForType - smallVOffset, isAerial: aerialIndices.length === 4 };
                tempPositions[1] = { x: centerX + colGap, y: baseYForType - smallVOffset, isAerial: aerialIndices.length === 4 };
                tempPositions[2] = { x: centerX - colGap, y: baseYForType, isAerial: aerialIndices.length === 4 };
                tempPositions[3] = { x: centerX + colGap, y: baseYForType, isAerial: aerialIndices.length === 4 };
            }
            
            // If 3 enemies, triangle
            if (enemyCount === 3 && (aerialIndices.length === 3 || terrestrialIndices.length === 3)) {
                const baseYForType = aerialIndices.length === 3 ? baseY - aerialOffset : baseY;
                const smallVOffset = isMobile ? 55 : 130;
                // Desktop: wider spread so sprites don't overlap
                const colGap = isMobile ? hSpacing / 2 : hSpacing * 0.55;
                
                let centerX = baseX;
                if (centerX + colGap > maxX) centerX = maxX - colGap;
                if (centerX - colGap < minX) centerX = minX + colGap;
                
                tempPositions[0] = { x: centerX, y: baseYForType - smallVOffset, isAerial: aerialIndices.length === 3 };
                tempPositions[1] = { x: centerX - colGap, y: baseYForType, isAerial: aerialIndices.length === 3 };
                tempPositions[2] = { x: centerX + colGap, y: baseYForType, isAerial: aerialIndices.length === 3 };
            }
            
            return tempPositions;
        }

        startTimedBattle() {
            // Find all events and their SimTime values
            this.battleEvents = this.eventsList.map(evt => ({
                event: evt,
                simTime: getEventField(evt, 'SimTime') ?? getEventField(evt, 'simTime') ?? 0
            })).sort((a, b) => a.simTime - b.simTime);
            
            this.currentEventIndex = 0;
            this.battleStartTime = Date.now();
            this.currentSimTime = 0;
            this.battleFinished = false;
            this.isPlaying = true;
            
            // Process initial events (HP updates, BattleStart) that have simTime 0
            while (this.currentEventIndex < this.battleEvents.length) {
                const eventData = this.battleEvents[this.currentEventIndex];
                if (eventData.simTime > 0) break;
                this.processEvent(eventData.event);
                this.currentEventIndex++;
            }
        }

        // ── INTERACTIVE MODE METHODS ─────────────────────────────────────

        /** Initialize HP/action-time from the data sent by Blazor (no pre-computed events). */
        initInteractiveState() {
            // Player state from Blazor
            if (this.interactivePlayerHP != null) {
                this.playerCurrentHp = this.interactivePlayerHP;
                this.playerMaxHp = this.interactivePlayerMaxHP ?? this.interactivePlayerHP;
            }
            if (this.interactivePlayerActionTime != null) {
                this.playerActionTime = this.interactivePlayerActionTime;
            }

            // Enemy state from Blazor
            if (this.interactiveEnemies && this.interactiveEnemies.length > 0) {
                for (let i = 0; i < this.interactiveEnemies.length && i < this.enemyCount; i++) {
                    const e = this.interactiveEnemies[i];
                    const hp = e.hp ?? e.HP ?? 100;
                    const maxHp = e.maxHP ?? e.MaxHP ?? hp;
                    const at = e.actionTime ?? e.ActionTime ?? 3.5;
                    this.enemyHPs[i] = { current: hp, max: maxHp };
                    this.enemyActionTimes[i] = at;
                }
            }

            // Initialize UI bars
            this.updatePlayerHPBar();
            for (let i = 0; i < this.enemyCount; i++) {
                this.updateIndividualEnemyHPBar(i);
            }

            // Init speed bar timers
            this.playerSpeedBarTimer = this.playerActionTime * 1000;
            for (let i = 0; i < this.enemyCount; i++) {
                this.enemySpeedBarTimers[i] = this.enemyActionTimes[i] * 1000;
            }

            // Initialize spell cooldowns — preserve existing values across stages
            for (const spell of this.spells) {
                const id = spell.attackId ?? spell.AttackId;
                // Only reset to 0 if no preserved cooldown exists
                if (this.spellCooldowns[id] == null) {
                    this.spellCooldowns[id] = 0;
                }
            }
        }

        /** Create the spell button bar — horizontal, centered, above the consumable bar. */
        createSpellBar() {
            if (!this.spells || this.spells.length === 0) return;

            const width = this.app.screen.width;
            const height = this.app.screen.height;
            const isMobile = width < 768;
            const btnSize = isMobile
                ? Math.min(52, Math.floor(width / (this.spells.length + 2)))
                : Math.min(68, Math.floor(width / (this.spells.length + 2)));
            const btnGap = isMobile ? 6 : 10;
            const totalWidth = this.spells.length * btnSize + (this.spells.length - 1) * btnGap;
            const startX = (width - totalWidth) / 2;
            // Position above consumable bar — must match actual consumable btn sizing
            const consumableBtnSize = isMobile ? 60 : 76;
            const barY = height - consumableBtnSize - 10 - btnSize - 18;

            this.spellBarContainer = new PIXI.Container();
            this.stage.addChild(this.spellBarContainer);

            // Semi-transparent backdrop behind spell bar
            const backdrop = new PIXI.Graphics();
            backdrop.roundRect(startX - 8, barY - 4, totalWidth + 16, btnSize + 8, 10);
            backdrop.fill({ color: 0x0d1117, alpha: 0.65 });
            this.spellBarContainer.addChild(backdrop);

            this.spellButtons = [];

            for (let i = 0; i < this.spells.length; i++) {
                const spell = this.spells[i];
                const attackId = spell.attackId ?? spell.AttackId;
                const name = spell.name ?? spell.Name ?? attackId;
                const icon = spell.icon ?? spell.Icon ?? '⚡';
                const cooldown = spell.cooldownSeconds ?? spell.CooldownSeconds ?? 10;
                const x = startX + i * (btnSize + btnGap);

                const btnContainer = new PIXI.Container();
                btnContainer.x = x;
                btnContainer.y = barY;

                // Button background
                const bg = new PIXI.Graphics();
                bg.roundRect(0, 0, btnSize, btnSize, 8);
                bg.fill({ color: 0x1a1a3a, alpha: 0.95 });
                bg.stroke({ color: 0x5566cc, width: 2 });
                btnContainer.addChild(bg);

                // Icon text
                const iconText = new PIXI.Text({
                    text: icon,
                    style: { fontSize: isMobile ? Math.min(22, btnSize * 0.4) : Math.min(28, btnSize * 0.42), fontFamily: 'Arial, sans-serif', fill: 0xffffff }
                });
                iconText.anchor.set(0.5);
                iconText.x = btnSize / 2;
                iconText.y = btnSize / 2 - 4;
                btnContainer.addChild(iconText);

                // Spell name (small, below icon)
                const nameText = new PIXI.Text({
                    text: name.length > 7 ? name.substring(0, 7) : name,
                    style: { fontSize: isMobile ? 7 : 9, fontFamily: 'Arial, sans-serif', fill: 0x999999 }
                });
                nameText.anchor.set(0.5);
                nameText.x = btnSize / 2;
                nameText.y = btnSize - 5;
                btnContainer.addChild(nameText);

                // Cooldown overlay
                const cdOverlay = new PIXI.Graphics();
                cdOverlay.roundRect(0, 0, btnSize, btnSize, 8);
                cdOverlay.fill({ color: 0x000000, alpha: 0.7 });
                cdOverlay.visible = false;
                btnContainer.addChild(cdOverlay);

                // Cooldown timer text
                const cdText = new PIXI.Text({
                    text: '',
                    style: { fontSize: 14, fontFamily: 'Arial, sans-serif', fontWeight: 'bold', fill: 0xffffff }
                });
                cdText.anchor.set(0.5);
                cdText.x = btnSize / 2;
                cdText.y = btnSize / 2;
                cdText.visible = false;
                btnContainer.addChild(cdText);

                // Make interactive
                btnContainer.eventMode = 'static';
                btnContainer.cursor = 'pointer';
                btnContainer.on('pointerdown', () => this.onSpellButtonClick(attackId));

                this.spellBarContainer.addChild(btnContainer);

                this.spellButtons.push({
                    container: btnContainer,
                    bg,
                    iconText,
                    nameText,
                    cdOverlay,
                    cdText,
                    attackId,
                    cooldownSeconds: cooldown,
                    spell
                });
            }
        }

        /** Create the consumable bar at the bottom of the canvas (healing items: Fino, Caneca). */
        createConsumableBar() {
            const width = this.app.screen.width;
            const height = this.app.screen.height;
            const isMobile = width < 768;
            const btnGap = isMobile ? 6 : 10;

            const allConsumables = [
                { type: 'fino',    name: 'Fino',    color: 0xf5a623, fallbackIcon: '🍺' },
                { type: 'caneca',  name: 'Caneca',  color: 0xf5a623, fallbackIcon: '🍻' }
            ];
            // Only render buttons for consumables the player actually has
            const consumables = allConsumables.filter(c => (this.consumableQuantities[c.type] ?? 0) > 0);
            if (consumables.length === 0) return; // Nothing to show

            // Responsive button size — larger on desktop
            const maxBarWidth = isMobile ? Math.min(width * 0.9, 320) : Math.min(width * 0.9, 420);
            const btnSize = isMobile
                ? Math.min(60, Math.floor((maxBarWidth - (consumables.length - 1) * btnGap) / consumables.length))
                : Math.min(76, Math.floor((maxBarWidth - (consumables.length - 1) * btnGap) / consumables.length));

            const totalWidth = consumables.length * btnSize + (consumables.length - 1) * btnGap;
            const startX = (width - totalWidth) / 2;
            const barY = height - btnSize - 10;

            this.consumableBarContainer = new PIXI.Container();
            this.stage.addChild(this.consumableBarContainer);

            // Semi-transparent backdrop
            const backdrop = new PIXI.Graphics();
            backdrop.roundRect(startX - 8, barY - 6, totalWidth + 16, btnSize + 12, 10);
            backdrop.fill({ color: 0x0d1117, alpha: 0.7 });
            this.consumableBarContainer.addChild(backdrop);

            this.consumableButtons = [];

            for (let i = 0; i < consumables.length; i++) {
                const c = consumables[i];
                const qty = this.consumableQuantities[c.type] ?? 0;
                const x = startX + i * (btnSize + btnGap);

                const btnContainer = new PIXI.Container();
                btnContainer.x = x;
                btnContainer.y = barY;

                // Button background
                const bg = new PIXI.Graphics();
                bg.roundRect(0, 0, btnSize, btnSize, 8);
                bg.fill({ color: qty > 0 ? 0x1a2332 : 0x1a1a1a, alpha: 0.95 });
                bg.stroke({ color: qty > 0 ? c.color : 0x444444, width: 2 });
                btnContainer.addChild(bg);

                // Sprite image from CDN (or fallback emoji)
                const imageUrl = this.consumableImages[c.type];
                let iconDisplay;
                if (imageUrl) {
                    // Load async sprite — start with placeholder, swap when ready
                    const spriteAlias = `consumable_${c.type}_${imageUrl}`;
                    const spriteContainer = new PIXI.Container();
                    spriteContainer.x = btnSize / 2;
                    spriteContainer.y = btnSize / 2 - 2;
                    btnContainer.addChild(spriteContainer);

                    // Load sprite texture asynchronously
                    (async () => {
                        try {
                            if (!loadedAssetAliases.has(spriteAlias)) {
                                await PIXI.Assets.load({ alias: spriteAlias, src: imageUrl });
                                loadedAssetAliases.add(spriteAlias);
                            }
                            const spr = PIXI.Sprite.from(spriteAlias);
                            spr.anchor.set(0.5);
                            const maxDim = btnSize * 0.6;
                            const scale = Math.min(maxDim / spr.width, maxDim / spr.height);
                            spr.scale.set(scale);
                            spriteContainer.addChild(spr);
                        } catch (e) {
                            // Fallback: show emoji icon
                            const fallback = new PIXI.Text({
                                text: c.fallbackIcon,
                                style: { fontSize: Math.min(22, btnSize * 0.45), fontFamily: 'Arial, sans-serif' }
                            });
                            fallback.anchor.set(0.5);
                            spriteContainer.addChild(fallback);
                        }
                    })();
                    iconDisplay = spriteContainer;
                } else {
                    // No image URL — show emoji fallback
                    const fallbackText = new PIXI.Text({
                        text: c.fallbackIcon,
                        style: { fontSize: Math.min(22, btnSize * 0.45), fontFamily: 'Arial, sans-serif' }
                    });
                    fallbackText.anchor.set(0.5);
                    fallbackText.x = btnSize / 2;
                    fallbackText.y = btnSize / 2 - 2;
                    btnContainer.addChild(fallbackText);
                    iconDisplay = fallbackText;
                }

                // Name label at bottom
                const nameText = new PIXI.Text({
                    text: c.name.length > 7 ? c.name.substring(0, 7) : c.name,
                    style: { fontSize: 7, fontFamily: 'Arial, sans-serif', fill: 0x888888 }
                });
                nameText.anchor.set(0.5);
                nameText.x = btnSize / 2;
                nameText.y = btnSize - 5;
                btnContainer.addChild(nameText);

                // Quantity badge (top-right corner)
                const qtyBg = new PIXI.Graphics();
                qtyBg.circle(btnSize - 4, 4, 9);
                qtyBg.fill({ color: qty > 0 ? 0x2e7d32 : 0x444444, alpha: 0.95 });
                btnContainer.addChild(qtyBg);

                const qtyText = new PIXI.Text({
                    text: qty.toString(),
                    style: { fontSize: 9, fontFamily: 'Arial, sans-serif', fontWeight: 'bold', fill: 0xffffff }
                });
                qtyText.anchor.set(0.5);
                qtyText.x = btnSize - 4;
                qtyText.y = 4;
                btnContainer.addChild(qtyText);

                // Greyed out overlay when qty == 0 or buff already active
                const emptyOverlay = new PIXI.Graphics();
                emptyOverlay.roundRect(0, 0, btnSize, btnSize, 8);
                emptyOverlay.fill({ color: 0x000000, alpha: 0.6 });
                const isBuffType = ['cigarro', 'canhao'].includes(c.type);
                const isActive = isBuffType && (this.activeBuffs[c.type] ?? false);
                emptyOverlay.visible = qty <= 0 || isActive;
                btnContainer.addChild(emptyOverlay);

                // Cooldown overlay
                const cdOverlay = new PIXI.Graphics();
                cdOverlay.roundRect(0, 0, btnSize, btnSize, 8);
                cdOverlay.fill({ color: 0x000000, alpha: 0.65 });
                const initCd = this.consumableCooldowns[c.type] ?? 0;
                cdOverlay.visible = initCd > 0;
                btnContainer.addChild(cdOverlay);

                // Cooldown timer text
                const cdText = new PIXI.Text({
                    text: initCd > 0 ? `${Math.ceil(initCd)}s` : '',
                    style: { fontSize: 12, fontFamily: 'Arial, sans-serif', fontWeight: 'bold', fill: 0xff6666 }
                });
                cdText.anchor.set(0.5);
                cdText.x = btnSize / 2;
                cdText.y = btnSize / 2;
                cdText.visible = initCd > 0;
                btnContainer.addChild(cdText);

                // Interactive
                btnContainer.eventMode = 'static';
                const isOnCooldown = initCd > 0;
                btnContainer.cursor = (qty > 0 && !isOnCooldown && !isActive) ? 'pointer' : 'not-allowed';
                const consumableType = c.type;
                btnContainer.on('pointerdown', () => this.onConsumableClick(consumableType));

                this.consumableBarContainer.addChild(btnContainer);

                this.consumableButtons.push({
                    container: btnContainer, bg, iconText: iconDisplay, nameText,
                    qtyBg, qtyText, emptyOverlay, cdOverlay, cdText,
                    type: c.type, color: c.color, btnSize,
                    isBuffType: ['cigarro', 'canhao'].includes(c.type)
                });
            }
        }

        /** Called when a consumable button is clicked. */
        onConsumableClick(type) {
            if (this.battleFinished || !this.isPlaying) return;
            if (this._consumablePending) return;
            const qty = this.consumableQuantities[type] ?? 0;
            if (qty <= 0) return;
            // Block if consumable is on cooldown
            const cd = this.consumableCooldowns[type] ?? 0;
            if (cd > 0) return;
            // Block if this buff is already active
            const isBuffType = ['cigarro', 'canhao'].includes(type);
            if (isBuffType && this.activeBuffs[type]) return;

            this._consumablePending = true;
            this.requestUseConsumable(type);
        }

        /** Call server to use a consumable during combat. */
        async requestUseConsumable(type) {
            if (!this.dotNetRef || this.battleFinished) {
                this._consumablePending = false;
                return;
            }
            try {
                const json = await this.dotNetRef.invokeMethodAsync('OnUseConsumable', type);
                if (json) {
                    const result = JSON.parse(json);
                    if (result.success) {
                        // Update quantity
                        this.consumableQuantities[type] = result.newQuantity ?? 0;
                        this.updateConsumableButton(type);

                        // Start consumable cooldown if server returned one
                        if (result.cooldownSeconds != null && result.cooldownSeconds > 0) {
                            this.consumableCooldowns[type] = result.cooldownSeconds;
                            this.updateConsumableButton(type);
                        }

                        // Handle heal (Fino / Caneca)
                        if (result.playerHP != null) {
                            this.playerCurrentHp = result.playerHP;
                            this.playerMaxHp = result.playerMaxHP ?? this.playerMaxHp;
                            this.updatePlayerHPBar();
                            // Show heal floating text
                            if (result.healAmount && result.healAmount > 0 && this.playerSprite) {
                                this.showFloatingText(`+${formatNum(result.healAmount)} HP`,
                                    this.playerSprite.x, this.playerSprite.y - (this.playerSprite.height || 40) - 20, 0x44ff44);
                                this.playBuffVfx(this.playerSprite, 0x44ff44);
                            }
                        }

                        // Handle buff activation (cigarro, canhao, shot, penalty)
                        if (result.buffMessage && this.playerSprite) {
                            const buffColor = type === 'cigarro' ? 0x90caf9 : type === 'canhao' ? 0xef5350
                                : type === 'shot' ? 0xab47bc : 0xffee58;
                            this.showFloatingText(result.buffMessage,
                                this.playerSprite.x, this.playerSprite.y - (this.playerSprite.height || 40) - 20, buffColor);
                            this.playBuffVfx(this.playerSprite, buffColor);
                        }

                        // Mark buff as active and update button visual
                        if (result.buffActive) {
                            this.activeBuffs[type] = true;
                            this.updateConsumableButton(type);
                        }

                        // Handle penalty speed change — update the action time for the speed bar
                        if (result.newActionTime != null && result.newActionTime > 0) {
                            this.playerActionTime = result.newActionTime;
                            this.updatePlayerSpeedBar();
                        }

                        // Show shot aura if Shot was used
                        if (type === 'shot' && !this.playerAura && this.playerSprite) {
                            const auraSize = this.playerDisplayHeight * 0.7;
                            this.playerAura = new PIXI.Graphics();
                            this.playerAura.circle(0, 0, auraSize);
                            this.playerAura.fill({ color: 0x44bbff, alpha: 0.35 });
                            this.playerAura.x = this.playerSprite.x;
                            this.playerAura.y = this.playerSprite.y - this.playerDisplayHeight / 2;
                            const playerIdx = this.stage.getChildIndex(this.playerSprite);
                            this.stage.addChildAt(this.playerAura, playerIdx);
                            this.hasShotBuff = true;
                        }
                    } else {
                        // Show failure message
                        if (this.playerSprite) {
                            this.showFloatingText(result.message ?? 'Sem stock!',
                                this.playerSprite.x, this.playerSprite.y - (this.playerSprite.height || 40) - 20, 0xff4444);
                        }
                    }
                }
            } catch (e) {
                console.warn('OnUseConsumable error:', e);
            } finally {
                this._consumablePending = false;
            }
        }

        /** Update a single consumable button after use. */
        updateConsumableButton(type) {
            const btn = this.consumableButtons.find(b => b.type === type);
            if (!btn) return;
            const sz = btn.btnSize ?? 48;
            const qty = this.consumableQuantities[type] ?? 0;
            const isActive = this.activeBuffs[type] ?? false;
            const cd = this.consumableCooldowns[type] ?? 0;
            const isOnCooldown = cd > 0;
            const isDisabled = qty <= 0 || (btn.isBuffType && isActive) || isOnCooldown;

            // Update quantity text
            btn.qtyText.text = qty.toString();

            // Update badge color
            btn.qtyBg.clear();
            btn.qtyBg.circle(sz - 4, 4, 9);
            btn.qtyBg.fill({ color: qty > 0 ? 0x2e7d32 : 0x444444, alpha: 0.95 });

            // Update button style - active buffs get bright glow
            btn.bg.clear();
            btn.bg.roundRect(0, 0, sz, sz, 8);
            if (isActive) {
                btn.bg.fill({ color: btn.color, alpha: 0.35 });
                btn.bg.stroke({ color: btn.color, width: 3 });
            } else {
                btn.bg.fill({ color: qty > 0 ? 0x1a2332 : 0x1a1a1a, alpha: 0.95 });
                btn.bg.stroke({ color: qty > 0 ? btn.color : 0x444444, width: 2 });
            }

            // Update name label - show "ATIVO" when active
            btn.nameText.text = isActive ? 'ATIVO' : (type === 'canhao' ? 'Canhão' : type.charAt(0).toUpperCase() + type.slice(1));
            btn.nameText.style.fill = isActive ? btn.color : 0x888888;
            btn.nameText.style.fontWeight = isActive ? 'bold' : 'normal';

            // Cooldown overlay and timer
            if (btn.cdOverlay) {
                btn.cdOverlay.visible = isOnCooldown;
            }
            if (btn.cdText) {
                btn.cdText.visible = isOnCooldown;
                btn.cdText.text = isOnCooldown ? `${Math.ceil(cd)}s` : '';
            }

            // Show/hide empty overlay (hidden when cooldown overlay is showing)
            btn.emptyOverlay.visible = !isOnCooldown && (qty <= 0 || (btn.isBuffType && isActive));
            btn.container.cursor = isDisabled ? 'not-allowed' : 'pointer';
            // Hide the entire button when qty reaches 0 (only show when player has the item)
            btn.container.visible = qty > 0;
        }

        /** Update cooldown visuals for all consumable buttons (called each tick). */
        updateConsumableCooldownVisuals() {
            for (const btn of this.consumableButtons) {
                const cd = this.consumableCooldowns[btn.type] ?? 0;
                if (btn.cdOverlay) {
                    btn.cdOverlay.visible = cd > 0;
                }
                if (btn.cdText) {
                    btn.cdText.visible = cd > 0;
                    btn.cdText.text = cd > 0 ? `${Math.ceil(cd)}s` : '';
                }
                // Update cursor
                const qty = this.consumableQuantities[btn.type] ?? 0;
                const isActive = this.activeBuffs[btn.type] ?? false;
                const isDisabled = qty <= 0 || (btn.isBuffType && isActive) || cd > 0;
                btn.container.cursor = isDisabled ? 'not-allowed' : 'pointer';
            }
        }

        /** Start interactive battle — no pre-computed events, driven by speed bars + server calls. */
        startInteractiveBattle() {
            this.battleStartTime = Date.now();
            this.currentSimTime = 0;
            this.battleFinished = false;
            this.isPlaying = true;
            this._playerAttackPending = false;
            this._enemyAttackPending = Array(this.enemyCount).fill(false);
            this._spellPending = false;
            this._cooldownTickAccum = 0;
            this._consumableTickAccum = 0;
        }

        /** Called when a spell button is clicked. */
        onSpellButtonClick(attackId) {
            if (this.battleFinished || !this.isPlaying) return;
            if (this._spellPending) return;
            // Check cooldown
            const cd = this.spellCooldowns[attackId] ?? 0;
            if (cd > 0) return;

            this._spellPending = true;
            this.requestPlayerSpell(attackId);
        }

        /** Call server: player auto-attack. */
        async requestPlayerAutoAttack() {
            if (!this.dotNetRef || this.battleFinished) {
                this._playerAttackPending = false;
                return;
            }
            try {
                const json = await this.dotNetRef.invokeMethodAsync('OnPlayerAutoAttack');
                if (json) this.processServerResult(JSON.parse(json));
            } catch (e) {
                console.warn('OnPlayerAutoAttack error:', e);
            } finally {
                this._playerAttackPending = false;
            }
        }

        /** Call server: enemy auto-attack. */
        async requestEnemyAttack(enemyIndex) {
            if (!this.dotNetRef || this.battleFinished) {
                this._enemyAttackPending[enemyIndex] = false;
                return;
            }
            try {
                const json = await this.dotNetRef.invokeMethodAsync('OnEnemyAttack', enemyIndex);
                if (json) this.processServerResult(JSON.parse(json));
            } catch (e) {
                console.warn('OnEnemyAttack error:', e);
            } finally {
                this._enemyAttackPending[enemyIndex] = false;
            }
        }

        /** Call server: player spell cast. */
        async requestPlayerSpell(attackId) {
            if (!this.dotNetRef || this.battleFinished) {
                this._spellPending = false;
                return;
            }
            try {
                const json = await this.dotNetRef.invokeMethodAsync('OnPlayerSpell', attackId);
                if (json) this.processServerResult(JSON.parse(json));
            } catch (e) {
                console.warn('OnPlayerSpell error:', e);
            } finally {
                this._spellPending = false;
            }
        }

        /** Call server: tick cooldowns. */
        async requestTickCooldowns(elapsedSeconds) {
            if (!this.dotNetRef || this.battleFinished) return;
            try {
                const json = await this.dotNetRef.invokeMethodAsync('OnTickCooldowns', elapsedSeconds);
                if (json) {
                    const data = JSON.parse(json);
                    // New format: { spells: {...} }
                    const spellCooldowns = data.spells ?? data;
                    for (const [id, remaining] of Object.entries(spellCooldowns)) {
                        this.spellCooldowns[id] = remaining;
                    }
                }
            } catch (e) {
                console.warn('OnTickCooldowns error:', e);
            }
        }

        /** Tick consumable cooldowns on the server using real elapsed time (not scaled by battle speed). */
        async requestTickConsumableCooldowns(realElapsedSeconds) {
            if (!this.dotNetRef || this.battleFinished) return;
            try {
                const json = await this.dotNetRef.invokeMethodAsync('OnTickConsumableCooldowns', realElapsedSeconds);
                if (json) {
                    const data = JSON.parse(json);
                    for (const [type, remaining] of Object.entries(data)) {
                        this.consumableCooldowns[type] = remaining;
                    }
                    this.updateConsumableCooldownVisuals();
                }
            } catch (e) {
                console.warn('OnTickConsumableCooldowns error:', e);
            }
        }

        /** Process a CombatActionResult from the server — animate its events. */
        processServerResult(result) {
            if (!result) return;

            // Process each event in the result
            const events = result.events ?? result.Events ?? [];
            for (const evt of events) {
                this.processInteractiveEvent(evt);
            }

            // Update cooldowns from server
            const cooldowns = result.spellCooldowns ?? result.SpellCooldowns;
            if (cooldowns) {
                for (const [id, remaining] of Object.entries(cooldowns)) {
                    this.spellCooldowns[id] = remaining;
                }
            }

            // Check if battle is over
            const battleOver = result.battleOver ?? result.BattleOver ?? false;
            if (battleOver) {
                this.isPlaying = false;
                // Determine outcome
                const outcome = result.outcome ?? result.Outcome;
                // outcome: 0=AttackerWon, 1=DefenderWon, 2=Draw (matches BattleOutcome enum)
                if (outcome === 0) {
                    // Player won
                    this.handleVictory({ Type: 'Victory', Winner: 'Player' });
                } else if (outcome === 2) {
                    this.handleDraw();
                } else {
                    // Player lost
                    this.handleVictory({ Type: 'Victory', Winner: 'Enemy' });
                }
            }
        }

        /** Process a single interactive event (from server response). */
        processInteractiveEvent(evt) {
            const evtType = evt.type ?? evt.Type;
            const attackId = evt.attackId ?? evt.AttackId;

            switch (evtType) {
                case 'HPUpdate':
                    this.handleHPUpdate(evt);
                    break;
                case 'Attack':
                    if (attackId) {
                        // This is a spell attack — play spell VFX
                        this.handleSpellAttack(evt);
                    } else {
                        this.handleAttack(evt);
                    }
                    break;
                case 'KO':
                    this.handleKO(evt);
                    break;
                case 'StatusEffect':
                    this.handleStatusEffect(evt);
                    break;
                case 'Victory':
                    // Don't process here — handled in processServerResult
                    break;
                case 'BattleStart':
                    break;
            }
        }

        /** Handle a spell attack event — play VFX based on vfxType. */
        handleSpellAttack(evt) {
            const attacker = evt.attacker ?? evt.Attacker;
            const defender = evt.defender ?? evt.Defender;
            const damage = evt.damage ?? evt.Damage ?? 0;
            const isCritical = evt.isCritical ?? evt.IsCritical ?? false;
            const vfxType = evt.vfxType ?? evt.VfxType; // 0-6
            const vfxColor = evt.vfxColor ?? evt.VfxColor ?? '#ff6600';
            const screenShake = evt.screenShake ?? evt.ScreenShake ?? false;
            const visualHint = evt.visualHint ?? evt.VisualHint;
            const abilityName = evt.abilityName ?? evt.AbilityName ?? 'Spell';
            const targets = evt.targets ?? evt.Targets ?? [];
            const targetDamages = evt.targetDamages ?? evt.TargetDamages ?? [];
            const effectName = evt.effectName ?? evt.EffectName;

            // Parse hex color to number
            const color = typeof vfxColor === 'string' && vfxColor.startsWith('#')
                ? parseInt(vfxColor.replace('#', ''), 16)
                : (typeof vfxColor === 'number' ? vfxColor : 0xff6600);

            // Screen shake
            if (screenShake || visualHint === 'screenShake') this.screenShake();

            if (attacker === 'Player' || attacker === 'Attacker') {
                // ── PLAYER SPELL ──
                this.animatePlayerAttack(isCritical);

                // Show ability name above player
                if (this.playerSprite) {
                    this.showFloatingText(abilityName.toUpperCase(), this.playerSprite.x, 
                        this.playerSprite.y - (this.playerSprite.height || 40) - 20, color);
                }

                if (defender === 'Player' || defender === 'Attacker') {
                    // Self-buff/heal — VFX on player
                    this.playBuffVfx(this.playerSprite, color);
                    if (damage < 0 && this.playerSprite) {
                        // Negative damage = healing
                        this.showFloatingText(`+${formatNum(Math.abs(damage))}`, this.playerSprite.x,
                            this.playerSprite.y - (this.playerSprite.height || 40) * 0.8, 0x44ff44);
                    }
                    // Show effect label on self
                    if (effectName && this.playerSprite) {
                        this.showEffectLabel(effectName, this.playerSprite);
                    }
                } else if (targets.length > 1 || vfxType === 2) {
                    // AoE — hit multiple enemies
                    for (let t = 0; t < targets.length; t++) {
                        const targetId = targets[t];
                        const dmg = targetDamages[t] ?? damage;
                        const enemyIndex = this.resolveEnemyIndex(targetId);
                        if (enemyIndex >= 0) {
                            this.playSpellVfx(vfxType, color, this.playerSprite, this.enemySprites[enemyIndex]);
                            this.flashEnemy(enemyIndex, isCritical);
                            const enemy = this.enemySprites[enemyIndex];
                            if (enemy && dmg > 0) {
                                this.showDamageText(dmg, isCritical, enemy.x, enemy.y - (enemy.height || 40) * 0.8);
                            }
                            // Show effect label on enemy
                            if (effectName && enemy) {
                                this.showEffectLabel(effectName, enemy);
                            }
                        }
                    }
                } else {
                    // Single target
                    const enemyIndex = defender ? this.resolveEnemyIndex(defender) : 0;
                    if (enemyIndex >= 0 && this.enemySprites[enemyIndex]) {
                        this.playSpellVfx(vfxType, color, this.playerSprite, this.enemySprites[enemyIndex]);
                        this.flashEnemy(enemyIndex, isCritical);
                        const enemy = this.enemySprites[enemyIndex];
                        if (enemy && damage > 0) {
                            this.showDamageText(damage, isCritical, enemy.x, enemy.y - (enemy.height || 40) * 0.8);
                        }
                        // Show effect label on enemy
                        if (effectName && enemy) {
                            this.showEffectLabel(effectName, enemy);
                        }
                    }
                }

                // Play spell-specific sound instead of generic attack
                const attackId = evt.attackId ?? evt.AttackId;
                if (attackId) {
                    this.playSpellSound(attackId);
                } else {
                    this.playSound(isCritical ? 'critical' : 'attack');
                }
            }
        }

        /** Show a brief status effect label floating above a sprite. */
        showEffectLabel(effectName, sprite) {
            const labels = {
                sleep: '💤 SLEEP', bleed: '🩸 BLEED', slow: '🐌 SLOW',
                vulnerable: '⚡ VULN', powerboost: '💪 POWER UP', haste: '⚡ HASTE',
                shield: '🛡️ SHIELD', defensebreak: '💥 DEF BREAK',
                defenseboost: '🛡️ DEF UP', regen: '💚 REGEN'
            };
            const colors = {
                sleep: 0x9966ff, bleed: 0xff3333, slow: 0x6699cc,
                vulnerable: 0xffaa00, powerboost: 0xff6600, haste: 0x00ff88,
                shield: 0x4488ff, defensebreak: 0xff4444, defenseboost: 0x4488ff,
                regen: 0x44ff44
            };
            const label = labels[effectName] || effectName.toUpperCase();
            const color = colors[effectName] || 0xffffff;
            this.showFloatingText(label, sprite.x, sprite.y - (sprite.height || 40) - 30, color);
        }

        /** Resolve "Enemy0", "Enemy1", etc. to an index. */
        resolveEnemyIndex(identifier) {
            if (!identifier) return 0;
            if (identifier === 'Defender') return 0;
            if (identifier.startsWith('Enemy')) {
                const idx = parseInt(identifier.replace('Enemy', ''));
                return isNaN(idx) ? 0 : idx;
            }
            return 0;
        }

        /** Handle status effect events (sleep, bleed ticks, etc.) */
        handleStatusEffect(evt) {
            const character = evt.character ?? evt.Character;
            const effectName = evt.effectName ?? evt.EffectName ?? '';
            const damage = evt.damage ?? evt.Damage ?? 0;

            const effectLabels = {
                sleep: '💤 SLEEP',
                bleed: '🩸 BLEED',
                slow: '🐌 SLOW',
                vulnerable: '⚡ VULN',
                powerboost: '💪 POWER UP',
                haste: '⚡ HASTE',
                shield: '🛡️ SHIELD',
                defensebreak: '💥 DEF BREAK',
                defenseboost: '🛡️ DEF UP',
                regen: '💚 REGEN'
            };

            const label = effectLabels[effectName] || effectName.toUpperCase();
            const effectColors = {
                sleep: 0x9966ff, bleed: 0xff3333, slow: 0x6699cc,
                vulnerable: 0xffaa00, powerboost: 0xff6600, haste: 0x00ff88,
                shield: 0x4488ff, defensebreak: 0xff4444, defenseboost: 0x4488ff,
                regen: 0x44ff44
            };
            const color = effectColors[effectName] || 0xffffff;

            if (character === 'Player' || character === 'Attacker') {
                if (this.playerSprite) {
                    this.showFloatingText(label, this.playerSprite.x,
                        this.playerSprite.y - (this.playerSprite.height || 40) - 10, color);
                }
            } else {
                const idx = this.resolveEnemyIndex(character);
                if (idx >= 0 && this.enemySprites[idx]) {
                    const enemy = this.enemySprites[idx];
                    this.showFloatingText(label, enemy.x,
                        enemy.y - (enemy.height || 40) - 10, color);
                    if (damage > 0) {
                        this.showDamageText(damage, false, enemy.x, enemy.y - (enemy.height || 40) * 0.8);
                    }
                }
            }
        }

        // ── SPELL VFX ──────────────────────────────────────────────────────

        /** Dispatch to the right VFX based on type. */
        playSpellVfx(vfxType, color, source, target) {
            if (!source || !target || !this.stage) return;

            const srcX = source.x;
            const srcY = source.y - (source.height || 40) / 2;
            const tgtX = target.x;
            const tgtY = target.y - (target.height || 40) / 2;

            switch (vfxType) {
                case 0: // Projectile
                    this.createProjectileVfx(color, srcX, srcY, tgtX, tgtY);
                    break;
                case 1: // Beam
                    this.createBeamVfx(color, tgtX, tgtY);
                    break;
                case 2: // AreaOfEffect
                    this.createAoeVfx(color, tgtX, tgtY);
                    break;
                case 4: // MeleeStrike
                    this.createMeleeStrikeVfx(color, tgtX, tgtY);
                    break;
                case 5: // SoundWave — expanding concentric rings
                    this.createSoundWaveVfx(color, srcX, srcY, tgtX, tgtY);
                    break;
                case 6: // MusicNotes — floating music note particles
                    this.createMusicNotesVfx(color, srcX, srcY, tgtX, tgtY);
                    break;
                default:
                    this.createProjectileVfx(color, srcX, srcY, tgtX, tgtY);
            }
        }

        /** Animated circle traveling from source to target. */
        createProjectileVfx(color, srcX, srcY, tgtX, tgtY) {
            const proj = new PIXI.Graphics();
            proj.circle(0, 0, 8);
            proj.fill({ color, alpha: 0.9 });
            proj.x = srcX;
            proj.y = srcY;
            this.stage.addChild(proj);

            // Trail glow
            const glow = new PIXI.Graphics();
            glow.circle(0, 0, 14);
            glow.fill({ color, alpha: 0.3 });
            glow.x = srcX;
            glow.y = srcY;
            this.stage.addChild(glow);

            const duration = 350 / this.battleSpeed;
            const startTime = Date.now();
            const animate = () => {
                const elapsed = Date.now() - startTime;
                const t = Math.min(elapsed / duration, 1);
                proj.x = srcX + (tgtX - srcX) * t;
                proj.y = srcY + (tgtY - srcY) * t;
                glow.x = proj.x;
                glow.y = proj.y;
                glow.alpha = 0.3 * (1 - t * 0.5);

                if (t < 1) {
                    requestAnimationFrame(animate);
                } else {
                    // Impact flash
                    const flash = new PIXI.Graphics();
                    flash.circle(0, 0, 20);
                    flash.fill({ color, alpha: 0.8 });
                    flash.x = tgtX;
                    flash.y = tgtY;
                    this.stage.addChild(flash);
                    this.animateTo(flash, { alpha: 0, scale: 2 }, 200, () => {
                        if (flash.parent) flash.parent.removeChild(flash);
                        flash.destroy();
                    });
                    if (proj.parent) proj.parent.removeChild(proj);
                    proj.destroy();
                    if (glow.parent) glow.parent.removeChild(glow);
                    glow.destroy();
                }
            };
            requestAnimationFrame(animate);
        }

        /** Vertical beam dropping on target. */
        createBeamVfx(color, tgtX, tgtY) {
            const beam = new PIXI.Graphics();
            beam.rect(-4, -200, 8, 200);
            beam.fill({ color, alpha: 0.8 });
            beam.x = tgtX;
            beam.y = tgtY;
            beam.alpha = 0;
            this.stage.addChild(beam);

            this.animateTo(beam, { alpha: 1 }, 100, () => {
                this.animateTo(beam, { alpha: 0 }, 400, () => {
                    if (beam.parent) beam.parent.removeChild(beam);
                    beam.destroy();
                });
            });
        }

        /** Expanding ring at target area. */
        createAoeVfx(color, tgtX, tgtY) {
            const ring = new PIXI.Graphics();
            ring.circle(0, 0, 10);
            ring.stroke({ color, width: 3, alpha: 0.9 });
            ring.x = tgtX;
            ring.y = tgtY;
            this.stage.addChild(ring);

            this.animateTo(ring, { scale: 6, alpha: 0 }, 500, () => {
                if (ring.parent) ring.parent.removeChild(ring);
                ring.destroy();
            });
        }

        /** Upward particles on a target (buff/heal). */
        playBuffVfx(target, color) {
            if (!target || !this.stage) return;
            const cx = target.x;
            const cy = target.y - (target.height || 40) / 2;

            for (let i = 0; i < 8; i++) {
                const p = new PIXI.Graphics();
                p.circle(0, 0, 3);
                p.fill({ color, alpha: 0.8 });
                p.x = cx + (Math.random() - 0.5) * 30;
                p.y = cy + (Math.random() - 0.5) * 20;
                this.stage.addChild(p);

                this.animateTo(p, { y: p.y - 40 - Math.random() * 30, alpha: 0 }, 600 + Math.random() * 200, () => {
                    if (p.parent) p.parent.removeChild(p);
                    p.destroy();
                });
            }
        }

        /** Slash effect at target position. */
        createMeleeStrikeVfx(color, tgtX, tgtY) {
            const slash = new PIXI.Graphics();
            slash.moveTo(-15, -15);
            slash.lineTo(15, 15);
            slash.moveTo(15, -15);
            slash.lineTo(-15, 15);
            slash.stroke({ color, width: 4, alpha: 0.9 });
            slash.x = tgtX;
            slash.y = tgtY;
            this.stage.addChild(slash);

            this.animateTo(slash, { alpha: 0, scale: 2 }, 350, () => {
                if (slash.parent) slash.parent.removeChild(slash);
                slash.destroy();
            });
        }

        /** Expanding concentric sound wave rings from source toward target. */
        createSoundWaveVfx(color, srcX, srcY, tgtX, tgtY) {
            const midX = (srcX + tgtX) / 2;
            const midY = (srcY + tgtY) / 2;
            for (let i = 0; i < 3; i++) {
                const ring = new PIXI.Graphics();
                ring.circle(0, 0, 12);
                ring.stroke({ color, width: 3, alpha: 0.8 });
                ring.x = midX;
                ring.y = midY;
                ring.scale.set(0.3);
                ring.alpha = 0;
                this.stage.addChild(ring);

                setTimeout(() => {
                    ring.alpha = 0.8;
                    this.animateTo(ring, { alpha: 0, scale: 3 + i }, 500 / this.battleSpeed, () => {
                        if (ring.parent) ring.parent.removeChild(ring);
                        ring.destroy();
                    });
                }, i * 120 / this.battleSpeed);
            }
        }

        /** Floating music note particles drifting from source to target. */
        createMusicNotesVfx(color, srcX, srcY, tgtX, tgtY) {
            const notes = ['♪', '♫', '♩', '♬'];
            for (let i = 0; i < 5; i++) {
                const note = new PIXI.Text({
                    text: notes[i % notes.length],
                    style: { fontSize: 18 + Math.random() * 8, fill: color, fontFamily: 'serif' }
                });
                note.anchor.set(0.5);
                note.x = srcX + (Math.random() - 0.5) * 30;
                note.y = srcY + (Math.random() - 0.5) * 20;
                note.alpha = 0;
                this.stage.addChild(note);

                const delay = i * 80 / this.battleSpeed;
                const endX = tgtX + (Math.random() - 0.5) * 40;
                const endY = tgtY - 20 + (Math.random() - 0.5) * 30;
                setTimeout(() => {
                    note.alpha = 1;
                    const duration = 450 / this.battleSpeed;
                    const startTime = Date.now();
                    const startX = note.x;
                    const startY = note.y;
                    const animate = () => {
                        const t = Math.min((Date.now() - startTime) / duration, 1);
                        note.x = startX + (endX - startX) * t;
                        note.y = startY + (endY - startY) * t - Math.sin(t * Math.PI) * 20;
                        note.alpha = 1 - t * 0.6;
                        note.rotation = Math.sin(t * Math.PI * 2) * 0.3;
                        if (t < 1) {
                            requestAnimationFrame(animate);
                        } else {
                            if (note.parent) note.parent.removeChild(note);
                            note.destroy();
                        }
                    };
                    animate();
                }, delay);
            }
        }

        /** Brief screen shake effect. */
        screenShake() {
            if (!this.stage) return;
            const intensity = 4;
            const originalX = this.stage.x;
            const originalY = this.stage.y;
            let count = 0;
            const shake = () => {
                if (count >= 6 || !this.stage) {
                    if (this.stage) { this.stage.x = originalX; this.stage.y = originalY; }
                    return;
                }
                this.stage.x = originalX + (Math.random() - 0.5) * intensity * 2;
                this.stage.y = originalY + (Math.random() - 0.5) * intensity * 2;
                count++;
                setTimeout(shake, 30);
            };
            shake();
        }

        /** Update spell button cooldown visuals. */
        updateSpellCooldownVisuals() {
            for (const btn of this.spellButtons) {
                const cd = this.spellCooldowns[btn.attackId] ?? 0;
                if (cd > 0) {
                    btn.cdOverlay.visible = true;
                    btn.cdText.visible = true;
                    btn.cdText.text = Math.ceil(cd).toString();
                    btn.container.cursor = 'not-allowed';
                    btn.bg.alpha = 0.5;
                } else {
                    btn.cdOverlay.visible = false;
                    btn.cdText.visible = false;
                    btn.container.cursor = 'pointer';
                    btn.bg.alpha = 0.9;
                }
            }
        }

        // ── END INTERACTIVE MODE METHODS ────────────────────────────────────

        update() {
            // Guard: if app or stage was destroyed (GL context loss, dispose), stop
            if (!this.app || !this.stage) return;
            const deltaMs = this.app.ticker.deltaMS;
            
            // Idle animation for enemies - always runs even during pauses
            this.updateIdleAnimation(deltaMs);

            // ── Interactive mode: speed bars trigger server calls ──
            if (this.interactiveMode && !this.battleFinished && this.isPlaying) {
                const simDelta = deltaMs * this.battleSpeed;
                this.currentSimTime += simDelta;

                // Player speed bar
                if (this.playerCurrentHp > 0) {
                    this.playerSpeedBarTimer = Math.max(0, this.playerSpeedBarTimer - simDelta);
                    this.updatePlayerSpeedBar();

                    if (this.playerSpeedBarTimer <= 0 && !this._playerAttackPending) {
                        this._playerAttackPending = true;
                        this.playerSpeedBarTimer = this.playerActionTime * 1000;
                        this.requestPlayerAutoAttack();
                    }
                }

                // Enemy speed bars
                for (let i = 0; i < this.enemyCount; i++) {
                    if (this.enemyHPs[i] && this.enemyHPs[i].current > 0) {
                        this.enemySpeedBarTimers[i] = Math.max(0, this.enemySpeedBarTimers[i] - simDelta);
                        this.updateEnemySpeedBar(i);

                        if (this.enemySpeedBarTimers[i] <= 0 && !this._enemyAttackPending[i]) {
                            this._enemyAttackPending[i] = true;
                            this.enemySpeedBarTimers[i] = this.enemyActionTimes[i] * 1000;
                            this.requestEnemyAttack(i);
                        }
                    }
                }

                // Tick spell cooldowns on server ~200ms of sim time (scales with battle speed)
                this._cooldownTickAccum += simDelta;
                if (this._cooldownTickAccum >= 200) {
                    const spellElapsed = this._cooldownTickAccum / 1000;
                    this._cooldownTickAccum = 0;
                    for (const id of Object.keys(this.spellCooldowns)) {
                        this.spellCooldowns[id] = Math.max(0, this.spellCooldowns[id] - spellElapsed);
                    }
                    this.updateSpellCooldownVisuals();
                    this.requestTickCooldowns(spellElapsed);
                }

                // Tick consumable cooldowns on server ~200ms of REAL time (not affected by speed)
                this._consumableTickAccum += deltaMs;
                if (this._consumableTickAccum >= 200) {
                    const realElapsed = this._consumableTickAccum / 1000;
                    this._consumableTickAccum = 0;
                    for (const type of Object.keys(this.consumableCooldowns)) {
                        this.consumableCooldowns[type] = Math.max(0, this.consumableCooldowns[type] - realElapsed);
                    }
                    this.updateConsumableCooldownVisuals();
                    this.requestTickConsumableCooldowns(realElapsed);
                }

                return; // Don't process pre-computed events
            }
            
            // ── Pre-computed mode: process events by SimTime ──
            if (!this.battleFinished && this.battleEvents && this.isPlaying) {
                // Advance simulation time based on battle speed
                const simDelta = deltaMs * this.battleSpeed;
                this.currentSimTime += simDelta;
                
                // Update player speed bar timer (drain towards 0)
                if (this.playerCurrentHp > 0) {
                    this.playerSpeedBarTimer = Math.max(0, this.playerSpeedBarTimer - simDelta);
                    this.updatePlayerSpeedBar();
                }
                
                // Update enemy speed bar timers
                for (let i = 0; i < this.enemyCount; i++) {
                    if (this.enemyHPs[i] && this.enemyHPs[i].current > 0) {
                        this.enemySpeedBarTimers[i] = Math.max(0, this.enemySpeedBarTimers[i] - simDelta);
                        this.updateEnemySpeedBar(i);
                    }
                }
                
                // Process events that should occur at current simulation time
                while (this.currentEventIndex < this.battleEvents.length) {
                    const eventData = this.battleEvents[this.currentEventIndex];
                    if (eventData.simTime > this.currentSimTime) break;
                    
                    const evt = eventData.event;
                    const evtType = getEventField(evt, 'Type');
                    
                    // When an attack happens, reset the attacker's speed bar
                    if (evtType === 'Attack') {
                        const attacker = getEventField(evt, 'Attacker');
                        if (attacker === 'Attacker' || attacker === 'Player') {
                            this.playerSpeedBarTimer = this.playerActionTime * 1000;
                        } else if (attacker === 'Defender') {
                            // Single-enemy battle - reset enemy 0's speed bar
                            this.enemySpeedBarTimers[0] = this.enemyActionTimes[0] * 1000;
                        } else if (attacker.startsWith('Enemy')) {
                            const enemyIndex = parseInt(attacker.replace('Enemy', ''));
                            if (!isNaN(enemyIndex) && enemyIndex >= 0 && enemyIndex < this.enemyCount) {
                                this.enemySpeedBarTimers[enemyIndex] = this.enemyActionTimes[enemyIndex] * 1000;
                            }
                        }
                    }
                    
                    this.processEvent(evt);
                    this.currentEventIndex++;
                    
                    // Check for battle end - stop processing events, let finishBattle be called after delay
                    if (evtType === 'Victory' || evtType === 'Draw') {
                        this.isPlaying = false; // Stop processing more events
                        break;
                    }
                }

                // Safety net: all events consumed but no Victory/Draw was found — force finish
                if (this.isPlaying && this.currentEventIndex >= this.battleEvents.length) {
                    console.warn('All battle events consumed without Victory/Draw — forcing finishBattle');
                    this.isPlaying = false;
                    setTimeout(() => this.finishBattle(), 400 / this.battleSpeed);
                }
            }
        }

        updateIdleAnimation(deltaMs) {
            this.idleAnimationTime += deltaMs * 0.002; // Slow animation speed

            // Animate player with gentle bobbing (skip during attack lunge)
            if (this.playerSprite && !this.playerSprite.destroyed && this.playerIdleOffset && this.playerCurrentHp > 0 && !this._playerAttacking) {
                const po = this.playerIdleOffset;
                const bobY = Math.sin(this.idleAnimationTime * 1.5 + po.phase) * po.bobAmplitude;
                const swayX = Math.sin(this.idleAnimationTime * 0.8 + po.phase * 1.3) * po.swayAmplitude;
                this.playerSprite.y = po.baseY + bobY;
                this.playerSprite.x = po.baseX + swayX;
                
                // Animate blue aura to follow player and pulse
                if (this.playerAura && !this.playerAura.destroyed) {
                    this.playerAura.x = po.baseX + swayX;
                    this.playerAura.y = po.baseY + bobY - this.playerDisplayHeight / 2;
                    this.playerAura.alpha = 0.25 + Math.sin(this.idleAnimationTime * 1.2) * 0.12;
                }
            }

            // Animate enemies with a gentle floating/bobbing motion
            if (!this.enemySprites || this.enemySprites.length === 0) return;
            
            for (let i = 0; i < this.enemySprites.length; i++) {
                const enemy = this.enemySprites[i];
                const offset = this.enemyIdleOffsets[i];
                
                if (!enemy || !offset || this.enemyHPs[i]?.current <= 0) continue;
                
                // Skip idle repositioning while this enemy is lunging
                if (this._enemyAttacking[i]) continue;
                
                const phase = offset.phase;
                const bobAmplitude = offset.bobAmplitude || 3;
                const swayAmplitude = offset.swayAmplitude || 2;
                
                // Aerial enemies bob faster and more dramatically
                const bobSpeed = offset.isAerial ? 2.0 : 1.5;
                const swaySpeed = offset.isAerial ? 1.2 : 0.8;
                
                // Y bob (up and down)
                const bobY = Math.sin(this.idleAnimationTime * bobSpeed + phase) * bobAmplitude;
                
                // X sway
                const swayX = Math.sin(this.idleAnimationTime * swaySpeed + phase * 1.3) * swayAmplitude;
                
                enemy.y = offset.baseY + bobY;
                enemy.x = offset.baseX + swayX;
            }
        }

        updatePlayerSpeedBar() {
            if (!this.playerSpeedBar) return;
            const totalMs = this.playerActionTime * 1000;
            const ratio = Math.max(0, this.playerSpeedBarTimer / totalMs);
            const newWidth = this.playerSpeedBar.maxWidth * ratio;
            this.playerSpeedBar.bar.width = newWidth;
            // Show countdown: remaining seconds (e.g. "2.1s")
            if (this.playerSpeedBar.text) {
                const remainingSec = Math.max(0, this.playerSpeedBarTimer / 1000);
                this.playerSpeedBar.text.text = `${remainingSec.toFixed(1)}s`;
            }
        }

        updateEnemySpeedBar(enemyIndex) {
            if (!this.enemySpeedBars[enemyIndex]) return;
            const actionTimeMs = this.enemyActionTimes[enemyIndex] * 1000;
            const ratio = Math.max(0, this.enemySpeedBarTimers[enemyIndex] / actionTimeMs);
            const newWidth = this.enemySpeedBars[enemyIndex].maxWidth * ratio;
            this.enemySpeedBars[enemyIndex].bar.width = newWidth;

            // Sync boss HUD speed bar
            if (this.bossSpeedBar && enemyIndex === 0) {
                this.bossSpeedBar.bar.width = this.bossSpeedBar.maxWidth * ratio;
                if (this.bossSpeedBar.text) {
                    const remainingSec = Math.max(0, this.enemySpeedBarTimers[enemyIndex] / 1000);
                    this.bossSpeedBar.text.text = `${remainingSec.toFixed(1)}s`;
                }
            }
        }

        processEvent(evt) {
            const evtType = getEventField(evt, 'Type');

            switch (evtType) {
                case 'HPUpdate':
                    this.handleHPUpdate(evt);
                    break;
                case 'Attack':
                    this.handleAttack(evt);
                    break;
                case 'KO':
                    this.handleKO(evt);
                    break;
                case 'Victory':
                    this.handleVictory(evt);
                    break;
                case 'Draw':
                    this.handleDraw();
                    break;
                case 'BattleStart':
                    // Battle start event - nothing to do
                    break;
            }
        }

        processNextEvent() {
            if (this.battleFinished || !this.isPlaying) return;
            if (this.currentEventIndex >= this.eventsList.length) {
                this.finishBattle();
                return;
            }

            const evt = this.eventsList[this.currentEventIndex];
            this.currentEventIndex++;

            const evtType = getEventField(evt, 'Type');

            switch (evtType) {
                case 'HPUpdate':
                    this.handleHPUpdate(evt);
                    break;
                case 'Attack':
                    this.handleAttack(evt);
                    break;
                case 'KO':
                    this.handleKO(evt);
                    break;
                case 'Victory':
                    this.handleVictory(evt);
                    break;
                case 'Draw':
                    this.handleDraw();
                    break;
            }
        }

        handleHPUpdate(evt) {
            const character = getEventField(evt, 'Character');
            const hp = getEventField(evt, 'HP');
            const maxHP = getEventField(evt, 'MaxHP');

            if (character === 'Attacker' || character === 'Player') {
                if (maxHP && maxHP > this.playerMaxHp) {
                    this.playerMaxHp = maxHP;
                }
                this.playerCurrentHp = hp;
                this.updatePlayerHPBar();
            } else if (character.startsWith('Enemy')) {
                const enemyIndex = parseInt(character.replace('Enemy', ''));
                if (!isNaN(enemyIndex) && enemyIndex >= 0 && enemyIndex < this.enemyHpBars.length) {
                    if (maxHP && maxHP > this.enemyHPs[enemyIndex].max) {
                        this.enemyHPs[enemyIndex].max = maxHP;
                    }
                    this.enemyHPs[enemyIndex].current = hp;
                    this.updateIndividualEnemyHPBar(enemyIndex);
                }
            } else {
                // Handle "Defender" (single enemy battles) - use maxHP from event if available
                if (maxHP) {
                    this.enemyMaxHp = maxHP;
                    // Also update the enemyHPs array for consistency
                    if (this.enemyHPs[0]) {
                        this.enemyHPs[0].max = maxHP;
                    }
                }
                this.enemyCurrentHp = hp;
                if (this.enemyHPs[0]) {
                    this.enemyHPs[0].current = hp;
                }
                this.updateEnemyHPBar();
            }
        }

        updatePlayerHPBar() {
            // Clamp ratio to [0,1] to prevent bar overflow when CurrentHP > MaxHP
            const ratio = Math.min(1, Math.max(0, this.playerCurrentHp / this.playerMaxHp));
            const maxWidth = this.playerHpBar.maxWidth || 200;
            const barHeight = this.playerHpBar.barHeight || 22;
            const radius = barHeight / 2;

            // Update bar color based on HP percentage
            const fillColor = ratio > 0.5 ? 0x4caf50 : ratio > 0.25 ? 0xff9800 : 0xf44336;
            this.playerHpBar.bar.clear();
            this.playerHpBar.bar.roundRect(0, 0, maxWidth, barHeight, radius);
            this.playerHpBar.bar.fill(fillColor);

            this.animateTo(this.playerHpBar.bar, { width: maxWidth * ratio }, 200);
            this.playerHpBar.text.text = `${formatNum(Math.max(0, this.playerCurrentHp))} / ${formatNum(this.playerMaxHp)} HP`;
        }

        updateIndividualEnemyHPBar(enemyIndex) {
            if (!this.enemyHPs || !this.enemyHPs[enemyIndex]) return;
            
            const enemyHP = this.enemyHPs[enemyIndex];
            const ratio = Math.max(0, enemyHP.current / enemyHP.max);
            const hpBarData = this.enemyHpBars[enemyIndex];
            
            if (!hpBarData) return;
            
            const newWidth = hpBarData.maxWidth * ratio;
            this.animateTo(hpBarData.bar, { width: Math.max(0, newWidth) }, 200);
            hpBarData.text.text = `${formatNum(Math.max(0, Math.round(enemyHP.current)))}/${formatNum(Math.round(enemyHP.max))}`;

            // Sync boss HUD bar (1v1 boss)
            if (this.bossHpBar && enemyIndex === 0) {
                const maxW = this.bossHpBar.maxWidth;
                const bh = this.bossHpBar.barHeight;
                const r = bh / 2;
                const fillColor = ratio > 0.5 ? 0xf44336 : ratio > 0.25 ? 0xd32f2f : 0xb71c1c;
                this.bossHpBar.bar.clear();
                this.bossHpBar.bar.roundRect(0, 0, maxW, bh, r);
                this.bossHpBar.bar.fill(fillColor);
                this.animateTo(this.bossHpBar.bar, { width: maxW * ratio }, 200);
                this.bossHpBar.text.text = `${formatNum(Math.max(0, Math.round(enemyHP.current)))} / ${formatNum(Math.round(enemyHP.max))} HP`;
            }
        }

        updateEnemyHPBar() {
            const ratio = Math.max(0, this.enemyCurrentHp / this.enemyMaxHp);
            
            this.enemyHpBars.forEach(hpBarData => {
                const newWidth = hpBarData.maxWidth * ratio;
                this.animateTo(hpBarData.bar, { width: Math.max(0, newWidth) }, 200);
                hpBarData.text.text = `${formatNum(Math.max(0, Math.round(this.enemyCurrentHp / this.enemyCount)))}/${formatNum(Math.round(this.enemyMaxHp / this.enemyCount))}`;
            });

            // Sync boss HUD bar
            if (this.bossHpBar) {
                const maxW = this.bossHpBar.maxWidth;
                const bh = this.bossHpBar.barHeight;
                const r = bh / 2;
                const fillColor = ratio > 0.5 ? 0xf44336 : ratio > 0.25 ? 0xd32f2f : 0xb71c1c;
                this.bossHpBar.bar.clear();
                this.bossHpBar.bar.roundRect(0, 0, maxW, bh, r);
                this.bossHpBar.bar.fill(fillColor);
                this.animateTo(this.bossHpBar.bar, { width: maxW * ratio }, 200);
                this.bossHpBar.text.text = `${formatNum(Math.max(0, Math.round(this.enemyCurrentHp)))} / ${formatNum(Math.round(this.enemyMaxHp))} HP`;
            }
        }

        handleAttack(evt) {
            const attacker = getEventField(evt, 'Attacker');
            const defender = getEventField(evt, 'Defender');
            const damage = getEventField(evt, 'Damage') ?? 0;
            const isCritical = getEventField(evt, 'IsCritical') ?? false;
            const isBlocked = getEventField(evt, 'IsBlocked') ?? false;
            const isDodged = getEventField(evt, 'IsDodged') ?? false;
            const isBoosted = getEventField(evt, 'IsBoosted') ?? false;

            if (attacker === 'Attacker' || attacker === 'Player') {
                this.animatePlayerAttack(isCritical);
                if (defender && defender.startsWith('Enemy')) {
                    const enemyIndex = parseInt(defender.replace('Enemy', ''));
                    this.flashEnemy(enemyIndex, isCritical);
                    // Show damage text on the hit enemy
                    const enemy = this.enemySprites[enemyIndex];
                    if (enemy && damage > 0) {
                        if (isBlocked) {
                            this.showFloatingText('BLOCKED', enemy.x, enemy.y - (enemy.height || 40) * 0.8, 0x00e5ff);
                        } else {
                            this.showDamageText(damage, isCritical, enemy.x, enemy.y - (enemy.height || 40) * 0.8);
                        }
                    }
                    if (isBoosted) {
                        if (enemy) this.showFloatingText('EXTRA', enemy.x, enemy.y - (enemy.height || 40) * 0.8 - 25, 0xff9800);
                    }
                } else {
                    this.flashEnemies(isCritical);
                    const enemy = this.enemySprites[0];
                    if (enemy && damage > 0) {
                        if (isBlocked) {
                            this.showFloatingText('BLOCKED', enemy.x, enemy.y - (enemy.height || 40) * 0.8, 0x00e5ff);
                        } else {
                            this.showDamageText(damage, isCritical, enemy.x, enemy.y - (enemy.height || 40) * 0.8);
                        }
                    }
                    if (isBoosted && this.enemySprites.length > 0) {
                        if (enemy) this.showFloatingText('EXTRA', enemy.x, enemy.y - (enemy.height || 40) * 0.8 - 25, 0xff9800);
                    }
                }
            } else if (attacker.startsWith('Enemy')) {
                const enemyIndex = parseInt(attacker.replace('Enemy', ''));
                this.animateSingleEnemyAttack(enemyIndex, isCritical);
                if (isBlocked || isDodged) {
                    this.showFloatingText(isDodged ? 'DODGE' : 'BLOCKED', this.playerSprite.x, this.playerSprite.y - (this.playerSprite.height || 40) * 0.8, 0x00e5ff);
                } else {
                    this.flashPlayer(isCritical);
                    if (damage > 0) {
                        this.showDamageText(damage, isCritical, this.playerSprite.x, this.playerSprite.y - (this.playerSprite.height || 40) * 0.8);
                    }
                }
            } else {
                this.animateEnemyAttack(isCritical);
                if (isBlocked || isDodged) {
                    this.showFloatingText(isDodged ? 'DODGE' : 'BLOCKED', this.playerSprite.x, this.playerSprite.y - (this.playerSprite.height || 40) * 0.8, 0x00e5ff);
                } else {
                    this.flashPlayer(isCritical);
                    if (damage > 0) {
                        this.showDamageText(damage, isCritical, this.playerSprite.x, this.playerSprite.y - (this.playerSprite.height || 40) * 0.8);
                    }
                }
            }

            this.playSound((isBlocked || isDodged) ? 'block' : (isCritical ? 'critical' : 'attack'));
        }

        showDamageText(damage, isCritical, x, y) {
            if (!this.stage || !this.app) return;
            const damageValue = Math.abs(damage);
            const text = isCritical ? `CRIT! -${formatNum(damageValue)}` : `-${formatNum(damageValue)}`;
            const fontSize = isCritical ? 28 : 22;
            const color = isCritical ? 0xffff00 : 0xff4444;
            const strokeWidth = isCritical ? 5 : 4;
            const floatDistance = isCritical ? 80 : 60;
            const duration = isCritical ? 1000 : 800;

            const damageText = this._getPooledText(text, {
                fontFamily: 'Arial',
                fontSize: fontSize,
                fontWeight: 'bold',
                fill: color,
                stroke: { color: 0x000000, width: strokeWidth }
            });
            if (!damageText) return;
            damageText.anchor.set(0.5);
            damageText.x = x;
            damageText.y = y;
            this.stage.addChild(damageText);

            this.animateTo(damageText, {
                y: damageText.y - floatDistance,
                alpha: 0
            }, duration, () => {
                this._releaseText(damageText);
            });
        }

        showFloatingText(text, x, y, color) {
            if (!this.stage || !this.app) return;
            const floatText = this._getPooledText(text, {
                fontFamily: 'Arial',
                fontSize: 26,
                fontWeight: 'bold',
                fill: color,
                stroke: { color: 0x000000, width: 4 }
            });
            if (!floatText) return;
            floatText.anchor.set(0.5);
            floatText.x = x;
            floatText.y = y;
            this.stage.addChild(floatText);

            this.animateTo(floatText, {
                y: floatText.y - 70,
                alpha: 0
            }, 900, () => {
                this._releaseText(floatText);
            });
        }

        animatePlayerAttack(isCritical) {
            if (!this.playerSprite) return;
            
            this._playerAttacking = true;
            const lungeDistance = isCritical ? 80 : 60;
            const lungeDuration = isCritical ? 120 : 150;

            if (this.isMobile) {
                // Mobile: lunge upward toward enemies
                const originalY = this.playerSprite.y;
                this.animateTo(this.playerSprite, { y: originalY - lungeDistance }, lungeDuration, () => {
                    this.animateTo(this.playerSprite, { y: originalY }, 240, () => {
                        this._playerAttacking = false;
                    });
                });
            } else {
                // Desktop: lunge rightward toward enemies
                const originalX = this.playerSprite.x;
                this.animateTo(this.playerSprite, { x: originalX + lungeDistance }, lungeDuration, () => {
                    this.animateTo(this.playerSprite, { x: originalX }, 240, () => {
                        this._playerAttacking = false;
                    });
                });
            }
        }

        animateEnemyAttack(isCritical) {
            this.enemySprites.forEach((enemy, index) => {
                this._enemyAttacking[index] = true;
                const lungeDistance = isCritical ? 80 : 60;
                const lungeDuration = isCritical ? 120 : 150;
                setTimeout(() => {
                    if (this.isMobile) {
                        // Mobile: lunge downward toward player
                        const originalY = enemy.y;
                        this.animateTo(enemy, { y: originalY + lungeDistance }, lungeDuration, () => {
                            this.animateTo(enemy, { y: originalY }, 240, () => {
                                this._enemyAttacking[index] = false;
                            });
                        });
                    } else {
                        // Desktop: lunge leftward toward player
                        const originalX = enemy.x;
                        this.animateTo(enemy, { x: originalX - lungeDistance }, lungeDuration, () => {
                            this.animateTo(enemy, { x: originalX }, 240, () => {
                                this._enemyAttacking[index] = false;
                            });
                        });
                    }
                }, (index * 50) / this.battleSpeed);
            });
        }

        animateSingleEnemyAttack(enemyIndex, isCritical) {
            if (enemyIndex >= 0 && enemyIndex < this.enemySprites.length) {
                const enemy = this.enemySprites[enemyIndex];
                if (enemy) {
                    this._enemyAttacking[enemyIndex] = true;
                    const lungeDistance = isCritical ? 80 : 60;
                    const lungeDuration = isCritical ? 120 : 150;
                    if (this.isMobile) {
                        const originalY = enemy.y;
                        this.animateTo(enemy, { y: originalY + lungeDistance }, lungeDuration, () => {
                            this.animateTo(enemy, { y: originalY }, 240, () => {
                                this._enemyAttacking[enemyIndex] = false;
                            });
                        });
                    } else {
                        const originalX = enemy.x;
                        this.animateTo(enemy, { x: originalX - lungeDistance }, lungeDuration, () => {
                            this.animateTo(enemy, { x: originalX }, 240, () => {
                                this._enemyAttacking[enemyIndex] = false;
                            });
                        });
                    }
                }
            }
        }

        flashPlayer(isCritical) {
            if (!this.playerSprite) return;
            this.playerSprite.tint = isCritical ? 0xcc0000 : 0xff0000;
            const flashDuration = isCritical ? 180 : 100;
            setTimeout(() => {
                this.playerSprite.tint = 0xffffff;
            }, flashDuration / this.battleSpeed);
        }

        flashEnemy(enemyIndex, isCritical) {
            if (enemyIndex >= 0 && enemyIndex < this.enemySprites.length) {
                const enemy = this.enemySprites[enemyIndex];
                if (enemy) {
                    enemy.tint = isCritical ? 0xcc0000 : 0xff0000;
                    const flashDuration = isCritical ? 180 : 100;
                    setTimeout(() => {
                        enemy.tint = 0xffffff;
                    }, flashDuration / this.battleSpeed);
                }
            }
        }

        flashEnemies(isCritical) {
            this.enemySprites.forEach(enemy => {
                enemy.tint = isCritical ? 0xcc0000 : 0xff0000;
                const flashDuration = isCritical ? 180 : 100;
                setTimeout(() => {
                    enemy.tint = 0xffffff;
                }, flashDuration / this.battleSpeed);
            });
        }

        handleKO(evt) {
            const character = getEventField(evt, 'Character');
            this.playSound('ko');

            if (character === 'Attacker' || character === 'Player') {
                this.animateTo(this.playerSprite, { alpha: 0.3, rotation: Math.PI / 2 }, 500);
            } else if (character.startsWith('Enemy')) {
                const enemyIndex = parseInt(character.replace('Enemy', ''));
                if (!isNaN(enemyIndex) && enemyIndex >= 0 && enemyIndex < this.enemySprites.length) {
                    const enemy = this.enemySprites[enemyIndex];
                    this.animateTo(enemy, { alpha: 0, y: enemy.y - 50 }, 500);
                    
                    // Hide HP bar
                    const hpBarData = this.enemyHpBars[enemyIndex];
                    if (hpBarData) {
                        this.animateTo(hpBarData.bar, { alpha: 0 }, 300);
                        this.animateTo(hpBarData.barBg, { alpha: 0 }, 300);
                        this.animateTo(hpBarData.text, { alpha: 0 }, 300);
                    }
                    
                    // Hide Speed bar
                    const speedBarData = this.enemySpeedBars[enemyIndex];
                    if (speedBarData) {
                        this.animateTo(speedBarData.bar, { alpha: 0 }, 300);
                        this.animateTo(speedBarData.barBg, { alpha: 0 }, 300);
                    }

                    // Fade boss HUD bars on boss KO
                    if (this.bossHpBar && enemyIndex === 0) {
                        this.animateTo(this.bossHpBar.bar, { alpha: 0 }, 300);
                        this.animateTo(this.bossHpBar.barBg, { alpha: 0 }, 300);
                        this.animateTo(this.bossHpBar.border, { alpha: 0 }, 300);
                        this.animateTo(this.bossHpBar.text, { alpha: 0 }, 300);
                    }
                    if (this.bossSpeedBar && enemyIndex === 0) {
                        this.animateTo(this.bossSpeedBar.bar, { alpha: 0 }, 300);
                        this.animateTo(this.bossSpeedBar.barBg, { alpha: 0 }, 300);
                        if (this.bossSpeedBar.text) this.animateTo(this.bossSpeedBar.text, { alpha: 0 }, 300);
                    }
                }
            } else {
                this.enemySprites.forEach((enemy, idx) => {
                    this.animateTo(enemy, { alpha: 0, y: enemy.y - 50 }, 500);
                    
                    // Hide HP bar
                    const hpBarData = this.enemyHpBars[idx];
                    if (hpBarData) {
                        this.animateTo(hpBarData.bar, { alpha: 0 }, 300);
                        this.animateTo(hpBarData.barBg, { alpha: 0 }, 300);
                        this.animateTo(hpBarData.text, { alpha: 0 }, 300);
                    }
                    
                    // Hide Speed bar
                    const speedBarData = this.enemySpeedBars[idx];
                    if (speedBarData) {
                        this.animateTo(speedBarData.bar, { alpha: 0 }, 300);
                        this.animateTo(speedBarData.barBg, { alpha: 0 }, 300);
                    }
                });

                // Fade boss HUD bars on generic enemy KO
                if (this.bossHpBar) {
                    this.animateTo(this.bossHpBar.bar, { alpha: 0 }, 300);
                    this.animateTo(this.bossHpBar.barBg, { alpha: 0 }, 300);
                    this.animateTo(this.bossHpBar.border, { alpha: 0 }, 300);
                    this.animateTo(this.bossHpBar.text, { alpha: 0 }, 300);
                }
                if (this.bossSpeedBar) {
                    this.animateTo(this.bossSpeedBar.bar, { alpha: 0 }, 300);
                    this.animateTo(this.bossSpeedBar.barBg, { alpha: 0 }, 300);
                    if (this.bossSpeedBar.text) this.animateTo(this.bossSpeedBar.text, { alpha: 0 }, 300);
                }
            }
        }

        handleVictory(evt) {
            const winner = getEventField(evt, 'Winner');
            // Check for both 'Attacker' (1v1 battles) and 'Player' (multi-enemy battles)
            const isPlayerWin = winner === 'Attacker' || winner === 'Player';
            
            if (isPlayerWin) {
                this.playSound('victory');
                
                const originalY = this.playerSprite.y;
                this.animateTo(this.playerSprite, { y: originalY - 20 }, 200, () => {
                    this.animateTo(this.playerSprite, { y: originalY }, 200, () => {
                        this.animateTo(this.playerSprite, { y: originalY - 20 }, 200, () => {
                            this.animateTo(this.playerSprite, { y: originalY }, 200);
                        });
                    });
                });
            } else {
                this.playSound('defeat');
            }

            // Only show big VICTORY/DEFEAT text on boss stages (every 10th) or on defeat
            const isBossStage = this.enemyType === 'boss' || (this.stageNumber % 10 === 0);
            if (isBossStage || !isPlayerWin) {
                const resultText = new PIXI.Text({
                    text: isPlayerWin ? 'VICTORY!' : 'DEFEAT',
                    style: {
                        fontSize: 48,
                        fontFamily: 'Arial, sans-serif',
                        fontWeight: 'bold',
                        fill: isPlayerWin ? 0x44ff44 : 0xff4444,
                        stroke: { color: 0x000000, width: 6 }
                    }
                });
                resultText.anchor.set(0.5);
                resultText.x = this.app.screen.width / 2;
                resultText.y = this.app.screen.height / 2;
                resultText.scale.set(0);
                this.stage.addChild(resultText);

                this.animateTo(resultText, { scale: 1 }, 500);
            }

            // Delay finishBattle to allow victory animation to show
            // Shorter delay for non-boss wins since there's no big text to show
            const finishDelay = (isBossStage || !isPlayerWin) ? 800 : 400;
            setTimeout(() => this.finishBattle(), finishDelay / this.battleSpeed);
        }

        handleDraw() {
            const drawText = new PIXI.Text({
                text: 'DRAW',
                style: {
                    fontSize: 48,
                    fontFamily: 'Arial, sans-serif',
                    fontWeight: 'bold',
                    fill: 0xffaa00,
                    stroke: { color: 0x000000, width: 6 }
                }
            });
            drawText.anchor.set(0.5);
            drawText.x = this.app.screen.width / 2;
            drawText.y = this.app.screen.height / 2;
            this.stage.addChild(drawText);

            // Delay finishBattle to allow draw animation to show
            setTimeout(() => this.finishBattle(), 800 / this.battleSpeed);
        }

        finishBattle() {
            if (this.battleFinished) return;
            this.battleFinished = true;
            this.isPlaying = false;

            if (this.eventTimer) {
                clearInterval(this.eventTimer);
                this.eventTimer = null;
            }

            if (this.dotNetRef) {
                try {
                    this.dotNetRef.invokeMethodAsync('OnBattleFinished').catch(e => {
                        console.warn('Could not notify Blazor of battle finish:', e);
                    });
                } catch (e) {
                    console.warn('finishBattle: dotNetRef error:', e.message);
                }
            }
        }

        /** Play a unique musical sound for each special attack.
         *  Uses Web Audio API oscillators to synthesize instrument-specific tones. */
        playSpellSound(attackId) {
            if (!this.audioEnabled || !this.audioContext || !this.sfxVolume) return;
            if (this.audioContext.state === 'suspended') this.audioContext.resume();
            const ctx = this.audioContext;
            const vol = this.sfxVolume;
            const t = ctx.currentTime;

            const playNote = (freq, type, start, dur, v = 0.3) => {
                const osc = ctx.createOscillator();
                const g = ctx.createGain();
                osc.connect(g); g.connect(ctx.destination);
                osc.frequency.value = freq; osc.type = type;
                g.gain.setValueAtTime(vol * v, t + start);
                g.gain.exponentialRampToValueAtTime(0.01, t + start + dur);
                osc.start(t + start); osc.stop(t + start + dur);
            };

            switch (attackId) {
                case 'heavy_attack': {
                    // Big impact — low power hit + mid crunch
                    playNote(120, 'sawtooth', 0, 0.15, 0.5);
                    playNote(180, 'square', 0.03, 0.12, 0.4);
                    break;
                }
                case 'guitarra_barrage': {
                    // Guitar barrage — rapid machine-gun power chords
                    for (let i = 0; i < 6; i++) {
                        playNote(82 + i * 15, 'sawtooth', i * 0.07, 0.08, 0.4);
                        playNote(165 + i * 10, 'square', i * 0.07 + 0.03, 0.06, 0.25);
                    }
                    break;
                }
                case 'bandolim_swiftchord': {
                    // Mandolin — sharp swift single chord strike
                    playNote(587, 'triangle', 0, 0.12, 0.4);
                    playNote(784, 'triangle', 0.02, 0.1, 0.35);
                    playNote(988, 'sine', 0.04, 0.08, 0.3);
                    break;
                }
                case 'cavaquinho_paralysis': {
                    // Cavaquinho — electric paralysing zap (staccato + high buzz)
                    for (let i = 0; i < 5; i++) {
                        playNote(800 + Math.random() * 400, 'square', i * 0.06, 0.05, 0.3);
                    }
                    playNote(200, 'sawtooth', 0.35, 0.2, 0.4);
                    break;
                }
                case 'acordeao_fear': {
                    // Accordion — deep ominous dread (descending dissonant chord)
                    playNote(130, 'sawtooth', 0, 0.6, 0.4);
                    playNote(138, 'sawtooth', 0, 0.55, 0.35);
                    playNote(98, 'square', 0.1, 0.4, 0.3);
                    playNote(65, 'triangle', 0.2, 0.4, 0.25);
                    break;
                }
                case 'contrabaixo_sonicboom': {
                    // Contrabass — big sonic boom (deep impact + expanding wave)
                    const osc = ctx.createOscillator();
                    const g = ctx.createGain();
                    osc.connect(g); g.connect(ctx.destination);
                    osc.type = 'sine';
                    osc.frequency.setValueAtTime(110, t);
                    osc.frequency.exponentialRampToValueAtTime(35, t + 0.5);
                    g.gain.setValueAtTime(vol * 0.6, t);
                    g.gain.exponentialRampToValueAtTime(0.01, t + 0.5);
                    osc.start(t); osc.stop(t + 0.5);
                    playNote(55, 'triangle', 0, 0.4, 0.3);
                    playNote(220, 'square', 0.05, 0.15, 0.2);
                    break;
                }
                case 'percussao_combo': {
                    // Percussion — 1-2-3 combo hits, 3rd hit bigger
                    playNote(100, 'square', 0, 0.08, 0.35);
                    playNote(120, 'square', 0.12, 0.08, 0.4);
                    playNote(80, 'sawtooth', 0.28, 0.15, 0.55);
                    playNote(60, 'triangle', 0.3, 0.2, 0.4);
                    break;
                }
                case 'pandeireta_boomerang': {
                    // Pandeireta boomerang — whoosh out and back
                    for (let i = 0; i < 4; i++) {
                        playNote(600 + i * 200, 'sine', i * 0.08, 0.1, 0.25);
                    }
                    for (let i = 0; i < 4; i++) {
                        playNote(1400 - i * 200, 'sine', 0.4 + i * 0.08, 0.1, 0.25);
                    }
                    break;
                }
                case 'estandarte_rally': {
                    // Flag/banner — military fanfare (rising brass-like notes)
                    const fanfare = [262, 330, 392, 523, 659];
                    fanfare.forEach((f, i) => playNote(f, 'square', i * 0.1, 0.15, 0.35));
                    break;
                }
                case 'violino_sleep': {
                    // Violin — haunting lullaby melody (legato high notes)
                    const lullaby = [659, 587, 523, 494, 440];
                    lullaby.forEach((f, i) => playNote(f, 'sine', i * 0.15, 0.25, 0.35));
                    playNote(330, 'triangle', 0, 0.7, 0.15);
                    break;
                }
                default:
                    // Fallback to generic attack sound
                    this.playSound('attack');
                    break;
            }
        }

        playSound(type) {
            if (!this.audioEnabled || !this.audioContext || !this.sfxVolume) return;
            
            if (this.audioContext.state === 'suspended') {
                this.audioContext.resume();
            }

            const ctx = this.audioContext;
            const oscillator = ctx.createOscillator();
            const gainNode = ctx.createGain();
            
            oscillator.connect(gainNode);
            gainNode.connect(ctx.destination);
            
            switch (type) {
                case 'attack':
                    oscillator.frequency.value = 200;
                    oscillator.type = 'square';
                    gainNode.gain.setValueAtTime(this.sfxVolume * 0.3, ctx.currentTime);
                    gainNode.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.1);
                    oscillator.start(ctx.currentTime);
                    oscillator.stop(ctx.currentTime + 0.1);
                    break;
                case 'critical':
                    oscillator.frequency.value = 400;
                    oscillator.type = 'sine';
                    gainNode.gain.setValueAtTime(this.sfxVolume * 0.5, ctx.currentTime);
                    gainNode.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.2);
                    oscillator.start(ctx.currentTime);
                    oscillator.stop(ctx.currentTime + 0.2);
                    break;
                case 'ko':
                    oscillator.frequency.setValueAtTime(300, ctx.currentTime);
                    oscillator.frequency.exponentialRampToValueAtTime(50, ctx.currentTime + 0.5);
                    oscillator.type = 'triangle';
                    gainNode.gain.setValueAtTime(this.sfxVolume * 0.6, ctx.currentTime);
                    gainNode.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.5);
                    oscillator.start(ctx.currentTime);
                    oscillator.stop(ctx.currentTime + 0.5);
                    break;
                case 'victory':
                    const notes = [262, 330, 392, 523];
                    notes.forEach((freq, i) => {
                        const osc = ctx.createOscillator();
                        const gain = ctx.createGain();
                        osc.connect(gain);
                        gain.connect(ctx.destination);
                        osc.frequency.value = freq;
                        osc.type = 'sine';
                        gain.gain.setValueAtTime(this.sfxVolume * 0.4, ctx.currentTime + i * 0.15);
                        gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + i * 0.15 + 0.3);
                        osc.start(ctx.currentTime + i * 0.15);
                        osc.stop(ctx.currentTime + i * 0.15 + 0.3);
                    });
                    break;
                case 'defeat':
                    oscillator.frequency.setValueAtTime(200, ctx.currentTime);
                    oscillator.frequency.exponentialRampToValueAtTime(80, ctx.currentTime + 0.6);
                    oscillator.type = 'sawtooth';
                    gainNode.gain.setValueAtTime(this.sfxVolume * 0.4, ctx.currentTime);
                    gainNode.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.6);
                    oscillator.start(ctx.currentTime);
                    oscillator.stop(ctx.currentTime + 0.6);
                    break;
                case 'block':
                    oscillator.frequency.value = 150;
                    oscillator.type = 'triangle';
                    gainNode.gain.setValueAtTime(this.sfxVolume * 0.4, ctx.currentTime);
                    gainNode.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.15);
                    oscillator.start(ctx.currentTime);
                    oscillator.stop(ctx.currentTime + 0.15);
                    break;
            }
        }

        setSpeed(speed) {
            // Only allow valid speeds (1, 3, 5) to prevent console exploits
            const allowedSpeeds = [1, 3, 5];
            const validSpeed = allowedSpeeds.includes(speed) ? speed : Math.min(5, Math.max(1, Math.round(speed)));
            this.playbackSpeed = validSpeed;
            // Convert playback speed to battle speed (1x = 1.0, 3x = 3.0, 5x = 5.0)
            this.battleSpeed = validSpeed;
        }

        setAudioEnabled(enabled) {
            this.audioEnabled = enabled;
            globalAudioEnabled = enabled; // Sync with global state
            
            // Control background music
            if (backgroundMusicGainNode) {
                backgroundMusicGainNode.gain.value = enabled ? 0.3 : 0;
            }
        }

        _getPooledText(text, style) {
            let t;
            if (this._textPool.length > 0) {
                t = this._textPool.pop();
                // Pooled text may have been destroyed (GL context loss) — verify
                if (t && !t.destroyed) {
                    try {
                        t.text = text;
                        t.style = style;
                    } catch (e) {
                        // If properties fail (destroyed internally), create fresh
                        t = null;
                    }
                } else {
                    t = null;
                }
            }
            if (!t) {
                try {
                    t = new PIXI.Text({ text, style });
                } catch (e) {
                    console.warn('Failed to create PIXI.Text:', e.message);
                    return null;
                }
            }
            if (!t) return null;
            t.alpha = 1;
            t.scale.set(1);
            t.visible = true;
            return t;
        }

        _releaseText(t) {
            if (t.parent) t.parent.removeChild(t);
            t.visible = false;
            if (this._textPool.length < 20) {
                this._textPool.push(t);
            } else {
                t.destroy();
            }
        }

        animateTo(target, properties, duration, onComplete) {
            // Guard against null/destroyed targets upfront
            if (!target || target.destroyed) {
                if (onComplete) onComplete();
                return;
            }

            // Adjust animation duration based on battle speed
            const adjustedDuration = duration / this.battleSpeed;
            
            const startProps = {};
            try {
                Object.keys(properties).forEach(key => {
                    if (key === 'scale') {
                        startProps[key] = target.scale?.x ?? 1;
                    } else if (key === 'width' || key === 'height') {
                        startProps[key] = target[key] ?? 0;
                    } else {
                        startProps[key] = target[key] ?? (key === 'alpha' ? 1 : 0);
                    }
                });
            } catch (e) {
                // Target properties inaccessible (destroyed internally)
                if (onComplete) onComplete();
                return;
            }

            const startTime = Date.now();
            const animate = () => {
                // Guard against destroyed or null targets
                if (!target || target.destroyed) {
                    if (onComplete) onComplete();
                    return;
                }
                
                const elapsed = Date.now() - startTime;
                const progress = Math.min(elapsed / adjustedDuration, 1);
                
                try {
                    Object.keys(properties).forEach(key => {
                        const start = startProps[key];
                        const end = properties[key];
                        if (key === 'scale') {
                            target.scale.set(start + (end - start) * progress);
                        } else {
                            target[key] = start + (end - start) * progress;
                        }
                    });
                } catch (e) {
                    // Target was destroyed mid-animation
                    if (onComplete) onComplete();
                    return;
                }

                if (progress < 1) {
                    requestAnimationFrame(animate);
                } else if (onComplete) {
                    onComplete();
                }
            };
            animate();
        }

        destroy() {
            // Remove resize listener
            if (this._onResize) {
                window.removeEventListener('resize', this._onResize);
                this._onResize = null;
            }
            // Remove context-loss/restore listeners
            if (this._onContextLost && this.app?.canvas) {
                this.app.canvas.removeEventListener('webglcontextlost', this._onContextLost);
            }
            if (this._onContextRestored && this.app?.canvas) {
                this.app.canvas.removeEventListener('webglcontextrestored', this._onContextRestored);
            }

            // Clear all pending timers
            for (const id of this._timeoutIds) clearTimeout(id);
            for (const id of this._rafIds) cancelAnimationFrame(id);
            this._timeoutIds = [];
            this._rafIds = [];

            // Destroy pooled texts
            for (const t of this._textPool) { try { t.destroy(); } catch (_) {} }

            // Clean up spell bar
            this.spellButtons = [];
            this.spellBarContainer = null;
            this._textPool = [];

            if (this.eventTimer) {
                clearInterval(this.eventTimer);
                this.eventTimer = null;
            }
            
            if (!this.app) return; // Already destroyed
            
            // Stop ticker before destroying
            try { this.app.ticker.stop(); } catch (e) { /* ignore */ }
            
            // Clear stage children manually to avoid null reference issues
            try {
                while (this.stage && this.stage.children && this.stage.children.length > 0) {
                    const child = this.stage.children[0];
                    this.stage.removeChild(child);
                    if (child.destroy) {
                        try {
                            child.destroy({ children: true, texture: false, baseTexture: false });
                        } catch (e) {
                            // Ignore errors during child destruction
                        }
                    }
                }
            } catch (e) {
                // Ignore errors during stage cleanup
            }
            
            // Destroy the app — catch PixiJS internal errors (GpuBufferSystem null ref, etc.)
            try {
                this.app.destroy(false);
            } catch (e) {
                // PixiJS can throw when GPU context is already lost or buffers are null
                console.warn('PixiJS app.destroy error (safe to ignore):', e.message);
            }
            this.app = null;
            this.stage = null;
        }
        
        // Reset scene for next battle — smooth transition, player persists, no flash!
        async resetForNextBattle(data) {
            // Guard: if app or stage was destroyed (GL context loss), bail out
            if (!this.app || !this.stage) {
                console.warn('resetForNextBattle: app/stage destroyed, skipping');
                return;
            }

            // Stop current battle processing
            this.isPlaying = false;
            this.battleFinished = true;
            
            // Update battle data
            this.eventsList = data?.events ?? [];
            this.dotNetRef = data?.dotNetRef ?? this.dotNetRef;
            this.stageNumber = data?.stageNumber ?? this.stageNumber + 1;
            this.enemyType = data?.enemyType ?? 'Normal';
            this.enemyCount = data?.enemyCount ?? 1;
            this.playerName = data?.playerName ?? this.playerName;
            this.enemyName = data?.enemyName ?? this.enemyName;
            this.backgroundPath = data?.backgroundPath ?? this.backgroundPath;
            this.playerSpritePath = data?.playerSpritePath ?? this.playerSpritePath;
            this.enemySpritePaths = data?.enemySprites ?? [];
            this.enemyPlacements = data?.enemyPlacements ?? Array(this.enemyCount).fill(0);
            this.hasShotBuff = data?.HasShotBuff ?? data?.hasShotBuff ?? this.hasShotBuff;
            this.hasPenaltyBuff = data?.HasPenaltyBuff ?? data?.hasPenaltyBuff ?? false;
            
            // Interactive mode fields
            this.interactiveMode = data?.interactiveMode ?? data?.InteractiveMode ?? this.interactiveMode;
            this.spells = data?.spells ?? data?.Spells ?? this.spells;
            this.interactivePlayerHP = data?.playerHP ?? data?.PlayerHP ?? null;
            this.interactivePlayerMaxHP = data?.playerMaxHP ?? data?.PlayerMaxHP ?? null;
            this.interactivePlayerActionTime = data?.playerActionTime ?? data?.PlayerActionTime ?? null;
            this.interactiveEnemies = data?.enemies ?? data?.Enemies ?? [];
            this._enemyAttackPending = Array(this.enemyCount).fill(false);

            // Update consumable quantities from server
            const cData = data?.consumables ?? data?.Consumables;
            if (cData) {
                this.consumableQuantities = {
                    fino: cData.fino ?? cData.Fino ?? this.consumableQuantities.fino,
                    caneca: cData.caneca ?? cData.Caneca ?? this.consumableQuantities.caneca,
                    cigarro: cData.cigarro ?? cData.Cigarro ?? this.consumableQuantities.cigarro,
                    canhao: cData.canhao ?? cData.Canhao ?? this.consumableQuantities.canhao,
                    shot: cData.shot ?? cData.Shot ?? this.consumableQuantities.shot,
                    penalty: cData.penalty ?? cData.Penalty ?? this.consumableQuantities.penalty
                };
            }

            // Update consumable images from server (fallback to local SVGs)
            const defaultConsumableImages = {
                fino: '/images/consumables/fino.svg',
                caneca: '/images/consumables/caneca.svg',
                cigarro: '/images/consumables/cigarro.svg',
                canhao: '/images/consumables/canhao.svg'
            };
            const ciData = data?.consumableImages ?? data?.ConsumableImages;
            if (ciData) {
                this.consumableImages = {
                    fino: ciData.fino ?? ciData.Fino ?? this.consumableImages.fino ?? defaultConsumableImages.fino,
                    caneca: ciData.caneca ?? ciData.Caneca ?? this.consumableImages.caneca ?? defaultConsumableImages.caneca,
                    cigarro: ciData.cigarro ?? ciData.Cigarro ?? this.consumableImages.cigarro ?? defaultConsumableImages.cigarro,
                    canhao: ciData.canhao ?? ciData.Canhao ?? this.consumableImages.canhao ?? defaultConsumableImages.canhao
                };
            }

            // Update active buff state from server
            const abData = data?.activeBuffs ?? data?.ActiveBuffs;
            if (abData) {
                this.activeBuffs = {
                    cigarro: !!(abData.cigarro ?? abData.Cigarro),
                    canhao: !!(abData.canhao ?? abData.Canhao),
                    shot: !!(abData.shot ?? abData.Shot),
                    penalty: !!(abData.penalty ?? abData.Penalty)
                };
            }

            // Update consumable cooldowns from server (persist across stages)
            const ccData = data?.consumableCooldowns ?? data?.ConsumableCooldowns;
            if (ccData) {
                for (const [type, remaining] of Object.entries(ccData)) {
                    this.consumableCooldowns[type] = remaining;
                }
            }

            // Update spell cooldowns from server (persist across stages)
            const scData = data?.spellCooldowns ?? data?.SpellCooldowns;
            if (scData) {
                for (const [id, remaining] of Object.entries(scData)) {
                    this.spellCooldowns[id] = remaining;
                }
            }
            
            // PRE-LOAD new textures while old scene is still fully visible (no flash)
            try {
                await this.loadAssets();
            } catch (e) {
                console.error('Failed to load assets for next stage, attempting to continue:', e);
                // Don't hang — proceed with whatever textures are available
            }
            
            // Re-check after async — scene may have been destroyed while loading
            if (!this.app || !this.stage) {
                console.warn('resetForNextBattle: app/stage destroyed during asset load');
                return;
            }
            
            // Reset battle state
            this.currentEventIndex = 0;
            this.battleFinished = false;
            this.playerMaxHp = 100;
            this.playerCurrentHp = 100;
            this.playerActionTime = 3.5;
            this.enemyHPs = Array(this.enemyCount).fill(null).map(() => ({ current: 100, max: 100 }));
            this.idleAnimationTime = 0;
            this.enemyIdleOffsets = [];
            
            // Build set of persistent display objects (player + scene base)
            const persistent = new Set();
            persistent.add(this.backgroundSprite);
            if (this._overlay) persistent.add(this._overlay);
            if (this._ground) persistent.add(this._ground);
            if (this.playerSprite) persistent.add(this.playerSprite);
            if (this.playerAura) persistent.add(this.playerAura);
            if (this.playerHpBar) {
                persistent.add(this.playerHpBar.bar);
                persistent.add(this.playerHpBar.barBg);
                persistent.add(this.playerHpBar.text);
                if (this.playerHpBar.border) persistent.add(this.playerHpBar.border);
            }
            if (this.playerSpeedBar) {
                persistent.add(this.playerSpeedBar.bar);
                persistent.add(this.playerSpeedBar.barBg);
                if (this.playerSpeedBar.border) persistent.add(this.playerSpeedBar.border);
                if (this.playerSpeedBar.text) persistent.add(this.playerSpeedBar.text);
            }
            // Boss HUD bars (persisted so they're not destroyed between boss fights)
            if (this.bossHpBar) {
                persistent.add(this.bossHpBar.bar);
                persistent.add(this.bossHpBar.barBg);
                if (this.bossHpBar.border) persistent.add(this.bossHpBar.border);
                persistent.add(this.bossHpBar.text);
            }
            if (this.bossSpeedBar) {
                persistent.add(this.bossSpeedBar.bar);
                persistent.add(this.bossSpeedBar.barBg);
                if (this.bossSpeedBar.text) persistent.add(this.bossSpeedBar.text);
            }
            // Keep consumable and spell bars — recreating them causes CDN image flicker
            if (this.consumableBarContainer) persistent.add(this.consumableBarContainer);
            if (this.spellBarContainer) persistent.add(this.spellBarContainer);
            
            // Remove ONLY non-persistent children (enemies, log, result text, floating text)
            // Guard: stage may have been destroyed by GL context loss during async loadAssets
            if (!this.stage) return;
            const toRemove = [];
            try {
                for (const child of [...this.stage.children]) {
                    if (!persistent.has(child)) {
                        toRemove.push(child);
                    }
                }
            } catch (e) {
                console.warn('resetForNextBattle: error iterating stage children:', e.message);
                return;
            }
            for (const child of toRemove) {
                try {
                    this.stage.removeChild(child);
                    if (child.destroy) {
                        child.destroy({ children: true, texture: false, baseTexture: false });
                    }
                } catch (e) { /* ignore destroyed child */ }
            }
            
            // Reset player visual state (undo KO rotation/fade, attack tint)
            if (this.playerSprite) {
                this.playerSprite.alpha = 1;
                this.playerSprite.rotation = 0;
                this.playerSprite.tint = 0xffffff;
                // Snap back to idle base position in case animation was mid-flight
                if (this.playerIdleOffset) {
                    this.playerSprite.x = this.playerIdleOffset.baseX;
                    this.playerSprite.y = this.playerIdleOffset.baseY;
                }
            }
            
            // Reset player HP bar
            if (this.playerHpBar) {
                this.playerHpBar.bar.width = this.playerHpBar.maxWidth;
                this.playerHpBar.text.text = '100/100';
            }
            
            // Reset player speed bar
            if (this.playerSpeedBar) {
                this.playerSpeedBar.bar.width = this.playerSpeedBar.maxWidth;
            }

            // Reset boss HUD bars
            if (this.bossHpBar) {
                const bh = this.bossHpBar.barHeight;
                const r = bh / 2;
                this.bossHpBar.bar.clear();
                this.bossHpBar.bar.roundRect(0, 0, this.bossHpBar.maxWidth, bh, r);
                this.bossHpBar.bar.fill(0xf44336);
                this.bossHpBar.bar.width = this.bossHpBar.maxWidth;
                this.bossHpBar.bar.alpha = 1;
                this.bossHpBar.barBg.alpha = 1;
                this.bossHpBar.border.alpha = 1;
                this.bossHpBar.text.alpha = 1;
                this.bossHpBar.text.text = '100/100';
            }
            if (this.bossSpeedBar) {
                this.bossSpeedBar.bar.width = this.bossSpeedBar.maxWidth;
                this.bossSpeedBar.bar.alpha = 1;
                this.bossSpeedBar.barBg.alpha = 1;
                if (this.bossSpeedBar.text) this.bossSpeedBar.text.alpha = 1;
            }
            
            // Handle shot buff aura changes between stages
            if (this.hasShotBuff && !this.playerAura) {
                // Buff gained mid-run — create aura behind player
                const auraSize = this.playerDisplayHeight * 0.7;
                this.playerAura = new PIXI.Graphics();
                this.playerAura.circle(0, 0, auraSize);
                this.playerAura.fill({ color: 0x44bbff, alpha: 0.35 });
                this.playerAura.x = this.playerSprite.x;
                this.playerAura.y = this.playerSprite.y - this.playerDisplayHeight / 2;
                const playerIdx = this.stage.getChildIndex(this.playerSprite);
                this.stage.addChildAt(this.playerAura, playerIdx);
            } else if (!this.hasShotBuff && this.playerAura) {
                this.playerAura.visible = false;
            } else if (this.hasShotBuff && this.playerAura) {
                this.playerAura.visible = true;
                this.playerAura.alpha = 0.35;
            }
            
            // Update background texture if it changed (new biome)
            if (this.backgroundSprite) {
                const newBgTexture = PIXI.Assets.get(this.bgAlias);
                if (newBgTexture && this.backgroundSprite.texture !== newBgTexture) {
                    this.backgroundSprite.texture = newBgTexture;
                }
            }
            
            // Reset enemy arrays (player arrays kept intact)
            this.enemySprites = [];
            this.enemyHpBars = [];
            this.enemySpeedBars = [];
            this.enemySpeedBarTimers = Array(this.enemyCount).fill(3500);
            this.enemyActionTimes = Array(this.enemyCount).fill(3.5);
            
            // Reset attack animation flags
            this._playerAttacking = false;
            this._enemyAttacking = {};
            
            // Create new enemies (player persists — no recreation)
            const width = this.app.screen.width;
            const height = this.app.screen.height;
            // Refresh mobile flag in case viewport changed (device rotation)
            this.isMobile = width <= height || width < 500;
            this.createEnemies(width, height);
            
            // Fade in new enemies for a smooth transition
            for (const enemy of this.enemySprites) {
                if (enemy) {
                    enemy.alpha = 0;
                    this.animateTo(enemy, { alpha: 1 }, 250);
                }
            }
            for (const hpBar of this.enemyHpBars) {
                if (hpBar) {
                    if (hpBar.bar) { hpBar.bar.alpha = 0; this.animateTo(hpBar.bar, { alpha: 1 }, 250); }
                    if (hpBar.barBg) { hpBar.barBg.alpha = 0; this.animateTo(hpBar.barBg, { alpha: 1 }, 250); }
                    if (hpBar.text) { hpBar.text.alpha = 0; this.animateTo(hpBar.text, { alpha: 1 }, 250); }
                }
            }
            for (const speedBar of this.enemySpeedBars) {
                if (speedBar) {
                    if (speedBar.bar) { speedBar.bar.alpha = 0; this.animateTo(speedBar.bar, { alpha: 1 }, 250); }
                    if (speedBar.barBg) { speedBar.barBg.alpha = 0; this.animateTo(speedBar.barBg, { alpha: 1 }, 250); }
                }
            }
            
            // Music continues playing across battles — no stop/restart on boss transitions
            
            // Restart battle
            if (this.interactiveMode) {
                this.initInteractiveState();
                // Spell bar: recreate only if spells changed (currently always empty)
                if (this.spells && this.spells.length > 0) {
                    if (this.spellBarContainer) {
                        this.spellBarContainer.destroy({ children: true, texture: false });
                        this.spellBarContainer = null;
                        this.spellButtons = [];
                    }
                    this.createSpellBar();
                }
                // Consumable bar: update in place — no destroy/recreate to avoid CDN flicker
                if (this.consumableBarContainer && this.consumableButtons.length > 0) {
                    for (const btn of this.consumableButtons) {
                        this.updateConsumableButton(btn.type);
                    }
                } else {
                    this.createConsumableBar();
                }
                this.startInteractiveBattle();
            } else {
                this.preprocessInitialEvents();
                this.startTimedBattle();
            }
        }
        
        static stopBackgroundMusic() {
            if (backgroundMusic) {
                try {
                    backgroundMusic.stop();
                } catch (e) {
                    // Ignore if already stopped
                }
                backgroundMusic = null;
            }
            backgroundMusicGainNode = null;
            currentMusicType = null;
        }
    }

    window.stageBattleGame = {
        start: function (containerId, battleData) {
            const container = document.getElementById(containerId);
            if (!container) {
                console.error('Stage battle container not found:', containerId);
                return;
            }

            if (stageScene) {
                stageScene.destroy();
                stageScene = null;
            }

            const events = resolveEvents(battleData);
            const dotNetRef = battleData?.dotNetRef ?? battleData?.DotNetRef ?? null;
            const stageNumber = battleData?.stageNumber ?? battleData?.StageNumber ?? 1;
            const enemyType = battleData?.enemyType ?? battleData?.EnemyType ?? 'normal';
            const enemyCount = battleData?.enemyCount ?? battleData?.EnemyCount ?? 1;
            const playerName = battleData?.playerName ?? battleData?.PlayerName ?? 'Player';
            const enemyName = battleData?.enemyName ?? battleData?.EnemyName ?? 'Enemy';
            const backgroundPath = battleData?.backgroundPath ?? battleData?.BackgroundPath ?? defaultSprites.background;
            const playerSpritePath = battleData?.playerSpritePath ?? battleData?.PlayerSpritePath ?? defaultSprites.player;
            const enemySprites = battleData?.enemySprites ?? battleData?.EnemySprites;
            const enemyPlacements = battleData?.enemyPlacements ?? battleData?.EnemyPlacements ?? [];
            const initialBattleSpeed = battleData?.battleSpeed ?? battleData?.BattleSpeed ?? 1.0;
            const hasShotBuff = battleData?.HasShotBuff ?? battleData?.hasShotBuff ?? false;

            // Interactive mode fields
            const interactiveMode = battleData?.interactiveMode ?? battleData?.InteractiveMode ?? false;
            const spells = battleData?.spells ?? battleData?.Spells ?? [];
            const playerHP = battleData?.playerHP ?? battleData?.PlayerHP ?? null;
            const playerMaxHP = battleData?.playerMaxHP ?? battleData?.PlayerMaxHP ?? null;
            const playerActionTime = battleData?.playerActionTime ?? battleData?.PlayerActionTime ?? null;
            const enemies = battleData?.enemies ?? battleData?.Enemies ?? [];
            const consumables = battleData?.consumables ?? battleData?.Consumables ?? {};
            const consumableImages = battleData?.consumableImages ?? battleData?.ConsumableImages ?? {};
            const activeBuffs = battleData?.activeBuffs ?? battleData?.ActiveBuffs ?? {};

            stageScene = new StageBattleScene(container, {
                events: events,
                dotNetRef: dotNetRef,
                stageNumber: stageNumber,
                enemyType: enemyType,
                enemyCount: enemyCount,
                playerName: playerName,
                enemyName: enemyName,
                backgroundPath: backgroundPath,
                playerSpritePath: playerSpritePath,
                enemySprites: enemySprites,
                enemyPlacements: enemyPlacements,
                HasShotBuff: hasShotBuff,
                interactiveMode: interactiveMode,
                spells: spells,
                playerHP: playerHP,
                playerMaxHP: playerMaxHP,
                playerActionTime: playerActionTime,
                enemies: enemies,
                consumables: consumables,
                consumableImages: consumableImages,
                activeBuffs: activeBuffs
            });
            
            // Apply initial battle speed after scene is created
            if (initialBattleSpeed !== 1.0) {
                stageScene.setSpeed(initialBattleSpeed);
            }
        },

        destroy: function () {
            if (stageScene) {
                stageScene.destroy();
                stageScene = null;
            }
            // Stop background music when leaving stage mode
            StageBattleScene.stopBackgroundMusic();
        },
        
        // Destroy scene only, keep music playing (for stage transitions)
        destroySceneOnly: function () {
            if (stageScene) {
                stageScene.destroy();
                stageScene = null;
            }
        },

        setSpeed: function (speed) {
            if (stageScene) {
                stageScene.setSpeed(speed);
            }
        },

        setAudioEnabled: function (enabled) {
            if (stageScene) {
                stageScene.setAudioEnabled(enabled);
            }
            // Also update global state if no scene exists yet
            globalAudioEnabled = enabled;
            
            // Control background music even without scene
            if (backgroundMusicGainNode) {
                backgroundMusicGainNode.gain.value = enabled ? 0.3 : 0;
            }
        },

        nextBattle: function (battleData) {
            if (!stageScene || !stageScene.app || !stageScene.stage) {
                console.warn('No active scene, using start() instead');
                // Destroy any orphaned scene first
                if (stageScene) {
                    try { stageScene.destroy(); } catch (e) { /* ignore */ }
                    stageScene = null;
                }
                this.start('phaserBattleContainer', battleData);
                return;
            }

            const events = resolveEvents(battleData);
            const stageNumber = battleData?.StageNumber ?? battleData?.stageNumber ?? 1;
            const enemyType = battleData?.EnemyType ?? battleData?.enemyType ?? 'Normal';
            const enemyCount = battleData?.EnemyCount ?? battleData?.enemyCount ?? 1;
            const playerName = battleData?.PlayerName ?? battleData?.playerName ?? 'Player';
            const enemyName = battleData?.EnemyName ?? battleData?.enemyName ?? 'Enemy';
            const backgroundPath = battleData?.BackgroundPath ?? battleData?.backgroundPath ?? defaultSprites.background;
            const playerSpritePath = battleData?.playerSpritePath ?? battleData?.PlayerSpritePath ?? defaultSprites.player;
            const enemySprites = battleData?.enemySprites ?? battleData?.EnemySprites;
            const enemyPlacements = battleData?.enemyPlacements ?? battleData?.EnemyPlacements ?? [];
            const dotNetRef = battleData?.DotNetRef ?? battleData?.dotNetRef ?? null;
            const hasShotBuff = battleData?.HasShotBuff ?? battleData?.hasShotBuff ?? false;

            // Interactive mode fields
            const interactiveMode = battleData?.interactiveMode ?? battleData?.InteractiveMode ?? false;
            const spells = battleData?.spells ?? battleData?.Spells ?? [];
            const playerHP = battleData?.playerHP ?? battleData?.PlayerHP ?? null;
            const playerMaxHP = battleData?.playerMaxHP ?? battleData?.PlayerMaxHP ?? null;
            const playerActionTime = battleData?.playerActionTime ?? battleData?.PlayerActionTime ?? null;
            const enemies = battleData?.enemies ?? battleData?.Enemies ?? [];
            const consumables = battleData?.consumables ?? battleData?.Consumables ?? {};
            const consumableImages = battleData?.consumableImages ?? battleData?.ConsumableImages ?? {};
            const consumableCooldowns = battleData?.consumableCooldowns ?? battleData?.ConsumableCooldowns ?? {};
            const spellCooldowns = battleData?.spellCooldowns ?? battleData?.SpellCooldowns ?? {};
            const activeBuffs = battleData?.activeBuffs ?? battleData?.ActiveBuffs ?? null;

            // Use fast reset instead of destroy/recreate
            stageScene.resetForNextBattle({
                events: events,
                dotNetRef: dotNetRef,
                stageNumber: stageNumber,
                enemyType: enemyType,
                enemyCount: enemyCount,
                playerName: playerName,
                enemyName: enemyName,
                backgroundPath: backgroundPath,
                playerSpritePath: playerSpritePath,
                enemySprites: enemySprites,
                enemyPlacements: enemyPlacements,
                HasShotBuff: hasShotBuff,
                interactiveMode: interactiveMode,
                spells: spells,
                playerHP: playerHP,
                playerMaxHP: playerMaxHP,
                playerActionTime: playerActionTime,
                enemies: enemies,
                consumables: consumables,
                consumableImages: consumableImages,
                consumableCooldowns: consumableCooldowns,
                spellCooldowns: spellCooldowns,
                activeBuffs: activeBuffs
            });
        }
    };
})();
