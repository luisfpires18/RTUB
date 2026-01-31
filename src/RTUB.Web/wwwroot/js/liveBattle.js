/**
 * Live Battle Animation - MyBrute-style battle visualization
 * Animates battles in real-time as events are generated
 */
'use strict';
    
    let canvas = null;
    let ctx = null;
    let dotNetRef = null;
    let isAnimating = false;
    let currentEventIndex = 0;
    let events = [];
    let animationFrameId = null;
    let eventTimerId = null;

    // Character positions and states
    const attacker = {
        x: 150,
        y: 250,
        width: 100,
        height: 150,
        hp: 100,
        maxHp: 100,
        name: "Attacker",
        offsetX: 0
    };

    const defender = {
        x: 650,
        y: 250,
        width: 100,
        height: 150,
        hp: 100,
        maxHp: 100,
        name: "Defender",
        offsetX: 0
    };

    // Sprite images
    let attackerSprite = null;
    let defenderSprite = null;
    const attackerSpritePath = "/sprites/games/my-tuno/tuno_attacking_right.png";
    const defenderSpritePath = "/sprites/games/my-tuno/tuno_attacking_left.png";

    // Animation state
    let attackAnimation = null;
    let damageTexts = [];
    let hpBars = { attacker: 100, defender: 100 };
    const ATTACK_LUNGE_FRAMES = 24;
    const ATTACK_LUNGE_DISTANCE = 0.45;
    const ATTACK_RECOIL_DISTANCE = 12;

    export function init(canvasId, dotNetReference) {
        console.log('[liveBattle] init', { canvasId });
        canvas = document.getElementById(canvasId);
        if (!canvas) return;

        ctx = canvas.getContext('2d');
        dotNetRef = dotNetReference;

        // Set canvas size
        canvas.width = 800;
        canvas.height = 500;

        // Load sprite images
        attackerSprite = new Image();
        defenderSprite = new Image();
        
        attackerSprite.onload = function() {
            console.log('[liveBattle] Attacker sprite loaded:', attackerSpritePath);
            if (isAnimating) render();
        };
        attackerSprite.onerror = function() {
            console.error('[liveBattle] Failed to load attacker sprite:', attackerSpritePath);
        };
        
        defenderSprite.onload = function() {
            console.log('[liveBattle] Defender sprite loaded:', defenderSpritePath);
            if (isAnimating) render();
        };
        defenderSprite.onerror = function() {
            console.error('[liveBattle] Failed to load defender sprite:', defenderSpritePath);
        };
        
        attackerSprite.src = attackerSpritePath;
        defenderSprite.src = defenderSpritePath;

        // Start render loop
        render();
    }

    export function startBattle(eventsJson) {
        events = JSON.parse(eventsJson);
        console.log('[liveBattle] startBattle', { eventCount: events.length });
        currentEventIndex = 0;
        isAnimating = true;
        
        // Initialize HP from first HPUpdate events
        initializeHP();
        
        // Start animation
        animateNextEvent();
    }

    function initializeHP() {
        // Find initial HP values (first HPUpdate for each character sets maxHp)
        let attackerInitialized = false;
        let defenderInitialized = false;
        
        for (let i = 0; i < events.length; i++) {
            const evt = events[i];
            if (evt.Type === "HPUpdate") {
                if (evt.Character === "Attacker" && !attackerInitialized) {
                    attacker.maxHp = evt.HP || 100;
                    attacker.hp = evt.HP || 100;
                    hpBars.attacker = evt.HP || 100;
                    attackerInitialized = true;
                } else if (evt.Character === "Defender" && !defenderInitialized) {
                    defender.maxHp = evt.HP || 100;
                    defender.hp = evt.HP || 100;
                    hpBars.defender = evt.HP || 100;
                    defenderInitialized = true;
                }
                
                // Update current HP for subsequent HPUpdate events
                if (evt.Character === "Attacker" && attackerInitialized) {
                    attacker.hp = evt.HP || 0;
                    hpBars.attacker = evt.HP || 0;
                } else if (evt.Character === "Defender" && defenderInitialized) {
                    defender.hp = evt.HP || 0;
                    hpBars.defender = evt.HP || 0;
                }
            }
        }
    }

    function animateNextEvent() {
        if (!isAnimating || currentEventIndex >= events.length) {
            isAnimating = false;
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync('OnBattleFinished');
            }
            return;
        }

        const evt = events[currentEventIndex];
        processEvent(evt);
        currentEventIndex++;

        // Animate this event, then move to next
        eventTimerId = window.setTimeout(() => {
            animateNextEvent();
        }, 800); // 800ms per event for live feel
    }

    function processEvent(evt) {
        if (evt.Type === "HPUpdate") {
            if (evt.Character === "Attacker") {
                attacker.hp = evt.HP || 0;
                hpBars.attacker = evt.HP || 0;
            } else if (evt.Character === "Defender") {
                defender.hp = evt.HP || 0;
                hpBars.defender = evt.HP || 0;
            }
        } else if (evt.Type === "Attack") {
            const attackerChar = evt.Attacker === "Attacker" ? attacker : defender;
            const defenderChar = evt.Defender === "Defender" ? defender : attacker;
            const startX = getCharacterX(attackerChar);
            const targetX = startX + (getCharacterX(defenderChar) - startX) * ATTACK_LUNGE_DISTANCE;

            attackAnimation = {
                attackerChar,
                defenderChar,
                attackerStartX: startX,
                defenderStartX: getCharacterX(defenderChar),
                attackerTargetX: targetX,
                progress: 0,
                duration: ATTACK_LUNGE_FRAMES,
                defenderOffsetX: 0
            };

            if (evt.Damage) {
                damageTexts.push({
                    x: getCharacterX(defenderChar) + defenderChar.width / 2,
                    y: defenderChar.y,
                    damage: evt.Damage,
                    alpha: 1.0,
                    lifetime: 0
                });
            }
        }
    }

    function render() {
        // Clear canvas
        ctx.fillStyle = '#1a1a1a';
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        // Draw arena
        drawArena();

        if (attackAnimation) {
            updateAttackAnimation();
        }

        // Draw characters
        drawCharacter(attacker, true);
        drawCharacter(defender, false);

        // Draw HP bars
        drawHPBar(attacker, 50, 50);
        drawHPBar(defender, 550, 50);

        // Draw attack animation
        if (attackAnimation) {
            drawAttackAnimation();
        }

        // Draw damage texts
        drawDamageTexts();

        // Continue animation loop
        animationFrameId = requestAnimationFrame(render);
    }

    function drawArena() {
        // Ground
        ctx.fillStyle = '#2a2a2a';
        ctx.fillRect(0, canvas.height - 50, canvas.width, 50);

        // Center line
        ctx.strokeStyle = '#444';
        ctx.lineWidth = 2;
        ctx.setLineDash([10, 10]);
        ctx.beginPath();
        ctx.moveTo(canvas.width / 2, 0);
        ctx.lineTo(canvas.width / 2, canvas.height - 50);
        ctx.stroke();
        ctx.setLineDash([]);
    }

    function drawCharacter(char, isLeft) {
        const x = getCharacterX(char);
        const y = char.y;
        const sprite = isLeft ? attackerSprite : defenderSprite;

        // Draw sprite if loaded, otherwise draw placeholder box
        if (sprite && sprite.complete && sprite.naturalWidth > 0) {
            // Calculate sprite dimensions to fit within character bounds
            const spriteAspectRatio = sprite.naturalWidth / sprite.naturalHeight;
            let drawWidth = char.width;
            let drawHeight = char.height;
            
            // Maintain aspect ratio
            if (spriteAspectRatio > drawWidth / drawHeight) {
                drawHeight = drawWidth / spriteAspectRatio;
            } else {
                drawWidth = drawHeight * spriteAspectRatio;
            }
            
            // Center the sprite
            const drawX = x + (char.width - drawWidth) / 2;
            const drawY = y + (char.height - drawHeight) / 2;
            
            ctx.drawImage(sprite, drawX, drawY, drawWidth, drawHeight);
        } else {
            // Fallback: draw placeholder box
            ctx.fillStyle = isLeft ? '#4a9eff' : '#ff4a4a';
            ctx.fillRect(x, y, char.width, char.height);

            // Outline
            ctx.strokeStyle = '#fff';
            ctx.lineWidth = 2;
            ctx.strokeRect(x, y, char.width, char.height);
        }

        // Name
        ctx.fillStyle = '#fff';
        ctx.font = 'bold 16px Arial';
        ctx.textAlign = 'center';
        ctx.fillText(char.name, x + char.width / 2, y - 10);
    }

    function drawHPBar(char, x, y) {
        const barWidth = 200;
        const barHeight = 25;
        const currentHP = char.name === "Attacker" ? hpBars.attacker : hpBars.defender;
        const hpPercent = char.maxHp > 0 ? currentHP / char.maxHp : 0;

        // Background
        ctx.fillStyle = '#333';
        ctx.fillRect(x, y, barWidth, barHeight);

        // HP bar
        ctx.fillStyle = hpPercent > 0.5 ? '#4caf50' : hpPercent > 0.25 ? '#ff9800' : '#f44336';
        ctx.fillRect(x, y, barWidth * hpPercent, barHeight);

        // Border
        ctx.strokeStyle = '#fff';
        ctx.lineWidth = 2;
        ctx.strokeRect(x, y, barWidth, barHeight);

        // HP text
        ctx.fillStyle = '#fff';
        ctx.font = 'bold 14px Arial';
        ctx.textAlign = 'center';
        ctx.fillText(`${currentHP} / ${char.maxHp} HP`, x + barWidth / 2, y + 18);
    }

    function drawAttackAnimation() {
        if (!attackAnimation) return;

        const { attackerChar, defenderChar, progress, duration } = attackAnimation;
        const t = Math.min(progress / duration, 1);
        const from = {
            x: getCharacterX(attackerChar) + attackerChar.width / 2,
            y: attackerChar.y + attackerChar.height / 2
        };
        const to = {
            x: getCharacterX(defenderChar) + defenderChar.width / 2,
            y: defenderChar.y + defenderChar.height / 2
        };

        // Attack line
        ctx.strokeStyle = '#ffeb3b';
        ctx.lineWidth = 4;
        ctx.beginPath();
        ctx.moveTo(from.x, from.y);
        
        const currentX = from.x + (to.x - from.x) * t;
        const currentY = from.y + (to.y - from.y) * t;
        ctx.lineTo(currentX, currentY);
        ctx.stroke();

        // Attack effect
        ctx.fillStyle = 'rgba(255, 235, 59, 0.6)';
        ctx.beginPath();
        ctx.arc(currentX, currentY, 15 * (1 - t), 0, Math.PI * 2);
        ctx.fill();
    }

    function updateAttackAnimation() {
        if (!attackAnimation) return;

        const { attackerChar, defenderChar, attackerStartX, attackerTargetX, defenderStartX, progress, duration } = attackAnimation;
        const t = Math.min(progress / duration, 1);

        const eased = t < 0.5
            ? t * 2
            : (1 - t) * 2;

        attackerChar.offsetX = attackerStartX + (attackerTargetX - attackerStartX) * eased - attackerChar.x;

        const impactWindow = t > 0.45 && t < 0.7;
        if (impactWindow) {
            const recoilT = (t - 0.45) / 0.25;
            const recoilStrength = Math.sin(Math.min(recoilT, 1) * Math.PI);
            const direction = defenderChar === defender ? 1 : -1;
            attackAnimation.defenderOffsetX = recoilStrength * ATTACK_RECOIL_DISTANCE * direction;
        } else {
            attackAnimation.defenderOffsetX = 0;
        }

        defenderChar.offsetX = defenderStartX + attackAnimation.defenderOffsetX - defenderChar.x;

        attackAnimation.progress += 1;
        if (attackAnimation.progress >= duration) {
            attackerChar.offsetX = 0;
            defenderChar.offsetX = 0;
            attackAnimation = null;
        }
    }

    function getCharacterX(char) {
        return char.x + (char.offsetX || 0);
    }

    function drawDamageTexts() {
        damageTexts = damageTexts.filter(text => {
            text.y -= 3;
            text.alpha -= 0.02;
            text.lifetime++;

            if (text.alpha <= 0 || text.lifetime > 60) {
                return false;
            }

            ctx.fillStyle = `rgba(255, 0, 0, ${text.alpha})`;
            ctx.font = 'bold 24px Arial';
            ctx.textAlign = 'center';
            ctx.fillText(`-${text.damage}`, text.x, text.y);

            return true;
        });
    }

    export function stop() {
        isAnimating = false;
        currentEventIndex = 0;
        events = [];
        attackAnimation = null;
        damageTexts = [];
        attacker.offsetX = 0;
        defender.offsetX = 0;
        if (eventTimerId) {
            clearTimeout(eventTimerId);
            eventTimerId = null;
        }
        // Reset HP bars
        hpBars.attacker = attacker.maxHp;
        hpBars.defender = defender.maxHp;
    }

    export function dispose() {
        if (animationFrameId) {
            cancelAnimationFrame(animationFrameId);
            animationFrameId = null;
        }
        if (eventTimerId) {
            clearTimeout(eventTimerId);
            eventTimerId = null;
        }
        stop();
        canvas = null;
        ctx = null;
        dotNetRef = null;
    }

// Expose to global scope for fallback usage
window.liveBattle = {
    init,
    startBattle,
    stop,
    dispose
};
