/**
 * Survive Mode — Projectile Manager.
 *
 * Manages all projectile types: straight, homing, boomerang arcs, AoE zones.
 * Uses object pools for PIXI.Graphics and spatial grid for collision detection.
 */
import * as PIXI from 'pixi.js';
import {
  PROJECTILE_SPEED, PROJECTILE_RADIUS, PROJECTILE_LIFETIME,
  ObjectPool, dist,
} from './types';
import type { Projectile, EnemyState } from './types';
import type { EnemyManager } from './EnemyManager';

export class ProjectileManager {
  projectiles: Projectile[] = [];

  private worldContainer: PIXI.Container;
  private pool: ObjectPool<PIXI.Graphics>;
  private mapWidth: number;
  private mapHeight: number;
  private enemyManager: EnemyManager;

  constructor(worldContainer: PIXI.Container, mapWidth: number, mapHeight: number, enemyManager: EnemyManager) {
    this.worldContainer = worldContainer;
    this.mapWidth = mapWidth;
    this.mapHeight = mapHeight;
    this.enemyManager = enemyManager;

    this.pool = new ObjectPool<PIXI.Graphics>(
      () => new PIXI.Graphics(),
      (gfx) => { gfx.clear(); gfx.alpha = 1; gfx.visible = false; },
      30,
    );
  }

  /** Fire a straight projectile toward a target direction. */
  fireProjectile(
    fromX: number, fromY: number,
    toX: number, toY: number,
    damage: number,
    color: number,
    radius = PROJECTILE_RADIUS,
    speedMult = 1.0,
    pierce = 0,
    aoeRadius = 0,
    lifetime = PROJECTILE_LIFETIME,
    weaponId?: string,
  ): void {
    const dx = toX - fromX;
    const dy = toY - fromY;
    const d = Math.sqrt(dx * dx + dy * dy);
    if (d < 1) return;

    const gfx = this._getGfx(color, radius);
    gfx.position.set(fromX, fromY);
    this.worldContainer.addChild(gfx);

    this.projectiles.push({
      gfx, x: fromX, y: fromY,
      vx: (dx / d) * PROJECTILE_SPEED * speedMult,
      vy: (dy / d) * PROJECTILE_SPEED * speedMult,
      damage, lifetime, age: 0,
      weaponId, pierceLeft: pierce, aoeRadius,
    });
  }

  /** Fire a boomerang projectile. */
  fireBoomerang(
    fromX: number, fromY: number,
    toX: number, toY: number,
    damage: number,
    color: number,
    radius: number,
    maxRange: number,
    pierce: number,
    speedMult = 1.0,
    weaponId?: string,
  ): void {
    const dx = toX - fromX;
    const dy = toY - fromY;
    const d = Math.sqrt(dx * dx + dy * dy);
    if (d < 1) return;

    const gfx = this._getGfx(color, radius);
    gfx.position.set(fromX, fromY);
    this.worldContainer.addChild(gfx);

    this.projectiles.push({
      gfx, x: fromX, y: fromY,
      vx: (dx / d) * PROJECTILE_SPEED * speedMult,
      vy: (dy / d) * PROJECTILE_SPEED * speedMult,
      damage, lifetime: 5.0, age: 0,
      weaponId, pierceLeft: pierce,
      returning: false, originX: fromX, originY: fromY, maxRange,
    });
  }

  /** Spawn a static AoE zone that damages enemies once. */
  spawnAoE(
    x: number, y: number,
    damage: number,
    radius: number,
    color: number,
    coinDropMult: number,
  ): void {
    // Draw the AoE circle (visual)
    const gfx = new PIXI.Graphics();
    gfx.circle(0, 0, radius);
    gfx.fill({ color, alpha: 0.3 });
    gfx.circle(0, 0, radius);
    gfx.setStrokeStyle({ width: 2, color, alpha: 0.6 });
    gfx.stroke();
    gfx.position.set(x, y);
    this.worldContainer.addChild(gfx);

    // Damage all enemies in range immediately
    for (let i = this.enemyManager.enemies.length - 1; i >= 0; i--) {
      const enemy = this.enemyManager.enemies[i];
      if (!enemy.alive) continue;
      const d = dist({ x, y }, { x: enemy.x, y: enemy.y });
      if (d < radius + enemy.hitRadius) {
        this.enemyManager.damageEnemy(enemy, damage, coinDropMult);
      }
    }

    // Fade out the AoE visual
    const startTime = performance.now();
    const animate = () => {
      const elapsed = (performance.now() - startTime) / 1000;
      if (elapsed >= 0.5) {
        try { this.worldContainer.removeChild(gfx); gfx.destroy(); } catch { /* noop */ }
        return;
      }
      gfx.alpha = 1 - elapsed / 0.5;
      requestAnimationFrame(animate);
    };
    requestAnimationFrame(animate);
  }

  update(dt: number, coinDropMult: number, playerX?: number, playerY?: number): void {
    for (let i = this.projectiles.length - 1; i >= 0; i--) {
      const proj = this.projectiles[i];
      proj.age += dt;

      // ─── Boomerang logic ─────────────────────────────────
      if (proj.originX !== undefined && proj.maxRange !== undefined) {
        const dFromOrigin = dist({ x: proj.x, y: proj.originX }, { x: proj.y, y: proj.originY! });
        if (!proj.returning && dFromOrigin >= proj.maxRange) {
          proj.returning = true;
        }
        if (proj.returning && playerX !== undefined && playerY !== undefined) {
          const dx = playerX - proj.x;
          const dy = playerY - proj.y;
          const d = Math.sqrt(dx * dx + dy * dy);
          if (d > 1) {
            const speed = Math.sqrt(proj.vx * proj.vx + proj.vy * proj.vy);
            proj.vx = (dx / d) * speed * 1.2;
            proj.vy = (dy / d) * speed * 1.2;
          }
          // Remove when it reaches the player
          if (d < PROJECTILE_RADIUS + 20) {
            this._release(proj);
            this.projectiles.splice(i, 1);
            continue;
          }
        }
      }

      // ─── Movement ────────────────────────────────────────
      proj.x += proj.vx * dt;
      proj.y += proj.vy * dt;
      proj.gfx.position.set(proj.x, proj.y);

      // ─── Lifetime / bounds check ────────────────────────
      if (
        proj.age >= proj.lifetime ||
        proj.x < -50 || proj.x > this.mapWidth + 50 ||
        proj.y < -50 || proj.y > this.mapHeight + 50
      ) {
        this._release(proj);
        this.projectiles.splice(i, 1);
        continue;
      }

      // ─── Enemy collision ────────────────────────────────
      for (let j = this.enemyManager.enemies.length - 1; j >= 0; j--) {
        const enemy = this.enemyManager.enemies[j];
        if (!enemy.alive) continue;
        if (proj.hitEnemies?.has(enemy)) continue;

        const d = dist({ x: proj.x, y: proj.y }, { x: enemy.x, y: enemy.y });
        const hitDist = (proj.gfx.width / 2 || PROJECTILE_RADIUS) + enemy.hitRadius;
        if (d < hitDist) {
          // AoE on impact
          if (proj.aoeRadius && proj.aoeRadius > 0) {
            this.spawnAoE(proj.x, proj.y, proj.damage, proj.aoeRadius, 0xff5522, coinDropMult);
            this._release(proj);
            this.projectiles.splice(i, 1);
            break;
          }

          const died = this.enemyManager.damageEnemy(enemy, proj.damage, coinDropMult);

          if (!proj.hitEnemies) proj.hitEnemies = new Set();
          proj.hitEnemies.add(enemy);

          // Pierce check
          if (proj.pierceLeft !== undefined && proj.pierceLeft !== -1) {
            if (proj.pierceLeft <= 0) {
              this._release(proj);
              this.projectiles.splice(i, 1);
              break;
            }
            proj.pierceLeft--;
          } else if (proj.pierceLeft === undefined) {
            // No pierce — remove on first hit
            this._release(proj);
            this.projectiles.splice(i, 1);
            break;
          }
          // pierceLeft === -1 means infinite pierce (orbitals)
        }
      }
    }
  }

  cleanup(): void {
    for (const proj of this.projectiles) {
      if (proj.gfx) {
        proj.gfx.visible = false;
        if (proj.gfx.parent) proj.gfx.parent.removeChild(proj.gfx);
      }
    }
    this.projectiles = [];
  }

  private _getGfx(color: number, radius: number): PIXI.Graphics {
    const gfx = this.pool.get();
    gfx.clear();
    gfx.circle(0, 0, radius);
    gfx.fill(color);
    gfx.circle(0, 0, radius + 2);
    gfx.fill({ color, alpha: 0.3 });
    gfx.visible = true;
    gfx.alpha = 1;
    return gfx;
  }

  private _release(proj: Projectile): void {
    proj.gfx.visible = false;
    if (proj.gfx.parent) proj.gfx.parent.removeChild(proj.gfx);
    this.pool.release(proj.gfx);
  }
}
