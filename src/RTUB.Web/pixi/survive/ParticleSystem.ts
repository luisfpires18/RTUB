/**
 * Survive Mode — Particle System.
 *
 * Manages death burst particles, hit flashes, and floating damage numbers.
 * All particles use PIXI.Graphics from an object pool.
 */
import * as PIXI from 'pixi.js';
import { ObjectPool } from './types';
import type { Particle } from './types';

export class ParticleSystem {
  private particles: Particle[] = [];
  private pool: ObjectPool<PIXI.Graphics>;
  private worldContainer: PIXI.Container;

  constructor(worldContainer: PIXI.Container) {
    this.worldContainer = worldContainer;
    this.pool = new ObjectPool<PIXI.Graphics>(
      () => new PIXI.Graphics(),
      (gfx) => { gfx.clear(); gfx.alpha = 1; gfx.visible = false; },
      50,
    );
  }

  /** Spawn a radial burst of particles (e.g. on enemy death). */
  spawnBurst(x: number, y: number, color: number, count = 6): void {
    for (let i = 0; i < count; i++) {
      const angle = (Math.PI * 2 / count) * i + Math.random() * 0.5;
      const speed = 40 + Math.random() * 60;
      const gfx = this._getGfx(color);
      gfx.position.set(x, y);
      this.worldContainer.addChild(gfx);
      this.particles.push({
        gfx,
        vx: Math.cos(angle) * speed,
        vy: Math.sin(angle) * speed,
        lifetime: 0.5 + Math.random() * 0.3,
        age: 0,
      });
    }
  }

  /** Spawn a screen flash overlay (in UI space). */
  flashScreen(uiContainer: PIXI.Container, vpWidth: number, vpHeight: number, color: number, alpha = 0.35, durationMs = 300): void {
    const flash = new PIXI.Graphics();
    flash.rect(0, 0, vpWidth, vpHeight);
    flash.fill({ color, alpha });
    uiContainer.addChild(flash);
    setTimeout(() => {
      try { uiContainer.removeChild(flash); } catch { /* noop */ }
    }, durationMs);
  }

  /** Spawn a floating damage number in world space. */
  spawnDamageNumber(x: number, y: number, damage: number, color = 0xffffff): void {
    const text = new PIXI.Text({
      text: `-${damage}`,
      style: {
        fontFamily: 'Arial',
        fontSize: 14,
        fontWeight: 'bold',
        fill: color,
        stroke: { color: 0x000000, width: 2 },
      },
    });
    text.anchor.set(0.5, 0.5);
    text.position.set(x + (Math.random() - 0.5) * 20, y - 10);
    this.worldContainer.addChild(text);

    const lifetime = 0.8;
    const startY = text.position.y;
    const startTime = performance.now();
    const animate = () => {
      const elapsed = (performance.now() - startTime) / 1000;
      if (elapsed >= lifetime) {
        try { this.worldContainer.removeChild(text); text.destroy(); } catch { /* noop */ }
        return;
      }
      const t = elapsed / lifetime;
      text.position.y = startY - t * 30;
      text.alpha = 1 - t;
      requestAnimationFrame(animate);
    };
    requestAnimationFrame(animate);
  }

  update(dt: number): void {
    for (let i = this.particles.length - 1; i >= 0; i--) {
      const p = this.particles[i];
      p.age += dt;
      p.gfx.position.x += p.vx * dt;
      p.gfx.position.y += p.vy * dt;
      p.gfx.alpha = 1 - (p.age / p.lifetime);

      if (p.age >= p.lifetime) {
        this._releaseGfx(p.gfx);
        this.particles.splice(i, 1);
      }
    }
  }

  cleanup(): void {
    for (const p of this.particles) {
      p.gfx.visible = false;
      if (p.gfx.parent) p.gfx.parent.removeChild(p.gfx);
    }
    this.particles = [];
  }

  private _getGfx(color: number): PIXI.Graphics {
    const gfx = this.pool.get();
    gfx.clear();
    gfx.circle(0, 0, 2 + Math.random() * 3);
    gfx.fill(color);
    gfx.visible = true;
    gfx.alpha = 1;
    return gfx;
  }

  private _releaseGfx(gfx: PIXI.Graphics): void {
    gfx.visible = false;
    if (gfx.parent) gfx.parent.removeChild(gfx);
    this.pool.release(gfx);
  }
}
