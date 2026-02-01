(function () {
    'use strict';

    const DEFAULT_WIDTH = 800;
    const DEFAULT_HEIGHT = 500;
    const DEFAULT_EVENT_INTERVAL = 800;

    let game = null;
    let activeScene = null;

    const spritePaths = {
        attacker: '/sprites/games/my-tuno/tuno_attacking_right.png',
        defender: '/sprites/games/my-tuno/tuno_attacking_left.png'
    };

    const getEventField = (evt, field) => {
        if (!evt) return undefined;
        return evt[field] ?? evt[field.toLowerCase()];
    };

    const resolveEvents = (battleData) => {
        if (!battleData) {
            return [];
        }
        
        // Check for EventsJson with different casings (JSInterop might change casing)
        const eventsJson = battleData.EventsJson ?? battleData.eventsJson ?? battleData.eventsjson;
        
        // If EventsJson is provided as a JSON string, parse it
        if (eventsJson && typeof eventsJson === 'string') {
            try {
                const parsed = JSON.parse(eventsJson);
                return parsed;
            } catch (e) {
                console.error('Failed to parse EventsJson:', e);
                return [];
            }
        }
        
        // Legacy support: if Events is an array
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

    class BattleScene extends Phaser.Scene {
        constructor() {
            super({ key: 'BattleScene' });
            this.eventsList = [];
            this.dotNetRef = null;
            this.mode = 'live';
            this.eventInterval = DEFAULT_EVENT_INTERVAL;
            this.currentEventIndex = 0;
            this.maxHp = { attacker: 100, defender: 100 };
            this.currentHp = { attacker: 100, defender: 100 };
            this.characterSprites = {};
            this.hpGraphics = null;
            this.hpTexts = {};
            this.nameTexts = {};
            this.logEntries = [];
            this.logText = null;
            this.logBackground = null;
            this.replayIndex = 0;
            this.isPlaying = false;
            this.playbackSpeed = 1;
            this.replayAccumulator = 0;
            this.attackerName = 'Attacker';
            this.defenderName = 'Defender';
        }

        init(data) {
            this.eventsList = data?.events ?? [];
            this.dotNetRef = data?.dotNetRef ?? null;
            this.mode = data?.mode ?? 'live';
            this.eventInterval = data?.eventInterval ?? DEFAULT_EVENT_INTERVAL;
            this.attackerName = data?.attackerName ?? 'Attacker';
            this.defenderName = data?.defenderName ?? 'Defender';
            this.currentEventIndex = 0;
            this.replayIndex = 0;
            this.isPlaying = this.mode === 'live';
            this.playbackSpeed = 1;
            this.replayAccumulator = 0;
        }

        preload() {
            this.load.image('attackerSprite', spritePaths.attacker);
            this.load.image('defenderSprite', spritePaths.defender);
        }

        create() {
            activeScene = this;

            const { width, height } = this.scale;
            this.add.rectangle(width / 2, height / 2, width, height, 0x1a1a1a).setDepth(-2);
            this.createArena(width, height);
            this.createCharacters(width, height);
            this.initializeHpFromEvents();
            this.drawHpBars();
            this.createLogPanel(width, height);

            if (this.mode === 'live') {
                this.scheduleNextEvent();
            } else {
                this.setupReplayLoop();
                this.updateCharacterStates(0);
            }
        }

        createArena(width, height) {
            const groundHeight = 50;
            this.add.rectangle(width / 2, height - groundHeight / 2, width, groundHeight, 0x2a2a2a);

            const line = this.add.graphics();
            line.lineStyle(2, 0x444444, 1);
            line.beginPath();
            line.moveTo(width / 2, 0);
            line.lineTo(width / 2, height - groundHeight);
            line.strokePath();
        }

        createCharacters(width, height) {
            const groundOffset = 60;
            const attackerX = width * 0.25;
            const defenderX = width * 0.75;
            const characterY = height - groundOffset;

            const attackerSprite = this.add.sprite(attackerX, characterY, 'attackerSprite');
            attackerSprite.setOrigin(0.5, 1);
            const attackerScale = this.getSpriteScale(attackerSprite, height);
            attackerSprite.setScale(attackerScale);

            const defenderSprite = this.add.sprite(defenderX, characterY, 'defenderSprite');
            defenderSprite.setOrigin(0.5, 1);
            const defenderScale = this.getSpriteScale(defenderSprite, height);
            defenderSprite.setScale(defenderScale);

            this.characterSprites = {
                attacker: { sprite: attackerSprite, originX: attackerX },
                defender: { sprite: defenderSprite, originX: defenderX }
            };

            this.nameTexts.attacker = this.add.text(attackerX, characterY - attackerSprite.displayHeight - 20, this.attackerName, {
                fontFamily: 'Arial',
                fontSize: '16px',
                fontStyle: 'bold',
                color: '#ffffff'
            }).setOrigin(0.5, 0);

            this.nameTexts.defender = this.add.text(defenderX, characterY - defenderSprite.displayHeight - 20, this.defenderName, {
                fontFamily: 'Arial',
                fontSize: '16px',
                fontStyle: 'bold',
                color: '#ffffff'
            }).setOrigin(0.5, 0);
        }

        getSpriteScale(sprite, height) {
            const maxSpriteHeight = height * 0.45;
            const sourceImage = sprite.texture.getSourceImage();
            if (!sourceImage || !sourceImage.height) {
                return 0.6;
            }
            return Math.min(1, maxSpriteHeight / sourceImage.height);
        }

        createLogPanel(width, height) {
            const panelHeight = 80;
            const groundHeight = 50;
            // Position log panel at very bottom of canvas, below ground
            const panelY = height - panelHeight / 2 - 5;
            this.logBackground = this.add.rectangle(width / 2, panelY, width - 40, panelHeight, 0x0f0f0f, 0.9);
            this.logBackground.setStrokeStyle(1, 0x333333, 1);

            this.logText = this.add.text(30, panelY - panelHeight / 2 + 10, '', {
                fontFamily: 'Arial',
                fontSize: '13px',
                color: '#f1f1f1',
                wordWrap: { width: width - 60 }
            });
        }

        addLogEntry(message) {
            if (!message) return;
            this.logEntries.unshift(message);
            this.logEntries = this.logEntries.slice(0, 4);
            if (this.logText) {
                this.logText.setText(this.logEntries.join('\n'));
            }
        }

        initializeHpFromEvents() {
            let attackerInitialized = false;
            let defenderInitialized = false;

            this.maxHp = { attacker: 100, defender: 100 };
            this.currentHp = { attacker: 100, defender: 100 };

            this.eventsList.forEach((evt) => {
                const type = getEventField(evt, 'Type');
                if (type !== 'HPUpdate') return;

                const character = getEventField(evt, 'Character');
                const hp = getEventField(evt, 'HP');
                if (character === 'Attacker' && !attackerInitialized) {
                    this.maxHp.attacker = hp ?? 100;
                    this.currentHp.attacker = hp ?? 100;
                    attackerInitialized = true;
                }
                if (character === 'Defender' && !defenderInitialized) {
                    this.maxHp.defender = hp ?? 100;
                    this.currentHp.defender = hp ?? 100;
                    defenderInitialized = true;
                }
            });
        }

        drawHpBars() {
            const barWidth = 200;
            const barHeight = 24;
            const paddingTop = 30;

            if (!this.hpGraphics) {
                this.hpGraphics = this.add.graphics();
            }
            this.hpGraphics.clear();

            const drawBar = (x, currentHp, maxHp) => {
                const hpPercent = maxHp > 0 ? currentHp / maxHp : 0;
                const fillColor = hpPercent > 0.5 ? 0x4caf50 : hpPercent > 0.25 ? 0xff9800 : 0xf44336;

                this.hpGraphics.fillStyle(0x333333, 1);
                this.hpGraphics.fillRect(x, paddingTop, barWidth, barHeight);
                this.hpGraphics.fillStyle(fillColor, 1);
                this.hpGraphics.fillRect(x, paddingTop, barWidth * hpPercent, barHeight);
                this.hpGraphics.lineStyle(2, 0xffffff, 1);
                this.hpGraphics.strokeRect(x, paddingTop, barWidth, barHeight);
            };

            drawBar(50, this.currentHp.attacker, this.maxHp.attacker);
            drawBar(this.scale.width - 50 - barWidth, this.currentHp.defender, this.maxHp.defender);

            if (!this.hpTexts.attacker) {
                this.hpTexts.attacker = this.add.text(50 + barWidth / 2, paddingTop + 4, '', {
                    fontFamily: 'Arial',
                    fontSize: '14px',
                    fontStyle: 'bold',
                    color: '#ffffff'
                }).setOrigin(0.5, 0);
            }
            if (!this.hpTexts.defender) {
                this.hpTexts.defender = this.add.text(this.scale.width - 50 - barWidth / 2, paddingTop + 4, '', {
                    fontFamily: 'Arial',
                    fontSize: '14px',
                    fontStyle: 'bold',
                    color: '#ffffff'
                }).setOrigin(0.5, 0);
            }

            this.hpTexts.attacker.setText(`${this.currentHp.attacker} / ${this.maxHp.attacker} HP`);
            this.hpTexts.defender.setText(`${this.currentHp.defender} / ${this.maxHp.defender} HP`);
        }

        scheduleNextEvent() {
            if (this.currentEventIndex >= this.eventsList.length) {
                this.finishBattle();
                return;
            }

            const evt = this.eventsList[this.currentEventIndex];
            this.processEvent(evt);
            this.currentEventIndex += 1;

            this.time.delayedCall(this.eventInterval, () => {
                this.scheduleNextEvent();
            });
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
                this.playAttack(attacker, defender, damage);
                if (attacker && defender) {
                    const damageText = damage ? `-${damage}` : '0';
                    this.addLogEntry(`${attacker} atacou ${defender} (${damageText})`);
                }
                return;
            }

            if (type === 'KO') {
                const character = getEventField(evt, 'Character');
                this.playKo(character);
                if (character) {
                    this.addLogEntry(`${character} foi nocauteado`);
                }
                return;
            }

            if (type === 'Victory') {
                const winner = getEventField(evt, 'Winner');
                this.showVictory(winner);
                if (winner) {
                    this.addLogEntry(`${winner} venceu a batalha`);
                }
            }
        }

        playAttack(attackerKey, defenderKey, damage) {
            const attacker = attackerKey === 'Defender' ? this.characterSprites.defender : this.characterSprites.attacker;
            const defender = defenderKey === 'Attacker' ? this.characterSprites.attacker : this.characterSprites.defender;

            if (!attacker || !defender) return;

            const direction = attackerKey === 'Defender' ? -1 : 1;
            const distance = Math.abs(defender.sprite.x - attacker.sprite.x);
            const lungeOffset = Math.min(220, distance * 0.6);
            const startX = attacker.originX;
            const startY = attacker.sprite.y;
            const targetX = startX + direction * lungeOffset;
            const targetY = startY - 15;

            const damageValue = damage ?? 0;
            const isCritical = damageValue > 25; // Detect critical hits (higher damage)

            // Enhanced attacker lunge animation with variable speed based on attack strength
            const lungeDuration = isCritical ? 150 : 200;
            const originalScale = attacker.sprite.scaleX; // Store original scale
            const targetScale = originalScale * (isCritical ? 1.1 : 1);
            
            this.tweens.add({
                targets: attacker.sprite,
                x: targetX,
                y: targetY,
                angle: direction * (isCritical ? 18 : 12),
                scale: targetScale,
                duration: lungeDuration,
                ease: 'Power3',
                onComplete: () => {
                    // Return to original position and scale
                    this.tweens.add({
                        targets: attacker.sprite,
                        x: startX,
                        y: startY,
                        angle: 0,
                        scale: originalScale, // Restore original scale directly
                        duration: 240,
                        ease: 'Back.Out'
                    });
                }
            });

            // Enhanced defender hit reaction
            const defenderTintColor = isCritical ? 0xff0000 : 0xff5555;
            defender.sprite.setTintFill(defenderTintColor);
            this.time.delayedCall(200, () => defender.sprite.clearTint());

            const defenderStartX = defender.sprite.x;
            const recoilDistance = isCritical ? 30 : 20;
            this.tweens.add({
                targets: defender.sprite,
                x: defenderStartX + direction * recoilDistance,
                yoyo: true,
                duration: isCritical ? 100 : 120,
                ease: 'Back.Out'
            });

            // Screen shake for critical hits
            if (isCritical && this.cameras && this.cameras.main) {
                this.cameras.main.shake(150, 0.006);
            }

            // Enhanced impact visual with particles for critical hits
            const impactX = defender.sprite.x;
            const impactY = defender.sprite.y - defender.sprite.displayHeight * 0.4;
            
            if (isCritical) {
                // Create particle burst for critical hits
                const particles = this.add.particles(impactX, impactY, 'attackerSprite', {
                    speed: { min: 50, max: 150 },
                    angle: { min: 0, max: 360 },
                    scale: { start: 0.3, end: 0 },
                    alpha: { start: 1, end: 0 },
                    tint: [0xffff00, 0xff9900, 0xff0000],
                    lifespan: 400,
                    quantity: 12,
                    blendMode: 'ADD'
                });
                this.time.delayedCall(400, () => particles.destroy());
            }

            // Impact flash
            const impactColor = isCritical ? 0xffff00 : 0xffd54f;
            const impactSize = isCritical ? 25 : 18;
            const impact = this.add.circle(impactX, impactY, impactSize, impactColor, 0.9);
            this.tweens.add({
                targets: impact,
                alpha: 0,
                scale: isCritical ? 2.2 : 1.6,
                duration: isCritical ? 400 : 300,
                onComplete: () => impact.destroy()
            });

            // Enhanced slash effect
            const slash = this.add.graphics();
            const slashColor = isCritical ? 0xffff00 : 0xffffff;
            slash.lineStyle(isCritical ? 6 : 4, slashColor, 0.9);
            slash.beginPath();
            slash.moveTo(attacker.sprite.x, attacker.sprite.y - attacker.sprite.displayHeight * 0.5);
            slash.lineTo(defender.sprite.x, defender.sprite.y - defender.sprite.displayHeight * 0.5);
            slash.strokePath();
            this.tweens.add({
                targets: slash,
                alpha: 0,
                duration: isCritical ? 250 : 200,
                onComplete: () => slash.destroy()
            });

            // Enhanced damage text with critical styling
            const damageText = this.add.text(
                defender.sprite.x, 
                defender.sprite.y - defender.sprite.displayHeight * 0.6, 
                isCritical ? `CRIT! -${damageValue}` : `-${damageValue}`, 
                {
                    fontFamily: 'Arial',
                    fontSize: isCritical ? '28px' : '24px',
                    fontStyle: 'bold',
                    color: isCritical ? '#ffff00' : '#ff4444',
                    stroke: '#000000',
                    strokeThickness: 3
                }
            ).setOrigin(0.5, 0.5);

            this.tweens.add({
                targets: damageText,
                y: damageText.y - (isCritical ? 80 : 60),
                alpha: 0,
                scale: isCritical ? 1.3 : 1.1,
                duration: isCritical ? 1000 : 800,
                ease: 'Power2',
                onComplete: () => damageText.destroy()
            });

            // Show attacker gains (small "+X" for attacker)
            const attackerText = this.add.text(
                attacker.sprite.x, 
                attacker.sprite.y - attacker.sprite.displayHeight * 0.6, 
                `+${damageValue}`, 
                {
                    fontFamily: 'Arial',
                    fontSize: '18px',
                    fontStyle: 'bold',
                    color: '#4caf50'
                }
            ).setOrigin(0.5, 0.5);

            this.tweens.add({
                targets: attackerText,
                y: attackerText.y - 20,
                alpha: 0,
                duration: 700,
                ease: 'Power2', // Match damage text easing for consistency
                onComplete: () => attackerText.destroy()
            });
        }

        playKo(character) {
            const target = character === 'Defender' ? this.characterSprites.defender : this.characterSprites.attacker;
            if (!target) return;
            
            // Enhanced KO animation with fall effect
            this.tweens.add({
                targets: target.sprite,
                alpha: 0.4,
                angle: character === 'Defender' ? 90 : -90,
                y: target.sprite.y + 30,
                duration: 600,
                ease: 'Bounce.Out'
            });
            
            // Add "K.O." text above defeated character
            const koText = this.add.text(
                target.sprite.x,
                target.sprite.y - target.sprite.displayHeight - 30,
                'K.O.!',
                {
                    fontFamily: 'Arial',
                    fontSize: '36px',
                    fontStyle: 'bold',
                    color: '#ff0000',
                    stroke: '#000000',
                    strokeThickness: 4
                }
            ).setOrigin(0.5, 0.5).setAlpha(0);
            
            this.tweens.add({
                targets: koText,
                alpha: 1,
                scale: { from: 0.5, to: 1.5 },
                duration: 400,
                ease: 'Back.Out',
                yoyo: true,
                hold: 400,
                onComplete: () => koText.destroy()
            });
            
            // Screen flash on KO
            if (this.cameras && this.cameras.main) {
                this.cameras.main.flash(300, 255, 0, 0);
            }
        }

        showVictory(winner) {
            const isAttackerWinner = winner === 'Attacker' || winner === this.attackerName;
            const winnerSprite = isAttackerWinner ? this.characterSprites.attacker : this.characterSprites.defender;
            
            // Winner celebration animation
            if (winnerSprite) {
                this.tweens.add({
                    targets: winnerSprite.sprite,
                    y: winnerSprite.sprite.y - 20,
                    yoyo: true,
                    repeat: 3,
                    duration: 200,
                    ease: 'Sine.InOut'
                });
            }
            
            // Victory text with enhanced animation
            const victoryText = this.add.text(
                this.scale.width / 2, 
                this.scale.height / 2 - 50, 
                `${winner} vence!`, 
                {
                    fontFamily: 'Arial',
                    fontSize: '48px',
                    fontStyle: 'bold',
                    color: '#ffd700',
                    stroke: '#000000',
                    strokeThickness: 6,
                    shadow: {
                        offsetX: 3,
                        offsetY: 3,
                        color: '#000000',
                        blur: 5,
                        fill: true
                    }
                }
            ).setOrigin(0.5, 0.5).setAlpha(0).setScale(0.5);

            this.tweens.add({
                targets: victoryText,
                alpha: 1,
                scale: 1.2,
                duration: 400,
                ease: 'Back.Out'
            });
            
            // Confetti/celebration particles
            if (winnerSprite) {
                const particles = this.add.particles(
                    this.scale.width / 2, 
                    this.scale.height / 2 - 100, 
                    'attackerSprite', 
                    {
                        speed: { min: 100, max: 250 },
                        angle: { min: 0, max: 360 },
                        scale: { start: 0.4, end: 0 },
                        alpha: { start: 1, end: 0 },
                        tint: [0xffd700, 0xffa500, 0xffff00, 0xff69b4],
                        lifespan: 1500,
                        quantity: 3,
                        frequency: 100,
                        blendMode: 'ADD'
                    }
                );
                
                this.time.delayedCall(2000, () => particles.destroy());
            }

            // Fade out victory text
            this.tweens.add({
                targets: victoryText,
                alpha: 0,
                y: victoryText.y - 30,
                duration: 800,
                delay: 1200,
                ease: 'Power2',
                onComplete: () => victoryText.destroy()
            });
        }

        finishBattle() {
            if (this.dotNetRef?.invokeMethodAsync) {
                this.dotNetRef.invokeMethodAsync('OnBattleFinished');
            }
        }

        setupReplayLoop() {
            this.isPlaying = false;
            this.replayAccumulator = 0;

            this.time.addEvent({
                delay: 100,
                loop: true,
                callback: () => {
                    if (!this.isPlaying || this.replayIndex >= this.eventsList.length) {
                        return;
                    }

                    this.replayAccumulator += 100 * this.playbackSpeed;
                    if (this.replayAccumulator >= 1000) {
                        this.replayAccumulator = 0;
                        this.replayIndex += 1;
                        if (this.replayIndex < this.eventsList.length) {
                            this.processEvent(this.eventsList[this.replayIndex]);
                            this.updateCharacterStates(this.replayIndex);
                        }
                    }
                }
            });
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
        }

        jumpToEvent(index) {
            if (index < 0 || index >= this.eventsList.length) return;
            this.replayIndex = index;
            this.updateCharacterStates(index);
            this.processEvent(this.eventsList[index]);
        }
    }

    const createGame = (hostId, battleData, mode) => {
        const container = document.getElementById(hostId);
        if (!container) return;

        const config = {
            type: Phaser.AUTO,
            parent: hostId,
            width: DEFAULT_WIDTH,
            height: DEFAULT_HEIGHT,
            backgroundColor: '#1a1a1a',
            scale: {
                mode: Phaser.Scale.NONE,  // Fixed size, no zoom/scale
                width: DEFAULT_WIDTH,
                height: DEFAULT_HEIGHT
            },
            scene: BattleScene
        };

        const events = resolveEvents(battleData);
        const dotNetRef = resolveDotNetRef(battleData);
        const attackerName = resolveAttackerName(battleData);
        const defenderName = resolveDefenderName(battleData);

        game = new Phaser.Game({
            ...config,
            scene: new BattleScene()
        });

        game.scene.start('BattleScene', {
            events,
            dotNetRef,
            mode,
            attackerName,
            defenderName
        });
    };

    const destroyBattle = () => {
        if (game) {
            game.destroy(true);
            game = null;
            activeScene = null;
        }
    };

    window.myTunoGame = {
        startBattle: (hostId, battleData) => {
            destroyBattle();
            createGame(hostId, battleData, 'live');
        },
        startReplay: (hostId, battleData) => {
            destroyBattle();
            createGame(hostId, battleData, 'replay');
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
        destroyBattle
    };
})();
