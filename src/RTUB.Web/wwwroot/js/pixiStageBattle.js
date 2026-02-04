/**
 * Stage Battle PixiJS Scene
 * Different layout from Arena - player at bottom, enemies at top
 * Supports multiple enemies, backgrounds, and region-specific sprites
 */
(function () {
    'use strict';

    const DEFAULT_WIDTH = 800;
    const DEFAULT_HEIGHT = 500;
    const DEFAULT_EVENT_INTERVAL = 600;

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
            miniBoss: '/sprites/games/my-tuno/enemies/forest/wolf.png',
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
            
            this.stageNumber = data?.stageNumber ?? 1;
            this.enemyType = data?.enemyType ?? 'normal';
            this.enemyCount = data?.enemyCount ?? 1;
            this.playerName = data?.playerName ?? 'Player';
            this.enemyName = data?.enemyName ?? 'Enemy';
            this.backgroundPath = data?.backgroundPath ?? defaultSprites.background;
            this.playerSpritePath = data?.playerSpritePath ?? defaultSprites.player;
            
            if (data?.enemySprites && Array.isArray(data.enemySprites)) {
                this.enemySpritePaths = data.enemySprites;
            } else {
                const singlePath = data?.enemySpritePath ?? defaultSprites.enemies[this.enemyType] ?? defaultSprites.enemies.normal;
                this.enemySpritePaths = Array(this.enemyCount).fill(singlePath);
            }
            
            this.enemyHPs = Array(this.enemyCount).fill(null).map(() => ({ current: 100, max: 100 }));
            
            // Use global audio state to persist settings between stages
            this.audioEnabled = globalAudioEnabled;
            this.sfxVolume = globalSfxVolume;
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
            const assets = [
                { alias: 'stageBg', src: this.backgroundPath },
                { alias: 'stagePlayer', src: this.playerSpritePath }
            ];

            if (this.enemySpritePaths && Array.isArray(this.enemySpritePaths)) {
                for (let i = 0; i < this.enemySpritePaths.length; i++) {
                    assets.push({ alias: `stageEnemy${i}`, src: this.enemySpritePaths[i] });
                }
            }

            await PIXI.Assets.load(assets);
        }

        create() {
            stageScene = this;

            const width = this.app.screen.width;
            const height = this.app.screen.height;

            this.backgroundSprite = PIXI.Sprite.from('stageBg');
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

            this.isPlaying = true;
            this.eventTimer = setInterval(() => this.processNextEvent(), this.eventInterval / this.playbackSpeed);
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
        }

        processInitialHPEvent(evt) {
            const character = getEventField(evt, 'Character');
            const hp = getEventField(evt, 'HP');
            const maxHP = getEventField(evt, 'MaxHP');

            if (character === 'Attacker' || character === 'Player') {
                this.playerMaxHp = maxHP;
                this.playerCurrentHp = hp;
                if (this.playerHpBar) {
                    const ratio = Math.max(0, hp / maxHP);
                    this.playerHpBar.bar.width = this.playerHpBar.maxWidth * ratio;
                }
                if (this.playerHpBar?.text) {
                    this.playerHpBar.text.text = `${hp}/${maxHP}`;
                }
            } else if (character.startsWith('Enemy')) {
                const enemyIndex = parseInt(character.replace('Enemy', ''));
                if (!isNaN(enemyIndex) && enemyIndex >= 0 && enemyIndex < this.enemyHPs.length) {
                    this.enemyHPs[enemyIndex].max = maxHP;
                    this.enemyHPs[enemyIndex].current = hp;
                    const hpBarData = this.enemyHpBars[enemyIndex];
                    if (hpBarData && hpBarData.text) {
                        hpBarData.bar.width = hpBarData.maxWidth;
                        hpBarData.text.text = `${hp}/${maxHP}`;
                        hpBarData.text.visible = true;
                    }
                }
            } else if (character === 'Defender' && this.enemyCount === 1) {
                const enemyIndex = 0;
                this.enemyHPs[enemyIndex].max = maxHP;
                this.enemyHPs[enemyIndex].current = hp;
                const hpBarData = this.enemyHpBars[enemyIndex];
                if (hpBarData && hpBarData.text) {
                    hpBarData.bar.width = hpBarData.maxWidth;
                    hpBarData.text.text = `${hp}/${maxHP}`;
                    hpBarData.text.visible = true;
                }
            }
        }

        createPlayer(width, height) {
            const groundOffset = 60;
            const playerX = width * 0.25;
            const playerY = height - groundOffset;
            
            this.playerSprite = PIXI.Sprite.from('stagePlayer');
            this.playerSprite.anchor.set(0.5, 1);
            this.playerSprite.x = playerX;
            this.playerSprite.y = playerY;
            
            const maxSpriteHeight = height * 0.45;
            const scale = Math.min(1, maxSpriteHeight / this.playerSprite.height);
            this.playerSprite.scale.set(scale);
            
            this.stage.addChild(this.playerSprite);
            
            this.playerX = playerX;
            this.playerDisplayHeight = this.playerSprite.height * scale;
            
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
        }

        createEnemies(width, height) {
            this.enemySprites = [];
            this.enemyHpBars = [];
            
            const isMobile = width <= height || width < 500;
            const groundOffset = 60;
            const enemyX = width * 0.75;
            const baseEnemyY = height - groundOffset;
            
            const positions = this.calculateEnemyPositions(isMobile, this.enemyCount, enemyX, baseEnemyY, width, height);

            for (let i = 0; i < this.enemyCount; i++) {
                const pos = positions[i];
                
                const enemy = PIXI.Sprite.from(`stageEnemy${i}`);
                enemy.anchor.set(0.5, 1);
                enemy.x = pos.x;
                enemy.y = pos.y;
                
                const mobileScale = isMobile ? 0.22 : 0.45;
                const maxSpriteHeight = height * mobileScale;
                const scale = Math.min(1, maxSpriteHeight / enemy.height);
                enemy.scale.set(scale);
                
                this.stage.addChild(enemy);
                this.enemySprites.push(enemy);
                
                const hpBarY = pos.y - enemy.height * scale - 5;
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
            }
        }

        calculateEnemyPositions(isMobile, enemyCount, baseX, baseY, width, height) {
            const positions = [];
            
            if (enemyCount >= 4) {
                const hSpacing = isMobile ? 55 : 80;
                const vSpacing = isMobile ? 70 : 90;
                const topRowY = baseY - vSpacing;
                const bottomRowY = baseY;
                
                if (enemyCount === 4) {
                    positions.push({ x: baseX - hSpacing/2, y: topRowY });
                    positions.push({ x: baseX + hSpacing/2, y: topRowY });
                    positions.push({ x: baseX - hSpacing/2, y: bottomRowY });
                    positions.push({ x: baseX + hSpacing/2, y: bottomRowY });
                } else if (enemyCount === 5) {
                    const midRowY = baseY - vSpacing/2;
                    positions.push({ x: baseX - hSpacing/2, y: topRowY });
                    positions.push({ x: baseX + hSpacing/2, y: topRowY });
                    positions.push({ x: baseX, y: midRowY });
                    positions.push({ x: baseX - hSpacing/2, y: bottomRowY });
                    positions.push({ x: baseX + hSpacing/2, y: bottomRowY });
                } else {
                    const topCount = Math.ceil(enemyCount / 2);
                    const bottomCount = enemyCount - topCount;
                    
                    for (let i = 0; i < topCount; i++) {
                        const xOffset = (i - (topCount - 1) / 2) * hSpacing;
                        positions.push({ x: baseX + xOffset, y: topRowY });
                    }
                    for (let i = 0; i < bottomCount; i++) {
                        const xOffset = (i - (bottomCount - 1) / 2) * hSpacing;
                        positions.push({ x: baseX + xOffset, y: bottomRowY });
                    }
                }
            } else {
                const spacing = isMobile ? 50 : 80;
                const startX = baseX - ((enemyCount - 1) * spacing) / 2;
                
                for (let i = 0; i < enemyCount; i++) {
                    positions.push({ x: startX + i * spacing, y: baseY });
                }
            }
            
            return positions;
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
                if (this.enemyMaxHp === 100 && hp > 100) {
                    this.enemyMaxHp = hp;
                }
                this.enemyCurrentHp = hp;
                this.updateEnemyHPBar();
            }
        }

        updatePlayerHPBar() {
            const ratio = Math.max(0, this.playerCurrentHp / this.playerMaxHp);
            const maxWidth = this.playerHpBar.maxWidth || 200;
            
            this.animateTo(this.playerHpBar.bar, { width: maxWidth * ratio }, 200);
            this.playerHpBar.text.text = `${Math.max(0, this.playerCurrentHp)}/${this.playerMaxHp}`;
        }

        updateIndividualEnemyHPBar(enemyIndex) {
            if (!this.enemyHPs || !this.enemyHPs[enemyIndex]) return;
            
            const enemyHP = this.enemyHPs[enemyIndex];
            const ratio = Math.max(0, enemyHP.current / enemyHP.max);
            const hpBarData = this.enemyHpBars[enemyIndex];
            
            if (!hpBarData) return;
            
            const newWidth = hpBarData.maxWidth * ratio;
            this.animateTo(hpBarData.bar, { width: Math.max(0, newWidth) }, 200);
            hpBarData.text.text = `${Math.max(0, Math.round(enemyHP.current))}/${Math.round(enemyHP.max)}`;
        }

        updateEnemyHPBar() {
            const ratio = Math.max(0, this.enemyCurrentHp / this.enemyMaxHp);
            
            this.enemyHpBars.forEach(hpBarData => {
                const newWidth = hpBarData.maxWidth * ratio;
                this.animateTo(hpBarData.bar, { width: Math.max(0, newWidth) }, 200);
                hpBarData.text.text = `${Math.max(0, Math.round(this.enemyCurrentHp / this.enemyCount))}/${Math.round(this.enemyMaxHp / this.enemyCount)}`;
            });
        }

        handleAttack(evt) {
            const attacker = getEventField(evt, 'Attacker');
            const defender = getEventField(evt, 'Defender');
            const damage = getEventField(evt, 'Damage') ?? 0;
            const isCritical = getEventField(evt, 'IsCritical') ?? false;

            if (attacker === 'Attacker' || attacker === 'Player') {
                this.animatePlayerAttack();
                if (defender && defender.startsWith('Enemy')) {
                    const enemyIndex = parseInt(defender.replace('Enemy', ''));
                    this.flashEnemy(enemyIndex);
                } else {
                    this.flashEnemies();
                }
            } else if (attacker.startsWith('Enemy')) {
                const enemyIndex = parseInt(attacker.replace('Enemy', ''));
                this.animateSingleEnemyAttack(enemyIndex);
                this.flashPlayer();
            } else {
                this.animateEnemyAttack();
                this.flashPlayer();
            }

            this.playSound(isCritical ? 'critical' : 'attack');

            const attackerName = attacker === 'Attacker' || attacker === 'Player' ? this.playerName : this.enemyName;
            const critText = isCritical ? ' (CRIT!)' : '';
            this.addLogEntry(`${attackerName}: ${damage} dmg${critText}`);
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
                }, index * 50);
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
            }, 100);
        }

        flashEnemy(enemyIndex) {
            if (enemyIndex >= 0 && enemyIndex < this.enemySprites.length) {
                const enemy = this.enemySprites[enemyIndex];
                if (enemy) {
                    enemy.tint = 0xff0000;
                    setTimeout(() => {
                        enemy.tint = 0xffffff;
                    }, 100);
                }
            }
        }

        flashEnemies() {
            this.enemySprites.forEach(enemy => {
                enemy.tint = 0xff0000;
                setTimeout(() => {
                    enemy.tint = 0xffffff;
                }, 100);
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
                    
                    const hpBarData = this.enemyHpBars[enemyIndex];
                    if (hpBarData) {
                        this.animateTo(hpBarData.bar, { alpha: 0 }, 300);
                        this.animateTo(hpBarData.barBg, { alpha: 0 }, 300);
                        this.animateTo(hpBarData.text, { alpha: 0 }, 300);
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
            const isPlayerWin = winner === 'Attacker';
            
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

            this.finishBattle();
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

            this.finishBattle();
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
            }
        }

        setSpeed(speed) {
            this.playbackSpeed = speed;
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
                const elapsed = Date.now() - startTime;
                const progress = Math.min(elapsed / duration, 1);
                
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
                enemySprites: enemySprites
            });
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
                console.warn('No active game, using start() instead');
                this.start('phaserBattleContainer', battleData);
                return;
            }

            console.log('Starting next stage battle');
            // Use destroySceneOnly to keep music playing between stages
            this.destroySceneOnly();
            this.start('phaserBattleContainer', battleData);
        }
    };
})();
