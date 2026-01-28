/**
 * Replay Viewer - Canvas-based battle replay animation
 * Renders combat events on a canvas with play/pause/step controls
 */
const replayViewer = (function () {
    let canvas = null;
    let ctx = null;
    let dotNetRef = null;
    let events = [];
    let currentEventIndex = 0;
    let isPlaying = false;
    let playbackSpeed = 1.0;
    let animationFrameId = null;
    let lastUpdateTime = 0;
    let eventTimer = 0;

    // Character positions and states
    const attacker = {
        x: 150,
        y: 200,
        width: 80,
        height: 120,
        hp: 100,
        maxHp: 100,
        name: "Attacker"
    };

    const defender = {
        x: 650,
        y: 200,
        width: 80,
        height: 120,
        hp: 100,
        maxHp: 100,
        name: "Defender"
    };

    // Animation state
    let attackAnimation = null;
    let damageTexts = [];

    function init(canvasId, dotNetReference, eventsJson) {
        canvas = document.getElementById(canvasId);
        if (!canvas) return;

        ctx = canvas.getContext('2d');
        dotNetRef = dotNetReference;
        events = JSON.parse(eventsJson);

        // Set canvas size
        canvas.width = 800;
        canvas.height = 500;

        // Initialize character HP from first HPUpdate events
        initializeCharacterHP();

        // Initialize character states from current event
        updateCharacterStates(0);
        
        // Start render loop
        lastUpdateTime = performance.now();
        render();
    }

    function initializeCharacterHP() {
        // Find initial HP values from events
        for (let i = 0; i < events.length; i++) {
            const evt = events[i];
            if (evt.Type === "HPUpdate") {
                if (evt.Character === "Attacker" && attacker.maxHp === 100) {
                    attacker.maxHp = evt.HP || 100;
                    attacker.hp = evt.HP || 100;
                } else if (evt.Character === "Defender" && defender.maxHp === 100) {
                    defender.maxHp = evt.HP || 100;
                    defender.hp = evt.HP || 100;
                }
            }
        }
    }

    function updateCharacterStates(eventIndex) {
        // Reset to initial HP
        initializeCharacterHP();
        
        // Process all events up to current index to get accurate state
        for (let i = 0; i <= eventIndex && i < events.length; i++) {
            const evt = events[i];
            
            if (evt.Type === "HPUpdate") {
                if (evt.Character === "Attacker") {
                    attacker.hp = evt.HP || 0;
                } else if (evt.Character === "Defender") {
                    defender.hp = evt.HP || 0;
                }
            }
        }
    }

    function render() {
        // Clear canvas
        ctx.fillStyle = '#1a1a1a';
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        // Draw arena background
        drawArena();

        // Draw characters
        drawCharacter(attacker, true);
        drawCharacter(defender, false);

        // Draw HP bars
        drawHPBar(attacker, 50, 50);
        drawHPBar(defender, 550, 50);

        // Draw attack animation if active
        if (attackAnimation) {
            drawAttackAnimation();
        }

        // Draw damage texts
        drawDamageTexts();

        // Draw current event info
        if (currentEventIndex < events.length) {
            const evt = events[currentEventIndex];
            drawEventInfo(evt);
        }

        // Continue animation loop
        animationFrameId = requestAnimationFrame(render);
    }

    function drawArena() {
        // Draw ground
        ctx.fillStyle = '#2a2a2a';
        ctx.fillRect(0, canvas.height - 50, canvas.width, 50);

        // Draw center line
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
        const x = char.x;
        const y = char.y;

        // Character body (simple rectangle)
        ctx.fillStyle = isLeft ? '#4a9eff' : '#ff4a4a';
        ctx.fillRect(x, y, char.width, char.height);

        // Character outline
        ctx.strokeStyle = '#fff';
        ctx.lineWidth = 2;
        ctx.strokeRect(x, y, char.width, char.height);

        // Character name
        ctx.fillStyle = '#fff';
        ctx.font = '14px Arial';
        ctx.textAlign = 'center';
        ctx.fillText(char.name, x + char.width / 2, y - 10);
    }

    function drawHPBar(char, x, y) {
        const barWidth = 200;
        const barHeight = 20;
        const hpPercent = char.maxHp > 0 ? char.hp / char.maxHp : 0;

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
        ctx.fillText(`${char.hp} / ${char.maxHp} HP`, x + barWidth / 2, y + 15);
    }

    function drawAttackAnimation() {
        if (!attackAnimation) return;

        const { from, to, progress } = attackAnimation;
        const t = Math.min(progress, 1);

        // Draw attack line
        ctx.strokeStyle = '#ffeb3b';
        ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(from.x, from.y);
        
        // Animate attack line
        const currentX = from.x + (to.x - from.x) * t;
        const currentY = from.y + (to.y - from.y) * t;
        ctx.lineTo(currentX, currentY);
        ctx.stroke();

        // Draw attack effect
        ctx.fillStyle = 'rgba(255, 235, 59, 0.5)';
        ctx.beginPath();
        ctx.arc(currentX, currentY, 10 * (1 - t), 0, Math.PI * 2);
        ctx.fill();

        // Update progress
        attackAnimation.progress += 0.1;
        if (attackAnimation.progress >= 1) {
            attackAnimation = null;
        }
    }

    function drawDamageTexts() {
        damageTexts = damageTexts.filter(text => {
            text.y -= 2;
            text.alpha -= 0.02;

            if (text.alpha <= 0) {
                return false;
            }

            ctx.fillStyle = `rgba(255, 0, 0, ${text.alpha})`;
            ctx.font = 'bold 20px Arial';
            ctx.textAlign = 'center';
            ctx.fillText(`-${text.damage}`, text.x, text.y);

            return true;
        });
    }

    function drawEventInfo(evt) {
        ctx.fillStyle = 'rgba(0, 0, 0, 0.7)';
        ctx.fillRect(10, canvas.height - 80, canvas.width - 20, 70);

        ctx.fillStyle = '#fff';
        ctx.font = '14px Arial';
        ctx.textAlign = 'left';
        ctx.fillText(`Event: ${evt.Type}`, 20, canvas.height - 60);
        
        if (evt.Round) {
            ctx.fillText(`Round: ${evt.Round}`, 20, canvas.height - 40);
        }
        
        ctx.fillText(`Timestamp: ${evt.Timestamp}`, 20, canvas.height - 20);
    }

    function processEvent(eventIndex) {
        if (eventIndex < 0 || eventIndex >= events.length) return;

        const evt = events[eventIndex];
        updateCharacterStates(eventIndex);

        // Handle attack animation
        if (evt.Type === "Attack") {
            const attackerChar = evt.Attacker === "Attacker" ? attacker : defender;
            const defenderChar = evt.Defender === "Defender" ? defender : attacker;

            attackAnimation = {
                from: { x: attackerChar.x + attackerChar.width / 2, y: attackerChar.y + attackerChar.height / 2 },
                to: { x: defenderChar.x + defenderChar.width / 2, y: defenderChar.y + defenderChar.height / 2 },
                progress: 0
            };

            // Add damage text
            if (evt.Damage) {
                damageTexts.push({
                    x: defenderChar.x + defenderChar.width / 2,
                    y: defenderChar.y,
                    damage: evt.Damage,
                    alpha: 1.0
                });
            }
        }
    }

    function setPlaying(playing) {
        isPlaying = playing;
        if (isPlaying) {
            lastUpdateTime = performance.now();
            eventTimer = 0;
        }
    }

    function setSpeed(speed) {
        playbackSpeed = speed;
    }

    function jumpToEvent(index) {
        if (index < 0 || index >= events.length) return;
        
        currentEventIndex = index;
        updateCharacterStates(index);
        processEvent(index);
        
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnEventChanged', index);
        }
    }

    function update(deltaTime) {
        if (!isPlaying || currentEventIndex >= events.length) {
            if (currentEventIndex >= events.length && isPlaying) {
                isPlaying = false;
                if (dotNetRef) {
                    dotNetRef.invokeMethodAsync('OnPlaybackFinished');
                }
            }
            return;
        }

        // Advance to next event based on speed
        eventTimer += deltaTime * playbackSpeed;
        const eventInterval = 1000; // 1 second per event at 1x speed

        if (eventTimer >= eventInterval) {
            eventTimer = 0;
            currentEventIndex++;
            
            if (currentEventIndex < events.length) {
                processEvent(currentEventIndex);
                if (dotNetRef) {
                    dotNetRef.invokeMethodAsync('OnEventChanged', currentEventIndex);
                }
            }
        }
    }

    // Animation loop
    function animate() {
        const currentTime = performance.now();
        const deltaTime = (currentTime - lastUpdateTime) / 1000; // Convert to seconds
        lastUpdateTime = currentTime;

        update(deltaTime);
    }

    // Start animation loop
    setInterval(animate, 16); // ~60 FPS

    function dispose() {
        if (animationFrameId) {
            cancelAnimationFrame(animationFrameId);
        }
        canvas = null;
        ctx = null;
        dotNetRef = null;
        events = [];
    }

    return {
        init,
        setPlaying,
        setSpeed,
        jumpToEvent,
        dispose
    };
})();
