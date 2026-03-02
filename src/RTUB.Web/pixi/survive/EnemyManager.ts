/**
 * Survive Mode — Enemy Manager.
 *
 * Handles spawning, movement AI, spatial grid, collision with player,
 * boss spawning, and enemy lifecycle.
 */
import * as PIXI from 'pixi.js';
import { SESSION_CACHE_BUST } from '@shared/utils';
import {
  PLAYER_RADIUS, ENEMY_HIT_RADIUS, ELITE_SCALE, ELITE_SPEED_MULT,
  BOSS_HIT_RADIUS, BOSS_SPEED_MULT, BOSS_HP_MULT,
  ENEMY_DAMAGE, ELITE_DAMAGE, BOSS_DAMAGE,
  INVULN_DURATION, BASE_ENEMY_HP, ELITE_HP_MULT, ENEMY_HP_PER_LEVEL,
  EnemyBehaviour, SpatialGrid, clamp, dist,
} from './types';
import type { EnemyState } from './types';
import type { SurviveLevelData } from '@types/survive-data';
import type { ParticleSystem } from './ParticleSystem';
import type { AudioManager } from './AudioManager';

export interface EnemyManagerConfig {
  level: number;
  baseEnemyCount: number;
  maxEnemyCount: number;
  spawnInterval: number;
  enemySpeed: number;
  maxEnemySpeed: number;
  enemyScale: number;
  hasElites: boolean;
  eliteChance: number;
  mapWidth: number;
  mapHeight: number;
  vpWidth: number;
  vpHeight: number;
  isFinalLevel: boolean;
  spawnRampPerMinute: number;
  speedRampPerMinute: number;
  timerDuration: number;
  enemySpriteAliases: string[];
  bossSpriteAliases: string[];
}

export class EnemyManager {
  // ─── Public state (read by other subsystems) ────────────────

  enemies: EnemyState[] = [];
  activeBoss: EnemyState | null = null;
  readonly spatialGrid = new SpatialGrid<EnemyState>(128);

  enemiesKilled = 0;
  bossesKilled = 0;
  totalSpawned = 0;

  midBossSpawned = false;
  finalBossSpawned = false;

  // ─── Config ─────────────────────────────────────────────────

  private cfg: EnemyManagerConfig;
  private worldContainer: PIXI.Container;

  // Runtime ramp state 
  private currentEnemySpeed: number;
  private spawnTimer = 0;
  private speedRampTimer = 0;
  private lastMinuteRamp = 0;
  private spawnRampBonus = 0;
  private speedRampBonus = 0;

  // Pools
  private _containerPool: PIXI.Container[] = [];

  // Callbacks 
  private particles: ParticleSystem;
  private audio: AudioManager;

  /** Callback when an XP orb should be spawned at (x, y). */
  onSpawnOrb: ((x: number, y: number) => void) | null = null;
  /** Callback when the boss HP bar should update. */
  onBossHPChanged: (() => void) | null = null;
  /** Callback for boss spawn (for UI bar). */
  onBossSpawn: ((isFinal: boolean) => void) | null = null;
  /** Callback for boss death. */
  onBossDeath: ((isFinal: boolean) => void) | null = null;

  constructor(
    cfg: EnemyManagerConfig,
    worldContainer: PIXI.Container,
    particles: ParticleSystem,
    audio: AudioManager,
  ) {
    this.cfg = cfg;
    this.worldContainer = worldContainer;
    this.particles = particles;
    this.audio = audio;
    this.currentEnemySpeed = cfg.enemySpeed;
  }

  // ─── Spawning ───────────────────────────────────────────────

  spawnInitial(): void {
    for (let i = 0; i < this.cfg.baseEnemyCount; i++) this.spawnEnemy();
  }

  /** Called each frame. Handles timed wave spawning. */
  updateSpawning(dt: number, timeElapsed: number, camX: number, camY: number): void {
    this.spawnTimer += dt;
    this.speedRampTimer += dt;

    // Per-minute difficulty ramp
    const currentMinute = Math.floor(timeElapsed / 60);
    if (currentMinute > this.lastMinuteRamp) {
      const newMinutes = currentMinute - this.lastMinuteRamp;
      this.spawnRampBonus += newMinutes * this.cfg.spawnRampPerMinute;
      this.speedRampBonus += newMinutes * this.cfg.speedRampPerMinute;
      this.lastMinuteRamp = currentMinute;
    }

    // Speed ramp
    const rampFactor = 1 + (this.speedRampTimer / this.cfg.timerDuration) * 0.8 + this.speedRampBonus;
    this.currentEnemySpeed = Math.min(this.cfg.enemySpeed * rampFactor, this.cfg.maxEnemySpeed);

    // Wave spawning
    if (this.spawnTimer >= this.cfg.spawnInterval) {
      this.spawnTimer = 0;
      const timeScale = Math.floor(timeElapsed / 5);
      const rampMult = 1 + this.spawnRampBonus;
      const baseSpawn = Math.ceil((3 + timeScale) * rampMult);
      const burstBonus = Math.floor(timeElapsed / 35) * 5;
      const toSpawn = Math.min(baseSpawn + burstBonus, 30);
      for (let i = 0; i < toSpawn; i++) this.spawnEnemy(camX, camY);
    }
  }

  private spawnEnemy(camX = 0, camY = 0): void {
    if (this.enemies.length >= this.cfg.maxEnemyCount) return;

    const side = Math.floor(Math.random() * 4);
    let ex = 0, ey = 0;
    const margin = 80;

    switch (side) {
      case 0: ex = Math.random() * this.cfg.mapWidth; ey = Math.max(0, camY - margin); break;
      case 1: ex = Math.min(this.cfg.mapWidth, camX + this.cfg.vpWidth + margin); ey = Math.random() * this.cfg.mapHeight; break;
      case 2: ex = Math.random() * this.cfg.mapWidth; ey = Math.min(this.cfg.mapHeight, camY + this.cfg.vpHeight + margin); break;
      case 3: ex = Math.max(0, camX - margin); ey = Math.random() * this.cfg.mapHeight; break;
    }

    const isElite = this.cfg.hasElites && Math.random() < this.cfg.eliteChance;
    const scale = isElite ? this.cfg.enemyScale * ELITE_SCALE : this.cfg.enemyScale;
    const speed = isElite
      ? this.currentEnemySpeed * ELITE_SPEED_MULT
      : this.currentEnemySpeed * (0.8 + Math.random() * 0.4);

    const container = this._getContainer();
    let sprite: PIXI.Sprite | null = null;

    if (this.cfg.enemySpriteAliases.length > 0) {
      const alias = this.cfg.enemySpriteAliases[Math.floor(Math.random() * this.cfg.enemySpriteAliases.length)];
      try {
        const tex = PIXI.Assets.get(alias);
        if (tex) {
          sprite = new PIXI.Sprite(tex);
          sprite.anchor.set(0.5, 0.5);
          const maxSize = ENEMY_HIT_RADIUS * 4 * scale;
          const s = Math.min(maxSize / sprite.width, maxSize / sprite.height);
          sprite.scale.set(s);
          container.addChild(sprite);
        }
      } catch { /* fallback */ }
    }

    if (!sprite) {
      const gfx = new PIXI.Graphics();
      const radius = ENEMY_HIT_RADIUS * scale;
      gfx.circle(0, 0, radius);
      gfx.fill(isElite ? 0xff4444 : 0xe53935);
      if (isElite) {
        gfx.setStrokeStyle({ width: 2, color: 0xffff00 });
        gfx.stroke();
      }
      container.addChild(gfx);
    }

    if (isElite) {
      const glow = new PIXI.Graphics();
      glow.circle(0, 0, ENEMY_HIT_RADIUS * scale + 4);
      glow.fill({ color: 0xff4444, alpha: 0.2 });
      container.addChildAt(glow, 0);
    }

    container.position.set(ex, ey);
    this.worldContainer.addChild(container);

    // HP formula
    const baseHP = BASE_ENEMY_HP + this.cfg.level * ENEMY_HP_PER_LEVEL;
    const minuteBonus = 1 + Math.floor(this.speedRampTimer / 60) * 0.15;
    const hp = Math.ceil(isElite ? baseHP * minuteBonus * ELITE_HP_MULT : baseHP * minuteBonus);

    // Behaviour assignment — weighted random
    let behaviour = EnemyBehaviour.Chaser;
    if (!isElite) {
      const r = Math.random();
      if (r < 0.1) behaviour = EnemyBehaviour.Flanker;
      else if (r < 0.15) behaviour = EnemyBehaviour.Sprinter;
      else if (r < 0.2 && this.cfg.level >= 3) behaviour = EnemyBehaviour.Tank;
    }

    this.enemies.push({
      container, x: ex, y: ey, speed, isElite, scale,
      hitRadius: ENEMY_HIT_RADIUS * scale,
      wobblePhase: Math.random() * Math.PI * 2,
      alive: true, hp, maxHp: hp,
      behaviour, aiTimer: 0,
    });

    this.totalSpawned++;
  }

  // ─── Boss ───────────────────────────────────────────────────

  spawnBoss(isFinal: boolean, playerX: number, playerY: number): void {
    // Clear existing enemies
    for (const enemy of this.enemies) {
      if (enemy.container) this._releaseContainer(enemy.container);
    }
    this.enemies = [];

    const container = this._getContainer();
    let sprite: PIXI.Sprite | null = null;

    if (this.cfg.bossSpriteAliases.length > 0) {
      const alias = this.cfg.bossSpriteAliases[Math.floor(Math.random() * this.cfg.bossSpriteAliases.length)];
      try {
        const tex = PIXI.Assets.get(alias);
        if (tex) {
          sprite = new PIXI.Sprite(tex);
          sprite.anchor.set(0.5, 0.5);
          const maxSize = BOSS_HIT_RADIUS * 4;
          const s = Math.min(maxSize / sprite.width, maxSize / sprite.height);
          sprite.scale.set(s);
          container.addChild(sprite);
        }
      } catch { /* fallback */ }
    }

    if (!sprite) {
      const gfx = new PIXI.Graphics();
      gfx.circle(0, 0, BOSS_HIT_RADIUS);
      gfx.fill(isFinal ? 0xaa0000 : 0xcc2222);
      gfx.setStrokeStyle({ width: 3, color: 0xffcc00 });
      gfx.stroke();
      container.addChild(gfx);
    }

    // Boss glow
    const glow = new PIXI.Graphics();
    glow.circle(0, 0, BOSS_HIT_RADIUS + 10);
    glow.fill({ color: isFinal ? 0xff0000 : 0xff6600, alpha: 0.25 });
    container.addChildAt(glow, 0);

    if (isFinal) {
      const crown = new PIXI.Text({ text: '👑', style: { fontSize: 20 } });
      crown.anchor.set(0.5, 1);
      crown.position.set(0, -BOSS_HIT_RADIUS - 5);
      container.addChild(crown);
    }

    const angle = Math.random() * Math.PI * 2;
    const bx = clamp(playerX + Math.cos(angle) * 350, BOSS_HIT_RADIUS, this.cfg.mapWidth - BOSS_HIT_RADIUS);
    const by = clamp(playerY + Math.sin(angle) * 350, BOSS_HIT_RADIUS, this.cfg.mapHeight - BOSS_HIT_RADIUS);

    container.position.set(bx, by);
    this.worldContainer.addChild(container);

    const minuteBonus = 1 + Math.floor(this.speedRampTimer / 60) * 0.20;
    const bossHP = Math.ceil(
      (BASE_ENEMY_HP + this.cfg.level * ENEMY_HP_PER_LEVEL) *
      BOSS_HP_MULT * minuteBonus * (isFinal ? 2.0 : 1.0),
    );

    const boss: EnemyState = {
      container, x: bx, y: by,
      speed: this.currentEnemySpeed * BOSS_SPEED_MULT,
      isElite: false, isBoss: true, isFinalBoss: isFinal,
      scale: 2.5, hitRadius: BOSS_HIT_RADIUS,
      wobblePhase: Math.random() * Math.PI * 2,
      alive: true, hp: bossHP, maxHp: bossHP,
      behaviour: EnemyBehaviour.Chaser, aiTimer: 0,
    };

    this.enemies.push(boss);
    this.activeBoss = boss;
    this.totalSpawned++;

    this.onBossSpawn?.(isFinal);
  }

  // ─── Movement AI ────────────────────────────────────────────

  updateMovement(dt: number, playerX: number, playerY: number, camX: number, camY: number): void {
    const cullMargin = 200;
    const camLeft = camX - cullMargin;
    const camRight = camX + this.cfg.vpWidth + cullMargin;
    const camTop = camY - cullMargin;
    const camBottom = camY + this.cfg.vpHeight + cullMargin;

    for (const enemy of this.enemies) {
      if (!enemy.alive) continue;

      const dx = playerX - enemy.x;
      const dy = playerY - enemy.y;
      const d = Math.sqrt(dx * dx + dy * dy);

      if (d > 1) {
        enemy.aiTimer += dt;

        let moveX = dx / d;
        let moveY = dy / d;
        let spd = enemy.speed;

        switch (enemy.behaviour) {
          case EnemyBehaviour.Flanker: {
            // Approach at an angle
            const flankAngle = Math.atan2(dy, dx) + Math.PI * 0.3 * Math.sin(enemy.aiTimer * 2);
            moveX = Math.cos(flankAngle);
            moveY = Math.sin(flankAngle);
            spd *= 1.15;
            break;
          }
          case EnemyBehaviour.Tank:
            spd *= 0.6;
            break;
          case EnemyBehaviour.Sprinter: {
            // Periodic dash
            const dashCycle = enemy.aiTimer % 3.0;
            if (dashCycle < 0.3) spd *= 2.5;
            else if (dashCycle < 1.0) spd *= 0.5;
            break;
          }
          default: {
            // Chaser with wobble
            enemy.wobblePhase += dt * 3;
            const wobbleX = Math.sin(enemy.wobblePhase) * 15;
            const wobbleY = Math.cos(enemy.wobblePhase * 0.7) * 15;
            moveX = dx / d + wobbleX / d;
            moveY = dy / d + wobbleY / d;
            break;
          }
        }

        enemy.x += moveX * spd * dt;
        enemy.y += moveY * spd * dt;
        enemy.x = clamp(enemy.x, 0, this.cfg.mapWidth);
        enemy.y = clamp(enemy.y, 0, this.cfg.mapHeight);
        enemy.container.position.set(enemy.x, enemy.y);
        enemy.container.scale.x = dx > 0 ? Math.abs(enemy.container.scale.x) : -Math.abs(enemy.container.scale.x);
      }

      // Viewport culling
      enemy.container.visible =
        enemy.x >= camLeft && enemy.x <= camRight &&
        enemy.y >= camTop && enemy.y <= camBottom;
    }
  }

  // ─── Spatial Grid ───────────────────────────────────────────

  rebuildGrid(): void {
    this.spatialGrid.clear();
    for (const e of this.enemies) {
      if (e.alive) this.spatialGrid.insert(e);
    }
  }

  // ─── Player Collision ───────────────────────────────────────

  /** Returns damage dealt to player (0 = no hit). */
  checkPlayerCollision(playerX: number, playerY: number, invulnerable: boolean): { damage: number; knockbackAngle: number } | null {
    if (invulnerable) return null;

    const nearby = this.spatialGrid.query(playerX, playerY);
    for (const enemy of nearby) {
      if (!enemy.alive) continue;
      const d = dist({ x: playerX, y: playerY }, { x: enemy.x, y: enemy.y });
      if (d < PLAYER_RADIUS + enemy.hitRadius) {
        const dmg = enemy.isBoss ? BOSS_DAMAGE : (enemy.isElite ? ELITE_DAMAGE : ENEMY_DAMAGE);
        const angle = Math.atan2(playerY - enemy.y, playerX - enemy.x);
        return { damage: dmg, knockbackAngle: angle };
      }
    }
    return null;
  }

  // ─── Enemy Damage (from weapons) ────────────────────────────

  /** Apply damage to an enemy. Returns true if the enemy died. */
  damageEnemy(enemy: EnemyState, damage: number, coinDropMult: number): boolean {
    enemy.hp -= damage;
    enemy.container.alpha = 0.5;
    setTimeout(() => { if (enemy.container) enemy.container.alpha = 1; }, 80);

    if (enemy.hp <= 0) {
      enemy.alive = false;
      this.particles.spawnBurst(enemy.x, enemy.y, enemy.isBoss ? 0xff6600 : (enemy.isElite ? 0xffff00 : 0xff4444));

      const coinCount = enemy.isBoss ? coinDropMult * 5 : coinDropMult;
      for (let c = 0; c < coinCount; c++) {
        const ox = c === 0 ? 0 : (Math.random() - 0.5) * 30;
        const oy = c === 0 ? 0 : (Math.random() - 0.5) * 30;
        this.onSpawnOrb?.(enemy.x + ox, enemy.y + oy);
      }

      this._releaseContainer(enemy.container);
      const idx = this.enemies.indexOf(enemy);
      if (idx >= 0) this.enemies.splice(idx, 1);
      this.enemiesKilled++;
      this.audio.playSFX('hit');

      if (enemy.isBoss) {
        this.bossesKilled++;
        this.activeBoss = null;
        this.onBossDeath?.(!!enemy.isFinalBoss);
      }

      return true;
    }
    return false;
  }

  // ─── Container Pool ─────────────────────────────────────────

  private _getContainer(): PIXI.Container {
    if (this._containerPool.length > 0) {
      const c = this._containerPool.pop()!;
      c.visible = true;
      c.alpha = 1;
      return c;
    }
    return new PIXI.Container();
  }

  private _releaseContainer(container: PIXI.Container): void {
    if (!container) return;
    if (container.parent) container.parent.removeChild(container);
    while (container.children.length > 0) {
      const child = container.children[0];
      container.removeChild(child);
      try { child.destroy({ children: false, texture: false }); } catch { /* noop */ }
    }
    container.visible = false;
    if (this._containerPool.length < 50) {
      this._containerPool.push(container);
    } else {
      try { container.destroy({ children: true }); } catch { /* noop */ }
    }
  }

  // ─── Cleanup ────────────────────────────────────────────────

  cleanup(): void {
    for (const enemy of this.enemies) {
      if (enemy.container) this._releaseContainer(enemy.container);
    }
    this.enemies = [];
    for (const c of this._containerPool) {
      try { c.destroy({ children: true }); } catch { /* noop */ }
    }
    this._containerPool = [];
    this.activeBoss = null;
  }
}
