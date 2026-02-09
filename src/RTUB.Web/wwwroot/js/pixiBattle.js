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
            this.logEntries = [];
            this.logText = null;
            this.logBackground = null;
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
            
            // Audio system initialization
            this.audioEnabled = true;
            this.musicVolume = 0.3;
            this.sfxVolume = 0.5;
            this.setupAudio();

            this.initPixi();
        }

        setupAudio() {
            this.audioContext = null;
            
            try {
                if (typeof AudioContext !== 'undefined') {
                    this.audioContext = new AudioContext();
                } else if (typeof webkitAudioContext !== 'undefined') {
                    this.audioContext = new webkitAudioContext();
                }
                
                // Start background music if not already playing
                if (this.audioContext && !arenaBackgroundMusic) {
                    this.loadBackgroundMusic();
                }
            } catch (e) {
                console.warn('Audio not supported:', e);
                this.audioEnabled = false;
            }
        }
        
        async loadBackgroundMusic() {
            try {
                const response = await fetch('/sound/arena_battle.mp3');
                const arrayBuffer = await response.arrayBuffer();
                const audioBuffer = await this.audioContext.decodeAudioData(arrayBuffer);
                
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
            await PIXI.Assets.load([
                { alias: 'attackerSprite', src: spritePaths.attacker },
                { alias: 'defenderSprite', src: spritePaths.defender },
                { alias: 'arenaBg', src: spritePaths.background }
            ]);
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
            this.createLogPanel(width, height);

            if (this.mode === 'live') {
                this.startTimedBattle();
            } else {
                this.setupReplayLoop();
                this.updateCharacterStates(0);
            }

            this.app.ticker.add(() => this.update());
        }

        createArena(width, height) {
            const groundHeight = 50;
            const ground = new PIXI.Graphics();
            ground.rect(0, height - groundHeight, width, groundHeight);
            ground.fill(0x2a2a2a);
            this.stage.addChild(ground);

            const line = new PIXI.Graphics();
            line.moveTo(width / 2, 0);
            line.lineTo(width / 2, height - groundHeight);
            line.stroke({ width: 2, color: 0x444444 });
            this.stage.addChild(line);
        }

        createCharacters(width, height) {
            const groundOffset = 60;
            const attackerX = width * 0.25;
            const defenderX = width * 0.75;
            const characterY = height - groundOffset;

            const attackerSprite = PIXI.Sprite.from('attackerSprite');
            attackerSprite.anchor.set(0.5, 1);
            attackerSprite.x = attackerX;
            attackerSprite.y = characterY;
            const attackerScale = this.getSpriteScale(attackerSprite, height);
            attackerSprite.scale.set(attackerScale);
            
            // Add blue aura BEFORE sprite so it renders behind
            if (this.hasShotBuff) {
                const attackerDisplayHeight = attackerSprite.height;
                const auraSize = attackerDisplayHeight * 0.7;
                this.attackerAura = new PIXI.Graphics();
                this.attackerAura.circle(0, 0, auraSize);
                this.attackerAura.fill({ color: 0x44bbff, alpha: 0.35 });
                this.attackerAura.x = attackerX;
                this.attackerAura.y = characterY - attackerSprite.height / 2;
                this.stage.addChild(this.attackerAura);
            }
            
            this.stage.addChild(attackerSprite);

            const defenderSprite = PIXI.Sprite.from('defenderSprite');
            defenderSprite.anchor.set(0.5, 1);
            defenderSprite.x = defenderX;
            defenderSprite.y = characterY;
            const defenderScale = this.getSpriteScale(defenderSprite, height);
            defenderSprite.scale.set(defenderScale);
            this.stage.addChild(defenderSprite);

            this.characterSprites = {
                attacker: { sprite: attackerSprite, originX: attackerX, originY: characterY },
                defender: { sprite: defenderSprite, originX: defenderX, originY: characterY }
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
            this.nameTexts.attacker.y = characterY - attackerSprite.height * attackerScale - 20;
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
            this.nameTexts.defender.y = characterY - defenderSprite.height * defenderScale - 20;
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

        getSpriteScale(sprite, height) {
            const maxSpriteHeight = height * 0.45;
            if (!sprite.texture || !sprite.texture.height) {
                return 0.6;
            }
            return Math.min(1, maxSpriteHeight / sprite.texture.height);
        }

        createLogPanel(width, height) {
            const panelHeight = 80;
            const panelY = height - panelHeight / 2 - 5;
            
            this.logBackground = new PIXI.Graphics();
            this.logBackground.rect(20, panelY - panelHeight / 2, width - 40, panelHeight);
            this.logBackground.fill({ color: 0x0f0f0f, alpha: 0.9 });
            this.logBackground.stroke({ width: 1, color: 0x333333 });
            this.stage.addChild(this.logBackground);

            this.logText = new PIXI.Text({
                text: '',
                style: {
                    fontFamily: 'Arial',
                    fontSize: 13,
                    fill: 0xf1f1f1,
                    wordWrap: true,
                    wordWrapWidth: width - 60
                }
            });
            this.logText.x = 30;
            this.logText.y = panelY - panelHeight / 2 + 10;
            this.stage.addChild(this.logText);
        }

        addLogEntry(message) {
            if (!message) return;
            this.logEntries.unshift(message);
            this.logEntries = this.logEntries.slice(0, 4);
            if (this.logText) {
                this.logText.text = this.logEntries.join('\n');
            }
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
            const barWidth = 200;
            const barHeight = 24;
            const paddingTop = 30;

            if (!this.hpGraphics) {
                this.hpGraphics = new PIXI.Graphics();
                this.stage.addChild(this.hpGraphics);
            }
            this.hpGraphics.clear();

            const drawBar = (x, currentHp, maxHp) => {
                const hpPercent = maxHp > 0 ? currentHp / maxHp : 0;
                const fillColor = hpPercent > 0.5 ? 0x4caf50 : hpPercent > 0.25 ? 0xff9800 : 0xf44336;

                this.hpGraphics.rect(x, paddingTop, barWidth, barHeight);
                this.hpGraphics.fill(0x333333);
                this.hpGraphics.rect(x, paddingTop, barWidth * hpPercent, barHeight);
                this.hpGraphics.fill(fillColor);
                this.hpGraphics.rect(x, paddingTop, barWidth, barHeight);
                this.hpGraphics.stroke({ width: 2, color: 0xffffff });
            };

            drawBar(50, this.currentHp.attacker, this.maxHp.attacker);
            drawBar(this.app.screen.width - 50 - barWidth, this.currentHp.defender, this.maxHp.defender);

            if (!this.hpTexts.attacker) {
                this.hpTexts.attacker = new PIXI.Text({
                    text: '',
                    style: {
                        fontFamily: 'Arial',
                        fontSize: 14,
                        fontWeight: 'bold',
                        fill: 0xffffff
                    }
                });
                this.hpTexts.attacker.anchor.set(0.5, 0);
                this.hpTexts.attacker.x = 50 + barWidth / 2;
                this.hpTexts.attacker.y = paddingTop + 4;
                this.stage.addChild(this.hpTexts.attacker);
            }
            if (!this.hpTexts.defender) {
                this.hpTexts.defender = new PIXI.Text({
                    text: '',
                    style: {
                        fontFamily: 'Arial',
                        fontSize: 14,
                        fontWeight: 'bold',
                        fill: 0xffffff
                    }
                });
                this.hpTexts.defender.anchor.set(0.5, 0);
                this.hpTexts.defender.x = this.app.screen.width - 50 - barWidth / 2;
                this.hpTexts.defender.y = paddingTop + 4;
                this.stage.addChild(this.hpTexts.defender);
            }

            this.hpTexts.attacker.text = `${formatNum(this.currentHp.attacker)} / ${formatNum(this.maxHp.attacker)} HP`;
            this.hpTexts.defender.text = `${formatNum(this.currentHp.defender)} / ${formatNum(this.maxHp.defender)} HP`;
        }

        drawSpeedBars() {
            const barWidth = 200;
            const barHeight = 8;
            const paddingTop = 58; // Below HP bar

            if (!this.speedBarGraphics) {
                this.speedBarGraphics = new PIXI.Graphics();
                this.stage.addChild(this.speedBarGraphics);
            }
            this.speedBarGraphics.clear();

            const drawSpeedBar = (x, timerMs, actionTimeMs) => {
                // Speed bar fills from right to left as timer drains
                const speedPercent = actionTimeMs > 0 ? timerMs / actionTimeMs : 0;
                
                // Background
                this.speedBarGraphics.rect(x, paddingTop, barWidth, barHeight);
                this.speedBarGraphics.fill(0x222222);
                
                // Fill - cyan/blue color for speed
                this.speedBarGraphics.rect(x, paddingTop, barWidth * speedPercent, barHeight);
                this.speedBarGraphics.fill(0x00bcd4);
                
                // Border
                this.speedBarGraphics.rect(x, paddingTop, barWidth, barHeight);
                this.speedBarGraphics.stroke({ width: 1, color: 0x666666 });
            };

            const attackerActionTimeMs = this.actionTime.attacker * 1000;
            const defenderActionTimeMs = this.actionTime.defender * 1000;

            drawSpeedBar(50, this.speedBarTimers.attacker, attackerActionTimeMs);
            drawSpeedBar(this.app.screen.width - 50 - barWidth, this.speedBarTimers.defender, defenderActionTimeMs);
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
                if (attacker && defender) {
                    const damageText = damage ? `-${formatNum(damage)}` : '0';
                    const attackerName = attacker === 'Attacker' ? this.attackerName : this.defenderName;
                    const defenderName = defender === 'Defender' ? this.defenderName : this.attackerName;
                    this.addLogEntry(`${attackerName} atacou ${defenderName} (${damageText})`);
                }
                return;
            }

            if (type === 'KO') {
                const character = getEventField(evt, 'Character');
                this.playKo(character);
                if (character) {
                    const characterName = character === 'Attacker' ? this.attackerName : this.defenderName;
                    this.addLogEntry(`${characterName} foi nocauteado`);
                }
                return;
            }

            if (type === 'Victory') {
                const winner = getEventField(evt, 'Winner');
                this.showVictory(winner);
                if (winner) {
                    const winnerName = winner === 'Attacker' ? this.attackerName : this.defenderName;
                    this.addLogEntry(`${winnerName} venceu a batalha`);
                }
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

            this.playSound(isCritical ? 'critical' : 'attack');

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

            const defenderTintColor = isCritical ? 0xff0000 : 0xff5555;
            defender.sprite.tint = defenderTintColor;
            setTimeout(() => defender.sprite.tint = 0xffffff, 200 / this.battleSpeed);

            this.playSound('hit');

            const defenderStartX = defender.sprite.x;
            const recoilDistance = isCritical ? 30 : 20;
            this.animateTo(defender.sprite, { 
                x: defenderStartX + direction * recoilDistance 
            }, isCritical ? 100 : 120, () => {
                this.animateTo(defender.sprite, { x: defenderStartX }, 100);
            });

            const impactX = defender.sprite.x;
            const impactY = defender.sprite.y - defender.sprite.height * 0.4;
            
            const impactColor = isCritical ? 0xffff00 : 0xffd54f;
            const impactSize = isCritical ? 25 : 18;
            const impact = new PIXI.Graphics();
            impact.circle(impactX, impactY, impactSize);
            impact.fill({ color: impactColor, alpha: 0.9 });
            this.stage.addChild(impact);
            
            this.fadeOut(impact, isCritical ? 400 : 300, () => {
                this.stage.removeChild(impact);
            });

            const slash = new PIXI.Graphics();
            const slashColor = isCritical ? 0xffff00 : 0xffffff;
            slash.moveTo(attacker.sprite.x, attacker.sprite.y - attacker.sprite.height * 0.5);
            slash.lineTo(defender.sprite.x, defender.sprite.y - defender.sprite.height * 0.5);
            slash.stroke({ width: isCritical ? 6 : 4, color: slashColor, alpha: 0.9 });
            this.stage.addChild(slash);
            
            this.fadeOut(slash, isCritical ? 250 : 200, () => {
                this.stage.removeChild(slash);
            });

            const damageText = new PIXI.Text({
                text: isCritical ? `CRIT! -${formatNum(damageValue)}` : `-${formatNum(damageValue)}`,
                style: {
                    fontFamily: 'Arial',
                    fontSize: isCritical ? 28 : 24,
                    fontWeight: 'bold',
                    fill: isCritical ? 0xffff00 : 0xff4444,
                    stroke: { color: 0x000000, width: 3 }
                }
            });
            damageText.anchor.set(0.5);
            damageText.x = defender.sprite.x;
            damageText.y = defender.sprite.y - defender.sprite.height * 0.6;
            this.stage.addChild(damageText);

            this.animateTo(damageText, { 
                y: damageText.y - (isCritical ? 80 : 60),
                alpha: 0
            }, isCritical ? 1000 : 800, () => {
                this.stage.removeChild(damageText);
            });

            const attackerText = new PIXI.Text({
                text: `+${formatNum(damageValue)}`,
                style: {
                    fontFamily: 'Arial',
                    fontSize: 18,
                    fontWeight: 'bold',
                    fill: 0x4caf50
                }
            });
            attackerText.anchor.set(0.5);
            attackerText.x = attacker.sprite.x;
            attackerText.y = attacker.sprite.y - attacker.sprite.height * 0.6;
            this.stage.addChild(attackerText);

            this.animateTo(attackerText, { 
                y: attackerText.y - 20,
                alpha: 0
            }, 700, () => {
                this.stage.removeChild(attackerText);
            });
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
                this.dotNetRef.invokeMethodAsync('OnBattleFinished');
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

        return new BattleScene(container, {
            events,
            dotNetRef,
            mode,
            attackerName,
            defenderName,
            HasShotBuff: hasShotBuff
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
