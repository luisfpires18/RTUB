/**
 * Tomato Thrower Game - Whack-a-Mole Style Game
 * Players click on debtors that pop up from holes to throw tomatoes at them.
 */
const tomatoThrowerGame = (function () {
    let canvas = null;
    let ctx = null;
    let dotNetRef = null;
    let debtors = [];
    
    // Game configuration constants
    const GRID_COLS = 3;
    const GRID_ROWS = 3;
    const MIN_SHOW_TIME = 0.8; // Minimum time debtor shows (seconds)
    const MAX_SHOW_TIME = 2.0; // Maximum time debtor shows (seconds)
    const MIN_HIDE_TIME = 0.5; // Minimum time between spawns
    const MAX_HIDE_TIME = 1.5; // Maximum time between spawns
    const MAX_DELTA_TIME = 0.1; // Maximum delta time to prevent spiral of death
    const GRASS_BLADE_COUNT = 100; // Number of grass blades to render
    const GRASS_PRIME_X = 137; // Prime number for pseudo-random grass X distribution
    const GRASS_PRIME_Y = 73; // Prime number for pseudo-random grass Y distribution
    const SPLAT_MIN_DISTANCE = 15; // Minimum distance for splat particles
    const SPLAT_DISTANCE_RANGE = 10; // Additional random distance for splat particles
    
    let gameRunning = false;
    let points = 0;
    let hits = 0;
    let timeElapsed = 0;
    
    let holes = [];
    let activeDebtors = [];
    let tomatoSplats = [];
    let clickParticles = [];
    
    let lastFrameTime = 0;
    let animationId = null;
    
    // Images
    let avatarImages = {};
    let imagesLoaded = 0;
    let totalImages = 0;

    function init(canvasId, dotNetReference, debtorsJson) {
        canvas = document.getElementById(canvasId);
        if (!canvas) return;
        
        ctx = canvas.getContext('2d');
        dotNetRef = dotNetReference;
        
        try {
            debtors = JSON.parse(debtorsJson);
        } catch (e) {
            console.error('Failed to parse debtors:', e);
            debtors = [];
        }
        
        setupCanvas();
        initializeHoles();
        loadAvatars();
        setupInputHandlers();
        drawInitialState();
    }

    function setupCanvas() {
        canvas.style.cursor = 'crosshair';
    }

    function initializeHoles() {
        holes = [];
        const holeWidth = canvas.width / GRID_COLS;
        const holeHeight = canvas.height / GRID_ROWS;
        
        for (let row = 0; row < GRID_ROWS; row++) {
            for (let col = 0; col < GRID_COLS; col++) {
                holes.push({
                    x: col * holeWidth + holeWidth / 2,
                    y: row * holeHeight + holeHeight / 2,
                    radius: Math.min(holeWidth, holeHeight) * 0.35,
                    occupied: false
                });
            }
        }
    }

    function loadAvatars() {
        totalImages = debtors.length;
        imagesLoaded = 0;
        
        debtors.forEach((debtor, index) => {
            const img = new Image();
            img.onload = () => {
                imagesLoaded++;
                avatarImages[index] = img;
            };
            img.onerror = () => {
                imagesLoaded++;
                // Use fallback if image fails to load
            };
            img.src = debtor.avatar;
        });
    }

    function setupInputHandlers() {
        canvas.addEventListener('click', handleClick);
        canvas.addEventListener('touchstart', handleTouchStart);
        document.addEventListener('visibilitychange', handleVisibilityChange);
    }

    function handleClick(e) {
        if (!gameRunning) return;
        
        const rect = canvas.getBoundingClientRect();
        const scaleX = canvas.width / rect.width;
        const scaleY = canvas.height / rect.height;
        const x = (e.clientX - rect.left) * scaleX;
        const y = (e.clientY - rect.top) * scaleY;
        
        throwTomato(x, y);
    }

    function handleTouchStart(e) {
        if (!gameRunning) return;
        e.preventDefault();
        
        const rect = canvas.getBoundingClientRect();
        const scaleX = canvas.width / rect.width;
        const scaleY = canvas.height / rect.height;
        const touch = e.touches[0];
        const x = (touch.clientX - rect.left) * scaleX;
        const y = (touch.clientY - rect.top) * scaleY;
        
        throwTomato(x, y);
    }

    function throwTomato(x, y) {
        // Check if hit any active debtor
        let hitDebtor = false;
        for (let i = activeDebtors.length - 1; i >= 0; i--) {
            const debtor = activeDebtors[i];
            if (debtor.state === 'visible') {
                const hole = holes[debtor.holeIndex];
                // Avatar is positioned inside the hole
                const avatarY = hole.y - hole.radius * 0.3;
                
                const dx = x - hole.x;
                const dy = y - avatarY;
                const distance = Math.sqrt(dx * dx + dy * dy);
                
                // Check if click is within avatar radius
                const avatarRadius = hole.radius * 0.7;
                if (distance < avatarRadius) {
                    hitDebtor = true;
                    hits++;
                    
                    // Award points based on debt amount (1 point per €1 of debt)
                    const debtorData = debtors[debtor.debtorIndex];
                    const pointsAwarded = Math.max(1, Math.floor(debtorData.debtAmount));
                    points += pointsAwarded;
                    
                    // Create tomato splat only on hit
                    tomatoSplats.push({
                        x: hole.x,
                        y: avatarY,
                        age: 0,
                        lifetime: 1.0,
                        pointsAwarded: pointsAwarded
                    });
                    
                    // Remove debtor
                    holes[debtor.holeIndex].occupied = false;
                    activeDebtors.splice(i, 1);
                    break;
                }
            }
        }
        
        // Create click particles only on hit
        if (hitDebtor) {
            createClickParticles(x, y, true);
        }
        
        updateDotNetStats();
    }

    function createClickParticles(x, y, isHit) {
        const color = isHit ? '#ff6b6b' : '#ffffff';
        const count = isHit ? 12 : 6;
        
        for (let i = 0; i < count; i++) {
            const angle = (Math.PI * 2 * i) / count + Math.random() * 0.3;
            const speed = 50 + Math.random() * 50;
            clickParticles.push({
                x: x,
                y: y,
                vx: Math.cos(angle) * speed,
                vy: Math.sin(angle) * speed,
                age: 0,
                lifetime: 0.3,
                color: color,
                size: isHit ? 4 : 2
            });
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

    function drawInitialState() {
        ctx.fillStyle = '#2d5016';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        drawGrass();
        drawHoles();
    }

    function start() {
        if (debtors.length === 0) return;
        
        points = 0;
        hits = 0;
        timeElapsed = 0;
        activeDebtors = [];
        tomatoSplats = [];
        clickParticles = [];
        
        holes.forEach(h => h.occupied = false);
        
        gameRunning = true;
        lastFrameTime = performance.now();
        animationId = requestAnimationFrame(gameLoop);
    }

    function gameLoop(currentTime) {
        if (!gameRunning) return;
        
        const deltaTime = (currentTime - lastFrameTime) / 1000;
        lastFrameTime = currentTime;
        const dt = Math.min(deltaTime, MAX_DELTA_TIME);
        
        update(dt);
        draw();
        
        animationId = requestAnimationFrame(gameLoop);
    }

    function update(dt) {
        timeElapsed += dt;
        
        // Endless game - no time limit
        updateDebtors(dt);
        spawnDebtors();
        updateTomatoSplats(dt);
        updateClickParticles(dt);
        updateDotNetStats();
    }

    function updateDebtors(dt) {
        for (let i = activeDebtors.length - 1; i >= 0; i--) {
            const debtor = activeDebtors[i];
            
            debtor.timer += dt;
            
            switch (debtor.state) {
                case 'rising':
                    debtor.visibility = Math.min(1, debtor.visibility + dt * 3);
                    if (debtor.visibility >= 1) {
                        debtor.state = 'visible';
                        debtor.timer = 0;
                    }
                    break;
                    
                case 'visible':
                    if (debtor.timer >= debtor.showTime) {
                        debtor.state = 'hiding';
                        debtor.timer = 0;
                    }
                    break;
                    
                case 'hiding':
                    debtor.visibility = Math.max(0, debtor.visibility - dt * 3);
                    if (debtor.visibility <= 0) {
                        holes[debtor.holeIndex].occupied = false;
                        activeDebtors.splice(i, 1);
                    }
                    break;
            }
        }
    }

    function spawnDebtors() {
        // Calculate spawn chance based on time and number of debtors
        const maxConcurrent = Math.min(holes.length, Math.max(3, Math.ceil(debtors.length / 3)));
        
        if (activeDebtors.length < maxConcurrent && Math.random() < 0.03) {
            const availableHoles = holes
                .map((hole, index) => ({ hole, index }))
                .filter(h => !h.hole.occupied);
            
            if (availableHoles.length > 0) {
                const randomHole = availableHoles[Math.floor(Math.random() * availableHoles.length)];
                const debtorIndex = Math.floor(Math.random() * debtors.length);
                const debtor = debtors[debtorIndex];
                
                randomHole.hole.occupied = true;
                
                activeDebtors.push({
                    holeIndex: randomHole.index,
                    debtorIndex: debtorIndex,
                    state: 'rising',
                    visibility: 0,
                    timer: 0,
                    showTime: MIN_SHOW_TIME + Math.random() * (MAX_SHOW_TIME - MIN_SHOW_TIME),
                    width: randomHole.hole.radius * 1.6,
                    height: randomHole.hole.radius * 2
                });
            }
        }
    }

    function updateTomatoSplats(dt) {
        for (let i = tomatoSplats.length - 1; i >= 0; i--) {
            tomatoSplats[i].age += dt;
            if (tomatoSplats[i].age >= tomatoSplats[i].lifetime) {
                tomatoSplats.splice(i, 1);
            }
        }
    }

    function updateClickParticles(dt) {
        for (let i = clickParticles.length - 1; i >= 0; i--) {
            const particle = clickParticles[i];
            particle.x += particle.vx * dt;
            particle.y += particle.vy * dt;
            particle.age += dt;
            
            if (particle.age >= particle.lifetime) {
                clickParticles.splice(i, 1);
            }
        }
    }

    function draw() {
        // Background
        ctx.fillStyle = '#2d5016';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        
        drawGrass();
        drawHoles();
        drawDebtors();
        drawTomatoSplats();
        drawClickParticles();
        // No timer - endless game
    }

    function drawGrass() {
        // Simple grass texture using prime numbers for pseudo-random distribution
        ctx.fillStyle = '#3d6b1f';
        for (let i = 0; i < GRASS_BLADE_COUNT; i++) {
            const x = (i * GRASS_PRIME_X) % canvas.width;
            const y = (i * GRASS_PRIME_Y) % canvas.height;
            ctx.fillRect(x, y, 2, 8);
        }
    }

    function drawHoles() {
        for (const hole of holes) {
            // Hole shadow
            ctx.fillStyle = '#1a1a1a';
            ctx.beginPath();
            ctx.ellipse(hole.x, hole.y, hole.radius, hole.radius * 0.5, 0, 0, Math.PI * 2);
            ctx.fill();
            
            // Hole edge
            ctx.strokeStyle = '#4d2800';
            ctx.lineWidth = 3;
            ctx.stroke();
        }
    }

    function drawDebtors() {
        for (const debtor of activeDebtors) {
            const hole = holes[debtor.holeIndex];
            const debtorData = debtors[debtor.debtorIndex];
            
            // Avatar positioned inside the hole, rising up as visibility increases
            const avatarRadius = hole.radius * 0.7;
            // Start from bottom of hole and rise to center
            const startY = hole.y + hole.radius * 0.3;
            const endY = hole.y - hole.radius * 0.3;
            const avatarY = startY + (endY - startY) * debtor.visibility;
            
            // Only draw if visibility > 0
            if (debtor.visibility > 0) {
                // Save context for clipping
                ctx.save();
                
                // Avatar background (white circle)
                ctx.fillStyle = '#ffffff';
                ctx.beginPath();
                ctx.arc(hole.x, avatarY, avatarRadius, 0, Math.PI * 2);
                ctx.fill();
                
                // Draw avatar image if loaded
                const img = avatarImages[debtor.debtorIndex];
                if (img) {
                    ctx.save();
                    ctx.beginPath();
                    ctx.arc(hole.x, avatarY, avatarRadius - 2, 0, Math.PI * 2);
                    ctx.clip();
                    ctx.drawImage(
                        img,
                        hole.x - avatarRadius + 2,
                        avatarY - avatarRadius + 2,
                        (avatarRadius - 2) * 2,
                        (avatarRadius - 2) * 2
                    );
                    ctx.restore();
                }
                
                // Avatar border
                ctx.strokeStyle = '#333333';
                ctx.lineWidth = 2;
                ctx.beginPath();
                ctx.arc(hole.x, avatarY, avatarRadius, 0, Math.PI * 2);
                ctx.stroke();
                
                // Name label (above avatar)
                ctx.fillStyle = '#ffffff';
                ctx.strokeStyle = '#000000';
                ctx.lineWidth = 3;
                ctx.font = 'bold 11px Arial';
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                
                const name = debtorData.name;
                const nameY = avatarY - avatarRadius - 10;
                
                ctx.strokeText(name, hole.x, nameY);
                ctx.fillText(name, hole.x, nameY);
                
                // Debt amount (below name, above avatar)
                const debt = `€${debtorData.debtAmount.toFixed(2)}`;
                const debtY = nameY + 12;
                
                ctx.fillStyle = '#ffeb3b';
                ctx.font = 'bold 10px Arial';
                ctx.strokeText(debt, hole.x, debtY);
                ctx.fillText(debt, hole.x, debtY);
                
                ctx.restore();
            }
        }
    }

    function drawTomatoSplats() {
        for (const splat of tomatoSplats) {
            const alpha = 1 - (splat.age / splat.lifetime);
            
            // Splat
            ctx.globalAlpha = alpha;
            ctx.fillStyle = '#ff6b6b';
            
            for (let i = 0; i < 8; i++) {
                const angle = (Math.PI * 2 * i) / 8;
                const distance = SPLAT_MIN_DISTANCE + Math.random() * SPLAT_DISTANCE_RANGE;
                const x = splat.x + Math.cos(angle) * distance;
                const y = splat.y + Math.sin(angle) * distance;
                
                ctx.beginPath();
                ctx.arc(x, y, 5, 0, Math.PI * 2);
                ctx.fill();
            }
            
            // Points awarded
            if (splat.age < 0.5) {
                ctx.font = 'bold 20px Arial';
                ctx.fillStyle = '#ffeb3b';
                ctx.strokeStyle = '#000000';
                ctx.lineWidth = 3;
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                
                const offsetY = splat.age * 30;
                ctx.strokeText(`+${splat.pointsAwarded}`, splat.x, splat.y - 30 - offsetY);
                ctx.fillText(`+${splat.pointsAwarded}`, splat.x, splat.y - 30 - offsetY);
            }
            
            ctx.globalAlpha = 1;
        }
    }

    function drawClickParticles() {
        for (const particle of clickParticles) {
            const alpha = 1 - (particle.age / particle.lifetime);
            
            ctx.globalAlpha = alpha;
            ctx.fillStyle = particle.color;
            ctx.beginPath();
            ctx.arc(particle.x, particle.y, particle.size, 0, Math.PI * 2);
            ctx.fill();
        }
        
        ctx.globalAlpha = 1;
    }

    function updateDotNetStats() {
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('UpdateStats', points, hits, hits, timeElapsed);
        }
    }

    function endGame() {
        gameRunning = false;
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnGameOver', points, hits, hits, timeElapsed);
        }
    }

    function dispose() {
        gameRunning = false;
        if (animationId) {
            cancelAnimationFrame(animationId);
            animationId = null;
        }
        if (canvas) {
            canvas.removeEventListener('click', handleClick);
            canvas.removeEventListener('touchstart', handleTouchStart);
        }
        document.removeEventListener('visibilitychange', handleVisibilityChange);
        dotNetRef = null;
        avatarImages = {};
    }

    return { init, start, dispose };
})();

window.tomatoThrowerGame = tomatoThrowerGame;
