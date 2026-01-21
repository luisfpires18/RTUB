/**
 * Passaro Maluco - Flappy Bird Clone
 * A simple HTML5 Canvas game where you control a bird and avoid pipes.
 * Pipe sprites can be customized in wwwroot/sprites/games/passaro-maluco/
 */
const passaroMalucoGame = (function () {
    let canvas = null;
    let ctx = null;
    let dotNetRef = null;
    
    // Sprite configuration - easily changeable
    const sprites = {
        pipeUrl: '/sprites/games/passaro-maluco/pipe.svg',
        birdUrl: '/sprites/games/passaro-maluco/bird.svg'
    };
    
    // Loaded images
    let pipeImage = null;
    let birdImage = null;
    let imagesLoaded = false;
    
    // Game configuration
    const config = {
        gravity: 0.08,
        jumpStrength: -4,
        pipeWidth: 52,
        pipeGap: 250,
        pipeSpeed: 1,
        pipeSpawnRate: 3000, // milliseconds
        groundHeight: 50,
        maxDeltaTime: 0.1,           // Cap delta time to prevent large jumps
        collisionPadding: 5,         // Padding for collision box
        pipeCapHeight: 26,           // Height of pipe cap
        pipeCapExtraWidth: 6         // Extra width of pipe cap
    };
    
    // Game state
    let gameRunning = false;
    let score = 0;
    let highScore = 0;
    
    // Bird state
    const bird = {
        x: 80,
        y: 0,
        width: 40,
        height: 40,
        velocity: 0,
        rotation: 0
    };
    
    // Pipes array
    let pipes = [];
    let lastPipeSpawn = 0;
    
    // Animation
    let animationId = null;
    let lastFrameTime = 0;
    
    // Wing animation
    let wingFrame = 0;
    let wingTimer = 0;

    function init(canvasId, dotNetReference) {
        canvas = document.getElementById(canvasId);
        if (!canvas) return;
        
        ctx = canvas.getContext('2d');
        dotNetRef = dotNetReference;
        
        // Center bird vertically
        bird.y = canvas.height / 2 - bird.height / 2;
        
        // Load high score from localStorage
        const savedHighScore = localStorage.getItem('passaroMaluco_highScore');
        if (savedHighScore) {
            highScore = parseInt(savedHighScore, 10);
        }
        
        // Load sprites
        loadSprites().then(() => {
            imagesLoaded = true;
            drawInitialState();
        });
        
        // Setup input handlers
        setupInputHandlers();
    }

    function loadSprites() {
        return new Promise((resolve) => {
            let loadedCount = 0;
            const totalImages = 2;
            
            const onLoad = () => {
                loadedCount++;
                if (loadedCount >= totalImages) {
                    resolve();
                }
            };
            
            pipeImage = new Image();
            pipeImage.onload = onLoad;
            pipeImage.onerror = onLoad; // Continue even if image fails
            pipeImage.src = sprites.pipeUrl;
            
            birdImage = new Image();
            birdImage.onload = onLoad;
            birdImage.onerror = onLoad;
            birdImage.src = sprites.birdUrl;
        });
    }

    function setupInputHandlers() {
        // Keyboard
        document.addEventListener('keydown', handleKeyDown);
        
        // Mouse/Touch on canvas
        if (canvas) {
            canvas.addEventListener('click', handleClick);
            canvas.addEventListener('touchstart', handleTouch, { passive: false });
        }
        
        // Visibility change
        document.addEventListener('visibilitychange', handleVisibilityChange);
    }

    function handleKeyDown(e) {
        if (e.code === 'Space' || e.key === ' ' || e.key === 'ArrowUp') {
            e.preventDefault();
            if (gameRunning) {
                jump();
            }
        }
    }

    function handleClick(e) {
        if (gameRunning) {
            jump();
        }
    }

    function handleTouch(e) {
        e.preventDefault();
        if (gameRunning) {
            jump();
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

    function jump() {
        bird.velocity = config.jumpStrength;
    }

    // Called from Blazor to make the bird jump
    function triggerJump() {
        if (gameRunning) {
            jump();
        }
    }

    function drawInitialState() {
        draw();
    }

    function start() {
        // Reset game state
        score = 0;
        pipes = [];
        lastPipeSpawn = 0;
        
        // Reset bird
        bird.y = canvas.height / 2 - bird.height / 2;
        bird.velocity = 0;
        bird.rotation = 0;
        
        gameRunning = true;
        lastFrameTime = performance.now();
        animationId = requestAnimationFrame(gameLoop);
    }

    function gameLoop(currentTime) {
        if (!gameRunning) return;
        
        const deltaTime = (currentTime - lastFrameTime) / 1000;
        lastFrameTime = currentTime;
        const dt = Math.min(deltaTime, config.maxDeltaTime);
        
        update(dt, currentTime);
        draw();
        updateDotNetStats();
        
        animationId = requestAnimationFrame(gameLoop);
    }

    function update(dt, currentTime) {
        // Update bird physics
        bird.velocity += config.gravity;
        bird.y += bird.velocity;
        
        // Update bird rotation based on velocity
        bird.rotation = Math.min(Math.max(bird.velocity * 3, -30), 90);
        
        // Update wing animation
        wingTimer += dt * 1000;
        if (wingTimer > 100) {
            wingFrame = (wingFrame + 1) % 3;
            wingTimer = 0;
        }
        
        // Spawn pipes
        if (currentTime - lastPipeSpawn > config.pipeSpawnRate) {
            spawnPipe();
            lastPipeSpawn = currentTime;
        }
        
        // Update pipes
        for (let i = pipes.length - 1; i >= 0; i--) {
            const pipe = pipes[i];
            pipe.x -= config.pipeSpeed;
            
            // Check if bird passed the pipe
            if (!pipe.passed && pipe.x + config.pipeWidth < bird.x) {
                pipe.passed = true;
                score++;
            }
            
            // Remove off-screen pipes
            if (pipe.x + config.pipeWidth < 0) {
                pipes.splice(i, 1);
            }
        }
        
        // Check collisions
        if (checkCollisions()) {
            endGame();
        }
    }

    function spawnPipe() {
        const minHeight = 50;
        const maxHeight = canvas.height - config.groundHeight - config.pipeGap - minHeight;
        const topHeight = Math.random() * (maxHeight - minHeight) + minHeight;
        
        pipes.push({
            x: canvas.width,
            topHeight: topHeight,
            bottomY: topHeight + config.pipeGap,
            passed: false
        });
    }

    function checkCollisions() {
        // Ground collision
        if (bird.y + bird.height > canvas.height - config.groundHeight) {
            return true;
        }
        
        // Ceiling collision
        if (bird.y < 0) {
            return true;
        }
        
        // Pipe collisions
        const birdBox = {
            x: bird.x + config.collisionPadding,
            y: bird.y + config.collisionPadding,
            width: bird.width - config.collisionPadding * 2,
            height: bird.height - config.collisionPadding * 2
        };
        
        for (const pipe of pipes) {
            // Top pipe
            const topPipe = {
                x: pipe.x,
                y: 0,
                width: config.pipeWidth,
                height: pipe.topHeight
            };
            
            // Bottom pipe
            const bottomPipe = {
                x: pipe.x,
                y: pipe.bottomY,
                width: config.pipeWidth,
                height: canvas.height - pipe.bottomY - config.groundHeight
            };
            
            if (rectsIntersect(birdBox, topPipe) || rectsIntersect(birdBox, bottomPipe)) {
                return true;
            }
        }
        
        return false;
    }

    function rectsIntersect(a, b) {
        return a.x < b.x + b.width &&
               a.x + a.width > b.x &&
               a.y < b.y + b.height &&
               a.y + a.height > b.y;
    }

    function draw() {
        // Clear and draw background
        drawBackground();
        
        // Draw pipes
        drawPipes();
        
        // Draw ground
        drawGround();
        
        // Draw bird
        drawBird();
        
        // Draw score (in-game)
        if (gameRunning) {
            drawScore();
        }
    }

    function drawBackground() {
        // Sky gradient
        const gradient = ctx.createLinearGradient(0, 0, 0, canvas.height - config.groundHeight);
        gradient.addColorStop(0, '#87CEEB');
        gradient.addColorStop(0.5, '#4A90D9');
        gradient.addColorStop(1, '#5B9BD5');
        ctx.fillStyle = gradient;
        ctx.fillRect(0, 0, canvas.width, canvas.height - config.groundHeight);
        
        // Clouds
        ctx.fillStyle = 'rgba(255, 255, 255, 0.8)';
        drawCloud(100, 80, 40);
        drawCloud(300, 120, 50);
        drawCloud(500, 60, 35);
        drawCloud(700, 100, 45);
    }

    function drawCloud(x, y, size) {
        ctx.beginPath();
        ctx.arc(x, y, size, 0, Math.PI * 2);
        ctx.arc(x + size * 0.6, y - size * 0.2, size * 0.7, 0, Math.PI * 2);
        ctx.arc(x + size * 1.2, y, size * 0.8, 0, Math.PI * 2);
        ctx.fill();
    }

    function drawGround() {
        // Dirt
        ctx.fillStyle = '#8B7355';
        ctx.fillRect(0, canvas.height - config.groundHeight, canvas.width, config.groundHeight);
        
        // Grass
        ctx.fillStyle = '#90EE90';
        ctx.fillRect(0, canvas.height - config.groundHeight, canvas.width, 10);
        
        // Grass pattern
        ctx.fillStyle = '#7CCD7C';
        for (let i = 0; i < canvas.width; i += 20) {
            ctx.fillRect(i, canvas.height - config.groundHeight, 3, 10);
        }
    }

    function drawPipes() {
        for (const pipe of pipes) {
            if (imagesLoaded && pipeImage.complete && pipeImage.naturalWidth > 0) {
                // Draw top pipe (flipped)
                ctx.save();
                ctx.translate(pipe.x + config.pipeWidth / 2, pipe.topHeight);
                ctx.scale(1, -1);
                ctx.drawImage(pipeImage, -config.pipeWidth / 2, 0, config.pipeWidth, pipe.topHeight);
                ctx.restore();
                
                // Draw bottom pipe
                const bottomHeight = canvas.height - pipe.bottomY - config.groundHeight;
                ctx.drawImage(pipeImage, pipe.x, pipe.bottomY, config.pipeWidth, bottomHeight);
            } else {
                // Fallback: Draw pipes as rectangles
                drawPipeFallback(pipe);
            }
        }
    }

    function drawPipeFallback(pipe) {
        const capHeight = config.pipeCapHeight;
        const capExtraWidth = config.pipeCapExtraWidth;
        
        // Top pipe
        ctx.fillStyle = '#73bf2e';
        ctx.fillRect(pipe.x, 0, config.pipeWidth, pipe.topHeight - capHeight);
        
        // Top pipe cap
        ctx.fillRect(pipe.x - capExtraWidth, pipe.topHeight - capHeight, config.pipeWidth + capExtraWidth * 2, capHeight);
        
        // Top pipe highlight
        ctx.fillStyle = '#8fd14f';
        ctx.fillRect(pipe.x, 0, 8, pipe.topHeight - capHeight);
        ctx.fillRect(pipe.x - capExtraWidth, pipe.topHeight - capHeight, 8, capHeight);
        
        // Top pipe shadow
        ctx.fillStyle = '#5a9b24';
        ctx.fillRect(pipe.x + config.pipeWidth - 8, 0, 8, pipe.topHeight - capHeight);
        ctx.fillRect(pipe.x + config.pipeWidth + capExtraWidth - 8, pipe.topHeight - capHeight, 8, capHeight);
        
        // Bottom pipe
        const bottomHeight = canvas.height - pipe.bottomY - config.groundHeight;
        
        ctx.fillStyle = '#73bf2e';
        ctx.fillRect(pipe.x, pipe.bottomY + capHeight, config.pipeWidth, bottomHeight - capHeight);
        
        // Bottom pipe cap
        ctx.fillRect(pipe.x - capExtraWidth, pipe.bottomY, config.pipeWidth + capExtraWidth * 2, capHeight);
        
        // Bottom pipe highlight
        ctx.fillStyle = '#8fd14f';
        ctx.fillRect(pipe.x, pipe.bottomY + capHeight, 8, bottomHeight - capHeight);
        ctx.fillRect(pipe.x - capExtraWidth, pipe.bottomY, 8, capHeight);
        
        // Bottom pipe shadow
        ctx.fillStyle = '#5a9b24';
        ctx.fillRect(pipe.x + config.pipeWidth - 8, pipe.bottomY + capHeight, 8, bottomHeight - capHeight);
        ctx.fillRect(pipe.x + config.pipeWidth + capExtraWidth - 8, pipe.bottomY, 8, capHeight);
    }

    function drawBird() {
        ctx.save();
        ctx.translate(bird.x + bird.width / 2, bird.y + bird.height / 2);
        ctx.rotate(bird.rotation * Math.PI / 180);
        
        if (imagesLoaded && birdImage.complete && birdImage.naturalWidth > 0) {
            ctx.drawImage(birdImage, -bird.width / 2, -bird.height / 2, bird.width, bird.height);
        } else {
            // Fallback: Draw bird as shapes
            drawBirdFallback();
        }
        
        ctx.restore();
    }

    function drawBirdFallback() {
        const w = bird.width;
        const h = bird.height;
        
        // Body
        ctx.fillStyle = '#f7dc6f';
        ctx.beginPath();
        ctx.ellipse(0, 2, w / 2.5, h / 2.8, 0, 0, Math.PI * 2);
        ctx.fill();
        
        // Wing
        const wingOffset = [0, -2, 2][wingFrame];
        ctx.fillStyle = '#e8c547';
        ctx.beginPath();
        ctx.ellipse(-w / 5, 4 + wingOffset, w / 5, h / 6, 0, 0, Math.PI * 2);
        ctx.fill();
        
        // Eye white
        ctx.fillStyle = 'white';
        ctx.beginPath();
        ctx.arc(w / 10, -h / 10, h / 6, 0, Math.PI * 2);
        ctx.fill();
        
        // Eye pupil
        ctx.fillStyle = '#333';
        ctx.beginPath();
        ctx.arc(w / 8, -h / 10, h / 10, 0, Math.PI * 2);
        ctx.fill();
        
        // Beak
        ctx.fillStyle = '#ff6b35';
        ctx.beginPath();
        ctx.moveTo(w / 4, 0);
        ctx.lineTo(w / 2, 2);
        ctx.lineTo(w / 4, 4);
        ctx.closePath();
        ctx.fill();
    }

    function drawScore() {
        ctx.fillStyle = 'white';
        ctx.strokeStyle = 'black';
        ctx.lineWidth = 3;
        ctx.font = 'bold 48px Arial';
        ctx.textAlign = 'center';
        ctx.strokeText(score.toString(), canvas.width / 2, 60);
        ctx.fillText(score.toString(), canvas.width / 2, 60);
    }

    function updateDotNetStats() {
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('UpdateStats', score);
        }
    }

    function endGame() {
        gameRunning = false;
        
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }
        
        // Update high score
        if (score > highScore) {
            highScore = score;
            localStorage.setItem('passaroMaluco_highScore', highScore.toString());
        }
        
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnGameOver', score, highScore);
        }
    }

    function getHighScore() {
        return highScore;
    }

    function dispose() {
        gameRunning = false;
        
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }
        
        document.removeEventListener('keydown', handleKeyDown);
        document.removeEventListener('visibilitychange', handleVisibilityChange);
        
        if (canvas) {
            canvas.removeEventListener('click', handleClick);
            canvas.removeEventListener('touchstart', handleTouch);
        }
        
        dotNetRef = null;
    }

    return {
        init,
        start,
        triggerJump,
        getHighScore,
        dispose
    };
})();

window.passaroMalucoGame = passaroMalucoGame;
