# Avoid Questions Game - Developer Documentation

## Overview
Browser-based dodging game where players control a character in Portuguese academic traje, avoiding falling questions from governing bodies.

## Quick Reference

### URLs & Access
- **Route**: `/games/avoid-questions`
- **Auth**: Required ([Authorize] attribute)
- **Listed**: `/games` page

### Controls
- **Desktop**: ← → arrow keys, ESC pause
- **Mobile**: Touch buttons (left/right)

### Files
```
src/RTUB.Web/
├── Pages/Games/AvoidQuestionsGame.razor  (343 lines)
├── wwwroot/
│   ├── js/avoidQuestionsGame.js          (580 lines)
│   ├── css/4-pages/avoid-questions.css   (406 lines)
│   └── sprites/avoid-questions/
│       └── README.md
```

## Game Mechanics

| Component | Details |
|-----------|---------|
| **Lives** | 5 (visual hearts) |
| **Scoring** | +1 per dodged question |
| **Levels** | Every 60 seconds, infinite scaling |
| **Questions** | 7 Portuguese texts, random spawn |
| **Difficulty** | ↑ Speed & spawn rate per level |

### Player
- Size: 40×60px
- Movement: Left/Right (5 units × 100 × deltaTime)
- Visual: Purple cape + ponytail (default)

### Questions
- Size: 120×40px
- Speed: `2 + (level-1) × 0.5`
- Spawn: `1500ms - (level-1) × 100ms` (min 500ms)

## API

### JS Exports
```javascript
initGame(canvasId, dotNetHelper)
startGame()
pauseGame()
resumeGame()
setPlayerDirection(direction)  // -1, 0, 1
destroyGame()
```

### C# Callbacks
```csharp
[JSInvokable] OnGameOver(score, level)
[JSInvokable] OnScoreUpdate(score)
[JSInvokable] OnLevelUp(level)
[JSInvokable] OnLivesUpdate(lives)
```

## Customization

### Sprites (Optional)
Add to `/wwwroot/sprites/avoid-questions/`:
- `player.png` (40×60px)
- `question.png` (120×40px)
- `background.png` (full canvas)

### Questions
Edit `questionTexts` array in JS:
```javascript
questionTexts: [
    "Quanto é a quota?",
    // Add more...
]
```

### Difficulty
Edit `gameState` constants:
```javascript
baseQuestionSpeed: 2,
questionSpawnRate: 1500,
player.speed: 5,
```

## Technical Details

### Frame-Rate Independence
All movement uses `deltaTime`:
- Player: `speed × speedMultiplier (100) × deltaTime`
- Questions: `speed × questionSpeedMultiplier (60) × deltaTime`

### Collision Detection
AABB (Axis-Aligned Bounding Box):
```javascript
if (player.x < q.x + q.w && player.x + player.w > q.x &&
    player.y < q.y + q.h && player.y + player.h > q.y) {
    // Collision
}
```

### Responsive Design
| Breakpoint | Canvas Height | Features |
|------------|---------------|----------|
| ≥768px | 500px | Keyboard only |
| <768px | 400px | Touch buttons visible |
| <576px | 400px | Compact layout |

## Testing

```bash
# Build
cd /home/runner/work/RTUB/RTUB
dotnet build src/RTUB.Web/RTUB.csproj

# Manual Tests
- [ ] Start game from /games
- [ ] Keyboard controls (← → ESC)
- [ ] Touch controls (mobile)
- [ ] Collisions work
- [ ] Lives/Score/Level update
- [ ] Pause/Resume
- [ ] Game Over screen
- [ ] Restart functionality
- [ ] Canvas resize
```

## Security
✅ CodeQL: 0 vulnerabilities  
✅ [Authorize] enforced  
✅ No XSS risks (canvas rendering)  
✅ No eval() or dynamic code  

## Troubleshooting

| Issue | Fix |
|-------|-----|
| Game won't start | Check console, verify JS loaded |
| Choppy movement | Check FPS (should be 60) |
| Collision bugs | Verify dimensions, test AABB |
| Memory leak | Ensure `destroyGame()` called |

## Future Ideas
- Sound effects
- Particle effects on collision
- Power-ups (shield, slow-mo)
- Leaderboard integration
- Achievements

---
**Version**: 1.0 (2025-01-17)  
**Team**: RTUB Frontend
