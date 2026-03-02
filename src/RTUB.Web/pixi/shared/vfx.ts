/**
 * Shared visual effects for Arena and Stage battle scenes.
 * Includes floating text, screen shake, buff particles.
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

/**
 * Minimum visible duration (ms) for floating/damage text after speed-scaling.
 * At high speeds (10x/20x) the base duration ÷ speed can shrink to <20ms (invisible).
 * We compensate by passing a pre-inflated duration so animateTo still produces
 * at least MIN_FLOAT_MS of on-screen time.
 */
const MIN_FLOAT_MS = 350;

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
  // Compensate so visible duration ≥ MIN_FLOAT_MS after animateTo divides by speed
  const speed = owner.battleSpeed || 1;
  const compensated = Math.max(900, MIN_FLOAT_MS * speed);
  animateTo(owner, floatText, { y: floatText.y - 70, alpha: 0 }, compensated, () => {
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
  // Slight random X-offset so overlapping texts at high speeds don't pile up
  damageText.x = x + (Math.random() - 0.5) * 30;
  damageText.y = y;
  owner.stage.addChild(damageText);
  // Compensate so visible duration ≥ MIN_FLOAT_MS after animateTo divides by speed
  const speed = owner.battleSpeed || 1;
  const compensated = Math.max(duration, MIN_FLOAT_MS * speed);
  animateTo(owner, damageText, { y: damageText.y - floatDistance, alpha: 0 }, compensated, () => {
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
