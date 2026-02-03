/**
 * Stage Battle Phaser3 Scene
 * Different layout from Arena - player at bottom, enemies at top
 * Supports multiple enemies, backgrounds, and region-specific sprites
 */
(function () {
    'use strict';

    const DEFAULT_WIDTH = 800;
    const DEFAULT_HEIGHT = 500;
    const DEFAULT_EVENT_INTERVAL = 600;

    let stageGame = null;
    let stageScene = null;

    // Default sprite paths
    const defaultSprites = {
        player: '/sprites/games/my-tuno/default_tuno.png',
        background: '/sprites/games/my-tuno/backgrounds/forest.png',
        enemies: {
            normal: '/sprites/games/my-tuno/enemies/wolf.png',
            miniBoss: '/sprites/games/my-tuno/enemies/wolf.png',
            boss: '/sprites/games/my-tuno/enemies/boss_bear.png'
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

    class StageBattleScene extends Phaser.Scene {
        constructor() {
            super({ key: 'StageBattleScene' });
            this.eventsList = [];
            this.dotNetRef = null;
            this.eventInterval = DEFAULT_EVENT_INTERVAL;
            this.currentEventIndex = 0;
            
            // HP tracking
            this.playerMaxHp = 100;
            this.playerCurrentHp = 100;
            this.enemyMaxHp = 100;
            this.enemyCurrentHp = 100;
            this.enemyHPs = []; // Individual enemy HP tracking for multi-enemy battles
            this.isInitialSetup = true; // Flag to skip animations for initial HP setup
            
            // Sprites
            this.playerSprite = null;
            this.enemySprites = [];
            this.backgroundSprite = null;
            
            // UI elements - per-enemy HP bars
            this.playerHpBar = null;
            this.enemyHpBars = []; // Array of {bar, barBg, text} for each enemy
            this.stageText = null;
            this.logEntries = [];
            this.logText = null;
            
            // Battle state
            this.isPlaying = false;
            this.playbackSpeed = 1;
            this.battleFinished = false;
            
            // Config
            this.stageNumber = 1;
            this.enemyType = 'normal';
            this.enemyCount = 1;
            this.playerName = 'Player';
            this.enemyName = 'Enemy';
            
            // Audio
            this.audioEnabled = true;
            this.sfxVolume = 0.5;
            this.audioContext = null;
        }

        init(data) {
            this.eventsList = data?.events ?? [];
            this.dotNetRef = data?.dotNetRef ?? null;
            this.eventInterval = data?.eventInterval ?? DEFAULT_EVENT_INTERVAL;
            this.stageNumber = data?.stageNumber ?? 1;
            this.enemyType = data?.enemyType ?? 'normal';
            this.enemyCount = data?.enemyCount ?? 1;
            this.playerName = data?.playerName ?? 'Player';
            this.enemyName = data?.enemyName ?? 'Enemy';
            this.backgroundPath = data?.backgroundPath ?? defaultSprites.background;
            this.playerSpritePath = data?.playerSpritePath ?? defaultSprites.player;
            
            // Support both array of sprites (new) and single sprite path (legacy)
            if (data?.enemySprites && Array.isArray(data.enemySprites)) {
                this.enemySpritePaths = data.enemySprites;
            } else {
                const singlePath = data?.enemySpritePath ?? defaultSprites.enemies[this.enemyType] ?? defaultSprites.enemies.normal;
                this.enemySpritePaths = Array(this.enemyCount).fill(singlePath);
            }
            
            // Initialize individual enemy HP tracking
            this.enemyHPs = Array(this.enemyCount).fill(null).map(() => ({ current: 100, max: 100 }));
            
            this.currentEventIndex = 0;
            this.isPlaying = true;
            this.playbackSpeed = 1;
            this.battleFinished = false;
            this.logEntries = [];
            this.isInitialSetup = true; // Will be set to false after preprocessing
            
            this.setupAudio();
        }

        setupAudio() {
            try {
                if (typeof AudioContext !== 'undefined') {
                    this.audioContext = new AudioContext();
                } else if (typeof webkitAudioContext !== 'undefined') {
                    this.audioContext = new webkitAudioContext();
                }
            } catch (e) {
                console.warn('Audio not supported:', e);
                this.audioEnabled = false;
            }
        }

        preload() {
            // Load background
            this.load.image('stageBg', this.backgroundPath);
            
            // Load player sprite
            this.load.image('stagePlayer', this.playerSpritePath);
            
            // Load individual enemy sprites for each enemy
            if (this.enemySpritePaths && Array.isArray(this.enemySpritePaths)) {
                for (let i = 0; i < this.enemySpritePaths.length; i++) {
                    this.load.image(`stageEnemy${i}`, this.enemySpritePaths[i]);
                }
            }
        }

        create() {
            const width = this.cameras.main.width;
            const height = this.cameras.main.height;

            // Background - stretch to fill
            this.backgroundSprite = this.add.image(width / 2, height / 2, 'stageBg');
            this.backgroundSprite.setDisplaySize(width, height);
            
            // Semi-transparent overlay for better visibility
            const overlay = this.add.rectangle(width / 2, height / 2, width, height, 0x000000, 0.3);

            // Ground element like Arena
            const groundHeight = 50;
            this.add.rectangle(width / 2, height - groundHeight / 2, width, groundHeight, 0x2a2a2a);

            // Create player on LEFT side
            this.createPlayer(width, height);

            // Create enemies on RIGHT side (can have multiple)
            this.createEnemies(width, height);

            // Create HP bars
            this.createHPBars(width, height);

            // Create battle log (compact, bottom-left)
            this.createBattleLog(width, height);

            // PRE-PROCESS: Find first player attack and process initial events instantly
            this.preprocessInitialEvents();

            // Start processing events from where we left off
            this.time.addEvent({
                delay: this.eventInterval / this.playbackSpeed,
                callback: this.processNextEvent,
                callbackScope: this,
                loop: true
            });
        }

        preprocessInitialEvents() {
            // Find the index of the first player attack
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

            // Process only initial HPUpdate events (those with MaxHP) up to the first player attack
            for (let i = 0; i < this.eventsList.length && i < firstPlayerAttackIndex; i++) {
                const evt = this.eventsList[i];
                const evtType = getEventField(evt, 'Type');
                const maxHP = getEventField(evt, 'MaxHP');
                
                // Only process initial HP setup events (those with MaxHP)
                if (evtType === 'HPUpdate' && maxHP) {
                    this.processInitialHPEvent(evt);
                }
            }

            // Set current event index to the first player attack (or 0 if not found)
            this.currentEventIndex = firstPlayerAttackIndex >= 0 ? firstPlayerAttackIndex : 0;
            this.isInitialSetup = false; // Done with initial setup
        }

        processInitialHPEvent(evt) {
            const character = getEventField(evt, 'Character');
            const hp = getEventField(evt, 'HP');
            const maxHP = getEventField(evt, 'MaxHP');

            if (character === 'Attacker' || character === 'Player') {
                this.playerMaxHp = maxHP;
                this.playerCurrentHp = hp;
                // Set bar directly without animation - use correct width
                if (this.playerHpBar) {
                    const ratio = Math.max(0, hp / maxHP);
                    this.playerHpBar.width = this.playerHpBar.maxWidth * ratio;
                }
                if (this.playerHpText) {
                    this.playerHpText.setText(`${hp}/${maxHP}`);
                }
            } else if (character.startsWith('Enemy')) {
                // Multi-enemy format: "Enemy0", "Enemy1", etc.
                const enemyIndex = parseInt(character.replace('Enemy', ''));
                if (!isNaN(enemyIndex) && enemyIndex >= 0 && enemyIndex < this.enemyHPs.length) {
                    this.enemyHPs[enemyIndex].max = maxHP;
                    this.enemyHPs[enemyIndex].current = hp;
                    // Set bar directly without animation
                    const hpBarData = this.enemyHpBars[enemyIndex];
                    if (hpBarData && hpBarData.text) {
                        hpBarData.bar.width = hpBarData.maxWidth;
                        hpBarData.text.setText(`${hp}/${maxHP}`);
                        hpBarData.text.setVisible(true);
                    }
                }
            } else if (character === 'Defender' && this.enemyCount === 1) {
                // Legacy single-enemy format: "Defender"
                const enemyIndex = 0; // First (and only) enemy
                this.enemyHPs[enemyIndex].max = maxHP;
                this.enemyHPs[enemyIndex].current = hp;
                // Set bar directly without animation
                const hpBarData = this.enemyHpBars[enemyIndex];
                if (hpBarData && hpBarData.text) {
                    hpBarData.bar.width = hpBarData.maxWidth;
                    hpBarData.text.setText(`${hp}/${maxHP}`);
                    hpBarData.text.setVisible(true);
                }
            }
        }

        createEnemies(width, height) {
            this.enemySprites = [];
            this.enemyHpBars = [];
            
            // Detect mobile based on aspect ratio (mobile has narrower width relative to height)
            const isMobile = width <= height || width < 500;
            
            // Position enemies on the RIGHT side, grounded at bottom (matches Arena style)
            const groundOffset = 60;
            const enemyX = width * 0.75;
            const baseEnemyY = height - groundOffset;
            
            // Calculate enemy positions based on count and device
            const positions = this.calculateEnemyPositions(isMobile, this.enemyCount, enemyX, baseEnemyY, width, height);

            for (let i = 0; i < this.enemyCount; i++) {
                const pos = positions[i];
                
                // Use individual sprite for each enemy
                const enemy = this.add.image(pos.x, pos.y, `stageEnemy${i}`);
                enemy.setOrigin(0.5, 1); // Origin at bottom center like Arena
                
                // Scale enemy based on height - smaller on mobile for many enemies
                const mobileScale = isMobile ? 0.22 : 0.45;
                const maxSpriteHeight = height * mobileScale;
                const scale = Math.min(1, maxSpriteHeight / enemy.height);
                enemy.setScale(scale);
                
                this.enemySprites.push(enemy);
                
                // Create HP bar above this enemy (small, above sprite)
                const hpBarY = pos.y - enemy.displayHeight - 8;
                const hpBarWidth = isMobile ? 35 : 60;
                const hpBarHeight = isMobile ? 4 : 8;
                
                // HP bar background (dark)
                const barBg = this.add.rectangle(pos.x, hpBarY, hpBarWidth, hpBarHeight, 0x333333);
                barBg.setOrigin(0.5, 0.5);
                
                // HP bar fill (red)
                const bar = this.add.rectangle(pos.x - hpBarWidth/2, hpBarY, hpBarWidth, hpBarHeight, 0xff4444);
                bar.setOrigin(0, 0.5);
                
                // HP text (hidden on mobile with many enemies)
                const showText = !isMobile || this.enemyCount <= 2;
                const hpText = this.add.text(pos.x, hpBarY - 6, '', {
                    fontSize: isMobile ? '7px' : '10px',
                    fontFamily: 'Arial, sans-serif',
                    color: '#ffffff',
                    stroke: '#000000',
                    strokeThickness: 2
                }).setOrigin(0.5, 0.5);
                hpText.setVisible(showText);
                
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
                // Grid layout for 4+ enemies (both mobile and web)
                const hSpacing = isMobile ? 55 : 80;
                const vSpacing = isMobile ? 70 : 90;
                const topRowY = baseY - vSpacing;
                const bottomRowY = baseY;
                
                if (enemyCount === 4) {
                    // 2 top, 2 bottom
                    positions.push({ x: baseX - hSpacing/2, y: topRowY });
                    positions.push({ x: baseX + hSpacing/2, y: topRowY });
                    positions.push({ x: baseX - hSpacing/2, y: bottomRowY });
                    positions.push({ x: baseX + hSpacing/2, y: bottomRowY });
                } else if (enemyCount === 5) {
                    // 2 top, 1 middle, 2 bottom
                    const midRowY = baseY - vSpacing/2;
                    positions.push({ x: baseX - hSpacing/2, y: topRowY });
                    positions.push({ x: baseX + hSpacing/2, y: topRowY });
                    positions.push({ x: baseX, y: midRowY });
                    positions.push({ x: baseX - hSpacing/2, y: bottomRowY });
                    positions.push({ x: baseX + hSpacing/2, y: bottomRowY });
                } else {
                    // 6+ enemies: 3 top, rest bottom
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
                // 1-3 enemies: horizontal line
                const spacing = isMobile ? 50 : 80;
                const startX = baseX - ((enemyCount - 1) * spacing) / 2;
                
                for (let i = 0; i < enemyCount; i++) {
                    positions.push({ x: startX + i * spacing, y: baseY });
                }
            }
            
            return positions;
        }

        createPlayer(width, height) {
            // Player on LEFT side, grounded at bottom (matches Arena style)
            const groundOffset = 60;
            const playerX = width * 0.25;
            const playerY = height - groundOffset;
            this.playerSprite = this.add.image(playerX, playerY, 'stagePlayer');
            this.playerSprite.setOrigin(0.5, 1); // Origin at bottom center like Arena
            
            // Scale player based on height like Arena
            const maxSpriteHeight = height * 0.45;
            const scale = Math.min(1, maxSpriteHeight / this.playerSprite.height);
            this.playerSprite.setScale(scale);
            
            // Store player position for HP bar
            this.playerX = playerX;
            this.playerDisplayHeight = this.playerSprite.displayHeight;
        }

        createHPBars(width, height) {
            // Player HP bar - small, positioned above the player character
            const groundOffset = 60;
            const playerY = height - groundOffset;
            const hpBarY = playerY - this.playerDisplayHeight - 15;
            const barWidth = 60;
            const barHeight = 8;
            
            // Player HP bar background
            this.add.rectangle(this.playerX, hpBarY, barWidth, barHeight, 0x333333);
            
            // Player HP bar fill
            this.playerHpBar = this.add.rectangle(this.playerX - barWidth / 2, hpBarY, barWidth, barHeight, 0x44ff44);
            this.playerHpBar.setOrigin(0, 0.5);
            this.playerHpBar.maxWidth = barWidth;
            
            // Player HP text (small, above bar)
            this.playerHpText = this.add.text(this.playerX, hpBarY - 10, '100/100', {
                fontSize: '10px',
                fontFamily: 'Arial, sans-serif',
                fontStyle: 'bold',
                color: '#ffffff',
                stroke: '#000000',
                strokeThickness: 2
            }).setOrigin(0.5, 0.5);

            // Note: Enemy HP bars are created per-enemy in createEnemies()
        }

        createBattleLog(width, height) {
            // Battle log at BOTTOM (matches Arena style - inside ground area)
            const panelHeight = 50;
            const panelY = height - panelHeight / 2;
            
            this.add.rectangle(width / 2, panelY, width - 40, panelHeight, 0x0f0f0f, 0.9)
                .setOrigin(0.5, 0.5)
                .setStrokeStyle(1, 0x333333);
            
            this.logText = this.add.text(25, panelY - panelHeight / 2 + 8, '', {
                fontFamily: 'Arial',
                fontSize: '12px',
                color: '#f1f1f1',
                wordWrap: { width: width - 50 }
            });
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
                // Player HP
                if (maxHP && maxHP > this.playerMaxHp) {
                    this.playerMaxHp = maxHP;
                }
                this.playerCurrentHp = hp;
                this.updatePlayerHPBar();
            } else if (character.startsWith('Enemy')) {
                // Individual enemy HP (e.g., "Enemy0", "Enemy1")
                const enemyIndex = parseInt(character.replace('Enemy', ''));
                if (!isNaN(enemyIndex) && enemyIndex >= 0 && enemyIndex < this.enemyHpBars.length) {
                    // Update individual enemy HP
                    if (maxHP && maxHP > this.enemyHPs[enemyIndex].max) {
                        this.enemyHPs[enemyIndex].max = maxHP;
                    }
                    this.enemyHPs[enemyIndex].current = hp;
                    this.updateIndividualEnemyHPBar(enemyIndex);
                }
            } else {
                // Legacy: single enemy HP (backward compatibility)
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
            this.tweens.add({
                targets: this.playerHpBar,
                width: maxWidth * ratio,
                duration: 200,
                ease: 'Power2'
            });
            this.playerHpText.setText(`${Math.max(0, this.playerCurrentHp)}/${this.playerMaxHp}`);
        }

        updateIndividualEnemyHPBar(enemyIndex) {
            if (!this.enemyHPs || !this.enemyHPs[enemyIndex]) return;
            
            const enemyHP = this.enemyHPs[enemyIndex];
            const ratio = Math.max(0, enemyHP.current / enemyHP.max);
            const hpBarData = this.enemyHpBars[enemyIndex];
            
            if (!hpBarData) return;
            
            // Calculate new width based on ratio
            const newWidth = hpBarData.maxWidth * ratio;
            
            this.tweens.add({
                targets: hpBarData.bar,
                width: Math.max(0, newWidth),
                duration: 200,
                ease: 'Power2'
            });
            
            // Update HP text
            hpBarData.text.setText(`${Math.max(0, Math.round(enemyHP.current))}/${Math.round(enemyHP.max)}`);
        }

        updateEnemyHPBar() {
            const ratio = Math.max(0, this.enemyCurrentHp / this.enemyMaxHp);
            
            // Update all enemy HP bars (they share the same total HP for now)
            this.enemyHpBars.forEach(hpBarData => {
                // Calculate new width based on ratio
                const newWidth = hpBarData.maxWidth * ratio;
                
                this.tweens.add({
                    targets: hpBarData.bar,
                    width: Math.max(0, newWidth),
                    duration: 200,
                    ease: 'Power2'
                });
                
                // Update HP text
                hpBarData.text.setText(`${Math.max(0, Math.round(this.enemyCurrentHp / this.enemyCount))}/${Math.round(this.enemyMaxHp / this.enemyCount)}`);
            });
        }

        handleAttack(evt) {
            const attacker = getEventField(evt, 'Attacker');
            const defender = getEventField(evt, 'Defender');
            const damage = getEventField(evt, 'Damage') ?? 0;
            const isCritical = getEventField(evt, 'IsCritical') ?? false;

            if (attacker === 'Attacker' || attacker === 'Player') {
                // Player attacking a specific enemy
                this.animatePlayerAttack();
                // Flash only the targeted enemy
                if (defender && defender.startsWith('Enemy')) {
                    const enemyIndex = parseInt(defender.replace('Enemy', ''));
                    this.flashEnemy(enemyIndex);
                } else {
                    // Fallback: flash all enemies (legacy)
                    this.flashEnemies();
                }
            } else if (attacker.startsWith('Enemy')) {
                // Specific enemy attacking player
                const enemyIndex = parseInt(attacker.replace('Enemy', ''));
                this.animateSingleEnemyAttack(enemyIndex);
                this.flashPlayer();
            } else {
                // Legacy: enemy attacking player
                this.animateEnemyAttack();
                this.flashPlayer();
            }

            // Play sound
            this.playSound(isCritical ? 'critical' : 'attack');

            // Add to log
            const attackerName = attacker === 'Attacker' || attacker === 'Player' ? this.playerName : this.enemyName;
            const critText = isCritical ? ' (CRIT!)' : '';
            this.addLogEntry(`${attackerName}: ${damage} dmg${critText}`);
        }

        animatePlayerAttack() {
            if (!this.playerSprite) return;
            
            // Player moves RIGHT towards enemies (mirrors Arena's horizontal lunge)
            const originalX = this.playerSprite.x;
            this.tweens.add({
                targets: this.playerSprite,
                x: originalX + 60,
                duration: 150,
                ease: 'Power3',
                onComplete: () => {
                    // Return to original position
                    this.tweens.add({
                        targets: this.playerSprite,
                        x: originalX,
                        duration: 240,
                        ease: 'Back.Out'
                    });
                }
            });
        }

        animateEnemyAttack() {
            // Enemies move LEFT towards player (all of them - legacy)
            this.enemySprites.forEach((enemy, index) => {
                const originalX = enemy.x;
                this.tweens.add({
                    targets: enemy,
                    x: originalX - 60,
                    duration: 150,
                    delay: index * 50,
                    ease: 'Power3',
                    onComplete: () => {
                        this.tweens.add({
                            targets: enemy,
                            x: originalX,
                            duration: 240,
                            ease: 'Back.Out'
                        });
                    }
                });
            });
        }

        animateSingleEnemyAttack(enemyIndex) {
            // Only the attacking enemy moves LEFT towards player
            if (enemyIndex >= 0 && enemyIndex < this.enemySprites.length) {
                const enemy = this.enemySprites[enemyIndex];
                if (enemy) {
                    const originalX = enemy.x;
                    this.tweens.add({
                        targets: enemy,
                        x: originalX - 60,
                        duration: 150,
                        ease: 'Power3',
                        onComplete: () => {
                            this.tweens.add({
                                targets: enemy,
                                x: originalX,
                                duration: 240,
                                ease: 'Back.Out'
                            });
                        }
                    });
                }
            }
        }

        flashPlayer() {
            if (!this.playerSprite) return;
            this.playerSprite.setTint(0xff0000);
            this.time.delayedCall(100, () => {
                this.playerSprite.clearTint();
            });
        }

        flashEnemy(enemyIndex) {
            // Flash only the specific enemy that was hit
            if (enemyIndex >= 0 && enemyIndex < this.enemySprites.length) {
                const enemy = this.enemySprites[enemyIndex];
                if (enemy) {
                    enemy.setTint(0xff0000);
                    this.time.delayedCall(100, () => {
                        enemy.clearTint();
                    });
                }
            }
        }

        flashEnemies() {
            this.enemySprites.forEach(enemy => {
                enemy.setTint(0xff0000);
                this.time.delayedCall(100, () => {
                    enemy.clearTint();
                });
            });
        }

        handleKO(evt) {
            const character = getEventField(evt, 'Character');
            this.playSound('ko');

            if (character === 'Attacker' || character === 'Player') {
                // Player KO'd
                this.tweens.add({
                    targets: this.playerSprite,
                    alpha: 0.3,
                    angle: 90,
                    duration: 500
                });
                this.addLogEntry(`${this.playerName} defeated!`);
            } else if (character.startsWith('Enemy')) {
                // Individual enemy KO'd
                const enemyIndex = parseInt(character.replace('Enemy', ''));
                if (!isNaN(enemyIndex) && enemyIndex >= 0 && enemyIndex < this.enemySprites.length) {
                    const enemy = this.enemySprites[enemyIndex];
                    this.tweens.add({
                        targets: enemy,
                        alpha: 0,
                        y: enemy.y - 50,
                        duration: 500
                    });
                    this.addLogEntry(`Enemy ${enemyIndex + 1} defeated!`);
                    
                    // Hide the HP bar for this enemy
                    const hpBarData = this.enemyHpBars[enemyIndex];
                    if (hpBarData) {
                        this.tweens.add({
                            targets: [hpBarData.bar, hpBarData.barBg, hpBarData.text],
                            alpha: 0,
                            duration: 300
                        });
                    }
                }
            } else {
                // Legacy: all enemies KO'd at once
                this.enemySprites.forEach(enemy => {
                    this.tweens.add({
                        targets: enemy,
                        alpha: 0,
                        y: enemy.y - 50,
                        duration: 500
                    });
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
                
                // Victory animation for player
                this.tweens.add({
                    targets: this.playerSprite,
                    y: this.playerSprite.y - 20,
                    duration: 200,
                    yoyo: true,
                    repeat: 2
                });
            } else {
                this.playSound('defeat');
                this.addLogEntry('💀 DEFEAT');
            }

            // Display result text
            const resultText = this.add.text(
                this.cameras.main.width / 2,
                this.cameras.main.height / 2,
                isPlayerWin ? 'VICTORY!' : 'DEFEAT',
                {
                    fontSize: '48px',
                    fontFamily: 'Arial, sans-serif',
                    fontStyle: 'bold',
                    color: isPlayerWin ? '#44ff44' : '#ff4444',
                    stroke: '#000000',
                    strokeThickness: 6
                }
            ).setOrigin(0.5);

            this.tweens.add({
                targets: resultText,
                scale: { from: 0, to: 1 },
                duration: 500,
                ease: 'Back.easeOut'
            });

            this.finishBattle();
        }

        handleDraw() {
            this.addLogEntry('Draw!');
            
            const drawText = this.add.text(
                this.cameras.main.width / 2,
                this.cameras.main.height / 2,
                'DRAW',
                {
                    fontSize: '48px',
                    fontFamily: 'Arial, sans-serif',
                    fontStyle: 'bold',
                    color: '#ffaa00',
                    stroke: '#000000',
                    strokeThickness: 6
                }
            ).setOrigin(0.5);

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
            this.logEntries = this.logEntries.slice(0, 4); // Keep only last 4 entries (matches Arena)
            if (this.logText) {
                this.logText.setText(this.logEntries.join('\n'));
            }
        }

        finishBattle() {
            if (this.battleFinished) return;
            this.battleFinished = true;
            this.isPlaying = false;

            // Notify Blazor that battle is done
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
        }
    }

    // Public API
    window.stageBattleGame = {
        start: function (containerId, battleData) {
            console.log('Starting stage battle game in container:', containerId);
            
            const container = document.getElementById(containerId);
            if (!container) {
                console.error('Stage battle container not found:', containerId);
                return;
            }

            // Destroy existing game if any
            if (stageGame) {
                stageGame.destroy(true);
                stageGame = null;
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
            
            // Support both array of sprites (new) and single sprite path (legacy)
            const enemySprites = battleData?.enemySprites ?? battleData?.EnemySprites;

            const config = {
                type: Phaser.AUTO,
                width: container.clientWidth || DEFAULT_WIDTH,
                height: container.clientHeight || DEFAULT_HEIGHT,
                parent: containerId,
                backgroundColor: '#1a1a1a',
                scene: StageBattleScene
            };

            stageGame = new Phaser.Game(config);

            stageGame.events.once('ready', () => {
                stageScene = stageGame.scene.getScene('StageBattleScene');
                if (stageScene) {
                    stageScene.scene.restart({
                        events: events,
                        dotNetRef: dotNetRef,
                        stageNumber: stageNumber,
                        enemyType: enemyType,
                        enemyCount: enemyCount,
                        playerName: playerName,
                        enemyName: enemyName,
                        backgroundPath: backgroundPath,
                        playerSpritePath: playerSpritePath,
                        enemySprites: enemySprites // Pass array of sprite paths
                    });
                }
            });
        },

        destroy: function () {
            if (stageGame) {
                stageGame.destroy(true);
                stageGame = null;
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
        },

        // Start next battle without destroying/recreating the game
        nextBattle: function (battleData) {
            if (!stageGame || !stageScene) {
                console.warn('No active game, using start() instead');
                this.start('phaserBattleContainer', battleData);
                return;
            }

            console.log('Starting next stage battle in existing scene');
            
            const events = resolveEvents(battleData);
            const dotNetRef = battleData?.dotNetRef ?? battleData?.DotNetRef ?? null;
            const stageNumber = battleData?.stageNumber ?? battleData?.StageNumber ?? 1;
            const enemyType = battleData?.enemyType ?? battleData?.EnemyType ?? 'normal';
            const enemyCount = battleData?.enemyCount ?? battleData?.EnemyCount ?? 1;
            const playerName = battleData?.playerName ?? battleData?.PlayerName ?? 'Player';
            const enemyName = battleData?.enemyName ?? battleData?.EnemyName ?? 'Enemy';
            const backgroundPath = battleData?.backgroundPath ?? battleData?.BackgroundPath ?? defaultSprites.background;
            const playerSpritePath = battleData?.playerSpritePath ?? battleData?.PlayerSpritePath ?? defaultSprites.player;
            
            // Support both array of sprites (new) and single sprite path (legacy)
            const enemySprites = battleData?.enemySprites ?? battleData?.EnemySprites;

            // Restart the scene with new data (no need to reload assets if they're the same)
            stageScene.scene.restart({
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
        }
    };
})();
