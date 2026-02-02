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
            this.enemySpritePath = data?.enemySpritePath ?? defaultSprites.enemies[this.enemyType] ?? defaultSprites.enemies.normal;
            
            this.currentEventIndex = 0;
            this.isPlaying = true;
            this.playbackSpeed = 1;
            this.battleFinished = false;
            this.logEntries = [];
            
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
            
            // Load enemy sprite
            this.load.image('stageEnemy', this.enemySpritePath);
        }

        create() {
            const width = this.cameras.main.width;
            const height = this.cameras.main.height;

            // Background - stretch to fill
            this.backgroundSprite = this.add.image(width / 2, height / 2, 'stageBg');
            this.backgroundSprite.setDisplaySize(width, height);
            
            // Semi-transparent overlay for better visibility
            const overlay = this.add.rectangle(width / 2, height / 2, width, height, 0x000000, 0.3);

            // Stage number display
            this.stageText = this.add.text(width / 2, 20, `Stage ${this.stageNumber}`, {
                fontSize: '24px',
                fontFamily: 'Arial, sans-serif',
                fontStyle: 'bold',
                color: '#ffffff',
                stroke: '#000000',
                strokeThickness: 3
            }).setOrigin(0.5, 0);

            // Enemy type badge
            const enemyTypeText = this.enemyType === 'boss' ? '⚔️ BOSS' : 
                                  this.enemyType === 'miniBoss' ? '🛡️ Mini-Boss' : '';
            if (enemyTypeText) {
                this.add.text(width / 2, 50, enemyTypeText, {
                    fontSize: '18px',
                    fontFamily: 'Arial, sans-serif',
                    fontStyle: 'bold',
                    color: this.enemyType === 'boss' ? '#ff4444' : '#ffaa00',
                    stroke: '#000000',
                    strokeThickness: 2
                }).setOrigin(0.5, 0);
            }

            // Create enemies at top area (can have multiple)
            this.createEnemies(width, height);

            // Create player at bottom center
            this.createPlayer(width, height);

            // Create HP bars
            this.createHPBars(width, height);

            // Create battle log
            this.createBattleLog(width, height);

            // Start processing events
            this.time.addEvent({
                delay: this.eventInterval / this.playbackSpeed,
                callback: this.processNextEvent,
                callbackScope: this,
                loop: true
            });
        }

        createEnemies(width, height) {
            this.enemySprites = [];
            this.enemyHpBars = [];
            
            // Position enemies in a row at the top
            const enemyY = 160; // Moved down slightly to make room for HP bars
            const enemySpacing = Math.min(150, (width - 100) / Math.max(this.enemyCount, 1));
            const startX = width / 2 - ((this.enemyCount - 1) * enemySpacing) / 2;

            for (let i = 0; i < this.enemyCount; i++) {
                const enemyX = startX + i * enemySpacing;
                const enemy = this.add.image(enemyX, enemyY, 'stageEnemy');
                
                // Scale enemy appropriately based on type
                const enemySizes = { boss: 120, miniBoss: 100, normal: 80 };
                const maxSize = enemySizes[this.enemyType] || enemySizes.normal;
                const scale = maxSize / Math.max(enemy.width, enemy.height);
                enemy.setScale(scale);
                
                // Add slight variation for multiple enemies
                if (this.enemyCount > 1) {
                    enemy.setTint(Phaser.Display.Color.HSLToColor(0.1 * i, 0.8, 0.6).color);
                }
                
                this.enemySprites.push(enemy);
                
                // Create HP bar above this enemy
                const scaledHeight = enemy.height * scale;
                const hpBarY = enemyY - scaledHeight / 2 - 25; // Above enemy sprite
                const hpBarWidth = 60;
                const hpBarHeight = 8;
                
                // HP bar background (dark)
                const barBg = this.add.rectangle(enemyX, hpBarY, hpBarWidth, hpBarHeight, 0x333333);
                barBg.setOrigin(0.5, 0.5);
                
                // HP bar fill (red)
                const bar = this.add.rectangle(enemyX - hpBarWidth/2, hpBarY, hpBarWidth, hpBarHeight, 0xff4444);
                bar.setOrigin(0, 0.5);
                
                // HP text (smaller, above bar)
                const hpText = this.add.text(enemyX, hpBarY - 10, '', {
                    fontSize: '10px',
                    fontFamily: 'Arial, sans-serif',
                    color: '#ffffff',
                    stroke: '#000000',
                    strokeThickness: 2
                }).setOrigin(0.5, 0.5);
                
                this.enemyHpBars.push({ bar, barBg, text: hpText, maxWidth: hpBarWidth });
            }
        }

        createPlayer(width, height) {
            // Player at bottom center
            const playerY = height - 120;
            this.playerSprite = this.add.image(width / 2, playerY, 'stagePlayer');
            
            // Scale player
            const maxSize = 100;
            const scale = maxSize / Math.max(this.playerSprite.width, this.playerSprite.height);
            this.playerSprite.setScale(scale);
        }

        createHPBars(width, height) {
            // Player HP bar at bottom
            const playerBarY = height - 40;
            this.add.text(20, playerBarY - 20, this.playerName, {
                fontSize: '14px',
                fontFamily: 'Arial, sans-serif',
                color: '#ffffff'
            });
            
            // Player HP bar background
            this.add.rectangle(20 + 100, playerBarY, 200, 20, 0x333333).setOrigin(0, 0.5);
            
            // Player HP bar fill
            this.playerHpBar = this.add.rectangle(20 + 100, playerBarY, 200, 20, 0x44ff44).setOrigin(0, 0.5);
            
            // Player HP text
            this.playerHpText = this.add.text(20 + 200, playerBarY, '100/100', {
                fontSize: '12px',
                fontFamily: 'Arial, sans-serif',
                color: '#ffffff'
            }).setOrigin(0.5, 0.5);

            // Note: Enemy HP bars are created per-enemy in createEnemies()
        }

        createBattleLog(width, height) {
            // Battle log on the right side
            const logX = width - 190;
            const logY = height / 2 - 50;
            
            this.add.rectangle(logX, logY, 180, 150, 0x000000, 0.6)
                .setOrigin(0, 0)
                .setStrokeStyle(1, 0x444444);
            
            this.add.text(logX + 10, logY + 5, 'Battle Log', {
                fontSize: '12px',
                fontFamily: 'Arial, sans-serif',
                fontStyle: 'bold',
                color: '#ffffff'
            });
            
            this.logText = this.add.text(logX + 10, logY + 25, '', {
                fontSize: '10px',
                fontFamily: 'Arial, sans-serif',
                color: '#cccccc',
                wordWrap: { width: 160 }
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

            if (character === 'Attacker') {
                // Player HP
                if (this.playerMaxHp === 100 && hp > 100) {
                    this.playerMaxHp = hp;
                }
                this.playerCurrentHp = hp;
                this.updatePlayerHPBar();
            } else {
                // Enemy HP
                if (this.enemyMaxHp === 100 && hp > 100) {
                    this.enemyMaxHp = hp;
                }
                this.enemyCurrentHp = hp;
                this.updateEnemyHPBar();
            }
        }

        updatePlayerHPBar() {
            const ratio = Math.max(0, this.playerCurrentHp / this.playerMaxHp);
            this.tweens.add({
                targets: this.playerHpBar,
                scaleX: ratio,
                duration: 200,
                ease: 'Power2'
            });
            this.playerHpText.setText(`${Math.max(0, this.playerCurrentHp)}/${this.playerMaxHp}`);
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
            const damage = getEventField(evt, 'Damage') ?? 0;
            const isCritical = getEventField(evt, 'IsCritical') ?? false;

            if (attacker === 'Attacker') {
                // Player attacking enemy
                this.animatePlayerAttack();
                this.flashEnemies();
            } else {
                // Enemy attacking player
                this.animateEnemyAttack();
                this.flashPlayer();
            }

            // Play sound
            this.playSound(isCritical ? 'critical' : 'attack');

            // Add to log
            const attackerName = attacker === 'Attacker' ? this.playerName : this.enemyName;
            const critText = isCritical ? ' (CRIT!)' : '';
            this.addLogEntry(`${attackerName}: ${damage} dmg${critText}`);
        }

        animatePlayerAttack() {
            if (!this.playerSprite) return;
            
            // Player moves up towards enemies
            this.tweens.add({
                targets: this.playerSprite,
                y: this.playerSprite.y - 50,
                duration: 100,
                yoyo: true,
                ease: 'Power2'
            });
        }

        animateEnemyAttack() {
            // Enemies move down towards player
            this.enemySprites.forEach((enemy, index) => {
                this.tweens.add({
                    targets: enemy,
                    y: enemy.y + 30,
                    duration: 100,
                    delay: index * 50,
                    yoyo: true,
                    ease: 'Power2'
                });
            });
        }

        flashPlayer() {
            if (!this.playerSprite) return;
            this.playerSprite.setTint(0xff0000);
            this.time.delayedCall(100, () => {
                this.playerSprite.clearTint();
            });
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

            if (character === 'Attacker') {
                // Player KO'd
                this.tweens.add({
                    targets: this.playerSprite,
                    alpha: 0.3,
                    angle: 90,
                    duration: 500
                });
                this.addLogEntry(`${this.playerName} defeated!`);
            } else {
                // Enemy KO'd
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
            this.logEntries.push(text);
            // Keep only last 8 entries
            if (this.logEntries.length > 8) {
                this.logEntries.shift();
            }
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
            const enemySpritePath = battleData?.enemySpritePath ?? battleData?.EnemySpritePath ?? defaultSprites.enemies[enemyType] ?? defaultSprites.enemies.normal;

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
                        enemySpritePath: enemySpritePath
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
            const enemySpritePath = battleData?.enemySpritePath ?? battleData?.EnemySpritePath ?? defaultSprites.enemies[enemyType] ?? defaultSprites.enemies.normal;

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
                enemySpritePath: enemySpritePath
            });
        }
    };
})();
