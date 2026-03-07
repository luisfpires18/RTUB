/**
 * Survive Mode — Weapon System.
 *
 * Manages active weapon instances, fires attacks per weapon pattern,
 * handles orbital/companion/lightning/forcefield weapon types.
 * Max 6 weapon slots.
 */
import * as PIXI from 'pixi.js';
import {
  AttackPattern, PROJECTILE_RADIUS, PROJECTILE_SPEED,
  PLAYER_RADIUS, dist, clamp, lerp,
} from './types';
import type { WeaponDef, WeaponInstance, WeaponLevelStats, PlayerStats, EnemyState, Companion } from './types';
import { MAX_WEAPON_SLOTS, MAX_WEAPON_LEVEL } from './weapons/WeaponDefs';
import type { ProjectileManager } from './ProjectileManager';
import type { EnemyManager } from './EnemyManager';
import type { ParticleSystem } from './ParticleSystem';

export class WeaponSystem {
  /** Active weapons (max MAX_WEAPON_SLOTS). */
  weapons: WeaponInstance[] = [];

  /** Active companion entities. */
  companions: Companion[] = [];

  // ─── Orbital visuals ────────────────────────────────────────
  private orbitalContainers: Map<string, PIXI.Graphics[]> = new Map();
  private orbitalAngle = 0;

  // ─── ForceField visual ──────────────────────────────────────
  private forceFieldGfx: PIXI.Graphics | null = null;
  private forceFieldTimer = 0;

  // ─── Dependencies ───────────────────────────────────────────
  private projectiles: ProjectileManager;
  private enemyManager: EnemyManager;
  private particles: ParticleSystem;
  private worldContainer: PIXI.Container;
  private mapWidth: number;
  private mapHeight: number;

  constructor(
    projectiles: ProjectileManager,
    enemyManager: EnemyManager,
    particles: ParticleSystem,
    worldContainer: PIXI.Container,
    mapWidth: number,
    mapHeight: number,
  ) {
    this.projectiles = projectiles;
    this.enemyManager = enemyManager;
    this.particles = particles;
    this.worldContainer = worldContainer;
    this.mapWidth = mapWidth;
    this.mapHeight = mapHeight;
  }

  get weaponCount(): number { return this.weapons.length; }
  get isFull(): boolean { return this.weapons.length >= MAX_WEAPON_SLOTS; }

  /** Add a new weapon at level 1. */
  addWeapon(def: WeaponDef): WeaponInstance | null {
    if (this.isFull) return null;
    if (this.weapons.some(w => w.def.id === def.id)) return null; // already owned

    const instance: WeaponInstance = {
      def,
      level: 1,
      cooldownTimer: 0,
      state: {},
    };
    this.weapons.push(instance);

    // Special init for companion — spawn pig
    if (def.pattern === AttackPattern.Companion) {
      this._spawnCompanionPig(instance);
    }

    // Special init for orbital — create blades
    if (def.pattern === AttackPattern.Orbital) {
      this._createOrbitalVisuals(instance);
    }

    return instance;
  }

  /** Level up a weapon. Returns false if already max. */
  levelUpWeapon(weaponId: string): boolean {
    const weapon = this.weapons.find(w => w.def.id === weaponId);
    if (!weapon || weapon.level >= MAX_WEAPON_LEVEL) return false;
    weapon.level++;

    // Re-create orbital visuals if count changed
    if (weapon.def.pattern === AttackPattern.Orbital) {
      this._destroyOrbitalVisuals(weapon.def.id);
      this._createOrbitalVisuals(weapon);
    }

    // Spawn additional companions if count increased
    if (weapon.def.pattern === AttackPattern.Companion) {
      const stats = this._getStats(weapon);
      while (this.companions.length < stats.count) {
        this._spawnCompanionPig(weapon);
      }
    }

    return true;
  }

  getWeapon(id: string): WeaponInstance | undefined {
    return this.weapons.find(w => w.def.id === id);
  }

  /**
   * Evolve two weapons into one. Removes both ingredients and adds the evolved weapon.
   * The evolved weapon occupies one slot (net -1 slot), freeing space.
   * Returns the new evolved instance, or null if ingredients not found.
   */
  evolveWeapons(ingredientAId: string, ingredientBId: string, evolvedDef: WeaponDef): WeaponInstance | null {
    const idxA = this.weapons.findIndex(w => w.def.id === ingredientAId);
    const idxB = this.weapons.findIndex(w => w.def.id === ingredientBId);
    if (idxA < 0 || idxB < 0) return null;

    // Clean up visuals for removed weapons
    this._cleanupWeaponVisuals(ingredientAId);
    this._cleanupWeaponVisuals(ingredientBId);

    // Remove both (remove higher index first to avoid shift issues)
    const toRemove = [idxA, idxB].sort((a, b) => b - a);
    for (const idx of toRemove) this.weapons.splice(idx, 1);

    // Add evolved weapon at level 1 (evolved weapons have only 1 level)
    const instance: WeaponInstance = {
      def: evolvedDef,
      level: 1,
      cooldownTimer: 0,
      state: {},
    };
    this.weapons.push(instance);

    // Init visuals for the new weapon
    if (evolvedDef.pattern === AttackPattern.Companion) {
      const stats = this._getStats(instance);
      for (let i = 0; i < stats.count; i++) {
        this._spawnCompanionPig(instance);
      }
    }
    if (evolvedDef.pattern === AttackPattern.Orbital) {
      this._createOrbitalVisuals(instance);
    }

    return instance;
  }

  /** Remove visuals associated with a weapon being destroyed. */
  private _cleanupWeaponVisuals(weaponId: string): void {
    const weapon = this.weapons.find(w => w.def.id === weaponId);
    if (!weapon) return;

    if (weapon.def.pattern === AttackPattern.Orbital) {
      this._destroyOrbitalVisuals(weaponId);
    }
    if (weapon.def.pattern === AttackPattern.Companion) {
      for (const comp of this.companions) {
        if (comp.container.parent) comp.container.parent.removeChild(comp.container);
        try { comp.container.destroy({ children: true }); } catch { /* noop */ }
      }
      this.companions = [];
    }
    if (weapon.def.pattern === AttackPattern.ForceField && this.forceFieldGfx) {
      if (this.forceFieldGfx.parent) this.forceFieldGfx.parent.removeChild(this.forceFieldGfx);
      try { this.forceFieldGfx.destroy(); } catch { /* noop */ }
      this.forceFieldGfx = null;
    }
  }

  /** Main update — fires all weapon attacks. */
  update(dt: number, playerX: number, playerY: number, stats: PlayerStats): void {
    this.orbitalAngle += dt * 2.0;

    for (const weapon of this.weapons) {
      weapon.cooldownTimer -= dt;
      const wStats = this._getStats(weapon);
      const effectiveCooldown = wStats.cooldown * stats.cooldownMultiplier;
      const effectiveDamage = Math.ceil(wStats.damage * stats.damageMultiplier);
      const effectiveRange = wStats.range * stats.rangeMultiplier;

      if (weapon.cooldownTimer > 0 && weapon.def.pattern !== AttackPattern.Orbital && weapon.def.pattern !== AttackPattern.ForceField) {
        continue;
      }

      switch (weapon.def.pattern) {
        case AttackPattern.Projectile:
          this._fireProjectile(weapon, wStats, playerX, playerY, effectiveDamage, effectiveRange, effectiveCooldown);
          break;
        case AttackPattern.Orbital:
          this._updateOrbital(weapon, wStats, playerX, playerY, effectiveDamage, stats);
          break;
        case AttackPattern.AoE:
          this._fireAoE(weapon, wStats, playerX, playerY, effectiveDamage, effectiveCooldown, stats.coinDropMult);
          break;
        case AttackPattern.Boomerang:
          this._fireBoomerang(weapon, wStats, playerX, playerY, effectiveDamage, effectiveRange, effectiveCooldown);
          break;
        case AttackPattern.Lightning:
          this._fireLightning(weapon, wStats, playerX, playerY, effectiveDamage, effectiveRange, effectiveCooldown, stats.coinDropMult);
          break;
        case AttackPattern.ForceField:
          this._updateForceField(weapon, wStats, dt, playerX, playerY, effectiveDamage, effectiveCooldown, stats.coinDropMult);
          break;
        case AttackPattern.Companion:
          // Companions are updated separately below
          break;
      }
    }

    // Update companions
    this._updateCompanions(dt, playerX, playerY, stats);
  }

  // ─── Projectile Weapon ──────────────────────────────────────

  private _fireProjectile(
    weapon: WeaponInstance, wStats: WeaponLevelStats,
    px: number, py: number, damage: number, range: number, cooldown: number,
  ): void {
    if (weapon.cooldownTimer > 0) return;

    // Find targets within range
    const targets = this._findNearestEnemies(px, py, range, wStats.count);
    if (targets.length === 0) return;

    weapon.cooldownTimer = cooldown;

    // Fire Rain variant — targets random positions near enemies
    if (weapon.def.id === 'firerain') {
      for (const target of targets) {
        const ox = target.x + (Math.random() - 0.5) * 60;
        const oy = target.y + (Math.random() - 0.5) * 60;
        this.projectiles.fireProjectile(
          px, py - 30, // Fire from above player
          ox, oy,
          damage, weapon.def.color,
          weapon.def.projectileRadius ?? PROJECTILE_RADIUS,
          wStats.speedMult, wStats.pierce, wStats.aoeRadius,
          2.0, weapon.def.id,
        );
      }
      return;
    }

    // Standard projectile — fire at nearest enemies
    for (const target of targets) {
      this.projectiles.fireProjectile(
        px, py, target.x, target.y,
        damage, weapon.def.color,
        weapon.def.projectileRadius ?? PROJECTILE_RADIUS,
        wStats.speedMult, wStats.pierce, wStats.aoeRadius,
        undefined, weapon.def.id,
      );
    }
  }

  // ─── Orbital Weapon ─────────────────────────────────────────

  private _updateOrbital(
    weapon: WeaponInstance, wStats: WeaponLevelStats,
    px: number, py: number, damage: number, stats: PlayerStats,
  ): void {
    const blades = this.orbitalContainers.get(weapon.def.id);
    if (!blades) return;

    const orbitRadius = wStats.range;
    const angleStep = (Math.PI * 2) / blades.length;

    for (let i = 0; i < blades.length; i++) {
      const blade = blades[i];
      const angle = this.orbitalAngle + angleStep * i;
      const bx = px + Math.cos(angle) * orbitRadius;
      const by = py + Math.sin(angle) * orbitRadius;
      blade.position.set(bx, by);
      blade.rotation += 0.15;

      // Check collision with enemies
      const nearby = this.enemyManager.spatialGrid.query(bx, by);
      for (const enemy of nearby) {
        if (!enemy.alive) continue;
        const d = dist({ x: bx, y: by }, { x: enemy.x, y: enemy.y });
        if (d < 16 + enemy.hitRadius) {
          this.enemyManager.damageEnemy(enemy, damage, stats.coinDropMult);
        }
      }
    }

    // Evolved Guardian Blade: periodic AoE pulse at player position
    if (weapon.def.id === 'evo_guardianblade' && wStats.aoeRadius > 0) {
      const pulseKey = '__guardianPulseTimer';
      const timer = (weapon.state[pulseKey] as number ?? 0) + (1 / 60);
      weapon.state[pulseKey] = timer;
      if (timer >= 1.5) {
        weapon.state[pulseKey] = 0;
        this.projectiles.spawnAoE(px, py, Math.ceil(damage * 0.6), wStats.aoeRadius, weapon.def.color, stats.coinDropMult);
      }
    }
  }

  private _createOrbitalVisuals(weapon: WeaponInstance): void {
    const wStats = this._getStats(weapon);
    const blades: PIXI.Graphics[] = [];
    for (let i = 0; i < wStats.count; i++) {
      const gfx = new PIXI.Graphics();
      // Draw a blade shape
      gfx.moveTo(-12, 0);
      gfx.lineTo(0, -4);
      gfx.lineTo(12, 0);
      gfx.lineTo(0, 4);
      gfx.closePath();
      gfx.fill(weapon.def.color);
      gfx.setStrokeStyle({ width: 1, color: 0xffffff, alpha: 0.6 });
      gfx.stroke();
      this.worldContainer.addChild(gfx);
      blades.push(gfx);
    }
    this.orbitalContainers.set(weapon.def.id, blades);
  }

  private _destroyOrbitalVisuals(weaponId: string): void {
    const blades = this.orbitalContainers.get(weaponId);
    if (blades) {
      for (const b of blades) {
        if (b.parent) b.parent.removeChild(b);
        try { b.destroy(); } catch { /* noop */ }
      }
      this.orbitalContainers.delete(weaponId);
    }
  }

  // ─── AoE Weapon ─────────────────────────────────────────────

  private _fireAoE(
    weapon: WeaponInstance, wStats: WeaponLevelStats,
    px: number, py: number, damage: number, cooldown: number, coinDropMult: number,
  ): void {
    if (weapon.cooldownTimer > 0) return;
    weapon.cooldownTimer = cooldown;

    // Evolved Apocalypse: spawn multiple AoEs at enemy positions
    if (weapon.def.id === 'evo_apocalypse') {
      const targets = this._findNearestEnemies(px, py, wStats.range, wStats.count);
      if (targets.length > 0) {
        for (const t of targets) {
          const ox = t.x + (Math.random() - 0.5) * 40;
          const oy = t.y + (Math.random() - 0.5) * 40;
          this.projectiles.spawnAoE(ox, oy, damage, wStats.aoeRadius, weapon.def.color, coinDropMult);
        }
      } else {
        // No targets — drop around player
        for (let i = 0; i < wStats.count; i++) {
          const ox = px + (Math.random() - 0.5) * 200;
          const oy = py + (Math.random() - 0.5) * 200;
          this.projectiles.spawnAoE(ox, oy, damage, wStats.aoeRadius, weapon.def.color, coinDropMult);
        }
      }
      this.particles.flashScreen(
        this.worldContainer.parent?.children[1] as PIXI.Container ?? this.worldContainer,
        this.mapWidth, this.mapHeight, weapon.def.color, 0.25, 300,
      );
      return;
    }

    this.projectiles.spawnAoE(px, py, damage, wStats.aoeRadius, weapon.def.color, coinDropMult);
    this.particles.flashScreen(
      this.worldContainer.parent?.children[1] as PIXI.Container ?? this.worldContainer,
      this.mapWidth, this.mapHeight, weapon.def.color, 0.15, 200,
    );
  }

  // ─── Boomerang Weapon ───────────────────────────────────────

  private _fireBoomerang(
    weapon: WeaponInstance, wStats: WeaponLevelStats,
    px: number, py: number, damage: number, range: number, cooldown: number,
  ): void {
    if (weapon.cooldownTimer > 0) return;

    const targets = this._findNearestEnemies(px, py, range * 1.5, wStats.count);
    if (targets.length === 0) return;

    weapon.cooldownTimer = cooldown;

    for (const target of targets) {
      this.projectiles.fireBoomerang(
        px, py, target.x, target.y,
        damage, weapon.def.color,
        weapon.def.projectileRadius ?? 6,
        range, wStats.pierce, wStats.speedMult, weapon.def.id,
      );
    }
  }

  // ─── Lightning Weapon ───────────────────────────────────────

  private _fireLightning(
    weapon: WeaponInstance, wStats: WeaponLevelStats,
    px: number, py: number, damage: number, range: number, cooldown: number, coinDropMult: number,
  ): void {
    if (weapon.cooldownTimer > 0) return;

    // Find initial targets
    const initial = this._findNearestEnemies(px, py, range, wStats.count);
    if (initial.length === 0) return;

    weapon.cooldownTimer = cooldown;

    for (const target of initial) {
      this._chainLightning(px, py, target, damage, wStats.pierce, range * 0.6, coinDropMult, weapon.def.color);
    }
  }

  private _chainLightning(
    fromX: number, fromY: number, target: EnemyState,
    damage: number, chainsLeft: number, chainRange: number, coinDropMult: number, color: number,
  ): void {
    // Draw lightning bolt
    this._drawLightningBolt(fromX, fromY, target.x, target.y, color);

    // Damage target
    this.enemyManager.damageEnemy(target, damage, coinDropMult);

    // Chain to nearby enemies
    if (chainsLeft > 0) {
      const nearby = this.enemyManager.enemies
        .filter(e => e.alive && e !== target && dist({ x: target.x, y: target.y }, { x: e.x, y: e.y }) < chainRange)
        .sort((a, b) => dist({ x: target.x, y: target.y }, a) - dist({ x: target.x, y: target.y }, b));

      if (nearby.length > 0) {
        // Chain to nearest
        this._chainLightning(target.x, target.y, nearby[0], Math.ceil(damage * 0.8), chainsLeft - 1, chainRange, coinDropMult, color);
      }
    }
  }

  private _drawLightningBolt(fromX: number, fromY: number, toX: number, toY: number, color: number): void {
    const gfx = new PIXI.Graphics();
    gfx.setStrokeStyle({ width: 3, color, alpha: 0.9 });

    const segments = 6;
    const dx = (toX - fromX) / segments;
    const dy = (toY - fromY) / segments;

    gfx.moveTo(fromX, fromY);
    for (let i = 1; i < segments; i++) {
      const jitterX = (Math.random() - 0.5) * 20;
      const jitterY = (Math.random() - 0.5) * 20;
      gfx.lineTo(fromX + dx * i + jitterX, fromY + dy * i + jitterY);
    }
    gfx.lineTo(toX, toY);
    gfx.stroke();

    this.worldContainer.addChild(gfx);

    // Fade out
    const startTime = performance.now();
    const animate = () => {
      const elapsed = (performance.now() - startTime) / 1000;
      if (elapsed >= 0.3) {
        try { this.worldContainer.removeChild(gfx); gfx.destroy(); } catch { /* noop */ }
        return;
      }
      gfx.alpha = 1 - elapsed / 0.3;
      requestAnimationFrame(animate);
    };
    requestAnimationFrame(animate);
  }

  // ─── ForceField Weapon ──────────────────────────────────────

  private _updateForceField(
    weapon: WeaponInstance, wStats: WeaponLevelStats,
    dt: number, px: number, py: number, damage: number, cooldown: number, coinDropMult: number,
  ): void {
    const radius = wStats.aoeRadius;

    // Create or update visual
    if (!this.forceFieldGfx) {
      this.forceFieldGfx = new PIXI.Graphics();
      this.worldContainer.addChild(this.forceFieldGfx);
    }

    this.forceFieldGfx.clear();
    this.forceFieldGfx.circle(0, 0, radius);
    this.forceFieldGfx.fill({ color: weapon.def.color, alpha: 0.08 });
    this.forceFieldGfx.circle(0, 0, radius);
    this.forceFieldGfx.setStrokeStyle({ width: 2, color: weapon.def.color, alpha: 0.3 + Math.sin(this.forceFieldTimer) * 0.1 });
    this.forceFieldGfx.stroke();
    this.forceFieldGfx.position.set(px, py);

    this.forceFieldTimer += dt * 3;

    // Damage tick
    weapon.cooldownTimer -= dt;
    if (weapon.cooldownTimer <= 0) {
      weapon.cooldownTimer = cooldown;

      // Damage all enemies in radius
      for (let i = this.enemyManager.enemies.length - 1; i >= 0; i--) {
        const enemy = this.enemyManager.enemies[i];
        if (!enemy.alive) continue;
        const d = dist({ x: px, y: py }, { x: enemy.x, y: enemy.y });
        if (d < radius + enemy.hitRadius) {
          this.enemyManager.damageEnemy(enemy, damage, coinDropMult);
        }
      }
    }
  }

  // ─── Companion System ───────────────────────────────────────

  private _spawnCompanionPig(weapon: WeaponInstance): void {
    const idx = this.companions.length;
    const container = new PIXI.Container();
    const wStats = this._getStats(weapon);

    // Pig body
    const body = new PIXI.Graphics();
    body.circle(0, 0, 12);
    body.fill(0xffb6c1);
    body.setStrokeStyle({ width: 1.5, color: 0xff69b4 });
    body.stroke();
    container.addChild(body);

    // Snout
    const snout = new PIXI.Graphics();
    snout.ellipse(10, 0, 5, 4);
    snout.fill(0xff9999);
    snout.circle(12, -1.5, 1);
    snout.fill(0xcc6666);
    snout.circle(12, 1.5, 1);
    snout.fill(0xcc6666);
    container.addChild(snout);

    // Ears
    const ear = new PIXI.Graphics();
    ear.moveTo(-5, -10);
    ear.lineTo(0, -16);
    ear.lineTo(5, -10);
    ear.closePath();
    ear.fill(0xff8da1);
    container.addChild(ear);

    // Eyes
    const eyes = new PIXI.Graphics();
    eyes.circle(3, -4, 2);
    eyes.fill(0x222222);
    container.addChild(eyes);

    this.worldContainer.addChild(container);

    this.companions.push({
      container,
      x: 0, y: 0,
      orbitAngle: idx * (Math.PI * 2 / Math.max(this.companions.length + 1, 1)),
      attackCooldown: 0,
      attackRange: wStats.range,
      orbitRadius: 50 + idx * 20,
      damage: wStats.damage,
    });
  }

  private _updateCompanions(dt: number, px: number, py: number, stats: PlayerStats): void {
    const companionWeapon = this.weapons.find(w => w.def.pattern === AttackPattern.Companion);
    if (!companionWeapon || this.companions.length === 0) return;

    const wStats = this._getStats(companionWeapon);
    const effectiveDamage = Math.ceil(wStats.damage * stats.damageMultiplier);
    const effectiveCooldown = wStats.cooldown * stats.cooldownMultiplier;

    for (const comp of this.companions) {
      comp.orbitAngle += dt * 1.5;
      const targetX = px + Math.cos(comp.orbitAngle) * comp.orbitRadius;
      const targetY = py + Math.sin(comp.orbitAngle) * comp.orbitRadius;

      comp.x = lerp(comp.x, targetX, dt * 5);
      comp.y = lerp(comp.y, targetY, dt * 5);
      comp.container.position.set(comp.x, comp.y);

      const dxComp = targetX - comp.x;
      if (Math.abs(dxComp) > 0.5) {
        comp.container.scale.x = dxComp > 0 ? 1 : -1;
      }

      // Auto-attack
      comp.attackCooldown -= dt;
      if (comp.attackCooldown <= 0) {
        const targets = this._findNearestEnemies(comp.x, comp.y, wStats.range * stats.rangeMultiplier, 1);
        if (targets.length > 0) {
          comp.attackCooldown = effectiveCooldown;

          // Evolved Pig Boomerang: companions throw boomerangs
          if (companionWeapon.def.id === 'evo_pigrang') {
            this.projectiles.fireBoomerang(
              comp.x, comp.y, targets[0].x, targets[0].y,
              effectiveDamage, companionWeapon.def.color,
              companionWeapon.def.projectileRadius ?? 7,
              wStats.range * stats.rangeMultiplier,
              wStats.pierce, wStats.speedMult, companionWeapon.def.id,
            );
          } else {
            this.projectiles.fireProjectile(
              comp.x, comp.y, targets[0].x, targets[0].y,
              effectiveDamage, 0xff69b4,
              companionWeapon.def.projectileRadius ?? 3,
              wStats.speedMult, wStats.pierce, 0,
              undefined, companionWeapon.def.id,
            );
          }
        }
      }
    }
  }

  // ─── Helpers ────────────────────────────────────────────────

  private _getStats(weapon: WeaponInstance): WeaponLevelStats {
    return weapon.def.levels[weapon.level - 1];
  }

  private _findNearestEnemies(x: number, y: number, range: number, count: number): EnemyState[] {
    const enemies = this.enemyManager.enemies
      .filter(e => e.alive)
      .map(e => ({ enemy: e, dist: dist({ x, y }, { x: e.x, y: e.y }) }))
      .filter(e => e.dist < range + e.enemy.hitRadius)
      .sort((a, b) => a.dist - b.dist);

    return enemies.slice(0, count).map(e => e.enemy);
  }

  // ─── Cleanup ────────────────────────────────────────────────

  cleanup(): void {
    // Orbital visuals
    for (const [id] of this.orbitalContainers) {
      this._destroyOrbitalVisuals(id);
    }
    this.orbitalContainers.clear();

    // ForceField
    if (this.forceFieldGfx) {
      if (this.forceFieldGfx.parent) this.forceFieldGfx.parent.removeChild(this.forceFieldGfx);
      try { this.forceFieldGfx.destroy(); } catch { /* noop */ }
      this.forceFieldGfx = null;
    }

    // Companions
    for (const comp of this.companions) {
      if (comp.container.parent) comp.container.parent.removeChild(comp.container);
      try { comp.container.destroy({ children: true }); } catch { /* noop */ }
    }
    this.companions = [];

    this.weapons = [];
  }
}
