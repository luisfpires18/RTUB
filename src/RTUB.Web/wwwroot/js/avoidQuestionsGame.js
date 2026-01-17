// Avoid Questions Game - JavaScript Module
// A dodging game where the player avoids falling questions

let gameState = {
    canvas: null,
    ctx: null,
    dotNetHelper: null,
    isRunning: false,
    isPaused: false,
    animationFrameId: null,
    
    // Game objects
    player: {
        x: 0,
        y: 0,
        width: 40,
        height: 60,
        speed: 5,
        direction: 0, // -1 = left, 0 = stop, 1 = right
        color: '#8b4789', // Purple for traje
        speedMultiplier: 100 // Convert speed units to pixels per second for frame-rate independence
    },
    
    questions: [],
    
    // Game stats
    score: 0,
    level: 1,
    lives: 5,
    startTime: 0,
    levelStartTime: 0,
    
    // Difficulty settings
    baseQuestionSpeed: 2,
    questionSpeedMultiplier: 60, // Convert speed units to pixels per second for deltaTime
    questionSpawnRate: 1500, // ms between spawns
    lastQuestionSpawn: 0,
    
    // Question texts
    questionTexts: [
        "Quanto é a quota?",
        "Quando é o próximo ensaio?",
        "Onde é a atuação?",
        "Vais ao ensaio?",
        "Pagaste a quota?",
        "Tens o traje?",
        "Quem é o Magister?"
    ],
    
    // Sprites (optional)
    sprites: {
        player: null,
        question: null,
        background: null
    },
    
    // Last update time for delta calculation
    lastTime: 0
};

// Initialize the game
export function initGame(canvasId, dotNetHelper) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) {
        console.error('Canvas not found:', canvasId);
        return;
    }
    
    gameState.canvas = canvas;
    gameState.ctx = canvas.getContext('2d');
    gameState.dotNetHelper = dotNetHelper;
    
    // Set canvas size
    resizeCanvas();
    window.addEventListener('resize', resizeCanvas);
    
    // Set up keyboard controls
    document.addEventListener('keydown', handleKeyDown);
    document.addEventListener('keyup', handleKeyUp);
    
    // Try to load sprites (optional)
    loadSprites();
    
    // Initialize player position
    resetPlayerPosition();
    
    // Draw initial state
    drawGame();
    
    console.log('Avoid Questions Game initialized');
}

// Resize canvas to fit container
function resizeCanvas() {
    if (!gameState.canvas) return;
    
    const container = gameState.canvas.parentElement;
    const isMobile = window.innerWidth < 768;
    
    gameState.canvas.width = container.clientWidth;
    gameState.canvas.height = isMobile ? 400 : 500;
    
    // Reset player position on resize
    if (gameState.player) {
        resetPlayerPosition();
    }
}

// Reset player to starting position
function resetPlayerPosition() {
    if (!gameState.canvas) return;
    
    gameState.player.x = gameState.canvas.width / 2 - gameState.player.width / 2;
    gameState.player.y = gameState.canvas.height - gameState.player.height - 20;
}

// Load sprites (if they exist)
function loadSprites() {
    const spriteBasePath = '/sprites/avoid-questions/';
    
    // Try to load player sprite
    const playerImg = new Image();
    playerImg.src = spriteBasePath + 'player.png';
    playerImg.onerror = () => { gameState.sprites.player = null; };
    playerImg.onload = () => { gameState.sprites.player = playerImg; };
    
    // Try to load question sprite
    const questionImg = new Image();
    questionImg.src = spriteBasePath + 'question.png';
    questionImg.onerror = () => { gameState.sprites.question = null; };
    questionImg.onload = () => { gameState.sprites.question = questionImg; };
    
    // Try to load background
    const bgImg = new Image();
    bgImg.src = spriteBasePath + 'background.png';
    bgImg.onerror = () => { gameState.sprites.background = null; };
    bgImg.onload = () => { gameState.sprites.background = bgImg; };
}

// Keyboard event handlers
function handleKeyDown(e) {
    if (!gameState.isRunning || gameState.isPaused) return;
    
    switch(e.key) {
        case 'ArrowLeft':
        case 'a':
        case 'A':
            e.preventDefault();
            gameState.player.direction = -1;
            break;
        case 'ArrowRight':
        case 'd':
        case 'D':
            e.preventDefault();
            gameState.player.direction = 1;
            break;
        case 'Escape':
            e.preventDefault();
            pauseGame();
            break;
    }
}

function handleKeyUp(e) {
    if (!gameState.isRunning) return;
    
    switch(e.key) {
        case 'ArrowLeft':
        case 'a':
        case 'A':
        case 'ArrowRight':
        case 'd':
        case 'D':
            e.preventDefault();
            gameState.player.direction = 0;
            break;
    }
}

// Set player direction (for mobile controls)
export function setPlayerDirection(direction) {
    if (gameState.isRunning && !gameState.isPaused) {
        gameState.player.direction = direction;
    }
}

// Start or restart the game
export function startGame() {
    // Reset game state
    gameState.score = 0;
    gameState.level = 1;
    gameState.lives = 5;
    gameState.questions = [];
    gameState.isRunning = true;
    gameState.isPaused = false;
    gameState.startTime = Date.now();
    gameState.levelStartTime = Date.now();
    gameState.lastQuestionSpawn = Date.now();
    gameState.lastTime = performance.now();
    
    resetPlayerPosition();
    
    // Update .NET UI
    if (gameState.dotNetHelper) {
        gameState.dotNetHelper.invokeMethodAsync('OnScoreUpdate', gameState.score);
        gameState.dotNetHelper.invokeMethodAsync('OnLevelUp', gameState.level);
        gameState.dotNetHelper.invokeMethodAsync('OnLivesUpdate', gameState.lives);
    }
    
    // Start game loop
    gameLoop(performance.now());
}

// Pause the game
export function pauseGame() {
    gameState.isPaused = true;
    if (gameState.animationFrameId) {
        cancelAnimationFrame(gameState.animationFrameId);
        gameState.animationFrameId = null;
    }
}

// Resume the game
export function resumeGame() {
    if (!gameState.isRunning) return;
    
    gameState.isPaused = false;
    gameState.lastTime = performance.now();
    gameLoop(performance.now());
}

// Main game loop
function gameLoop(currentTime) {
    if (!gameState.isRunning || gameState.isPaused) return;
    
    const deltaTime = (currentTime - gameState.lastTime) / 1000; // Convert to seconds
    gameState.lastTime = currentTime;
    
    // Update game state
    updateGame(deltaTime);
    
    // Draw game
    drawGame();
    
    // Continue loop
    gameState.animationFrameId = requestAnimationFrame(gameLoop);
}

// Update game logic
function updateGame(deltaTime) {
    const now = Date.now();
    
    // Check for level progression (60 seconds per level)
    const levelDuration = 60000; // 60 seconds
    const timeSinceLevelStart = now - gameState.levelStartTime;
    
    if (timeSinceLevelStart >= levelDuration) {
        levelUp();
    }
    
    // Update player position
    updatePlayer(deltaTime);
    
    // Spawn new questions
    spawnQuestions(now);
    
    // Update questions
    updateQuestions(deltaTime);
    
    // Check collisions
    checkCollisions();
    
    // Remove off-screen questions and award points
    removeOffscreenQuestions();
}

// Update player movement
function updatePlayer(deltaTime) {
    const player = gameState.player;
    
    // Move player based on direction (frame-rate independent)
    // Multiply speed by speedMultiplier and deltaTime for consistent movement across different frame rates
    player.x += player.direction * player.speed * player.speedMultiplier * deltaTime;
    
    // Keep player within bounds
    if (player.x < 0) player.x = 0;
    if (player.x + player.width > gameState.canvas.width) {
        player.x = gameState.canvas.width - player.width;
    }
}

// Spawn new questions
function spawnQuestions(now) {
    const timeSinceLastSpawn = now - gameState.lastQuestionSpawn;
    const currentSpawnRate = Math.max(500, gameState.questionSpawnRate - (gameState.level - 1) * 100);
    
    if (timeSinceLastSpawn >= currentSpawnRate) {
        const questionText = gameState.questionTexts[
            Math.floor(Math.random() * gameState.questionTexts.length)
        ];
        
        const questionWidth = 120;
        const questionHeight = 40;
        
        const question = {
            x: Math.random() * (gameState.canvas.width - questionWidth),
            y: -questionHeight,
            width: questionWidth,
            height: questionHeight,
            speed: gameState.baseQuestionSpeed + (gameState.level - 1) * 0.5,
            text: questionText,
            color: getRandomQuestionColor()
        };
        
        gameState.questions.push(question);
        gameState.lastQuestionSpawn = now;
    }
}

// Get random color for questions
function getRandomQuestionColor() {
    const colors = [
        '#e74c3c', // Red
        '#3498db', // Blue
        '#f39c12', // Orange
        '#9b59b6', // Purple
        '#1abc9c', // Teal
        '#e67e22'  // Dark orange
    ];
    return colors[Math.floor(Math.random() * colors.length)];
}

// Update question positions
function updateQuestions(deltaTime) {
    // Update questions with frame-rate independent movement
    // Multiply speed by questionSpeedMultiplier and deltaTime for consistent movement across frame rates
    gameState.questions.forEach(question => {
        question.y += question.speed * gameState.questionSpeedMultiplier * deltaTime;
    });
}

// Check for collisions between player and questions
function checkCollisions() {
    const player = gameState.player;
    
    for (let i = gameState.questions.length - 1; i >= 0; i--) {
        const question = gameState.questions[i];
        
        // Simple AABB collision detection
        if (player.x < question.x + question.width &&
            player.x + player.width > question.x &&
            player.y < question.y + question.height &&
            player.y + player.height > question.y) {
            
            // Collision detected
            gameState.questions.splice(i, 1);
            loseLife();
        }
    }
}

// Remove questions that have gone off-screen and award points
function removeOffscreenQuestions() {
    const initialLength = gameState.questions.length;
    
    gameState.questions = gameState.questions.filter(question => {
        if (question.y > gameState.canvas.height) {
            // Question dodged successfully
            gameState.score++;
            if (gameState.dotNetHelper) {
                gameState.dotNetHelper.invokeMethodAsync('OnScoreUpdate', gameState.score);
            }
            return false;
        }
        return true;
    });
}

// Player loses a life
function loseLife() {
    gameState.lives--;
    
    if (gameState.dotNetHelper) {
        gameState.dotNetHelper.invokeMethodAsync('OnLivesUpdate', gameState.lives);
    }
    
    if (gameState.lives <= 0) {
        gameOver();
    }
}

// Level up
function levelUp() {
    gameState.level++;
    gameState.levelStartTime = Date.now();
    
    if (gameState.dotNetHelper) {
        gameState.dotNetHelper.invokeMethodAsync('OnLevelUp', gameState.level);
    }
}

// Game over
function gameOver() {
    gameState.isRunning = false;
    
    if (gameState.animationFrameId) {
        cancelAnimationFrame(gameState.animationFrameId);
        gameState.animationFrameId = null;
    }
    
    if (gameState.dotNetHelper) {
        gameState.dotNetHelper.invokeMethodAsync('OnGameOver', gameState.score, gameState.level);
    }
}

// Draw the game
function drawGame() {
    const ctx = gameState.ctx;
    const canvas = gameState.canvas;
    
    if (!ctx || !canvas) return;
    
    // Clear canvas
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    
    // Draw background
    if (gameState.sprites.background) {
        ctx.drawImage(gameState.sprites.background, 0, 0, canvas.width, canvas.height);
    } else {
        // Default gradient background
        const gradient = ctx.createLinearGradient(0, 0, 0, canvas.height);
        gradient.addColorStop(0, '#1a1a2e');
        gradient.addColorStop(1, '#0f3460');
        ctx.fillStyle = gradient;
        ctx.fillRect(0, 0, canvas.width, canvas.height);
    }
    
    // Draw questions
    gameState.questions.forEach(question => {
        drawQuestion(question);
    });
    
    // Draw player
    drawPlayer();
}

// Draw the player character
function drawPlayer() {
    const ctx = gameState.ctx;
    const player = gameState.player;
    
    if (gameState.sprites.player) {
        // Use sprite if available
        ctx.drawImage(gameState.sprites.player, player.x, player.y, player.width, player.height);
    } else {
        // Default rendering: simple figure with cape and ponytail
        ctx.save();
        
        // Body (traje/cape)
        ctx.fillStyle = player.color;
        ctx.beginPath();
        ctx.moveTo(player.x + player.width / 2, player.y + 15); // Top of cape
        ctx.lineTo(player.x, player.y + player.height); // Bottom left
        ctx.lineTo(player.x + player.width, player.y + player.height); // Bottom right
        ctx.closePath();
        ctx.fill();
        
        // Head
        ctx.fillStyle = '#ffc8a2'; // Skin tone
        ctx.beginPath();
        ctx.arc(player.x + player.width / 2, player.y + 10, 8, 0, Math.PI * 2);
        ctx.fill();
        
        // Ponytail
        ctx.fillStyle = '#4a2c2a'; // Dark brown hair
        ctx.beginPath();
        ctx.ellipse(player.x + player.width / 2 + 6, player.y + 10, 4, 8, Math.PI / 4, 0, Math.PI * 2);
        ctx.fill();
        
        // Add a small highlight to show it's the player
        ctx.strokeStyle = '#fff';
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.arc(player.x + player.width / 2, player.y + 10, 10, 0, Math.PI * 2);
        ctx.stroke();
        
        ctx.restore();
    }
}

// Draw a question
function drawQuestion(question) {
    const ctx = gameState.ctx;
    
    if (gameState.sprites.question) {
        // Use sprite if available
        ctx.drawImage(gameState.sprites.question, question.x, question.y, question.width, question.height);
    } else {
        // Default rendering: colored rectangle with text
        ctx.save();
        
        // Question box
        ctx.fillStyle = question.color;
        ctx.fillRect(question.x, question.y, question.width, question.height);
        
        // Border
        ctx.strokeStyle = '#fff';
        ctx.lineWidth = 2;
        ctx.strokeRect(question.x, question.y, question.width, question.height);
        
        // Text
        ctx.fillStyle = '#fff';
        ctx.font = 'bold 10px Arial';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        
        // Word wrap for long text
        const words = question.text.split(' ');
        const maxWidth = question.width - 10;
        let line = '';
        let y = question.y + question.height / 2 - 5;
        
        words.forEach((word, index) => {
            const testLine = line + word + ' ';
            const metrics = ctx.measureText(testLine);
            
            if (metrics.width > maxWidth && index > 0) {
                ctx.fillText(line, question.x + question.width / 2, y);
                line = word + ' ';
                y += 12;
            } else {
                line = testLine;
            }
        });
        
        ctx.fillText(line, question.x + question.width / 2, y);
        
        ctx.restore();
    }
}

// Get current score (for external queries)
export function getCurrentScore() {
    return gameState.score;
}

// Get current level (for external queries)
export function getCurrentLevel() {
    return gameState.level;
}

// Cleanup and destroy the game
export function destroyGame() {
    // Stop the game loop
    gameState.isRunning = false;
    gameState.isPaused = false;
    
    if (gameState.animationFrameId) {
        cancelAnimationFrame(gameState.animationFrameId);
        gameState.animationFrameId = null;
    }
    
    // Remove event listeners
    window.removeEventListener('resize', resizeCanvas);
    document.removeEventListener('keydown', handleKeyDown);
    document.removeEventListener('keyup', handleKeyUp);
    
    // Clear canvas
    if (gameState.ctx && gameState.canvas) {
        gameState.ctx.clearRect(0, 0, gameState.canvas.width, gameState.canvas.height);
    }
    
    // Reset state
    gameState.canvas = null;
    gameState.ctx = null;
    gameState.dotNetHelper = null;
    gameState.questions = [];
    
    console.log('Avoid Questions Game destroyed');
}
