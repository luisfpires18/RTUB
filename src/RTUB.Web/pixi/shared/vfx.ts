/**
 * Shared visual effects for Arena and Stage battle scenes.
 * Includes spell VFX, floating text, screen shake, buff particles.
 */
import type { Container, Text } from 'pixi.js';
import { animateTo } from './tween';
import { getPooledText, releaseText, type TextPool } from './text-pool';
import { formatNum } from './utils';

// ─── Owner interface (the scene instance) ─────────────────────
export interface VfxOwner {
  stage: Container | null;
  app: { screen: { width: number; height: number } } | null;
  battleSpeed: number;
  _rafIds: number[];
  _timeoutIds: number[];
  _textPool: TextPool;
}

// ─── Status Effect Constants ──────────────────────────────────
const STATUS_LABELS: Record<string, string> = {
  sleep: '💤 SLEEP',
  bleed: '🩸 BLEED',
  slow: '🐌 SLOW',
  vulnerable: '⚡ VULN',
  powerboost: '💪 POWER UP',
  haste: '⚡ HASTE',
  shield: '🛡️ SHIELD',
  defensebreak: '💥 DEF BREAK',
  defenseboost: '🛡️ DEF UP',
  regen: '💚 REGEN',
};

const STATUS_COLORS: Record<string, number> = {
  sleep: 0x9966ff,
  bleed: 0xff3333,
  slow: 0x6699cc,
  vulnerable: 0xffaa00,
  powerboost: 0xff6600,
  haste: 0x00ff88,
  shield: 0x4488ff,
  defensebreak: 0xff4444,
  defenseboost: 0x4488ff,
  regen: 0x44ff44,
};

// ─── Floating Text ────────────────────────────────────────────

/** Show floating text that drifts up and fades. */
export function showFloatingText(
  owner: VfxOwner,
  text: string,
  x: number,
  y: number,
  color: number,
): void {
  if (!owner.stage || !owner.app) return;
  const floatText = getPooledText(owner._textPool, text, {
    fontFamily: 'Arial',
    fontSize: 26,
    fontWeight: 'bold',
    fill: color,
    stroke: { color: 0x000000, width: 4 },
  });
  if (!floatText) return;
  floatText.anchor.set(0.5);
  floatText.x = x;
  floatText.y = y;
  owner.stage.addChild(floatText);
  animateTo(owner, floatText, { y: floatText.y - 70, alpha: 0 }, 900, () => {
    releaseText(owner._textPool, floatText);
  });
}

/** Show damage number floating above a position. */
export function showDamageText(
  owner: VfxOwner,
  damage: number,
  isCritical: boolean,
  x: number,
  y: number,
): void {
  if (!owner.stage || !owner.app) return;
  const text = isCritical
    ? `CRIT! -${formatNum(Math.abs(damage))}`
    : `-${formatNum(Math.abs(damage))}`;
  const fontSize = isCritical ? 28 : 24;
  const fillColor = isCritical ? 0xffff00 : 0xff4444;
  const strokeWidth = isCritical ? 4 : 3;
  const floatDistance = isCritical ? 80 : 60;
  const duration = isCritical ? 1000 : 800;

  const damageText = getPooledText(owner._textPool, text, {
    fontFamily: 'Arial',
    fontSize,
    fontWeight: 'bold',
    fill: fillColor,
    stroke: { color: 0x000000, width: strokeWidth },
  });
  if (!damageText) return;
  damageText.anchor.set(0.5);
  damageText.x = x;
  damageText.y = y;
  owner.stage.addChild(damageText);
  animateTo(owner, damageText, { y: damageText.y - floatDistance, alpha: 0 }, duration, () => {
    releaseText(owner._textPool, damageText);
  });
}

/** Show a status effect label floating above a sprite. */
export function showEffectLabel(
  owner: VfxOwner,
  effectName: string,
  sprite: Container,
): void {
  const label = STATUS_LABELS[effectName] || effectName.toUpperCase();
  const color = STATUS_COLORS[effectName] || 0xffffff;
  showFloatingText(
    owner,
    label,
    sprite.x,
    sprite.y - ((sprite as unknown as { height?: number }).height || 40) - 30,
    color,
  );
}

// ─── Screen Shake ─────────────────────────────────────────────

/** Brief screen shake effect. */
export function screenShake(owner: VfxOwner): void {
  if (!owner.stage) return;
  const intensity = 4;
  const originalX = owner.stage.x;
  const originalY = owner.stage.y;
  let count = 0;
  const shake = (): void => {
    if (count >= 6 || !owner.stage) {
      if (owner.stage) {
        owner.stage.x = originalX;
        owner.stage.y = originalY;
      }
      return;
    }
    owner.stage.x = originalX + (Math.random() - 0.5) * intensity * 2;
    owner.stage.y = originalY + (Math.random() - 0.5) * intensity * 2;
    count++;
    const id = setTimeout(shake, 30) as unknown as number;
    owner._timeoutIds.push(id);
  };
  shake();
}

// ─── Spell VFX ────────────────────────────────────────────────

/** Play spell VFX based on type enum. */
export function playSpellVfx(
  owner: VfxOwner,
  vfxType: number | undefined,
  color: number,
  source: Container,
  target: Container,
): void {
  if (!source || !target || !owner.stage) return;
  const srcX = source.x;
  const srcY = source.y - ((source as unknown as { height?: number }).height || 40) / 2;
  const tgtX = target.x;
  const tgtY = target.y - ((target as unknown as { height?: number }).height || 40) / 2;

  switch (vfxType) {
    case 0:
      createProjectileVfx(owner, color, srcX, srcY, tgtX, tgtY);
      break;
    case 1:
      createBeamVfx(owner, color, tgtX, tgtY);
      break;
    case 2:
      createAoeVfx(owner, color, tgtX, tgtY);
      break;
    case 4:
      createMeleeStrikeVfx(owner, color, tgtX, tgtY);
      break;
    case 5:
      createSoundWaveVfx(owner, color, srcX, srcY, tgtX, tgtY);
      break;
    case 6:
      createMusicNotesVfx(owner, color, srcX, srcY, tgtX, tgtY);
      break;
    default:
      createProjectileVfx(owner, color, srcX, srcY, tgtX, tgtY);
  }
}

function createProjectileVfx(
  owner: VfxOwner,
  color: number,
  srcX: number,
  srcY: number,
  tgtX: number,
  tgtY: number,
): void {
  if (!owner.stage) return;
  const proj = new PIXI.Graphics();
  proj.circle(0, 0, 8);
  proj.fill({ color, alpha: 0.9 });
  proj.x = srcX;
  proj.y = srcY;
  owner.stage.addChild(proj);

  const glow = new PIXI.Graphics();
  glow.circle(0, 0, 14);
  glow.fill({ color, alpha: 0.3 });
  glow.x = srcX;
  glow.y = srcY;
  owner.stage.addChild(glow);

  const duration = 350 / owner.battleSpeed;
  const startTime = Date.now();
  const animate = (): void => {
    const t = Math.min((Date.now() - startTime) / duration, 1);
    proj.x = srcX + (tgtX - srcX) * t;
    proj.y = srcY + (tgtY - srcY) * t;
    glow.x = proj.x;
    glow.y = proj.y;
    glow.alpha = 0.3 * (1 - t * 0.5);
    if (t < 1) {
      requestAnimationFrame(animate);
    } else {
      const flash = new PIXI.Graphics();
      flash.circle(0, 0, 20);
      flash.fill({ color, alpha: 0.8 });
      flash.x = tgtX;
      flash.y = tgtY;
      owner.stage!.addChild(flash);
      animateTo(owner, flash, { alpha: 0, scale: 2 }, 200, () => {
        if (flash.parent) flash.parent.removeChild(flash);
        flash.destroy();
      });
      if (proj.parent) proj.parent.removeChild(proj);
      proj.destroy();
      if (glow.parent) glow.parent.removeChild(glow);
      glow.destroy();
    }
  };
  requestAnimationFrame(animate);
}

function createBeamVfx(
  owner: VfxOwner,
  color: number,
  tgtX: number,
  tgtY: number,
): void {
  if (!owner.stage) return;
  const beam = new PIXI.Graphics();
  beam.rect(-4, -200, 8, 200);
  beam.fill({ color, alpha: 0.8 });
  beam.x = tgtX;
  beam.y = tgtY;
  beam.alpha = 0;
  owner.stage.addChild(beam);
  animateTo(owner, beam, { alpha: 1 }, 100, () => {
    animateTo(owner, beam, { alpha: 0 }, 400, () => {
      if (beam.parent) beam.parent.removeChild(beam);
      beam.destroy();
    });
  });
}

function createAoeVfx(
  owner: VfxOwner,
  color: number,
  tgtX: number,
  tgtY: number,
): void {
  if (!owner.stage) return;
  const ring = new PIXI.Graphics();
  ring.circle(0, 0, 10);
  ring.stroke({ color, width: 3, alpha: 0.9 });
  ring.x = tgtX;
  ring.y = tgtY;
  owner.stage.addChild(ring);
  animateTo(owner, ring, { scale: 6, alpha: 0 }, 500, () => {
    if (ring.parent) ring.parent.removeChild(ring);
    ring.destroy();
  });
}

function createMeleeStrikeVfx(
  owner: VfxOwner,
  color: number,
  tgtX: number,
  tgtY: number,
): void {
  if (!owner.stage) return;
  const slash = new PIXI.Graphics();
  slash.moveTo(-15, -15);
  slash.lineTo(15, 15);
  slash.moveTo(15, -15);
  slash.lineTo(-15, 15);
  slash.stroke({ color, width: 4, alpha: 0.9 });
  slash.x = tgtX;
  slash.y = tgtY;
  owner.stage.addChild(slash);
  animateTo(owner, slash, { alpha: 0, scale: 2 }, 350, () => {
    if (slash.parent) slash.parent.removeChild(slash);
    slash.destroy();
  });
}

function createSoundWaveVfx(
  owner: VfxOwner,
  color: number,
  srcX: number,
  srcY: number,
  tgtX: number,
  tgtY: number,
): void {
  if (!owner.stage) return;
  const midX = (srcX + tgtX) / 2;
  const midY = (srcY + tgtY) / 2;
  for (let i = 0; i < 3; i++) {
    const ring = new PIXI.Graphics();
    ring.circle(0, 0, 12);
    ring.stroke({ color, width: 3, alpha: 0.8 });
    ring.x = midX;
    ring.y = midY;
    ring.scale.set(0.3);
    ring.alpha = 0;
    owner.stage.addChild(ring);
    const delay = (i * 120) / owner.battleSpeed;
    const capturedI = i;
    const id = setTimeout(() => {
      ring.alpha = 0.8;
      animateTo(owner, ring, { alpha: 0, scale: 3 + capturedI }, 500 / owner.battleSpeed, () => {
        if (ring.parent) ring.parent.removeChild(ring);
        ring.destroy();
      });
    }, delay) as unknown as number;
    owner._timeoutIds.push(id);
  }
}

function createMusicNotesVfx(
  owner: VfxOwner,
  color: number,
  srcX: number,
  srcY: number,
  tgtX: number,
  tgtY: number,
): void {
  if (!owner.stage) return;
  const notes = ['♪', '♫', '♩', '♬'];
  for (let i = 0; i < 5; i++) {
    const note = new PIXI.Text({
      text: notes[i % notes.length],
      style: { fontSize: 18 + Math.random() * 8, fill: color, fontFamily: 'serif' },
    });
    note.anchor.set(0.5);
    note.x = srcX + (Math.random() - 0.5) * 30;
    note.y = srcY + (Math.random() - 0.5) * 20;
    note.alpha = 0;
    owner.stage.addChild(note);
    const delay = (i * 80) / owner.battleSpeed;
    const endX = tgtX + (Math.random() - 0.5) * 40;
    const endY = tgtY - 20 + (Math.random() - 0.5) * 30;
    const noteStartX = note.x;
    const noteStartY = note.y;
    const id = setTimeout(() => {
      note.alpha = 1;
      const duration = 450 / owner.battleSpeed;
      const startTime = Date.now();
      const animateNote = (): void => {
        const t = Math.min((Date.now() - startTime) / duration, 1);
        note.x = noteStartX + (endX - noteStartX) * t;
        note.y = noteStartY + (endY - noteStartY) * t - Math.sin(t * Math.PI) * 20;
        note.alpha = 1 - t * 0.6;
        note.rotation = Math.sin(t * Math.PI * 2) * 0.3;
        if (t < 1) {
          requestAnimationFrame(animateNote);
        } else {
          if (note.parent) note.parent.removeChild(note);
          note.destroy();
        }
      };
      animateNote();
    }, delay) as unknown as number;
    owner._timeoutIds.push(id);
  }
}

/** Play upward buff/heal particles on a target sprite. */
export function playBuffVfx(
  owner: VfxOwner,
  target: Container,
  color: number,
): void {
  if (!target || !owner.stage) return;
  const cx = target.x;
  const cy = target.y - ((target as unknown as { height?: number }).height || 40) / 2;
  for (let i = 0; i < 8; i++) {
    const p = new PIXI.Graphics();
    p.circle(0, 0, 3);
    p.fill({ color, alpha: 0.8 });
    p.x = cx + (Math.random() - 0.5) * 30;
    p.y = cy + (Math.random() - 0.5) * 20;
    owner.stage.addChild(p);
    animateTo(
      owner,
      p,
      { y: p.y - 40 - Math.random() * 30, alpha: 0 },
      600 + Math.random() * 200,
      () => {
        if (p.parent) p.parent.removeChild(p);
        p.destroy();
      },
    );
  }
}
