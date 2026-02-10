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

    const defaultSprites = {
        player: '/sprites/games/my-tuno/default_tuno.png',
        background: '/sprites/games/my-tuno/backgrounds/forest.png',
        enemies: {
            normal: '/sprites/games/my-tuno/enemies/forest/wolf.png',
            boss: '/sprites/games/my-tuno/enemies/forest/boss_1_bear.png'
        }
    };

    const getEventField = (evt, field) => {
        if (!evt) return undefined;
        return evt[field] ?? evt[field.toLowerCase()];
    };

    const resolveEvents = (battleData) => {
        if (!battleData) return [];
        const eventsJson = battleData.EventsJson ?? battleData.eventsJson ?? battleData.eventsjson;
        if (eventsJson && typeof eventsJson === 'string') {
            try {
                return JSON.parse(eventsJson);
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
            this.logEntries = [];
            this.logText = null;
            
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
            
            console.log('StageBattleScene constructor - Stage:', this.stageNumber, 'PlacementsData:', placementsData, 'Set placements:', this.enemyPlacements);
            
            this.enemyHPs = Array(this.enemyCount).fill(null).map(() => ({ current: 100, max: 100 }));
            
            // Idle animation settings
            this.idleAnimationTime = 0;
            this.enemyIdleOffsets = []; // Store original Y positions for idle bob
            
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
            
            this.setupAudio();
            this.initPixi();
        }

        setupAudio() {
            try {
                if (typeof AudioContext !== 'undefined') {
                    this.audioContext = new AudioContext();
                } else if (typeof webkitAudioContext !== 'undefined') {
                    this.audioContext = new webkitAudioContext();
                }
                
                // Start background music if not already playing
                if (this.audioContext && !backgroundMusic) {
                    this.loadBackgroundMusic();
                }
            } catch (e) {
                console.warn('Audio not supported:', e);
                this.audioEnabled = false;
            }
        }
        
        async loadBackgroundMusic() {
            try {
                const response = await fetch('/sound/stage_battle.mp3');
                const arrayBuffer = await response.arrayBuffer();
                const audioBuffer = await this.audioContext.decodeAudioData(arrayBuffer);
                
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
            
            this.app = new PIXI.Application();
            await this.app.init({
                width: DEFAULT_WIDTH,
                height: DEFAULT_HEIGHT,
                backgroundColor: 0x1a1a1a,
                antialias: true
            });

            this.container.appendChild(this.app.canvas);
            this.stage = this.app.stage;

            await this.loadAssets();
            this.create();
        }

        async loadAssets() {
            // Use unique alias + cache busting to avoid stale textures
            const ts = Date.now();
            const cacheBust = `?v=${ts}`;
            this.bgAlias = `stageBg_${this.stageNumber}_${ts}`;
            const assets = [
                { alias: this.bgAlias, src: this.backgroundPath + cacheBust },
                { alias: `stagePlayer_${ts}`, src: this.playerSpritePath + cacheBust }
            ];
            this._playerAlias = `stagePlayer_${ts}`;

            // Store the actual paths for creating sprites later
            this.enemySpriteAliases = [];
            if (this.enemySpritePaths && Array.isArray(this.enemySpritePaths)) {
                for (let i = 0; i < this.enemySpritePaths.length; i++) {
                    // Use unique alias combining index and path to avoid caching issues
                    const alias = `stageEnemy${i}_${this.stageNumber}_${ts}`;
                    assets.push({ alias: alias, src: this.enemySpritePaths[i] + cacheBust });
                    this.enemySpriteAliases.push(alias);
                }
            }

            await PIXI.Assets.load(assets);
        }

        create() {
            stageScene = this;

            const width = this.app.screen.width;
            const height = this.app.screen.height;

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

            const groundHeight = 50;
            const ground = new PIXI.Graphics();
            ground.rect(0, height - groundHeight, width, groundHeight);
            ground.fill(0x2a2a2a);
            this.stage.addChild(ground);

            this.createPlayer(width, height);
            this.createEnemies(width, height);
            this.createBattleLog(width, height);

            this.preprocessInitialEvents();
            this.startTimedBattle();
            
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
            }
        }

        createPlayer(width, height) {
            const groundOffset = 60;
            const playerX = width * 0.25;
            const playerY = height - groundOffset;
            
            this.playerSprite = PIXI.Sprite.from(this._playerAlias || 'stagePlayer');
            this.playerSprite.anchor.set(0.5, 1);
            this.playerSprite.x = playerX;
            this.playerSprite.y = playerY;
            
            const maxSpriteHeight = height * 0.45;
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
            
            // Create HP bar above player sprite
            const barWidth = 60;
            const barHeight = 8;
            const hpBarY = playerY - this.playerDisplayHeight - 10;
            
            const playerHpBarBg = new PIXI.Graphics();
            playerHpBarBg.rect(playerX - barWidth / 2, hpBarY - barHeight / 2, barWidth, barHeight);
            playerHpBarBg.fill(0x333333);
            this.stage.addChild(playerHpBarBg);
            
            const playerHpBarFill = new PIXI.Graphics();
            playerHpBarFill.rect(0, 0, barWidth, barHeight);
            playerHpBarFill.fill(0x44ff44);
            playerHpBarFill.x = playerX - barWidth / 2;
            playerHpBarFill.y = hpBarY - barHeight / 2;
            this.stage.addChild(playerHpBarFill);
            
            const playerHpText = new PIXI.Text({
                text: '100/100',
                style: {
                    fontSize: 10,
                    fontFamily: 'Arial, sans-serif',
                    fontWeight: 'bold',
                    fill: 0xffffff,
                    stroke: { color: 0x000000, width: 2 }
                }
            });
            playerHpText.anchor.set(0.5);
            playerHpText.x = playerX;
            playerHpText.y = hpBarY - 10;
            this.stage.addChild(playerHpText);

            this.playerHpBar = {
                bar: playerHpBarFill,
                barBg: playerHpBarBg,
                text: playerHpText,
                maxWidth: barWidth
            };
            
            // Create Speed bar below HP bar (smaller)
            const speedBarHeight = 4;
            const speedBarY = hpBarY + barHeight / 2 + 3;
            
            const playerSpeedBarBg = new PIXI.Graphics();
            playerSpeedBarBg.rect(playerX - barWidth / 2, speedBarY, barWidth, speedBarHeight);
            playerSpeedBarBg.fill(0x222222);
            this.stage.addChild(playerSpeedBarBg);
            
            const playerSpeedBarFill = new PIXI.Graphics();
            playerSpeedBarFill.rect(0, 0, barWidth, speedBarHeight);
            playerSpeedBarFill.fill(0x00bcd4); // Cyan for speed
            playerSpeedBarFill.x = playerX - barWidth / 2;
            playerSpeedBarFill.y = speedBarY;
            this.stage.addChild(playerSpeedBarFill);
            
            this.playerSpeedBar = {
                bar: playerSpeedBarFill,
                barBg: playerSpeedBarBg,
                maxWidth: barWidth
            };

            // Store base position for idle bobbing animation
            this.playerIdleOffset = {
                baseX: playerX,
                baseY: playerY,
                phase: Math.PI, // Offset phase from enemies
                bobAmplitude: 3,
                swayAmplitude: 2
            };
        }

        createEnemies(width, height) {
            this.enemySprites = [];
            this.enemyHpBars = [];
            this.enemyIdleOffsets = []; // Store base positions for idle animation
            
            const isMobile = width <= height || width < 500;
            const groundOffset = 60;
            const enemyX = width * 0.72; // Shift left slightly to give more room
            const baseEnemyY = height - groundOffset;
            
            console.log('createEnemies - Stage:', this.stageNumber, 'Using placements:', this.enemyPlacements);
            
            // Pass placements to calculate positions
            const positions = this.calculateEnemyPositions(isMobile, this.enemyCount, enemyX, baseEnemyY, width, height, this.enemyPlacements);
            
            console.log('createEnemies - Calculated positions:', positions);

            // Determine scale factor based on enemy count
            // More enemies = smaller sprites to fit them all
            const isBoss = this.enemyType && this.enemyType.toLowerCase() === 'boss';
            let countScaleFactor = 1.0;
            if (this.enemyCount >= 6) {
                countScaleFactor = 0.55;
            } else if (this.enemyCount >= 5) {
                countScaleFactor = 0.65;
            } else if (this.enemyCount >= 4) {
                countScaleFactor = 0.85;
            } else if (this.enemyCount >= 3) {
                countScaleFactor = 0.92;
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
                
                // Use the stage-specific alias stored during loadAssets
                const alias = this.enemySpriteAliases && this.enemySpriteAliases[i] 
                    ? this.enemySpriteAliases[i] 
                    : `stageEnemy${i}_${this.stageNumber}`;
                const enemy = PIXI.Sprite.from(alias);
                enemy.anchor.set(0.5, 1);
                enemy.x = pos.x;
                enemy.y = pos.y;
                
                // Calculate base scale from height
                const mobileScale = isMobile ? 0.22 : 0.40;
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
            if (enemyCount >= 6) {
                hSpacing = isMobile ? 55 : 90;
                vSpacing = isMobile ? 70 : 90;
            } else if (enemyCount >= 5) {
                hSpacing = isMobile ? 80 : 130;
                vSpacing = isMobile ? 95 : 130;
            } else if (enemyCount >= 4) {
                hSpacing = isMobile ? 75 : 115;
                vSpacing = isMobile ? 90 : 115;
            } else {
                hSpacing = isMobile ? 90 : 140;
                vSpacing = isMobile ? 100 : 130;
            }
            
            const aerialOffset = isMobile ? 80 : 120; // How high aerial enemies fly
            
            // Calculate max X to keep enemies on screen (with some padding)
            const maxX = width - 40;
            const minX = width * 0.45; // Don't go past middle of screen
            
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
                const smallVOffset = isMobile ? 55 : 80;
                
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
            }
            
            // If 4 enemies of same type, use 2x2 grid
            if (enemyCount === 4 && (aerialIndices.length === 4 || terrestrialIndices.length === 4)) {
                const baseYForType = aerialIndices.length === 4 ? baseY - aerialOffset : baseY;
                const smallVOffset = isMobile ? 55 : 75;
                
                let centerX = baseX;
                if (centerX + hSpacing/2 > maxX) centerX = maxX - hSpacing/2;
                if (centerX - hSpacing/2 < minX) centerX = minX + hSpacing/2;
                
                tempPositions[0] = { x: centerX - hSpacing/2, y: baseYForType - smallVOffset, isAerial: aerialIndices.length === 4 };
                tempPositions[1] = { x: centerX + hSpacing/2, y: baseYForType - smallVOffset, isAerial: aerialIndices.length === 4 };
                tempPositions[2] = { x: centerX - hSpacing/2, y: baseYForType, isAerial: aerialIndices.length === 4 };
                tempPositions[3] = { x: centerX + hSpacing/2, y: baseYForType, isAerial: aerialIndices.length === 4 };
            }
            
            // If 3 enemies, triangle
            if (enemyCount === 3 && (aerialIndices.length === 3 || terrestrialIndices.length === 3)) {
                const baseYForType = aerialIndices.length === 3 ? baseY - aerialOffset : baseY;
                const smallVOffset = isMobile ? 55 : 75;
                
                let centerX = baseX;
                if (centerX + hSpacing/2 > maxX) centerX = maxX - hSpacing/2;
                if (centerX - hSpacing/2 < minX) centerX = minX + hSpacing/2;
                
                tempPositions[0] = { x: centerX, y: baseYForType - smallVOffset, isAerial: aerialIndices.length === 3 };
                tempPositions[1] = { x: centerX - hSpacing/2, y: baseYForType, isAerial: aerialIndices.length === 3 };
                tempPositions[2] = { x: centerX + hSpacing/2, y: baseYForType, isAerial: aerialIndices.length === 3 };
            }
            
            return tempPositions;
        }

        createBattleLog(width, height) {
            const panelHeight = 50;
            const panelY = height - panelHeight / 2;
            
            const panel = new PIXI.Graphics();
            panel.rect(20, panelY - panelHeight / 2, width - 40, panelHeight);
            panel.fill({ color: 0x0f0f0f, alpha: 0.9 });
            panel.stroke({ width: 1, color: 0x333333 });
            this.stage.addChild(panel);
            
            this.logText = new PIXI.Text({
                text: '',
                style: {
                    fontFamily: 'Arial',
                    fontSize: 12,
                    fill: 0xf1f1f1,
                    wordWrap: true,
                    wordWrapWidth: width - 50
                }
            });
            this.logText.x = 25;
            this.logText.y = panelY - panelHeight / 2 + 8;
            this.stage.addChild(this.logText);
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

        update() {
            const deltaMs = this.app.ticker.deltaMS;
            
            // Idle animation for enemies - always runs even during pauses
            this.updateIdleAnimation(deltaMs);
            
            // Time-based battle simulation
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
            }
        }

        updateIdleAnimation(deltaMs) {
            this.idleAnimationTime += deltaMs * 0.002; // Slow animation speed

            // Animate player with gentle bobbing
            if (this.playerSprite && !this.playerSprite.destroyed && this.playerIdleOffset && this.playerCurrentHp > 0) {
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
            const ratio = Math.max(0, this.playerSpeedBarTimer / (this.playerActionTime * 1000));
            const newWidth = this.playerSpeedBar.maxWidth * ratio;
            this.playerSpeedBar.bar.width = newWidth;
        }

        updateEnemySpeedBar(enemyIndex) {
            if (!this.enemySpeedBars[enemyIndex]) return;
            const actionTimeMs = this.enemyActionTimes[enemyIndex] * 1000;
            const ratio = Math.max(0, this.enemySpeedBarTimers[enemyIndex] / actionTimeMs);
            const newWidth = this.enemySpeedBars[enemyIndex].maxWidth * ratio;
            this.enemySpeedBars[enemyIndex].bar.width = newWidth;
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
                case 'RoundStart':
                    this.handleRoundStart(evt);
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
            const ratio = Math.max(0, this.playerCurrentHp / this.playerMaxHp);
            const maxWidth = this.playerHpBar.maxWidth || 200;
            
            this.animateTo(this.playerHpBar.bar, { width: maxWidth * ratio }, 200);
            this.playerHpBar.text.text = `${formatNum(Math.max(0, this.playerCurrentHp))}/${formatNum(this.playerMaxHp)}`;
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
        }

        updateEnemyHPBar() {
            const ratio = Math.max(0, this.enemyCurrentHp / this.enemyMaxHp);
            
            this.enemyHpBars.forEach(hpBarData => {
                const newWidth = hpBarData.maxWidth * ratio;
                this.animateTo(hpBarData.bar, { width: Math.max(0, newWidth) }, 200);
                hpBarData.text.text = `${formatNum(Math.max(0, Math.round(this.enemyCurrentHp / this.enemyCount)))}/${formatNum(Math.round(this.enemyMaxHp / this.enemyCount))}`;
            });
        }

        handleAttack(evt) {
            const attacker = getEventField(evt, 'Attacker');
            const defender = getEventField(evt, 'Defender');
            const damage = getEventField(evt, 'Damage') ?? 0;
            const isCritical = getEventField(evt, 'IsCritical') ?? false;
            const isBlocked = getEventField(evt, 'IsBlocked') ?? false;
            const isBoosted = getEventField(evt, 'IsBoosted') ?? false;

            if (attacker === 'Attacker' || attacker === 'Player') {
                this.animatePlayerAttack();
                if (defender && defender.startsWith('Enemy')) {
                    const enemyIndex = parseInt(defender.replace('Enemy', ''));
                    this.flashEnemy(enemyIndex);
                    if (isBoosted) {
                        const enemy = this.enemySprites[enemyIndex];
                        if (enemy) this.showFloatingText('EXTRA', enemy.x, enemy.y - (enemy.height || 40) * 0.8, 0xff9800);
                    }
                } else {
                    this.flashEnemies();
                    if (isBoosted && this.enemySprites.length > 0) {
                        const enemy = this.enemySprites[0];
                        if (enemy) this.showFloatingText('EXTRA', enemy.x, enemy.y - (enemy.height || 40) * 0.8, 0xff9800);
                    }
                }
            } else if (attacker.startsWith('Enemy')) {
                const enemyIndex = parseInt(attacker.replace('Enemy', ''));
                this.animateSingleEnemyAttack(enemyIndex);
                if (isBlocked) {
                    this.showFloatingText('BLOCKED', this.playerSprite.x, this.playerSprite.y - (this.playerSprite.height || 40) * 0.8, 0x00e5ff);
                } else {
                    this.flashPlayer();
                }
            } else {
                this.animateEnemyAttack();
                if (isBlocked) {
                    this.showFloatingText('BLOCKED', this.playerSprite.x, this.playerSprite.y - (this.playerSprite.height || 40) * 0.8, 0x00e5ff);
                } else {
                    this.flashPlayer();
                }
            }

            this.playSound(isBlocked ? 'block' : (isCritical ? 'critical' : 'attack'));

            const attackerName = attacker === 'Attacker' || attacker === 'Player' ? this.playerName : this.enemyName;
            const critText = isCritical ? ' (CRIT!)' : '';
            const blockedText = isBlocked ? ' BLOCKED' : '';
            const boostedText = isBoosted ? ' EXTRA' : '';
            this.addLogEntry(`${attackerName}: ${formatNum(damage)} dmg${critText}${blockedText}${boostedText}`);
        }

        showFloatingText(text, x, y, color) {
            if (!this.stage) return;
            const floatText = new PIXI.Text({
                text: text,
                style: {
                    fontFamily: 'Arial',
                    fontSize: 26,
                    fontWeight: 'bold',
                    fill: color,
                    stroke: { color: 0x000000, width: 4 }
                }
            });
            floatText.anchor.set(0.5);
            floatText.x = x;
            floatText.y = y;
            this.stage.addChild(floatText);

            this.animateTo(floatText, {
                y: floatText.y - 70,
                alpha: 0
            }, 900, () => {
                this.stage.removeChild(floatText);
            });
        }

        animatePlayerAttack() {
            if (!this.playerSprite) return;
            
            const originalX = this.playerSprite.x;
            this.animateTo(this.playerSprite, { x: originalX + 60 }, 150, () => {
                this.animateTo(this.playerSprite, { x: originalX }, 240);
            });
        }

        animateEnemyAttack() {
            this.enemySprites.forEach((enemy, index) => {
                const originalX = enemy.x;
                setTimeout(() => {
                    this.animateTo(enemy, { x: originalX - 60 }, 150, () => {
                        this.animateTo(enemy, { x: originalX }, 240);
                    });
                }, (index * 50) / this.battleSpeed);
            });
        }

        animateSingleEnemyAttack(enemyIndex) {
            if (enemyIndex >= 0 && enemyIndex < this.enemySprites.length) {
                const enemy = this.enemySprites[enemyIndex];
                if (enemy) {
                    const originalX = enemy.x;
                    this.animateTo(enemy, { x: originalX - 60 }, 150, () => {
                        this.animateTo(enemy, { x: originalX }, 240);
                    });
                }
            }
        }

        flashPlayer() {
            if (!this.playerSprite) return;
            this.playerSprite.tint = 0xff0000;
            setTimeout(() => {
                this.playerSprite.tint = 0xffffff;
            }, 100 / this.battleSpeed);
        }

        flashEnemy(enemyIndex) {
            if (enemyIndex >= 0 && enemyIndex < this.enemySprites.length) {
                const enemy = this.enemySprites[enemyIndex];
                if (enemy) {
                    enemy.tint = 0xff0000;
                    setTimeout(() => {
                        enemy.tint = 0xffffff;
                    }, 100 / this.battleSpeed);
                }
            }
        }

        flashEnemies() {
            this.enemySprites.forEach(enemy => {
                enemy.tint = 0xff0000;
                setTimeout(() => {
                    enemy.tint = 0xffffff;
                }, 100 / this.battleSpeed);
            });
        }

        handleKO(evt) {
            const character = getEventField(evt, 'Character');
            this.playSound('ko');

            if (character === 'Attacker' || character === 'Player') {
                this.animateTo(this.playerSprite, { alpha: 0.3, rotation: Math.PI / 2 }, 500);
                this.addLogEntry(`${this.playerName} defeated!`);
            } else if (character.startsWith('Enemy')) {
                const enemyIndex = parseInt(character.replace('Enemy', ''));
                if (!isNaN(enemyIndex) && enemyIndex >= 0 && enemyIndex < this.enemySprites.length) {
                    const enemy = this.enemySprites[enemyIndex];
                    this.animateTo(enemy, { alpha: 0, y: enemy.y - 50 }, 500);
                    this.addLogEntry(`Enemy ${enemyIndex + 1} defeated!`);
                    
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
                }
            } else {
                this.enemySprites.forEach(enemy => {
                    this.animateTo(enemy, { alpha: 0, y: enemy.y - 50 }, 500);
                });
                this.addLogEntry(`${this.enemyName} defeated!`);
            }
        }

        handleVictory(evt) {
            const winner = getEventField(evt, 'Winner');
            // Check for both 'Attacker' (1v1 battles) and 'Player' (multi-enemy battles)
            const isPlayerWin = winner === 'Attacker' || winner === 'Player';
            
            if (isPlayerWin) {
                this.playSound('victory');
                this.addLogEntry('🎉 VICTORY!');
                
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
                this.addLogEntry('💀 DEFEAT');
            }

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

            // Delay finishBattle to allow victory animation to show
            setTimeout(() => this.finishBattle(), 800 / this.battleSpeed);
        }

        handleDraw() {
            this.addLogEntry('Draw!');
            
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

        handleRoundStart(evt) {
            const round = getEventField(evt, 'Round');
            if (round) {
                this.addLogEntry(`--- Round ${round} ---`);
            }
        }

        addLogEntry(text) {
            if (!text) return;
            this.logEntries.unshift(text);
            this.logEntries = this.logEntries.slice(0, 4);
            if (this.logText) {
                this.logText.text = this.logEntries.join('\n');
            }
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
                    this.dotNetRef.invokeMethodAsync('OnBattleFinished');
                } catch (e) {
                    console.warn('Could not notify Blazor:', e);
                }
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
            this.playbackSpeed = speed;
            // Convert playback speed to battle speed (1x = 1.0, 2x = 2.0, 3x = 3.0)
            this.battleSpeed = speed;
        }

        setAudioEnabled(enabled) {
            this.audioEnabled = enabled;
            globalAudioEnabled = enabled; // Sync with global state
            
            // Control background music
            if (backgroundMusicGainNode) {
                backgroundMusicGainNode.gain.value = enabled ? 0.3 : 0;
            }
        }

        animateTo(target, properties, duration, onComplete) {
            // Adjust animation duration based on battle speed
            const adjustedDuration = duration / this.battleSpeed;
            
            const startProps = {};
            Object.keys(properties).forEach(key => {
                if (key === 'scale') {
                    startProps[key] = target.scale.x;
                } else if (key === 'width' || key === 'height') {
                    startProps[key] = target[key];
                } else {
                    startProps[key] = target[key] ?? (key === 'alpha' ? 1 : 0);
                }
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

        destroy() {
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
        
        // Reset scene for next battle without destroying the app - much faster!
        async resetForNextBattle(data) {
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
            
            console.log('resetForNextBattle - Stage:', this.stageNumber, 'Received placements:', data?.enemyPlacements, 'Set placements:', this.enemyPlacements);
            
            // Reset battle state
            this.currentEventIndex = 0;
            this.battleFinished = false;
            this.logEntries = [];
            this.playerMaxHp = 100;
            this.playerCurrentHp = 100;
            this.enemyHPs = Array(this.enemyCount).fill(null).map(() => ({ current: 100, max: 100 }));
            this.idleAnimationTime = 0;
            this.enemyIdleOffsets = [];
            
            // Clear all sprites from stage except background
            const childrenToRemove = [];
            for (let i = this.stage.children.length - 1; i >= 0; i--) {
                const child = this.stage.children[i];
                if (child !== this.backgroundSprite) {
                    childrenToRemove.push(child);
                }
            }
            childrenToRemove.forEach(child => {
                this.stage.removeChild(child);
                if (child.destroy) {
                    try {
                        child.destroy({ children: true, texture: false, baseTexture: false });
                    } catch (e) {}
                }
            });
            
            // Reset arrays
            this.enemySprites = [];
            this.enemyHpBars = [];
            this.enemySpeedBars = [];
            this.enemySpeedBarTimers = [];
            this.enemyActionTimes = [];
            this.playerSprite = null;
            this.playerIdleOffset = null;
            
            // Load new enemy textures
            await this.loadAssets();
            
            // Update background if it changed
            if (this.backgroundSprite) {
                const newBgTexture = PIXI.Assets.get(this.bgAlias);
                if (newBgTexture && this.backgroundSprite.texture !== newBgTexture) {
                    this.backgroundSprite.texture = newBgTexture;
                }
            }
            
            // Rebuild scene
            const width = this.app.screen.width;
            const height = this.app.screen.height;
            
            this.createPlayer(width, height);
            this.createEnemies(width, height);
            this.createBattleLog(width, height);
            
            // Restart battle
            this.preprocessInitialEvents();
            this.startTimedBattle();
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
        }
    }

    window.stageBattleGame = {
        start: function (containerId, battleData) {
            console.log('Starting stage battle game in container:', containerId);
            
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
                HasShotBuff: hasShotBuff
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
            if (!stageScene) {
                console.warn('No active scene, using start() instead');
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
                HasShotBuff: hasShotBuff
            });
        }
    };
})();
