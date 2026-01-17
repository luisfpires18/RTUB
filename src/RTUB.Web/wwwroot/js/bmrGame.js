/**
 * BMR - Bebe mais Rui - HTML5 Canvas Platformer Game
 * Player controls Borat, jumping on platforms, collecting beers, and dodging Fat Ladies.
 */
const bmrGame = (function () {
    let canvas = null;
    let ctx = null;
    let dotNetRef = null;
    
    let config = {
        startingHealth: 100,
        invulnerabilityMs: 1500,
        jumpStrength: 450,
        gravity: 1200,
        moveSpeed: 200,
        beerPoints: 10,
        levelUpSeconds: 30,
        difficultyScaling: {
            baseSpawnRate: 2.5,
            spawnRateDecreasePerLevel: 0.1,
            minSpawnRate: 0.5,
            enemySpeedIncreasePerLevel: 5,
            platformGapIncreasePerLevel: 5
        },
        enemyTiers: []
    };
    
    let gameRunning = false;
    let health = 100;
    let level = 1;
    let points = 0;
    let distanceTraveled = 0;
    let maxLevelReached = 1;
    
    // Player state
    const player = {
        x: 0, y: 0, width: 40, height: 64,
        vx: 0, vy: 0,
        direction: 0,
        onGround: false,
        invulnerable: false,
        invulnerableUntil: 0
    };
    
    // Game objects
    let platforms = [];
    let enemies = [];
    let beers = [];
    let lastFrameTime = 0;
    let animationId = null;
    let keysPressed = {};
    let nextEnemySpawnTime = 0;
    let nextBeerSpawnTime = 0;
    let cameraX = 0;
    
    // Sprites
    let sprites = {
        player: null,
        background: null,
        beer: null,
        enemies: {}
    };
    let spritesLoaded = false;

    // Game world dimensions (scaled to canvas)
    let GROUND_Y = 520;
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
        GROUND_Y = 520 * scaleY;
        
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

    function generateInitialPlatforms() {
        platforms = [];
        // Add some initial platforms (scaled)
        const platformPositions = [
            { x: 150, y: 420, width: 100 },
            { x: 350, y: 350, width: 120 },
            { x: 550, y: 400, width: 100 },
            { x: 700, y: 320, width: 140 },
            { x: 900, y: 380, width: 100 },
            { x: 1100, y: 340, width: 120 }
        ];
        
        platformPositions.forEach(p => {
            platforms.push({
                x: p.x * scaleX,
                y: p.y * scaleY,
                width: p.width * scaleX,
                height: PLATFORM_HEIGHT * scaleY
            });
        });
    }

    function start() {
        health = config.startingHealth;
        level = 1;
        points = 0;
        distanceTraveled = 0;
        maxLevelReached = 1;
        cameraX = 0;
        enemies = [];
        beers = [];
        nextEnemySpawnTime = 0;
        nextBeerSpawnTime = 0;
        
        player.x = 100 * scaleX;
        player.y = GROUND_Y - player.height * scaleY;
        player.vx = 0;
        player.vy = 0;
        player.direction = 0;
        player.onGround = true;
        player.invulnerable = false;
        player.invulnerableUntil = 0;
        
        generateInitialPlatforms();
        
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
        // Level progression based on distance traveled (every 500 units = 1 level up)
        const distancePerLevel = 500 * scaleX;
        const newLevel = Math.floor(distanceTraveled / distancePerLevel) + 1;
        if (newLevel > level) {
            level = newLevel;
            maxLevelReached = Math.max(maxLevelReached, level);
        }
        
        // Update invulnerability
        if (player.invulnerable && currentTime >= player.invulnerableUntil) {
            player.invulnerable = false;
        }
        
        updatePlayer(dt);
        updateCamera();
        spawnEnemies(dt);
        spawnBeers(dt);
        updateEnemies(dt);
        updateBeers();
        generatePlatformsAhead();
        checkCollisions(currentTime);
        
        if (health <= 0) endGame();
    }

    function updatePlayer(dt) {
        const scaledWidth = player.width * scaleX;
        const scaledHeight = player.height * scaleY;
        
        // Horizontal movement (scaled)
        player.vx = player.direction * config.moveSpeed * scaleX;
        player.x += player.vx * dt;
        
        // Apply gravity (scaled)
        player.vy += config.gravity * scaleY * dt;
        player.y += player.vy * dt;
        
        // Ground collision
        if (player.y + scaledHeight >= GROUND_Y) {
            player.y = GROUND_Y - scaledHeight;
            player.vy = 0;
            player.onGround = true;
        }
        
        // Platform collisions
        player.onGround = player.y + scaledHeight >= GROUND_Y;
        
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
        
        // Keep player in bounds (left side)
        if (player.x < cameraX) {
            player.x = cameraX;
        }
        
        // Track distance for progression
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
            const spawnRate = Math.max(
                config.difficultyScaling.minSpawnRate,
                config.difficultyScaling.baseSpawnRate - (level - 1) * config.difficultyScaling.spawnRateDecreasePerLevel
            );
            nextEnemySpawnTime = spawnRate;
            
            // Select enemy tier based on spawn weights
            const tier = selectEnemyTier();
            if (tier) {
                // Spawn enemy ahead of camera
                const spawnX = cameraX + canvas.width + 50 + Math.random() * 200;
                
                // Find a platform or ground to spawn on
                let spawnY = GROUND_Y;
                for (const platform of platforms) {
                    if (platform.x <= spawnX && platform.x + platform.width >= spawnX) {
                        spawnY = platform.y;
                        break;
                    }
                }
                
                const enemySize = getEnemySize(tier.name);
                const scaledWidth = enemySize.width * scaleX;
                const scaledHeight = enemySize.height * scaleY;
                const speedMultiplier = 1 + (level - 1) * config.difficultyScaling.enemySpeedIncreasePerLevel / 100;
                
                // Larger patrol range for more movement (300-400 units each direction)
                const patrolRange = 300 + Math.random() * 100;
                
                enemies.push({
                    x: spawnX,
                    y: spawnY - scaledHeight,
                    width: scaledWidth,
                    height: scaledHeight,
                    tier: tier,
                    speed: tier.speed * speedMultiplier * scaleX,
                    direction: -1, // Patrol left initially
                    patrolStartX: spawnX - patrolRange,
                    patrolEndX: spawnX + patrolRange
                });
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
            nextBeerSpawnTime = 1.5 + Math.random(); // Random interval
            
            // Spawn beer ahead of camera
            const spawnX = cameraX + canvas.width + (50 + Math.random() * 300) * scaleX;
            
            // Place on platform or floating
            let spawnY = GROUND_Y - (60 + Math.random() * 100) * scaleY;
            for (const platform of platforms) {
                if (Math.abs(platform.x + platform.width / 2 - spawnX) < 100 * scaleX) {
                    spawnY = platform.y - 40 * scaleY;
                    break;
                }
            }
            
            beers.push({
                x: spawnX,
                y: spawnY,
                width: 24 * scaleX,
                height: 32 * scaleY,
                collected: false
            });
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

    function generatePlatformsAhead() {
        // Generate new platforms as player moves
        const generateAheadDistance = cameraX + canvas.width + 500 * scaleX;
        const lastPlatform = platforms.length > 0 ? platforms[platforms.length - 1] : null;
        
        if (!lastPlatform || lastPlatform.x + lastPlatform.width < generateAheadDistance) {
            const startX = lastPlatform ? lastPlatform.x + lastPlatform.width + (80 + Math.random() * 100) * scaleX : generateAheadDistance;
            const baseGap = (80 + (level - 1) * config.difficultyScaling.platformGapIncreasePerLevel) * scaleX;
            
            // Add a few platforms
            for (let i = 0; i < 3; i++) {
                const x = startX + i * ((150 + Math.random() * 100) * scaleX + baseGap);
                const y = (300 + Math.random() * 180) * scaleY; // Random height between 300-480 (scaled)
                const width = (80 + Math.random() * 80) * scaleX;
                
                platforms.push({
                    x: x,
                    y: y,
                    width: width,
                    height: PLATFORM_HEIGHT * scaleY
                });
            }
        }
        
        // Remove platforms too far behind camera
        for (let i = platforms.length - 1; i >= 0; i--) {
            if (platforms[i].x + platforms[i].width < cameraX - 200 * scaleX) {
                platforms.splice(i, 1);
            }
        }
    }

    function checkCollisions(currentTime) {
        const scaledWidth = player.width * scaleX;
        const scaledHeight = player.height * scaleY;
        const playerRect = {x: player.x, y: player.y, width: scaledWidth, height: scaledHeight};
        
        // Check enemy collisions
        if (!player.invulnerable) {
            for (const enemy of enemies) {
                if (rectsIntersect(playerRect, enemy)) {
                    health -= enemy.tier.damage;
                    player.invulnerable = true;
                    player.invulnerableUntil = currentTime + config.invulnerabilityMs;
                    
                    // Knockback (scaled)
                    player.vx = -200 * scaleX;
                    player.vy = -150 * scaleY;
                    break;
                }
            }
        }
        
        // Check beer collection
        for (const beer of beers) {
            if (!beer.collected && rectsIntersect(playerRect, beer)) {
                beer.collected = true;
                points += config.beerPoints;
            }
        }
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
        drawPlatforms();
        drawBeers();
        drawEnemies();
        drawPlayer();
        
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
        // Draw ground extending beyond camera
        const groundStart = Math.floor(cameraX / (100 * scaleX)) * (100 * scaleX) - 100 * scaleX;
        const groundEnd = cameraX + canvas.width + 100 * scaleX;
        
        ctx.fillStyle = '#5a7a6a';
        ctx.fillRect(groundStart, GROUND_Y, groundEnd - groundStart, canvas.height - GROUND_Y);
        
        ctx.fillStyle = '#6a8a7a';
        ctx.fillRect(groundStart, GROUND_Y - 4 * scaleY, groundEnd - groundStart, 8 * scaleY);
        
        // Grass tufts
        ctx.fillStyle = '#7a9a8a';
        for (let x = groundStart; x < groundEnd; x += 60 * scaleX) {
            ctx.beginPath();
            ctx.moveTo(x, GROUND_Y);
            ctx.lineTo(x + 5 * scaleX, GROUND_Y - 10 * scaleY);
            ctx.lineTo(x + 10 * scaleX, GROUND_Y);
            ctx.fill();
        }
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
                if (spritesLoaded && sprites.beer.complete) {
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
            dotNetRef.invokeMethodAsync('UpdateStats', health, level, points);
        }
    }

    function endGame() {
        gameRunning = false;
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnGameOver', points, maxLevelReached);
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
