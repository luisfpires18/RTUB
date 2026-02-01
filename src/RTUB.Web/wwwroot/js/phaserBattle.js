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
        console.log('resolveEvents called with:', battleData);
        
        if (!battleData) {
            console.log('No battleData provided');
            return [];
        }
        
        // If EventsJson is provided as a JSON string, parse it
        if (battleData.EventsJson && typeof battleData.EventsJson === 'string') {
            console.log('Parsing EventsJson string, length:', battleData.EventsJson.length);
            try {
                const parsed = JSON.parse(battleData.EventsJson);
                console.log('Parsed events count:', parsed.length);
                return parsed;
            } catch (e) {
                console.error('Failed to parse EventsJson:', e);
                return [];
            }
        }
        
        // Legacy support: if Events is an array
        if (Array.isArray(battleData)) {
            console.log('battleData is array, length:', battleData.length);
            return battleData;
        }
        
        const events = battleData.events ?? battleData.Events ?? [];
        console.log('Using events/Events property, length:', events.length);
        return events;
    };

    const resolveDotNetRef = (battleData) => {
        if (!battleData || Array.isArray(battleData)) return null;
        return battleData.dotNetRef ?? battleData.DotNetRef ?? null;
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
        }

        init(data) {
            this.eventsList = data?.events ?? [];
            this.dotNetRef = data?.dotNetRef ?? null;
            this.mode = data?.mode ?? 'live';
            this.eventInterval = data?.eventInterval ?? DEFAULT_EVENT_INTERVAL;
            this.currentEventIndex = 0;
            this.replayIndex = 0;
            this.isPlaying = this.mode === 'live';
            this.playbackSpeed = 1;
            this.replayAccumulator = 0;
            
            // Debug: Log what we received
            console.log('BattleScene.init called');
            console.log('Events count:', this.eventsList.length);
            console.log('Mode:', this.mode);
            console.log('Event interval:', this.eventInterval);
            if (this.eventsList.length > 0) {
                console.log('First 3 events:', this.eventsList.slice(0, 3));
            }
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

            this.nameTexts.attacker = this.add.text(attackerX, characterY - attackerSprite.displayHeight - 20, 'Attacker', {
                fontFamily: 'Arial',
                fontSize: '16px',
                fontStyle: 'bold',
                color: '#ffffff'
            }).setOrigin(0.5, 0);

            this.nameTexts.defender = this.add.text(defenderX, characterY - defenderSprite.displayHeight - 20, 'Defender', {
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
            const panelHeight = 90;
            const panelY = height - 50 - panelHeight / 2;
            this.logBackground = this.add.rectangle(width / 2, panelY, width - 40, panelHeight, 0x0f0f0f, 0.7);
            this.logBackground.setStrokeStyle(1, 0x333333, 1);

            this.logText = this.add.text(30, panelY - panelHeight / 2 + 10, '', {
                fontFamily: 'Arial',
                fontSize: '14px',
                color: '#f1f1f1'
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
            console.log('scheduleNextEvent - index:', this.currentEventIndex, 'total:', this.eventsList.length);
            
            if (this.currentEventIndex >= this.eventsList.length) {
                console.log('Battle finished - calling finishBattle()');
                this.finishBattle();
                return;
            }

            const evt = this.eventsList[this.currentEventIndex];
            console.log('Processing event', this.currentEventIndex, ':', evt);
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

            this.tweens.timeline({
                targets: attacker.sprite,
                tweens: [
                    { x: targetX, y: targetY, angle: direction * 12, duration: 200, ease: 'Power2' },
                    { x: startX, y: startY, angle: 0, duration: 240, ease: 'Power2' }
                ]
            });

            defender.sprite.setTintFill(0xff5555);
            this.time.delayedCall(200, () => defender.sprite.clearTint());

            const defenderStartX = defender.sprite.x;
            this.tweens.add({
                targets: defender.sprite,
                x: defenderStartX + direction * 20,
                yoyo: true,
                duration: 120,
                ease: 'Back.Out'
            });

            const impact = this.add.circle(defender.sprite.x, defender.sprite.y - defender.sprite.displayHeight * 0.4, 18, 0xffd54f, 0.9);
            this.tweens.add({
                targets: impact,
                alpha: 0,
                scale: 1.6,
                duration: 300,
                onComplete: () => impact.destroy()
            });

            const slash = this.add.graphics();
            slash.lineStyle(4, 0xffffff, 0.8);
            slash.beginPath();
            slash.moveTo(attacker.sprite.x, attacker.sprite.y - attacker.sprite.displayHeight * 0.5);
            slash.lineTo(defender.sprite.x, defender.sprite.y - defender.sprite.displayHeight * 0.5);
            slash.strokePath();
            this.tweens.add({
                targets: slash,
                alpha: 0,
                duration: 200,
                onComplete: () => slash.destroy()
            });

            const damageValue = damage ?? 0;
            const damageText = this.add.text(defender.sprite.x, defender.sprite.y - defender.sprite.displayHeight * 0.6, `-${damageValue}`, {
                fontFamily: 'Arial',
                fontSize: '24px',
                fontStyle: 'bold',
                color: '#ff4444'
            }).setOrigin(0.5, 0.5);

            this.tweens.add({
                targets: damageText,
                y: damageText.y - 30,
                alpha: 0,
                duration: 900,
                onComplete: () => damageText.destroy()
            });

            const attackerText = this.add.text(attacker.sprite.x, attacker.sprite.y - attacker.sprite.displayHeight * 0.6, `+${damageValue}`, {
                fontFamily: 'Arial',
                fontSize: '18px',
                fontStyle: 'bold',
                color: '#4caf50'
            }).setOrigin(0.5, 0.5);

            this.tweens.add({
                targets: attackerText,
                y: attackerText.y - 20,
                alpha: 0,
                duration: 700,
                onComplete: () => attackerText.destroy()
            });
        }

        playKo(character) {
            const target = character === 'Defender' ? this.characterSprites.defender : this.characterSprites.attacker;
            if (!target) return;
            this.tweens.add({
                targets: target.sprite,
                alpha: 0.4,
                duration: 400,
                ease: 'Power2'
            });
        }

        showVictory(winner) {
            const text = this.add.text(this.scale.width / 2, this.scale.height / 2, `${winner} vence!`, {
                fontFamily: 'Arial',
                fontSize: '32px',
                fontStyle: 'bold',
                color: '#ffffff',
                backgroundColor: 'rgba(0,0,0,0.6)',
                padding: { x: 16, y: 8 }
            }).setOrigin(0.5, 0.5);

            this.tweens.add({
                targets: text,
                alpha: 0,
                duration: 1600,
                delay: 800,
                onComplete: () => text.destroy()
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
                mode: Phaser.Scale.FIT,
                autoCenter: Phaser.Scale.CENTER_BOTH,
                width: DEFAULT_WIDTH,
                height: DEFAULT_HEIGHT
            },
            scene: BattleScene
        };

        const events = resolveEvents(battleData);
        const dotNetRef = resolveDotNetRef(battleData);

        game = new Phaser.Game({
            ...config,
            scene: new BattleScene()
        });

        game.scene.start('BattleScene', {
            events,
            dotNetRef,
            mode
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
