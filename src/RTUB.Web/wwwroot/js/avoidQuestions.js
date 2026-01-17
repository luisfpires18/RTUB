/**
 * Avoid Questions Game - HTML5 Canvas Game
 * Player controls a Magister character that moves left/right to avoid falling questions.
 */
const avoidQuestionsGame = (function () {
    let canvas = null;
    let ctx = null;
    let dotNetRef = null;
    
    let config = {
        questions: [],
        baseSpawnRate: 1.5,
        baseFallingSpeed: 100,
        spawnRateDecreasePerLevel: 0.1,
        speedIncreasePerLevel: 15,
        levelDurationSeconds: 60,
        maxLives: 5,
        pointsPerDodge: 1
    };
    
    let gameRunning = false;
    let lives = 5;
    let level = 1;
    let points = 0;
    let timeElapsed = 0;
    let maxLevelReached = 1;
    
    const player = {
        x: 0, y: 0, width: 60, height: 80, speed: 350, direction: 0
    };
    
    let questions = [];
    let nextSpawnTime = 0;
    let lastFrameTime = 0;
    let animationId = null;
    let keysPressed = {};
    let playerFlashing = false;
    let flashEndTime = 0;

    function init(canvasId, dotNetReference, configJson) {
        canvas = document.getElementById(canvasId);
        if (!canvas) return;
        
        ctx = canvas.getContext('2d');
        dotNetRef = dotNetReference;
        
        try { config = JSON.parse(configJson); } catch (e) { }
        
        lives = config.maxLives;
        player.x = canvas.width / 2 - player.width / 2;
        player.y = canvas.height - player.height - 20;
        
        setupInputHandlers();
        drawInitialState();
    }

    function setupInputHandlers() {
        document.addEventListener('keydown', handleKeyDown);
        document.addEventListener('keyup', handleKeyUp);
        document.addEventListener('visibilitychange', handleVisibilityChange);
    }

    function handleKeyDown(e) {
        keysPressed[e.key] = true;
        if (gameRunning) {
            if (e.key === 'a' || e.key === 'A' || e.key === 'ArrowLeft') {
                player.direction = -1;
                e.preventDefault();
            } else if (e.key === 'd' || e.key === 'D' || e.key === 'ArrowRight') {
                player.direction = 1;
                e.preventDefault();
            }
        }
    }

    function handleKeyUp(e) {
        keysPressed[e.key] = false;
        if (gameRunning) {
            const leftPressed = keysPressed['a'] || keysPressed['A'] || keysPressed['ArrowLeft'];
            const rightPressed = keysPressed['d'] || keysPressed['D'] || keysPressed['ArrowRight'];
            
            if (!leftPressed && !rightPressed) player.direction = 0;
            else if (leftPressed) player.direction = -1;
            else if (rightPressed) player.direction = 1;
        }
    }

    function handleVisibilityChange() {
        if (document.hidden && gameRunning) {
            cancelAnimationFrame(animationId);
        } else if (!document.hidden && gameRunning) {
            lastFrameTime = performance.now();
            animationId = requestAnimationFrame(gameLoop);
        }
    }

    function startMove(direction) {
        if (gameRunning) player.direction = direction;
    }

    function stopMove() {
        player.direction = 0;
    }

    function drawInitialState() {
        ctx.fillStyle = '#1a1a1b';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        drawPlayer();
    }

    function start() {
        lives = config.maxLives;
        level = 1;
        points = 0;
        timeElapsed = 0;
        maxLevelReached = 1;
        questions = [];
        nextSpawnTime = 0;
        
        player.x = canvas.width / 2 - player.width / 2;
        player.y = canvas.height - player.height - 20;
        player.direction = 0;
        
        gameRunning = true;
        lastFrameTime = performance.now();
        animationId = requestAnimationFrame(gameLoop);
    }

    function gameLoop(currentTime) {
        if (!gameRunning) return;
        
        const deltaTime = (currentTime - lastFrameTime) / 1000;
        lastFrameTime = currentTime;
        const dt = Math.min(deltaTime, 0.1);
        
        update(dt);
        draw();
        updateDotNetStats();
        
        animationId = requestAnimationFrame(gameLoop);
    }

    function update(dt) {
        timeElapsed += dt;
        
        // Progressive level scaling: 30s, 45s, 60s, 75s, etc.
        // Base duration is 30s, increases by 15s per level
        const newLevel = calculateLevel(timeElapsed);
        if (newLevel > level) {
            level = newLevel;
            maxLevelReached = Math.max(maxLevelReached, level);
        }
        
        updatePlayer(dt);
        spawnQuestions(dt);
        updateQuestions(dt);
        checkCollisions();
        
        if (lives <= 0) endGame();
    }
    
    // Calculate level based on progressive time thresholds
    // Level 1->2: 30s, Level 2->3: 45s, Level 3->4: 60s, etc.
    function calculateLevel(elapsed) {
        let cumulativeTime = 0;
        let lvl = 1;
        let baseDuration = 30; // First level duration
        let increment = 15;    // Increase per level
        
        while (true) {
            const levelDuration = baseDuration + (lvl - 1) * increment;
            if (elapsed < cumulativeTime + levelDuration) {
                return lvl;
            }
            cumulativeTime += levelDuration;
            lvl++;
            // Safety cap to prevent infinite loop
            if (lvl > 100) return lvl;
        }
    }

    function updatePlayer(dt) {
        const newX = player.x + player.direction * player.speed * dt;
        player.x = Math.max(0, Math.min(canvas.width - player.width, newX));
    }

    function spawnQuestions(dt) {
        nextSpawnTime -= dt;
        
        if (nextSpawnTime <= 0) {
            const spawnRate = Math.max(0.3, config.baseSpawnRate - (level - 1) * config.spawnRateDecreasePerLevel);
            nextSpawnTime = spawnRate;
            
            const questionText = config.questions[Math.floor(Math.random() * config.questions.length)] || '?';
            const width = Math.min(ctx.measureText(questionText).width + 40, 200);
            
            questions.push({
                x: Math.random() * (canvas.width - width),
                y: -60,
                width: Math.max(60, width),
                height: 50,
                text: questionText,
                speed: config.baseFallingSpeed + (level - 1) * config.speedIncreasePerLevel,
                counted: false
            });
        }
    }

    function updateQuestions(dt) {
        for (let i = questions.length - 1; i >= 0; i--) {
            const q = questions[i];
            q.y += q.speed * dt;
            
            if (q.y > canvas.height) {
                if (!q.counted) {
                    points += config.pointsPerDodge;
                    q.counted = true;
                }
                questions.splice(i, 1);
            }
        }
    }

    function checkCollisions() {
        const playerRect = {
            x: player.x + 5, y: player.y + 5,
            width: player.width - 10, height: player.height - 10
        };
        
        for (let i = questions.length - 1; i >= 0; i--) {
            const q = questions[i];
            if (rectsIntersect(playerRect, q)) {
                lives--;
                questions.splice(i, 1);
                flashPlayer();
            }
        }
    }

    function rectsIntersect(a, b) {
        return a.x < b.x + b.width && a.x + a.width > b.x &&
               a.y < b.y + b.height && a.y + a.height > b.y;
    }

    function flashPlayer() {
        playerFlashing = true;
        flashEndTime = performance.now() + 300;
    }

    function draw() {
        ctx.fillStyle = '#0a0a0a';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        drawBackground();
        drawQuestions();
        drawPlayer();
    }

    function drawBackground() {
        const gradient = ctx.createLinearGradient(0, 0, 0, canvas.height);
        gradient.addColorStop(0, '#1a1a2e');
        gradient.addColorStop(0.5, '#16213e');
        gradient.addColorStop(1, '#0f3460');
        ctx.fillStyle = gradient;
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        
        ctx.fillStyle = 'rgba(255, 255, 255, 0.5)';
        for (let i = 0; i < 50; i++) {
            const x = (i * 137) % canvas.width;
            const y = (i * 73) % (canvas.height * 0.6);
            ctx.beginPath();
            ctx.arc(x, y, 1, 0, Math.PI * 2);
            ctx.fill();
        }
    }

    function drawRoundRect(x, y, width, height, radius) {
        // Polyfill for roundRect (Safari < 15.4, older browsers)
        if (ctx.roundRect) {
            ctx.roundRect(x, y, width, height, radius);
        } else {
            ctx.moveTo(x + radius, y);
            ctx.lineTo(x + width - radius, y);
            ctx.arcTo(x + width, y, x + width, y + radius, radius);
            ctx.lineTo(x + width, y + height - radius);
            ctx.arcTo(x + width, y + height, x + width - radius, y + height, radius);
            ctx.lineTo(x + radius, y + height);
            ctx.arcTo(x, y + height, x, y + height - radius, radius);
            ctx.lineTo(x, y + radius);
            ctx.arcTo(x, y, x + radius, y, radius);
        }
    }

    function drawQuestions() {
        for (const q of questions) {
            ctx.fillStyle = '#e94560';
            ctx.beginPath();
            drawRoundRect(q.x, q.y, q.width, q.height, 8);
            ctx.fill();

            const padding = 8;
            const maxWidth = q.width - padding * 2;
            const maxHeight = q.height - padding * 2;
            const fontResult = fitTextToBox(q.text, maxWidth, maxHeight, 12, 9);

            ctx.fillStyle = '#ffffff';
            ctx.font = `bold ${fontResult.fontSize}px Arial`;
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';

            const lineHeight = fontResult.fontSize + 2;
            const totalHeight = fontResult.lines.length * lineHeight;
            const startY = q.y + q.height / 2 - totalHeight / 2 + lineHeight / 2;
            fontResult.lines.forEach((line, index) => {
                ctx.fillText(line, q.x + q.width / 2, startY + index * lineHeight);
            });
        }
    }

    function fitTextToBox(text, maxWidth, maxHeight, startSize, minSize) {
        let fontSize = startSize;
        let lines = [];

        while (fontSize >= minSize) {
            ctx.font = `bold ${fontSize}px Arial`;
            lines = wrapText(text, maxWidth);
            const lineHeight = fontSize + 2;
            if (lines.length * lineHeight <= maxHeight) {
                return { fontSize, lines };
            }
            fontSize -= 1;
        }

        ctx.font = `bold ${minSize}px Arial`;
        lines = wrapText(text, maxWidth);
        const maxLines = Math.max(1, Math.floor(maxHeight / (minSize + 2)));
        if (lines.length > maxLines) {
            lines = lines.slice(0, maxLines);
            const lastIndex = lines.length - 1;
            let trimmed = lines[lastIndex];
            while (ctx.measureText(`${trimmed}...`).width > maxWidth && trimmed.length > 0) {
                trimmed = trimmed.slice(0, -1);
            }
            lines[lastIndex] = `${trimmed}...`;
        }

        return { fontSize: minSize, lines };
    }

    function wrapText(text, maxWidth) {
        const words = text.split(' ');
        const lines = [];
        let currentLine = '';

        words.forEach((word) => {
            const testLine = currentLine ? `${currentLine} ${word}` : word;
            if (ctx.measureText(testLine).width <= maxWidth) {
                currentLine = testLine;
                return;
            }

            if (currentLine) {
                lines.push(currentLine);
            }
            currentLine = word;
        });

        if (currentLine) {
            lines.push(currentLine);
        }

        return lines;
    }

    function drawPlayer() {
        if (playerFlashing) {
            if (performance.now() < flashEndTime) {
                if (Math.floor((performance.now() - flashEndTime + 300) / 50) % 2 === 0) return;
            } else {
                playerFlashing = false;
            }
        }
        
        const x = player.x, y = player.y, w = player.width, h = player.height;
        
        // Robe
        ctx.fillStyle = '#2d1a4a';
        ctx.beginPath();
        ctx.moveTo(x + w * 0.2, y + h * 0.3);
        ctx.lineTo(x + w * 0.1, y + h);
        ctx.lineTo(x + w * 0.9, y + h);
        ctx.lineTo(x + w * 0.8, y + h * 0.3);
        ctx.closePath();
        ctx.fill();
        
        // Sashes
        ctx.strokeStyle = '#006600';
        ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(x + w * 0.3, y + h * 0.35);
        ctx.lineTo(x + w * 0.2, y + h * 0.8);
        ctx.stroke();
        
        ctx.strokeStyle = '#cc0000';
        ctx.beginPath();
        ctx.moveTo(x + w * 0.7, y + h * 0.35);
        ctx.lineTo(x + w * 0.8, y + h * 0.8);
        ctx.stroke();
        
        // Head
        ctx.fillStyle = '#f5deb3';
        ctx.beginPath();
        ctx.arc(x + w / 2, y + h * 0.2, w * 0.22, 0, Math.PI * 2);
        ctx.fill();
        
        // Hair
        ctx.fillStyle = '#4a3728';
        ctx.beginPath();
        ctx.ellipse(x + w * 0.75, y + h * 0.22, w * 0.08, h * 0.12, 0.3, 0, Math.PI * 2);
        ctx.fill();
        ctx.beginPath();
        ctx.arc(x + w / 2, y + h * 0.15, w * 0.18, Math.PI, 0);
        ctx.fill();
        
        // Eyes
        ctx.fillStyle = '#333333';
        ctx.beginPath();
        ctx.arc(x + w * 0.4, y + h * 0.18, 2, 0, Math.PI * 2);
        ctx.arc(x + w * 0.6, y + h * 0.18, 2, 0, Math.PI * 2);
        ctx.fill();
        
        // Smile
        ctx.strokeStyle = '#333333';
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.arc(x + w / 2, y + h * 0.22, w * 0.08, 0.2, Math.PI - 0.2);
        ctx.stroke();
        
        // Collar
        ctx.fillStyle = '#ffffff';
        ctx.beginPath();
        ctx.moveTo(x + w * 0.35, y + h * 0.32);
        ctx.lineTo(x + w / 2, y + h * 0.4);
        ctx.lineTo(x + w * 0.65, y + h * 0.32);
        ctx.closePath();
        ctx.fill();
    }

    function updateDotNetStats() {
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('UpdateStats', lives, level, points, timeElapsed);
        }
    }

    function endGame() {
        gameRunning = false;
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnGameOver', points, maxLevelReached, timeElapsed);
        }
    }

    function dispose() {
        gameRunning = false;
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }
        document.removeEventListener('keydown', handleKeyDown);
        document.removeEventListener('keyup', handleKeyUp);
        document.removeEventListener('visibilitychange', handleVisibilityChange);
        dotNetRef = null;
    }

    return { init, start, startMove, stopMove, dispose };
})();

window.avoidQuestionsGame = avoidQuestionsGame;
