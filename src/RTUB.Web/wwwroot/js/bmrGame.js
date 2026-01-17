/**
 * BMR - Bebe mais Rui - HTML5 Canvas Platformer Game
 * Player controls Borat, jumping on platforms, collecting beers, stomping Fat Ladies.
 * Endless mode with pipes, holes, question boxes, and super beers!
 */
const bmrGame = (function () {
    let canvas = null;
    let ctx = null;
    let dotNetRef = null;
    
    let config = {
        startingHealth: 100,
        invulnerabilityMs: 1500,
        jumpStrength: 650,
        gravity: 1200,
        moveSpeed: 200,
        beerPoints: 10,
        superBeerPoints: 50,
        powerUpDurationMs: 5000,
        difficultyScaling: {
            baseSpawnRate: 3.5,
            spawnRateDecreasePerLevel: 0.15,
            minSpawnRate: 1.0,
            enemySpeedIncreasePerLevel: 5,
            platformGapIncreasePerLevel: 5
        },
        enemyTiers: []
    };
    
    let gameRunning = false;
    let health = 100;
    let points = 0;
    let distanceTraveled = 0;
    let enemiesStomped = 0;
    
    // Player state
    const player = {
        x: 0, y: 0, width: 40, height: 64,
        vx: 0, vy: 0,
        direction: 0,
        onGround: false,
        invulnerable: false,
        invulnerableUntil: 0,
        powerUp: false, // 5-second immunity from question box
        powerUpUntil: 0
    };
    
    // Game objects
    let platforms = [];
    let enemies = [];
    let beers = [];
    let pipes = [];
    let holes = [];
    let questionBoxes = [];
    let lastFrameTime = 0;
    let animationId = null;
    let keysPressed = {};
    let nextEnemySpawnTime = 0;
    let nextBeerSpawnTime = 0;
    let cameraX = 0;
    let worldGenX = 0; // Track how far we've generated the world
    
    // Sprites
    let sprites = {
        player: null,
        background: null,
        beer: null,
        enemies: {}
    };
    let spritesLoaded = false;

    // Game world dimensions (scaled to canvas)
    const BASE_GROUND_Y = 520;
    let groundY = 520;
    const PLATFORM_HEIGHT = 15;
    const BASE_WIDTH = 800;
    const BASE_HEIGHT = 600;
    let scaleX = 1;
    let scaleY = 1;
    
    function init(canvasId, dotNetReference, configJson) {
        canvas = document.getElementById(canvasId);
        if (!canvas) return;
        
        ctx = canvas.getContext('2d');
        dotNetRef = dotNetReference;
        
        try { 
            config = JSON.parse(configJson);
        } catch (e) { 
            console.error('Failed to parse config', e);
        }
        
        health = config.startingHealth;
        
        // Setup resize handler and initial size
        resizeCanvas();
        window.addEventListener('resize', resizeCanvas);
        
        loadSprites();
        setupInputHandlers();
        drawInitialState();
    }
    
    function resizeCanvas() {
        const wrapper = canvas.parentElement;
        if (!wrapper) return;
        
        // Get the available width from wrapper
        const rect = wrapper.getBoundingClientRect();
        const availableWidth = rect.width - 4; // Account for border
        
        // Maintain 4:3 aspect ratio
        const aspectRatio = BASE_WIDTH / BASE_HEIGHT;
        let newWidth = availableWidth;
        let newHeight = newWidth / aspectRatio;
        
        // Set canvas internal resolution
        canvas.width = newWidth;
        canvas.height = newHeight;
        
        // Calculate scale factors
        scaleX = newWidth / BASE_WIDTH;
        scaleY = newHeight / BASE_HEIGHT;
        
        // Update ground level based on scale
        groundY = BASE_GROUND_Y * scaleY;
        
        // Redraw if not running
        if (!gameRunning) {
            drawInitialState();
        }
    }

    function loadSprites() {
        // Load player sprite
        sprites.player = new Image();
        sprites.player.src = '/sprites/bmr/player.svg';
        
        // Load background
        sprites.background = new Image();
        sprites.background.src = '/sprites/bmr/background.svg';
        
        // Load beer
        sprites.beer = new Image();
        sprites.beer.src = '/sprites/bmr/beer.svg';
        
        // Load enemy sprites
        if (config.enemyTiers && config.enemyTiers.length > 0) {
            config.enemyTiers.forEach(tier => {
                sprites.enemies[tier.name] = new Image();
                sprites.enemies[tier.name].src = tier.spritePath;
            });
        }
        
        // Track loaded sprites
        let loaded = 0;
        const total = 3 + Object.keys(sprites.enemies).length;
        const onLoad = () => {
            loaded++;
            if (loaded >= total) spritesLoaded = true;
        };
        
        sprites.player.onload = onLoad;
        sprites.background.onload = onLoad;
        sprites.beer.onload = onLoad;
        Object.values(sprites.enemies).forEach(img => img.onload = onLoad);
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
            } else if ((e.key === 'w' || e.key === 'W' || e.key === 'ArrowUp' || e.key === ' ') && player.onGround) {
                player.vy = -config.jumpStrength * scaleY;
                player.onGround = false;
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

    function jump() {
        if (gameRunning && player.onGround) {
            player.vy = -config.jumpStrength * scaleY;
            player.onGround = false;
        }
    }

    function drawInitialState() {
        drawBackground();
        drawGround();
    }

    function generateInitialWorld() {
        // Generate initial world section
        worldGenX = 0;
        generateWorldSection(0, 1500 * scaleX);
    }
    
    function generateWorldAhead() {
        // Generate world ahead of player
        const generateAheadDistance = cameraX + canvas.width + 800 * scaleX;
        
        if (worldGenX < generateAheadDistance) {
            generateWorldSection(worldGenX, generateAheadDistance);
            worldGenX = generateAheadDistance;
        }
        
        // Remove objects too far behind camera
        const removeThreshold = cameraX - 300 * scaleX;
        platforms = platforms.filter(p => p.x + p.width > removeThreshold);
        pipes = pipes.filter(p => p.x + p.width > removeThreshold);
        holes = holes.filter(h => h.x + h.width > removeThreshold);
        questionBoxes = questionBoxes.filter(b => b.x + b.width > removeThreshold);
    }
    
    function generateWorldSection(startX, endX) {
        let x = startX;
        
        while (x < endX) {
            // Random world element
            const element = Math.random();
            
            if (element < 0.15 && x > 300 * scaleX) {
                // Hole (15% chance, not at start)
                const holeWidth = (60 + Math.random() * 60) * scaleX;
                holes.push({
                    x: x,
                    width: holeWidth
                });
                x += holeWidth + 50 * scaleX;
            } else if (element < 0.30) {
                // Pipe
                const pipeWidth = 50 * scaleX;
                const pipeHeight = (60 + Math.random() * 80) * scaleY;
                pipes.push({
                    x: x,
                    y: groundY - pipeHeight,
                    width: pipeWidth,
                    height: pipeHeight
                });
                x += pipeWidth + (100 + Math.random() * 100) * scaleX;
            } else if (element < 0.45) {
                // Platform with possible question box
                const platformWidth = (80 + Math.random() * 80) * scaleX;
                const platformY = (350 + Math.random() * 120) * scaleY;
                
                platforms.push({
                    x: x,
                    y: platformY,
                    width: platformWidth,
                    height: PLATFORM_HEIGHT * scaleY
                });
                
                // 50% chance to add question box above platform
                if (Math.random() > 0.5) {
                    questionBoxes.push({
                        x: x + platformWidth / 2 - 16 * scaleX,
                        y: platformY - 80 * scaleY,
                        width: 32 * scaleX,
                        height: 32 * scaleY,
                        used: false
                    });
                }
                
                x += platformWidth + (80 + Math.random() * 80) * scaleX;
            } else if (element < 0.55) {
                // Question box at ground level (floating)
                questionBoxes.push({
                    x: x,
                    y: groundY - (100 + Math.random() * 60) * scaleY,
                    width: 32 * scaleX,
                    height: 32 * scaleY,
                    used: false
                });
                x += (100 + Math.random() * 100) * scaleX;
            } else {
                // Empty space
                x += (80 + Math.random() * 120) * scaleX;
            }
        }
    }
    
    function updateQuestionBoxes() {
        // Animation for used boxes could go here
    }

    function start() {
        health = config.startingHealth;
        points = 0;
        distanceTraveled = 0;
        enemiesStomped = 0;
        cameraX = 0;
        worldGenX = 0;
        enemies = [];
        beers = [];
        pipes = [];
        holes = [];
        questionBoxes = [];
        platforms = [];
        nextEnemySpawnTime = 0;
        nextBeerSpawnTime = 0;
        
        player.x = 100 * scaleX;
        player.y = groundY - player.height * scaleY;
        player.vx = 0;
        player.vy = 0;
        player.direction = 0;
        player.onGround = true;
        player.invulnerable = false;
        player.invulnerableUntil = 0;
        player.powerUp = false;
        player.powerUpUntil = 0;
        
        generateInitialWorld();
        
        gameRunning = true;
        lastFrameTime = performance.now();
        animationId = requestAnimationFrame(gameLoop);
    }

    function gameLoop(currentTime) {
        if (!gameRunning) return;
        
        const deltaTime = (currentTime - lastFrameTime) / 1000;
        lastFrameTime = currentTime;
        const dt = Math.min(deltaTime, 0.1);
        
        update(dt, currentTime);
        draw();
        updateDotNetStats();
        
        animationId = requestAnimationFrame(gameLoop);
    }

    function update(dt, currentTime) {
        // Endless mode - no level progression, just endless fun
        
        // Update invulnerability
        if (player.invulnerable && currentTime >= player.invulnerableUntil) {
            player.invulnerable = false;
        }
        
        // Update power-up
        if (player.powerUp && currentTime >= player.powerUpUntil) {
            player.powerUp = false;
        }
        
        updatePlayer(dt, currentTime);
        updateCamera();
        spawnEnemies(dt);
        spawnBeers(dt);
        updateEnemies(dt);
        updateBeers();
        updateQuestionBoxes();
        generateWorldAhead();
        checkCollisions(currentTime);
        
        // Check if player fell in a hole
        if (checkHoleCollision()) {
            health = 0;
        }
        
        if (health <= 0) endGame();
    }

    function updatePlayer(dt, currentTime) {
        const scaledWidth = player.width * scaleX;
        const scaledHeight = player.height * scaleY;
        
        // Horizontal movement (scaled)
        player.vx = player.direction * config.moveSpeed * scaleX;
        player.x += player.vx * dt;
        
        // Apply gravity (scaled)
        player.vy += config.gravity * scaleY * dt;
        player.y += player.vy * dt;
        
        // Ground collision (check if not in a hole)
        const inHole = checkHoleCollision();
        if (!inHole && player.y + scaledHeight >= groundY) {
            player.y = groundY - scaledHeight;
            player.vy = 0;
            player.onGround = true;
        }
        
        // Platform collisions
        player.onGround = !inHole && player.y + scaledHeight >= groundY;
        
        for (const platform of platforms) {
            if (checkPlatformCollision({x: player.x, y: player.y, width: scaledWidth, height: scaledHeight}, platform)) {
                // Landing on platform from above
                if (player.vy > 0 && player.y + scaledHeight - player.vy * dt <= platform.y) {
                    player.y = platform.y - scaledHeight;
                    player.vy = 0;
                    player.onGround = true;
                }
            }
        }
        
        // Pipe top collision (can stand on pipes)
        for (const pipe of pipes) {
            const pipeTop = {x: pipe.x, y: pipe.y, width: pipe.width, height: 10 * scaleY};
            if (checkPlatformCollision({x: player.x, y: player.y, width: scaledWidth, height: scaledHeight}, pipeTop)) {
                if (player.vy > 0 && player.y + scaledHeight - player.vy * dt <= pipe.y) {
                    player.y = pipe.y - scaledHeight;
                    player.vy = 0;
                    player.onGround = true;
                }
            }
        }
        
        // Question box collision (hit from below)
        for (const box of questionBoxes) {
            if (!box.used) {
                const playerTop = player.y;
                const boxBottom = box.y + box.height;
                const horizontalOverlap = player.x < box.x + box.width && player.x + scaledWidth > box.x;
                
                if (horizontalOverlap && player.vy < 0 && playerTop <= boxBottom && playerTop > boxBottom - 20 * scaleY) {
                    // Hit the box from below!
                    box.used = true;
                    player.vy = 0;
                    
                    // Random reward: beer, super beer, or power-up
                    const reward = Math.random();
                    if (reward < 0.4) {
                        // Regular beer
                        points += config.beerPoints;
                    } else if (reward < 0.7) {
                        // Super beer
                        points += config.superBeerPoints;
                    } else {
                        // Power-up: 5 seconds immunity
                        player.powerUp = true;
                        player.powerUpUntil = currentTime + config.powerUpDurationMs;
                    }
                }
            }
        }
        
        // Keep player in bounds (left side)
        if (player.x < cameraX) {
            player.x = cameraX;
        }
        
        // Track distance for scoring
        distanceTraveled = Math.max(distanceTraveled, player.x);
    }

    function checkPlatformCollision(obj, platform) {
        return obj.x < platform.x + platform.width &&
               obj.x + obj.width > platform.x &&
               obj.y + obj.height >= platform.y &&
               obj.y + obj.height <= platform.y + platform.height + 20;
    }

    function updateCamera() {
        // Camera follows player (side-scrolling)
        const targetCameraX = player.x - canvas.width * 0.3;
        cameraX = Math.max(0, targetCameraX);
    }

    function spawnEnemies(dt) {
        nextEnemySpawnTime -= dt;
        
        if (nextEnemySpawnTime <= 0) {
            const spawnRate = config.difficultyScaling.baseSpawnRate;
            nextEnemySpawnTime = spawnRate + Math.random() * 1.0;
            
            // Select enemy tier based on spawn weights
            const tier = selectEnemyTier();
            if (tier) {
                const enemySize = getEnemySize(tier.name);
                const scaledWidth = enemySize.width * scaleX;
                const scaledHeight = enemySize.height * scaleY;
                
                // Spawn ahead of camera on ground (not in holes)
                let spawnX = cameraX + canvas.width + (100 + Math.random() * 300) * scaleX;
                let spawnY = groundY;
                let patrolStartX = spawnX - 100 * scaleX;
                let patrolEndX = spawnX + 100 * scaleX;
                
                // Make sure not spawning in a hole
                let inHole = false;
                for (const hole of holes) {
                    if (spawnX > hole.x - scaledWidth && spawnX < hole.x + hole.width) {
                        inHole = true;
                        break;
                    }
                }
                
                if (!inHole) {
                    // Check if too close to existing enemies
                    const minEnemyDistance = 80 * scaleX;
                    let tooClose = false;
                    for (const enemy of enemies) {
                        const distance = Math.abs(enemy.x - spawnX);
                        if (distance < minEnemyDistance) {
                            tooClose = true;
                            break;
                        }
                    }
                    
                    if (!tooClose) {
                        enemies.push({
                            x: spawnX,
                            y: spawnY - scaledHeight,
                            width: scaledWidth,
                            height: scaledHeight,
                            tier: tier,
                            speed: tier.speed * scaleX,
                            direction: -1,
                            patrolStartX: patrolStartX,
                            patrolEndX: patrolEndX
                        });
                    }
                }
            }
        }
    }

    function selectEnemyTier() {
        if (!config.enemyTiers || config.enemyTiers.length === 0) return null;
        
        const totalWeight = config.enemyTiers.reduce((sum, tier) => sum + tier.spawnWeight, 0);
        let random = Math.random() * totalWeight;
        
        for (const tier of config.enemyTiers) {
            random -= tier.spawnWeight;
            if (random <= 0) return tier;
        }
        
        return config.enemyTiers[0];
    }

    function getEnemySize(tierName) {
        const sizes = {
            'Chubby': { width: 36, height: 48 },
            'Plus': { width: 44, height: 56 },
            'Heavy': { width: 52, height: 64 },
            'Mega': { width: 64, height: 76 }
        };
        return sizes[tierName] || { width: 40, height: 50 };
    }

    function spawnBeers(dt) {
        nextBeerSpawnTime -= dt;
        
        if (nextBeerSpawnTime <= 0) {
            nextBeerSpawnTime = 2.0 + Math.random() * 1.5;
            
            // Spawn beer ahead of camera
            const spawnX = cameraX + canvas.width + (50 + Math.random() * 300) * scaleX;
            
            // Check not spawning in a hole
            let inHole = false;
            for (const hole of holes) {
                if (spawnX > hole.x && spawnX < hole.x + hole.width) {
                    inHole = true;
                    break;
                }
            }
            
            if (!inHole) {
                // Place floating or on platform
                let spawnY = groundY - (60 + Math.random() * 100) * scaleY;
                
                // 20% chance for super beer
                const isSuper = Math.random() < 0.2;
                
                beers.push({
                    x: spawnX,
                    y: spawnY,
                    width: (isSuper ? 32 : 24) * scaleX,
                    height: (isSuper ? 40 : 32) * scaleY,
                    collected: false,
                    isSuper: isSuper
                });
            }
        }
    }

    function updateEnemies(dt) {
        for (let i = enemies.length - 1; i >= 0; i--) {
            const enemy = enemies[i];
            
            // Patrol behavior
            enemy.x += enemy.direction * enemy.speed * dt;
            
            // Reverse direction at patrol bounds
            if (enemy.x <= enemy.patrolStartX) {
                enemy.direction = 1;
            } else if (enemy.x >= enemy.patrolEndX) {
                enemy.direction = -1;
            }
            
            // Remove enemies that are too far behind camera
            if (enemy.x + enemy.width < cameraX - 100 * scaleX) {
                enemies.splice(i, 1);
            }
        }
    }

    function updateBeers() {
        // Remove collected or off-screen beers
        for (let i = beers.length - 1; i >= 0; i--) {
            const beer = beers[i];
            if (beer.collected || beer.x + beer.width < cameraX - 50 * scaleX) {
                beers.splice(i, 1);
            }
        }
    }

    function checkCollisions(currentTime) {
        const scaledWidth = player.width * scaleX;
        const scaledHeight = player.height * scaleY;
        const playerRect = {x: player.x, y: player.y, width: scaledWidth, height: scaledHeight};
        
        // Check enemy collisions with stomp mechanic
        for (let i = enemies.length - 1; i >= 0; i--) {
            const enemy = enemies[i];
            if (rectsIntersect(playerRect, enemy)) {
                // Check if player is stomping (falling onto enemy from above)
                const playerBottom = player.y + scaledHeight;
                const enemyTop = enemy.y;
                const playerWasFalling = player.vy > 0;
                const landingOnTop = playerBottom <= enemyTop + 20 * scaleY;
                
                if (playerWasFalling && landingOnTop) {
                    // STOMP! Kill the enemy
                    enemies.splice(i, 1);
                    enemiesStomped++;
                    points += 20; // Bonus for stomping
                    
                    // Bounce up after stomp
                    player.vy = -config.jumpStrength * scaleY * 0.6;
                } else if (!player.invulnerable && !player.powerUp) {
                    // Player takes damage (unless powered up)
                    health -= enemy.tier.damage;
                    player.invulnerable = true;
                    player.invulnerableUntil = currentTime + config.invulnerabilityMs;
                    
                    // Knockback (scaled)
                    player.vx = -200 * scaleX;
                    player.vy = -150 * scaleY;
                }
            }
        }
        
        // Check beer collection
        for (const beer of beers) {
            if (!beer.collected && rectsIntersect(playerRect, beer)) {
                beer.collected = true;
                if (beer.isSuper) {
                    points += config.superBeerPoints;
                } else {
                    points += config.beerPoints;
                }
            }
        }
        
        // Check pipe collision (can't walk through pipes)
        for (const pipe of pipes) {
            if (rectsIntersect(playerRect, pipe)) {
                // Push player out of pipe
                const overlapLeft = (player.x + scaledWidth) - pipe.x;
                const overlapRight = (pipe.x + pipe.width) - player.x;
                
                if (overlapLeft < overlapRight && overlapLeft > 0) {
                    player.x = pipe.x - scaledWidth;
                } else if (overlapRight > 0) {
                    player.x = pipe.x + pipe.width;
                }
            }
        }
    }
    
    function checkHoleCollision() {
        const scaledWidth = player.width * scaleX;
        const playerCenterX = player.x + scaledWidth / 2;
        
        for (const hole of holes) {
            if (playerCenterX > hole.x && playerCenterX < hole.x + hole.width) {
                // Player is over a hole
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
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        
        drawBackground();
        
        ctx.save();
        ctx.translate(-cameraX, 0);
        
        drawGround();
        drawHoles();
        drawPipes();
        drawPlatforms();
        drawQuestionBoxes();
        drawBeers();
        drawEnemies();
        drawPlayer();
        
        // Draw power-up indicator
        if (player.powerUp) {
            drawPowerUpAura();
        }
        
        ctx.restore();
    }

    function drawBackground() {
        if (spritesLoaded && sprites.background.complete) {
            // Tile background with parallax effect
            const parallaxX = -cameraX * 0.3;
            const bgWidth = sprites.background.width || canvas.width;
            const startX = Math.floor(parallaxX / bgWidth) * bgWidth;
            
            for (let x = startX; x < canvas.width + bgWidth; x += bgWidth) {
                ctx.drawImage(sprites.background, x - parallaxX % bgWidth, 0, canvas.width, canvas.height);
            }
        } else {
            // Fallback gradient background
            const gradient = ctx.createLinearGradient(0, 0, 0, canvas.height);
            gradient.addColorStop(0, '#1a3a5c');
            gradient.addColorStop(0.4, '#2d5a7b');
            gradient.addColorStop(1, '#4a7a9a');
            ctx.fillStyle = gradient;
            ctx.fillRect(0, 0, canvas.width, canvas.height);
        }
    }

    function drawGround() {
        // Draw ground extending beyond camera, but skip holes
        const groundStart = Math.floor(cameraX / (100 * scaleX)) * (100 * scaleX) - 100 * scaleX;
        const groundEnd = cameraX + canvas.width + 100 * scaleX;
        
        // Draw ground in segments, skipping holes
        let currentX = groundStart;
        
        // Sort holes by x position for proper rendering
        const sortedHoles = [...holes].sort((a, b) => a.x - b.x);
        
        for (const hole of sortedHoles) {
            if (hole.x > currentX && hole.x < groundEnd) {
                // Draw ground segment before hole
                const segmentEnd = Math.min(hole.x, groundEnd);
                if (segmentEnd > currentX) {
                    drawGroundSegment(currentX, segmentEnd);
                }
                currentX = hole.x + hole.width;
            }
        }
        
        // Draw remaining ground after last hole
        if (currentX < groundEnd) {
            drawGroundSegment(currentX, groundEnd);
        }
    }
    
    function drawGroundSegment(startX, endX) {
        const width = endX - startX;
        if (width <= 0) return;
        
        ctx.fillStyle = '#5a7a6a';
        ctx.fillRect(startX, groundY, width, canvas.height - groundY + cameraX);
        
        ctx.fillStyle = '#6a8a7a';
        ctx.fillRect(startX, groundY - 4 * scaleY, width, 8 * scaleY);
        
        // Grass tufts
        ctx.fillStyle = '#7a9a8a';
        for (let x = startX; x < endX; x += 60 * scaleX) {
            ctx.beginPath();
            ctx.moveTo(x, groundY);
            ctx.lineTo(x + 5 * scaleX, groundY - 10 * scaleY);
            ctx.lineTo(x + 10 * scaleX, groundY);
            ctx.fill();
        }
    }
    
    function drawHoles() {
        // Draw dark void for holes
        for (const hole of holes) {
            if (hole.x + hole.width > cameraX && hole.x < cameraX + canvas.width) {
                ctx.fillStyle = '#1a1a2e';
                ctx.fillRect(hole.x, groundY, hole.width, canvas.height - groundY + 100);
            }
        }
    }
    
    function drawPipes() {
        for (const pipe of pipes) {
            if (pipe.x + pipe.width > cameraX && pipe.x < cameraX + canvas.width) {
                // Pipe body (green like Mario)
                ctx.fillStyle = '#2e8b57';
                ctx.fillRect(pipe.x + 4 * scaleX, pipe.y + 20 * scaleY, pipe.width - 8 * scaleX, pipe.height - 20 * scaleY);
                
                // Pipe top (wider)
                ctx.fillStyle = '#3cb371';
                ctx.fillRect(pipe.x, pipe.y, pipe.width, 20 * scaleY);
                
                // Pipe highlight
                ctx.fillStyle = '#48d178';
                ctx.fillRect(pipe.x + 2 * scaleX, pipe.y + 2 * scaleY, 6 * scaleX, 16 * scaleY);
                
                // Pipe dark side
                ctx.fillStyle = '#228b22';
                ctx.fillRect(pipe.x + pipe.width - 8 * scaleX, pipe.y + 2 * scaleY, 6 * scaleX, 16 * scaleY);
            }
        }
    }
    
    function drawQuestionBoxes() {
        for (const box of questionBoxes) {
            if (box.x + box.width > cameraX && box.x < cameraX + canvas.width) {
                if (box.used) {
                    // Used box (gray)
                    ctx.fillStyle = '#666666';
                    ctx.fillRect(box.x, box.y, box.width, box.height);
                    ctx.strokeStyle = '#444444';
                    ctx.lineWidth = 2;
                    ctx.strokeRect(box.x, box.y, box.width, box.height);
                } else {
                    // Active question box (yellow/gold)
                    ctx.fillStyle = '#ffd700';
                    ctx.fillRect(box.x, box.y, box.width, box.height);
                    ctx.strokeStyle = '#b8860b';
                    ctx.lineWidth = 3;
                    ctx.strokeRect(box.x, box.y, box.width, box.height);
                    
                    // Question mark
                    ctx.fillStyle = '#8b4513';
                    ctx.font = `bold ${20 * scaleY}px Arial`;
                    ctx.textAlign = 'center';
                    ctx.textBaseline = 'middle';
                    ctx.fillText('?', box.x + box.width / 2, box.y + box.height / 2);
                }
            }
        }
    }
    
    function drawPowerUpAura() {
        // Draw golden glow around player when powered up
        const scaledWidth = player.width * scaleX;
        const scaledHeight = player.height * scaleY;
        const centerX = player.x + scaledWidth / 2;
        const centerY = player.y + scaledHeight / 2;
        
        ctx.save();
        ctx.globalAlpha = 0.3 + Math.sin(performance.now() / 100) * 0.2;
        ctx.fillStyle = '#ffd700';
        ctx.beginPath();
        ctx.ellipse(centerX, centerY, scaledWidth * 0.8, scaledHeight * 0.6, 0, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
    }

    function drawPlatforms() {
        for (const platform of platforms) {
            // Only draw visible platforms
            if (platform.x + platform.width > cameraX && platform.x < cameraX + canvas.width) {
                ctx.fillStyle = '#8b7355';
                ctx.beginPath();
                drawRoundRect(platform.x, platform.y, platform.width, platform.height, 5 * scaleX);
                ctx.fill();
                
                // Platform highlight
                ctx.fillStyle = '#a08060';
                ctx.fillRect(platform.x + 2 * scaleX, platform.y + 2 * scaleY, platform.width - 4 * scaleX, 4 * scaleY);
            }
        }
    }

    function drawRoundRect(x, y, width, height, radius) {
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

    function drawPlayer() {
        const scaledWidth = player.width * scaleX;
        const scaledHeight = player.height * scaleY;
        
        // Skip drawing during invulnerability flash
        if (player.invulnerable) {
            if (Math.floor(performance.now() / 100) % 2 === 0) return;
        }
        
        if (spritesLoaded && sprites.player.complete) {
            ctx.save();
            if (player.direction === -1) {
                // Flip horizontally when moving left
                ctx.translate(player.x + scaledWidth, player.y);
                ctx.scale(-1, 1);
                ctx.drawImage(sprites.player, 0, 0, scaledWidth, scaledHeight);
            } else {
                ctx.drawImage(sprites.player, player.x, player.y, scaledWidth, scaledHeight);
            }
            ctx.restore();
        } else {
            // Fallback: draw simple character
            drawFallbackPlayer();
        }
    }

    function drawFallbackPlayer() {
        const scaledWidth = player.width * scaleX;
        const scaledHeight = player.height * scaleY;
        const x = player.x, y = player.y, w = scaledWidth, h = scaledHeight;
        
        // Body (green shirt)
        ctx.fillStyle = '#2ecc71';
        ctx.fillRect(x + 4 * scaleX, y + 20 * scaleY, w - 8 * scaleX, h - 30 * scaleY);
        
        // Head
        ctx.fillStyle = '#f5deb3';
        ctx.beginPath();
        ctx.arc(x + w/2, y + 12 * scaleY, 12 * scaleX, 0, Math.PI * 2);
        ctx.fill();
        
        // Hair
        ctx.fillStyle = '#1a1a1a';
        ctx.beginPath();
        ctx.arc(x + w/2, y + 8 * scaleY, 10 * scaleX, Math.PI, 0);
        ctx.fill();
        
        // Eyes
        ctx.fillStyle = '#333';
        ctx.beginPath();
        ctx.arc(x + w/2 - 4 * scaleX, y + 10 * scaleY, 2 * scaleX, 0, Math.PI * 2);
        ctx.arc(x + w/2 + 4 * scaleX, y + 10 * scaleY, 2 * scaleX, 0, Math.PI * 2);
        ctx.fill();
        
        // Mustache
        ctx.fillStyle = '#1a1a1a';
        ctx.beginPath();
        ctx.ellipse(x + w/2, y + 17 * scaleY, 6 * scaleX, 3 * scaleY, 0, 0, Math.PI * 2);
        ctx.fill();
        
        // Legs
        ctx.fillStyle = '#3498db';
        ctx.fillRect(x + 8 * scaleX, y + h - 16 * scaleY, 8 * scaleX, 16 * scaleY);
        ctx.fillRect(x + w - 16 * scaleX, y + h - 16 * scaleY, 8 * scaleX, 16 * scaleY);
    }

    function drawEnemies() {
        for (const enemy of enemies) {
            // Only draw visible enemies
            if (enemy.x + enemy.width > cameraX && enemy.x < cameraX + canvas.width) {
                const sprite = sprites.enemies[enemy.tier.name];
                if (spritesLoaded && sprite && sprite.complete) {
                    ctx.save();
                    if (enemy.direction === 1) {
                        // Flip horizontally when moving right
                        ctx.translate(enemy.x + enemy.width, enemy.y);
                        ctx.scale(-1, 1);
                        ctx.drawImage(sprite, 0, 0, enemy.width, enemy.height);
                    } else {
                        ctx.drawImage(sprite, enemy.x, enemy.y, enemy.width, enemy.height);
                    }
                    ctx.restore();
                } else {
                    // Fallback: draw colored ellipse
                    drawFallbackEnemy(enemy);
                }
            }
        }
    }

    function drawFallbackEnemy(enemy) {
        const colors = {
            'Chubby': '#ff69b4',
            'Plus': '#ff1493',
            'Heavy': '#c71585',
            'Mega': '#8b008b'
        };
        
        ctx.fillStyle = colors[enemy.tier.name] || '#ff69b4';
        ctx.beginPath();
        ctx.ellipse(enemy.x + enemy.width/2, enemy.y + enemy.height * 0.6, 
                    enemy.width/2, enemy.height * 0.4, 0, 0, Math.PI * 2);
        ctx.fill();
        
        // Head
        ctx.fillStyle = '#f5deb3';
        ctx.beginPath();
        ctx.arc(enemy.x + enemy.width/2, enemy.y + enemy.height * 0.25, enemy.width * 0.35, 0, Math.PI * 2);
        ctx.fill();
        
        // Angry eyes
        ctx.fillStyle = '#333';
        ctx.beginPath();
        ctx.arc(enemy.x + enemy.width * 0.35, enemy.y + enemy.height * 0.22, 2 * scaleX, 0, Math.PI * 2);
        ctx.arc(enemy.x + enemy.width * 0.65, enemy.y + enemy.height * 0.22, 2 * scaleX, 0, Math.PI * 2);
        ctx.fill();
    }

    function drawBeers() {
        for (const beer of beers) {
            if (beer.collected) continue;
            
            // Only draw visible beers
            if (beer.x + beer.width > cameraX && beer.x < cameraX + canvas.width) {
                if (beer.isSuper) {
                    // Super beer - golden with sparkles
                    ctx.fillStyle = '#ffd700';
                    ctx.fillRect(beer.x + 2 * scaleX, beer.y + 6 * scaleY, beer.width - 6 * scaleX, beer.height - 8 * scaleY);
                    ctx.fillStyle = '#ffffff';
                    ctx.beginPath();
                    ctx.ellipse(beer.x + beer.width/2, beer.y + 8 * scaleY, beer.width/2 - 2 * scaleX, 5 * scaleY, 0, 0, Math.PI * 2);
                    ctx.fill();
                    // Star decoration
                    ctx.fillStyle = '#ffff00';
                    ctx.font = `${12 * scaleY}px Arial`;
                    ctx.textAlign = 'center';
                    ctx.fillText('★', beer.x + beer.width/2, beer.y - 5 * scaleY);
                } else if (spritesLoaded && sprites.beer.complete) {
                    ctx.drawImage(sprites.beer, beer.x, beer.y, beer.width, beer.height);
                } else {
                    // Fallback: draw simple beer mug
                    ctx.fillStyle = '#daa520';
                    ctx.fillRect(beer.x + 2 * scaleX, beer.y + 6 * scaleY, beer.width - 6 * scaleX, beer.height - 8 * scaleY);
                    ctx.fillStyle = '#fffacd';
                    ctx.beginPath();
                    ctx.ellipse(beer.x + beer.width/2, beer.y + 8 * scaleY, beer.width/2 - 2 * scaleX, 4 * scaleY, 0, 0, Math.PI * 2);
                    ctx.fill();
                }
            }
        }
    }

    function updateDotNetStats() {
        if (dotNetRef) {
            // Endless mode - no levels, just show points
            dotNetRef.invokeMethodAsync('UpdateStats', health, 1, points);
        }
    }

    function endGame() {
        gameRunning = false;
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }
        if (dotNetRef) {
            // Endless mode - report enemies stomped as "level"
            dotNetRef.invokeMethodAsync('OnGameOver', points, enemiesStomped);
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
        window.removeEventListener('resize', resizeCanvas);
        dotNetRef = null;
    }

    return { init, start, startMove, stopMove, jump, dispose };
})();

window.bmrGame = bmrGame;
