(function () {
    'use strict';

    const DEFAULT_WIDTH = 800;
    const DEFAULT_HEIGHT = 500;
    const DEFAULT_EVENT_INTERVAL = 800;

    let app = null;
    let activeScene = null;
    
    // Background music for arena
    let arenaBackgroundMusic = null;
    let arenaBackgroundMusicGainNode = null;
    
    // Session-level cache bust — set once per page load so the browser
    // can reuse HTTP-cached sprites across arena battles.
    const SESSION_CACHE_BUST = `?v=${Date.now()}`;
    // Track which asset paths are already loaded in PIXI.Assets to skip re-fetches
    const loadedAssetAliases = new Set();
    // Cache decoded AudioBuffers so music file is only fetched/decoded once per session
    const audioBufferCache = {};
    // Cache-bust version — refreshes audio once per page session
    const audioCacheBuster = `?v=${Date.now()}`;

    // Shared AudioContext — reused across BattleScene instances to avoid leaks
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

    const spritePaths = {
        attacker: '/sprites/games/my-tuno/tuno_attacking_right.png',
        defender: '/sprites/games/my-tuno/tuno_attacking_left.png',
        background: '/sprites/games/my-tuno/backgrounds/arena.png'
    };

    const getEventField = (evt, field) => {
        if (!evt) return undefined;
        return evt[field] ?? evt[field.toLowerCase()];
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

    const resolveEvents = (battleData) => {
        if (!battleData) {
            return [];
        }
        
        const eventsJson = battleData.EventsJson ?? battleData.eventsJson ?? battleData.eventsjson;
        
        if (eventsJson && typeof eventsJson === 'string') {
            try {
                const parsed = JSON.parse(eventsJson);
                return parsed;
            } catch (e) {
                console.error('Failed to parse EventsJson:', e);
                return [];
            }
        }
        
        if (Array.isArray(battleData)) {
            return battleData;
        }
        
        const events = battleData.events ?? battleData.Events ?? [];
        return events;
    };

    const resolveDotNetRef = (battleData) => {
        if (!battleData || Array.isArray(battleData)) return null;
        return battleData.dotNetRef ?? battleData.DotNetRef ?? null;
    };

    const resolveAttackerName = (battleData) => {
        if (!battleData || Array.isArray(battleData)) return 'Attacker';
        return battleData.attackerName ?? battleData.AttackerName ?? 'Attacker';
    };

    const resolveDefenderName = (battleData) => {
        if (!battleData || Array.isArray(battleData)) return 'Defender';
        return battleData.defenderName ?? battleData.DefenderName ?? 'Defender';
    };

    class BattleScene {
        constructor(container, data) {
            this.container = container;
            this.eventsList = data?.events ?? [];
            this.dotNetRef = data?.dotNetRef ?? null;
            this.mode = data?.mode ?? 'live';
            this.eventInterval = data?.eventInterval ?? DEFAULT_EVENT_INTERVAL;
            this.attackerName = data?.attackerName ?? 'Attacker';
            this.defenderName = data?.defenderName ?? 'Defender';
            this.currentEventIndex = 0;
            this.maxHp = { attacker: 100, defender: 100 };
            this.currentHp = { attacker: 100, defender: 100 };
            this.characterSprites = {};
            this.hpGraphics = null;
            this.hpTexts = {};
            this.nameTexts = {};
            this.replayIndex = 0;
            this.isPlaying = false;
            this.playbackSpeed = 1;
            this.replayAccumulator = 0;
            
            // Shot buff visual
            this.hasShotBuff = data?.HasShotBuff ?? data?.hasShotBuff ?? false;
            this.attackerAura = null;
            
            // Speed bar system - time-based combat
            this.actionTime = { attacker: 5.0, defender: 5.0 }; // In seconds
            this.speedBars = { attacker: null, defender: null };
            this.speedBarTimers = { attacker: 0, defender: 0 }; // Current timer values (0 = ready to attack)
            this.battleStartTime = 0;
            this.currentSimTime = 0;
            this.battleSpeed = 1.0;
            
            // Timer tracking for cleanup
            this._timeoutIds = [];
            this._rafIds = [];

            // PIXI.Text pool for floating damage/status text
            this._textPool = [];

            // ── Interactive mode (spells / Blade Crafter style) ─────────────
            this.interactiveMode = data?.InteractiveMode ?? data?.interactiveMode ?? false;
            this.spells = data?.Spells ?? data?.spells ?? [];
            this.interactivePlayerHP = data?.PlayerHP ?? data?.playerHP ?? null;
            this.interactivePlayerMaxHP = data?.PlayerMaxHP ?? data?.playerMaxHP ?? null;
            this.interactivePlayerActionTime = data?.PlayerActionTime ?? data?.playerActionTime ?? null;
            this.interactiveEnemies = data?.Enemies ?? data?.enemies ?? [];

            // Pending request flags (prevent double-fire during async calls)
            this._playerAttackPending = false;
            this._enemyAttackPending = false;
            this._spellPending = false;
            this._cooldownTickAccum = 0;

            // Spell bar UI elements
            this.spellButtons = [];
            this.spellCooldowns = {};
            this.spellBarContainer = null;

            // Audio system initialization
            this.audioEnabled = true;
            this.musicVolume = 0.3;
            this.sfxVolume = 0.5;
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
                if (!arenaBackgroundMusic) {
                    this.loadBackgroundMusic();
                }
            } else {
                this.audioEnabled = false;
            }
        }
        
        async loadBackgroundMusic() {
            try {
                const musicFile = '/sound/arena_battle.mp3' + audioCacheBuster;
                
                // Use cached AudioBuffer if available, otherwise fetch and decode once
                let audioBuffer = audioBufferCache[musicFile];
                if (!audioBuffer) {
                    const response = await fetch(musicFile);
                    const arrayBuffer = await response.arrayBuffer();
                    audioBuffer = await this.audioContext.decodeAudioData(arrayBuffer);
                    audioBufferCache[musicFile] = audioBuffer;
                }
                
                // Create gain node for volume control
                arenaBackgroundMusicGainNode = this.audioContext.createGain();
                arenaBackgroundMusicGainNode.connect(this.audioContext.destination);
                arenaBackgroundMusicGainNode.gain.value = this.audioEnabled ? this.musicVolume : 0;
                
                // Create and start looping background music
                arenaBackgroundMusic = this.audioContext.createBufferSource();
                arenaBackgroundMusic.buffer = audioBuffer;
                arenaBackgroundMusic.loop = true;
                arenaBackgroundMusic.connect(arenaBackgroundMusicGainNode);
                arenaBackgroundMusic.start(0);
            } catch (e) {
                console.warn('Could not load arena background music:', e);
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
                    
                case 'hit':
                    oscillator.frequency.value = 150;
                    oscillator.type = 'sawtooth';
                    gainNode.gain.setValueAtTime(this.sfxVolume * 0.4, ctx.currentTime);
                    gainNode.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.15);
                    oscillator.start(ctx.currentTime);
                    oscillator.stop(ctx.currentTime + 0.15);
                    break;
                    
                case 'critical':
                    oscillator.frequency.value = 400;
                    oscillator.type = 'sine';
                    gainNode.gain.setValueAtTime(this.sfxVolume * 0.5, ctx.currentTime);
                    gainNode.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.2);
                    
                    const osc2 = ctx.createOscillator();
                    const gain2 = ctx.createGain();
                    osc2.connect(gain2);
                    gain2.connect(ctx.destination);
                    osc2.frequency.value = 600;
                    osc2.type = 'sine';
                    gain2.gain.setValueAtTime(this.sfxVolume * 0.3, ctx.currentTime + 0.05);
                    gain2.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.25);
                    
                    oscillator.start(ctx.currentTime);
                    oscillator.stop(ctx.currentTime + 0.2);
                    osc2.start(ctx.currentTime + 0.05);
                    osc2.stop(ctx.currentTime + 0.25);
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
                        const startTime = ctx.currentTime + i * 0.15;
                        gain.gain.setValueAtTime(this.sfxVolume * 0.4, startTime);
                        gain.gain.exponentialRampToValueAtTime(0.01, startTime + 0.3);
                        osc.start(startTime);
                        osc.stop(startTime + 0.3);
                    });
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

        toggleAudio() {
            this.audioEnabled = !this.audioEnabled;
            
            // Control background music
            if (arenaBackgroundMusicGainNode) {
                arenaBackgroundMusicGainNode.gain.value = this.audioEnabled ? this.musicVolume : 0;
            }
            
            return this.audioEnabled;
        }

        setVolume(musicVol, sfxVol) {
            this.musicVolume = Math.max(0, Math.min(1, musicVol));
            this.sfxVolume = Math.max(0, Math.min(1, sfxVol));
            
            // Update background music volume
            if (arenaBackgroundMusicGainNode && this.audioEnabled) {
                arenaBackgroundMusicGainNode.gain.value = this.musicVolume;
            }
        }

        async initPixi() {
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

            // Detect WebGL context loss (OS reclaims GPU when app is backgrounded)
            this._onContextLost = (e) => {
                console.warn('WebGL context lost — finishing battle to recover');
                e.preventDefault();
                this.finishBattle();
            };
            this.app.canvas.addEventListener('webglcontextlost', this._onContextLost);

            // Detect user returning to the app after backgrounding
            this._onVisibilityChange = () => {
                if (document.visibilityState === 'visible' && !this.battleFinished) {
                    const gl = this.app?.canvas?.getContext('webgl2') || this.app?.canvas?.getContext('webgl');
                    if (!gl || gl.isContextLost()) {
                        console.warn('App returned from background with lost GL context — finishing battle');
                        this.finishBattle();
                    }
                }
            };
            document.addEventListener('visibilitychange', this._onVisibilityChange);

            await this.loadAssets();
            this.create();
        }

        async loadAssets() {
            const toLoad = [];
            
            if (!loadedAssetAliases.has('attackerSprite')) {
                toLoad.push({ alias: 'attackerSprite', src: spritePaths.attacker + SESSION_CACHE_BUST });
            }
            if (!loadedAssetAliases.has('defenderSprite')) {
                toLoad.push({ alias: 'defenderSprite', src: spritePaths.defender + SESSION_CACHE_BUST });
            }
            if (!loadedAssetAliases.has('arenaBg')) {
                toLoad.push({ alias: 'arenaBg', src: spritePaths.background + SESSION_CACHE_BUST });
            }
            
            if (toLoad.length > 0) {
                await PIXI.Assets.load(toLoad);
                for (const a of toLoad) {
                    loadedAssetAliases.add(a.alias);
                }
            }
        }

        create() {
            activeScene = this;

            const { width, height } = this.app.screen;
            
            // Add background image
            const bg = PIXI.Sprite.from('arenaBg');
            bg.width = width;
            bg.height = height;
            bg.x = width / 2;
            bg.y = height / 2;
            bg.anchor.set(0.5);
            bg.zIndex = -2;
            this.stage.addChild(bg);

            this.createArena(width, height);
            this.createCharacters(width, height);
            this.initializeHpFromEvents();
            this.drawHpBars();
            this.drawSpeedBars();

            if (this.interactiveMode) {
                this.initInteractiveState();
                this.createSpellBar();
                this.startInteractiveBattle();
            } else if (this.mode === 'live') {
                this.startTimedBattle();
            } else {
                this.setupReplayLoop();
                this.updateCharacterStates(0);
            }

            this.app.ticker.add(() => this.update());
        }

        createArena(width, height) {
            const line = new PIXI.Graphics();
            line.moveTo(width / 2, 0);
            line.lineTo(width / 2, height);
            line.stroke({ width: 2, color: 0x444444 });
            this.stage.addChild(line);
        }

        createCharacters(width, height) {
            // Mobile portrait: player bottom-center, enemy top-center
            this.isMobile = width <= height || width < 500;

            // Reserve space at bottom for interactive controls
            const bottomBarReserve = this.interactiveMode ? Math.min(140, height * 0.15) : 0;
            const groundOffset = 60 + bottomBarReserve;

            let attackerX, attackerY, defenderX, defenderY;
            if (this.isMobile) {
                // Mobile: player at bottom center, enemy at top center
                attackerX = width * 0.5;
                attackerY = height - groundOffset;
                defenderX = width * 0.5;
                defenderY = height * 0.3;
            } else {
                // Desktop: player left, enemy right
                attackerX = width * 0.25;
                attackerY = height - groundOffset;
                defenderX = width * 0.75;
                defenderY = height - groundOffset;
            }

            const maxSpriteHeight = this.isMobile ? height * 0.25 : height * 0.45;

            const attackerSprite = PIXI.Sprite.from('attackerSprite');
            attackerSprite.anchor.set(0.5, 1);
            attackerSprite.x = attackerX;
            attackerSprite.y = attackerY;
            const attackerScale = this.getSpriteScale(attackerSprite, maxSpriteHeight);
            attackerSprite.scale.set(attackerScale);
            
            // Add blue aura BEFORE sprite so it renders behind
            if (this.hasShotBuff) {
                const attackerDisplayHeight = attackerSprite.height;
                const auraSize = attackerDisplayHeight * 0.7;
                this.attackerAura = new PIXI.Graphics();
                this.attackerAura.circle(0, 0, auraSize);
                this.attackerAura.fill({ color: 0x44bbff, alpha: 0.35 });
                this.attackerAura.x = attackerX;
                this.attackerAura.y = attackerY - attackerSprite.height / 2;
                this.stage.addChild(this.attackerAura);
            }
            
            this.stage.addChild(attackerSprite);

            const defenderSprite = PIXI.Sprite.from('defenderSprite');
            defenderSprite.anchor.set(0.5, 1);
            defenderSprite.x = defenderX;
            defenderSprite.y = defenderY;
            const defenderScale = this.getSpriteScale(defenderSprite, maxSpriteHeight);
            defenderSprite.scale.set(defenderScale);
            this.stage.addChild(defenderSprite);

            this.characterSprites = {
                attacker: { sprite: attackerSprite, originX: attackerX, originY: attackerY },
                defender: { sprite: defenderSprite, originX: defenderX, originY: defenderY }
            };

            this.startIdleAnimation(attackerSprite);
            this.startIdleAnimation(defenderSprite);

            this.nameTexts.attacker = new PIXI.Text({
                text: this.attackerName,
                style: {
                    fontFamily: 'Arial',
                    fontSize: 16,
                    fontWeight: 'bold',
                    fill: 0xffffff
                }
            });
            this.nameTexts.attacker.anchor.set(0.5, 0);
            this.nameTexts.attacker.x = attackerX;
            this.nameTexts.attacker.y = attackerY - attackerSprite.height * attackerScale - 20;
            this.stage.addChild(this.nameTexts.attacker);

            this.nameTexts.defender = new PIXI.Text({
                text: this.defenderName,
                style: {
                    fontFamily: 'Arial',
                    fontSize: 16,
                    fontWeight: 'bold',
                    fill: 0xffffff
                }
            });
            this.nameTexts.defender.anchor.set(0.5, 0);
            this.nameTexts.defender.x = defenderX;
            this.nameTexts.defender.y = defenderY - defenderSprite.height * defenderScale - 20;
            this.stage.addChild(this.nameTexts.defender);
        }

        startIdleAnimation(sprite) {
            sprite.idleAnimationData = {
                originalX: sprite.x,
                originalY: sprite.y,
                originalScale: sprite.scale.x,
                breathTime: 0,
                scaleTime: 0
            };
        }

        getSpriteScale(sprite, maxSpriteHeight) {
            if (!sprite.texture || !sprite.texture.height) {
                return 0.6;
            }
            return Math.min(1, maxSpriteHeight / sprite.texture.height);
        }

        initializeHpFromEvents() {
            let attackerInitialized = false;
            let defenderInitialized = false;

            this.maxHp = { attacker: 100, defender: 100 };
            this.currentHp = { attacker: 100, defender: 100 };
            this.actionTime = { attacker: 5.0, defender: 5.0 };

            this.eventsList.forEach((evt) => {
                const type = getEventField(evt, 'Type');
                if (type !== 'HPUpdate') return;

                const character = getEventField(evt, 'Character');
                const hp = getEventField(evt, 'HP');
                const maxHP = getEventField(evt, 'MaxHP');
                const actionTime = getEventField(evt, 'ActionTime') ?? getEventField(evt, 'actionTime');
                
                if (character === 'Attacker' && !attackerInitialized) {
                    this.maxHp.attacker = maxHP ?? hp ?? 100;
                    this.currentHp.attacker = hp ?? 100;
                    if (actionTime) this.actionTime.attacker = actionTime;
                    attackerInitialized = true;
                }
                if (character === 'Defender' && !defenderInitialized) {
                    this.maxHp.defender = maxHP ?? hp ?? 100;
                    this.currentHp.defender = hp ?? 100;
                    if (actionTime) this.actionTime.defender = actionTime;
                    defenderInitialized = true;
                }
            });
            
            // Initialize speed bar timers to full (start draining from actionTime to 0)
            this.speedBarTimers.attacker = this.actionTime.attacker * 1000;
            this.speedBarTimers.defender = this.actionTime.defender * 1000;
        }

        drawHpBars() {
            const width = this.app.screen.width;
            const height = this.app.screen.height;
            const isMobile = width < 768;
            const barWidth = isMobile ? Math.min(180, width * 0.35) : Math.min(400, width * 0.40);
            const barHeight = isMobile ? Math.min(20, height * 0.03) : Math.min(36, height * 0.055);
            const topBarHeight = 54;
            const paddingTop = topBarHeight + 8;
            const paddingLeft = Math.min(16, width * 0.03);
            const radius = barHeight / 2;

            if (!this.hpGraphics) {
                this.hpGraphics = new PIXI.Graphics();
                this.stage.addChild(this.hpGraphics);
            }
            this.hpGraphics.clear();

            const drawBar = (x, currentHp, maxHp) => {
                const hpPercent = maxHp > 0 ? currentHp / maxHp : 0;
                const fillColor = hpPercent > 0.5 ? 0x4caf50 : hpPercent > 0.25 ? 0xff9800 : 0xf44336;

                // Dark background
                this.hpGraphics.roundRect(x, paddingTop, barWidth, barHeight, radius);
                this.hpGraphics.fill({ color: 0x1a1a1a, alpha: 0.85 });
                // Fill
                if (hpPercent > 0) {
                    this.hpGraphics.roundRect(x, paddingTop, barWidth * hpPercent, barHeight, radius);
                    this.hpGraphics.fill(fillColor);
                }
                // Border
                this.hpGraphics.roundRect(x, paddingTop, barWidth, barHeight, radius);
                this.hpGraphics.stroke({ width: 1.5, color: 0x66bb6a });
            };

            drawBar(paddingLeft, this.currentHp.attacker, this.maxHp.attacker);
            drawBar(width - paddingLeft - barWidth, this.currentHp.defender, this.maxHp.defender);

            const hpFontSize = isMobile ? Math.min(12, barHeight * 0.55) : Math.min(16, barHeight * 0.5);
            if (!this.hpTexts.attacker) {
                this.hpTexts.attacker = new PIXI.Text({
                    text: '',
                    style: {
                        fontFamily: 'Arial, sans-serif',
                        fontSize: hpFontSize,
                        fontWeight: 'bold',
                        fill: 0xffffff,
                        stroke: { color: 0x000000, width: 2 }
                    }
                });
                this.hpTexts.attacker.anchor.set(0.5, 0.5);
                this.stage.addChild(this.hpTexts.attacker);
            }
            this.hpTexts.attacker.x = paddingLeft + barWidth / 2;
            this.hpTexts.attacker.y = paddingTop + barHeight / 2;

            if (!this.hpTexts.defender) {
                this.hpTexts.defender = new PIXI.Text({
                    text: '',
                    style: {
                        fontFamily: 'Arial, sans-serif',
                        fontSize: hpFontSize,
                        fontWeight: 'bold',
                        fill: 0xffffff,
                        stroke: { color: 0x000000, width: 2 }
                    }
                });
                this.hpTexts.defender.anchor.set(0.5, 0.5);
                this.stage.addChild(this.hpTexts.defender);
            }
            this.hpTexts.defender.x = width - paddingLeft - barWidth / 2;
            this.hpTexts.defender.y = paddingTop + barHeight / 2;

            this.hpTexts.attacker.text = `${formatNum(this.currentHp.attacker)} / ${formatNum(this.maxHp.attacker)} HP`;
            this.hpTexts.defender.text = `${formatNum(this.currentHp.defender)} / ${formatNum(this.maxHp.defender)} HP`;

            // Store layout for speed bars
            this._hpBarLayout = { barWidth, barHeight, paddingTop, paddingLeft, isMobile };
        }

        drawSpeedBars() {
            const layout = this._hpBarLayout || {};
            const width = this.app.screen.width;
            const barWidth = layout.barWidth || 200;
            const hpBarHeight = layout.barHeight || 24;
            const hpPaddingTop = layout.paddingTop || 62;
            const paddingLeft = layout.paddingLeft || 16;
            const isMobile = layout.isMobile || false;
            const barHeight = isMobile ? Math.min(8, 8) : Math.min(18, 18);
            const paddingTop = hpPaddingTop + hpBarHeight + 3;
            const radius = barHeight / 2;

            if (!this.speedBarGraphics) {
                this.speedBarGraphics = new PIXI.Graphics();
                this.stage.addChild(this.speedBarGraphics);
            }
            this.speedBarGraphics.clear();

            const drawSpeedBar = (x, timerMs, actionTimeMs) => {
                const speedPercent = actionTimeMs > 0 ? timerMs / actionTimeMs : 0;
                
                // Background
                this.speedBarGraphics.roundRect(x, paddingTop, barWidth, barHeight, radius);
                this.speedBarGraphics.fill({ color: 0x111111, alpha: 0.85 });
                
                // Fill - cyan
                if (speedPercent > 0) {
                    this.speedBarGraphics.roundRect(x, paddingTop, barWidth * speedPercent, barHeight, radius);
                    this.speedBarGraphics.fill(0x00bcd4);
                }
            };

            const attackerActionTimeMs = this.actionTime.attacker * 1000;
            const defenderActionTimeMs = this.actionTime.defender * 1000;

            drawSpeedBar(paddingLeft, this.speedBarTimers.attacker, attackerActionTimeMs);
            drawSpeedBar(width - paddingLeft - barWidth, this.speedBarTimers.defender, defenderActionTimeMs);

            // Speed text (inside bars, white text with black stroke — like HP)
            const speedFontSize = isMobile ? Math.min(7, barHeight * 0.8) : Math.min(14, barHeight * 0.8);
            if (!this._speedTexts) {
                const mkText = () => new PIXI.Text({
                    text: '',
                    style: {
                        fontFamily: 'Arial, sans-serif', fontSize: speedFontSize, fontWeight: 'bold',
                        fill: 0xffffff,
                        stroke: { color: 0x000000, width: 2 }
                    }
                });
                this._speedTexts = { attacker: mkText(), defender: mkText() };
                this._speedTexts.attacker.anchor.set(0.5, 0.5);
                this._speedTexts.defender.anchor.set(0.5, 0.5);
                this.stage.addChild(this._speedTexts.attacker);
                this.stage.addChild(this._speedTexts.defender);
            }
            const atkSec = Math.max(0, this.speedBarTimers.attacker / 1000).toFixed(1);
            const defSec = Math.max(0, this.speedBarTimers.defender / 1000).toFixed(1);
            this._speedTexts.attacker.text = `${atkSec}s`;
            this._speedTexts.attacker.x = paddingLeft + barWidth / 2;
            this._speedTexts.attacker.y = paddingTop + barHeight / 2;
            this._speedTexts.defender.text = `${defSec}s`;
            this._speedTexts.defender.x = width - paddingLeft - barWidth / 2;
            this._speedTexts.defender.y = paddingTop + barHeight / 2;
        }

        // ── Interactive Mode Methods (Blade Crafter spell system) ──────────

        /** Initialize interactive state from Blazor-supplied player/enemy data. */
        initInteractiveState() {
            if (this.interactivePlayerHP != null) {
                this.currentHp.attacker = this.interactivePlayerHP;
                this.maxHp.attacker = this.interactivePlayerMaxHP ?? this.interactivePlayerHP;
            }
            if (this.interactivePlayerActionTime != null) {
                this.actionTime.attacker = this.interactivePlayerActionTime;
            }

            // Defender state from Blazor (1v1 — only first enemy)
            if (this.interactiveEnemies && this.interactiveEnemies.length > 0) {
                const e = this.interactiveEnemies[0];
                this.currentHp.defender = e.hp ?? e.HP ?? 100;
                this.maxHp.defender = e.maxHP ?? e.MaxHP ?? this.currentHp.defender;
                this.actionTime.defender = e.actionTime ?? e.ActionTime ?? 3.5;
            }

            this.drawHpBars();

            this.speedBarTimers.attacker = this.actionTime.attacker * 1000;
            this.speedBarTimers.defender = this.actionTime.defender * 1000;

            for (const spell of this.spells) {
                const id = spell.attackId ?? spell.AttackId;
                this.spellCooldowns[id] = 0;
            }
        }

        /** Create the spell button bar at the bottom of the canvas. */
        createSpellBar() {
            if (!this.spells || this.spells.length === 0) return;

            const width = this.app.screen.width;
            const height = this.app.screen.height;
            const isMobile = width < 768;
            const btnSize = isMobile ? 52 : 68;
            const btnGap = isMobile ? 10 : 14;
            const totalWidth = this.spells.length * btnSize + (this.spells.length - 1) * btnGap;
            const startX = (width - totalWidth) / 2;
            const barY = height - btnSize - 8;

            this.spellBarContainer = new PIXI.Container();
            this.stage.addChild(this.spellBarContainer);

            const backdrop = new PIXI.Graphics();
            backdrop.roundRect(startX - 8, barY - 6, totalWidth + 16, btnSize + 12, 8);
            backdrop.fill({ color: 0x000000, alpha: 0.5 });
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

                const bg = new PIXI.Graphics();
                bg.roundRect(0, 0, btnSize, btnSize, 6);
                bg.fill({ color: 0x2a2a4a, alpha: 0.9 });
                bg.stroke({ color: 0x6666aa, width: 2 });
                btnContainer.addChild(bg);

                const iconText = new PIXI.Text({
                    text: icon,
                    style: { fontSize: isMobile ? 22 : 28, fontFamily: 'Arial, sans-serif', fill: 0xffffff }
                });
                iconText.anchor.set(0.5);
                iconText.x = btnSize / 2;
                iconText.y = btnSize / 2 - 4;
                btnContainer.addChild(iconText);

                const nameText = new PIXI.Text({
                    text: name.length > 6 ? name.substring(0, 6) : name,
                    style: { fontSize: isMobile ? 8 : 10, fontFamily: 'Arial, sans-serif', fill: 0xcccccc }
                });
                nameText.anchor.set(0.5);
                nameText.x = btnSize / 2;
                nameText.y = btnSize - 6;
                btnContainer.addChild(nameText);

                const cdOverlay = new PIXI.Graphics();
                cdOverlay.roundRect(0, 0, btnSize, btnSize, 6);
                cdOverlay.fill({ color: 0x000000, alpha: 0.7 });
                cdOverlay.visible = false;
                btnContainer.addChild(cdOverlay);

                const cdText = new PIXI.Text({
                    text: '',
                    style: { fontSize: 16, fontFamily: 'Arial, sans-serif', fontWeight: 'bold', fill: 0xffffff }
                });
                cdText.anchor.set(0.5);
                cdText.x = btnSize / 2;
                cdText.y = btnSize / 2;
                cdText.visible = false;
                btnContainer.addChild(cdText);

                btnContainer.eventMode = 'static';
                btnContainer.cursor = 'pointer';
                btnContainer.on('pointerdown', () => this.onSpellButtonClick(attackId));

                this.spellBarContainer.addChild(btnContainer);

                this.spellButtons.push({
                    container: btnContainer, bg, iconText, nameText, cdOverlay, cdText,
                    attackId, cooldownSeconds: cooldown, spell
                });
            }
        }

        /** Start interactive battle — real-time speed bars + server calls. */
        startInteractiveBattle() {
            this.battleStartTime = Date.now();
            this.currentSimTime = 0;
            this.battleFinished = false;
            this.isPlaying = true;
            this._playerAttackPending = false;
            this._enemyAttackPending = false;
            this._spellPending = false;
            this._cooldownTickAccum = 0;
        }

        /** Spell button click handler. */
        onSpellButtonClick(attackId) {
            if (this.battleFinished || !this.isPlaying) return;
            if (this._spellPending) return;
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

        /** Call server: enemy (defender) auto-attack. */
        async requestEnemyAttack() {
            if (!this.dotNetRef || this.battleFinished) {
                this._enemyAttackPending = false;
                return;
            }
            try {
                const json = await this.dotNetRef.invokeMethodAsync('OnEnemyAttack', 0);
                if (json) this.processServerResult(JSON.parse(json));
            } catch (e) {
                console.warn('OnEnemyAttack error:', e);
            } finally {
                this._enemyAttackPending = false;
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
                    // New format: { spells: {...}, consumables: {...} }
                    const spellCooldowns = data.spells ?? data;
                    for (const [id, remaining] of Object.entries(spellCooldowns)) {
                        this.spellCooldowns[id] = remaining;
                    }
                }
            } catch (e) {
                console.warn('OnTickCooldowns error:', e);
            }
        }

        /** Process a CombatActionResult from the server. */
        processServerResult(result) {
            if (!result) return;

            const events = result.events ?? result.Events ?? [];
            for (const evt of events) {
                this.processInteractiveEvent(evt);
            }

            const cooldowns = result.spellCooldowns ?? result.SpellCooldowns;
            if (cooldowns) {
                for (const [id, remaining] of Object.entries(cooldowns)) {
                    this.spellCooldowns[id] = remaining;
                }
            }

            const battleOver = result.battleOver ?? result.BattleOver ?? false;
            if (battleOver) {
                this.isPlaying = false;
                const outcome = result.outcome ?? result.Outcome;
                // outcome: 0=AttackerWon, 1=DefenderWon, 2=Draw
                if (outcome === 0) {
                    this.showVictory('Attacker');
                } else if (outcome === 2) {
                    this.showDraw();
                } else {
                    this.showVictory('Defender');
                }
                setTimeout(() => this.finishBattle(), 2000 / this.battleSpeed);
            }
        }

        /** Process a single interactive event. */
        processInteractiveEvent(evt) {
            const evtType = evt.type ?? evt.Type;
            const attackId = evt.attackId ?? evt.AttackId;

            switch (evtType) {
                case 'HPUpdate':
                    this.processEvent(evt);
                    break;
                case 'Attack':
                    if (attackId) {
                        this.handleSpellAttack(evt);
                    } else {
                        this.processEvent(evt);
                    }
                    break;
                case 'KO':
                    this.processEvent(evt);
                    break;
                case 'StatusEffect':
                    this.handleStatusEffect(evt);
                    break;
                case 'Victory':
                case 'BattleStart':
                    break;
            }
        }

        /** Handle a spell attack event with VFX. */
        handleSpellAttack(evt) {
            const attacker = evt.attacker ?? evt.Attacker;
            const defender = evt.defender ?? evt.Defender;
            const damage = evt.damage ?? evt.Damage ?? 0;
            const isCritical = evt.isCritical ?? evt.IsCritical ?? false;
            const vfxType = evt.vfxType ?? evt.VfxType;
            const vfxColor = evt.vfxColor ?? evt.VfxColor ?? '#ff6600';
            const doScreenShake = evt.screenShake ?? evt.ScreenShake ?? false;
            const visualHint = evt.visualHint ?? evt.VisualHint;
            const abilityName = evt.abilityName ?? evt.AbilityName ?? 'Spell';
            const effectName = evt.effectName ?? evt.EffectName;
            const attackId = evt.attackId ?? evt.AttackId;

            const color = typeof vfxColor === 'string' && vfxColor.startsWith('#')
                ? parseInt(vfxColor.replace('#', ''), 16)
                : (typeof vfxColor === 'number' ? vfxColor : 0xff6600);

            if (doScreenShake || visualHint === 'screenShake') this.screenShake();

            const atkChar = attacker === 'Attacker' ? this.characterSprites.attacker : this.characterSprites.defender;
            const defChar = defender === 'Defender' ? this.characterSprites.defender : this.characterSprites.attacker;
            if (!atkChar || !defChar) return;

            const atkSpr = atkChar.sprite;
            const defSpr = defChar.sprite;

            if (attacker === 'Attacker') {
                // Player spell — lunge animation
                const startX = atkChar.originX;
                this.animateTo(atkSpr, { x: startX + 40 }, 120, () => {
                    this.animateTo(atkSpr, { x: startX }, 200);
                });

                // Show ability name above attacker
                this.showFloatingText(abilityName.toUpperCase(), atkSpr.x,
                    atkSpr.y - atkSpr.height * 0.8, color);

                if (defender === 'Attacker') {
                    // Self-buff/heal
                    this.playBuffVfx(atkSpr, color);
                    if (damage < 0) {
                        this.showFloatingText(`+${formatNum(Math.abs(damage))}`, atkSpr.x,
                            atkSpr.y - atkSpr.height * 0.6, 0x44ff44);
                    }
                    if (effectName) this.showEffectLabel(effectName, atkSpr);
                } else {
                    // Hit defender
                    this.playSpellVfx(vfxType, color, atkSpr, defSpr);
                    defSpr.tint = isCritical ? 0xff0000 : 0xff5555;
                    setTimeout(() => { if (!defSpr.destroyed) defSpr.tint = 0xffffff; }, 200 / this.battleSpeed);
                    if (damage > 0) {
                        this.showDamageText(damage, isCritical, defSpr.x, defSpr.y - defSpr.height * 0.6);
                    }
                    if (effectName) this.showEffectLabel(effectName, defSpr);
                }

                if (attackId) {
                    this.playSpellSound(attackId);
                } else {
                    this.playSound(isCritical ? 'critical' : 'attack');
                }
            }
        }

        /** Handle status effect events (bleed ticks, etc.). */
        handleStatusEffect(evt) {
            const character = evt.character ?? evt.Character;
            const effectName = evt.effectName ?? evt.EffectName ?? '';
            const damage = evt.damage ?? evt.Damage ?? 0;

            const target = character === 'Attacker'
                ? this.characterSprites.attacker?.sprite
                : this.characterSprites.defender?.sprite;
            if (!target) return;

            this.showEffectLabel(effectName, target);
            if (damage > 0) {
                this.showDamageText(damage, false, target.x, target.y - target.height * 0.6);
            }
        }

        /** Floating text that drifts up and fades. */
        showFloatingText(text, x, y, color) {
            if (!this.stage || !this.app) return;
            const floatText = this._getPooledText(text, {
                fontFamily: 'Arial', fontSize: 26, fontWeight: 'bold',
                fill: color, stroke: { color: 0x000000, width: 4 }
            });
            if (!floatText) return;
            floatText.anchor.set(0.5);
            floatText.x = x;
            floatText.y = y;
            this.stage.addChild(floatText);
            this.animateTo(floatText, { y: floatText.y - 70, alpha: 0 }, 900, () => {
                this._releaseText(floatText);
            });
        }

        /** Show damage number floating above a position. */
        showDamageText(damage, isCritical, x, y) {
            const text = isCritical ? `CRIT! -${formatNum(damage)}` : `-${formatNum(damage)}`;
            const damageText = this._getPooledText(text, {
                fontFamily: 'Arial',
                fontSize: isCritical ? 28 : 24,
                fontWeight: 'bold',
                fill: isCritical ? 0xffff00 : 0xff4444,
                stroke: { color: 0x000000, width: 3 }
            });
            damageText.anchor.set(0.5);
            damageText.x = x;
            damageText.y = y;
            this.stage.addChild(damageText);
            this.animateTo(damageText, {
                y: damageText.y - (isCritical ? 80 : 60), alpha: 0
            }, isCritical ? 1000 : 800, () => {
                this._releaseText(damageText);
            });
        }

        /** Show a status effect label floating above a sprite. */
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

        /** Show Draw result. */
        showDraw() {
            const drawText = new PIXI.Text({
                text: 'Empate!',
                style: {
                    fontFamily: 'Arial', fontSize: 48, fontWeight: 'bold',
                    fill: 0xcccccc,
                    stroke: { color: 0x000000, width: 6 }
                }
            });
            drawText.anchor.set(0.5);
            drawText.x = this.app.screen.width / 2;
            drawText.y = this.app.screen.height / 2 - 50;
            drawText.alpha = 0;
            drawText.scale.set(0.5);
            this.stage.addChild(drawText);
            this.animateTo(drawText, { alpha: 1, scale: 1.2 }, 400);
            setTimeout(() => {
                this.animateTo(drawText, { alpha: 0, y: drawText.y - 30 }, 800, () => {
                    this.stage.removeChild(drawText);
                });
            }, 1200 / this.battleSpeed);
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

        /** Play spell VFX based on type. */
        playSpellVfx(vfxType, color, source, target) {
            if (!source || !target || !this.stage) return;
            const srcX = source.x;
            const srcY = source.y - (source.height || 40) / 2;
            const tgtX = target.x;
            const tgtY = target.y - (target.height || 40) / 2;
            switch (vfxType) {
                case 0: this.createProjectileVfx(color, srcX, srcY, tgtX, tgtY); break;
                case 1: this.createBeamVfx(color, tgtX, tgtY); break;
                case 2: this.createAoeVfx(color, tgtX, tgtY); break;
                case 4: this.createMeleeStrikeVfx(color, tgtX, tgtY); break;
                case 5: this.createSoundWaveVfx(color, srcX, srcY, tgtX, tgtY); break;
                case 6: this.createMusicNotesVfx(color, srcX, srcY, tgtX, tgtY); break;
                default: this.createProjectileVfx(color, srcX, srcY, tgtX, tgtY);
            }
        }

        createProjectileVfx(color, srcX, srcY, tgtX, tgtY) {
            const proj = new PIXI.Graphics();
            proj.circle(0, 0, 8);
            proj.fill({ color, alpha: 0.9 });
            proj.x = srcX; proj.y = srcY;
            this.stage.addChild(proj);
            const glow = new PIXI.Graphics();
            glow.circle(0, 0, 14);
            glow.fill({ color, alpha: 0.3 });
            glow.x = srcX; glow.y = srcY;
            this.stage.addChild(glow);
            const duration = 350 / this.battleSpeed;
            const startTime = Date.now();
            const animate = () => {
                const t = Math.min((Date.now() - startTime) / duration, 1);
                proj.x = srcX + (tgtX - srcX) * t;
                proj.y = srcY + (tgtY - srcY) * t;
                glow.x = proj.x; glow.y = proj.y;
                glow.alpha = 0.3 * (1 - t * 0.5);
                if (t < 1) {
                    requestAnimationFrame(animate);
                } else {
                    const flash = new PIXI.Graphics();
                    flash.circle(0, 0, 20);
                    flash.fill({ color, alpha: 0.8 });
                    flash.x = tgtX; flash.y = tgtY;
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

        createBeamVfx(color, tgtX, tgtY) {
            const beam = new PIXI.Graphics();
            beam.rect(-4, -200, 8, 200);
            beam.fill({ color, alpha: 0.8 });
            beam.x = tgtX; beam.y = tgtY;
            beam.alpha = 0;
            this.stage.addChild(beam);
            this.animateTo(beam, { alpha: 1 }, 100, () => {
                this.animateTo(beam, { alpha: 0 }, 400, () => {
                    if (beam.parent) beam.parent.removeChild(beam);
                    beam.destroy();
                });
            });
        }

        createAoeVfx(color, tgtX, tgtY) {
            const ring = new PIXI.Graphics();
            ring.circle(0, 0, 10);
            ring.stroke({ color, width: 3, alpha: 0.9 });
            ring.x = tgtX; ring.y = tgtY;
            this.stage.addChild(ring);
            this.animateTo(ring, { scale: 6, alpha: 0 }, 500, () => {
                if (ring.parent) ring.parent.removeChild(ring);
                ring.destroy();
            });
        }

        createMeleeStrikeVfx(color, tgtX, tgtY) {
            const slash = new PIXI.Graphics();
            slash.moveTo(-15, -15); slash.lineTo(15, 15);
            slash.moveTo(15, -15); slash.lineTo(-15, 15);
            slash.stroke({ color, width: 4, alpha: 0.9 });
            slash.x = tgtX; slash.y = tgtY;
            this.stage.addChild(slash);
            this.animateTo(slash, { alpha: 0, scale: 2 }, 350, () => {
                if (slash.parent) slash.parent.removeChild(slash);
                slash.destroy();
            });
        }

        createSoundWaveVfx(color, srcX, srcY, tgtX, tgtY) {
            const midX = (srcX + tgtX) / 2;
            const midY = (srcY + tgtY) / 2;
            for (let i = 0; i < 3; i++) {
                const ring = new PIXI.Graphics();
                ring.circle(0, 0, 12);
                ring.stroke({ color, width: 3, alpha: 0.8 });
                ring.x = midX; ring.y = midY;
                ring.scale.set(0.3); ring.alpha = 0;
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
                    const noteStartX = note.x;
                    const noteStartY = note.y;
                    const animate = () => {
                        const t = Math.min((Date.now() - startTime) / duration, 1);
                        note.x = noteStartX + (endX - noteStartX) * t;
                        note.y = noteStartY + (endY - noteStartY) * t - Math.sin(t * Math.PI) * 20;
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

        /** Upward particles on target (buff/heal). */
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

        /** Play spell-specific sound. */
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
                case 'heavy_attack':
                    playNote(120, 'sawtooth', 0, 0.15, 0.5);
                    playNote(180, 'square', 0.03, 0.12, 0.4);
                    break;
                case 'guitarra_barrage':
                    for (let i = 0; i < 6; i++) {
                        playNote(82 + i * 15, 'sawtooth', i * 0.07, 0.08, 0.4);
                        playNote(165 + i * 10, 'square', i * 0.07 + 0.03, 0.06, 0.25);
                    }
                    break;
                case 'bandolim_swiftchord':
                    playNote(587, 'triangle', 0, 0.12, 0.4);
                    playNote(784, 'triangle', 0.02, 0.1, 0.35);
                    playNote(988, 'sine', 0.04, 0.08, 0.3);
                    break;
                case 'cavaquinho_paralysis':
                    for (let i = 0; i < 5; i++) playNote(800 + Math.random() * 400, 'square', i * 0.06, 0.05, 0.3);
                    playNote(200, 'sawtooth', 0.35, 0.2, 0.4);
                    break;
                case 'acordeao_fear':
                    playNote(130, 'sawtooth', 0, 0.6, 0.4);
                    playNote(138, 'sawtooth', 0, 0.55, 0.35);
                    playNote(98, 'square', 0.1, 0.4, 0.3);
                    playNote(65, 'triangle', 0.2, 0.4, 0.25);
                    break;
                case 'contrabaixo_sonicboom': {
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
                case 'percussao_combo':
                    playNote(100, 'square', 0, 0.08, 0.35);
                    playNote(120, 'square', 0.12, 0.08, 0.4);
                    playNote(80, 'sawtooth', 0.28, 0.15, 0.55);
                    playNote(60, 'triangle', 0.3, 0.2, 0.4);
                    break;
                case 'pandeireta_boomerang':
                    for (let i = 0; i < 4; i++) playNote(600 + i * 200, 'sine', i * 0.08, 0.1, 0.25);
                    for (let i = 0; i < 4; i++) playNote(1400 - i * 200, 'sine', 0.4 + i * 0.08, 0.1, 0.25);
                    break;
                case 'estandarte_rally':
                    [262, 330, 392, 523, 659].forEach((f, i) => playNote(f, 'square', i * 0.1, 0.15, 0.35));
                    break;
                case 'violino_sleep':
                    [659, 587, 523, 494, 440].forEach((f, i) => playNote(f, 'sine', i * 0.15, 0.25, 0.35));
                    playNote(330, 'triangle', 0, 0.7, 0.15);
                    break;
                default:
                    this.playSound('attack');
                    break;
            }
        }

        // ── End Interactive Mode Methods ───────────────────────────────────

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

        scheduleNextEvent() {
            if (this.currentEventIndex >= this.eventsList.length) {
                this.finishBattle();
                return;
            }

            const evt = this.eventsList[this.currentEventIndex];
            this.processEvent(evt);
            this.currentEventIndex += 1;

            setTimeout(() => {
                this.scheduleNextEvent();
            }, this.eventInterval);
        }

        processEvent(evt) {
            const type = getEventField(evt, 'Type');
            
            if (type === 'HPUpdate') {
                const character = getEventField(evt, 'Character');
                const hp = getEventField(evt, 'HP') ?? 0;

                if (character === 'Attacker') {
                    this.currentHp.attacker = hp;
                } else if (character === 'Defender') {
                    this.currentHp.defender = hp;
                }
                this.drawHpBars();
                return;
            }

            if (type === 'Attack') {
                const attacker = getEventField(evt, 'Attacker');
                const defender = getEventField(evt, 'Defender');
                const damage = getEventField(evt, 'Damage');
                this.playAttack(attacker, defender, damage, evt);
                return;
            }

            if (type === 'KO') {
                const character = getEventField(evt, 'Character');
                this.playKo(character);
                return;
            }

            if (type === 'Victory') {
                const winner = getEventField(evt, 'Winner');
                this.showVictory(winner);
            }
        }

        playAttack(attackerKey, defenderKey, damage, evt) {
            const attacker = attackerKey === 'Defender' ? this.characterSprites.defender : this.characterSprites.attacker;
            const defender = defenderKey === 'Attacker' ? this.characterSprites.attacker : this.characterSprites.defender;

            if (!attacker || !defender) return;

            const direction = attackerKey === 'Defender' ? -1 : 1;
            const distance = Math.abs(defender.sprite.x - attacker.sprite.x);
            const lungeOffset = Math.min(220, distance * 0.6);
            const startX = attacker.originX;
            const startY = attacker.originY;
            const targetX = startX + direction * lungeOffset;
            const targetY = startY - 15;

            const damageValue = damage ?? 0;
            const isCritical = evt?.isCritical === true || evt?.IsCritical === true;
            const isBlocked = evt?.isBlocked === true || evt?.IsBlocked === true;
            const isDodged = evt?.isDodged === true || evt?.IsDodged === true;
            const isBoosted = evt?.isBoosted === true || evt?.IsBoosted === true;

            this.playSound((isBlocked || isDodged) ? 'block' : (isCritical ? 'critical' : 'attack'));

            const lungeDuration = isCritical ? 150 : 200;
            this.animateTo(attacker.sprite, { 
                x: targetX, 
                y: targetY,
                rotation: (direction * (isCritical ? 18 : 12)) * Math.PI / 180
            }, lungeDuration, () => {
                this.animateTo(attacker.sprite, { 
                    x: startX, 
                    y: startY,
                    rotation: 0
                }, 240);
            });

            if (isBlocked || isDodged) {
                // Blocked/Dodged: cyan shield flash instead of red damage tint
                defender.sprite.tint = 0x00e5ff;
                setTimeout(() => defender.sprite.tint = 0xffffff, 300 / this.battleSpeed);
            } else {
                const defenderTintColor = isCritical ? 0xff0000 : 0xff5555;
                defender.sprite.tint = defenderTintColor;
                setTimeout(() => defender.sprite.tint = 0xffffff, 200 / this.battleSpeed);
            }

            this.playSound('hit');

            if (!isBlocked && !isDodged) {
                const defenderStartX = defender.sprite.x;
                const recoilDistance = isCritical ? 30 : 20;
                this.animateTo(defender.sprite, { 
                    x: defenderStartX + direction * recoilDistance 
                }, isCritical ? 100 : 120, () => {
                    this.animateTo(defender.sprite, { x: defenderStartX }, 100);
                });
            }

            const impactX = defender.sprite.x;
            const impactY = defender.sprite.y - defender.sprite.height * 0.4;
            
            const impactColor = (isBlocked || isDodged) ? 0x00e5ff : (isCritical ? 0xffff00 : 0xffd54f);
            const impactSize = isCritical ? 25 : 18;
            const impact = new PIXI.Graphics();
            impact.circle(impactX, impactY, impactSize);
            impact.fill({ color: impactColor, alpha: 0.9 });
            this.stage.addChild(impact);
            
            this.fadeOut(impact, isCritical ? 400 : 300, () => {
                this.stage.removeChild(impact);
            });

            if (!isBlocked && !isDodged) {
                const slash = new PIXI.Graphics();
                const slashColor = isCritical ? 0xffff00 : 0xffffff;
                slash.moveTo(attacker.sprite.x, attacker.sprite.y - attacker.sprite.height * 0.5);
                slash.lineTo(defender.sprite.x, defender.sprite.y - defender.sprite.height * 0.5);
                slash.stroke({ width: isCritical ? 6 : 4, color: slashColor, alpha: 0.9 });
                this.stage.addChild(slash);
                
                this.fadeOut(slash, isCritical ? 250 : 200, () => {
                    this.stage.removeChild(slash);
                });
            }

            // Floating text on defender
            if (isDodged) {
                const dodgeText = this._getPooledText('DODGE', {
                    fontFamily: 'Arial',
                    fontSize: 28,
                    fontWeight: 'bold',
                    fill: 0x00e5ff,
                    stroke: { color: 0x000000, width: 4 }
                });
                dodgeText.anchor.set(0.5);
                dodgeText.x = defender.sprite.x;
                dodgeText.y = defender.sprite.y - defender.sprite.height * 0.6;
                this.stage.addChild(dodgeText);

                this.animateTo(dodgeText, {
                    y: dodgeText.y - 70,
                    alpha: 0
                }, 900, () => {
                    this._releaseText(dodgeText);
                });
            } else if (isBlocked) {
                const blockedText = this._getPooledText('BLOCKED', {
                    fontFamily: 'Arial',
                    fontSize: 28,
                    fontWeight: 'bold',
                    fill: 0x00e5ff,
                    stroke: { color: 0x000000, width: 4 }
                });
                blockedText.anchor.set(0.5);
                blockedText.x = defender.sprite.x;
                blockedText.y = defender.sprite.y - defender.sprite.height * 0.6;
                this.stage.addChild(blockedText);

                this.animateTo(blockedText, {
                    y: blockedText.y - 70,
                    alpha: 0
                }, 900, () => {
                    this._releaseText(blockedText);
                });
            } else {
                const damageText = this._getPooledText(
                    isCritical ? `CRIT! -${formatNum(damageValue)}` : `-${formatNum(damageValue)}`,
                    {
                        fontFamily: 'Arial',
                        fontSize: isCritical ? 28 : 24,
                        fontWeight: 'bold',
                        fill: isCritical ? 0xffff00 : 0xff4444,
                        stroke: { color: 0x000000, width: 3 }
                    }
                );
                damageText.anchor.set(0.5);
                damageText.x = defender.sprite.x;
                damageText.y = defender.sprite.y - defender.sprite.height * 0.6;
                this.stage.addChild(damageText);

                this.animateTo(damageText, { 
                    y: damageText.y - (isCritical ? 80 : 60),
                    alpha: 0
                }, isCritical ? 1000 : 800, () => {
                    this._releaseText(damageText);
                });
            }

            // Floating text on attacker side
            if (isBoosted) {
                const extraText = this._getPooledText('EXTRA', {
                    fontFamily: 'Arial',
                    fontSize: 26,
                    fontWeight: 'bold',
                    fill: 0xff9800,
                    stroke: { color: 0x000000, width: 4 }
                });
                extraText.anchor.set(0.5);
                extraText.x = attacker.sprite.x;
                extraText.y = attacker.sprite.y - attacker.sprite.height * 0.8;
                this.stage.addChild(extraText);

                this.animateTo(extraText, {
                    y: extraText.y - 50,
                    alpha: 0
                }, 800, () => {
                    this._releaseText(extraText);
                });
            }
            
            if (!isBlocked && !isDodged) {
                const attackerText = this._getPooledText(`+${formatNum(damageValue)}`, {
                    fontFamily: 'Arial',
                    fontSize: 18,
                    fontWeight: 'bold',
                    fill: 0x4caf50
                });
                attackerText.anchor.set(0.5);
                attackerText.x = attacker.sprite.x;
                attackerText.y = attacker.sprite.y - attacker.sprite.height * 0.6;
                this.stage.addChild(attackerText);

                this.animateTo(attackerText, { 
                    y: attackerText.y - 20,
                    alpha: 0
                }, 700, () => {
                    this._releaseText(attackerText);
                });
            }
        }

        playKo(character) {
            const target = character === 'Defender' ? this.characterSprites.defender : this.characterSprites.attacker;
            if (!target) return;
            
            this.playSound('ko');
            
            this.animateTo(target.sprite, {
                alpha: 0.4,
                rotation: (character === 'Defender' ? 90 : -90) * Math.PI / 180,
                y: target.sprite.y + 30
            }, 600);
            
            const koText = new PIXI.Text({
                text: 'K.O.!',
                style: {
                    fontFamily: 'Arial',
                    fontSize: 36,
                    fontWeight: 'bold',
                    fill: 0xff0000,
                    stroke: { color: 0x000000, width: 4 }
                }
            });
            koText.anchor.set(0.5);
            koText.x = target.sprite.x;
            koText.y = target.sprite.y - target.sprite.height - 30;
            koText.alpha = 0;
            this.stage.addChild(koText);
            
            this.animateTo(koText, { alpha: 1 }, 200, () => {
                setTimeout(() => {
                    this.animateTo(koText, { alpha: 0 }, 200, () => {
                        this.stage.removeChild(koText);
                    });
                }, 400 / this.battleSpeed);
            });
        }

        showVictory(winner) {
            const isAttackerWinner = winner === 'Attacker' || winner === this.attackerName;
            const winnerSprite = isAttackerWinner ? this.characterSprites.attacker : this.characterSprites.defender;
            const winnerName = isAttackerWinner ? this.attackerName : this.defenderName;
            
            this.playSound('victory');
            
            if (winnerSprite) {
                const originalY = winnerSprite.sprite.y;
                this.animateTo(winnerSprite.sprite, { y: originalY - 20 }, 200, () => {
                    this.animateTo(winnerSprite.sprite, { y: originalY }, 200, () => {
                        this.animateTo(winnerSprite.sprite, { y: originalY - 20 }, 200, () => {
                            this.animateTo(winnerSprite.sprite, { y: originalY }, 200);
                        });
                    });
                });
            }
            
            const victoryText = new PIXI.Text({
                text: `${winnerName} vence!`,
                style: {
                    fontFamily: 'Arial',
                    fontSize: 48,
                    fontWeight: 'bold',
                    fill: 0xffd700,
                    stroke: { color: 0x000000, width: 6 },
                    dropShadow: {
                        color: 0x000000,
                        blur: 5,
                        angle: Math.PI / 4,
                        distance: 3
                    }
                }
            });
            victoryText.anchor.set(0.5);
            victoryText.x = this.app.screen.width / 2;
            victoryText.y = this.app.screen.height / 2 - 50;
            victoryText.alpha = 0;
            victoryText.scale.set(0.5);
            this.stage.addChild(victoryText);

            this.animateTo(victoryText, { alpha: 1, scale: 1.2 }, 400);
            
            setTimeout(() => {
                this.animateTo(victoryText, { 
                    alpha: 0, 
                    y: victoryText.y - 30 
                }, 800, () => {
                    this.stage.removeChild(victoryText);
                });
            }, 1200 / this.battleSpeed);
        }

        finishBattle() {
            if (this.battleFinished) return;
            this.battleFinished = true;
            
            if (this.dotNetRef?.invokeMethodAsync) {
                this.dotNetRef.invokeMethodAsync('OnBattleFinished').catch(e => {
                    console.warn('Could not notify Blazor of battle finish:', e);
                });
            }
        }

        setupReplayLoop() {
            this.isPlaying = false;
            this.replayAccumulator = 0;
        }

        updateCharacterStates(eventIndex) {
            this.initializeHpFromEvents();

            for (let i = 0; i <= eventIndex && i < this.eventsList.length; i += 1) {
                const evt = this.eventsList[i];
                const type = getEventField(evt, 'Type');

                if (type === 'HPUpdate') {
                    const character = getEventField(evt, 'Character');
                    const hp = getEventField(evt, 'HP') ?? 0;
                    if (character === 'Attacker') {
                        this.currentHp.attacker = hp;
                    } else if (character === 'Defender') {
                        this.currentHp.defender = hp;
                    }
                }
            }

            this.drawHpBars();
        }

        setReplayPlaying(isPlaying) {
            this.isPlaying = isPlaying;
        }

        setReplaySpeed(speed) {
            this.playbackSpeed = speed || 1;
            this.battleSpeed = speed || 1;
        }

        jumpToEvent(index) {
            if (index < 0 || index >= this.eventsList.length) return;
            this.replayIndex = index;
            this.updateCharacterStates(index);
            this.processEvent(this.eventsList[index]);
        }

        update() {
            const deltaMs = this.app.ticker.deltaMS;
            
            // Character idle animations
            Object.values(this.characterSprites).forEach(char => {
                if (char.sprite.idleAnimationData) {
                    const data = char.sprite.idleAnimationData;
                    data.breathTime += deltaMs / 1000;
                    data.scaleTime += deltaMs / 1000;
                    
                    const breathOffset = Math.sin(data.breathTime * Math.PI / 1.8) * 8;
                    char.sprite.y = data.originalY + breathOffset;

                    const swayOffset = Math.sin(data.breathTime * 0.8) * 3;
                    char.sprite.x = data.originalX + swayOffset;
                    
                    const scaleOffset = Math.sin(data.scaleTime * Math.PI / 2) * 0.02;
                    const newScale = data.originalScale * (1 + scaleOffset);
                    char.sprite.scale.set(newScale);
                }
            });
            
            // Animate attacker aura (shot buff glow)
            if (this.attackerAura && !this.attackerAura.destroyed && this.characterSprites.attacker) {
                const attacker = this.characterSprites.attacker;
                this.attackerAura.x = attacker.sprite.x;
                const spriteHeight = attacker.sprite.height;
                this.attackerAura.y = attacker.sprite.y - spriteHeight / 2;
                const time = performance.now() / 1000;
                this.attackerAura.alpha = 0.25 + Math.sin(time * 1.2) * 0.12;
            }

            // ── Interactive mode: speed bars trigger server calls ──
            if (this.interactiveMode && !this.battleFinished && this.isPlaying) {
                const simDelta = deltaMs * this.battleSpeed;
                this.currentSimTime += simDelta;

                // Attacker (player) speed bar
                if (this.currentHp.attacker > 0) {
                    this.speedBarTimers.attacker = Math.max(0, this.speedBarTimers.attacker - simDelta);
                    if (this.speedBarTimers.attacker <= 0 && !this._playerAttackPending) {
                        this._playerAttackPending = true;
                        this.speedBarTimers.attacker = this.actionTime.attacker * 1000;
                        this.requestPlayerAutoAttack();
                    }
                }

                // Defender (enemy) speed bar
                if (this.currentHp.defender > 0) {
                    this.speedBarTimers.defender = Math.max(0, this.speedBarTimers.defender - simDelta);
                    if (this.speedBarTimers.defender <= 0 && !this._enemyAttackPending) {
                        this._enemyAttackPending = true;
                        this.speedBarTimers.defender = this.actionTime.defender * 1000;
                        this.requestEnemyAttack();
                    }
                }

                // Tick cooldowns periodically (~200ms)
                this._cooldownTickAccum += simDelta;
                if (this._cooldownTickAccum >= 200) {
                    const elapsed = this._cooldownTickAccum / 1000;
                    this._cooldownTickAccum = 0;
                    for (const id of Object.keys(this.spellCooldowns)) {
                        this.spellCooldowns[id] = Math.max(0, this.spellCooldowns[id] - elapsed);
                    }
                    this.updateSpellCooldownVisuals();
                    this.requestTickCooldowns(elapsed);
                }

                this.drawSpeedBars();
                return; // Don't process pre-computed events
            }

            // Time-based battle simulation for live mode
            if (this.mode === 'live' && !this.battleFinished && this.isPlaying && this.battleEvents) {
                // Advance simulation time based on battle speed
                const simDelta = deltaMs * this.battleSpeed;
                this.currentSimTime += simDelta;
                
                // Update speed bar timers (drain towards 0)
                if (this.currentHp.attacker > 0) {
                    this.speedBarTimers.attacker = Math.max(0, this.speedBarTimers.attacker - simDelta);
                }
                if (this.currentHp.defender > 0) {
                    this.speedBarTimers.defender = Math.max(0, this.speedBarTimers.defender - simDelta);
                }
                
                // Process events that should occur at current simulation time
                while (this.currentEventIndex < this.battleEvents.length) {
                    const eventData = this.battleEvents[this.currentEventIndex];
                    if (eventData.simTime > this.currentSimTime) break;
                    
                    const evt = eventData.event;
                    const type = getEventField(evt, 'Type');
                    
                    // When an attack happens, reset the attacker's speed bar
                    if (type === 'Attack') {
                        const attacker = getEventField(evt, 'Attacker');
                        if (attacker === 'Attacker') {
                            this.speedBarTimers.attacker = this.actionTime.attacker * 1000;
                        } else if (attacker === 'Defender') {
                            this.speedBarTimers.defender = this.actionTime.defender * 1000;
                        }
                    }
                    
                    this.processEvent(evt);
                    this.currentEventIndex++;
                    
                    // Check for battle end - stop processing events and call finishBattle after delay
                    if (type === 'Victory' || type === 'Draw') {
                        this.isPlaying = false; // Stop processing more events
                        // Delay finishBattle to allow victory animation to show
                        setTimeout(() => this.finishBattle(), 2000 / this.battleSpeed);
                        break;
                    }
                }
                
                // Update speed bars visual
                this.drawSpeedBars();
            }

            // Replay mode
            if (this.mode === 'replay' && this.isPlaying) {
                this.replayAccumulator += deltaMs * this.playbackSpeed;
                if (this.replayAccumulator >= 1000) {
                    this.replayAccumulator = 0;
                    this.replayIndex += 1;
                    if (this.replayIndex < this.eventsList.length) {
                        this.processEvent(this.eventsList[this.replayIndex]);
                        this.updateCharacterStates(this.replayIndex);
                    }
                }
            }
        }

        _getPooledText(text, style) {
            let t;
            if (this._textPool.length > 0) {
                t = this._textPool.pop();
                t.text = text;
                t.style = style;
            } else {
                t = new PIXI.Text({ text, style });
            }
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
            // Adjust animation duration based on battle speed
            const adjustedDuration = duration / this.battleSpeed;

            const startProps = {};
            Object.keys(properties).forEach(key => {
                startProps[key] = target[key] ?? (key === 'alpha' ? 1 : 0);
            });

            const startTime = Date.now();
            const animate = () => {
                // Guard against destroyed or null targets
                if (!target || target.destroyed) {
                    if (onComplete) onComplete();
                    return;
                }
                
                const elapsed = Date.now() - startTime;
                const progress = Math.min(elapsed / adjustedDuration, 1);
                
                Object.keys(properties).forEach(key => {
                    const start = startProps[key];
                    const end = properties[key];
                    if (key === 'scale') {
                        target.scale.set(start + (end - start) * progress);
                    } else {
                        target[key] = start + (end - start) * progress;
                    }
                });

                if (progress < 1) {
                    requestAnimationFrame(animate);
                } else if (onComplete) {
                    onComplete();
                }
            };
            animate();
        }

        fadeOut(target, duration, onComplete) {
            this.animateTo(target, { alpha: 0 }, duration, onComplete);
        }

        destroy() {
            // Remove visibility/context-loss listeners
            if (this._onContextLost && this.app?.canvas) {
                this.app.canvas.removeEventListener('webglcontextlost', this._onContextLost);
            }
            if (this._onVisibilityChange) {
                document.removeEventListener('visibilitychange', this._onVisibilityChange);
            }

            // Clear all pending timers
            for (const id of this._timeoutIds) clearTimeout(id);
            for (const id of this._rafIds) cancelAnimationFrame(id);
            this._timeoutIds = [];
            this._rafIds = [];

            // Destroy pooled texts
            for (const t of this._textPool) { try { t.destroy(); } catch (_) {} }
            this._textPool = [];

            // Clean up interactive mode references
            this.spellBarContainer = null;
            this.spellButtons = [];

            if (this.app) {
                // Stop ticker before destroying
                this.app.ticker.stop();
                
                // Clear stage children manually to avoid null reference issues
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
                
                // Now destroy the app
                try {
                    this.app.destroy(false);
                } catch (e) {
                    console.warn('Error destroying PixiJS app:', e);
                }
                this.app = null;
                this.stage = null;
            }
        }
    }

    const createGame = (hostId, battleData, mode) => {
        const container = document.getElementById(hostId);
        if (!container) return null;

        const events = resolveEvents(battleData);
        const dotNetRef = resolveDotNetRef(battleData);
        const attackerName = resolveAttackerName(battleData);
        const defenderName = resolveDefenderName(battleData);
        const hasShotBuff = battleData?.HasShotBuff ?? battleData?.hasShotBuff ?? false;
        const interactiveMode = battleData?.InteractiveMode ?? battleData?.interactiveMode ?? false;
        const spells = battleData?.Spells ?? battleData?.spells ?? [];
        const playerHP = battleData?.PlayerHP ?? battleData?.playerHP ?? null;
        const playerMaxHP = battleData?.PlayerMaxHP ?? battleData?.playerMaxHP ?? null;
        const playerActionTime = battleData?.PlayerActionTime ?? battleData?.playerActionTime ?? null;
        const enemies = battleData?.Enemies ?? battleData?.enemies ?? [];

        return new BattleScene(container, {
            events,
            dotNetRef,
            mode,
            attackerName,
            defenderName,
            HasShotBuff: hasShotBuff,
            InteractiveMode: interactiveMode,
            Spells: spells,
            PlayerHP: playerHP,
            PlayerMaxHP: playerMaxHP,
            PlayerActionTime: playerActionTime,
            Enemies: enemies
        });
    };

    const stopBackgroundMusic = () => {
        if (arenaBackgroundMusic) {
            try {
                arenaBackgroundMusic.stop();
            } catch (e) {
                // Ignore if already stopped
            }
            arenaBackgroundMusic = null;
        }
        arenaBackgroundMusicGainNode = null;
    };

    const destroyBattle = () => {
        if (activeScene) {
            activeScene.destroy();
            activeScene = null;
        }
        // Stop background music when leaving arena
        stopBackgroundMusic();
    };

    window.myTunoGame = {
        startBattle: (hostId, battleData) => {
            destroyBattle();
            activeScene = createGame(hostId, battleData, 'live');
        },
        startReplay: (hostId, battleData) => {
            destroyBattle();
            activeScene = createGame(hostId, battleData, 'replay');
        },
        setReplayPlaying: (isPlaying) => {
            activeScene?.setReplayPlaying(isPlaying);
        },
        setReplaySpeed: (speed) => {
            activeScene?.setReplaySpeed(speed);
        },
        jumpToReplayEvent: (index) => {
            activeScene?.jumpToEvent(index);
        },
        setSpeed: (speed) => {
            if (activeScene) {
                activeScene.playbackSpeed = speed;
                activeScene.battleSpeed = speed;
            }
        },
        toggleAudio: () => {
            if (activeScene) {
                return activeScene.toggleAudio();
            }
            return false;
        },
        setVolume: (musicVol, sfxVol) => {
            activeScene?.setVolume(musicVol, sfxVol);
        },
        destroyBattle
    };
})();
