/**
 * SurviveScene — Orchestrator (survivor.io-inspired game engine).
 *
 * This thin conductor wires subsystem managers together:
 *   InputManager → Player movement → Camera → WeaponSystem → EnemyManager
 *   → ProjectileManager → UpgradeSystem → UIManager → ParticleSystem
 *
 * XP orbs, player HP/invulnerability, boss triggers, and win/death flow
 * live here because they cross-cut multiple subsystems.
 */
import * as PIXI from 'pixi.js';
import { SESSION_CACHE_BUST } from '@shared/utils';
import type { SurviveLevelData } from '@types/survive-data';

import {
  PLAYER_RADIUS, PLAYER_MAX_HP, XP_ORB_SPEED, XP_ORB_RADIUS,
  XP_PICKUP_RADIUS, INVULN_DURATION,
  clamp, lerp, formatTime, ObjectPool,
} from '../survive/types';
import type { XPOrb, PlayerStats } from '../survive/types';
import { WEAPON_SHARPSHOT } from '../survive/weapons/WeaponDefs';

import { AudioManager } from '../survive/AudioManager';
import { InputManager } from '../survive/InputManager';
import { ParticleSystem } from '../survive/ParticleSystem';
import { EnemyManager } from '../survive/EnemyManager';
import type { EnemyManagerConfig } from '../survive/EnemyManager';
import { ProjectileManager } from '../survive/ProjectileManager';
import { WeaponSystem } from '../survive/WeaponSystem';
import { UpgradeSystem } from '../survive/UpgradeSystem';
import { UIManager } from '../survive/UIManager';

// ─── Module-Level State ───────────────────────────────────────

let app: PIXI.Application | null = null;
let scene: SurviveScene | null = null;
let gameActive = false;
let gamePaused = false;
let dotNetRef: DotNet.DotNetObject | null = null;

// ─── SurviveScene ─────────────────────────────────────────────

export class SurviveScene {
  readonly containerId: string;
  private data: SurviveLevelData;

  // Level config
  private level: number;
  private biomeName: string;
  private timerDuration: number;
  private mapWidth: number;
  private mapHeight: number;
  private vpWidth: number;
  private vpHeight: number;
  private backgroundPath: string;
  private enemySprites: string[];
  private playerSpritePath: string;
  private bossSprites: string[];
  private isFinalLevel: boolean;

  // Runtime timers
  private timeRemaining: number;
  private timeElapsed = 0;
  private alive = true;
  private won = false;
  private timerStopped = false;
  private invulnTimer = 0;

  // Player
  private player: PIXI.Container | null = null;
  private playerX: number;
  private playerY: number;
  private playerGlow: PIXI.Graphics | null = null;
  private playerArrow: PIXI.Graphics | null = null;
  private _velX = 0;
  private _velY = 0;

  /** Shared mutable stats read/written by UpgradeSystem passives. */
  stats: PlayerStats;

  // Camera
  private camX: number;
  private camY: number;

  // Containers
  private worldContainer!: PIXI.Container;
  private uiContainer!: PIXI.Container;

  // XP orbs (cross-cut upgrade + economy)
  private xpOrbs: XPOrb[] = [];
  private _orbPool: ObjectPool<PIXI.Graphics> | null = null;

  // Asset aliases
  private _bgAlias = '';
  private _playerAlias = '';
  private enemySpriteAliases: string[] = [];
  private bossSpriteAliases: string[] = [];

  // ── Subsystem managers ──────────────────────────────────────
  private audio!: AudioManager;
  private input!: InputManager;
  private particles!: ParticleSystem;
  private enemyManager!: EnemyManager;
  private projectiles!: ProjectileManager;
  private weaponSystem!: WeaponSystem;
  private upgradeSystem!: UpgradeSystem;
  private ui!: UIManager;

  // ────────────────────────────────────────────────────────────

  constructor(containerId: string, levelData: SurviveLevelData) {
    this.containerId = containerId;
    this.data = levelData;

    this.level = levelData.level ?? levelData.Level ?? 1;
    this.biomeName = levelData.biomeName ?? levelData.BiomeName ?? 'Forest';
    this.timerDuration = levelData.timerDurationSeconds ?? levelData.TimerDurationSeconds ?? 60;
    this.mapWidth = levelData.mapWidth ?? levelData.MapWidth ?? 2000;
    this.mapHeight = levelData.mapHeight ?? levelData.MapHeight ?? 2000;
    this.vpWidth = levelData.viewportWidth ?? levelData.ViewportWidth ?? 800;
    this.vpHeight = levelData.viewportHeight ?? levelData.ViewportHeight ?? 600;
    this.backgroundPath = levelData.backgroundPath ?? levelData.BackgroundPath ?? '';
    this.enemySprites = levelData.enemySprites ?? levelData.EnemySprites ?? [];
    this.playerSpritePath = levelData.playerSpritePath ?? levelData.PlayerSpritePath ?? '';
    this.bossSprites = levelData.bossSprites ?? levelData.BossSprites ?? [];
    this.isFinalLevel = levelData.isFinalLevel ?? levelData.IsFinalLevel ?? false;

    this.timeRemaining = this.timerDuration;

    // Player position
    this.playerX = this.mapWidth / 2;
    this.playerY = this.mapHeight / 2;

    // Camera
    this.camX = clamp(this.playerX - this.vpWidth / 2, 0, this.mapWidth - this.vpWidth);
    this.camY = clamp(this.playerY - this.vpHeight / 2, 0, this.mapHeight - this.vpHeight);

    // Mutable player stats (upgraded via UpgradeSystem passives)
    this.stats = {
      playerSpeed: levelData.playerSpeed ?? levelData.PlayerSpeed ?? 120,
      maxHP: PLAYER_MAX_HP,
      playerHP: PLAYER_MAX_HP,
      damageMultiplier: 1,
      cooldownMultiplier: 1,
      rangeMultiplier: 1,
      magnetRadius: XP_PICKUP_RADIUS,
      coinDropMult: 1,
      armor: 0,
    };
  }

  // ─── Initialisation ─────────────────────────────────────────

  async init(): Promise<void> {
    const container = document.getElementById(this.containerId);
    if (!container) throw new Error(`Container #${this.containerId} not found`);
    container.innerHTML = '';

    const containerW = container.clientWidth || this.vpWidth;
    const containerH = container.clientHeight || this.vpHeight;
    this.vpWidth = containerW;
    this.vpHeight = containerH;

    if (!app) {
      app = new PIXI.Application();
      await app.init({
        width: containerW,
        height: containerH,
        backgroundColor: 0x1a1a2e,
        antialias: true,
        resolution: window.devicePixelRatio || 1,
        autoDensity: true,
        resizeTo: container,
      });
    } else {
      app.renderer.resize(containerW, containerH);
    }

    container.appendChild(app.canvas);

    this.worldContainer = new PIXI.Container();
    app.stage.addChild(this.worldContainer);

    this.uiContainer = new PIXI.Container();
    app.stage.addChild(this.uiContainer);

    this.worldContainer.position.set(-this.camX, -this.camY);

    // Orb pool
    this._orbPool = new ObjectPool<PIXI.Graphics>(
      () => new PIXI.Graphics(),
      (gfx) => { gfx.clear(); gfx.alpha = 1; gfx.visible = false; },
      40,
    );

    // Load assets
    await this._loadAssets();

    // Build world
    this._createBackground();
    this._createPlayer();

    // ── Wire subsystems ───────────────────────────────────────

    this.audio = new AudioManager();

    this.input = new InputManager(this.vpWidth, this.vpHeight);
    this.input.setup(app, this.uiContainer);

    this.particles = new ParticleSystem(this.worldContainer);

    const enemyCfg: EnemyManagerConfig = {
      level: this.level,
      baseEnemyCount: this.data.baseEnemyCount ?? this.data.BaseEnemyCount ?? 5,
      maxEnemyCount: this.data.maxEnemyCount ?? this.data.MaxEnemyCount ?? 80,
      spawnInterval: this.data.spawnIntervalSeconds ?? this.data.SpawnIntervalSeconds ?? 2,
      enemySpeed: this.data.enemySpeed ?? this.data.EnemySpeed ?? 60,
      maxEnemySpeed: this.data.maxEnemySpeed ?? this.data.MaxEnemySpeed ?? 150,
      enemyScale: this.data.enemyScale ?? this.data.EnemyScale ?? 1,
      hasElites: this.data.hasEliteEnemies ?? this.data.HasEliteEnemies ?? false,
      eliteChance: this.data.eliteSpawnChance ?? this.data.EliteSpawnChance ?? 0,
      mapWidth: this.mapWidth,
      mapHeight: this.mapHeight,
      vpWidth: this.vpWidth,
      vpHeight: this.vpHeight,
      isFinalLevel: this.isFinalLevel,
      spawnRampPerMinute: this.data.spawnRampPerMinute ?? this.data.SpawnRampPerMinute ?? 0.20,
      speedRampPerMinute: this.data.speedRampPerMinute ?? this.data.SpeedRampPerMinute ?? 0.10,
      timerDuration: this.timerDuration,
      enemySpriteAliases: this.enemySpriteAliases,
      bossSpriteAliases: this.bossSpriteAliases,
    };

    this.enemyManager = new EnemyManager(enemyCfg, this.worldContainer, this.particles, this.audio);

    // Wire enemy manager callbacks
    this.enemyManager.onSpawnOrb = (x, y) => this._spawnXPOrb(x, y);

    this.projectiles = new ProjectileManager(this.worldContainer, this.mapWidth, this.mapHeight, this.enemyManager);

    this.weaponSystem = new WeaponSystem(
      this.projectiles, this.enemyManager, this.particles,
      this.worldContainer, this.mapWidth, this.mapHeight,
    );

    this.upgradeSystem = new UpgradeSystem(
      this.weaponSystem, this.stats, this.audio,
      this.uiContainer, this.vpWidth, this.vpHeight,
    );

    this.ui = new UIManager(this.uiContainer, this.vpWidth, this.vpHeight);
    this.ui.create(this.level, this.biomeName, this.timerDuration, this.stats.maxHP);

    // Wire boss callbacks (depend on UI being created)
    this.enemyManager.onBossSpawn = (isFinal) => {
      this.ui.showBossHPBar(isFinal);
      this.particles.flashScreen(this.uiContainer, this.vpWidth, this.vpHeight, isFinal ? 0xff0000 : 0xff6600);
    };
    this.enemyManager.onBossDeath = (isFinal) => {
      this.ui.hideBossHPBar();
      if (isFinal) { this.won = true; this._onWin(); }
    };

    // Spawn initial enemies
    this.enemyManager.spawnInitial();

    // Give player the first weapon (Sharpshot = auto-projectile)
    this.weaponSystem.addWeapon(WEAPON_SHARPSHOT);

    // Music
    this.audio.playMusic();

    this.alive = true;
    this.won = false;
    gameActive = true;

    app.ticker.add(this._update, this);
  }

  // ─── Asset Loading ──────────────────────────────────────────

  private async _loadAssets(): Promise<void> {
    const assets: { alias: string; src: string }[] = [];
    const cacheBust = SESSION_CACHE_BUST;

    if (this.backgroundPath) {
      this._bgAlias = `surviveBg_${this.backgroundPath}`;
      assets.push({ alias: this._bgAlias, src: this.backgroundPath + cacheBust });
    }

    if (this.playerSpritePath) {
      this._playerAlias = `survivePlayer_${this.playerSpritePath}`;
      assets.push({ alias: this._playerAlias, src: this.playerSpritePath + cacheBust });
    }

    for (let i = 0; i < this.enemySprites.length; i++) {
      const alias = `surviveEnemy_${i}_${this.enemySprites[i]}`;
      assets.push({ alias, src: this.enemySprites[i] + cacheBust });
      this.enemySpriteAliases.push(alias);
    }

    for (let i = 0; i < this.bossSprites.length; i++) {
      const alias = `surviveBoss_${i}_${this.bossSprites[i]}`;
      assets.push({ alias, src: this.bossSprites[i] + cacheBust });
      this.bossSpriteAliases.push(alias);
    }

    if (assets.length > 0) {
      try { await PIXI.Assets.load(assets); }
      catch (e) { console.warn('Some survive mode assets failed to load:', e); }
    }
  }

  // ─── Background ─────────────────────────────────────────────

  private _createBackground(): void {
    const biomeColors: Record<string, number> = {
      Forest: 0x2d5a27, Swamp: 0x3a4a2a, Mountains: 0x6b6b6b,
      Snowy: 0xd0e0f0, Tropical: 0x3a8a5a, Caverns: 0x2a2a3a,
      Desert: 0xc4a35a, Volcanic: 0x4a1a1a, Ruins: 0x4a4a3a,
      Dark: 0x1a1a2a, Light: 0xf5e6a0, Void: 0x0a0a1a,
    };
    const bgColor = biomeColors[this.biomeName] ?? 0x2d5a27;

    const pad = Math.max(this.vpWidth, this.vpHeight);
    const ground = new PIXI.Graphics();
    ground.rect(-pad, -pad, this.mapWidth + pad * 2, this.mapHeight + pad * 2);
    ground.fill(bgColor);
    this.worldContainer.addChild(ground);

    // Grid lines
    const grid = new PIXI.Graphics();
    grid.setStrokeStyle({ width: 1, color: 0xffffff, alpha: 0.05 });
    for (let x = 0; x <= this.mapWidth; x += 100) { grid.moveTo(x, 0); grid.lineTo(x, this.mapHeight); }
    for (let y = 0; y <= this.mapHeight; y += 100) { grid.moveTo(0, y); grid.lineTo(this.mapWidth, y); }
    grid.stroke();
    this.worldContainer.addChild(grid);

    // Border
    const border = new PIXI.Graphics();
    border.setStrokeStyle({ width: 4, color: 0xff4444, alpha: 0.6 });
    border.rect(0, 0, this.mapWidth, this.mapHeight);
    border.stroke();
    this.worldContainer.addChild(border);

    // Biome decorations
    const decoColors: Record<string, number> = {
      Forest: 0x1a4a17, Swamp: 0x2a3a1a, Mountains: 0x8a8a8a,
      Snowy: 0xffffff, Tropical: 0x2a6a3a, Caverns: 0x3a3a5a,
      Desert: 0xd4b36a, Volcanic: 0x6a2a1a, Ruins: 0x5a5a4a,
      Dark: 0x2a2a4a, Light: 0xd4c870, Void: 0x1a1a3a,
    };
    const decoColor = decoColors[this.biomeName] ?? 0x1a4a17;
    const decorations = new PIXI.Graphics();
    for (let i = 0; i < 200; i++) {
      decorations.circle(Math.random() * this.mapWidth, Math.random() * this.mapHeight, 2 + Math.random() * 6);
    }
    decorations.fill({ color: decoColor, alpha: 0.3 });
    this.worldContainer.addChild(decorations);

    // Tiled background texture overlay
    try {
      const bgTexture = PIXI.Assets.get(this._bgAlias || `surviveBg_${this.level}`);
      if (bgTexture) {
        const tileW = this.vpWidth;
        const tileH = this.vpHeight;
        for (let tx = 0; tx < this.mapWidth; tx += tileW) {
          for (let ty = 0; ty < this.mapHeight; ty += tileH) {
            const bgSprite = new PIXI.Sprite(bgTexture);
            bgSprite.width = tileW;
            bgSprite.height = tileH;
            bgSprite.alpha = 0.15;
            bgSprite.position.set(tx, ty);
            this.worldContainer.addChild(bgSprite);
          }
        }
      }
    } catch { /* no background texture */ }
  }

  // ─── Player ─────────────────────────────────────────────────

  private _createPlayer(): void {
    const playerContainer = new PIXI.Container();

    let playerSprite: PIXI.Sprite | null = null;
    try {
      const tex = PIXI.Assets.get(this._playerAlias || 'survivePlayer');
      if (tex) {
        playerSprite = new PIXI.Sprite(tex);
        playerSprite.anchor.set(0.5, 0.5);
        const maxSize = PLAYER_RADIUS * 4;
        const s = Math.min(maxSize / playerSprite.width, maxSize / playerSprite.height);
        playerSprite.scale.set(s);
        playerContainer.addChild(playerSprite);
      }
    } catch { /* fallback below */ }

    if (!playerSprite) {
      const gfx = new PIXI.Graphics();
      gfx.circle(0, 0, PLAYER_RADIUS);
      gfx.fill(0x4fc3f7);
      gfx.setStrokeStyle({ width: 2, color: 0xffffff });
      gfx.stroke();
      playerContainer.addChild(gfx);

      const arrow = new PIXI.Graphics();
      arrow.moveTo(PLAYER_RADIUS, 0);
      arrow.lineTo(PLAYER_RADIUS - 6, -5);
      arrow.lineTo(PLAYER_RADIUS - 6, 5);
      arrow.closePath();
      arrow.fill(0xffffff);
      playerContainer.addChild(arrow);
      this.playerArrow = arrow;
    }

    // Glow
    const glow = new PIXI.Graphics();
    glow.circle(0, 0, PLAYER_RADIUS + 6);
    glow.fill({ color: 0x4fc3f7, alpha: 0.15 });
    playerContainer.addChildAt(glow, 0);
    this.playerGlow = glow;

    playerContainer.position.set(this.playerX, this.playerY);
    this.worldContainer.addChild(playerContainer);
    this.player = playerContainer;
  }

  // ─── Game Loop ──────────────────────────────────────────────

  private _update(ticker: PIXI.Ticker): void {
    if (!this.alive || this.won || gamePaused || this.upgradeSystem.paused) return;

    const dt = ticker.deltaMS / 1000;

    // Timers
    this.timeElapsed += dt;
    if (!this.timerStopped) this.timeRemaining -= dt;

    // Boss trigger: mid boss at 50%
    const hasBosses = this.bossSpriteAliases.length > 0 && !this.isFinalLevel;
    if (hasBosses && !this.enemyManager.midBossSpawned && this.timeRemaining <= this.timerDuration / 2) {
      this.enemyManager.midBossSpawned = true;
      this.enemyManager.spawnBoss(false, this.playerX, this.playerY);
    }

    // Timer expiry
    if (this.timeRemaining <= 0) {
      this.timeRemaining = 0;

      if (this.isFinalLevel || !hasBosses) {
        this.won = true;
        this._onWin();
        return;
      }

      if (!this.enemyManager.finalBossSpawned) {
        this.enemyManager.finalBossSpawned = true;
        this.timerStopped = true;
        this.enemyManager.spawnBoss(true, this.playerX, this.playerY);
      }
    }

    // Boss glow pulse
    if (this.enemyManager.activeBoss) {
      this.ui.updateBossHPBar(this.enemyManager.activeBoss);
      const bossGlow = this.enemyManager.activeBoss.container?.children[0];
      if (bossGlow) bossGlow.alpha = 0.15 + Math.sin(this.timeElapsed * 4) * 0.1;
    }

    // Invulnerability
    if (this.invulnTimer > 0) {
      this.invulnTimer -= dt;
      if (this.player) this.player.alpha = (Math.floor(this.timeElapsed / 0.08) % 2 === 0) ? 0.4 : 1.0;
    } else if (this.player) {
      this.player.alpha = 1.0;
    }

    // 1. Input → Player movement
    this.input.setCamera(this.camX, this.camY);
    const { dx, dy } = this.input.getMovement(dt);
    this._movePlayer(dt, dx, dy);
    this._updateCamera(dt);

    // 2. Enemy spawning & movement
    this.enemyManager.updateSpawning(dt, this.timeElapsed, this.camX, this.camY);
    this.enemyManager.updateMovement(dt, this.playerX, this.playerY, this.camX, this.camY);
    this.enemyManager.rebuildGrid();

    // 3. Weapon system fires
    this.weaponSystem.update(dt, this.playerX, this.playerY, this.stats);

    // 4. Projectile updates (movement + collision)
    this.projectiles.update(dt, this.stats.coinDropMult, this.playerX, this.playerY);

    // 5. Player collision with enemies
    const hit = this.enemyManager.checkPlayerCollision(this.playerX, this.playerY, this.invulnTimer > 0);
    if (hit) {
      const dmgReduced = hit.damage * (1 - this.stats.armor);
      this.stats.playerHP -= dmgReduced;
      this.invulnTimer = INVULN_DURATION;
      this.audio.playSFX('hit');

      // Knockback — offset position + reduce momentum
      const kbDist = 40;
      this.playerX += Math.cos(hit.knockbackAngle) * kbDist;
      this.playerY += Math.sin(hit.knockbackAngle) * kbDist;
      this._velX *= 0.3;
      this._velY *= 0.3;
      this.playerX = clamp(this.playerX, PLAYER_RADIUS, this.mapWidth - PLAYER_RADIUS);
      this.playerY = clamp(this.playerY, PLAYER_RADIUS, this.mapHeight - PLAYER_RADIUS);
      this.player!.position.set(this.playerX, this.playerY);

      if (this.stats.playerHP <= 0) {
        this.stats.playerHP = 0;
        this.alive = false;
        this._onDeath();
        return;
      }
    }

    // 6. XP orbs
    this._updateXPOrbs(dt);

    // 7. Particles
    this.particles.update(dt);

    // 8. UI
    this.ui.update(
      this.timeRemaining, this.timerDuration,
      this.stats.playerHP, this.stats.maxHP,
      this.enemyManager.enemiesKilled, this.upgradeSystem.xpOrbsCollected,
      this.stats, this.weaponSystem, this.upgradeSystem,
    );
    this.ui.updateMinimap(
      this.playerX, this.playerY,
      this.camX, this.camY,
      this.vpWidth, this.vpHeight,
      this.mapWidth, this.mapHeight,
      this.enemyManager.enemies,
    );

    // Player glow pulse
    if (this.playerGlow) {
      this.playerGlow.alpha = 0.1 + Math.sin(this.timeElapsed * 3) * 0.08;
    }
  }

  // ─── Player Movement ───────────────────────────────────────

  private _movePlayer(dt: number, dx: number, dy: number): void {
    const targetVX = dx * this.stats.playerSpeed;
    const targetVY = dy * this.stats.playerSpeed;

    // Smooth velocity — accelerate fast, decelerate gently
    const hasInput = dx !== 0 || dy !== 0;
    const speed = hasInput ? 20 : 12;
    const f = 1 - Math.exp(-speed * dt);
    this._velX += (targetVX - this._velX) * f;
    this._velY += (targetVY - this._velY) * f;

    // Snap to zero when negligible and no input
    if (!hasInput && Math.abs(this._velX) < 0.5 && Math.abs(this._velY) < 0.5) {
      this._velX = 0;
      this._velY = 0;
    }

    this.playerX += this._velX * dt;
    this.playerY += this._velY * dt;
    this.playerX = clamp(this.playerX, PLAYER_RADIUS, this.mapWidth - PLAYER_RADIUS);
    this.playerY = clamp(this.playerY, PLAYER_RADIUS, this.mapHeight - PLAYER_RADIUS);
    this.player!.position.set(this.playerX, this.playerY);

    if (this.playerArrow && (this._velX !== 0 || this._velY !== 0)) {
      this.playerArrow.rotation = Math.atan2(this._velY, this._velX);
    }
  }

  private _updateCamera(dt: number): void {
    const targetCamX = this.playerX - this.vpWidth / 2;
    const targetCamY = this.playerY - this.vpHeight / 2;
    // Frame-rate independent smooth follow
    const f = 1 - Math.exp(-8 * dt);
    this.camX += (targetCamX - this.camX) * f;
    this.camY += (targetCamY - this.camY) * f;
    this.camX = clamp(this.camX, 0, this.mapWidth - this.vpWidth);
    this.camY = clamp(this.camY, 0, this.mapHeight - this.vpHeight);
    this.worldContainer.position.set(-this.camX, -this.camY);
  }

  // ─── XP Orbs ────────────────────────────────────────────────

  private _spawnXPOrb(x: number, y: number): void {
    const gfx = this._orbPool!.get();
    gfx.clear();
    gfx.circle(0, 0, XP_ORB_RADIUS + 1);
    gfx.fill(0xffd700);
    gfx.circle(0, 0, XP_ORB_RADIUS - 1);
    gfx.fill(0xffb300);
    gfx.circle(0, 0, 2);
    gfx.fill(0xffd700);
    gfx.visible = true;
    gfx.alpha = 1;
    gfx.position.set(x, y);
    this.worldContainer.addChild(gfx);
    this.xpOrbs.push({ gfx, x, y, lifetime: 8.0 });
  }

  private _updateXPOrbs(dt: number): void {
    for (let i = this.xpOrbs.length - 1; i >= 0; i--) {
      const orb = this.xpOrbs[i];
      orb.lifetime -= dt;

      const ddx = this.playerX - orb.x;
      const ddy = this.playerY - orb.y;
      const d = Math.sqrt(ddx * ddx + ddy * ddy);

      if (d < this.stats.magnetRadius) {
        const speed = XP_ORB_SPEED * (1 - d / this.stats.magnetRadius);
        orb.x += (ddx / d) * speed * dt;
        orb.y += (ddy / d) * speed * dt;
        orb.gfx.position.set(orb.x, orb.y);

        if (d < PLAYER_RADIUS) {
          orb.gfx.visible = false;
          if (orb.gfx.parent) orb.gfx.parent.removeChild(orb.gfx);
          this._orbPool!.release(orb.gfx);
          this.xpOrbs.splice(i, 1);
          this.upgradeSystem.collectOrb();
          continue;
        }
      }

      if (orb.lifetime <= 0) {
        orb.gfx.visible = false;
        if (orb.gfx.parent) orb.gfx.parent.removeChild(orb.gfx);
        this._orbPool!.release(orb.gfx);
        this.xpOrbs.splice(i, 1);
        continue;
      }

      if (orb.lifetime < 2) orb.gfx.alpha = orb.lifetime / 2;
    }
  }

  // ─── End-Game ───────────────────────────────────────────────

  private _onWin(): void {
    gameActive = false;
    const title = this.enemyManager.finalBossSpawned ? 'BOSS DEFEATED!' : 'SURVIVED!';
    this.ui.showMessage(title, 0x43a047, `Level ${this.level} Complete!`);
    this.audio.playSFX('win');

    setTimeout(() => {
      if (dotNetRef) {
        try {
          dotNetRef.invokeMethodAsync(
            'OnLevelComplete',
            this.enemyManager.enemiesKilled,
            this.timeElapsed,
            this.upgradeSystem.xpOrbsCollected,
          );
        } catch (e) { console.error('Failed to invoke OnLevelComplete:', e); }
      }
    }, 2000);
  }

  private _onDeath(): void {
    gameActive = false;
    this.particles.spawnBurst(this.playerX, this.playerY, 0x4fc3f7);
    if (this.player) this.player.alpha = 0.3;
    this.ui.showMessage('SURVIVAL ENDED', 0xe53935, `Survived ${formatTime(this.timeElapsed)}`);
    this.audio.playSFX('death');

    setTimeout(() => {
      if (dotNetRef) {
        try {
          dotNetRef.invokeMethodAsync(
            'OnPlayerDeath',
            this.enemyManager.enemiesKilled,
            this.timeElapsed,
            this.upgradeSystem.xpOrbsCollected,
          );
        } catch (e) { console.error('Failed to invoke OnPlayerDeath:', e); }
      }
    }, 2000);
  }

  // ─── Cleanup / Destroy ──────────────────────────────────────

  cleanup(): void {
    if (app?.ticker) {
      try { app.ticker.remove(this._update, this); } catch { /* noop */ }
    }

    this.input?.cleanup();
    this.weaponSystem?.cleanup();
    this.projectiles?.cleanup();

    // Release orbs
    for (const orb of this.xpOrbs) {
      if (orb.gfx) { orb.gfx.visible = false; if (orb.gfx.parent) orb.gfx.parent.removeChild(orb.gfx); }
    }
    this.xpOrbs = [];
    this._orbPool = null;

    if (this.worldContainer) { try { this.worldContainer.removeChildren(); } catch { /* noop */ } }
    if (this.uiContainer) { try { this.uiContainer.removeChildren(); } catch { /* noop */ } }
  }

  destroy(): void {
    this.cleanup();
    this.audio?.destroy();

    if (app) {
      try {
        app.stage.removeChildren();
        app.destroy(true, { children: true, texture: false });
      } catch { /* noop */ }
      app = null;
    }

    gameActive = false;
  }
}

// ─── Public API ───────────────────────────────────────────────

export function createSurviveApi() {
  return {
    async start(containerId: string, levelData: SurviveLevelData, netRef: DotNet.DotNetObject): Promise<void> {
      dotNetRef = netRef;
      if (scene) scene.destroy();
      scene = new SurviveScene(containerId, levelData);
      await scene.init();
    },

    async nextLevel(levelData: SurviveLevelData): Promise<void> {
      if (scene) scene.cleanup();
      scene = new SurviveScene(scene?.containerId ?? 'surviveGameContainer', levelData);
      await scene.init();
    },

    pause(): void { gamePaused = true; },
    resume(): void { gamePaused = false; },

    setAudioEnabled(enabled: boolean): void {
      // Placeholder — audio manager is per-scene instance
    },

    destroy(): void {
      if (scene) { scene.destroy(); scene = null; }
      dotNetRef = null;
    },

    isActive(): boolean { return gameActive; },
  };
}
