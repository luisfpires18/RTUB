# Real-Time Interactive Combat & Special Attacks Plan

## Current State Analysis

### What ReplayJson Is Today

ReplayJson is a JSON string stored on every battle result. It contains the **full list of CombatEvent objects** produced by `DeterministicCombatEngine.Simulate()`. The frontend (PixiJS) parses this JSON once and plays back events sequentially, advancing by `SimTime` timestamps.

### Current Battle Pipeline (100% Pre-Computed)

```
User clicks "Fight"
  → Server: DeterministicCombatEngine.Simulate() — ALL events computed at once
  → Server: Serialize List<CombatEvent> as ReplayJson
  → Server → JS: InvokeVoidAsync("startBattle", { EventsJson, DotNetRef, ... })
  → JS: PixiJS ticker advances currentSimTime, plays events like a movie
  → JS → Server: dotNetRef.invokeMethodAsync("OnBattleFinished")
  → Server: Apply rewards, reload character
```

**Key constraint**: There is ZERO user interaction during battle playback. The only `[JSInvokable]` method is `OnBattleFinished`. No SignalR hub for battles exists.

### What Needs To Change

The user wants to **click attack buttons in real-time during combat** — no pre-rendering, no movie playback. When the speed bar fills, attack buttons appear, and the user picks Normal Attack or Special Attack. This requires flipping the architecture from "pre-compute then replay" to "compute per-action as the user plays."

### Three Different Serialization Formats (Current)

| Mode | Format | File |
|---|---|---|
| **Arena** | Raw `List<CombatEvent>` (flat array) | `BattleService.cs` |
| **Stage** | Wrapper `{ Events, EnemyCount, EnemySprites, EnemyPlacements, BiomeName, EnemyStats }` | `StageService.cs` |
| **Boss** | Wrapper `{ Events, EnemyCount, EnemySprites, EnemyPlacements, BiomeName, EnemyStats }` | `BossModeService.cs` |

### CombatEvent — All Fields Today

```
Type, Round, Attacker, Defender, Damage, IsCritical, IsBlocked, IsBoosted,
Character, HP, MaxHP, Winner, Timestamp, ActionTime, SimTime
```

### What Already Exists That We Keep

- **Speed bar system** — JS already renders draining speed bars per character. When bar hits 0, an attack fires. We just change WHO triggers the player's attack (user click instead of auto-fire).
- **HP bars, idle animations, lunge animations, floating text** — all stay.
- **DotNetObjectReference** — already connects JS→Server. We add more `[JSInvokable]` methods.
- **Audio, sprites, canvas** — all untouched.

---

## New Architecture: Real-Time Server-Authoritative Combat

### Core Concept

```
JS owns the clock (speed bars, ticker).
Server owns the math (damage, crits, HP, victory check).
Each attack is one round-trip: JS → Server → JS.
ReplayJson is recorded incrementally and saved at the end.
```

### New Battle Pipeline

```
User clicks "Fight"
  → Server: Create CombatSession (HP, buffs, action times — NO simulation)
  → Server → JS: startBattle({ session state, sprites, DotNetRef })
  → JS: Speed bars start draining (using known ActionTime values)

  LOOP:
    When PLAYER's speed bar hits 0:
      → JS: Show attack buttons (Normal ⚔️ + Special 🔥 if gauge full)
      → User clicks a button
      → JS → Server: dotNetRef.invokeMethodAsync("OnPlayerAttack", attackId)
      → Server: Compute damage, update HP, check KO/Victory, return event data
      → JS: Play attack animation, update HP bars, reset speed bar

    When ENEMY's speed bar hits 0:
      → JS → Server: dotNetRef.invokeMethodAsync("OnEnemyAttack", enemyIndex)
      → Server: Compute damage, update HP, check KO/Victory, return event data
      → JS: Play enemy attack animation, update HP bars, reset speed bar

  On Victory/KO/Draw:
    → JS → Server: OnBattleFinished()
    → Server: Save recorded events as ReplayJson, apply rewards
```

### Why This Works

| Concern | Solution |
|---|---|
| **Cheating** | Server computes all damage, HP, crits — JS only renders |
| **Speed bar manipulation** | Server validates timing (rejects attacks faster than ActionTime) |
| **Latency** | Blazor Server circuit is persistent WebSocket — ~5-20ms round-trip on same server |
| **Existing code reuse** | Same PixiJS sprites, HP bars, animations — just triggered differently |
| **ReplayJson** | Still produced — just recorded incrementally instead of pre-computed |

---

## Proposed Plan

### Phase 1 — CombatSession: Server-Side Battle State

**Goal**: Replace the one-shot `Simulate()` with a stateful session that processes one action at a time.

#### 1a. CombatSession Class

```csharp
public class CombatSession
{
    // Identification
    public int Seed { get; set; }
    public SeededRandom Rng { get; set; }
    public string Mode { get; set; } // "arena" | "stage" | "boss"

    // Combatants
    public CombatantState Player { get; set; }
    public List<CombatantState> Enemies { get; set; }
    public int CurrentTargetIndex { get; set; }

    // Buff tracking
    public int CigarroShieldRemaining { get; set; }
    public int CanhaoBoostRemaining { get; set; }

    // Special attack gauge
    public int PlayerHitCount { get; set; }
    public int GaugeThreshold { get; set; } = 5; // hits needed to fill gauge
    public List<SpecialAttackDef> EquippedSpecials { get; set; } = new();

    // Timing (for server-side validation)
    public DateTime BattleStartedAt { get; set; }
    public DateTime LastPlayerAttackAt { get; set; }
    public DateTime[] LastEnemyAttackAt { get; set; }

    // Recording
    public List<CombatEvent> RecordedEvents { get; set; } = new();
    public int EventTimestamp { get; set; }
    public bool IsComplete { get; set; }
    public BattleOutcome? Outcome { get; set; }
}

public class CombatantState
{
    public int CurrentHP { get; set; }
    public int MaxHP { get; set; }
    public int Power { get; set; }
    public int Defense { get; set; }
    public double CriticalChance { get; set; }
    public double ActionTime { get; set; } // seconds between attacks
    public string Name { get; set; }
}
```

#### 1b. ICombatActionService — One Action at a Time

```csharp
public interface ICombatActionService
{
    /// Initialize a new combat session (no simulation — just set up state)
    CombatSession CreateSession(Character player, List<Character> enemies, int seed,
                                 List<SpecialAttackDef>? equippedSpecials = null);

    /// Player clicked Normal Attack or a Special Attack button
    CombatActionResult ProcessPlayerAttack(CombatSession session, string? specialAttackId);

    /// Enemy's speed bar filled — auto-attack
    CombatActionResult ProcessEnemyAttack(CombatSession session, int enemyIndex);
}

public class CombatActionResult
{
    public List<CombatEvent> Events { get; set; } = new(); // Attack + HPUpdate + maybe KO/Victory
    public bool BattleOver { get; set; }
    public BattleOutcome? Outcome { get; set; }
    public double GaugePercent { get; set; } // 0.0–1.0 for UI
}
```

#### 1c. ProcessPlayerAttack Logic

```csharp
CombatActionResult ProcessPlayerAttack(CombatSession session, string? specialAttackId)
{
    var result = new CombatActionResult();

    // 1. Validate timing (optional — reject if called faster than ActionTime allows)
    // Can be lenient to account for network jitter

    // 2. Pick target (first alive enemy)
    var target = session.Enemies[session.CurrentTargetIndex];

    // 3. Calculate damage
    int damage;
    bool isCritical;
    string? attackId = null;
    string? vfxType = null;
    string? abilityName = null;

    if (specialAttackId != null)
    {
        // Special attack — find definition, apply multiplier
        var special = session.EquippedSpecials.First(s => s.AttackId == specialAttackId);
        damage = CalculateDamage(session.Player.Power * special.DamageMultiplier,
                                  session.Player.CriticalChance, target.Defense, session.Rng);
        isCritical = false; // Specials don't crit (or they always crit — design choice)
        attackId = special.AttackId;
        vfxType = special.VfxType;
        abilityName = special.Name;
        session.PlayerHitCount = 0; // Reset gauge
    }
    else
    {
        // Normal attack — existing damage formula
        damage = CalculateDamage(session.Player.Power, session.Player.CriticalChance,
                                  target.Defense, session.Rng);
        isCritical = RollCrit(session.Player.CriticalChance, session.Rng);
        session.PlayerHitCount++;
    }

    // 4. Apply damage
    target.CurrentHP = Math.Max(0, target.CurrentHP - damage);

    // 5. Record events
    var attackEvt = new CombatEvent
    {
        Type = "Attack",
        Attacker = "Player",
        Defender = $"Enemy{session.CurrentTargetIndex}",
        Damage = damage,
        IsCritical = isCritical,
        IsBoosted = session.CanhaoBoostRemaining > 0,
        AttackId = attackId,
        VfxType = vfxType,
        AbilityName = abilityName,
        Timestamp = session.EventTimestamp++
    };
    result.Events.Add(attackEvt);
    session.RecordedEvents.Add(attackEvt);

    // HPUpdate event
    var hpEvt = new CombatEvent { Type = "HPUpdate", Character = $"Enemy{session.CurrentTargetIndex}",
                                    HP = target.CurrentHP, MaxHP = target.MaxHP,
                                    Timestamp = session.EventTimestamp++ };
    result.Events.Add(hpEvt);
    session.RecordedEvents.Add(hpEvt);

    // 6. Check KO
    if (target.CurrentHP <= 0)
    {
        // ... emit KO event, advance target, check victory ...
    }

    // 7. Gauge percent
    result.GaugePercent = Math.Min(1.0, (double)session.PlayerHitCount / session.GaugeThreshold);

    return result;
}
```

#### 1d. Session Lives on the Razor Page Component

```csharp
// In Stage.razor / Arena.razor / BossMode.razor:
private CombatSession? _combatSession;
```

No DB persistence — if the Blazor circuit drops, battle is lost (same as current watchdog).

### Phase 2 — New JSInvokable Methods on Razor Pages

**Goal**: JS can call into .NET for each attack action.

```csharp
[JSInvokable]
public async Task<string> OnPlayerAttack(string? specialAttackId)
{
    if (_combatSession == null || _combatSession.IsComplete) return "{}";

    var result = _combatActionService.ProcessPlayerAttack(_combatSession, specialAttackId);

    if (result.BattleOver)
    {
        _combatSession.IsComplete = true;
        _combatSession.Outcome = result.Outcome;
    }

    return JsonSerializer.Serialize(result, _jsonOptions);
}

[JSInvokable]
public async Task<string> OnEnemyAttack(int enemyIndex)
{
    if (_combatSession == null || _combatSession.IsComplete) return "{}";

    var result = _combatActionService.ProcessEnemyAttack(_combatSession, enemyIndex);

    if (result.BattleOver)
    {
        _combatSession.IsComplete = true;
        _combatSession.Outcome = result.Outcome;
    }

    return JsonSerializer.Serialize(result, _jsonOptions);
}

[JSInvokable]
public async Task OnBattleFinished() // Already exists — extended
{
    if (_combatSession == null) return;

    // Build ReplayJson from all recorded events
    var replay = new BattleReplay
    {
        Version = 2,
        Mode = _combatSession.Mode,
        Events = _combatSession.RecordedEvents,
        Scene = BuildSceneMetadata()
    };
    currentBattle.ReplayJson = JsonSerializer.Serialize(replay, _jsonOptions);

    // Apply rewards (existing logic)
    await _battleService.FinalizeAndApplyRewardsAsync(currentBattle);
}
```

### Phase 3 — JS: Interactive Speed Bar + Attack Buttons

**Goal**: When the player's speed bar fills, show attack buttons. When enemy bars fill, auto-call server.

#### 3a. Start Battle — No Events, Just State

```javascript
// New startBattle signature — receives session state, not events
window.stageBattleGame.startInteractiveBattle(containerId, {
    DotNetRef: dotNetRef,
    PlayerName: "MyTuno",
    PlayerHP: 5000,
    PlayerMaxHP: 5000,
    PlayerActionTime: 3.5,        // seconds
    Enemies: [
        { Name: "Monkey", HP: 2000, MaxHP: 2000, ActionTime: 4.0, SpritePath: "...", Placement: 0 },
        { Name: "Bear", HP: 3500, MaxHP: 3500, ActionTime: 5.0, SpritePath: "...", Placement: 0 }
    ],
    BackgroundPath: "...",
    PlayerSpritePath: "...",
    EquippedSpecials: [
        { AttackId: "fireball", Name: "Bola de Fogo", Icon: "🔥", VfxType: "projectile" },
        { AttackId: "heal", Name: "Cura", Icon: "💚", VfxType: "buff" }
    ],
    GaugeThreshold: 5,            // normal attacks needed to fill gauge
    HasShotBuff: false
});
```

#### 3b. Updated `update()` Ticker — Interactive Mode

```javascript
update() {
    if (!this.app || !this.stage || this.battleFinished) return;
    const deltaMs = this.app.ticker.deltaMS;

    this.updateIdleAnimation(deltaMs);

    if (!this.isInteractive) {
        // Legacy pre-computed playback (existing code — untouched)
        this.updatePreComputed(deltaMs);
        return;
    }

    // ── Interactive mode ──

    // Drain PLAYER speed bar
    if (this.playerCurrentHp > 0 && !this.playerReady) {
        this.playerSpeedBarTimer = Math.max(0, this.playerSpeedBarTimer - deltaMs);
        this.updatePlayerSpeedBar();

        if (this.playerSpeedBarTimer <= 0) {
            this.playerReady = true;
            this.showAttackButtons();  // ← NEW
        }
    }

    // Drain ENEMY speed bars — auto-attack when ready
    for (let i = 0; i < this.enemies.length; i++) {
        if (this.enemies[i].currentHP <= 0) continue;
        this.enemies[i].speedTimer = Math.max(0, this.enemies[i].speedTimer - deltaMs);
        this.updateEnemySpeedBar(i);

        if (this.enemies[i].speedTimer <= 0 && !this.enemies[i].attacking) {
            this.enemies[i].attacking = true;
            this.executeEnemyAttack(i);  // ← NEW: calls server
        }
    }
}
```

#### 3c. Attack Buttons UI

 ```javascript
showAttackButtons() {
    if (this.attackButtonsContainer) return; // Already shown

    this.attackButtonsContainer = new PIXI.Container();
    const y = this.app.screen.height - 70;
    let x = this.app.screen.width / 2;

    // Normal Attack button — always available
    const normalBtn = this.createAttackButton({
        label: '⚔️ Ataque',
        color: 0x555555,
        x: x - 80,
        y: y,
        onClick: () => this.executePlayerAttack(null)
    });
    this.attackButtonsContainer.addChild(normalBtn);

    // Special Attack buttons — only if gauge is full
    if (this.gaugePercent >= 1.0 && this.equippedSpecials.length > 0) {
        this.equippedSpecials.forEach((special, i) => {
            const btn = this.createAttackButton({
                label: `${special.Icon} ${special.Name}`,
                color: 0xff6600,
                x: x + 60 + (i * 140),
                y: y,
                onClick: () => this.executePlayerAttack(special.AttackId)
            });
            this.attackButtonsContainer.addChild(btn);
        });
    }

    this.stage.addChild(this.attackButtonsContainer);
}

hideAttackButtons() {
    if (this.attackButtonsContainer) {
        this.attackButtonsContainer.destroy({ children: true });
        this.attackButtonsContainer = null;
    }
}
```

#### 3d. Execute Player Attack (JS → Server → JS)

```javascript
async executePlayerAttack(specialAttackId) {
    this.hideAttackButtons();
    this.playerReady = false;

    // Call server — returns CombatActionResult JSON
    const resultJson = await this.dotNetRef.invokeMethodAsync('OnPlayerAttack', specialAttackId);
    const result = JSON.parse(resultJson);

    // Play events (Attack, HPUpdate, possibly KO/Victory)
    for (const evt of result.Events) {
        await this.playEventAnimated(evt);  // Play with animations, awaiting each
    }

    // Update gauge
    this.gaugePercent = result.GaugePercent ?? 0;
    this.updateGaugeBar();

    // Reset speed bar (start draining again)
    if (!result.BattleOver) {
        this.playerSpeedBarTimer = this.playerActionTime * 1000;
    }

    // Check battle end
    if (result.BattleOver) {
        setTimeout(() => this.finishBattle(), 1500);
    }
}
```

#### 3e. Execute Enemy Attack (auto-triggered)

```javascript
async executeEnemyAttack(enemyIndex) {
    // Call server
    const resultJson = await this.dotNetRef.invokeMethodAsync('OnEnemyAttack', enemyIndex);
    const result = JSON.parse(resultJson);

    // Play events
    for (const evt of result.Events) {
        await this.playEventAnimated(evt);
    }

    // Reset enemy speed bar
    this.enemies[enemyIndex].attacking = false;
    if (!result.BattleOver) {
        this.enemies[enemyIndex].speedTimer = this.enemies[enemyIndex].actionTime * 1000;
    }

    if (result.BattleOver) {
        setTimeout(() => this.finishBattle(), 1500);
    }
}
```

#### 3f. Special Attack Gauge Bar (Visible During Combat)

```javascript
createGaugeBar() {
    // Render below player sprite — fills as normal attacks land
    // Orange/gold bar. When full: glows, pulses, buttons get special options
    const barWidth = 60;
    const barHeight = 6;
    // Position below player speed bar
    // Color: gradient from gray → orange → gold as it fills
    // When full (1.0): emit small particle glow effect
}

updateGaugeBar() {
    const fill = this.gaugePercent * this.gaugeBar.maxWidth;
    this.gaugeBar.bar.width = fill;
    // Change color: < 0.5 gray, 0.5-1.0 orange, 1.0 gold+glow
}
```

### Phase 4 — Extended CombatEvent for Special Attacks

**Goal**: CombatEvent gains fields for special attack identification and VFX hints.

```csharp
public class CombatEvent
{
    // ... existing fields (Type, Attacker, Defender, Damage, IsCritical, etc.) ...

    // NEW — Special attack identification
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AttackId { get; set; }        // "fireball", "thunder", "heal"

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AbilityName { get; set; }      // "Bola de Fogo"

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VfxType { get; set; }          // "projectile", "aoe", "beam", "buff"

    // NEW — Visual hints
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? VisualHint { get; set; }       // "screenShake", "flashWhite"

    // NEW — Multi-target for AoE attacks
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Targets { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<int>? TargetDamages { get; set; }
}
```

Normal attacks: these fields are all `null` → serialized as zero extra bytes.

### Phase 5 — Special Attack Definitions (Backend Entity)

**Goal**: Characters can own and equip special attacks.

```csharp
public class SpecialAttack
{
    public int Id { get; set; }
    public string AttackId { get; set; }          // "fireball"
    public string Name { get; set; }              // "Bola de Fogo"
    public string Description { get; set; }
    public string VfxType { get; set; }           // "projectile" | "aoe" | "beam" | "buff"
    public TargetType Target { get; set; }        // Single, AllEnemies, Self
    public double DamageMultiplier { get; set; }  // 1.5 = 150% base damage
    public string Icon { get; set; }              // "🔥"
    public string? SpriteKey { get; set; }        // Sprite for projectile animation
}

public enum TargetType { Single, AllEnemies, Self }

// Join table
public class CharacterSpecialAttack
{
    public int CharacterId { get; set; }
    public int SpecialAttackId { get; set; }
    public int Level { get; set; } = 1;
    public SpecialAttack SpecialAttack { get; set; }
}
```

### Phase 6 — PixiJS VFX for Special Attacks

**Goal**: Different visual animations based on `AttackId` / `VfxType`.

#### 6a. VFX Registry

```javascript
const VFX_REGISTRY = {
    fireball:       { type: 'projectile', color: 0xff4400, trail: true, size: 12, speed: 400 },
    thunder_strike: { type: 'beam', color: 0xffff00, screenShake: true },
    ice_shard:      { type: 'projectile', color: 0x44ddff, trail: true, size: 8, speed: 500 },
    earthquake:     { type: 'aoe', color: 0x886633, screenShake: true },
    heal:           { type: 'buff', color: 0x44ff88, aura: true, target: 'self' },
    power_up:       { type: 'buff', color: 0xff8800, aura: true, target: 'self' },
};
```

#### 6b. playEventAnimated — Dispatch by AttackId

```javascript
async playEventAnimated(evt) {
    const type = getEventField(evt, 'Type');

    if (type === 'Attack') {
        const attackId = getEventField(evt, 'AttackId');

        if (attackId && VFX_REGISTRY[attackId]) {
            await this.playSpecialAttackVfx(evt, VFX_REGISTRY[attackId]);
        } else {
            await this.playNormalAttack(evt); // existing lunge animation
        }
        return;
    }
    if (type === 'HPUpdate') { this.handleHPUpdate(evt); return; }
    if (type === 'KO') { await this.playKO(evt); return; }
    if (type === 'Victory') { await this.playVictory(evt); return; }
}
```

#### 6c. Special Attack VFX Implementations

| VfxType | Animation |
|---|---|
| `projectile` | Spawn colored circle at player, tween to enemy, particle burst on impact |
| `beam` | Draw glowing line from player to enemy, flash + fade, screen shake |
| `aoe` | Expanding ring from center, all enemies flash + damage numbers |
| `buff` | Aura pulse on self, floating ability name text, heal numbers |

#### 6d. Shared VFX Utilities (`pixiVfx.js`)

```
pixiVfx.js
├── createProjectile(stage, from, to, config) → Promise
├── createBeam(stage, from, to, config) → Promise
├── createAoeRing(stage, center, radius, config) → Promise
├── screenShake(stage, intensity, duration) → Promise
├── flashSprite(sprite, color, duration) → Promise
├── spawnParticles(stage, pos, count, config) → void
```

Shared across `pixiBattle.js`, `pixiStageBattle.js`, and future modes.

### Phase 7 — ReplayJson: Recording, Not Pre-Computing

**Goal**: ReplayJson becomes a **post-battle recording** instead of the battle driver.

#### How It Works

1. During battle, every event returned by `ProcessPlayerAttack`/`ProcessEnemyAttack` is appended to `session.RecordedEvents`
2. After `OnBattleFinished`, serialize all recorded events into `BattleReplay`:

```csharp
var replay = new BattleReplay
{
    Version = 2,
    Mode = session.Mode,
    Events = session.RecordedEvents,
    Scene = BuildSceneMetadata()
};
currentBattle.ReplayJson = JsonSerializer.Serialize(replay);
```

3. **Replay viewer** uses the existing pre-computed playback code (unchanged!) — it receives the full events list and plays them sequentially with `SimTime` or `Timestamp` ordering. The legacy code path continues to work.

#### Unified BattleReplay Format

```csharp
public class BattleReplay
{
    public int Version { get; set; } = 2;
    public string Mode { get; set; } = "arena";
    public List<CombatEvent> Events { get; set; } = new();
    public SceneMetadata? Scene { get; set; }
}

public class SceneMetadata
{
    public int EnemyCount { get; set; }
    public List<string> EnemySprites { get; set; } = new();
    public List<int> EnemyPlacements { get; set; } = new();
    public string? BiomeName { get; set; }
    public List<EnemyStat>? EnemyStats { get; set; }
}
```

All three modes (arena/stage/boss) use this same envelope. Old replays still work (JS resolveEvents handles both).

### Phase 8 — Attack Sprite Assets (Optional)

```
/sprites/games/my-tuno/attacks/
├── fireball.png
├── ice_shard.png
├── thunder_bolt.png
├── heal_glow.png
└── shield_barrier.png
```

Loaded on-demand when a special attack is first used. Cached in `loadedAssetAliases`.

---

## Mode-by-Mode Impact

### Stage (PvE Farming) — Primary Target
- ✅ Perfect fit — player clicks attacks against AI enemies
- **Auto-farm toggle**: When enabled, player attacks auto-fire (like current behavior) so farming isn't interrupted. Special attacks auto-use when gauge is full.
- When toggle is OFF: full interactive experience

### Boss Mode — High Impact
- ✅ Ideal — boss fights become interactive and strategic
- Player chooses when to use special attacks against the boss
- Same interactive flow as Stage

### Arena (1v1 PvP)
- ⚠️ Only the **attacker** (initiator) is interactive — defender is AI-controlled
- Defender's attacks auto-fire on their timer (server computes)
- Future: real-time PvP via SignalR (both players interactive)

### Survive Mode
- ❌ Not applicable — already real-time interactive with different mechanics

---

## Implementation Order

```
Phase 1: CombatSession + CombatActionService        ~4-5 hours  ← FOUNDATION
   ├── CombatSession class (state, HP, timers, gauge)
   ├── ICombatActionService (ProcessPlayerAttack, ProcessEnemyAttack)
   ├── Damage calculation (reuse existing CalculateDamage from engine)
   ├── KO / Victory / Draw detection
   ├── Event recording (RecordedEvents list)
   └── Unit tests

Phase 2: New JSInvokable methods on Razor pages      ~2-3 hours
   ├── OnPlayerAttack(specialAttackId) → returns CombatActionResult JSON
   ├── OnEnemyAttack(enemyIndex) → returns CombatActionResult JSON
   ├── Extended OnBattleFinished → saves RecordedEvents as ReplayJson
   ├── CreateSession on "Fight" click (replace Simulate call)
   └── Wire up in Stage.razor, Arena.razor, BossMode.razor

Phase 3: JS interactive speed bar + attack buttons   ~5-6 hours  ← KEY UX
   ├── startInteractiveBattle() — receive state, not events
   ├── update() — interactive mode (drain bars, show buttons)
   ├── Attack buttons UI (Normal ⚔️ + Specials 🔥)
   ├── executePlayerAttack() — JS→Server→JS round-trip
   ├── executeEnemyAttack() — auto-trigger on bar fill
   ├── Special gauge bar (fills with normal attacks)
   └── Auto-farm toggle (auto-attack when bar fills)

Phase 4: Extended CombatEvent fields                  ~1 hour
   ├── Add AttackId, VfxType, AbilityName, VisualHint, Targets
   ├── JsonIgnore for null suppression
   └── Update tests

Phase 5: SpecialAttack entity + DB                    ~3-4 hours
   ├── SpecialAttack entity, CharacterSpecialAttack join
   ├── DB migration
   ├── Seed 3-5 starter specials
   └── Character equip UI

Phase 6: PixiJS VFX for special attacks               ~4-5 hours
   ├── VFX_REGISTRY definitions
   ├── Projectile, beam, AoE, buff animations
   ├── pixiVfx.js shared module
   ├── Screen shake, particles
   └── Test across all modes

Phase 7: ReplayJson recording + replay viewer         ~2-3 hours
   ├── BattleReplay envelope DTO
   ├── Record events during interactive combat
   ├── Save on OnBattleFinished
   ├── Replay viewer uses existing playback code
   └── Backward compat with old replays

Phase 8: Attack sprites (optional)                    ~1-2 hours
   ├── Create/source sprites
   └── Lazy-load in pixiVfx.js
```

**Total estimate**: ~22-29 hours

**Minimum viable path** (interactive combat, no specials yet):
Phases 1→2→3 = **~11-14 hours** — gives you clickable Normal Attack buttons.
Then Phase 4→5→6 adds special attacks on top.

---

## Sequence Diagram

```
User          JS (PixiJS)              Server (.NET)           DB
 │                │                         │                   │
 │  Click Fight   │                         │                   │
 ├───────────────►│                         │                   │
 │                │  FightStageEnemy()      │                   │
 │                ├────────────────────────►│                   │
 │                │                         │  CreateSession()  │
 │                │                         │  (no Simulate!)   │
 │                │  startInteractiveBattle │                   │
 │                │◄────────────────────────┤                   │
 │                │                         │                   │
 │                │  Speed bars draining... │                   │
 │                │  ┌─────────────────┐    │                   │
 │                │  │ Player bar → 0  │    │                   │
 │                │  │ Show: ⚔️ 🔥 💚   │    │                   │
 │                │  └─────────────────┘    │                   │
 │                │                         │                   │
 │  Click ⚔️      │                         │                   │
 ├───────────────►│                         │                   │
 │                │  OnPlayerAttack(null)   │                   │
 │                ├────────────────────────►│                   │
 │                │                         │ CalcDamage, HP--  │
 │                │  { Attack, HPUpdate }   │                   │
 │                │◄────────────────────────┤                   │
 │                │  Lunge animation! 💥    │                   │
 │                │  Reset player bar       │                   │
 │                │                         │                   │
 │                │  ┌──────────────────┐   │                   │
 │                │  │ Enemy0 bar → 0   │   │                   │
 │                │  └──────────────────┘   │                   │
 │                │  OnEnemyAttack(0)       │                   │
 │                ├────────────────────────►│                   │
 │                │                         │ CalcDamage, HP--  │
 │                │  { Attack, HPUpdate }   │                   │
 │                │◄────────────────────────┤                   │
 │                │  Enemy lunge! 💥        │                   │
 │                │                         │                   │
 │                │  ... more rounds ...    │                   │
 │                │  Gauge fills up! ████   │                   │
 │                │                         │                   │
 │                │  ┌─────────────────┐    │                   │
 │                │  │ Player bar → 0  │    │                   │
 │                │  │ Show: ⚔️ 🔥 💚   │    │                   │
 │                │  └─────────────────┘    │                   │
 │  Click 🔥      │                         │                   │
 ├───────────────►│                         │                   │
 │                │  OnPlayerAttack         │                   │
 │                │  ("fireball")           │                   │
 │                ├────────────────────────►│                   │
 │                │                         │ 1.5x damage!      │
 │                │  { Attack(fireball),    │                   │
 │                │    HPUpdate, KO,        │                   │
 │                │    Victory }            │                   │
 │                │◄────────────────────────┤                   │
 │                │                         │                   │
 │   FIREBALL! ☄️ │  Projectile VFX! 💥     │                   │
 │   Enemy KO!    │  KO + Victory anim     │                   │
 │                │                         │                   │
 │                │  OnBattleFinished()     │                   │
 │                ├────────────────────────►│ Save replay       │
 │                │                         ├──────────────────►│
 │                │                         │ Apply rewards     │
 │                │                         ├──────────────────►│
```

---

## Key Design Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Who runs the clock? | **JS** (speed bars drain client-side) | No server-side timer needed; action times are known values |
| Who computes damage? | **Server** (always) | Anti-cheat — client can't manipulate damage/HP |
| How to validate timing? | **Server checks elapsed time** | Rejects attacks faster than ActionTime ± tolerance |
| One round-trip per attack? | **Yes** | Blazor Server WebSocket is ~5-20ms; imperceptible |
| What about Stage auto-farm? | **Auto-farm toggle** | When ON: attacks auto-fire, specials auto-use — same as current feel |
| ReplayJson still exist? | **Yes — recorded post-battle** | Replay viewer uses existing playback code unchanged |
| Keep old `Simulate()` method? | **Yes — for replays and AI vs AI** | Arena defender still uses auto-computed attacks server-side |

---

## Example: What The Player Sees

```
Battle starts → sprites appear, HP bars, speed bars draining...

[Speed bar fills] → Two buttons appear at bottom:
   ┌──────────┐
   │ ⚔️ Ataque │    (always available)
   └──────────┘
   Gauge: ░░░░░░░░░░ 0%

Player clicks ⚔️ → Lunge animation, -450 damage, enemy HP drops
   Gauge: ██░░░░░░░░ 20%

Enemy attacks back → -380 damage, crit! Yellow text

[Player bar fills again] → Button appears, click ⚔️
   Gauge: ████░░░░░░ 40%

... 3 more normal attacks ...
   Gauge: ██████████ 100% ✨ GLOWING

[Speed bar fills] → THREE buttons appear:
   ┌──────────┐  ┌───────────────┐  ┌──────────┐
   │ ⚔️ Ataque │  │ 🔥 Bola de Fogo│  │ 💚 Cura  │
   └──────────┘  └───────────────┘  └──────────┘

Player clicks 🔥 → FIREBALL flies across screen! ☄️💥 -900 damage!
   Screen shakes! Enemy KO!
   Gauge resets: ░░░░░░░░░░ 0%

Victory! 🏆
```
